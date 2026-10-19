using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class Phase6GearTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    ConsumableData _bread;
    ConsumableData _knife;
    ConsumableData _spadeRune;
    ConsumableData _heartRune;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _bread = LoadConsumable("Assets/Data/Consumables/Bread.asset");
        _knife = LoadConsumable("Assets/Data/Consumables/ThrowingKnife.asset");
        _spadeRune = LoadConsumable("Assets/Data/Consumables/SpadeRune.asset");
        _heartRune = LoadConsumable("Assets/Data/Consumables/HeartRune.asset");
        var allConsumables = AssetDatabase.FindAssets("t:ConsumableData", new[] { "Assets/Data/Consumables" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<ConsumableData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(value => value != null).ToArray();

        _cards = Make<CardManager>("Phase 6 Cards");
        _cards.Configure(null, null, null, Array.Empty<GearData>(), Array.Empty<CardEnhancementData>(), allConsumables);
        _cards.ConfigureRandom(new DeterministicRandom(62026));
        _cards.BuildDeck();
        _combat = Make<CombatManager>("Phase 6 Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Phase 6 Run");
        _run.thiefType = Enemy("Phase 6 Thief", 20, 3, 10);
        _run.goblinType = Enemy("Phase 6 Goblin", 20, 3, 10);
        _run.knightType = Enemy("Phase 6 Knight", 20, 3, 10);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
    }

    [Test]
    public void Phase6Assets_HaveApprovedMetadataAndExcludeGildedBlade()
    {
        var expected = new Dictionary<string, (string rarity, int price, int tier, string upgradeFrom)>
        {
            ["rel_120"] = ("Common", 20, 1, ""),
            ["rel_121"] = ("Rare", 30, 2, "rel_120"),
            ["rel_123"] = ("Common", 20, 1, ""),
            ["rel_117"] = ("Common", 20, 1, ""),
            ["rel_129"] = ("Rare", 30, 1, ""),
            ["rel_125"] = ("Common", 20, 1, ""),
            ["rel_127"] = ("Rare", 30, 1, ""),
            ["rel_124"] = ("Common", 20, 1, ""),
            ["rel_131"] = ("Rare", 30, 1, ""),
            ["rel_130"] = ("Rare", 30, 1, ""),
            ["rel_122"] = ("Common", 20, 1, "")
        };
        var assets = AssetDatabase.FindAssets("t:GearData", new[] { "Assets/Data/Relics" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GearData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(value => value != null && !string.IsNullOrEmpty(value.canonicalId) &&
                expected.ContainsKey(value.canonicalId)).ToArray();
        Assert.That(assets, Has.Length.EqualTo(expected.Count));
        foreach (var gear in assets)
        {
            var metadata = expected[gear.canonicalId];
            Assert.That((gear.rarity, gear.price, gear.tier, gear.upgradeFromId),
                Is.EqualTo(metadata), gear.canonicalId);
        }
        Assert.That(assets.Any(value => value.canonicalId == "rel_128"), Is.False);
    }

    [Test]
    public void TravelerPack_RareStacksThreeWithIndividualCharges_PartialFirst_AndShrinkIsWithdrawOnly()
    {
        var common = Gear("rel_120", GearSpecialRule.TravelerPackCommon, "Common", 1);
        var rare = Gear("rel_121", GearSpecialRule.TravelerPackRare, "Rare", 2, "rel_120");
        ConfigureGear(common, rare);
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        Assert.That(_cards.BackpackCapacity, Is.EqualTo(5));
        Assert.That(_cards.AddConsumable(_spadeRune, 3), Is.True);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(3), "Common remains non-stacking.");

        Assert.That(_cards.BuyGear(rare, 0), Is.True);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
        Assert.That(_cards.GetConsumableStackCountAtSlot(0), Is.EqualTo(3));
        _cards.DrawToHand(8);
        var target = _cards.hand.First(card => card.Suit != CardData.Suit.Spades);
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { target.Id }), Is.True);
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(2));
        target = _cards.hand.First(card => card.Suit != CardData.Suit.Spades);
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { target.Id }), Is.True);
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(1),
            "The partially used physical rune is consumed before untouched runes.");
        Assert.That(_cards.AddConsumable(_heartRune), Is.True);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(2), "Different rune suits never share a stack.");

        _cards.ownedGear.Remove(rare);
        _cards.gearIds.Remove(rare.id);
        int before = _cards.GetConsumableCount(_spadeRune);
        Assert.That(_cards.CanAddConsumable(_spadeRune), Is.False, "Lost stacking is withdraw-only.");
        Assert.That(_cards.GetConsumableCount(_spadeRune), Is.EqualTo(before), "Capacity loss never deletes items.");
    }

    [Test]
    public void TravelerPack_RareCanFillFiveStacksWithFifteenIdenticalItems()
    {
        var common = Gear("rel_120", GearSpecialRule.TravelerPackCommon, "Common", 1);
        var rare = Gear("rel_121", GearSpecialRule.TravelerPackRare, "Rare", 2, "rel_120");
        ConfigureGear(common, rare);
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        Assert.That(_cards.BuyGear(rare, 0), Is.True);
        Assert.That(_cards.CanAddConsumable(_bread, 15), Is.True);
        Assert.That(_cards.AddConsumable(_bread, 15), Is.True);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(5));
        for (int i = 0; i < 5; i++)
            Assert.That(_cards.GetConsumableStackCountAtSlot(i), Is.EqualTo(3));
        Assert.That(_cards.CanAddConsumable(_bread), Is.False);
    }

    [Test]
    public void PreparationManual_CreatesEncounterBlockPerDistinctType_BeforeShield_AndExpires()
    {
        var manual = Gear("rel_123", GearSpecialRule.PreparationManual, "Common", 1);
        ConfigureGear(manual);
        Assert.That(_cards.BuyGear(manual, 0), Is.True);
        Assert.That(_cards.AddConsumable(_bread, 2), Is.True);
        Assert.That(_cards.AddConsumable(_knife), Is.True);
        _combat.StartEnemy(new EnemyRuntime(Enemy("Preparation Target", 100, 1, 0)));
        Assert.That(_combat.player.EncounterBlock, Is.EqualTo(4));
        _combat.GrantPlayerShield(1);
        var action = new CombatActionContext(1, CombatActionOrigin.Legacy);
        var result = (DamageResult)typeof(CombatManager).GetMethod("ResolveDamage",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_combat, new object[]
            {
                new DamageRequest(action, 0, CombatDamageOrigin.EnemyAggregate, null, _combat.player, 5)
            });
        Assert.That(_combat.player.EncounterBlock, Is.Zero);
        Assert.That(result.ShieldConsumed, Is.True, "Numeric Block resolves before Shield.");
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
        _combat.ForceDefeatForDevelopment();
        Assert.That(_combat.player.EncounterBlock, Is.Zero);
    }

    [Test]
    public void FirstSuccessfulConsumableUse_DrawsAndHealsOnce_FailuresDoNotTrigger()
    {
        var pouch = Gear("rel_117", GearSpecialRule.ScavengersPouch, "Common", 1);
        var medic = Gear("rel_122", GearSpecialRule.FieldMedicsKit, "Common", 1);
        ConfigureGear(pouch, medic);
        Assert.That(_cards.BuyGear(pouch, 0), Is.True);
        Assert.That(_cards.BuyGear(medic, 0), Is.True);
        Assert.That(_cards.AddConsumable(_bread, 2), Is.True);
        _combat.TakeRunDamage(10);
        _combat.StartEnemy(new EnemyRuntime(Enemy("Use Hook Target", 100, 1, 0)));
        int baselineHand = _cards.HandCount;
        int hookCalls = 0;
        _cards.OnConsumableUsed += _ => hookCalls++;

        Assert.That(_cards.UseConsumableAtSlot(0, _combat), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(28));
        Assert.That(_cards.HandCount, Is.EqualTo(baselineHand + 1));
        Assert.That(hookCalls, Is.EqualTo(1));
        Assert.That(_cards.UseConsumableAtSlot(0, _combat), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
        Assert.That(_cards.HandCount, Is.EqualTo(baselineHand + 1));
        Assert.That(hookCalls, Is.EqualTo(2));
        Assert.That(_cards.UseConsumableAtSlot(0, _combat), Is.False);
        Assert.That(hookCalls, Is.EqualTo(2));
    }

    [Test]
    public void BadgeAndCashback_UseDisplayedPaidPrice_NoSelfBenefit_AndNoFreeReward()
    {
        var badge = Gear("rel_124", GearSpecialRule.MerchantsBadge, "Common", 1, price: 20);
        var cashback = Gear("rel_125", GearSpecialRule.CashbackToken, "Common", 1, price: 20);
        var other = Gear("test_other", GearSpecialRule.None, "Common", 1, price: 20);
        var tie = Gear("test_tie", GearSpecialRule.None, "Common", 1, price: 13);
        ConfigureGear(badge, cashback, other, tie);
        PrepareShop(cashback, badge, other, tie);
        var offers = (List<ShopOffer>)typeof(RunManager).GetField("_activeShopOffers",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_run);
        offers.Add(new ShopOffer { kind = ShopOfferKind.Investment, basePrice = 5, price = 5 });
        _cards.AddCredits(100);
        Assert.That(_run.PurchaseShopOffer("artifact:rel_125"), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(80), "Cashback does not refund its own acquisition.");
        Assert.That(_run.PurchaseShopOffer("artifact:rel_124"), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(63), "Badge is full price; existing Cashback refunds 3 Gold.");
        var otherOffer = _run.GetCurrentShopOffers().Single(value => value.gear == other);
        var tieOffer = _run.GetCurrentShopOffers().Single(value => value.gear == tie);
        var investment = _run.GetCurrentShopOffers().Single(value => value.kind == ShopOfferKind.Investment);
        Assert.That(otherOffer.price, Is.EqualTo(16), "Cached offer reprices in place without rerolling.");
        Assert.That(tieOffer.price, Is.EqualTo(10), "Badge rounds the displayed effective price nearest/ties up.");
        Assert.That(investment.price, Is.EqualTo(5), "Services are not Badge-eligible.");
        Assert.That(_run.PurchaseShopOffer(otherOffer.StableId), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(49), "16 spent then 2 refunded.");
        Assert.That(_run.PurchaseShopOffer(tieOffer.StableId), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(41), "10 spent then 2 refunded from the 1.5 Gold cashback tie.");
    }

    [Test]
    public void RewardTray_RequiresExplicitClaimReplaceOrDecline_AndGrantIsIdempotent()
    {
        Assert.That(_cards.AddConsumable(_bread, CardManager.BACKPACK_CAPACITY), Is.True);
        SetRandomContext("PHASE6-REWARD-TRAY");
        var node = new PathNode { id = 45, mapIndex = 2, kind = MapNodeType.Combat };
        var guaranteedKnife = ScriptableObject.Instantiate(_knife);
        guaranteedKnife.id = "phase6_guaranteed_knife";
        guaranteedKnife.dropAvailable = true;
        guaranteedKnife.dropChance = 1f;
        _created.Add(guaranteedKnife);
        _cards.consumableCatalog.Clear();
        _cards.consumableCatalog.Add(guaranteedKnife);
        Assert.That(Invoke<bool>("TryGrantConsumableDropForNode", node), Is.True);
        Assert.That(_run.PendingConsumableReward, Is.Not.Null);
        Assert.That(_run.ClaimPendingConsumableReward(0), Is.False);
        Assert.That(_run.ReplacePendingConsumableReward(0, 1), Is.True);
        Assert.That(_cards.GetConsumableCount(guaranteedKnife), Is.EqualTo(1));
        Assert.That(Invoke<bool>("TryGrantConsumableDropForNode", node), Is.False);
        Assert.That(_run.PendingConsumableReward, Is.Null);

        var second = new PathNode { id = 46, mapIndex = 2, kind = MapNodeType.Combat };
        Assert.That(Invoke<bool>("TryGrantConsumableDropForNode", second), Is.True);
        Assert.That(_run.DeclinePendingConsumableReward(), Is.True);
        Assert.That(_run.PendingConsumableReward, Is.Null);
    }

    [Test]
    public void MerchantGiftAndSatchel_CountOnlyPostAcquisitionPaidItemsAndWins()
    {
        var gift = Gear("rel_131", GearSpecialRule.MerchantsGift, "Rare", 1);
        var satchel = Gear("rel_129", GearSpecialRule.ScavengersSatchel, "Rare", 1);
        var a = Gear("phase6_purchase_a", GearSpecialRule.None, "Common", 1, price: 1);
        var b = Gear("phase6_purchase_b", GearSpecialRule.None, "Common", 1, price: 1);
        var c = Gear("phase6_purchase_c", GearSpecialRule.None, "Common", 1, price: 1);
        ConfigureGear(gift, satchel, a, b, c);
        _run.StartRunWithSeed("PHASE6-COUNTERS");
        Assert.That(_cards.BuyGear(gift, 0), Is.True);
        typeof(RunManager).GetMethod("CompletePaidItemPurchase", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_run, new object[] { 0, true, true });
        Assert.That(_run.PaidItemPurchaseCount, Is.Zero, "Free rewards count for neither Gift nor Cashback.");
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_cards.BuyGear(satchel, 0), Is.True);
        PrepareInjectedShop(a, b, c);
        _cards.AddCredits(3);
        Assert.That(_run.PurchaseShopOffer(a.id.Insert(0, "artifact:")), Is.True);
        Assert.That(_run.PurchaseShopOffer(b.id.Insert(0, "artifact:")), Is.True);
        Assert.That(_run.PendingConsumableReward, Is.Null);
        Assert.That(_run.PurchaseShopOffer(c.id.Insert(0, "artifact:")), Is.True);
        Assert.That(_run.PaidItemPurchaseCount, Is.EqualTo(3));
        Assert.That(_run.PendingConsumableReward.SourceLabel, Is.EqualTo("Merchant’s Gift"));
        Assert.That(_run.PendingConsumableReward.Choices.Single().effectType,
            Is.Not.EqualTo(ConsumableEffectType.ApplyEnhancement));
        Assert.That(_run.DeclinePendingConsumableReward(), Is.True);

        for (int i = 0; i < 3; i++)
            typeof(RunManager).GetMethod("ApplyVictoryGearRewards", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_run, new object[] { false });
        Assert.That(_run.CombatWinCount, Is.EqualTo(3));
        Assert.That(_run.PendingConsumableReward.SourceLabel, Is.EqualTo("Scavenger’s Satchel"));
    }

    [Test]
    public void AlchemistChoices_AreThreeEnhancementConsumables_CachedAndSameSeedStable()
    {
        var alchemist = Gear("rel_130", GearSpecialRule.AlchemistsKit, "Rare", 1);
        ConfigureGear(alchemist);
        string[] FirstChoices()
        {
            Assert.That(_cards.BuyGear(alchemist, 0), Is.True);
            typeof(RunManager).GetMethod("QueueAlchemistRewardForCurrentMap",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_run, null);
            var reward = _run.PendingConsumableReward;
            Assert.That(reward, Is.Not.Null);
            Assert.That(reward.Choices.Count, Is.EqualTo(3));
            Assert.That(reward.Choices.All(value => value.effectType == ConsumableEffectType.ApplyEnhancement), Is.True);
            var signature = reward.Choices.Select(value => value.canonicalId).ToArray();
            Assert.That(_run.DeclinePendingConsumableReward(), Is.True);
            typeof(RunManager).GetMethod("QueueAlchemistRewardForCurrentMap",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_run, null);
            Assert.That(_run.PendingConsumableReward, Is.Null, "Re-entry cannot duplicate the cached map reward.");
            return signature;
        }

        _run.StartRunWithSeed("PHASE6-ALCHEMIST");
        var first = FirstChoices();
        _run.RestartRun();
        var replay = FirstChoices();
        CollectionAssert.AreEqual(first, replay);
    }

    [Test]
    public void GoldenVault_SnapshotsAfterHeadlinerBaseAndBountySettlement_ThenRoundsTiesUpOnce()
    {
        var bounty = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/BountyLedger.asset");
        var vault = Gear("rel_127", GearSpecialRule.GoldenVault, "Rare", 1);
        Assert.That(bounty, Is.Not.Null);
        ConfigureGear(bounty, vault);
        _run.StartRunWithSeed("PHASE6-GOLDEN-VAULT");
        Assert.That(_cards.BuyGear(bounty, 0), Is.True);
        Assert.That(_cards.BuyGear(vault, 0), Is.True);
        typeof(RunManager).GetField("_activeNode", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_run, new PathNode { id = 99, mapIndex = 0, kind = MapNodeType.Headliner });
        typeof(CombatManager).GetField("_earnedCreditsReward", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_combat, 100);
        typeof(CombatManager).GetField("_isHeadlinerEncounter", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_combat, true);
        typeof(CombatManager).GetField("_isEliteOrHeadlinerEncounter", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_combat, true);
        typeof(CombatManager).GetField("_encounterResolved", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_combat, false);
        typeof(CombatManager).GetMethod("ResolveEncounter", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_combat, new object[] { EncounterResult.Victory });
        Assert.That(_cards.credits, Is.EqualTo(138),
            "100 Boss Gold becomes 125 through Bounty, then Vault grants 13 from the settled snapshot.");
    }

    void ConfigureGear(params GearData[] gearItems)
    {
        _cards.gearCatalog.Clear();
        _cards.gearCatalog.AddRange(gearItems);
    }

    GearData Gear(string canonicalId, GearSpecialRule rule, string rarity, int tier,
        string upgradeFrom = "", int price = 0)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        _created.Add(gear);
        gear.id = canonicalId;
        gear.canonicalId = canonicalId;
        gear.displayName = canonicalId;
        gear.specialRule = rule;
        gear.rarity = rarity;
        gear.tier = tier;
        gear.upgradeFromId = upgradeFrom;
        gear.price = price;
        return gear;
    }

    void PrepareInjectedShop(params GearData[] gearItems)
    {
        var active = typeof(RunManager).GetField("_activeNode", BindingFlags.Instance | BindingFlags.NonPublic);
        active.SetValue(_run, new PathNode { id = 1, mapIndex = 0, kind = MapNodeType.Shop, accessible = true });
        var offers = (List<ShopOffer>)typeof(RunManager).GetField("_activeShopOffers",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_run);
        offers.Clear();
        foreach (var gear in gearItems)
            offers.Add(new ShopOffer { kind = ShopOfferKind.Gear, gear = gear,
                basePrice = gear.price, price = gear.price });
    }

    void PrepareShop(params GearData[] gearItems)
    {
        _run.StartRunWithSeed("PHASE6-SHOP");
        ConfigureGear(gearItems);
        PrepareInjectedShop(gearItems);
    }

    void SetRandomContext(string seed) => typeof(RunManager).GetField("_randomContext",
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_run, new RunRandomContext(seed));

    T Invoke<T>(string method, params object[] args) => (T)typeof(RunManager).GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_run, args);

    ConsumableData LoadConsumable(string path)
    {
        var value = AssetDatabase.LoadAssetAtPath<ConsumableData>(path);
        Assert.That(value, Is.Not.Null, path);
        return value;
    }

    EnemyTypeData Enemy(string name, int hp, int attack, int credits)
    {
        var enemy = EnemyTypeData.Create(name, hp, attack, credits);
        _created.Add(enemy);
        return enemy;
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go.AddComponent<T>();
    }
}
