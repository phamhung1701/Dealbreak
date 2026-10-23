using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// RunManager: Incident/Risk choices, Blood Ritual, Hammer rewards and Gear discard choices.
public partial class RunManager
{
    readonly List<CardEnhancementData> _pendingHammerEnhancements = new();
    RunEventDefinition _pendingHammerEvent;
    bool _pendingHammerReward;
    bool _hammerRetryPending;
    RunEventDefinition _pendingBloodRitualEvent;
    GearData _pendingBloodRitualGear;
    GearData[] _pendingBloodRitualReplacements = Array.Empty<GearData>();
    int _pendingBloodRitualCost;
    bool _pendingBloodRitualSetMaxHealth;
    bool _pendingBloodRitualReward;

    public bool RequestGearDiscardChoice(string title, string description,
        IReadOnlyList<CardInstance> choices, Action<int> onResolved)
    {
        if (OnShowGearDiscardChoice == null || choices == null || choices.Count == 0 || onResolved == null)
            return false;
        bool resolved = false;
        GameplayInputGate.Set(GameplayInputBlockReason.GearChoice, true);
        OnShowGearDiscardChoice.Invoke(title, description, choices, cardId =>
        {
            if (resolved) return;
            resolved = true;
            GameplayInputGate.Set(GameplayInputBlockReason.GearChoice, false);
            onResolved(cardId);
        });
        return true;
    }

    public bool IsPendingHammerReward => _pendingHammerReward;

    string GetBloodRitualUnavailableReason(int choiceIndex)
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (cards == null || combat == null) return "Ritual unavailable";
        var balance = GameBalance.Current.redlineDeal;
        if (choiceIndex == 0)
            return GetRitualGearCandidates(balance.allInGearRarity).Length > 0
                ? string.Empty : $"No eligible {balance.allInGearRarity} Gear reward";
        if (choiceIndex == 1)
            return combat.player.currentHealth > balance.bigCutHpCost && GetRitualGearCandidates(balance.bigCutGearRarity).Length > 0
                ? string.Empty : combat.player.currentHealth <= balance.bigCutHpCost
                    ? $"Requires more than {balance.bigCutHpCost} HP" : $"No eligible {balance.bigCutGearRarity} Gear reward";
        if (choiceIndex == 2)
            return combat.player.currentHealth > balance.smallCutHpCost && GetRitualUpgradeCandidates().Length > 0
                ? string.Empty : combat.player.currentHealth <= balance.smallCutHpCost
                    ? $"Requires more than {balance.smallCutHpCost} HP" : "No eligible Gear upgrade";
        return "Choice unavailable";
    }

    GearData[] GetRitualGearCandidates(string rarity)
    {
        var cards = CardManager.Instance;
        if (cards == null) return Array.Empty<GearData>();
        return cards.gearCatalog.Where(item => item != null &&
                string.Equals(item.rarity, rarity, StringComparison.OrdinalIgnoreCase) &&
                !cards.HasGear(item) &&
                (item.tier <= 1 || cards.ownedGear.Any(owned => owned != null &&
                    string.Equals(owned.canonicalId, item.upgradeFromId, StringComparison.Ordinal))))
            .OrderBy(item => item.canonicalId, StringComparer.Ordinal)
            .ThenBy(item => item.id, StringComparer.Ordinal).ToArray();
    }

    GearData[] GetRitualUpgradeCandidates()
    {
        var cards = CardManager.Instance;
        if (cards == null) return Array.Empty<GearData>();
        return cards.ownedGear.Where(item => item != null)
            .Select(item => new { Owned = item, Upgrade = GearUpgradeResolver.FindImmediateUpgrade(item, cards.gearCatalog) })
            .Where(pair => pair.Upgrade != null && !cards.HasGear(pair.Upgrade))
            .Select(pair => pair.Upgrade)
            .OrderBy(item => item.canonicalId, StringComparer.Ordinal)
            .ThenBy(item => item.id, StringComparer.Ordinal).ToArray();
    }

    bool ResolveBloodRitualOption(int choiceIndex)
    {
        if (_pendingBloodRitualReward || !string.IsNullOrEmpty(GetBloodRitualUnavailableReason(choiceIndex))) return false;
        var combat = CombatManager.Instance;
        var balance = GameBalance.Current.redlineDeal;
        var candidates = choiceIndex switch
        {
            0 => GetRitualGearCandidates(balance.allInGearRarity),
            1 => GetRitualGearCandidates(balance.bigCutGearRarity),
            _ => GetRitualUpgradeCandidates()
        };
        if (candidates.Length == 0) return false;
        int context = unchecked((_activeNode?.mapIndex ?? CurrentMapIndex) * 7919 ^
            (_activeNode?.id ?? 0) * 397 ^ choiceIndex);
        var random = (_randomContext ?? new RunRandomContext(runSeed))
            .CreateStream("blood-ritual-reward", context);
        _pendingBloodRitualGear = candidates[random.NextInt(0, candidates.Length)];
        _pendingBloodRitualCost = choiceIndex == 1 ? balance.bigCutHpCost : choiceIndex == 2 ? balance.smallCutHpCost : 0;
        _pendingBloodRitualSetMaxHealth = choiceIndex == 0;
        _pendingBloodRitualReward = true;

        var cards = CardManager.Instance;
        if (cards.GearSlotsUsed >= cards.GearCapacity && _pendingBloodRitualGear.OccupiesCapacitySlot)
            return ShowBloodRitualReplacementPrompt();
        bool committed = CommitBloodRitualReward(null);
        ClearPendingBloodRitual();
        if (committed) CompleteEventChoice();
        return committed;
    }

    bool ShowBloodRitualReplacementPrompt()
    {
        var cards = CardManager.Instance;
        var gear = _pendingBloodRitualGear;
        if (cards == null || gear == null) return false;
        _pendingBloodRitualReplacements = gear.tier > 1
            ? cards.ownedGear.Where(item => item != null &&
                string.Equals(item.canonicalId, gear.upgradeFromId, StringComparison.Ordinal)).ToArray()
            : cards.ownedGear.Where(item => item != null && item.OccupiesCapacitySlot).ToArray();
        if (_pendingBloodRitualReplacements.Length == 0)
        {
            ClearPendingBloodRitual();
            return false;
        }

        _pendingBloodRitualEvent = ScriptableObject.CreateInstance<RunEventDefinition>();
        _pendingBloodRitualEvent.id = $"blood-ritual-replace:{_activeNode?.id ?? 0}";
        _pendingBloodRitualEvent.category = MapNodeType.Risk;
        _pendingBloodRitualEvent.flow = RunEventFlow.BloodRitualReplacement;
        _pendingBloodRitualEvent.title = "REDLINE DEAL — CLAIM YOUR REWARD";
        _pendingBloodRitualEvent.description = $"Choose a Gear item to replace with {_pendingBloodRitualGear.displayName}, or cancel without paying the cost.";
        _pendingBloodRitualEvent.choices = _pendingBloodRitualReplacements
            .Select(item => new RunEventChoice
            {
                label = $"Replace {item.displayName}",
                description = $"Replace it with {_pendingBloodRitualGear.displayName}.",
                effects = Array.Empty<RunEffect>(),
                interaction = RunEventChoiceInteraction.Immediate
            })
            .Concat(new[] { new RunEventChoice
            {
                label = "Cancel Ritual",
                description = "Keep your Gear. The ritual cost is not paid.",
                effects = Array.Empty<RunEffect>(),
                interaction = RunEventChoiceInteraction.Immediate
            }}).ToArray();
        _activeEvent = _pendingBloodRitualEvent;
        if (OnShowEvent == null)
        {
            _activeEvent = null;
            ClearPendingBloodRitual();
            return false;
        }
        OnShowEvent.Invoke(_activeEvent);
        return true;
    }

    bool ResolveBloodRitualReplacement(int choiceIndex)
    {
        if (!_pendingBloodRitualReward || _pendingBloodRitualEvent == null ||
            choiceIndex < 0 || choiceIndex >= _activeEvent.choices.Length) return false;
        bool cancel = choiceIndex == _pendingBloodRitualReplacements.Length;
        bool success = cancel || CommitBloodRitualReward(_pendingBloodRitualReplacements[choiceIndex]);
        if (!success) return false;
        OnHideEvent?.Invoke();
        _activeEvent = null;
        ClearPendingBloodRitual();
        CompleteActiveNode();
        ShowPathAfterNode();
        return true;
    }

    bool CommitBloodRitualReward(GearData replacement)
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        var gear = _pendingBloodRitualGear;
        if (!_pendingBloodRitualReward || cards == null || combat == null || gear == null ||
            _pendingBloodRitualCost > 0 && !combat.CanTakeRunDamage(_pendingBloodRitualCost)) return false;
        if (!cards.GrantGearReward(gear, replacement)) return false;
        if (_pendingBloodRitualSetMaxHealth)
            combat.SetPlayerMaxHealthAndClamp(GameBalance.Current.redlineDeal.allInMaxHealth);
        else if (_pendingBloodRitualCost > 0)
            combat.TakeRunDamage(_pendingBloodRitualCost);
        return true;
    }

    void ClearPendingBloodRitual()
    {
        if (_pendingBloodRitualEvent != null) DestroyRuntimeObject(_pendingBloodRitualEvent);
        _pendingBloodRitualEvent = null;
        _pendingBloodRitualGear = null;
        _pendingBloodRitualReplacements = Array.Empty<GearData>();
        _pendingBloodRitualCost = 0;
        _pendingBloodRitualSetMaxHealth = false;
        _pendingBloodRitualReward = false;
    }

    public bool CanChooseEventOption(int choiceIndex)
    {
        return string.IsNullOrEmpty(GetEventOptionUnavailableReason(choiceIndex));
    }

    public string GetEventOptionUnavailableReason(int choiceIndex)
    {
        if (_activeEvent == null || _activeEvent.choices == null ||
            choiceIndex < 0 || choiceIndex >= _activeEvent.choices.Length)
            return "Choice unavailable";

        switch (_activeEvent.flow)
        {
            case RunEventFlow.BloodRitual:
                return GetBloodRitualUnavailableReason(choiceIndex);
            case RunEventFlow.BloodRitualReplacement:
                return string.Empty;
            case RunEventFlow.ContractSelection:
                return GetContractChoiceUnavailableReason(choiceIndex);
        }
        var choice = _activeEvent.choices[choiceIndex];
        if (choice == null) return "Choice unavailable";
        if (IsHammerEvent())
            return choice.interaction is RunEventChoiceInteraction.HammerRetry or RunEventChoiceInteraction.HammerDecline
                ? string.Empty : choice.interaction == RunEventChoiceInteraction.ChooseEnhancementTarget
                    ? _pendingHammerEnhancements.Count == GearRules.ModBenchChoices ? GetModRewardUnavailableReason(_pendingHammerEnhancements)
                        : "Choice unavailable"
                    : "Choice unavailable";
        if (choice.interaction == RunEventChoiceInteraction.ChooseEnhancementTarget)
            return GetModRewardUnavailableReason(GetEventEnhancements());
        if (choice.interaction == RunEventChoiceInteraction.DiscardTwoForRandomEnhancement)
        {
            var cards = CardManager.Instance;
            if (cards == null || !cards.ownedCards.Any(card => card != null && card.Enhancement == null))
                return "No eligible cards to enhance";
            if (!GetEventEnhancements().Any()) return "No enhancements available";
            if (cards.hand.Count < 2) return "Need at least 2 cards in hand";
            return string.Empty;
        }

        if (choice.interaction == RunEventChoiceInteraction.DiscardCardsForTotalValue)
        {
            var cards = CardManager.Instance;
            if (cards == null) return "Choice unavailable";
            return HasHandCombinationForValue(cards, choice.requiredDiscardValue)
                ? string.Empty
                : $"No hand combination totals {choice.requiredDiscardValue}";
        }

        return RunEffectResolver.GetFailureReason(
            choice, CardManager.Instance, CombatManager.Instance, this);
    }

    public IReadOnlyList<CardEnhancementData> GetEventEnhancementsForChoice(int choiceIndex) =>
        IsHammerEvent() && IsActiveEventChoice(choiceIndex, RunEventChoiceInteraction.ChooseEnhancementTarget)
            ? _pendingHammerEnhancements
            : IsActiveEventChoice(choiceIndex, RunEventChoiceInteraction.ChooseEnhancementTarget)
                ? GetEventEnhancements()
                : Array.Empty<CardEnhancementData>();

    // An earned Mod arrives as its Mod Trick, applied later by dropping it on a hand card.
    static string GetModRewardUnavailableReason(IReadOnlyList<CardEnhancementData> mods)
    {
        var cards = CardManager.Instance;
        if (cards == null || mods == null) return "Choice unavailable";
        bool any = false;
        foreach (var mod in mods)
        {
            var trick = cards.FindModTrick(mod);
            if (trick == null) continue;
            any = true;
            if (cards.CanAddConsumable(trick)) return string.Empty;
        }
        return any ? "Trick slots full" : "No Mods available";
    }

    /// <summary>Why one Mod of a Mod choice cannot be taken now, or empty when it can.</summary>
    public string GetEventModUnavailableReason(int choiceIndex, CardEnhancementData mod)
    {
        var cards = CardManager.Instance;
        if (cards == null || mod == null || !GetEventEnhancementsForChoice(choiceIndex).Contains(mod))
            return "Choice unavailable";
        var trick = cards.FindModTrick(mod);
        if (trick == null) return "Mod unavailable";
        return cards.CanAddConsumable(trick) ? string.Empty : "Trick slots full";
    }

    public bool ChooseEventMod(int choiceIndex, CardEnhancementData mod)
    {
        if (GameplayInputGate.IsBlocked &&
                !(IsHammerEvent() && GameplayInputGate.Reasons == GameplayInputBlockReason.GearChoice) ||
            !IsActiveEventChoice(choiceIndex, RunEventChoiceInteraction.ChooseEnhancementTarget) ||
            !string.IsNullOrEmpty(GetEventModUnavailableReason(choiceIndex, mod))) return false;
        if (!CardManager.Instance.AddConsumable(CardManager.Instance.FindModTrick(mod))) return false;
        if (IsHammerEvent()) CompleteHammerReward();
        else CompleteEventChoice();
        return true;
    }

    public bool ChooseEventDiscardAndReward(int choiceIndex, IReadOnlyList<int> cardIds)
    {
        var cards = CardManager.Instance;
        if (GameplayInputGate.IsBlocked || cards == null || !IsActiveEventChoice(choiceIndex,
                RunEventChoiceInteraction.DiscardTwoForRandomEnhancement) || cardIds == null ||
            cardIds.Count != 2 || cardIds[0] == cardIds[1] ||
            !string.IsNullOrEmpty(GetEventOptionUnavailableReason(choiceIndex))) return false;

        CardInstance target = null;
        foreach (var card in cards.ownedCards)
            if (card != null && card.Enhancement == null && (target == null || card.Id < target.Id))
                target = card;
        var enhancements = GetEventEnhancements();
        if (target == null || enhancements.Length == 0) return false;

        var random = CreateEventRandom(choiceIndex);
        var enhancement = enhancements[random.NextInt(0, enhancements.Length)];
        target = cards.ownedCards
            .Where(card => card != null && card.Enhancement == null)
            .OrderBy(card => card.Id)
            .ElementAt(random.NextInt(0, cards.ownedCards.Count(card => card != null && card.Enhancement == null)));

        if (!cards.TryReturnToDrawPileByIds(cardIds)) return false;
        if (!cards.ApplyEnhancement(target.Id, enhancement))
        {
            Debug.LogError("RunManager: random event enhancement could not be applied after validated discard.", this);
            return false;
        }
        CompleteEventChoice();
        return true;
    }

    static bool HasHandCombinationForValue(CardManager cards, int requiredValue)
    {
        if (cards == null || requiredValue <= 0) return false;
        var reachable = new bool[requiredValue + 1];
        reachable[0] = true;
        foreach (var card in cards.hand)
        {
            int value = card != null ? card.BaseAttackValue : 0;
            if (value <= 0 || value > requiredValue) continue;
            for (int total = requiredValue; total >= value; total--)
                if (reachable[total - value]) reachable[total] = true;
        }
        return reachable[requiredValue];
    }

    public bool CanChooseEventDiscardForTotalValue(int choiceIndex, IReadOnlyList<int> cardIds)
    {
        var cards = CardManager.Instance;
        if (!IsActiveEventChoice(choiceIndex, RunEventChoiceInteraction.DiscardCardsForTotalValue) ||
            cards == null || cardIds == null || cardIds.Count == 0 ||
            !string.IsNullOrEmpty(GetEventOptionUnavailableReason(choiceIndex))) return false;

        int requiredValue = _activeEvent.choices[choiceIndex].requiredDiscardValue;
        int total = 0;
        var unique = new HashSet<int>();
        for (int i = 0; i < cardIds.Count; i++)
        {
            int id = cardIds[i];
            if (!unique.Add(id)) return false;
            var card = cards.FindOwnedCard(id);
            if (card == null || !cards.hand.Contains(card)) return false;
            total += card.BaseAttackValue;
            if (total > requiredValue) return false;
        }
        return total == requiredValue;
    }

    public bool ChooseEventDiscardForTotalValue(int choiceIndex, IReadOnlyList<int> cardIds)
    {
        var cards = CardManager.Instance;
        if (GameplayInputGate.IsBlocked || cards == null ||
            !CanChooseEventDiscardForTotalValue(choiceIndex, cardIds) ||
            !cards.TryReturnToDrawPileByIds(cardIds)) return false;
        CompleteEventChoice();
        return true;
    }

    bool IsActiveEventChoice(int choiceIndex, RunEventChoiceInteraction interaction) =>
        _activeEvent != null && _activeEvent.choices != null && choiceIndex >= 0 &&
        choiceIndex < _activeEvent.choices.Length && _activeEvent.choices[choiceIndex] != null &&
        _activeEvent.choices[choiceIndex].interaction == interaction &&
        (IsHammerEvent() || _activeNode != null && !_activeNode.completed);

    internal bool TryGrantRandomSwampReward()
    {
        var cards = CardManager.Instance;
        if (cards == null) return false;
        var random = CreateEventRandom(0, "swamp-event-reward");
        int rewardType = random.NextInt(0, 3);
        if (rewardType == 0)
        {
            var gearItems = cards.gearCatalog
                .Where(value => value != null &&
                    string.IsNullOrEmpty(cards.GetGearAcquisitionUnavailableReason(value)))
                .OrderBy(value => value.canonicalId, StringComparer.Ordinal)
                .ThenBy(value => value.id, StringComparer.Ordinal).ToArray();
            if (gearItems.Length > 0)
                cards.BuyGear(gearItems[random.NextInt(0, gearItems.Length)], 0);
            return true;
        }

        if (rewardType == 1)
        {
            var enhancements = GetEventEnhancements();
            var eligibleCards = cards.ownedCards
                .Where(card => card != null && card.Enhancement == null)
                .OrderBy(card => card.Id).ToArray();
            if (enhancements.Length > 0 && eligibleCards.Length > 0)
            {
                var enhancement = enhancements[random.NextInt(0, enhancements.Length)];
                var card = eligibleCards[random.NextInt(0, eligibleCards.Length)];
                cards.ApplyEnhancement(card.Id, enhancement);
            }
        }
        return true;
    }

    CardEnhancementData[] GetEventEnhancements() => CardManager.Instance == null
        ? Array.Empty<CardEnhancementData>()
        : CardManager.Instance.enhancementCatalog.Where(value => value != null)
            .OrderBy(value => value.canonicalId, StringComparer.Ordinal)
            .ThenBy(value => value.id, StringComparer.Ordinal).ToArray();

    /// <summary>A stream keyed by the active node, for a once-per-node pick that needs no saved state.</summary>
    internal IRandomSource CreateActiveNodeRandom(string streamName) => CreateEventRandom(0, streamName);

    IRandomSource CreateEventRandom(int choiceIndex, string streamName = "run-event-enhancement")
    {
        if (_randomContext == null) _randomContext = new RunRandomContext(runSeed);
        int nodeId = _activeNode != null ? _activeNode.id : 0;
        int mapIndex = _activeNode != null ? _activeNode.mapIndex : 0;
        int context = unchecked(mapIndex * 7919 ^ nodeId * 397 ^ choiceIndex);
        return _randomContext.CreateStream(streamName, context);
    }

    bool IsHammerEvent() => _pendingHammerReward && _activeEvent != null &&
        ReferenceEquals(_activeEvent, _pendingHammerEvent);

    void TryOpenHammerReward()
    {
        var cards = CardManager.Instance;
        var instance = GearRules.FindHammer(cards);
        if (cards == null || instance == null) return;
        if (!_pendingHammerReward)
        {
            int last = instance.State.GetCounter(GearCounterKeys.HammerLastRewardNode);
            if (_completedNodeCount - last < GearRules.ModBenchNodesPerReward) return;
            var common = cards.enhancementCatalog
                .Where(value => value != null && string.Equals(value.rarity, "Common", StringComparison.Ordinal))
                .OrderBy(value => value.canonicalId, StringComparer.Ordinal)
                .ThenBy(value => value.id, StringComparer.Ordinal).ToList();
            if (common.Count < GearRules.ModBenchChoices) return;
            if (_randomContext == null) _randomContext = new RunRandomContext(runSeed);
            var random = _randomContext.CreateStream("hammer-common-enhancements", _completedNodeCount);
            _pendingHammerEnhancements.Clear();
            for (int i = 0; i < GearRules.ModBenchChoices; i++)
            {
                int selected = random.NextInt(0, common.Count);
                _pendingHammerEnhancements.Add(common[selected]);
                common.RemoveAt(selected);
            }
            instance.State.SetCounter(GearCounterKeys.HammerLastRewardNode, _completedNodeCount);
            _pendingHammerReward = true;
        }
        else if (!_hammerRetryPending) return;

        bool hasTarget = string.IsNullOrEmpty(GetModRewardUnavailableReason(_pendingHammerEnhancements));
        if (_hammerRetryPending && !hasTarget) return;
        _hammerRetryPending = false;
        _pendingHammerEvent = ScriptableObject.CreateInstance<RunEventDefinition>();
        _pendingHammerEvent.id = $"artifact-hammer:{_completedNodeCount}";
        string benchName = instance.Definition.displayName;
        _pendingHammerEvent.title = benchName.ToUpperInvariant();
        _pendingHammerEvent.category = MapNodeType.Event;
        _pendingHammerEvent.flow = RunEventFlow.HammerReward;
        _pendingHammerEvent.description = hasTarget
            ? $"Choose one of {GearRules.ModBenchChoices} cached Common Mods."
            : "Trick slots are full. Retry after another node, or decline this reward.";
        _pendingHammerEvent.choices = hasTarget
            ? new[]
            {
                new RunEventChoice { label = "Choose Mod", description = $"Take one of the {GearRules.ModBenchChoices} cached Common Mods as a Trick.", interaction = RunEventChoiceInteraction.ChooseEnhancementTarget }
            }
            : new[]
            {
                new RunEventChoice { label = "Retry Later", description = "Continue the run and retry when a Trick slot is free.", interaction = RunEventChoiceInteraction.HammerRetry },
                new RunEventChoice { label = "Decline", description = $"Leave the {benchName} reward unused.", interaction = RunEventChoiceInteraction.HammerDecline }
            };
        _activeEvent = _pendingHammerEvent;
        GameplayInputGate.Set(GameplayInputBlockReason.GearChoice, true);
        if (OnShowEvent == null)
        {
            GameplayInputGate.Set(GameplayInputBlockReason.GearChoice, false);
            _activeEvent = null;
            DestroyRuntimeObject(_pendingHammerEvent);
            _pendingHammerEvent = null;
            _hammerRetryPending = true;
            return;
        }
        OnShowEvent.Invoke(_activeEvent);
    }

    bool FinishHammerEvent(bool retry)
    {
        if (!IsHammerEvent()) return false;
        OnHideEvent?.Invoke();
        _activeEvent = null;
        GameplayInputGate.Set(GameplayInputBlockReason.GearChoice, false);
        DestroyRuntimeObject(_pendingHammerEvent);
        _pendingHammerEvent = null;
        if (retry)
            _hammerRetryPending = true;
        else
        {
            _pendingHammerReward = false;
            _hammerRetryPending = false;
            _pendingHammerEnhancements.Clear();
        }
        ShowPathAfterNode();
        return true;
    }

    public bool RetryHammerReward() => FinishHammerEvent(retry: true);
    public bool DeclineHammerReward() => FinishHammerEvent(retry: false);

    void CompleteHammerReward()
    {
        if (!IsHammerEvent()) return;
        FinishHammerEvent(retry: false);
    }

    void CompleteEventChoice()
    {
        OnHideEvent?.Invoke();
        _activeEvent = null;
        CompleteActiveNode();
        ShowPathAfterNode();
    }

    /// <summary>
    /// No choice of an authored Incident can be taken (say, too few Credits and nothing left to scout):
    /// it can then be left with nothing gained or lost, so the run never stalls.
    /// </summary>
    public bool CanLeaveBlockedEvent
    {
        get
        {
            if (_activeEvent?.choices == null || _activeEvent.flow != RunEventFlow.Authored || IsHammerEvent()) return false;
            for (int i = 0; i < _activeEvent.choices.Length; i++)
                if (CanChooseEventOption(i)) return false;
            return true;
        }
    }

    public bool LeaveBlockedEvent()
    {
        if (GameplayInputGate.IsBlocked || !CanLeaveBlockedEvent) return false;
        CompleteEventChoice();
        return true;
    }

    public bool ChooseEventOption(int choiceIndex)
    {
        if (GameplayInputGate.IsBlocked || !CanChooseEventOption(choiceIndex) ||
            _activeEvent.choices[choiceIndex].interaction != RunEventChoiceInteraction.Immediate) return false;
        switch (_activeEvent.flow)
        {
            case RunEventFlow.BloodRitual: return ResolveBloodRitualOption(choiceIndex);
            case RunEventFlow.BloodRitualReplacement: return ResolveBloodRitualReplacement(choiceIndex);
            case RunEventFlow.ContractSelection: return ResolveContractSelection(choiceIndex);
        }
        if (!RunEffectResolver.Apply(
                _activeEvent.choices[choiceIndex], CardManager.Instance, CombatManager.Instance, this))
            return false;

        CompleteEventChoice();
        return true;
    }
}
