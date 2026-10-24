using System;
using System.Collections.Generic;

/// <summary>
/// Upgrades older run saves to the current schema before validation. Each step converts one
/// version to the next in place. Saves from a newer build are rejected, never guessed at.
/// When the schema changes: bump RunSaveValidation.Version, add the step for the previous
/// version here, and keep a fixture save of that version in the tests.
/// </summary>
public static class RunSaveMigrations
{
    // [N] upgrades a version-N payload to version N + 1.
    static readonly Dictionary<int, Action<RunSaveData>> Steps = new()
    {
        [1] = MarkScoutedNodes,
        [2] = RetireTrainingNodes,
        [3] = FoldDiscardIntoDrawPile,
        [4] = RenameEnemyContentIds,
        [5] = StartWithoutCharacter,
    };

    // A fight node's contentId is its enemy's asset name, and the enemy assets took their workbook
    // names in v5 (Knight became SecurityGuard, and so on).
    internal static readonly IReadOnlyDictionary<string, string> RenamedEnemyAssets = new Dictionary<string, string>
    {
        ["Goblin"] = "Scrapper", ["Thief"] = "Pickpocket", ["Knight"] = "SecurityGuard", ["Brute"] = "Heavy",
        ["Shieldbearer"] = "RiotGuard", ["WarDrummer"] = "HypeMan", ["Duelist"] = "Challenger",
        ["GoblinCaptain"] = "CrewLeader", ["RoyalKnight"] = "RiotCaptain", ["MasterThief"] = "Ghost",
    };

    // v6 saves the run's character. A run saved before then had none: it keeps playing with no
    // perk (the default look), never a perk it did not start with.
    static void StartWithoutCharacter(RunSaveData data)
    {
        if (data.cards != null) data.cards.character = string.Empty;
    }

    static void RenameEnemyContentIds(RunSaveData data)
    {
        if (data.nodes == null) return;
        foreach (var node in data.nodes)
            if (node.kind is (int)MapNodeType.Combat or (int)MapNodeType.Elite &&
                node.contentId != null && RenamedEnemyAssets.TryGetValue(node.contentId, out var renamed))
                node.contentId = renamed;
    }

    // v3 sent attack cards to a discard pile that came back only when the draw pile ran out. Played
    // cards now return to the draw pile at once, so a saved discard pile joins the bottom of it
    // (draws take from the top), keeping the order of every card already in the draw pile.
    static void FoldDiscardIntoDrawPile(RunSaveData data)
    {
        var cards = data.cards;
        if (cards?.deck == null || cards.discard == null || cards.discard.Count == 0) return;
        cards.deck.InsertRange(0, cards.discard);
        cards.discard.Clear();
    }

    // The Street Mechanic Incident grants a free Mod, as Training did.
    internal const string TrainingReplacementEventId = "evt_005";

    // v2 had Training (Upgrade) nodes. Each becomes the Street Mechanic Incident; a run saved
    // while standing on one resumes in that Incident. Its cached Training offers are dropped.
    static void RetireTrainingNodes(RunSaveData data)
    {
        if (data.nodes == null) return;
        foreach (var node in data.nodes)
        {
            if (node.kind != (int)MapNodeType.Upgrade) continue;
            node.kind = (int)MapNodeType.Event;
            node.type = "event";
            node.contentId = TrainingReplacementEventId;
            if (node.id == data.activeNodeId) data.activeEventId = TrainingReplacementEventId;
        }
        if (data.visitedNodeTypes != null)
            for (int i = 0; i < data.visitedNodeTypes.Count; i++)
                if (data.visitedNodeTypes[i] == (int)MapNodeType.Upgrade) data.visitedNodeTypes[i] = (int)MapNodeType.Event;
    }

    // v1 had no scouted flag: a node revealed ahead of travel (unvisited and not yet accessible, or
    // a hidden Incident/Risk) was a scouted one.
    static void MarkScoutedNodes(RunSaveData data)
    {
        if (data.nodes == null) return;
        foreach (var node in data.nodes)
            node.scouted = node.revealed && !node.completed && (node.hidden || !node.accessible);
    }

    public static bool TryUpgrade(RunSaveData data, out string error) => TryUpgrade(data, Steps, out error);

    internal static bool TryUpgrade(RunSaveData data, IReadOnlyDictionary<int, Action<RunSaveData>> steps,
        out string error)
    {
        if (data == null) { error = "Missing save data."; return false; }
        if (data.version > RunSaveValidation.Version)
        {
            error = $"Save version {data.version} is newer than this build supports.";
            return false;
        }
        while (data.version < RunSaveValidation.Version)
        {
            if (!steps.TryGetValue(data.version, out var step))
            {
                error = $"No migration from save version {data.version}.";
                return false;
            }
            step(data);
            data.version++;
        }
        error = null;
        return true;
    }
}
