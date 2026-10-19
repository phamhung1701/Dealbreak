using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// RunManager: Quest Contracts bought at the Black Market.
public partial class RunManager
{
    RunContractState _activeContract;
    RunEventDefinition _pendingContractSelectionEvent;
    int _pendingContractPrice;
    public RunContractState ActiveContract => _activeContract;

    // Contract selection choices, in display order; the final choice is Cancel.
    static readonly RunContractObjective[] ContractChoices =
    {
        RunContractObjective.WinTwoEncounters,
        RunContractObjective.DefeatEliteThisMap,
        RunContractObjective.WinCombatWithoutHpLoss
    };

    static bool IsContractCancel(int choiceIndex) => choiceIndex == ContractChoices.Length;

    string GetContractChoiceUnavailableReason(int choiceIndex)
    {
        if (IsContractCancel(choiceIndex)) return string.Empty;
        var cards = CardManager.Instance;
        if (cards == null || !cards.CanAfford(_pendingContractPrice))
            return $"Need {_pendingContractPrice}g to accept the Contract";
        return ContractChoices[choiceIndex] == RunContractObjective.DefeatEliteThisMap && !CanReachEliteAfterActiveNode()
            ? "No reachable Enforcer remains in this District" : string.Empty;
    }

    bool ShowContractSelection(int pendingPrice)
    {
        if (_activeNode == null || _activeNode.kind != MapNodeType.Shop || _activeContract != null ||
            _pendingContractSelectionEvent != null || OnShowEvent == null) return false;
        var selection = ScriptableObject.CreateInstance<RunEventDefinition>();
        selection.id = $"quest-contract-select:{_activeNode.mapIndex}:{_activeNode.id}";
        selection.category = MapNodeType.Event;
        selection.flow = RunEventFlow.ContractSelection;
        selection.title = "QUEST CONTRACT";
        selection.description = "Choose one objective. The Contract occupies no Gear slot; its 20 Credits fee is charged only when you confirm an objective. Cancel keeps your Credits.";
        selection.choices = new[]
        {
            new RunEventChoice { label = "Combat Contract", description = "Win 2 encounters within 12 Districts. Reward: 6 Credits. Failure: lose 5 HP.", effects = Array.Empty<RunEffect>() },
            new RunEventChoice { label = "Enforcer Contract", description = "Defeat a reachable Enforcer before leaving this District. Reward: a Rare Gear item, or heal 5 HP if none is eligible. Failure: lose 5 HP.", effects = Array.Empty<RunEffect>() },
            new RunEventChoice { label = "Flawless Contract", description = "Win 1 encounter without losing HP. Reward: 10 Credits. Failure: lose 5 HP.", effects = Array.Empty<RunEffect>() },
            new RunEventChoice { label = "Cancel", description = "Return to the Black Market. The Contract fee is not charged.", effects = Array.Empty<RunEffect>() }
        };
        _pendingContractPrice = Math.Max(0, pendingPrice);
        _pendingContractSelectionEvent = selection;
        _activeEvent = selection;
        OnHideShop?.Invoke();
        OnShowEvent.Invoke(selection);
        return true;
    }

    bool CanReachEliteAfterActiveNode()
    {
        if (_activeNode == null || currentPath.Count == 0) return false;
        var pending = new Queue<int>(_activeNode.next);
        var visited = new HashSet<int>();
        while (pending.Count > 0)
        {
            int id = pending.Dequeue();
            if (!visited.Add(id)) continue;
            var node = FindNode(id);
            if (node == null || node.completed || node.mapIndex != CurrentMapIndex) continue;
            if (node.kind == MapNodeType.Elite) return true;
            foreach (int next in node.next) pending.Enqueue(next);
        }
        return false;
    }

    bool ResolveContractSelection(int choiceIndex)
    {
        if (_pendingContractSelectionEvent == null || _activeContract != null ||
            choiceIndex < 0 || choiceIndex > ContractChoices.Length || _activeNode == null ||
            _activeNode.kind != MapNodeType.Shop)
            return false;
        if (IsContractCancel(choiceIndex))
        {
            OnHideEvent?.Invoke();
            _activeEvent = null;
            DestroyRuntimeObject(_pendingContractSelectionEvent);
            _pendingContractSelectionEvent = null;
            _pendingContractPrice = 0;
            OnShowShop?.Invoke();
            return true;
        }
        var objective = ContractChoices[choiceIndex];
        if (objective == RunContractObjective.DefeatEliteThisMap && !CanReachEliteAfterActiveNode())
            return false;
        var cards = CardManager.Instance;
        if (cards == null || !cards.CanAfford(_pendingContractPrice) || !cards.SpendCredits(_pendingContractPrice))
            return false;
        _usedSpecialShopOffers.Add(ShopOfferKind.Contract);
        _activeContract = new RunContractState
        {
            objective = objective,
            startedAtMapIndex = CurrentMapIndex,
            startedAtNodeId = _activeNode.id,
            deadlineMapExclusive = (int)Math.Min(int.MaxValue, (long)CurrentMapIndex + 12L),
            stableId = $"quest:{RunRandomContext.NormalizeSeed(runSeed)}:{_activeNode.mapIndex}:{_activeNode.id}:{choiceIndex}"
        };
        OnHideEvent?.Invoke();
        _activeEvent = null;
        DestroyRuntimeObject(_pendingContractSelectionEvent);
        _pendingContractSelectionEvent = null;
        _pendingContractPrice = 0;
        CompleteActiveNode();
        ShowPathAfterNode();
        Debug.Log($"Contract started: {_activeContract.ObjectiveDescription} Deadline: before District {(long)_activeContract.deadlineMapExclusive + 1L}.", this);
        return true;
    }

    void ProgressContractForVictory(PathNode node, int actualHpLost)
    {
        var contract = _activeContract;
        if (contract == null || node == null) return;
        bool encounter = node.kind is MapNodeType.Combat or MapNodeType.Elite or MapNodeType.Headliner;
        if (contract.objective == RunContractObjective.DefeatEliteThisMap &&
            node.kind == MapNodeType.Elite && node.mapIndex == contract.startedAtMapIndex &&
            (CombatManager.Instance?.EliteKillCount ?? 0) > 0)
        {
            ResolveContractSuccess();
            return;
        }
        if (contract.objective == RunContractObjective.WinCombatWithoutHpLoss && encounter && actualHpLost == 0)
        {
            ResolveContractSuccess();
            return;
        }
        if (contract.objective == RunContractObjective.WinTwoEncounters && encounter)
        {
            contract.encounterWins++;
            if (contract.encounterWins >= 2) ResolveContractSuccess();
        }
    }

    void ResolveContractSuccess()
    {
        var contract = _activeContract;
        if (contract == null) return;
        _activeContract = null;
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        switch (contract.objective)
        {
            case RunContractObjective.WinTwoEncounters:
                cards?.AddCredits(6);
                break;
            case RunContractObjective.WinCombatWithoutHpLoss:
                cards?.AddCredits(10);
                break;
            case RunContractObjective.DefeatEliteThisMap:
                var eligible = cards?.gearCatalog.Where(item => item != null &&
                        string.Equals(item.rarity, "Rare", StringComparison.OrdinalIgnoreCase) &&
                        !cards.HasGear(item) &&
                        (!item.OccupiesCapacitySlot || cards.GearSlotsUsed < cards.GearCapacity) &&
                        (item.tier <= 1 || cards.ownedGear.Any(owned => owned != null &&
                            string.Equals(owned.canonicalId, item.upgradeFromId, StringComparison.Ordinal))) &&
                        string.IsNullOrEmpty(cards.GetGearAcquisitionUnavailableReason(item)))
                    .OrderBy(item => item.canonicalId, StringComparer.Ordinal)
                    .ThenBy(item => item.id, StringComparer.Ordinal).ToArray();
                if (eligible != null && eligible.Length > 0)
                {
                    int context = unchecked(contract.startedAtMapIndex * 7919 ^
                        contract.startedAtNodeId * 397 ^ (int)contract.objective * 31);
                    var random = (_randomContext ?? new RunRandomContext(runSeed)).CreateStream("quest-rare-reward", context);
                    if (!cards.GrantGearReward(eligible[random.NextInt(0, eligible.Length)]))
                    {
                        combat?.HealPlayer(5);
                        Debug.Log("Contract reward could not be collected; healed 5 HP instead.", this);
                    }
                }
                else
                {
                    combat?.HealPlayer(5);
                    Debug.Log("Contract reward: no Rare Gear slot was available; healed 5 HP instead.", this);
                }
                break;
            default:
                break;
        }
        Debug.Log($"Contract completed: {contract.objective}.", this);
    }

    void SettleContractAtMapExit()
    {
        var contract = _activeContract;
        if (contract == null) return;
        bool failed = contract.objective == RunContractObjective.DefeatEliteThisMap &&
                CurrentMapIndex >= contract.startedAtMapIndex ||
            (long)CurrentMapIndex + 1L >= contract.deadlineMapExclusive;
        if (!failed) return;
        _activeContract = null;
        var combat = CombatManager.Instance;
        int hp = combat != null ? combat.player.currentHealth : 0;
        int safeLoss = Math.Min(5, Math.Max(0, hp - 1));
        if (safeLoss > 0) combat.TakeRunDamage(safeLoss);
        Debug.Log($"Contract failed ({contract.objective}); applied {safeLoss} nonlethal HP loss.", this);
    }
}
