using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// RunManager: Trick reward queue, enemy drops and Gear victory rewards.
public partial class RunManager
{
    readonly HashSet<int> _consumableDropResolvedContexts = new();
    readonly Queue<ConsumableRewardOffer> _pendingConsumableRewards = new();
    readonly HashSet<string> _resolvedConsumableRewardContexts = new(StringComparer.Ordinal);
    readonly Dictionary<int, ConsumableData[]> _alchemistChoicesByMap = new();
    int _combatWinCount;
    int _satchelRewardCount;
    public int CombatWinCount => _combatWinCount;

    public ConsumableRewardOffer PendingConsumableReward =>
        _pendingConsumableRewards.Count > 0 ? _pendingConsumableRewards.Peek() : null;

    // An enemy drop that did not fit waits on the board; the run goes on once it is claimed or left.
    ConsumableData _pendingEnemyDrop;
    Action _afterEnemyDrop;
    public ConsumableData PendingEnemyDrop => _pendingEnemyDrop;
    /// <summary>Raised when a drop starts or stops waiting on the board.</summary>
    public event Action OnEnemyDropChanged;
    /// <summary>Raised when an enemy drop enters the backpack (at once, or when claimed from the board).</summary>
    public event Action<ConsumableData> OnEnemyDropClaimed;

    public bool CanClaimPendingEnemyDrop => _pendingEnemyDrop != null && CardManager.Instance != null &&
        CardManager.Instance.CanAddConsumable(_pendingEnemyDrop);

    public bool ClaimPendingEnemyDrop()
    {
        var drop = _pendingEnemyDrop;
        if (drop == null || GameplayInputGate.IsBlocked || !CanClaimPendingEnemyDrop ||
            !CardManager.Instance.AddConsumable(drop)) return false;
        _pendingEnemyDrop = null;
        OnEnemyDropClaimed?.Invoke(drop);
        ContinueAfterEnemyDrop();
        return true;
    }

    public bool LeavePendingEnemyDrop()
    {
        if (_pendingEnemyDrop == null || GameplayInputGate.IsBlocked) return false;
        _pendingEnemyDrop = null;
        ContinueAfterEnemyDrop();
        return true;
    }

    void ContinueAfterEnemyDrop()
    {
        OnEnemyDropChanged?.Invoke();
        var next = _afterEnemyDrop;
        _afterEnemyDrop = null;
        next?.Invoke();
    }

    void ClearPendingEnemyDrop()
    {
        bool had = _pendingEnemyDrop != null;
        _pendingEnemyDrop = null;
        _afterEnemyDrop = null;
        if (had) OnEnemyDropChanged?.Invoke();
    }

    // These are run-authoritative node/content operations, invoked by typed effects only.
    internal bool TryGrantRandomCommonEnhancement(int completedNodeCount)
    {
        var cards = CardManager.Instance;
        if (_randomContext == null || cards == null) return false;
        var eligible = cards.ownedCards
            .Where(card => card != null && card.Enhancement == null)
            .OrderBy(card => card.Id).ToArray();
        var common = cards.enhancementCatalog
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.canonicalId) &&
                string.Equals(value.rarity, "Common", StringComparison.Ordinal))
            .OrderBy(value => value.canonicalId, StringComparer.Ordinal).ToArray();
        if (eligible.Length == 0 || common.Length == 0) return false;
        var cardRandom = _randomContext.CreateStream("artifact-common-card", completedNodeCount);
        var enhancementRandom = _randomContext.CreateStream("artifact-common-enhancement", completedNodeCount);
        var card = eligible[cardRandom.NextInt(0, eligible.Length)];
        var enhancement = common[enhancementRandom.NextInt(0, common.Length)];
        return cards.ApplyEnhancement(card.Id, enhancement);
    }

    public bool ClaimPendingConsumableReward(int choiceIndex)
    {
        var reward = PendingConsumableReward;
        var cards = CardManager.Instance;
        if (reward == null || cards == null || choiceIndex < 0 || choiceIndex >= reward.Choices.Count ||
            !cards.AddConsumable(reward.Choices[choiceIndex])) return false;
        CompletePendingConsumableReward();
        return true;
    }

    public bool ReplacePendingConsumableReward(int choiceIndex, int backpackSlotIndex)
    {
        var reward = PendingConsumableReward;
        var cards = CardManager.Instance;
        if (reward == null || cards == null || choiceIndex < 0 || choiceIndex >= reward.Choices.Count ||
            !cards.ReplaceConsumableSlot(backpackSlotIndex, reward.Choices[choiceIndex])) return false;
        CompletePendingConsumableReward();
        return true;
    }

    public bool DeclinePendingConsumableReward()
    {
        if (PendingConsumableReward == null) return false;
        CompletePendingConsumableReward();
        return true;
    }

    void CompletePendingConsumableReward()
    {
        if (_pendingConsumableRewards.Count > 0) _pendingConsumableRewards.Dequeue();
        GameplayInputGate.Set(GameplayInputBlockReason.ConsumableReward, _pendingConsumableRewards.Count > 0);
        OnConsumableRewardChanged?.Invoke();
    }

    bool QueueConsumableReward(string contextId, string sourceLabel, IReadOnlyList<ConsumableData> choices)
    {
        if (string.IsNullOrWhiteSpace(contextId) || choices == null || choices.Count == 0 ||
            choices.Any(value => value == null) || _resolvedConsumableRewardContexts.Contains(contextId)) return false;
        var distinctChoices = choices.Distinct().ToArray();
        if (distinctChoices.Length == 0) return false;
        _resolvedConsumableRewardContexts.Add(contextId);
        _pendingConsumableRewards.Enqueue(new ConsumableRewardOffer(contextId, sourceLabel, distinctChoices));
        GameplayInputGate.Set(GameplayInputBlockReason.ConsumableReward, true);
        OnConsumableRewardChanged?.Invoke();
        return true;
    }

    bool QueueRandomNonEnhancementReward(string contextId, string sourceLabel, string streamName, int context)
    {
        var cards = CardManager.Instance;
        if (cards == null || _randomContext == null) return false;
        var candidates = cards.consumableCatalog
            .Where(value => value != null && value.effectType != ConsumableEffectType.ApplyEnhancement)
            .OrderBy(value => value.id, StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return false;
        var random = _randomContext.CreateStream(streamName, context);
        return QueueConsumableReward(contextId, sourceLabel,
            new[] { candidates[random.NextInt(0, candidates.Length)] });
    }

    void QueueAlchemistRewardForCurrentMap()
    {
        var cards = CardManager.Instance;
        int mapIndex = CurrentMapIndex;
        if (cards == null || _randomContext == null ||
            !GearRules.OffersAlchemistRewards(cards)) return;
        if (!_alchemistChoicesByMap.TryGetValue(mapIndex, out var choices))
        {
            var candidates = cards.consumableCatalog
                .Where(value => value != null && value.effectType == ConsumableEffectType.ApplyEnhancement)
                .OrderBy(value => value.id, StringComparer.Ordinal).ToList();
            if (candidates.Count == 0) return;
            candidates.Shuffle(_randomContext.CreateStream("alchemist-options", mapIndex));
            choices = candidates.Take(Mathf.Min(3, candidates.Count)).ToArray();
            _alchemistChoicesByMap.Add(mapIndex, choices);
        }
        QueueConsumableReward($"alchemist:{mapIndex}", "Alchemist’s Kit", choices);
    }

    public string GetEnhancementTargetUnavailableReason(int cardId)
    {
        var card = CardManager.Instance != null ? CardManager.Instance.FindOwnedCard(cardId) : null;
        if (card == null) return "Target card unavailable";
        return card.Enhancement != null ? "Target already enhanced" : string.Empty;
    }

    void ApplyVictoryGearRewards(bool wasHeadliner)
    {
        var cards = CardManager.Instance;
        if (cards == null) return;

        bool wasElite = !wasHeadliner && _activeNode != null && _activeNode.kind == MapNodeType.Elite &&
            (CombatManager.Instance?.EliteKillCount ?? 0) > 0;
        GearRules.OnEncounterWon(cards, CombatManager.Instance, wasElite, wasHeadliner);
        if (!GearRules.CountsWinsForSatchelRewards(cards)) return;
        _combatWinCount = IncrementToMaximum(_combatWinCount);
        if (_combatWinCount % 3 != 0) return;
        _satchelRewardCount = IncrementToMaximum(_satchelRewardCount);
        QueueRandomNonEnhancementReward(
            $"scavenger-satchel:{_satchelRewardCount}", "Scavenger’s Satchel",
            "scavenger-satchel", _satchelRewardCount);
    }

    internal bool TryGrantConsumableDropForNode(PathNode node)
    {
        var cards = CardManager.Instance;
        if (node == null || cards == null || _randomContext == null) return false;
        int context = unchecked(node.mapIndex * 7919 ^ node.id);
        if (!_consumableDropResolvedContexts.Add(context)) return false;
        if (IsTutorialRun) return GrantEnemyDrop(TutorialDropFor(node));
        var candidates = cards.consumableCatalog
            .Where(value => value != null && value.dropAvailable && value.dropChance > 0f &&
                value.effectType != ConsumableEffectType.ApplyEnhancement)
            .OrderBy(value => value.id, StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return false;
        var random = _randomContext.CreateStream("consumable-drops", context);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (random.NextFloat() >= Mathf.Clamp01(candidates[i].dropChance)) continue;
            return GrantEnemyDrop(candidates[i]);
        }
        return false;
    }

    // A drop goes straight into the backpack, or waits on the board while the backpack is full.
    bool GrantEnemyDrop(ConsumableData drop)
    {
        var cards = CardManager.Instance;
        if (drop == null || cards == null) return false;
        if (cards.CanAddConsumable(drop) && cards.AddConsumable(drop))
        {
            OnEnemyDropClaimed?.Invoke(drop);
            return true;
        }
        _pendingEnemyDrop = drop;
        OnEnemyDropChanged?.Invoke();
        return true;
    }
}
