using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase5BuildProgressionTests
{
    readonly List<ScriptableObject> _assets = new();
    GameObject _cardObject;
    GameObject _combatObject;
    GameObject _runObject;
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    GearData[] _gears;
    CardEnhancementData[] _enhancements;
    ConsumableData[] _enhancementConsumables;

    [SetUp]
    public void SetUp()
    {
        _gears = new[]
        {
            CreateGear("clubs", 10, gear =>
            {
                gear.restrictToSuit = true;
                gear.affectedSuit = CardData.Suit.Clubs;
                gear.damageMultiplier = 2;
            }),
            CreateGear("artisan", 12, gear =>
            {
                gear.requiresEnhancedCard = true;
                gear.flatDamageBonus = 3;
            }),
            CreateGear("victory", 14, gear => gear.healAfterVictory = 2)
        };
        _enhancements = new[]
        {
            CreateEnhancement("sharp", 5, attack: 3),
            CreateEnhancement("guard", 6, defense: 3),
            CreateEnhancement("mend", 7, heal: 2)
        };

        _enhancementConsumables = _enhancements.Select((enhancement, index) =>
            CreateEnhancementConsumable(enhancement, index)).ToArray();

        _cardObject = new GameObject("Phase5 CardManager");
        _cards = _cardObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, _gears, _enhancements, _enhancementConsumables);

        _combatObject = new GameObject("Phase5 CombatManager");
        _combat = _combatObject.AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);

        _runObject = new GameObject("Phase5 RunManager");
        _run = _runObject.AddComponent<RunManager>();
        _run.thiefType = CreateEnemy("Thief", 10, 4, 5);
        _run.goblinType = CreateEnemy("Goblin", 15, 6, 7);
        _run.knightType = CreateEnemy("Knight", 25, 8, 10);
    }

    [TearDown]
    public void TearDown()
    {
        if (_runObject != null) Object.DestroyImmediate(_runObject);
        if (_combatObject != null) Object.DestroyImmediate(_combatObject);
        if (_cardObject != null) Object.DestroyImmediate(_cardObject);
        foreach (var asset in _assets)
            if (asset != null) Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void CardEnhancement_MutatesOnlyTargetInstanceAndPreservesIdentity()
    {
        var definition = CreateCard(CardData.Suit.Clubs, CardData.Rank.Five);
        var first = new CardInstance(definition, 11);
        var second = new CardInstance(definition, 12);

        Assert.That(first.TryApplyEnhancement(_enhancements[0]), Is.True);
        Assert.That(first.Id, Is.EqualTo(11));
        Assert.That(first.AttackValue, Is.EqualTo(8));
        Assert.That(second.AttackValue, Is.EqualTo(5));
        Assert.That(second.Enhancement, Is.Null);
        Assert.That(first.TryApplyEnhancement(_enhancements[1]), Is.False);
    }

    [Test]
    public void GearHooks_AreAuthoredAndRespectSuitAndEnhancementFilters()
    {
        var club = new CardInstance(CreateCard(CardData.Suit.Clubs, CardData.Rank.Four), 1);
        var heart = new CardInstance(CreateCard(CardData.Suit.Hearts, CardData.Rank.Four), 2);

        Assert.That(_gears[0].ModifyAttack(club, club.AttackValue), Is.EqualTo(8));
        Assert.That(_gears[0].ModifyAttack(heart, heart.AttackValue), Is.EqualTo(4));
        Assert.That(_gears[1].ModifyAttack(club, club.AttackValue), Is.EqualTo(4));

        Assert.That(club.TryApplyEnhancement(_enhancements[1]), Is.True);
        Assert.That(_gears[1].ModifyAttack(club, club.AttackValue), Is.EqualTo(7));
        Assert.That(_gears[1].ModifyDefense(club, club.DefenseValue), Is.EqualTo(7));
    }

    [Test]
    public void MixedShopOffers_AreDeterministicAndIndependentFromCardStreamConsumption()
    {
        _run.StartRunWithSeed("PHASE5-SHOP");
        var firstShop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        firstShop.accessible = true;
        _run.OnPathChosen(firstShop.id);
        string first = string.Join(",", _run.GetCurrentShopOffers().Select(offer => offer.StableId));

        _run.RestartRun();
        for (int i = 0; i < 8; i++) _cards.ShuffleDeck();
        var repeatedShop = _run.currentPath.First(node => node.id == firstShop.id);
        repeatedShop.accessible = true;
        _run.OnPathChosen(repeatedShop.id);
        string repeated = string.Join(",", _run.GetCurrentShopOffers().Select(offer => offer.StableId));

        Assert.That(first, Is.Not.Empty);
        Assert.That(repeated, Is.EqualTo(first));
        Assert.That(_run.GetCurrentShopOffers().Any(offer => offer.kind == ShopOfferKind.Gear), Is.True);
        Assert.That(_run.GetCurrentShopOffers().Any(offer => offer.kind == ShopOfferKind.Enhancement), Is.False);
        Assert.That(_run.GetCurrentShopOffers().Any(offer => offer.kind == ShopOfferKind.Consumable &&
            offer.consumable != null && offer.consumable.effectType == ConsumableEffectType.ApplyEnhancement), Is.True);
    }

    [Test]
    public void ShopPurchases_DeductOnceAndRejectDuplicateOffers()
    {
        _run.StartRunWithSeed("PHASE5-BUY");
        _cards.AddCredits(100);
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _run.OnPathChosen(shop.id);

        var gearOffer = _run.GetCurrentShopOffers().First(offer => offer.kind == ShopOfferKind.Gear);
        int beforeGear = _cards.credits;
        Assert.That(_run.PurchaseShopOffer(gearOffer.StableId), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(beforeGear - gearOffer.price));
        Assert.That(_cards.HasGear(gearOffer.gear), Is.True);
        Assert.That(_run.PurchaseShopOffer(gearOffer.StableId), Is.False);

        var enhancementConsumableOffer = _run.GetCurrentShopOffers().First(offer =>
            offer.kind == ShopOfferKind.Consumable && offer.consumable.effectType == ConsumableEffectType.ApplyEnhancement);
        int beforeConsumable = _cards.credits;
        Assert.That(_run.PurchaseShopOffer(enhancementConsumableOffer.StableId), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(beforeConsumable - enhancementConsumableOffer.price));
        Assert.That(_cards.GetConsumableCount(enhancementConsumableOffer.consumable), Is.EqualTo(1));
        Assert.That(_cards.hand.All(card => card.Enhancement == null), Is.True,
            "Shop purchase adds the mapped Consumable; it does not apply its Enhancement immediately.");
        Assert.That(_run.PurchaseShopOffer(enhancementConsumableOffer.StableId), Is.False);
    }

    [Test]
    public void Restart_ClearsGearEnhancementsAndEconomyWhileConservingBaselineCards()
    {
        _run.StartRunWithSeed("PHASE5-RESET");
        _cards.AddCredits(100);
        Assert.That(_cards.BuyGear(_gears[0]), Is.True);
        Assert.That(_cards.ApplyEnhancement(_cards.ownedCards[0].Id, _enhancements[0]), Is.True);

        _run.RestartRun();

        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_cards.ownedGear, Is.Empty);
        Assert.That(_cards.gearIds, Is.Empty);
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(40));
        Assert.That(_cards.ownedCards.All(card => card.Enhancement == null), Is.True);
        Assert.That(_cards.ownedCards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(40));
        Assert.That(_cards.deck.Count + _cards.hand.Count + _cards.discardPile.Count, Is.EqualTo(40));
    }

    ConsumableData CreateEnhancementConsumable(CardEnhancementData enhancement, int index)
    {
        var consumable = ScriptableObject.CreateInstance<ConsumableData>();
        consumable.id = "patch_" + enhancement.id;
        consumable.canonicalId = "phase5_con_" + index;
        consumable.displayName = enhancement.displayName + " Patch";
        consumable.description = "Apply " + enhancement.displayName + " to a card.";
        consumable.price = enhancement.price;
        consumable.uses = 1;
        consumable.effectType = ConsumableEffectType.ApplyEnhancement;
        consumable.enhancementToApply = enhancement;
        consumable.shopAvailable = true;
        _assets.Add(consumable);
        return consumable;
    }

    GearData CreateGear(string id, int price, System.Action<GearData> configure)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = id;
        gear.displayName = id;
        gear.price = price;
        gear.damageMultiplier = 1;
        configure?.Invoke(gear);
        _assets.Add(gear);
        return gear;
    }

    CardEnhancementData CreateEnhancement(string id, int price, int attack = 0, int defense = 0, int heal = 0)
    {
        var enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        enhancement.id = id;
        enhancement.displayName = id;
        enhancement.price = price;
        enhancement.attackBonus = attack;
        enhancement.defenseBonus = defense;
        enhancement.healOnPlay = heal;
        _assets.Add(enhancement);
        return enhancement;
    }

    EnemyTypeData CreateEnemy(string name, int hp, int attack, int credits)
    {
        var enemy = EnemyTypeData.Create(name, hp, attack, credits);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }

    CardData CreateCard(CardData.Suit suit, CardData.Rank rank)
    {
        var card = CardData.Create(suit, rank);
        _assets.Add(card);
        return card;
    }
}
