using System;
using System.Collections.Generic;
using System.Linq;

public static class RunSaveValidation
{
    public const int Version = 5;
    public const int ContentVersion = 1;
    public static bool Validate(RunSaveData s, SaveContentCatalog catalog, out string error)
    {
        try { Check(s, catalog); error = null; return true; }
        catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is NullReferenceException)
        { error = "Invalid run save: " + exception.Message; return false; }
    }
    static void Require(bool condition, string reason) { if (!condition) throw new ArgumentException(reason); }
    static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    static void Effects(EffectStateSaveData e)
    {
        Require(e != null && e.persistent != null && e.encounter != null, "Missing effect state.");
        foreach (var list in new[] { e.persistent, e.encounter })
            Require(list.Count <= 4096 && list.All(x => x != null) && list.Select(x => x.key).Distinct().Count() == list.Count, "Invalid effect counters.");
    }
    static bool Defined<T>(int value) where T : Enum => Enum.IsDefined(typeof(T), value);
    static void Check(RunSaveData s, SaveContentCatalog c)
    {
        Require(s != null && s.version == Version && s.contentVersion == ContentVersion, "Unsupported save/content version.");
        Require(c != null, "Save content catalog unavailable.");
        Require(!string.IsNullOrWhiteSpace(s.seed) && s.seed.Length <= 256 && s.seed == RunRandomContext.NormalizeSeed(s.seed), "Invalid seed.");
        Require(s.checkpoint == "safe-node", "Unsupported checkpoint kind.");
        Require(s.bossIndex >= 0 && (s.infiniteMode || s.bossIndex < 12) && s.completedNodeCount >= 0, "Invalid progression.");
        Require(s.bosses != null && s.bosses.Count == 12 && s.bosses.All(b => b != null), "Missing boss sequence.");
        Require(s.bosses.Select(b => (b.rank, b.suit)).Distinct().Count() == 12, "Duplicate boss identity.");
        for (int i = 0; i < 12; i++)
        {
            var b = s.bosses[i];
            Require(b.rank == (int)CardData.Rank.Jack + i / 4 && Defined<CardData.Suit>(b.suit) &&
                b.hp > 0 && b.attack >= 0 && b.gold >= 0 && b.encounterCount > 0 && b.fleeTurns >= 0 && b.abilities != null, "Invalid boss definition.");
            Require(b.abilities.Count <= 32 && b.abilities.All(a => a != null && Defined<EnemyAbilityEffect>(a.effect) && a.amount >= 0), "Invalid boss abilities.");
        }
        var p = s.player;
        Require(p != null && p.configuredMaxHealth > 0 && p.maxHealth > 0 && p.health > 0 && p.health <= p.maxHealth && p.shield >= 0 && p.block >= 0, "Invalid player health/resources.");
        Require(s.criticalRandom != 0 && s.duelistRandom != 0 && s.nextActionId > 0 && s.eliteKillCount >= 0 &&
            Finite(s.criticalChance) && s.criticalChance >= 0 && s.criticalChance <= 100, "Invalid combat continuation state.");
        var cards = s.cards;
        Require(cards != null && cards.random != 0 && cards.gold >= 0 && cards.owned != null &&
            cards.owned.Count > 0 && cards.owned.Count <= 10000 && cards.owned.All(x => x != null), "Invalid card collection.");
        var ids = new HashSet<int>();
        foreach (var card in cards.owned)
        {
            Require(card.id > 0 && ids.Add(card.id) && Defined<CardData.Rank>(card.rank) && Defined<CardData.Suit>(card.definitionSuit) &&
                Defined<CardData.Suit>(card.effectiveSuit) && card.permanentAttack >= 0, "Invalid card identity.");
            Require(string.IsNullOrEmpty(card.enhancementId) || c.Enhancement(card.enhancementId) != null, "Unknown Enhancement ID.");
            Effects(card.effects);
        }
        Require(cards.nextCardId > ids.Max(), "Invalid next card identity.");
        Require(cards.deck != null && cards.hand != null && cards.discard != null, "Missing card zones.");
        var zones = cards.deck.Concat(cards.hand).Concat(cards.discard).ToArray();
        Require(zones.Length == ids.Count && zones.Distinct().Count() == ids.Count && zones.All(ids.Contains), "Card membership duplicated or missing.");
        Require(cards.artifacts != null && cards.artifacts.Count <= 1024 && cards.artifacts.All(x => x != null) &&
            cards.artifacts.Select(x => x.id).Distinct().Count() == cards.artifacts.Count, "Invalid Artifact identities.");
        Require(cards.artifactIdWatermark >= 0 && cards.artifactIdWatermark < long.MaxValue / 2 &&
            cards.artifacts.All(x => x.id <= cards.artifactIdWatermark), "Invalid Artifact identity allocator.");
        foreach (var gear in cards.artifacts)
        {
            Require(gear.id > 0 && gear.id < long.MaxValue / 2 && c.Gear(gear.contentId) != null, "Unknown Artifact ID.");
            Require((gear.preparedCardId == 0 || ids.Contains(gear.preparedCardId)) &&
                (gear.pendingCardId == 0 || ids.Contains(gear.pendingCardId)), "Invalid Artifact card reference.");
            Effects(gear.effects);
        }
        Require(cards.backpack != null && cards.backpack.Count <= 64, "Invalid backpack.");
        foreach (var slot in cards.backpack)
        {
            var definition = slot == null ? null : c.Consumable(slot.contentId);
            Require(definition != null && slot.charges != null && slot.charges.Count > 0 && slot.charges.Count <= 64 &&
                slot.charges.All(x => x > 0 && x <= Math.Max(1, definition.uses)), "Unknown Consumable or invalid charges.");
        }
        Require(s.nodes != null && s.nodes.Count > 0 && s.nodes.Count <= 1000 && s.nodes.All(x => x != null) &&
            s.nodes.Select(x => x.id).Distinct().Count() == s.nodes.Count, "Invalid route nodes.");
        var nodes = s.nodes.ToDictionary(x => x.id);
        foreach (var node in s.nodes)
        {
            Require(node.id >= 0 && node.mapIndex == s.bossIndex && Defined<MapNodeType>(node.kind) && Finite(node.col) && Finite(node.row) &&
                node.next != null && node.next.Distinct().Count() == node.next.Count && (!node.completed || !node.accessible), "Invalid route state.");
            Require(node.next.All(id => nodes.TryGetValue(id, out var target) && target.col > node.col), "Invalid/cyclic route links.");
            var kind = (MapNodeType)node.kind;
            if (kind is MapNodeType.Combat or MapNodeType.Elite) Require(c.HasEnemy(node.contentId), "Missing encounter content: " + node.contentId);
            if (kind is MapNodeType.Event or MapNodeType.Risk) Require(c.Event(node.contentId, kind) != null, "Missing event content: " + node.contentId);
            Require(!node.shopCheckpoint || kind == MapNodeType.Shop, "Invalid checkpoint type.");
        }
        Require(s.nodes.Count(x => x.kind == (int)MapNodeType.Headliner) == 1, "Missing map boss.");
        Require(s.activeNodeId == -1 || nodes.ContainsKey(s.activeNodeId), "Missing active node.");
        NodeSaveData active = s.activeNodeId < 0 ? null : nodes[s.activeNodeId];
        if (active != null) Require(!active.completed && !active.accessible && (MapNodeType)active.kind is
            MapNodeType.Shop or MapNodeType.Event or MapNodeType.Risk, "Unsupported active checkpoint.");
        else Require(s.nodes.Any(x => x.accessible && !x.completed), "No available route.");
        bool eventActive = active != null && (MapNodeType)active.kind is MapNodeType.Event or MapNodeType.Risk;
        Require(eventActive ? s.activeEventId == active.contentId && c.Event(s.activeEventId, (MapNodeType)active.kind) != null
            : string.IsNullOrEmpty(s.activeEventId), "Invalid active event.");
        Require(s.shops != null && s.shops.Count <= 10000 && s.shops.All(x => x != null) &&
            s.shops.Select(x => (x.mapIndex, x.nodeId)).Distinct().Count() == s.shops.Count, "Invalid Shop identities.");
        foreach (var shop in s.shops)
        {
            Require(shop.mapIndex >= 0 && shop.mapIndex <= s.bossIndex && shop.nodeId >= 0 && shop.refreshCount >= 0 &&
                shop.offers != null && shop.usedServices != null && shop.purchasedConsumables != null && shop.purchasedGoods != null, "Invalid Shop state.");
            foreach (var offer in shop.offers) Offer(offer, c);
            Require(shop.usedServices.All(x => Defined<ShopOfferKind>(x) && (ShopOfferKind)x is ShopOfferKind.Guidance or ShopOfferKind.Contract or ShopOfferKind.Investment or ShopOfferKind.Supply), "Invalid used Services.");
            if (shop.mapIndex == s.bossIndex) Require(nodes.TryGetValue(shop.nodeId, out var n) && n.kind == (int)MapNodeType.Shop && n.completed == shop.completed, "Shop completion disagrees with route.");
        }
        if (active != null && active.kind == (int)MapNodeType.Shop)
            Require(s.shops.Any(x => x.mapIndex == active.mapIndex && x.nodeId == active.id && !x.completed), "Missing active Shop inventory.");
        Require(s.nodes.All(x => x.kind != (int)MapNodeType.Upgrade), "Retired Training node.");
        Require(s.investmentStakes != null && s.investmentStakes.All(x => x > 0) && s.paidItemPurchases >= 0 && s.combatWins >= 0 && s.merchantGiftRewards >= 0 && s.satchelRewards >= 0, "Invalid economy counters.");
        Require(s.visitedNodeTypes != null && s.visitedNodeTypes.All(Defined<MapNodeType>) && s.consumableDropContexts != null && s.resolvedRewardContexts != null, "Missing reward guards.");
        Require(s.alchemistChoices != null && s.alchemistChoices.All(x => x != null) &&
            s.alchemistChoices.Select(x => x.mapIndex).Distinct().Count() == s.alchemistChoices.Count, "Invalid Alchemist cache.");
        foreach (var cache in s.alchemistChoices) Require(cache.choices != null && cache.choices.All(id => c.Consumable(id) != null), "Unknown cached consumable.");
        if (s.hasContract) Require(s.contract != null && Defined<RunContractObjective>(s.contract.objective) && s.contract.startedMap >= 0 && s.contract.startedMap <= s.bossIndex &&
            s.contract.deadlineMap > s.contract.startedMap && s.contract.encounterWins >= 0 && !string.IsNullOrEmpty(s.contract.stableId), "Invalid Contract.");
        Require(s.timing != null && s.timing.splits != null && s.timing.mapIndex == s.bossIndex &&
            Finite(s.timing.totalElapsed) && Finite(s.timing.mapElapsed) && s.timing.totalElapsed >= 0 && s.timing.mapElapsed >= 0 &&
            s.timing.splits.All(x => x != null && x.mapIndex >= 0 && Finite(x.elapsed) && x.elapsed >= 0), "Invalid run timing.");
        Require(s.midShopGoods != null && s.preBossShopGoods != null && s.midShopGoods.Length == 3 && s.preBossShopGoods.Length == 3, "Invalid Shop distribution.");
    }
    static void Offer(OfferSaveData o, SaveContentCatalog c)
    {
        Require(o != null && Defined<ShopOfferKind>(o.kind) && o.price >= 0 && o.basePrice >= 0 && o.slot >= 0 && o.generation >= 0 && o.investmentStake >= 0 && o.supplyQuantity >= 0, "Invalid offer.");
        if (o.kind == (int)ShopOfferKind.Gear) Require(c.Gear(o.artifactId) != null, "Unknown offered Artifact.");
        if (o.kind == (int)ShopOfferKind.Enhancement) Require(c.Enhancement(o.enhancementId) != null, "Unknown offered Enhancement.");
        if (o.kind == (int)ShopOfferKind.Consumable) Require(c.Consumable(o.consumableId) != null, "Unknown offered Consumable.");
    }
}
