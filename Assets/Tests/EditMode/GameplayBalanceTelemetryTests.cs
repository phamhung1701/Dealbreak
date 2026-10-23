using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class GameplayBalanceTelemetryTests
{
    GameObject cardsObject;
    bool previousEnabled;

    [SetUp]
    public void SetUp()
    {
        previousEnabled = GameplayBalanceTelemetry.Enabled;
        GameplayBalanceTelemetry.Enabled = true;
        GameplayBalanceTelemetry.Begin("telemetry-test");
    }

    [TearDown]
    public void TearDown()
    {
        GameplayBalanceTelemetry.Finish("test");
        string path = GameplayBalanceTelemetry.LastExportPath;
        if (path != null && File.Exists(path)) File.Delete(path);
        GameplayBalanceTelemetry.Enabled = previousEnabled;
        if (cardsObject != null) Object.DestroyImmediate(cardsObject);
    }

    [Test]
    public void Credits_RecordActualClampedDeltasAndIgnoreFailedSpend()
    {
        cardsObject = new GameObject("Telemetry Cards");
        var cards = cardsObject.AddComponent<CardManager>();
        if (cards.HasInfiniteMoney) Assert.Ignore("Normal economy requires development infinite money disabled.");
        cards.AddCredits(12);
        Assert.That(cards.SpendCredits(5), Is.True);
        Assert.That(cards.SpendCredits(100), Is.False);
        cards.AddCredits(-100);
        var events = GameplayBalanceTelemetry.Current.events.Where(e => e.kind == "credits").ToArray();
        Assert.That(events.Select(e => e.a), Is.EqualTo(new[] { 12, -5, -7 }));
        Assert.That(cards.credits, Is.Zero);
    }

    [Test]
    public void DamageRecordsActualLossSeparatelyFromShieldAndOverkill()
    {
        var player = new PlayerRuntime();
        player.Configure(10);
        player.Reset();
        var action = new CombatActionContext(1, CombatActionOrigin.EnemyRetaliation);
        var request = new DamageRequest(action, 0, CombatDamageOrigin.EnemyAggregate, null, player, 50);
        GameplayBalanceTelemetry.Damage(new DamageResult(request, 50, 0, 50, 0, 0, true, false, false));
        GameplayBalanceTelemetry.Damage(new DamageResult(request, 50, 0, 0, 10, 40, false, false, true));
        var damage = GameplayBalanceTelemetry.Current.events.Where(e => e.kind == "damage_received").ToArray();
        Assert.That(damage.Sum(e => e.a), Is.EqualTo(10));
        Assert.That(damage.Sum(e => e.c), Is.EqualTo(50));
        Assert.That(GameplayBalanceTelemetry.Current.events.Single(e => e.kind == "lethal").detail,
            Is.EqualTo("EnemyAggregate"));
    }

    [Test]
    public void JsonRoundTrip_PreservesEscapedSeedAndIntegers()
    {
        var journal = new BalanceJournal { seed = "quote\" comma, newline\n" };
        journal.Add(new BalanceEvent { kind = "credits", a = int.MaxValue });
        var restored = JsonUtility.FromJson<BalanceJournal>(journal.ToJson());
        Assert.That(restored.seed, Is.EqualTo(journal.seed));
        Assert.That(restored.events.Single().a, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void JournalIsBoundedAndMarksTruncation()
    {
        var journal = new BalanceJournal();
        var entry = new BalanceEvent { kind = "hp" };
        for (int i = 0; i <= BalanceJournal.EventLimit; i++) journal.Add(entry);
        Assert.That(journal.events.Count, Is.EqualTo(BalanceJournal.EventLimit));
        Assert.That(journal.truncated, Is.True);
    }

    [Test]
    public void FinishIsIdempotentAndLateCommittedTrickKeepsEncounterIdentity()
    {
        GameplayBalanceTelemetry.EncounterStarted(System.Array.Empty<EnemyRuntime>(), MapNodeType.Combat);
        GameplayBalanceTelemetry.EncounterEnded(EncounterResult.Defeat);
        GameplayBalanceTelemetry.TrickUsed("trick-test", true);
        GameplayBalanceTelemetry.Finish("completed");
        var journal = GameplayBalanceTelemetry.Current;
        Assert.That(journal.outcome, Is.EqualTo("defeat"));
        Assert.That(journal.events.Count(e => e.kind == "run_end"), Is.EqualTo(1));
        Assert.That(journal.events.Single(e => e.kind == "trick_used").encounter, Is.EqualTo(1));
        Assert.That(File.Exists(GameplayBalanceTelemetry.LastExportPath), Is.True);
    }

    [Test]
    public void DisabledNewRunDoesNotRecord()
    {
        GameplayBalanceTelemetry.Finish("test");
        GameplayBalanceTelemetry.Enabled = false;
        GameplayBalanceTelemetry.Begin("disabled");
        GameplayBalanceTelemetry.Record("hp", "", 5);
        Assert.That(GameplayBalanceTelemetry.Current, Is.Null);
    }

    [Test]
    public void ContinuedRunReopensItsJournalCutBackToTheSave()
    {
        string id = GameplayBalanceTelemetry.Current.id;
        GameplayBalanceTelemetry.Record("kept");
        GameplayBalanceTelemetry.Checkpointed();
        GameplayBalanceTelemetry.Record("after_save");
        GameplayBalanceTelemetry.Finish("interrupted");

        GameplayBalanceTelemetry.Resume("telemetry-test");
        var journal = GameplayBalanceTelemetry.Current;
        Assert.That(journal.id, Is.EqualTo(id));
        Assert.That(journal.outcome, Is.EqualTo("in_progress"));
        Assert.That(journal.resumed && !journal.partial, Is.True);
        Assert.That(journal.events.Any(e => e.kind == "kept"), Is.True);
        Assert.That(journal.events.Any(e => e.kind == "after_save" || e.kind == "run_end"), Is.False);

        GameplayBalanceTelemetry.Finish("defeat");
        var store = BalanceStatsStore.Load(GameplayBalanceTelemetry.ExportDirectory);
        Assert.That(store.runs.Count(run => run.id == id), Is.EqualTo(1));
        Assert.That(store.runs.Single(run => run.id == id).outcome, Is.EqualTo("defeat"));
    }

    [Test]
    public void ContinuedRunWithoutAJournalStartsAPartialOne()
    {
        GameplayBalanceTelemetry.Resume("never-recorded-seed");
        Assert.That(GameplayBalanceTelemetry.Current.partial, Is.True);
        Assert.That(GameplayBalanceTelemetry.Current.seed, Is.EqualTo("never-recorded-seed"));
    }

    [Test]
    public void FinishedRunUpdatesTheStatsAndTheirCsv()
    {
        string id = GameplayBalanceTelemetry.Current.id;
        GameplayBalanceTelemetry.Offered(GameplayBalanceTelemetry.GearKey("gear-a"), 10);
        GameplayBalanceTelemetry.Bought(GameplayBalanceTelemetry.GearKey("gear-a"), 10);
        GameplayBalanceTelemetry.Finish("completed");
        string directory = GameplayBalanceTelemetry.ExportDirectory;
        var run = BalanceStatsStore.Load(directory).runs.Single(value => value.id == id);
        Assert.That(run.Won, Is.True);
        Assert.That(run.items.Single(item => item.key == "gear:gear-a").bought, Is.EqualTo(1));
        Assert.That(File.Exists(Path.Combine(directory, "Stats", "Gear.csv")), Is.True);
    }

    [Test]
    public void SummaryReadsFightsPurchasesRouteAndIncidents()
    {
        var journal = new BalanceJournal { seed = "s", outcome = "defeat" };
        void Add(string kind, string detail = "", int map = 0, int a = 0, int b = 0) =>
            journal.Add(new BalanceEvent { kind = kind, detail = detail, map = map, a = a, b = b });
        Add("node", "Combat", 0, 0); Add("node_option", "Combat"); Add("node_option", "Shop");
        Add("encounter_start", "Combat"); Add("enemy", "Thug"); Add("enemy", "Thug"); Add("encounter_end", "Victory", 0, 3);
        Add("credits", "adjustment", 0, 12); Add("credits", "spend", 0, -10);
        Add("offer", "gear:g1", 1, 10); Add("buy", "gear:g1", 1, 10, 1); Add("gear_gain", "g1", 1);
        Add("offer", "service:Supply", 1, 4); Add("buy", "service:Supply", 1, 8, 2);
        Add("incident_choice", "evt_diner|Take the tip", 1, 1);
        Add("trick_gain", "t1", 1, 2); Add("trick_used", "t1", 1);
        Add("encounter_start", "Headliner", 1); Add("enemy", "The Jack", 1); Add("encounter_end", "Defeat", 1, 20);
        Add("final_gear", "g1", 1);

        var summary = BalanceRunSummary.FromJournal(journal);
        Assert.That(summary.districtReached, Is.EqualTo(2));
        Assert.That(summary.encounters.Count, Is.EqualTo(2));
        Assert.That(summary.encounters[0].enemies, Is.EqualTo("Thug|Thug"));
        Assert.That(summary.encounters[0].hpLost, Is.EqualTo(3));
        Assert.That(summary.deathDistrict, Is.EqualTo(2));
        Assert.That(summary.deathKind, Is.EqualTo("Headliner"));
        Assert.That(summary.deathEnemies, Is.EqualTo("The Jack"));
        Assert.That(summary.creditsEarned, Is.EqualTo(12));
        Assert.That(summary.creditsSpent, Is.EqualTo(10));
        var gear = summary.items.Single(item => item.key == "gear:g1");
        Assert.That((gear.offered, gear.bought, gear.spent, gear.gained, gear.final), Is.EqualTo((1, 1, 10, 1, 1)));
        Assert.That(summary.items.Single(item => item.key == "service:Supply").bought, Is.EqualTo(2));
        var trick = summary.items.Single(item => item.key == "trick:t1");
        Assert.That((trick.gained, trick.used), Is.EqualTo((2, 1)));
        Assert.That(summary.nodes.Single().options, Is.EqualTo("Combat|Shop"));
        Assert.That(summary.incidents.Single().incident, Is.EqualTo("evt_diner"));
        Assert.That(summary.incidents.Single().label, Is.EqualTo("Take the tip"));
    }

    [Test]
    public void ReportComparesWinRateWithGearAgainstAllRuns()
    {
        BalanceRunSummary Run(string outcome, params string[] finalGear)
        {
            var run = new BalanceRunSummary { id = outcome + finalGear.Length, outcome = outcome, districtReached = 3 };
            foreach (string id in finalGear)
                run.items.Add(new BalanceItemTally { key = GameplayBalanceTelemetry.GearKey(id), offered = 2, bought = 1, final = 1 });
            return run;
        }
        var runs = new[] { Run("completed", "g1"), Run("defeat"), Run("defeat", "g2"), Run("interrupted", "g1") };
        runs[3].dev = true;
        var report = BalanceStatsReport.Build(runs, BalanceStatsFilter.Default);
        Assert.That(report.RunsConsidered, Is.EqualTo(3));
        Assert.That(report.RunsFinished, Is.EqualTo(3));
        var overview = report.Tables.Single(table => table.Name == "Overview");
        Assert.That(overview.Rows.Single(row => row[0] == "Win rate")[1], Is.EqualTo("33.3%"));
        var gear = report.Tables.Single(table => table.Name == "Gear");
        var g1 = gear.Rows.Single(row => row[1] == "g1");
        Assert.That(g1[5], Is.EqualTo("50.0%"));
        Assert.That(g1[11], Is.EqualTo("100.0%"));
        Assert.That(g1[12], Is.EqualTo("+66.7 pp"));
    }
}
