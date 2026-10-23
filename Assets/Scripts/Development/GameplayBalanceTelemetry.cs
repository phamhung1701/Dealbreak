using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Versioned observation journal. Never consumes gameplay RNG or restores gameplay state.</summary>
[Serializable]
public sealed class BalanceJournal
{
    public int schemaVersion = 2;
    public string id = Guid.NewGuid().ToString("N");
    public string seed, unityVersion, gameVersion, startedUtc, endedUtc;
    public string outcome = "in_progress";
    public bool truncated, developmentModified, invalid;
    // Continued from a save: resumed reopens the journal; partial means its start was not recorded.
    public bool resumed, partial;
    // Events recorded when the run save last matched the run; a continued run drops the rest.
    public int checkpointEvents = -1;
    public List<BalanceEvent> events = new();
    public const int EventLimit = 100000;

    public void Add(BalanceEvent entry)
    {
        if (events.Count >= EventLimit) { truncated = true; return; }
        events.Add(entry);
    }

    public string ToJson() => JsonUtility.ToJson(this, true);

    public string Export(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, id + ".json");
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, ToJson());
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
        return path;
    }
}

[Serializable]
public sealed class BalanceEvent
{
    public string kind, detail;
    public double seconds;
    public int encounter, map, node, a, b, c, d;
}

public static class GameplayBalanceTelemetry
{
    // On unless turned off (Tools/Dealbreak/Balance/Record New Runs); the test assemblies turn it off.
    // No assets, save schemas or balance configuration are changed, and no RNG is consumed.
    public static bool Enabled { get; set; } = true;
    public static BalanceJournal Current { get; private set; }
    public static string LastExportPath { get; private set; }
    /// <summary>Raised after a finished run has been rolled into the stats.</summary>
    public static event Action StatsUpdated;
    static int encounter;
    static bool encounterOpen;
    static double started;
    static int terminalFrame;
    internal static string ExportDirectoryOverride;
    public static string ExportDirectory => ExportDirectoryOverride ?? Path.Combine(Application.persistentDataPath, "BalanceTelemetry");

    public static string GearKey(string id) => "gear:" + id;
    public static string TrickKey(string id) => "trick:" + id;
    public static string ServiceKey(ShopOfferKind kind) => "service:" + kind;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        Current = null;
        encounter = 0;
        encounterOpen = false;
        LastExportPath = null;
    }

    public static void Begin(string seed)
    {
        Finish("restarted");
        if (!Enabled) { Current = null; return; }
        Open(NewJournal(seed));
        Record("run_start");
        var combat = CombatManager.Instance;
        if (combat != null) Health(combat.player.currentHealth, combat.player.maxHealth);
    }

    static BalanceJournal NewJournal(string seed) => new()
    {
        seed = seed, unityVersion = Application.unityVersion, gameVersion = Application.version,
        startedUtc = DateTime.UtcNow.ToString("O")
    };

    static void Open(BalanceJournal journal)
    {
        Current = journal;
        int last = journal.events.Count - 1;
        started = Time.timeAsDouble - (last >= 0 ? journal.events[last].seconds : 0);
        encounter = 0;
        foreach (var entry in journal.events) encounter = Math.Max(encounter, entry.encounter);
        encounterOpen = false;
    }

    /// <summary>
    /// A continued run reopens the journal it left (the latest interrupted one with its seed), cut back to
    /// what the save holds; without one it starts a partial journal.
    /// </summary>
    public static void Resume(string seed)
    {
        Finish("restarted");
        if (!Enabled) { Current = null; return; }
        var journal = BalanceStatsStore.LoadInterruptedJournal(ExportDirectory, seed);
        if (journal != null && journal.checkpointEvents >= 0)
        {
            if (journal.events.Count > journal.checkpointEvents)
                journal.events.RemoveRange(journal.checkpointEvents, journal.events.Count - journal.checkpointEvents);
            journal.outcome = "in_progress";
            journal.endedUtc = null;
            journal.resumed = true;
        }
        else journal = new BalanceJournal { seed = seed, unityVersion = Application.unityVersion,
            gameVersion = Application.version, startedUtc = DateTime.UtcNow.ToString("O"), partial = true, resumed = true };
        Open(journal);
        Record("resumed");
    }

    /// <summary>The run save now matches the run: a continued run keeps the events recorded so far.</summary>
    public static void Checkpointed()
    {
        if (Current != null && Current.outcome == "in_progress") Current.checkpointEvents = Current.events.Count;
    }

    public static void Record(string kind, string detail = "", int a = 0, int b = 0, int c = 0, int d = 0)
    {
        if (Current == null || Current.outcome != "in_progress") return;
        Current.developmentModified |= DevModeRuntime.Enabled;
        var run = RunManager.Instance;
        Current.Add(new BalanceEvent { kind = kind, detail = detail, a = a, b = b, c = c, d = d,
            seconds = Time.timeAsDouble - started, encounter = encounterOpen ? encounter : 0,
            map = run != null ? run.CurrentMapIndex : -1, node = run?.ActiveNode?.id ?? -1 });
    }

    public static void EncounterStarted(IReadOnlyList<EnemyRuntime> enemies, MapNodeType kind)
    {
        if (Current == null || Current.outcome != "in_progress") return;
        encounter++;
        encounterOpen = true;
        Record("encounter_start", kind.ToString());
        foreach (var enemy in enemies)
            Record("enemy", enemy.type != null ? enemy.type.enemyName : enemy.DisplayName, enemy.currentHp, enemy.EffectiveAttack);
        var cards = CardManager.Instance;
        if (cards != null)
            foreach (var gear in cards.ownedGear)
                if (gear != null) Record("gear_exposure", string.IsNullOrEmpty(gear.canonicalId) ? gear.name : gear.canonicalId);
    }

    public static void Invalidate(string reason)
    {
        if (Current == null || Current.outcome != "in_progress") return;
        Current.invalid = true;
        Record("invalid", reason);
    }

    public static void Damage(DamageResult result)
    {
        bool player = result.Request.Target is PlayerRuntime;
        Record(player ? "damage_received" : "damage_dealt", result.Request.Origin.ToString(),
            result.ActualHpLost, result.RequestedDamage, result.ShieldBlockedDamage, result.ModifierPreventedDamage);
        if (player && result.WasLethal) Record("lethal", result.Request.Origin.ToString());
    }

    public static void TrickUsed(string id, bool wasInEncounter)
    {
        if (Current == null) return;
        // A lethal Trick can finish the encounter/run before its charge is committed.
        if (Current.outcome != "in_progress" &&
            (!wasInEncounter || Time.frameCount != terminalFrame ||
             Current.outcome != "completed" && Current.outcome != "defeat")) return;
        var run = RunManager.Instance;
        Current.Add(new BalanceEvent { kind = "trick_used", detail = id,
            seconds = Time.timeAsDouble - started, encounter = wasInEncounter ? encounter : 0,
            map = run != null ? run.CurrentMapIndex : -1, node = run?.ActiveNode?.id ?? -1 });
        if (Current.outcome != "in_progress" && TryExport()) UpdateStats();
    }

    public static void Health(int hp, int maxHp) => Record("hp", "", hp, maxHp);

    // Black Market goods and services as stocked, bought, rerolled and sold. Keys: GearKey, TrickKey, ServiceKey.
    public static void Offered(string key, int price) => Record("offer", key, price);
    public static void Bought(string key, int creditsSpent, int quantity = 1) => Record("buy", key, creditsSpent, quantity);
    public static void Sold(string key, int payout) => Record("sell", key, payout);
    public static void Rerolled(int price) => Record("reroll", "", price);

    // Every source (Black Market, rewards, drops, Incidents, merges).
    public static void GearGained(string id) => Record("gear_gain", id);
    public static void GearMerged(string id) => Record("gear_merge", id);
    public static void GearLost(string id) => Record("gear_lose", id);
    public static void TrickGained(string id, int count) => Record("trick_gain", id, count);

    /// <summary>The route node taken and the node kinds that could have been taken instead.</summary>
    public static void NodeChosen(PathNode chosen, IEnumerable<PathNode> options)
    {
        if (Current == null || chosen == null) return;
        Record("node", chosen.kind.ToString(), chosen.mapIndex, chosen.id);
        foreach (var option in options)
            if (option != null) Record("node_option", option.kind.ToString(), option.mapIndex, option.id == chosen.id ? 1 : 0);
    }

    /// <summary>An Incident choice taken; choiceIndex -1 is LEAVE.</summary>
    public static void IncidentChosen(string eventId, int choiceIndex, string label) =>
        Record("incident_choice", eventId + "|" + label, choiceIndex);

    public static void EncounterEnded(EncounterResult result, int hpLost = 0)
    {
        Record("encounter_end", result.ToString(), hpLost);
        encounterOpen = false;
        if (result == EncounterResult.Defeat) Finish("defeat");
    }

    public static void Finish(string outcome)
    {
        if (Current == null || Current.outcome != "in_progress") return;
        RecordFinalBuild();
        Record("run_end", outcome);
        Current.outcome = outcome;
        Current.endedUtc = DateTime.UtcNow.ToString("O");
        terminalFrame = Time.frameCount;
        encounterOpen = false;
        if (TryExport()) UpdateStats();
    }

    static void RecordFinalBuild()
    {
        var cards = CardManager.Instance;
        if (cards == null) return;
        foreach (var gear in cards.ownedGear)
            if (gear != null) Record("final_gear", gear.id);
        for (int slot = 0; slot < cards.BackpackSlotsUsed; slot++)
        {
            var trick = cards.GetConsumableAtSlot(slot);
            if (trick != null) Record("final_trick", trick.id, cards.GetConsumableStackCountAtSlot(slot));
        }
        var combat = CombatManager.Instance;
        Record("final_state", "", combat != null ? combat.player.currentHealth : 0,
            combat != null ? combat.player.maxHealth : 0, cards.credits);
    }

    // Each finished (or interrupted) run replaces its own row in the stats, then the report files are rewritten.
    static void UpdateStats()
    {
        try
        {
            var store = BalanceStatsStore.Load(ExportDirectory);
            store.Upsert(BalanceRunSummary.FromJournal(Current));
            store.Save(ExportDirectory);
            var cards = CardManager.Instance;
            var report = BalanceStatsReport.Build(store.runs, BalanceStatsFilter.Default,
                cards != null ? BalanceNames.FromCatalogs(cards.gearCatalog, cards.consumableCatalog) : null);
            report.WriteCsv(Path.Combine(ExportDirectory, "Stats"));
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning("Balance stats update failed; the run journal was kept: " + exception.Message);
        }
        StatsUpdated?.Invoke();
    }

    public static bool TryExport()
    {
        if (Current == null) return false;
        try { LastExportPath = Current.Export(ExportDirectory); return true; }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning("Balance telemetry export failed; journal retained in memory: " + exception.Message);
            return false;
        }
    }
}
