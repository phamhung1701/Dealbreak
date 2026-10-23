using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Design stats rolled up from the balance journals: one compact summary per run (BalanceStatsStore,
// updated whenever a run ends) and the tables built from them (BalanceStatsReport).

[Serializable]
public sealed class BalanceItemTally
{
    // GameplayBalanceTelemetry.GearKey / TrickKey / ServiceKey.
    public string key;
    public int offered, bought, spent, gained, merged, lost, sold, used, final;
}

[Serializable]
public sealed class BalanceEncounterSummary
{
    public int map;
    public string kind, result;
    // Enemy type names, one per enemy, joined by '|'.
    public string enemies;
    public int hpLost;
}

[Serializable]
public sealed class BalanceNodeChoice
{
    public int map;
    public string picked;
    // The node kinds that could be taken, joined by '|'.
    public string options;
}

[Serializable]
public sealed class BalanceIncidentPick
{
    public string incident, label;
    // -1 is LEAVE.
    public int choice;
}

[Serializable]
public sealed class BalanceRunSummary
{
    public string id, seed, startedUtc, endedUtc, outcome, gameVersion;
    public bool dev, invalid, partial, truncated;
    public int journalSchema;
    // 1-based; 13 means all 12 Districts were cleared.
    public int districtReached;
    public double seconds;
    public int creditsEarned, creditsSpent, rerolls, rerollSpent, finalHp, finalMaxHp, finalCredits;
    public int deathDistrict;
    public string deathKind, deathEnemies;
    public List<BalanceItemTally> items = new();
    public List<BalanceEncounterSummary> encounters = new();
    public List<BalanceNodeChoice> nodes = new();
    public List<BalanceIncidentPick> incidents = new();

    // Abandoning or restarting ends a run without a win, so it counts against the win rate.
    public bool Finished => outcome is "completed" or "defeat" or "abandoned" or "restarted";
    public bool Won => outcome == "completed";

    public static BalanceRunSummary FromJournal(BalanceJournal journal)
    {
        var summary = new BalanceRunSummary
        {
            id = journal.id, seed = journal.seed, startedUtc = journal.startedUtc, endedUtc = journal.endedUtc,
            outcome = journal.outcome, gameVersion = journal.gameVersion, dev = journal.developmentModified,
            invalid = journal.invalid, partial = journal.partial, truncated = journal.truncated,
            journalSchema = journal.schemaVersion
        };
        var tallies = new Dictionary<string, BalanceItemTally>(StringComparer.Ordinal);
        BalanceItemTally Tally(string key)
        {
            if (!tallies.TryGetValue(key, out var tally))
            {
                tallies.Add(key, tally = new BalanceItemTally { key = key });
                summary.items.Add(tally);
            }
            return tally;
        }
        BalanceEncounterSummary open = null;
        var openEnemies = new List<string>();
        BalanceNodeChoice choice = null;
        var options = new List<string>();
        int maxMap = -1;
        foreach (var entry in journal.events)
        {
            if (entry == null || string.IsNullOrEmpty(entry.kind)) continue;
            // The run end is recorded after the last Headliner moves the District on.
            if (!entry.kind.StartsWith("final_", StringComparison.Ordinal) && entry.kind != "run_end")
                maxMap = Math.Max(maxMap, entry.map);
            summary.seconds = Math.Max(summary.seconds, entry.seconds);
            string detail = entry.detail ?? string.Empty;
            switch (entry.kind)
            {
                case "encounter_start":
                    open = new BalanceEncounterSummary { map = entry.map, kind = detail, result = "Unfinished" };
                    openEnemies.Clear();
                    summary.encounters.Add(open);
                    break;
                case "enemy":
                    if (open == null) break;
                    openEnemies.Add(detail);
                    open.enemies = string.Join("|", openEnemies);
                    break;
                case "encounter_end":
                    if (open == null) break;
                    open.result = detail;
                    open.hpLost = entry.a;
                    open = null;
                    break;
                case "credits":
                    if (detail == "spend") summary.creditsSpent += -entry.a;
                    else if (entry.a > 0) summary.creditsEarned += entry.a;
                    break;
                case "offer": Tally(detail).offered++; break;
                case "buy":
                    var bought = Tally(detail);
                    bought.bought += Math.Max(1, entry.b);
                    bought.spent += entry.a;
                    break;
                case "sell": Tally(detail).sold++; break;
                case "reroll": summary.rerolls++; summary.rerollSpent += entry.a; break;
                case "gear_gain": Tally(GameplayBalanceTelemetry.GearKey(detail)).gained++; break;
                case "gear_merge": Tally(GameplayBalanceTelemetry.GearKey(detail)).merged++; break;
                case "gear_lose": Tally(GameplayBalanceTelemetry.GearKey(detail)).lost++; break;
                case "trick_gain": Tally(GameplayBalanceTelemetry.TrickKey(detail)).gained += Math.Max(1, entry.a); break;
                case "trick_used": Tally(GameplayBalanceTelemetry.TrickKey(detail)).used++; break;
                case "final_gear": Tally(GameplayBalanceTelemetry.GearKey(detail)).final++; break;
                case "final_trick": Tally(GameplayBalanceTelemetry.TrickKey(detail)).final += Math.Max(1, entry.a); break;
                case "final_state":
                    summary.finalHp = entry.a;
                    summary.finalMaxHp = entry.b;
                    summary.finalCredits = entry.c;
                    break;
                case "node":
                    choice = new BalanceNodeChoice { map = entry.a, picked = detail, options = detail };
                    options.Clear();
                    summary.nodes.Add(choice);
                    break;
                case "node_option":
                    if (choice == null) break;
                    options.Add(detail);
                    choice.options = string.Join("|", options);
                    break;
                case "incident_choice":
                    int split = detail.IndexOf('|');
                    summary.incidents.Add(new BalanceIncidentPick
                    {
                        incident = split >= 0 ? detail.Substring(0, split) : detail,
                        label = split >= 0 ? detail.Substring(split + 1) : string.Empty,
                        choice = entry.a
                    });
                    break;
            }
        }
        summary.districtReached = Math.Max(1, maxMap + 1);
        if (summary.outcome == "defeat")
        {
            var fatal = summary.encounters.LastOrDefault();
            summary.deathDistrict = fatal != null ? fatal.map + 1 : summary.districtReached;
            summary.deathKind = fatal?.kind ?? string.Empty;
            summary.deathEnemies = fatal?.enemies ?? string.Empty;
        }
        return summary;
    }
}

[Serializable]
public sealed class BalanceStatsStore
{
    public const string FileName = "run-stats.json";
    public int schemaVersion = 1;
    public List<BalanceRunSummary> runs = new();

    public static BalanceStatsStore Load(string directory)
    {
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path)) return new BalanceStatsStore();
        try { return JsonUtility.FromJson<BalanceStatsStore>(File.ReadAllText(path)) ?? new BalanceStatsStore(); }
        catch (ArgumentException exception)
        {
            Debug.LogWarning($"Balance stats file is unreadable and will be rebuilt from the journals: {exception.Message}");
            return Rebuild(directory);
        }
    }

    public void Save(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, FileName);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(this));
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
    }

    /// <summary>A run keeps one row: a continued run replaces the row its interruption left.</summary>
    public void Upsert(BalanceRunSummary summary)
    {
        if (summary == null) return;
        int index = runs.FindIndex(run => run.id == summary.id);
        if (index >= 0) runs[index] = summary;
        else runs.Add(summary);
    }

    /// <summary>Re-reads every journal in the folder (older journals included) into a fresh store.</summary>
    public static BalanceStatsStore Rebuild(string directory)
    {
        var store = new BalanceStatsStore();
        if (!Directory.Exists(directory)) return store;
        foreach (string path in Directory.GetFiles(directory, "*.json").OrderBy(value => value, StringComparer.Ordinal))
        {
            if (Path.GetFileName(path) == FileName) continue;
            var journal = ReadJournal(path);
            if (journal != null && journal.outcome != "in_progress") store.Upsert(BalanceRunSummary.FromJournal(journal));
        }
        return store;
    }

    public static BalanceJournal LoadInterruptedJournal(string directory, string seed)
    {
        var match = Load(directory).runs
            .Where(run => run.outcome == "interrupted" && run.seed == seed)
            .OrderByDescending(run => run.startedUtc, StringComparer.Ordinal)
            .FirstOrDefault();
        return match != null ? ReadJournal(Path.Combine(directory, match.id + ".json")) : null;
    }

    static BalanceJournal ReadJournal(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var journal = JsonUtility.FromJson<BalanceJournal>(File.ReadAllText(path));
            return journal != null && !string.IsNullOrEmpty(journal.id) && journal.events != null ? journal : null;
        }
        catch (Exception exception) when (exception is IOException || exception is ArgumentException ||
                                          exception is UnauthorizedAccessException)
        {
            return null;
        }
    }
}

public struct BalanceStatsFilter
{
    public bool IncludeDev, IncludeInvalid;
    // Empty means every game version.
    public string GameVersion;

    public static BalanceStatsFilter Default => new();

    public bool Accepts(BalanceRunSummary run) => run != null &&
        (IncludeDev || !run.dev) && (IncludeInvalid || !run.invalid) &&
        (string.IsNullOrEmpty(GameVersion) || run.gameVersion == GameVersion);
}

/// <summary>Player-facing names and Gear rarity by stats key; unknown keys show their id.</summary>
public sealed class BalanceNames
{
    readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _rarity = new(StringComparer.Ordinal);

    public static BalanceNames FromCatalogs(IEnumerable<GearData> gear, IEnumerable<ConsumableData> tricks)
    {
        var names = new BalanceNames();
        foreach (var value in gear ?? Enumerable.Empty<GearData>())
        {
            if (value == null || string.IsNullOrEmpty(value.id)) continue;
            string key = GameplayBalanceTelemetry.GearKey(value.id);
            names._names[key] = value.displayName;
            names._rarity[key] = value.rarity;
        }
        foreach (var value in tricks ?? Enumerable.Empty<ConsumableData>())
            if (value != null && !string.IsNullOrEmpty(value.id))
                names._names[GameplayBalanceTelemetry.TrickKey(value.id)] = value.displayName;
        return names;
    }

    public string Name(string key)
    {
        if (key != null && _names.TryGetValue(key, out var name) && !string.IsNullOrEmpty(name)) return name;
        int colon = key?.IndexOf(':') ?? -1;
        return colon >= 0 ? key.Substring(colon + 1) : key;
    }

    public string Rarity(string key) => key != null && _rarity.TryGetValue(key, out var rarity) ? rarity : string.Empty;
}

public sealed class BalanceTable
{
    public string Name;
    public string[] Columns;
    public readonly List<string[]> Rows = new();

    public BalanceTable(string name, params string[] columns) { Name = name; Columns = columns; }
    public void Add(params object[] cells) => Rows.Add(cells.Select(BalanceStatsReport.Cell).ToArray());
}

public sealed class BalanceStatsReport
{
    public readonly List<BalanceTable> Tables = new();
    public int RunsConsidered, RunsFinished;

    static readonly string[] ServiceKinds = { "Guidance", "Contract", "Investment", "Supply" };

    public static string NodeName(string kind) => kind switch
    {
        "Combat" => "Fight",
        "Elite" => "Enforcer",
        "Shop" => "Black Market",
        "Event" => "Incident",
        "Risk" => "Risk Incident",
        _ => kind
    };

    static string ServiceName(string kind) => kind switch
    {
        "Guidance" => "Intel",
        "Investment" => "Stake",
        _ => kind
    };

    internal static string Cell(object value) => value switch
    {
        null => string.Empty,
        double number => double.IsNaN(number) ? "-" : number.ToString("0.##", CultureInfo.InvariantCulture),
        float number => ((double)number).ToString("0.##", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    static string Percent(int part, int whole) => whole <= 0 ? "-" :
        (100.0 * part / whole).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    static double Average(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? double.NaN : list.Average();
    }

    static IEnumerable<string> Enemies(BalanceEncounterSummary encounter) =>
        string.IsNullOrEmpty(encounter.enemies) ? Enumerable.Empty<string>() : encounter.enemies.Split('|').Distinct();

    public static BalanceStatsReport Build(IEnumerable<BalanceRunSummary> source, BalanceStatsFilter filter, BalanceNames names = null)
    {
        names ??= new BalanceNames();
        var runs = (source ?? Enumerable.Empty<BalanceRunSummary>()).Where(filter.Accepts).ToList();
        var finished = runs.Where(run => run.Finished).ToList();
        int wins = finished.Count(run => run.Won);
        var report = new BalanceStatsReport { RunsConsidered = runs.Count, RunsFinished = finished.Count };
        double overallWinRate = finished.Count == 0 ? double.NaN : (double)wins / finished.Count;

        var overview = new BalanceTable("Overview", "Stat", "Value");
        overview.Add("Runs recorded", runs.Count);
        overview.Add("Finished runs", finished.Count);
        overview.Add("Wins (all Districts cleared)", wins);
        overview.Add("Defeats", finished.Count(run => run.outcome == "defeat"));
        overview.Add("Abandoned or restarted", finished.Count(run => run.outcome is "abandoned" or "restarted"));
        overview.Add("Unfinished (saved mid-run)", runs.Count(run => !run.Finished));
        overview.Add("Win rate", Percent(wins, finished.Count));
        overview.Add("Avg District reached", Average(finished.Select(run => (double)Math.Min(run.districtReached, RunLength.Districts))));
        overview.Add("Avg run length (min)", Average(finished.Select(run => run.seconds / 60.0)));
        overview.Add("Avg fights per run", Average(finished.Select(run => (double)run.encounters.Count)));
        overview.Add("Avg Credits earned per run", Average(finished.Select(run => (double)run.creditsEarned)));
        overview.Add("Avg Credits spent per run", Average(finished.Select(run => (double)run.creditsSpent)));
        overview.Add("Avg goods rerolls per run", Average(finished.Select(run => (double)run.rerolls)));
        overview.Add("Avg Gear owned at end", Average(finished.Select(run =>
            (double)run.items.Where(item => item.key.StartsWith("gear:", StringComparison.Ordinal)).Sum(item => item.final))));
        overview.Add("Runs continued without their start (partial)", runs.Count(run => run.partial));
        report.Tables.Add(overview);

        var districts = new BalanceTable("Districts", "District", "Reached", "Survival from start", "Defeated here",
            "Abandoned here", "Death rate here", "Fights", "Avg HP lost per fight");
        int lastDistrict = Math.Max(RunLength.Districts, finished.Select(run => run.districtReached).DefaultIfEmpty(0).Max());
        for (int district = 1; district <= lastDistrict && district <= 99; district++)
        {
            int reached = finished.Count(run => run.districtReached >= district);
            if (district > RunLength.Districts && reached == 0) break;
            int died = finished.Count(run => run.outcome == "defeat" && run.deathDistrict == district);
            int abandoned = finished.Count(run => run.outcome is "abandoned" or "restarted" && run.districtReached == district);
            var fights = runs.SelectMany(run => run.encounters).Where(encounter => encounter.map + 1 == district).ToList();
            districts.Add(district, reached, Percent(reached, finished.Count), died, abandoned, Percent(died, reached),
                fights.Count, Average(fights.Select(fight => (double)fight.hpLost)));
        }
        report.Tables.Add(districts);

        var allFights = runs.SelectMany(run => run.encounters).ToList();
        var fightKinds = new BalanceTable("Fights by node", "Node", "Fights", "Won", "Defeats", "Defeat rate", "Avg HP lost");
        foreach (var group in allFights.GroupBy(fight => fight.kind).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            int defeats = group.Count(fight => fight.result == "Defeat");
            fightKinds.Add(NodeName(group.Key), group.Count(), group.Count(fight => fight.result == "Victory"), defeats,
                Percent(defeats, group.Count()), Average(group.Select(fight => (double)fight.hpLost)));
        }
        report.Tables.Add(fightKinds);

        var enemies = new BalanceTable("Enemies", "Enemy", "Fights", "As Headliner", "Defeats caused", "Defeat rate",
            "Avg HP lost", "First District", "Last District");
        foreach (var group in allFights.SelectMany(fight => Enemies(fight).Select(name => (name, fight)))
                     .GroupBy(pair => pair.name).OrderByDescending(group => group.Count()))
        {
            int defeats = group.Count(pair => pair.fight.result == "Defeat");
            enemies.Add(group.Key, group.Count(), group.Count(pair => pair.fight.kind == "Headliner"), defeats,
                Percent(defeats, group.Count()), Average(group.Select(pair => (double)pair.fight.hpLost)),
                group.Min(pair => pair.fight.map) + 1, group.Max(pair => pair.fight.map) + 1);
        }
        report.Tables.Add(enemies);

        var headliners = new BalanceTable("Headliners", "Headliner", "Fights", "Wins", "Defeats", "Win rate", "Avg HP lost", "Districts");
        foreach (var group in allFights.Where(fight => fight.kind == "Headliner" && !string.IsNullOrEmpty(fight.enemies))
                     .GroupBy(fight => fight.enemies.Split('|')[0]).OrderBy(group => group.Min(fight => fight.map)))
        {
            int won = group.Count(fight => fight.result == "Victory");
            int lost = group.Count(fight => fight.result == "Defeat");
            headliners.Add(group.Key, group.Count(), won, lost, Percent(won, won + lost),
                Average(group.Select(fight => (double)fight.hpLost)),
                string.Join(" ", group.Select(fight => fight.map + 1).Distinct().OrderBy(value => value)));
        }
        report.Tables.Add(headliners);

        var killers = new BalanceTable("Defeats", "District", "Node", "Enemies", "Defeats", "Share of defeats");
        var deaths = finished.Where(run => run.outcome == "defeat").ToList();
        foreach (var group in deaths.GroupBy(run => (run.deathDistrict, run.deathKind, run.deathEnemies))
                     .OrderByDescending(group => group.Count()).ThenBy(group => group.Key.deathDistrict))
            killers.Add(group.Key.deathDistrict, NodeName(group.Key.deathKind),
                (group.Key.deathEnemies ?? string.Empty).Replace("|", ", "), group.Count(), Percent(group.Count(), deaths.Count));
        report.Tables.Add(killers);

        report.Tables.Add(ItemTable("Gear", "gear:", runs, finished, overallWinRate, names, gear: true));
        report.Tables.Add(ItemTable("Tricks", "trick:", runs, finished, overallWinRate, names, gear: false));

        var services = new BalanceTable("Services", "Service", "Offered", "Purchases", "Buy rate", "Credits spent", "Runs that bought");
        foreach (string kind in ServiceKinds)
        {
            string key = "service:" + kind;
            var tallies = runs.SelectMany(run => run.items).Where(item => item.key == key).ToList();
            int offered = tallies.Sum(item => item.offered), bought = tallies.Sum(item => item.bought);
            // Supply counts cards bought, so its rate is per offer with any purchase capped at one.
            int purchased = tallies.Sum(item => Math.Min(item.bought, item.offered));
            services.Add(ServiceName(kind), offered, bought, Percent(purchased, offered),
                tallies.Sum(item => item.spent), tallies.Count(item => item.bought > 0));
        }
        services.Add("Goods reroll", "-", runs.Sum(run => run.rerolls), "-", runs.Sum(run => run.rerollSpent),
            runs.Count(run => run.rerolls > 0));
        report.Tables.Add(services);

        var route = new BalanceTable("Route choices", "Node", "Times available", "Times taken", "Pick rate");
        // Only real choices: a column with one open node (a Black Market checkpoint, the Headliner) is forced.
        var choices = runs.SelectMany(run => run.nodes).Where(choice => choice.options.Contains('|')).ToList();
        foreach (var kind in choices.SelectMany(choice => choice.options.Split('|')).Where(kind => kind.Length > 0)
                     .Distinct().OrderBy(kind => kind, StringComparer.Ordinal))
        {
            int available = choices.Count(choice => choice.options.Split('|').Contains(kind));
            int taken = choices.Count(choice => choice.picked == kind);
            route.Add(NodeName(kind), available, taken, Percent(taken, available));
        }
        report.Tables.Add(route);

        var incidents = new BalanceTable("Incident choices", "Incident", "Choice", "Visits", "Picked", "Pick rate");
        var picks = runs.SelectMany(run => run.incidents).ToList();
        foreach (var incident in picks.GroupBy(pick => pick.incident).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            int visits = incident.Count();
            foreach (var option in incident.GroupBy(pick => (pick.choice, pick.label)).OrderBy(group => group.Key.choice))
                incidents.Add(incident.Key, option.Key.choice < 0 ? "LEAVE" : option.Key.label, visits, option.Count(),
                    Percent(option.Count(), visits));
        }
        report.Tables.Add(incidents);

        var log = new BalanceTable("Runs", "Started (UTC)", "Seed", "Outcome", "District", "Died to", "Fights", "Minutes",
            "Credits earned", "Gear at end", "Version", "Dev");
        foreach (var run in runs.OrderByDescending(run => run.startedUtc, StringComparer.Ordinal))
            log.Add(run.startedUtc != null && run.startedUtc.Length >= 16 ? run.startedUtc.Substring(0, 16).Replace('T', ' ') : run.startedUtc,
                run.seed, run.outcome, Math.Min(run.districtReached, RunLength.Districts),
                (run.deathEnemies ?? string.Empty).Replace("|", ", "), run.encounters.Count, run.seconds / 60.0,
                run.creditsEarned,
                string.Join(", ", run.items.Where(item => item.key.StartsWith("gear:", StringComparison.Ordinal) && item.final > 0)
                    .Select(item => names.Name(item.key))),
                run.gameVersion, run.dev ? "dev" : string.Empty);
        report.Tables.Add(log);
        return report;
    }

    static BalanceTable ItemTable(string title, string prefix, List<BalanceRunSummary> runs, List<BalanceRunSummary> finished,
        double overallWinRate, BalanceNames names, bool gear)
    {
        var table = gear
            ? new BalanceTable(title, "Gear", "Id", "Rarity", "Offered", "Bought", "Buy rate", "Credits spent", "Gained (all sources)",
                "Merged into", "Sold", "Runs owning at end", "Win rate owning at end", "vs all runs")
            : new BalanceTable(title, "Trick", "Id", "Offered", "Bought", "Buy rate", "Credits spent", "Gained (all sources)",
                "Used", "Sold", "Runs that used it", "Win rate when used", "vs all runs");
        var keys = runs.SelectMany(run => run.items).Select(item => item.key)
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).Distinct();
        var rows = new List<(string key, int offered, string[] cells)>();
        foreach (string key in keys)
        {
            var tallies = runs.SelectMany(run => run.items).Where(item => item.key == key).ToList();
            int offered = tallies.Sum(item => item.offered);
            int bought = tallies.Sum(item => item.bought);
            string buyRate = Percent(bought, offered);
            var withItem = finished.Where(run => run.items.Any(item => item.key == key &&
                (gear ? item.final > 0 : item.used > 0))).ToList();
            int withWins = withItem.Count(run => run.Won);
            double rate = withItem.Count == 0 ? double.NaN : (double)withWins / withItem.Count;
            string delta = double.IsNaN(rate) || double.IsNaN(overallWinRate) ? "-" :
                ((rate - overallWinRate) * 100).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " pp";
            string id = key.Substring(prefix.Length);
            var cells = gear
                ? new object[] { names.Name(key), id, names.Rarity(key), offered, bought, buyRate, tallies.Sum(item => item.spent),
                    tallies.Sum(item => item.gained), tallies.Sum(item => item.merged), tallies.Sum(item => item.sold),
                    withItem.Count, Percent(withWins, withItem.Count), delta }
                : new object[] { names.Name(key), id, offered, bought, buyRate, tallies.Sum(item => item.spent),
                    tallies.Sum(item => item.gained), tallies.Sum(item => item.used), tallies.Sum(item => item.sold),
                    withItem.Count, Percent(withWins, withItem.Count), delta };
            rows.Add((key, offered, cells.Select(Cell).ToArray()));
        }
        foreach (var row in rows.OrderByDescending(row => row.offered).ThenBy(row => row.cells[0], StringComparer.Ordinal))
            table.Rows.Add(row.cells);
        return table;
    }

    /// <summary>One CSV per table (UTF-8, comma separated), replacing the previous files.</summary>
    public void WriteCsv(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var table in Tables)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", table.Columns.Select(Escape)));
            foreach (var row in table.Rows) builder.AppendLine(string.Join(",", row.Select(Escape)));
            File.WriteAllText(Path.Combine(directory, table.Name.Replace(' ', '_') + ".csv"), builder.ToString(), new UTF8Encoding(true));
        }
    }

    static string Escape(string value)
    {
        value ??= string.Empty;
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
