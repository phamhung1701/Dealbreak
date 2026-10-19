using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// RunManager: Trick reward queue, enemy drops, Gear victory rewards and Upgrade (Training) offers.
public partial class RunManager
{
    readonly List<ShopOffer> _activeUpgradeOffers = new();
    readonly HashSet<int> _consumableDropResolvedContexts = new();
    readonly Queue<ConsumableRewardOffer> _pendingConsumableRewards = new();
    readonly HashSet<string> _resolvedConsumableRewardContexts = new(StringComparer.Ordinal);
    readonly Dictionary<int, ConsumableData[]> _alchemistChoicesByMap = new();
    int _combatWinCount;
    int _satchelRewardCount;
    public int CombatWinCount => _combatWinCount;

    public ConsumableRewardOffer PendingConsumableReward =>
        _pendingConsumableRewards.Count > 0 ? _pendingConsumableRewards.Peek() : null;

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

    public IReadOnlyList<ShopOffer> GetCurrentUpgradeOffers() => _activeUpgradeOffers;

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

    // Shop, Upgrade, and direct Event acquisitions deliberately target the current hand only.
    public string GetEnhancementAcquisitionTargetUnavailableReason(int cardId, CardEnhancementData enhancement = null)
    {
        var cards = CardManager.Instance;
        var card = cards != null ? cards.FindOwnedCard(cardId) : null;
        if (card == null || !cards.hand.Contains(card)) return "Target must be a card in your hand";
        return enhancement != null && ReferenceEquals(card.Enhancement, enhancement)
            ? "Card already has this Mod" : string.Empty;
    }

    public bool ChooseUpgradeOffer(int offerIndex, int cardId, bool replaceExisting = false)
    {
        if (GameplayInputGate.IsBlocked || _activeNode == null || _activeNode.completed ||
            _activeNode.kind != MapNodeType.Upgrade || offerIndex < 0 || offerIndex >= _activeUpgradeOffers.Count)
            return false;
        var offer = _activeUpgradeOffers[offerIndex];
        if (offer == null || !string.IsNullOrEmpty(GetEnhancementAcquisitionTargetUnavailableReason(cardId, offer.enhancement)) ||
            offer.kind != ShopOfferKind.Enhancement || offer.enhancement == null ||
            !CardManager.Instance.ApplyEnhancement(cardId, offer.enhancement, replaceExisting))
            return false;

        OnHideUpgrade?.Invoke();
        _activeUpgradeOffers.Clear();
        CompleteActiveNode();
        ShowPathAfterNode();
        return true;
    }

    void PrepareUpgradeOffers(int maximumOffers = 3)
    {
        _activeUpgradeOffers.Clear();
        if (_activeNode == null || _activeNode.kind != MapNodeType.Upgrade) return;
        int context = unchecked(_activeNode.mapIndex * 613 ^ _activeNode.id);
        AddEnhancementOffers(
            _activeUpgradeOffers,
            maximumOffers,
            "upgrade-cards",
            "upgrade-enhancements",
            context,
            true);
    }

    void AddEnhancementOffers(
        List<ShopOffer> destination,
        int maximumOffers,
        string cardStream,
        string enhancementStream,
        int context,
        bool free,
        Func<CardEnhancementData, bool> eligible = null)
    {
        var cards = CardManager.Instance;
        if (cards == null || maximumOffers <= 0 || cards.enhancementCatalog.Count == 0) return;

        var targets = cards.ownedCards
            .Where(card => card != null && card.Enhancement == null)
            .OrderBy(card => card.Id)
            .ToList();
        var enhancements = cards.enhancementCatalog
            .Where(enhancement => enhancement != null && (eligible == null || eligible(enhancement)))
            .OrderBy(enhancement => enhancement.id, StringComparer.Ordinal)
            .ToList();
        if (targets.Count == 0 || enhancements.Count == 0) return;

        // Keep the original independent card stream's offer-generation consumption,
        // but never bind its shuffled card to the purchased Enhancement.
        targets.Shuffle(_randomContext.CreateStream(cardStream, context));
        enhancements.Shuffle(_randomContext.CreateStream(enhancementStream, context));
        int count = Mathf.Min(maximumOffers, targets.Count);
        for (int i = 0; i < count; i++)
        {
            var enhancement = enhancements[i % enhancements.Count];
            destination.Add(new ShopOffer
            {
                kind = ShopOfferKind.Enhancement,
                enhancement = enhancement,
                slot = i,
                basePrice = free ? 0 : enhancement.price,
                price = free ? 0 : CalculateEffectiveShopPrice(ShopOfferKind.Enhancement, enhancement.price)
            });
        }
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
        var candidates = cards.consumableCatalog
            .Where(value => value != null && value.dropAvailable && value.dropChance > 0f &&
                value.effectType != ConsumableEffectType.ApplyEnhancement)
            .OrderBy(value => value.id, StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return false;
        var random = _randomContext.CreateStream("consumable-drops", context);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (random.NextFloat() >= Mathf.Clamp01(candidates[i].dropChance)) continue;
            var selected = candidates[i];
            if (cards.CanAddConsumable(selected) && cards.AddConsumable(selected)) return true;
            return QueueConsumableReward($"enemy-drop:{context}", "Enemy Drop", new[] { selected });
        }
        return false;
    }
}
