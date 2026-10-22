using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GearUpgradeIntegrationTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    GearData[] _catalog;
    GearData Load(string id) => _catalog.Single(x => x.canonicalId == id);

    [SetUp]
    public void SetUp()
    {
        _catalog = SaveContentCatalog.Load().gearItems;
        _cards = Make<CardManager>("Artifact Upgrade Cards");
        _cards.Configure(null, null, null, _catalog);
        _cards.BuildDeck();
        _combat = Make<CombatManager>("Artifact Upgrade Combat");
        _combat.ConfigurePlayer(30);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created.AsEnumerable().Reverse()) if (obj) Object.DestroyImmediate(obj);
        _created.Clear();
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go.AddComponent<T>();
    }

    [Test]
    public void CatalogContainsEveryApprovedM2TierWithValidParentsAndSpecialRules()
    {
        Assert.That(GearUpgradeResolver.ValidateCatalog(_catalog), Is.Empty);
        var expected = new (string id, string parent, int tier, GearSpecialRule rule)[] {
            ("rel_025","rel_001",2,GearSpecialRule.RareClub),
            ("rel_026","rel_002",2,GearSpecialRule.RareHeart),
            ("rel_027","rel_003",2,GearSpecialRule.RareDiamond),
            ("rel_029","rel_028",2,GearSpecialRule.RareArsenal),
            ("rel_031","",1,GearSpecialRule.GlassCommon),
            ("rel_032","rel_031",2,GearSpecialRule.GlassRare),
            ("rel_033","rel_032",3,GearSpecialRule.GlassEpic),
            ("rel_022","rel_016",2,GearSpecialRule.RareRetaliation)
        };
        foreach (var e in expected)
        {
            var item = Load(e.id);
            Assert.That(item.upgradeFromId, Is.EqualTo(e.parent), e.id);
            Assert.That(item.tier, Is.EqualTo(e.tier), e.id);
            Assert.That(item.specialRule, Is.EqualTo(e.rule), e.id);
            Assert.That(item.OccupiesCapacitySlot, Is.True, e.id);
            Assert.That(item.description, Is.Not.Empty, e.id);
        }
    }

    [Test]
    public void DuplicateGearHaveIndependentRuntimeStateAndEffectsApplyPerCopy()
    {
        var glass = _catalog.Single(item => item != null &&
            item.specialRule == GearSpecialRule.GlassCommon && item.tier == 1);
        Assert.That(_cards.BuyGear(glass, 0), Is.True);
        Assert.That(_cards.BuyGear(glass, 0), Is.True);
        var first = _cards.GetGearInstanceAt(0);
        var second = _cards.GetGearInstanceAt(1);
        Assert.That(first.Id, Is.Not.EqualTo(second.Id));
        first.State.SetCounter(91, 7);
        Assert.That(second.State.GetCounter(91), Is.Zero);
        var card = _cards.ownedCards.First(item => item != null);
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards),
            Is.EqualTo(card.BaseAttackValue + 10));
        Assert.That(_cards.GetGearInstance(glass), Is.Null,
            "Definition-based lookup is ambiguous when duplicate instances are owned.");
    }

    [Test]
    public void ManualMergeConsumesTwoExactCopiesAndCreatesFreshNextTierInstance()
    {
        var common = Load("rel_001");
        var rare = Load("rel_025");
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        var first = _cards.GetGearInstanceAt(0);
        var second = _cards.GetGearInstanceAt(1);
        Assert.That(first.Id, Is.Not.EqualTo(second.Id));
        first.State.SetCounter(4, 10);
        second.State.SetCounter(4, 20);
        int creditsBefore = _cards.credits;
        Assert.That(_cards.MergeGear(first.Id, second.Id), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(creditsBefore));
        Assert.That(_cards.ownedGear, Is.EqualTo(new[] { rare }));
        Assert.That(_cards.GearSlotsUsed, Is.EqualTo(1));
        Assert.That(_cards.GetGearInstanceAt(0).State.GetCounter(4), Is.Zero);
        Assert.That(_cards.GetGearInstance(common), Is.Null);
    }

    [Test]
    public void ExactInstanceRemovalPreservesOtherCopyState()
    {
        var glass = _catalog.Single(item => item != null &&
            item.specialRule == GearSpecialRule.GlassCommon && item.tier == 1);
        Assert.That(_cards.BuyGear(glass, 0), Is.True);
        Assert.That(_cards.BuyGear(glass, 0), Is.True);
        var first = _cards.GetGearInstanceAt(0);
        var second = _cards.GetGearInstanceAt(1);
        second.State.SetCounter(92, 4);
        Assert.That(_cards.RemoveGearInstance(first.Id), Is.True);
        Assert.That(_cards.OwnedGearInstances.Count, Is.EqualTo(1));
        Assert.That(_cards.GetGearInstanceAt(0).Id, Is.EqualTo(second.Id));
        Assert.That(_cards.GetGearInstanceAt(0).State.GetCounter(92), Is.EqualTo(4));
        Assert.That(_cards.GetGearInstance(glass), Is.SameAs(second));
    }

    [Test]
    public void ManualMergeRejectsSameInstanceAndLeavesInventoryUnchanged()
    {
        var common = Load("rel_001");
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        var instance = _cards.GetGearInstanceAt(0);
        Assert.That(_cards.MergeGear(instance.Id, instance.Id), Is.False);
        Assert.That(_cards.ownedGear, Is.EqualTo(new[] { common }));
    }

    [Test]
    public void UpgradeReplacesPredecessorInPlaceWithoutUsingAnotherSlotOrRetainingState()
    {
        var predecessor = Load("rel_001");
        var upgrade = Load("rel_025");
        Assert.That(_cards.GetGearAcquisitionUnavailableReason(upgrade), Is.EqualTo("Requires immediate predecessor"));
        Assert.That(_cards.BuyGear(predecessor, 0), Is.True);
        var oldRuntime = _cards.GetGearInstance(predecessor);
        oldRuntime.State.SetCounter(4, 9);
        int slots = _cards.GearSlotsUsed;
        Assert.That(_cards.BuyGear(upgrade, 0), Is.True);
        Assert.That(_cards.GearSlotsUsed, Is.EqualTo(slots));
        Assert.That(_cards.ownedGear.Count(x => x != null && x.canonicalId == "rel_025"), Is.EqualTo(1));
        Assert.That(_cards.ownedGear.Any(x => x != null && x.canonicalId == "rel_001"), Is.False);
        Assert.That(_cards.GetGearInstance(upgrade).State.GetCounter(4), Is.Zero);
    }

    [Test]
    public void RareClubDoublesFinalCardAttackWithoutChangingSharedCardDefinition()
    {
        var common = Load("rel_001");
        var rare = Load("rel_025");
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        Assert.That(_cards.BuyGear(rare, 0), Is.True);
        var card = _cards.ownedCards.Single(x => x.Suit == CardData.Suit.Clubs && x.Rank == CardData.Rank.Seven);
        int baseDamage = card.BaseAttackValue;
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(baseDamage * 2));
        Assert.That(card.Definition.suit, Is.EqualTo(CardData.Suit.Clubs));
    }

    [Test]
    public void GlassTiersReplaceRatherThanStackAndEpicTriplesAtFiveHpOrLess()
    {
        var common = Load("rel_031");
        var rare = Load("rel_032");
        var epic = Load("rel_033");
        var card = _cards.ownedCards.First(x => x.Rank == CardData.Rank.Seven);
        int baseDamage = card.BaseAttackValue;
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(baseDamage + 5));
        Assert.That(_cards.BuyGear(rare, 0), Is.True);
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(baseDamage * 2));
        _combat.player.TakeDamage(29);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(1));
        Assert.That(_cards.BuyGear(epic, 0), Is.True);
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(baseDamage * 3));
        _combat.player.Heal(4);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(5));
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(baseDamage * 3));
        _combat.player.Heal(1);
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(baseDamage));
    }

    [Test]
    public void ArsenalAndGlassIncomingModifiersAffectOnlyEnemyAttacksAndRoundUpOnce()
    {
        var arsenal = Load("rel_029");
        Assert.That(_cards.BuyGear(Load("rel_028"), 0), Is.True);
        Assert.That(_cards.BuyGear(arsenal, 0), Is.True);
        var action = new CombatActionContext(1, CombatActionOrigin.EnemyRetaliation);
        var attack = new DamageRequest(action, 0, CombatDamageOrigin.EnemyAggregate, null, _combat.player, 3);
        Assert.That(GameplayEffectResolver.ModifyIncomingCombatDamage(
            new IncomingDamageEffectContext(attack, _combat, _cards, 3)), Is.EqualTo(5));
        var nonAttack = new DamageRequest(action, 0, CombatDamageOrigin.Legacy, null, _combat.player, 3);
        Assert.That(GameplayEffectResolver.ModifyIncomingCombatDamage(
            new IncomingDamageEffectContext(nonAttack, _combat, _cards, 3)), Is.EqualTo(3));
    }

    [Test]
    public void RareHeartChecksHealthBeforeHealingAndDiamondDrawsOnCardCommit()
    {
        Assert.That(_cards.BuyGear(Load("rel_002"), 0), Is.True);
        Assert.That(_cards.BuyGear(Load("rel_026"), 0), Is.True);
        var heart = _cards.ownedCards.First(x => x.Suit == CardData.Suit.Hearts && x.Rank == CardData.Rank.Seven);
        typeof(PlayerRuntime).GetProperty("currentHealth").GetSetMethod(true).Invoke(_combat.player, new object[] { 14 });
        var action = new CombatActionContext(41, CombatActionOrigin.PlayerCard, _combat.player, card: heart);
        var queue = new CombatReactionQueue();
        GameplayEffectResolver.EnqueueCardCommitted(new CardCommittedEffectContext(action, heart, null, _combat, _cards, 0), queue);
        Assert.That(queue.ProcessPhase(CombatReactionPhase.CardCommitted), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(17));
        Assert.That(_cards.credits, Is.EqualTo(3));

        Assert.That(_cards.BuyGear(Load("rel_003"), 0), Is.True);
        Assert.That(_cards.BuyGear(Load("rel_027"), 0), Is.True);
        _cards.DrawToHand(3);
        int before = _cards.HandCount;
        var diamond = _cards.ownedCards.First(x => x.Suit == CardData.Suit.Diamonds && x.Rank == CardData.Rank.Seven);
        var diamondAction = new CombatActionContext(42, CombatActionOrigin.PlayerCard, _combat.player, card: diamond);
        var diamondQueue = new CombatReactionQueue();
        GameplayEffectResolver.EnqueueCardCommitted(new CardCommittedEffectContext(diamondAction, diamond, null, _combat, _cards, 0), diamondQueue);
        Assert.That(diamondQueue.ProcessPhase(CombatReactionPhase.CardCommitted), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(before + 2));
    }

    [TestCase(2, 3, 100, 2)]
    [TestCase(3, 2, 100, 2)]
    [TestCase(4, 2, 100, 2)]
    [TestCase(0, 3, 100, 0)]
    [TestCase(8, 0, 100, 0)]
    [TestCase(4, 4, 150, 6)]
    [TestCase(5, 5, 150, 7)]
    [TestCase(3, 3, 200, 6)]
    public void RetaliationUsesActuallyBlockedDamage_ScaledAndRoundedDown(int block, int attack, int percent, int expected)
    {
        Assert.That(GameplayEffectResolver.CalculateRetaliationDamage(block, attack, percent), Is.EqualTo(expected));
    }

    [Test]
    public void ShopEligibilityRejectsStandaloneHigherTiersAndAllowsOnlyImmediateUpgrade()
    {
        var common = Load("rel_031");
        var rare = Load("rel_032");
        var epic = Load("rel_033");
        Assert.That(_cards.GetGearAcquisitionUnavailableReason(rare), Is.EqualTo("Requires immediate predecessor"));
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        Assert.That(_cards.GetGearAcquisitionUnavailableReason(rare), Is.Empty);
        Assert.That(_cards.GetGearAcquisitionUnavailableReason(epic), Is.EqualTo("Requires immediate predecessor"));
        Assert.That(_cards.BuyGear(rare, 0), Is.True);
        Assert.That(_cards.GetGearAcquisitionUnavailableReason(epic), Is.Empty);
    }
}
