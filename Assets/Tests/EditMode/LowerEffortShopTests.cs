using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class LowerEffortShopTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    const string Seed = "LOWER-EFFORT-SHOP-TEST";

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cards = Make<CardManager>("Shop Feature Cards");
        _cards.Configure(null, null, null, System.Array.Empty<GearData>());
        _combat = Make<CombatManager>("Shop Feature Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Shop Feature Run");
        _run.thiefType = Enemy("Pickpocket");
        _run.goblinType = Enemy("Scrapper");
        _run.knightType = Enemy("SecurityGuard");
        _run.StartRunWithSeed(Seed);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void ServiceDescriptions_PreserveInvestmentAndGuidanceMechanics()
    {
        OpenShop(Shop(3, 0));
        var investment = FindOffer(ShopOfferKind.Investment);
        var guidance = FindOffer(ShopOfferKind.Guidance);
        Assert.That(investment.investmentStake, Is.Zero);
        Assert.That(investment.Description(_cards), Does.Contain("3×"));
        Assert.That(guidance.Description(_cards), Does.Contain("hidden"));
    }

    [Test]
    public void Investment_ResolvesOnceOnTheNextShopWithSeededFiftyPercentReturn()
    {
        var first = Shop(3, 0, 2);
        var second = Shop(2, 0, 3);
        var third = Shop(4, 0);
        _run.currentPath.Clear();
        _run.currentPath.AddRange(new[] { first, second, third });
        _cards.AddCredits(5);
        _run.OnPathChosen(first.id);
        var investment = FindOffer(ShopOfferKind.Investment);

        Assert.That(_run.SetInvestmentStake(investment.StableId, 5), Is.True);
        Assert.That(_run.SetInvestmentStake(investment.StableId, 0), Is.False);
        Assert.That(_run.SetInvestmentStake(investment.StableId, int.MaxValue), Is.False);
        var transactionField = typeof(RunManager).GetField("_shopTransaction",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        transactionField.SetValue(_run, true);
        Assert.That(_run.SetInvestmentStake(investment.StableId, 4), Is.False,
            "Reentrant quantity changes are rejected while a shop transaction is committing.");
        transactionField.SetValue(_run, false);
        Assert.That(investment.investmentStake, Is.EqualTo(5));
        Assert.That(_run.PurchaseShopOffer(investment.StableId), Is.True);
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_run.PendingInvestmentCount, Is.EqualTo(1));
        Assert.That(_run.CanPurchaseShopOffer(investment.StableId), Is.False, "Investment is one purchase per shop visit.");
        int resultNotifications = 0;
        string resultMessage = null;
        _run.OnInvestmentResult += message => { resultNotifications++; resultMessage = message; };
        _run.OnShopDone();
        _run.OnPathChosen(second.id);

        var expectedRandom = new RunRandomContext(Seed).CreateStream("shop-investment-payout", unchecked(second.mapIndex * 7919 ^ second.id));
        int expectedCredits = expectedRandom.NextInt(0, 2) == 0 ? 15 : 0;
        Assert.That(_cards.credits, Is.EqualTo(expectedCredits));
        Assert.That(resultNotifications, Is.EqualTo(1));
        Assert.That(resultMessage, Does.Contain(expectedCredits > 0 ? "1 success" : "1 loss").IgnoreCase);
        Assert.That(_run.PendingInvestmentCount, Is.Zero);
        int resolvedCredits = _cards.credits;
        _run.OnShopDone();
        _run.OnPathChosen(third.id);
        Assert.That(_cards.credits, Is.EqualTo(resolvedCredits), "A settled investment must not resolve twice.");
        Assert.That(_run.PendingInvestmentCount, Is.Zero);
        Assert.That(resultNotifications, Is.EqualTo(1), "The investment result is announced only once.");
    }

    [Test]
    public void Guidance_CostsFiveCreditsRevealsOneReachableUnknownNodeAndIsOneTime()
    {
        // Node IDs must be unique: route lookup resolves the first node with a given ID.
        var shop = Shop(3, 0, 2, 4);
        var farther = new PathNode { id = 2, mapIndex = 0, kind = MapNodeType.Event, hidden = true, col = 2, row = 0 };
        var nearer = new PathNode { id = 4, mapIndex = 0, kind = MapNodeType.Risk, hidden = true, col = 1, row = 0 };
        _run.currentPath.Clear();
        _run.currentPath.AddRange(new[] { shop, farther, nearer });
        _cards.AddCredits(5);
        _run.OnPathChosen(shop.id);
        int notifications = 0;
        _run.OnMapRevealChanged += () => notifications++;
        var guidance = FindOffer(ShopOfferKind.Guidance);

        Assert.That(_run.PurchaseShopOffer(guidance.StableId), Is.True);

        Assert.That(_cards.credits, Is.Zero);
        Assert.That(new[] { nearer, farther }.Count(node => node.revealed), Is.EqualTo(1),
            "Intel scouts exactly one of the reachable unknown nodes.");
        Assert.That(_run.LastScoutedNode, Is.SameAs(nearer.revealed ? nearer : farther));
        Assert.That(notifications, Is.EqualTo(1));
        Assert.That(_run.CanPurchaseShopOffer(guidance.StableId), Is.False);
    }

    [Test]
    public void Guidance_IsUnavailableWithoutReachableEligibleHiddenNode()
    {
        var shop = Shop(3, 0);
        _run.currentPath.Clear();
        _run.currentPath.Add(shop);
        _cards.AddCredits(5);
        _run.OnPathChosen(shop.id);
        var guidance = FindOffer(ShopOfferKind.Guidance);

        Assert.That(_run.GetShopOfferUnavailableReason(guidance.StableId), Is.EqualTo("No eligible hidden node to reveal"));
        Assert.That(_run.PurchaseShopOffer(guidance.StableId), Is.False);
        Assert.That(_cards.credits, Is.EqualTo(5));
    }

    void OpenShop(PathNode shop)
    {
        _run.currentPath.Clear();
        _run.currentPath.Add(shop);
        _run.OnPathChosen(shop.id);
    }

    ShopOffer FindOffer(ShopOfferKind kind)
    {
        var offers = (List<ShopOffer>)_run.GetCurrentShopOffers();
        var existing = System.Linq.Enumerable.FirstOrDefault(offers, offer => offer.kind == kind);
        if (existing != null) return existing;
        // Mechanic fixtures select the service explicitly; seeded pairs are covered by CombatShopEconomyTests.
        var added = new ShopOffer { kind = kind, basePrice = 5, price = 5 };
        offers.Add(added);
        return added;
    }

    PathNode Shop(int id, int mapIndex, params int[] next) => new()
    {
        id = id, mapIndex = mapIndex, kind = MapNodeType.Shop, accessible = true,
        revealed = true, col = id, next = new List<int>(next)
    };

    EnemyTypeData Enemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 2, 2);
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
