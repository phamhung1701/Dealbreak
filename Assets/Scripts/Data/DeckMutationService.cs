using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Validates run-persistent deck edits before committing zone changes.</summary>
public static class DeckMutationService
{
    public static bool CanTarget(CardCollection collection, CardInstance target, bool inCombat)
    {
        if (collection == null || target == null || !collection.OwnedCards.Contains(target)) return false;
        return !inCombat || collection.ContainsInHand(target);
    }

    public static CardInstance Duplicate(CardCollection collection, CardInstance target, int newId, bool inCombat)
    {
        if (newId <= 0 || !CanTarget(collection, target, inCombat)) return null;
        var copy = target.ClonePermanentState(newId);
        return (inCombat ? collection.AddOwnedToHand(copy) : collection.AddOwnedToDeck(copy)) ? copy : null;
    }

    public static bool CanDestroy(CardCollection collection, IReadOnlyList<CardInstance> targets, bool inCombat)
    {
        if (collection == null || targets == null || targets.Count < 1 || targets.Count > 2 ||
            targets.Count >= collection.OwnedCards.Count) return false;
        var seen = new HashSet<CardInstance>();
        foreach (var target in targets)
            if (!CanTarget(collection, target, inCombat) || !seen.Add(target)) return false;
        return true;
    }

    public static bool Destroy(CardCollection collection, IReadOnlyList<CardInstance> targets, bool inCombat) =>
        CanDestroy(collection, targets, inCombat) && collection.TryRemoveOwnedCards(targets);
}
