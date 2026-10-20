using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase7EGearCapacityTests
{
    readonly List<Object> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    RunManager _run;
    GearData[] _gears;
    CardEnhancementData _enhancement;
    ConsumableData _enhancementPatch;

    [SetUp]
    public void SetUp()
    {
        _gears = Enumerable.Range(1, 7).Select(i => Gear($"artifact_{i}", 10 + i)).ToArray();
        _enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        _enhancement.id = "sharpened";
        _enhancement.price = 9;
        _assets.Add(_enhancement);
        _enhancementPatch = ScriptableObject.CreateInstance<ConsumableData>();
        _enhancementPatch.id = "sharpened_patch";
        _enhancementPatch.price = _enhancement.price;
        _enhancementPatch.effectType = ConsumableEffectType.ApplyEnhancement;
        _enhancementPatch.enhancementToApply = _enhancement;
        _enhancementPatch.shopAvailable = true;
        _assets.Add(_enhancementPatch);
        _cards = Make<CardManager>("Cards");
        _cards.Configure(null, null, null, _gears, new[] { _enhancement }, new[] { _enhancementPatch });
        var combat = Make<CombatManager>("Combat");
        combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Run");
        _run.thiefType = Enemy("Thief");
        _run.goblinType = Enemy("Goblin");
        _run.knightType = Enemy("Knight");
        _run.StartRunWithSeed("7E-CAPACITY");
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i]) Object.DestroyImmediate(_objects[i]);
        foreach (var asset in _assets)
            if (asset) Object.DestroyImmediate(asset);
        _objects.Clear();
        _assets.Clear();
    }

    [Test]
    public void BelowFive_FifthSucceeds_SixthFailsWithoutMutationOrCreditsLoss()
    {
        Assert.That(_cards.GearCapacity, Is.EqualTo(5));
        _cards.AddCredits(100);
        for (int i = 0; i < 5; i++)
        {
            Assert.That(_cards.BuyGear(_gears[i], 1), Is.True);
            Assert.That(_cards.GearSlotsUsed, Is.EqualTo(i + 1));
        }
        int credits = _cards.credits;
        Assert.That(_cards.ownedGear, Has.Count.EqualTo(5));
        Assert.That(_cards.GetGearAcquisitionUnavailableReason(_gears[5]), Is.EqualTo("Gear Capacity Full"));
        Assert.That(_cards.BuyGear(_gears[5], 1), Is.False);
        Assert.That(_cards.BuyGear(_gears[6]), Is.False, "Legacy non-Shop grant path shares the limit");
        Assert.That(_cards.credits, Is.EqualTo(credits));
        Assert.That(_cards.ownedGear, Has.Count.EqualTo(5));
        Assert.That(_cards.gearIds, Has.Count.EqualTo(5));
        Assert.That(_cards.HasGear(_gears[5]), Is.False);
    }

    [Test]
    // Since Gear merging (57afa28), owning copies is allowed; only the Black Market refuses owned Gear.
    public void CapacityRuleWinsForCopiesAtCapacity_AndInvalidPriceDoesNotGrant()
    {
        for (int i = 0; i < 5; i++) Assert.That(_cards.BuyGear(_gears[i], 0), Is.True);
        Assert.That(_cards.GetGearAcquisitionUnavailableReason(_gears[0]), Is.EqualTo("Gear Capacity Full"));
        Assert.That(_cards.BuyGear(_gears[0], 0), Is.False);
        Assert.That(_cards.BuyGear(_gears[5], -1), Is.False);
        Assert.That(_cards.ownedGear, Has.Count.EqualTo(5));
        Assert.That(_cards.credits, Is.Zero);
    }

    [Test]
    public void NonSlotGear_IsOwnedButDoesNotConsumeCapacity_AndCopiesStayTrackedOnce()
    {
        for (int i = 0; i < 5; i++) Assert.That(_cards.BuyGear(_gears[i], 0), Is.True);
        var contract = Gear("contract", 0);
        contract.capacityCategory = GearCapacityCategory.NonSlot;
        Assert.That(_cards.GetGearAcquisitionUnavailableReason(contract), Is.Empty);
        Assert.That(_cards.BuyGear(contract, 0), Is.True);
        Assert.That(_cards.ownedGear, Has.Count.EqualTo(6));
        Assert.That(_cards.GearSlotsUsed, Is.EqualTo(5));
        Assert.That(_cards.BuyGear(contract, 0), Is.True, "A NonSlot copy needs no capacity.");
        Assert.That(_cards.gearIds.Count(id => id == contract.id), Is.EqualTo(1));
        Assert.That(_cards.BuyGear(_gears[5], 0), Is.False);
        Assert.That(_cards.ownedGear, Does.Contain(contract));
    }

    [Test]
    public void UpgradingOneOfTwoCopies_KeepsTheOtherCopyOwned()
    {
        var common = Gear("copy_common", 0);
        common.canonicalId = "copy_common";
        var rare = Gear("copy_rare", 0);
        rare.canonicalId = "copy_rare";
        rare.tier = 2;
        rare.upgradeFromId = common.canonicalId;
        _cards.gearCatalog.AddRange(new[] { common, rare });
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        Assert.That(_cards.BuyGear(common, 0), Is.True);

        Assert.That(_cards.BuyGear(rare, 0), Is.True);

        Assert.That(_cards.ownedGear, Is.EqualTo(new[] { rare, common }));
        Assert.That(_cards.HasGear(common), Is.True, "The remaining copy must stay owned for rules and the Shop.");
        Assert.That(_cards.HasGear(rare), Is.True);
    }

    [Test]
    public void FullShop_KeepsCachedOfferAndPrice_BlocksGearButAllowsMappedEnhancementConsumable()
    {
        _cards.AddCredits(100);
        OpenShop();
        var shopGear = _run.GetCurrentShopOffers().Where(o => o.kind == ShopOfferKind.Gear).ToArray();
        var shopEnhancement = _run.GetCurrentShopOffers().First(o => o.kind == ShopOfferKind.Consumable &&
            o.consumable == _enhancementPatch);
        Assert.That(shopGear.Length, Is.GreaterThanOrEqualTo(1));
        foreach (var gear in _gears.Where(a => shopGear.All(o => o.gear != a)).Concat(_gears).Take(5))
            Assert.That(_cards.BuyStandaloneGear(gear, 0), Is.True);
        var offer = shopGear[0];
        string id = offer.StableId;
        int price = offer.price;
        int credits = _cards.credits;
        Assert.That(_cards.GearSlotsUsed, Is.EqualTo(5));
        Assert.That(_run.GetShopOfferUnavailableReason(id), Is.EqualTo("Gear Capacity Full"));
        Assert.That(_run.CanPurchaseShopOffer(id), Is.False);
        Assert.That(_run.PurchaseShopOffer(id), Is.False);
        Assert.That(_cards.credits, Is.EqualTo(credits));
        Assert.That(_cards.GearSlotsUsed, Is.EqualTo(5));
        Assert.That(_run.GetCurrentShopOffers(), Does.Contain(offer));
        Assert.That(offer.StableId, Is.EqualTo(id));
        Assert.That(offer.price, Is.EqualTo(price));

        int cardId = _cards.hand.First(card => card.Enhancement == null).Id;
        Assert.That(_run.GetShopOfferUnavailableReason(shopEnhancement.StableId), Is.Empty);
        Assert.That(_run.PurchaseShopOffer(shopEnhancement.StableId), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(credits - shopEnhancement.price));
        Assert.That(_cards.FindOwnedCard(cardId).Enhancement, Is.Null,
            "Buying the mapped Consumable does not immediately enhance a card.");
        Assert.That(_cards.GetConsumableCount(_enhancementPatch), Is.EqualTo(1));
        Assert.That(_cards.GearSlotsUsed, Is.EqualTo(5));
    }

    [Test]
    public void ResetRestartAndNewRun_ClearSlotsAndReturnToBaseCapacity()
    {
        for (int i = 0; i < 5; i++) _cards.BuyGear(_gears[i], 0);
        _run.RestartRun();
        Assert.That(_cards.GearCapacity, Is.EqualTo(5));
        Assert.That(_cards.GearSlotsUsed, Is.Zero);
        Assert.That(_cards.ownedGear, Is.Empty);
        Assert.That(_cards.ownedCards, Has.Count.EqualTo(40));
        Assert.That(_cards.BuyGear(_gears[0], 0), Is.True);
        _run.StartRunWithSeed("7E-CAPACITY-NEW");
        Assert.That(_cards.GearCapacity, Is.EqualTo(5));
        Assert.That(_cards.GearSlotsUsed, Is.Zero);
        Assert.That(_cards.ownedGear, Is.Empty);
        _cards.BuyGear(_gears[1], 0);
        _cards.Reset();
        Assert.That(_cards.GearCapacity, Is.EqualTo(5));
        Assert.That(_cards.GearSlotsUsed, Is.Zero);
    }

    void OpenShop()
    {
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _run.OnPathChosen(shop.id);
    }

    T Make<T>(string name) where T : Component
    {
        var obj = new GameObject(name);
        _objects.Add(obj);
        return obj.AddComponent<T>();
    }

    GearData Gear(string id, int price)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = id;
        gear.displayName = id;
        gear.price = price;
        gear.damageMultiplier = 1;
        _assets.Add(gear);
        return gear;
    }

    EnemyTypeData Enemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 3, 3);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }
}
