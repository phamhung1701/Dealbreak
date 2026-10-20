using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BreadConsumableTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    ConsumableData _bread;
    int _breadUses;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _bread = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/Bread.asset");
        Assert.That(_bread, Is.Not.Null);
        _breadUses = _bread.uses;
        _cards = Make<CardManager>("Bread Test Cards");
        _cards.Configure(null, null, null, System.Array.Empty<GearData>(),
            System.Array.Empty<CardEnhancementData>(), new[] { _bread });
        _combat = Make<CombatManager>("Bread Test Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Bread Test Run");
        _run.thiefType = Enemy("Thief");
        _run.goblinType = Enemy("Goblin");
        _run.knightType = Enemy("Knight");
        _run.StartRunWithSeed("BREAD-CONSUMABLE-TEST");
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        if (_bread != null) _bread.uses = _breadUses;
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void BreadAsset_UsesCanonicalOneChargeFiveHpHealingAndShopDropSources()
    {
        Assert.That(_bread.id, Is.EqualTo("bread"));
        Assert.That(_bread.canonicalId, Is.EqualTo("con_001"));
        Assert.That(_bread.healAmount, Is.EqualTo(5));
        Assert.That(_bread.uses, Is.EqualTo(1));
        Assert.That(_bread.price, Is.EqualTo(5));
        Assert.That(_bread.shopAvailable, Is.True);
        Assert.That(_bread.dropAvailable, Is.True);
        Assert.That(_bread.dropChance, Is.EqualTo(0.1f).Within(0.0001f));
    }

    [Test]
    public void BackpackHasThreeIndividualSlotsAndDoesNotStackDuplicates()
    {
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
        Assert.That(_cards.AddConsumable(_bread, 2), Is.True);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(2));
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(2));
        Assert.That(_cards.GetConsumableAtSlot(0), Is.SameAs(_bread));
        Assert.That(_cards.GetConsumableAtSlot(1), Is.SameAs(_bread));

        Assert.That(_cards.AddConsumable(_bread), Is.True);
        Assert.That(_cards.IsBackpackFull, Is.True);
        Assert.That(_cards.AddConsumable(_bread), Is.False);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(CardManager.BACKPACK_CAPACITY));
    }

    [Test]
    public void FullBackpack_RejectsShopPurchaseWithoutChargingCredits()
    {
        Assert.That(_cards.AddConsumable(_bread, CardManager.BACKPACK_CAPACITY), Is.True);
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _cards.AddCredits(10);
        _run.OnPathChosen(shop.id);
        var offer = _run.GetCurrentShopOffers().First(value => value.kind == ShopOfferKind.Consumable);

        Assert.That(_run.GetShopOfferUnavailableReason(offer.StableId), Is.EqualTo("Backpack Full"));
        Assert.That(_run.PurchaseShopOffer(offer.StableId), Is.False);
        Assert.That(_cards.credits, Is.EqualTo(10));
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(CardManager.BACKPACK_CAPACITY));
    }

    [Test]
    public void FullBackpack_QueuesEncounterDropForExplicitReplaceOrDecline()
    {
        Assert.That(_cards.AddConsumable(_bread, CardManager.BACKPACK_CAPACITY), Is.True);
        const int nodeId = 23;
        const int mapIndex = 1;
        int context = unchecked(mapIndex * 7919 ^ nodeId);
        string seed = Enumerable.Range(0, 1000)
            .Select(index => $"BREAD-FULL-DROP-{index}")
            .First(candidate => new RunRandomContext(candidate)
                .CreateStream("consumable-drops", context).NextFloat() < _bread.dropChance);
        _run.StartRunWithSeed(seed);
        Assert.That(_cards.AddConsumable(_bread, CardManager.BACKPACK_CAPACITY), Is.True);
        var node = new PathNode { id = nodeId, mapIndex = mapIndex, kind = MapNodeType.Combat };
        var method = typeof(RunManager).GetMethod("TryGrantConsumableDropForNode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.True);
        Assert.That(_run.PendingEnemyDrop, Is.SameAs(_bread), "A drop that does not fit waits on the board.");
        Assert.That(_run.PendingConsumableReward, Is.Null, "No reward panel opens for it.");
        Assert.That(GameplayInputGate.IsBlocked, Is.False, "Tricks stay usable so room can be made.");
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(CardManager.BACKPACK_CAPACITY));
        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.False, "A resolved drop is not rolled twice for the same node.");
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(CardManager.BACKPACK_CAPACITY));
        Assert.That(_run.LeavePendingEnemyDrop(), Is.True);
        Assert.That(_run.PendingEnemyDrop, Is.Null);
    }

    [Test]
    public void WaitingDrop_HoldsTheRunUntilClaimedAfterMakingRoom()
    {
        const int nodeId = 26;
        const int mapIndex = 2;
        _run.StartRunWithSeed(SeedForBreadDrop(nodeId, mapIndex, "BREAD-WAIT-DROP"));
        Assert.That(_cards.AddConsumable(_bread, CardManager.BACKPACK_CAPACITY), Is.True);
        var node = new PathNode { id = nodeId, mapIndex = mapIndex, kind = MapNodeType.Combat };
        var method = typeof(RunManager).GetMethod("TryGrantConsumableDropForNode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.True);
        int continued = 0;
        typeof(RunManager).GetField("_afterEnemyDrop", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(_run, (System.Action)(() => continued++));
        Assert.That(_run.CanCaptureSave, Is.False, "No checkpoint while a drop waits.");

        Assert.That(_run.CanClaimPendingEnemyDrop, Is.False);
        Assert.That(_run.ClaimPendingEnemyDrop(), Is.False);
        Assert.That(continued, Is.Zero, "The run waits while the drop cannot go in.");
        Assert.That(_cards.RemoveConsumableInstanceAtSlot(0, _cards.GetConsumableInstanceAtSlot(0)), Is.True);
        ConsumableData claimed = null;
        _run.OnEnemyDropClaimed += drop => claimed = drop;
        Assert.That(_run.ClaimPendingEnemyDrop(), Is.True);

        Assert.That(claimed, Is.SameAs(_bread));
        Assert.That(continued, Is.EqualTo(1), "The run moves on once the drop is resolved.");
        Assert.That(_run.PendingEnemyDrop, Is.Null);
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(CardManager.BACKPACK_CAPACITY));
    }

    [Test]
    public void EncounterDrop_AutoClaimsIntoAvailableSlotWithoutOpeningRewardChoice()
    {
        const int nodeId = 24;
        const int mapIndex = 2;
        _run.StartRunWithSeed(SeedForBreadDrop(nodeId, mapIndex, "BREAD-AUTO-DROP"));
        var node = new PathNode { id = nodeId, mapIndex = mapIndex, kind = MapNodeType.Combat };
        var method = typeof(RunManager).GetMethod("TryGrantConsumableDropForNode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.True);

        Assert.That(_run.PendingConsumableReward, Is.Null);
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1));
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
        Assert.That(GameplayInputGate.IsBlocked, Is.False);
        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.False);
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1), "A successful drop is not duplicated on repeat resolution.");
    }

    [Test]
    public void EncounterDrop_AutoMergesIntoExistingStackEvenWhenBackpackHasNoFreeSlot()
    {
        var pack = ScriptableObject.CreateInstance<GearData>();
        _created.Add(pack);
        pack.id = "test_rare_pack";
        pack.displayName = "Test Pack";
        pack.rarity = "Common";
        pack.tier = 1;
        pack.price = 0;
        pack.specialRule = GearSpecialRule.TravelerPackRare;
        pack.effects = System.Array.Empty<GameplayEffectDefinition>();
        pack.ruleModifiers = System.Array.Empty<GameplayRuleModifierData>();
        var extras = Enumerable.Range(0, 4).Select(index =>
        {
            var item = ScriptableObject.CreateInstance<ConsumableData>();
            _created.Add(item);
            item.id = "test_extra_" + index;
            item.displayName = "Extra " + index;
            item.shopAvailable = false;
            item.dropAvailable = false;
            item.dropChance = 0f;
            return item;
        }).ToArray();
        _cards.Configure(null, null, null, new[] { pack }, System.Array.Empty<CardEnhancementData>(),
            new[] { _bread }.Concat(extras).ToArray());
        Assert.That(_cards.BuyGear(pack, 0), Is.True);
        Assert.That(_cards.AddConsumable(_bread, 2), Is.True);
        foreach (var extra in extras) Assert.That(_cards.AddConsumable(extra), Is.True);
        Assert.That(_cards.IsBackpackFull, Is.True);
        Assert.That(_cards.GetConsumableStackCountAtSlot(0), Is.EqualTo(2));
        const int nodeId = 25;
        const int mapIndex = 2;
        _run.StartRunWithSeed(SeedForBreadDrop(nodeId, mapIndex, "BREAD-STACK-DROP"));
        Assert.That(_cards.BuyGear(pack, 0), Is.True);
        Assert.That(_cards.AddConsumable(_bread, 2), Is.True);
        foreach (var extra in extras) Assert.That(_cards.AddConsumable(extra), Is.True);
        var node = new PathNode { id = nodeId, mapIndex = mapIndex, kind = MapNodeType.Combat };
        var method = typeof(RunManager).GetMethod("TryGrantConsumableDropForNode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.True);

        Assert.That(_run.PendingConsumableReward, Is.Null);
        Assert.That(_cards.IsBackpackFull, Is.True);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(CardManager.TRAVELER_BACKPACK_CAPACITY));
        Assert.That(_cards.GetConsumableStackCountAtSlot(0), Is.EqualTo(3));
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(3));
    }

    [Test]
    public void FleeOnlyVictoryDoesNotGrantAnEnemyDrop()
    {
        const int nodeId = 26;
        const int mapIndex = 2;
        _run.StartRunWithSeed(SeedForBreadDrop(nodeId, mapIndex, "BREAD-FLEE-DROP"));
        var node = new PathNode { id = nodeId, mapIndex = mapIndex, kind = MapNodeType.Combat };
        typeof(RunManager).GetField("_activeNode", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(_run, node);
        var fleeingType = EnemyTypeData.Create("Drop Test Flee", 100, 0, 0);
        fleeingType.fleeAfterPlayerTurns = 1;
        _created.Add(fleeingType);
        var fleeingEnemy = new EnemyRuntime(fleeingType);
        _combat.StartEnemy(fleeingEnemy);
        fleeingEnemy.NotifyPlayerTurnCompleted();
        typeof(CombatManager).GetMethod("CompletePlayerTurnForEnemies",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(_combat, null);
        Assert.That(_combat.Enemies, Is.Empty);
        Assert.That(_combat.EncounterDefeatedEnemyCount, Is.Zero);

        typeof(CombatManager).GetMethod("ResolveEncounter",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(_combat, new object[] { EncounterResult.Victory });

        Assert.That(_cards.GetConsumableCount(_bread), Is.Zero);
        Assert.That(_run.PendingConsumableReward, Is.Null);
        Assert.That(GameplayInputGate.IsBlocked, Is.False);
    }

    [Test]
    public void DefeatNeverGrantsAnEnemyDrop()
    {
        const int nodeId = 27;
        const int mapIndex = 2;
        _run.StartRunWithSeed(SeedForBreadDrop(nodeId, mapIndex, "BREAD-DEFEAT-DROP"));
        typeof(RunManager).GetField("_activeNode", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(_run,
            new PathNode { id = nodeId, mapIndex = mapIndex, kind = MapNodeType.Combat });
        var type = EnemyTypeData.Create("Defeated Drop Enemy", 100, 0, 0);
        _created.Add(type);
        _combat.StartEnemy(new EnemyRuntime(type));

        typeof(CombatManager).GetMethod("ResolveEncounter",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(_combat, new object[] { EncounterResult.Defeat });

        Assert.That(_cards.GetConsumableCount(_bread), Is.Zero);
        Assert.That(_run.PendingConsumableReward, Is.Null);
        Assert.That(GameplayInputGate.IsBlocked, Is.False);
    }

    [Test]
    public void UseBread_HealsUpToMaxAndConsumesOneOnlyAfterAnEffectiveUse()
    {
        Assert.That(_cards.AddConsumable(_bread), Is.True);
        Assert.That(_combat.TakeRunDamage(3), Is.EqualTo(3));
        Assert.That(_cards.CanUseConsumable(_bread, _combat), Is.True);

        Assert.That(_cards.UseConsumable(_bread, _combat), Is.True);

        Assert.That(_combat.player.currentHealth, Is.EqualTo(_combat.player.maxHealth));
        Assert.That(_cards.GetConsumableCount(_bread), Is.Zero);
        Assert.That(_cards.CanUseConsumable(_bread, _combat), Is.False);
        Assert.That(_cards.UseConsumable(_bread, _combat), Is.False);
    }

    [Test]
    public void BreadAtFullHealth_IsUnavailableAndNotConsumed()
    {
        Assert.That(_cards.AddConsumable(_bread), Is.True);

        Assert.That(_cards.CanUseConsumable(_bread, _combat), Is.False);
        Assert.That(_cards.UseConsumable(_bread, _combat), Is.False);
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1));
    }

    [Test]
    public void ShopCanSellBread_AtAuthoredPriceAndOnlyOncePerVisit()
    {
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _cards.AddCredits(10);
        _run.OnPathChosen(shop.id);
        var offer = _run.GetCurrentShopOffers().First(value => value.kind == ShopOfferKind.Consumable);

        Assert.That(offer.consumable, Is.SameAs(_bread));
        Assert.That(offer.price, Is.EqualTo(5));
        Assert.That(_run.PurchaseShopOffer(offer.StableId), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(5));
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1));
        Assert.That(_run.PurchaseShopOffer(offer.StableId), Is.False);
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1));
    }

    [Test]
    public void SellingConsumable_UsesExactInstancePaysHalfAndIgnoresRemainingCharges()
    {
        _bread.uses = 3;
        Assert.That(_cards.AddConsumable(_bread), Is.True);
        var expected = _cards.GetConsumableInstanceAtSlot(0);
        Assert.That(expected.SpendCharge(), Is.True);
        Assert.That(expected.RemainingCharges, Is.EqualTo(2));
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _run.OnPathChosen(shop.id);
        _cards.AddCredits(10);
        int creditsBefore = _cards.credits;

        Assert.That(_run.SellConsumable(0, new ConsumableInstance(_bread), _bread.price), Is.False,
            "A different instance in the same slot must not be removed.");
        Assert.That(_run.SellConsumable(0, expected, _bread.price + 1), Is.False,
            "A changed authored price invalidates the selected quote.");
        Assert.That(_run.SellConsumable(0, expected, _bread.price), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(creditsBefore + _bread.price / 2));
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
    }

    [Test]
    public void SellingIsRejectedOutsideActiveShopWithoutChangingInventoryOrCredits()
    {
        Assert.That(_cards.AddConsumable(_bread), Is.True);
        var instance = _cards.GetConsumableInstanceAtSlot(0);
        int creditsBefore = _cards.credits;
        Assert.That(_run.SellConsumable(0, instance, _bread.price), Is.False);
        Assert.That(_cards.credits, Is.EqualTo(creditsBefore));
        Assert.That(_cards.GetConsumableInstanceAtSlot(0), Is.SameAs(instance));
    }

    [Test]
    public void BreadDrop_UsesSeededChanceAndCannotResolveTwiceForOneNode()
    {
        const int nodeId = 17;
        const int mapIndex = 2;
        int context = unchecked(mapIndex * 7919 ^ nodeId);
        string seed = Enumerable.Range(0, 1000)
            .Select(index => $"BREAD-DROP-{index}")
            .First(candidate => new RunRandomContext(candidate)
                .CreateStream("consumable-drops", context).NextFloat() < _bread.dropChance);
        _run.StartRunWithSeed(seed);
        var node = new PathNode { id = nodeId, mapIndex = mapIndex, kind = MapNodeType.Combat };
        var method = typeof(RunManager).GetMethod("TryGrantConsumableDropForNode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.True);
        Assert.That(_run.PendingConsumableReward, Is.Null);
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1));
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.False);
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1));
    }

    string SeedForBreadDrop(int nodeId, int mapIndex, string prefix)
    {
        int context = unchecked(mapIndex * 7919 ^ nodeId);
        return Enumerable.Range(0, 1000).Select(index => $"{prefix}-{index}")
            .First(seed => new RunRandomContext(seed).CreateStream("consumable-drops", context).NextFloat() < _bread.dropChance);
    }

    EnemyTypeData Enemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 2, 2);
        _created.Add(enemy);
        return enemy;
    }

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
