using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DevModeTests
{
    readonly List<UnityEngine.Object> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    GearData[] _gears;
    CardEnhancementData[] _enhancements;

    [SetUp]
    public void SetUp()
    {
        DevModeRuntime.Disable();
        GameplayInputGate.Clear();
        _gears = Enumerable.Range(0, 6).Select(i => Gear($"DEV-ART-{i:D2}", $"Dev Artifact {i}", 15 + i)).ToArray();
        _enhancements = Enumerable.Range(0, 2).Select(i => Enhancement($"DEV-ENH-{i:D2}", $"Dev Enhancement {i}", 10 + i)).ToArray();
        var mappedConsumables = _enhancements.Select((enhancement, index) =>
        {
            var item = ScriptableObject.CreateInstance<ConsumableData>();
            item.id = "DEV-PATCH-" + index;
            item.canonicalId = "DEV-CON-" + index;
            item.displayName = enhancement.displayName + " Patch";
            item.price = enhancement.price;
            item.effectType = ConsumableEffectType.ApplyEnhancement;
            item.enhancementToApply = enhancement;
            item.shopAvailable = true;
            _assets.Add(item);
            return item;
        }).ToArray();
        _cards = Make<CardManager>("Dev Test Cards");
        _cards.Configure(null, null, null, _gears, _enhancements, mappedConsumables);
        _cards.ConfigureRandom(new DeterministicRandom(991));
        _combat = Make<CombatManager>("Dev Test Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Dev Test Run");
        _run.thiefType = Enemy("Dev Thief", 20, 1);
        _run.goblinType = Enemy("Dev Goblin", 20, 2);
        _run.knightType = Enemy("Dev Knight", 20, 3);
    }

    [TearDown]
    public void TearDown()
    {
        DevModeRuntime.Disable();
        GameplayInputGate.Clear();
        foreach (var gameObject in _objects.AsEnumerable().Reverse())
            if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
        foreach (var asset in _assets)
            if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
    }

    public void DevMode_ResetSession_ClearsStaleEditorActivationKey()
    {
        PlayerPrefs.SetInt("Dealbreak.DevMode.Enabled", 1);
        DevModeRuntime.ResetSession();
        Assert.That(PlayerPrefs.GetInt("Dealbreak.DevMode.Enabled", 1), Is.Zero);
        Assert.That(DevModeRuntime.Enabled, Is.False);
    }

    [Test]
    public void DevMode_ConfigureThenDisableDoesNotRetainActivation()
    {
        DevModeRuntime.Configure(true, true, true, true);
        Assert.That(DevModeRuntime.Enabled, Is.True);
        DevModeRuntime.Disable();
        Assert.That(DevModeRuntime.Enabled, Is.False);
        Assert.That(DevModeRuntime.InfiniteHealth, Is.False);
        Assert.That(DevModeRuntime.InfiniteMoney, Is.False);
        Assert.That(DevModeRuntime.UnlimitedShopOffers, Is.False);
    }

    [Test]
    public void DevMode_InfiniteHealthSuppressesCombatAndRunDamage_AndDisablingRestoresDamage()
    {
        DevModeRuntime.Configure(true, true, false, false);
        Assert.That(_combat.TakeRunDamage(1000), Is.Zero);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
        Assert.That(_combat.CanTakeRunDamage(1000), Is.True);

        DevModeRuntime.Configure(true, false, false, false);
        Assert.That(_combat.TakeRunDamage(10), Is.EqualTo(10));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(20));
    }

    [Test]
    public void DevMode_InfiniteMoneyAllowsSpendingWithoutChangingCredits_AndDoesNotChangeGearCapacity()
    {
        _cards.credits = 17;
        DevModeRuntime.Configure(true, false, true, false);

        Assert.That(_cards.CanAfford(100000), Is.True);
        Assert.That(_cards.SpendCredits(100000), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(17));
        Assert.That(_cards.BuyGear(_gears[0], 100000), Is.True);
        Assert.That(_cards.GearSlotsUsed, Is.EqualTo(1));
        Assert.That(_cards.GearCapacity, Is.EqualTo(CardManager.BASE_GEAR_CAPACITY));

        DevModeRuntime.Disable();
        Assert.That(_cards.CanAfford(100000), Is.False);
        Assert.That(_cards.SpendCredits(100000), Is.False);
    }

    [Test]
    public void DevMode_UnlimitedShopOffersExposesEveryCatalogEntry_ButRetainsGearCapacity()
    {
        DevModeRuntime.Configure(true, true, true, true);
        _run.StartRunWithSeed("DEV-SHOP-OFFERS");
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _run.OnPathChosen(shop.id);

        int expectedCount = _gears.Length + _enhancements.Length + 4; // Every service too.
        Assert.That(_run.GetCurrentShopOffers(), Has.Count.EqualTo(expectedCount));
        CollectionAssert.AllItemsAreUnique(_run.GetCurrentShopOffers().Select(offer => offer.StableId).ToArray());
        Assert.That(_run.GetCurrentShopOffers().Count(offer => offer.kind == ShopOfferKind.Gear),
            Is.EqualTo(_gears.Length));
        Assert.That(_run.GetCurrentShopOffers().Count(offer => offer.kind == ShopOfferKind.Enhancement), Is.Zero);
        Assert.That(_run.GetCurrentShopOffers().Count(offer => offer.kind == ShopOfferKind.Consumable &&
            offer.consumable != null && offer.consumable.effectType == ConsumableEffectType.ApplyEnhancement),
            Is.EqualTo(_enhancements.Length));

        var gearOffers = _run.GetCurrentShopOffers().Where(offer => offer.kind == ShopOfferKind.Gear).ToArray();
        for (int i = 0; i < CardManager.BASE_GEAR_CAPACITY; i++)
            Assert.That(_run.PurchaseShopOffer(gearOffers[i].StableId), Is.True);
        Assert.That(_cards.GearSlotsUsed, Is.EqualTo(5));
        Assert.That(_cards.GearCapacity, Is.EqualTo(5));
        Assert.That(_run.GetShopOfferUnavailableReason(gearOffers[5].StableId),
            Is.EqualTo("Gear Capacity Full"));
        Assert.That(_run.PurchaseShopOffer(gearOffers[5].StableId), Is.False,
            "Unlimited Shop offer generation does not lift the Persistent Artifact capacity rule.");
        Assert.That(_run.GetCurrentShopOffers(), Has.Count.EqualTo(expectedCount),
            "The cached dev offer list remains inspectable after purchases.");
        Assert.That(_cards.credits, Is.Zero, "Infinite money spends nothing.");
    }

    [Test]
    public void DevMode_ImmediateLoseRaisesOneNormalDefeatResult()
    {
        _run.StartRunWithSeed("DEV-FORCE-LOSE");
        DevModeRuntime.Configure(true, true, true, true);
        int results = 0;
        EncounterResult? terminal = null;
        _combat.OnEncounterResult += result => { results++; terminal = result; };

        Assert.That(_combat.ForceDefeatForDevelopment(), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameOver));
        Assert.That(terminal, Is.EqualTo(EncounterResult.Defeat));
        Assert.That(results, Is.EqualTo(1));
        Assert.That(_combat.ForceDefeatForDevelopment(), Is.False);
        Assert.That(results, Is.EqualTo(1));
    }

    GearData Gear(string id, string name, int price)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = id;
        gear.canonicalId = id;
        gear.displayName = name;
        gear.rarity = "Common";
        gear.tier = 1;
        gear.price = price;
        gear.capacityCategory = GearCapacityCategory.Persistent;
        gear.effects = Array.Empty<GameplayEffectDefinition>();
        gear.ruleModifiers = Array.Empty<GameplayRuleModifierData>();
        _assets.Add(gear);
        return gear;
    }

    CardEnhancementData Enhancement(string id, string name, int price)
    {
        var enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        enhancement.id = id;
        enhancement.canonicalId = id;
        enhancement.displayName = name;
        enhancement.rarity = "Common";
        enhancement.tier = 1;
        enhancement.price = price;
        enhancement.effects = Array.Empty<GameplayEffectDefinition>();
        enhancement.ruleModifiers = Array.Empty<GameplayRuleModifierData>();
        _assets.Add(enhancement);
        return enhancement;
    }

    EnemyTypeData Enemy(string name, int hp, int attack)
    {
        var enemy = EnemyTypeData.Create(name, hp, attack, 0);
        _assets.Add(enemy);
        return enemy;
    }

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _objects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
