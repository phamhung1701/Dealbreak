using System;
using System.Linq;
using UnityEngine;

public partial class RunManager
{
    public bool CanCaptureSave => !IsRunCompleted && HeadlinerTotal == 12 && !string.IsNullOrEmpty(runSeed) &&
        CombatManager.Instance != null && CombatManager.Instance.CanCapturePersistentState &&
        CardManager.Instance != null && !_shopTransaction &&
        (_activeNode == null || _activeNode.kind is MapNodeType.Shop or MapNodeType.Event or MapNodeType.Risk or MapNodeType.Upgrade) &&
        !_pendingHammerReward && !_hammerRetryPending && !_pendingBloodRitualReward &&
        _pendingContractSelectionEvent == null && _pendingConsumableRewards.Count == 0 &&
        (GameplayInputGate.Reasons & (GameplayInputBlockReason.GearChoice | GameplayInputBlockReason.ConsumableReward)) == 0;

    public RunSaveData CaptureSave()
    {
        if (!CanCaptureSave) throw new InvalidOperationException("The run is not at a safe checkpoint.");
        var data = new RunSaveData
        {
            seed = runSeed, bossIndex = headlinerIndex, infiniteMode = IsInfiniteMode,
            completedNodeCount = _completedNodeCount, activeNodeId = _activeNode?.id ?? -1,
            activeEventId = _activeEvent != null ? _activeEvent.id : null,
            cards = CardManager.Instance.CaptureSave(), timing = Timing.CaptureSave(),
            investmentStakes = _pendingInvestmentStakes.ToList(), paidItemPurchases = _paidItemPurchaseCount,
            combatWins = _combatWinCount, merchantGiftRewards = _merchantGiftRewardCount, satchelRewards = _satchelRewardCount,
            visitedMapIndex = _visitedNodeTypesMapIndex, visitedNodeTypes = _visitedNodeTypesCurrentMap.Select(x => (int)x).OrderBy(x => x).ToList(),
            consumableDropContexts = _consumableDropResolvedContexts.OrderBy(x => x).ToList(),
            resolvedRewardContexts = _resolvedConsumableRewardContexts.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            alchemistChoices = _alchemistChoicesByMap.OrderBy(x => x.Key).Select(x => new AlchemistSaveData
                { mapIndex = x.Key, choices = x.Value.Select(item => item.id).ToList() }).ToList(),
            upgradeOffers = _activeUpgradeOffers.Select(CaptureOffer).ToList(),
            midShopGoods = new[] { midShopGoods.x, midShopGoods.y, midShopGoods.z },
            preBossShopGoods = new[] { preHeadlinerShopGoods.x, preHeadlinerShopGoods.y, preHeadlinerShopGoods.z }
        };
        data.hasContract = _activeContract != null;
        if (_activeContract != null) data.contract = new ContractSaveData
        { objective = (int)_activeContract.objective, startedMap = _activeContract.startedAtMapIndex,
            startedNode = _activeContract.startedAtNodeId, deadlineMap = _activeContract.deadlineMapExclusive,
            encounterWins = _activeContract.encounterWins, stableId = _activeContract.stableId };
        data.nodes = currentPath.Select(n => new NodeSaveData { id = n.id, kind = (int)n.kind, type = n.type,
            contentId = n.contentId, mapIndex = n.mapIndex, col = n.col, row = n.row, next = n.next.ToList(),
            completed = n.completed, accessible = n.accessible, revealed = n.revealed, hidden = n.hidden,
            shopCheckpoint = n.isShopCheckpoint }).ToList();
        data.bosses = headlinerDeck.Select(b => new BossSaveData { name = b.enemyName, hp = b.maxHp, attack = b.baseAttack,
            gold = b.creditsReward, rank = (int)b.sourceCard.rank, suit = (int)b.sourceCard.suit,
            encounterCount = b.encounterCount, fleeTurns = b.fleeAfterPlayerTurns,
            abilities = (b.abilities ?? Array.Empty<EnemyAbility>()).Select(a => new AbilitySaveData
            { id = a.id, displayName = a.displayName, description = a.description, effect = (int)a.effect, amount = a.amount }).ToList() }).ToList();
        foreach (var entry in _shopStates.OrderBy(x => x.Key.map).ThenBy(x => x.Key.node))
        {
            var shop = entry.Value;
            data.shops.Add(new ShopSaveData { mapIndex = entry.Key.map, nodeId = entry.Key.node,
                refreshCount = shop.RefreshCount, completed = shop.Completed,
                madePricedPurchase = shop == _activeShopState ? _madeSuccessfulPurchaseInCurrentShop : shop.MadePricedPurchase,
                offers = shop.Offers.Select(CaptureOffer).ToList(), usedServices = shop.UsedServices.Select(x => (int)x).OrderBy(x => x).ToList(),
                purchasedConsumables = shop.PurchasedConsumables.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                purchasedGoods = shop.PurchasedGoods.OrderBy(x => x, StringComparer.Ordinal).ToList() });
        }
        CombatManager.Instance.CapturePersistentState(data);
        return data;
    }

    public void RestoreSave(RunSaveData data, SaveContentCatalog catalog)
    {
        if (!RunSaveValidation.Validate(data, catalog, out string error)) throw new ArgumentException(error, nameof(data));
        if (CardManager.Instance == null || CombatManager.Instance == null) throw new InvalidOperationException("Managers must be configured before restore.");
        ResetRuntimeState();
        runSeed = data.seed; _randomContext = new RunRandomContext(data.seed);
        CombatManager.Instance.RestorePersistentState(data);
        CardManager.Instance.RestoreSave(data.cards, catalog);
        headlinerIndex = data.bossIndex; IsInfiniteMode = data.infiniteMode;
        _completedNodeCount = data.completedNodeCount;
        currentPath.AddRange(data.nodes.Select(n => new PathNode { id = n.id, kind = (MapNodeType)n.kind,
            type = n.type, contentId = n.contentId, mapIndex = n.mapIndex, col = n.col, row = n.row,
            next = n.next.ToList(), completed = n.completed, accessible = n.accessible,
            revealed = n.revealed, hidden = n.hidden, isShopCheckpoint = n.shopCheckpoint }));
        foreach (var saved in data.bosses)
        {
            // Stats and abilities restore exactly; player-facing text is regenerated from identity
            // so terminology updates also reach existing saves.
            var sourceCard = CardData.Create((CardData.Suit)saved.suit, (CardData.Rank)saved.rank);
            var headliner = EnemyTypeData.Create(GetCanonicalHeadlinerName(sourceCard), saved.hp, saved.attack, saved.gold);
            headliner.sourceCard = sourceCard;
            headliner.encounterCount = saved.encounterCount; headliner.fleeAfterPlayerTurns = saved.fleeTurns;
            headliner.abilities = saved.abilities.Select(a =>
            {
                var ability = ScriptableObject.CreateInstance<EnemyAbility>();
                ability.id = a.id; ability.effect = (EnemyAbilityEffect)a.effect; ability.amount = a.amount;
                ability.displayName = HeadlinerAbilityName(ability.effect);
                ability.description = HeadlinerAbilityDescription(ability.effect);
                _generatedEnemyAbilities.Add(ability); return ability;
            }).ToArray();
            _generatedEnemyTypes.Add(headliner); headlinerDeck.Add(headliner);
        }
        foreach (var saved in data.shops)
        {
            var shop = new ShopCheckpointState { RefreshCount = saved.refreshCount,
                MadePricedPurchase = saved.madePricedPurchase, Completed = saved.completed };
            shop.Offers.AddRange(saved.offers.Select(o => RestoreOffer(o, catalog)));
            shop.UsedServices.UnionWith(saved.usedServices.Select(x => (ShopOfferKind)x));
            shop.PurchasedConsumables.UnionWith(saved.purchasedConsumables); shop.PurchasedGoods.UnionWith(saved.purchasedGoods);
            _shopStates.Add((saved.mapIndex, saved.nodeId), shop);
        }
        _activeNode = data.activeNodeId < 0 ? null : FindNode(data.activeNodeId);
        if (_activeNode != null && _activeNode.kind == MapNodeType.Shop)
        {
            _activeShopState = _shopStates[(_activeNode.mapIndex, _activeNode.id)];
            _activeShopOffers = _activeShopState.Offers; _usedSpecialShopOffers = _activeShopState.UsedServices;
            _purchasedConsumablesInShop = _activeShopState.PurchasedConsumables;
            _madeSuccessfulPurchaseInCurrentShop = _activeShopState.MadePricedPurchase;
        }
        _activeEvent = string.IsNullOrEmpty(data.activeEventId) ? null : catalog.Event(data.activeEventId, _activeNode.kind);
        _activeUpgradeOffers.AddRange(data.upgradeOffers.Select(o => RestoreOffer(o, catalog)));
        _pendingInvestmentStakes.AddRange(data.investmentStakes);
        _paidItemPurchaseCount = data.paidItemPurchases; _combatWinCount = data.combatWins;
        _merchantGiftRewardCount = data.merchantGiftRewards; _satchelRewardCount = data.satchelRewards;
        _visitedNodeTypesMapIndex = data.visitedMapIndex;
        _visitedNodeTypesCurrentMap.UnionWith(data.visitedNodeTypes.Select(x => (MapNodeType)x));
        _consumableDropResolvedContexts.UnionWith(data.consumableDropContexts);
        _resolvedConsumableRewardContexts.UnionWith(data.resolvedRewardContexts);
        foreach (var saved in data.alchemistChoices) _alchemistChoicesByMap.Add(saved.mapIndex, saved.choices.Select(catalog.Consumable).ToArray());
        if (data.hasContract) _activeContract = new RunContractState
        { objective = (RunContractObjective)data.contract.objective, startedAtMapIndex = data.contract.startedMap,
            startedAtNodeId = data.contract.startedNode, deadlineMapExclusive = data.contract.deadlineMap,
            encounterWins = data.contract.encounterWins, stableId = data.contract.stableId };
        Timing.RestoreSave(data.timing);
        midShopGoods = new Vector3Int(data.midShopGoods[0], data.midShopGoods[1], data.midShopGoods[2]);
        preHeadlinerShopGoods = new Vector3Int(data.preBossShopGoods[0], data.preBossShopGoods[1], data.preBossShopGoods[2]);
        SubscribeToCombatResults();
    }

    public void PresentRestoredSave()
    {
        // Presentation notifications only: deliberately no MapReady, NodeCompleted, encounter startup or purchases.
        OnRunStarted?.Invoke(runSeed);
        CardManager.Instance.PresentRestoredSave();
        OnCycleStarted?.Invoke(IsInfiniteMode ? $"District {CurrentCycle} / Infinite" : $"District {CurrentCycle} / {HeadlinerTotal}");
        if (_activeNode == null) OnShowPathScreen?.Invoke();
        else if (_activeNode.kind == MapNodeType.Shop) OnShowShop?.Invoke();
        else if (_activeNode.kind == MapNodeType.Upgrade) OnShowUpgrade?.Invoke();
        else if (_activeEvent != null) OnShowEvent?.Invoke(_activeEvent);
    }

    static OfferSaveData CaptureOffer(ShopOffer o) => new()
    { kind = (int)o.kind, slot = o.slot, basePrice = o.basePrice, price = o.price,
        investmentStake = o.investmentStake, supplyQuantity = o.supplyQuantity, generation = o.generation,
        purchased = o.purchased, artifactId = o.gear != null ? o.gear.id : null,
        enhancementId = o.enhancement != null ? o.enhancement.id : null,
        consumableId = o.consumable != null ? o.consumable.id : null };
    static ShopOffer RestoreOffer(OfferSaveData o, SaveContentCatalog c) => new()
    { kind = (ShopOfferKind)o.kind, slot = o.slot, basePrice = o.basePrice, price = o.price,
        investmentStake = o.investmentStake, supplyQuantity = o.supplyQuantity, generation = o.generation,
        purchased = o.purchased, gear = string.IsNullOrEmpty(o.artifactId) ? null : c.Gear(o.artifactId),
        enhancement = string.IsNullOrEmpty(o.enhancementId) ? null : c.Enhancement(o.enhancementId),
        consumable = string.IsNullOrEmpty(o.consumableId) ? null : c.Consumable(o.consumableId) };
}
