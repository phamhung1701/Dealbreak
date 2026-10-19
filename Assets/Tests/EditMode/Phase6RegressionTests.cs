using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase6RegressionTests
{
    readonly List<ScriptableObject> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    GearData _gear;
    CardEnhancementData _enhancement;

    [SetUp]
    public void SetUp()
    {
        _gear = CreateGear("phase6_artifact", 10);
        _enhancement = CreateEnhancement("phase6_enhancement", 8, attack: 3);

        _cards = CreateObject<CardManager>("Phase6 CardManager");
        _cards.Configure(null, null, null, new[] { _gear }, new[] { _enhancement });
        _combat = CreateObject<CombatManager>("Phase6 CombatManager");
        _combat.ConfigurePlayer(30);
        _run = CreateObject<RunManager>("Phase6 RunManager");
        _run.thiefType = CreateEnemy("Thief", 10, 4, 5);
        _run.goblinType = CreateEnemy("Goblin", 15, 6, 7);
        _run.knightType = CreateEnemy("Knight", 25, 8, 10);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
        _objects.Clear();

        foreach (var asset in _assets)
            if (asset != null) Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void CachedGearOffer_ChargesDisplayedPriceEvenIfDefinitionChanges()
    {
        _run.StartRunWithSeed("PHASE6-CACHED-PRICE");
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _run.OnPathChosen(shop.id);
        var offer = _run.GetCurrentShopOffers().Single(value => value.kind == ShopOfferKind.Gear);
        int cachedPrice = offer.price;
        offer.gear.price = cachedPrice + 100;
        _cards.AddCredits(cachedPrice);

        Assert.That(_run.PurchaseShopOffer(offer.StableId), Is.True);
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_cards.HasGear(offer.gear), Is.True);
    }

    [Test]
    public void NegativePrices_CannotIncreaseCreditsOrGrantGear()
    {
        _cards.AddCredits(5);
        _gear.price = -10;

        Assert.That(_cards.SpendCredits(-1), Is.False);
        Assert.That(_cards.BuyGear(_gear), Is.False);
        Assert.That(_cards.credits, Is.EqualTo(5));
        Assert.That(_cards.ownedGear, Is.Empty);
    }

    [Test]
    public void GearAttackStacking_IsOrderIndependentAndUsesMultiplierThenFlatBonus()
    {
        _run.StartRunWithSeed("PHASE6-STACKING");
        var card = _cards.ownedCards.First(value => value.Suit == CardData.Suit.Clubs && value.Rank == CardData.Rank.Four);
        Assert.That(_cards.ApplyEnhancement(card.Id, _enhancement), Is.True);

        var club = CreateGear("club", 0);
        club.restrictToSuit = true;
        club.affectedSuit = CardData.Suit.Clubs;
        club.damageMultiplier = 2;
        var artisan = CreateGear("artisan", 0);
        artisan.requiresEnhancedCard = true;
        artisan.flatDamageBonus = 3;

        _cards.ownedGear.Clear();
        _cards.ownedGear.Add(club);
        _cards.ownedGear.Add(artisan);
        int firstOrder = _combat.CalculateCardAttackDamage(card);

        _cards.ownedGear.Clear();
        _cards.ownedGear.Add(artisan);
        _cards.ownedGear.Add(club);
        int secondOrder = _combat.CalculateCardAttackDamage(card);

        Assert.That(card.AttackValue, Is.EqualTo(7));
        Assert.That(firstOrder, Is.EqualTo(17));
        Assert.That(secondOrder, Is.EqualTo(firstOrder));
    }

    [Test]
    public void RunEffectValidation_UsesAuthoredOrderAndExplainsUnavailableCosts()
    {
        var fundedByChoice = new RunEventChoice
        {
            effects = new[]
            {
                new RunEffect { type = RunEffectType.GainCredits, amount = 5 },
                new RunEffect { type = RunEffectType.LoseCredits, amount = 5 }
            }
        };
        var maxHealthBeforeCost = new RunEventChoice
        {
            effects = new[]
            {
                new RunEffect { type = RunEffectType.GainMaxHealth, amount = 5 },
                new RunEffect { type = RunEffectType.LoseHealth, amount = 32 }
            }
        };
        var lethal = new RunEventChoice
        {
            effects = new[] { new RunEffect { type = RunEffectType.LoseHealth, amount = 30 } }
        };

        Assert.That(RunEffectResolver.CanApply(fundedByChoice, _cards, _combat), Is.True);
        Assert.That(RunEffectResolver.CanApply(maxHealthBeforeCost, _cards, _combat), Is.True);
        Assert.That(RunEffectResolver.GetFailureReason(lethal, _cards, _combat), Does.Contain("more than 30 HP"));
    }

    [Test]
    public void ShopWithNoCatalogOffers_ShowsSpecialDealsAndRemainsCompletableExactlyOnce()
    {
        _cards.Configure(null, null, null, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        _run.StartRunWithSeed("PHASE6-EMPTY-SHOP");
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        int showPathCount = 0;
        _run.OnShowPathScreen += () => showPathCount++;
        _run.OnPathChosen(shop.id);

        Assert.That(_run.GetCurrentShopOffers(), Has.Count.EqualTo(2));
        Assert.That(_run.GetCurrentShopOffers().All(offer => offer.IsService), Is.True);
        Assert.That(_run.GetCurrentShopOffers().Select(offer => offer.kind).Distinct().Count(), Is.EqualTo(2));
        _run.OnShopDone();
        _run.OnShopDone();

        Assert.That(shop.completed, Is.True);
        Assert.That(_run.ActiveNode, Is.Null);
        Assert.That(showPathCount, Is.EqualTo(1));
    }

    [Test]
    public void RunStart_ClosesResultAndDeckOverlays()
    {
        var resultHost = new GameObject("Phase6 Result Host");
        resultHost.SetActive(false);
        _objects.Add(resultHost);
        var resultPanel = new GameObject("Result Panel");
        resultPanel.transform.SetParent(resultHost.transform);
        var rewardBanner = new GameObject("Reward Banner");
        rewardBanner.transform.SetParent(resultHost.transform);
        var resultUi = resultHost.AddComponent<RunResultUI>();
        resultUi.resultPanel = resultPanel;
        resultUi.rewardBanner = rewardBanner;
        InvokeLifecycle(resultUi, "OnEnable");

        var deckHost = new GameObject("Phase6 Deck Host");
        deckHost.SetActive(false);
        _objects.Add(deckHost);
        var deckPanel = new GameObject("Deck Panel");
        deckPanel.transform.SetParent(deckHost.transform);
        var deckUi = deckHost.AddComponent<DeckViewerUI>();
        deckUi.panel = deckPanel;
        InvokeLifecycle(deckUi, "OnEnable");

        resultPanel.SetActive(true);
        rewardBanner.SetActive(true);
        deckPanel.SetActive(true);
        _run.StartRunWithSeed("PHASE6-OVERLAY-RESET");

        Assert.That(resultPanel.activeSelf, Is.False);
        Assert.That(rewardBanner.activeSelf, Is.False);
        Assert.That(deckPanel.activeSelf, Is.False);

        deckPanel.SetActive(true);
        var combatNode = _run.currentPath.First(node => node.kind == MapNodeType.Combat && node.accessible);
        _run.OnPathChosen(combatNode.id);
        Assert.That(deckPanel.activeSelf, Is.False);

        InvokeLifecycle(resultUi, "OnDisable");
        InvokeLifecycle(deckUi, "OnDisable");
    }

    static void InvokeLifecycle(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(target, null);
    }

    T CreateObject<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _objects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    GearData CreateGear(string id, int price)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = id;
        gear.displayName = id;
        gear.price = price;
        gear.damageMultiplier = 1;
        _assets.Add(gear);
        return gear;
    }

    CardEnhancementData CreateEnhancement(string id, int price, int attack = 0)
    {
        var enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        enhancement.id = id;
        enhancement.displayName = id;
        enhancement.price = price;
        enhancement.attackBonus = attack;
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
}
