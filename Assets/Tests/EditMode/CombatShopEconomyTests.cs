using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CombatShopEconomyTests
{
    readonly List<Object> created = new();
    CardManager cards;
    RunManager run;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void Setup()
    {
        GameplayInputGate.Clear();
        cards = Make<CardManager>();
        var gearItems = new List<GearData>();
        var enhancements = new List<CardEnhancementData>();
        var consumables = new List<ConsumableData>();
        for (int i = 0; i < 8; i++)
        {
            var a = ScriptableObject.CreateInstance<GearData>(); created.Add(a);
            a.id = "economy-a-" + i; a.price = 10; gearItems.Add(a);
            var e = ScriptableObject.CreateInstance<CardEnhancementData>(); created.Add(e);
            e.id = "economy-e-" + i; e.price = 8; enhancements.Add(e);
            var mapped = ScriptableObject.CreateInstance<ConsumableData>(); created.Add(mapped);
            mapped.id = "economy-map-" + i; mapped.price = e.price; mapped.shopAvailable = true;
            mapped.effectType = ConsumableEffectType.ApplyEnhancement; mapped.enhancementToApply = e; consumables.Add(mapped);
            var c = ScriptableObject.CreateInstance<ConsumableData>(); created.Add(c);
            c.id = "economy-c-" + i; c.price = 5; c.shopAvailable = true; consumables.Add(c);
        }
        cards.Configure(null, null, null, gearItems.ToArray(), enhancements.ToArray(), consumables.ToArray());
        cards.BuildDeck();
        cards.DrawExistingToHand(3);
        cards.AddCredits(200);
        Make<CombatManager>().ConfigurePlayer(30);
        run = Make<RunManager>();
        Set("_randomContext", new RunRandomContext("economy-check"));
    }

    [TearDown]
    public void Cleanup()
    {
        GameplayInputGate.Clear();
        for (int i = created.Count - 1; i >= 0; i--) if (created[i]) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void ServicePair_IsDistinct_AndSupplyGuaranteedAcrossBothCheckpoints()
    {
        for (int map = 0; map < 12; map++)
        {
            Open(map, false);
            var first = run.GetCurrentShopOffers().Where(o => o.IsService).Select(o => o.kind).ToArray();
            Open(map, true);
            var second = run.GetCurrentShopOffers().Where(o => o.IsService).Select(o => o.kind).ToArray();
            Assert.That(first.Distinct().Count(), Is.EqualTo(2));
            Assert.That(second.Distinct().Count(), Is.EqualTo(2));
            Assert.That(first.Concat(second), Does.Contain(ShopOfferKind.Supply));
        }
    }

    [Test]
    public void Supply_ChargesActualDraw_ConservesOwnership_RepeatsAndRejectsReentrancy()
    {
        var supply = OpenSupply();
        int owned = cards.ownedCards.Count, deck = cards.deck.Count, hand = cards.HandCount, credits = cards.credits;
        bool nested = true;
        cards.OnCreditsChanged += _ => nested = run.PurchaseSupply(supply.StableId, 1);
        Assert.That(run.PurchaseSupply(supply.StableId, 2), Is.True);
        Assert.That(nested, Is.False);
        Assert.That(cards.credits, Is.EqualTo(credits - 6));
        Assert.That(cards.HandCount, Is.EqualTo(hand + 2));
        Assert.That(cards.deck.Count, Is.EqualTo(deck - 2));
        Assert.That(cards.ownedCards.Count, Is.EqualTo(owned));

        // Supply can be bought again at the same Black Market while Credits, cards and hand space last.
        cards.AddCredits(3);
        int handBefore = cards.HandCount;
        bool roomForOne = cards.HandCount < cards.HandCapacity && cards.deck.Count > 0;
        Assert.That(run.PurchaseSupply(supply.StableId, 1), Is.EqualTo(roomForOne));
        Assert.That(cards.HandCount, Is.EqualTo(handBefore + (roomForOne ? 1 : 0)));
        Assert.That(run.GetCurrentShopOffers().Contains(supply), Is.True);
        Assert.That(supply.purchased, Is.False);
    }

    [Test]
    public void Supply_RejectsInvalidQuantitiesAndFullHandWithoutCharge()
    {
        var supply = OpenSupply();
        int credits = cards.credits;
        Assert.That(run.PurchaseSupply(supply.StableId, 0), Is.False);
        Assert.That(run.PurchaseSupply(supply.StableId, -1), Is.False);
        Assert.That(run.PurchaseSupply(supply.StableId, 6), Is.False);
        cards.DrawExistingToHand(20);
        Assert.That(run.GetSupplyMaximumQuantity(), Is.Zero);
        Assert.That(run.PurchaseSupply(supply.StableId, 1), Is.False);
        Assert.That(cards.credits, Is.EqualTo(credits));
    }

    [Test]
    public void Supply_EmptyDrawPileNeverRecyclesDiscard()
    {
        var supply = OpenSupply();
        var collection = (CardCollection)typeof(CardManager).GetField("_cards", Private).GetValue(cards);
        while (cards.deck.Count > 0)
        {
            collection.TryDiscard(collection.Hand.ToArray());
            cards.DrawExistingToHand(8);
        }
        collection.TryDiscard(collection.Hand.ToArray());
        int credits = cards.credits;
        Assert.That(run.PurchaseSupply(supply.StableId, 1), Is.False);
        Assert.That(cards.credits, Is.EqualTo(credits));
        Assert.That(cards.discardPile.Count, Is.EqualTo(40));
    }

    [Test]
    public void Refresh_ProgressionServicesPersistenceAndDuplicateRequest()
    {
        Open(0, false);
        var services = run.GetCurrentShopOffers().Where(o => o.IsService).ToArray();
        int[] prices = { 3, 5, 8, 12, 17, 23, 30 };
        for (int i = 0; i < prices.Length; i++)
        {
            Assert.That(run.GoodsRefreshPrice, Is.EqualTo(prices[i]));
            int credits = cards.credits;
            Assert.That(run.RefreshGoods(i), Is.True);
            Assert.That(cards.credits, Is.EqualTo(credits - prices[i]));
            Assert.That(run.RefreshGoods(i), Is.False);
            Assert.That(run.GetCurrentShopOffers().Where(o => o.IsService), Is.EqualTo(services));
        }
        var saved = run.GetCurrentShopOffers().ToArray();
        Open(0, true);
        Assert.That(run.GoodsRefreshPrice, Is.EqualTo(3));
        Open(0, false);
        Assert.That(run.GoodsRefreshCount, Is.EqualTo(prices.Length));
        Assert.That(run.GetCurrentShopOffers(), Is.EqualTo(saved));
    }

    [Test]
    public void PurchasedGoodsNeverReturn_AndStaleOffersAreRejected()
    {
        int map = 0;
        while (Open(map, false) == null || !run.GetCurrentShopOffers().Any(o => o.kind == ShopOfferKind.Gear) ||
               !run.GetCurrentShopOffers().Any(o => o.kind == ShopOfferKind.Consumable)) map++;
        var offer = run.GetCurrentShopOffers().First(o => o.kind == ShopOfferKind.Gear);
        var stale = run.GetCurrentShopOffers().First(o => o.kind == ShopOfferKind.Consumable).StableId;
        Assert.That(run.PurchaseShopOffer(offer.StableId), Is.True);
        Assert.That(run.PurchaseShopOffer(offer.StableId), Is.False);
        Assert.That(run.RefreshGoods(0), Is.True);
        Assert.That(run.GetCurrentShopOffers().Any(o => o.gear == offer.gear), Is.False);
        Assert.That(run.PurchaseShopOffer(stale), Is.False);
    }

    [Test]
    public void Goods_AreThreeQuartersGear_WithRarity631_AndNoLegendary()
    {
        var gear = new List<GearData>();
        void Add(string rarity, int count, int tier = 1)
        {
            for (int i = 0; i < count; i++)
            {
                var item = ScriptableObject.CreateInstance<GearData>(); created.Add(item);
                item.id = $"{rarity}-{tier}-{i}"; item.rarity = rarity; item.tier = tier; item.price = 10; gear.Add(item);
            }
        }
        Add("Common", 20);
        Add("Rare", 20, 2);
        Add("Epic", 20, 3);
        Add("Legendary", 5);
        cards.gearCatalog.Clear();
        cards.gearCatalog.AddRange(gear);

        var goods = new List<ShopOffer>();
        for (int map = 0; map < 400; map++)
        {
            Open(map, false);
            goods.AddRange(run.GetCurrentShopOffers().Where(o => !o.IsService));
        }
        var sold = goods.Where(o => o.kind == ShopOfferKind.Gear).Select(o => o.gear).ToList();
        Assert.That(goods, Has.Count.EqualTo(400 * 5), "Every Goods slot is filled.");
        Assert.That(sold.Count / (double)goods.Count, Is.EqualTo(0.75).Within(0.03), "Goods are Gear 75% of the time.");
        Assert.That(sold.Any(item => item.rarity == "Legendary"), Is.False, "Legendary Gear is never sold.");
        Assert.That(sold.Count(item => item.rarity == "Common") / (double)sold.Count, Is.EqualTo(0.6).Within(0.03));
        Assert.That(sold.Count(item => item.rarity == "Rare") / (double)sold.Count, Is.EqualTo(0.3).Within(0.03));
        Assert.That(sold.Count(item => item.rarity == "Epic") / (double)sold.Count, Is.EqualTo(0.1).Within(0.02));
    }

    [Test]
    public void ShopGear_IsStandalone_AndIgnoresOwnedTiers()
    {
        var common = ScriptableObject.CreateInstance<GearData>(); created.Add(common);
        common.id = "family-common"; common.canonicalId = "family-common"; common.rarity = "Common"; common.price = 10;
        var rare = ScriptableObject.CreateInstance<GearData>(); created.Add(rare);
        rare.id = "family-rare"; rare.canonicalId = "family-rare"; rare.rarity = "Rare"; rare.tier = 2;
        rare.upgradeFromId = common.canonicalId; rare.price = 10;
        cards.gearCatalog.Clear();
        cards.gearCatalog.AddRange(new[] { common, rare });

        Assert.That(cards.GetStandaloneGearUnavailableReason(rare), Is.Empty, "A Rare tier needs no Common to be bought.");
        Assert.That(cards.BuyStandaloneGear(common, 0), Is.True);
        Assert.That(cards.BuyStandaloneGear(common, 0), Is.True, "An owned Gear can be bought again as a copy.");
        Assert.That(cards.BuyStandaloneGear(rare, 0), Is.True);
        Assert.That(cards.ownedGear, Is.EqualTo(new[] { common, common, rare }), "Buying a higher tier never replaces a lower one.");
    }

    [Test]
    public void CheckpointLeave_IsFreeAndCompletionHooksRunOnceWithoutConsumingRouteDepth()
    {
        Open(0, false);
        var node = run.ActiveNode;
        int count = run.CompletedNodeCount, credits = cards.credits;
        run.OnShopDone();
        run.OnShopDone();
        Assert.That(node.completed, Is.True);
        Assert.That(run.ActiveNode, Is.Null);
        Assert.That(run.CompletedNodeCount, Is.EqualTo(count + 1));
        Assert.That(run.currentPath.Count(n => n.completed && !n.isShopCheckpoint && n.kind != MapNodeType.Headliner), Is.Zero);
        Assert.That(cards.credits, Is.EqualTo(credits));
    }

    [Test]
    public void Intel_IsNeverOfferedInTheLastBlackMarketBeforeTheHeadliner()
    {
        bool midOffersIntel = false;
        for (int map = 0; map < 12; map++)
        {
            Open(map, true);
            Assert.That(run.GetCurrentShopOffers().Any(o => o.kind == ShopOfferKind.Guidance), Is.False, $"District {map + 1}");
            Assert.That(run.GetCurrentShopOffers().Count(o => o.IsService), Is.EqualTo(2), "It still offers two services.");
            Open(map, false);
            midOffersIntel |= run.GetCurrentShopOffers().Any(o => o.kind == ShopOfferKind.Guidance);
        }
        Assert.That(midOffersIntel, Is.True, "Earlier Black Markets still offer Intel.");
    }

    ShopOffer OpenSupply()
    {
        Open(0, false);
        var offer = run.GetCurrentShopOffers().FirstOrDefault(o => o.kind == ShopOfferKind.Supply);
        if (offer != null) return offer;
        Open(0, true);
        return run.GetCurrentShopOffers().Single(o => o.kind == ShopOfferKind.Supply);
    }
    object Open(int map, bool preHeadliner)
    {
        // The last Black Market is the checkpoint that leads to the Headliner.
        run.currentPath.Clear();
        var headliner = new PathNode { id = 18, mapIndex = map, kind = MapNodeType.Headliner, col = 5 };
        var shop = new PathNode { id = preHeadliner ? 17 : 16, mapIndex = map,
            kind = MapNodeType.Shop, col = preHeadliner ? 4.5f : 1.5f, isShopCheckpoint = true };
        if (preHeadliner) shop.next.Add(headliner.id);
        run.currentPath.Add(shop);
        run.currentPath.Add(headliner);
        Set("_activeNode", shop);
        typeof(RunManager).GetMethod("PrepareShopOffers", Private).Invoke(run, new object[] { 4 });
        return run;
    }
    void Set(string field, object value) => typeof(RunManager).GetField(field, Private).SetValue(run, value);
    T Make<T>() where T : Component
    {
        var go = new GameObject(typeof(T).Name); created.Add(go); return go.AddComponent<T>();
    }
}
