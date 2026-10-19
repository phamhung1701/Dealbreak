using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase4DeterminismTests
{
    readonly List<ScriptableObject> _assets = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _assets)
            if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void RandomStreams_AreStableAndIndependent()
    {
        var first = new RunRandomContext("TEST-SEED");
        var second = new RunRandomContext("TEST-SEED");

        var firstCards = first.CreateStream("cards");
        var secondCards = second.CreateStream("cards");
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 12).Select(_ => firstCards.NextInt(0, 1000)),
            Enumerable.Range(0, 12).Select(_ => secondCards.NextInt(0, 1000)));

        var consumedShop = first.CreateStream("shop-offers", 4);
        for (int i = 0; i < 50; i++) consumedShop.NextInt(0, 1000);

        var firstMap = first.CreateStream("map-layout", 7);
        var secondMap = second.CreateStream("map-layout", 7);
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 12).Select(_ => firstMap.NextInt(0, 1000)),
            Enumerable.Range(0, 12).Select(_ => secondMap.NextInt(0, 1000)));
    }

    [Test]
    public void MapGeneration_IsDeterministicBranchingAndSupportsArbitraryIndices()
    {
        var enemies = CreateEnemies();
        var context = new RunRandomContext("MAP-SEED");

        var first = RunMapGenerator.Generate(context, 37, null, enemies);
        var second = RunMapGenerator.Generate(new RunRandomContext("MAP-SEED"), 37, null, enemies);

        Assert.That(Signature(first), Is.EqualTo(Signature(second)));
        // Route grid + Headliner + two mandatory Shop checkpoints (after Depths 2 and 5).
        Assert.That(first.Count, Is.EqualTo(RunMapGenerator.RowCount * RunMapGenerator.RouteColumnCount + 3));
        Assert.That(first.Count(node => node.isShopCheckpoint), Is.EqualTo(2));
        Assert.That(first.Count(node => node.kind == MapNodeType.Headliner), Is.EqualTo(1));
        Assert.That(first.Any(node => node.next.Count > 1), Is.True, "Expected at least one fork.");

        var incoming = first.SelectMany(node => node.next).GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
        Assert.That(incoming.Values.Any(count => count > 1), Is.True, "Expected at least one merge.");
        Assert.That(first.Any(node => node.hidden), Is.True);
        Assert.That(first.Count(node => node.kind == MapNodeType.Elite), Is.InRange(0, 2));
        Assert.That(first.Select(node => node.kind), Does.Contain(MapNodeType.Shop));
        Assert.That(first.Select(node => node.kind), Does.Contain(MapNodeType.Event));
        Assert.That(first.Select(node => node.kind), Does.Contain(MapNodeType.Upgrade));
        Assert.That(first.Select(node => node.kind), Does.Contain(MapNodeType.Risk));
    }

    [Test]
    public void EnemyAbilities_ApplyAuthoredHooks()
    {
        var type = CreateEnemy("Test Boss", 20, 5, 0);
        var guard = CreateAbility(EnemyAbilityEffect.ReduceIncomingCardDamage, 2);
        var fury = CreateAbility(EnemyAbilityEffect.IncreaseAttackAfterPlayerCard, 1);
        type.abilities = new[] { guard, fury };
        var enemy = new EnemyRuntime(type);

        var action = new CombatActionContext(1, CombatActionOrigin.PlayerCard, targetEnemy: enemy);
        int dealt = new CombatResolver().Resolve(
            new DamageRequest(action, 0, CombatDamageOrigin.Card, null, enemy, 7), null).ActualHpLost;
        foreach (var ability in type.abilities) ability.OnPlayerCardResolved(enemy, null);

        Assert.That(dealt, Is.EqualTo(5));
        Assert.That(enemy.currentHp, Is.EqualTo(15));
        Assert.That(enemy.currentAttack, Is.EqualTo(6));
    }

    EnemyTypeData[] CreateEnemies() => new[]
    {
        CreateEnemy("Thief", 10, 4, 5),
        CreateEnemy("Goblin", 15, 6, 7),
        CreateEnemy("Knight", 25, 8, 10)
    };

    EnemyTypeData CreateEnemy(string name, int hp, int attack, int credits)
    {
        var enemy = EnemyTypeData.Create(name, hp, attack, credits);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }

    EnemyAbility CreateAbility(EnemyAbilityEffect effect, int amount)
    {
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        ability.effect = effect;
        ability.amount = amount;
        _assets.Add(ability);
        return ability;
    }

    static string Signature(IEnumerable<PathNode> nodes)
    {
        return string.Join("|", nodes.Select(node =>
            $"{node.id}:{node.kind}:{node.contentId}:{node.col}:{node.row}:{node.hidden}:{string.Join(",", node.next)}"));
    }
}

public sealed class Phase4RunIntegrationTests
{
    readonly List<ScriptableObject> _assets = new();
    GameObject _cardObject;
    GameObject _combatObject;
    GameObject _runObject;
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;

    [SetUp]
    public void SetUp()
    {
        _cardObject = new GameObject("Phase4 CardManager");
        _cards = _cardObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, new[]
        {
            CreateGear("relic_a"),
            CreateGear("relic_b"),
            CreateGear("relic_c"),
            CreateGear("relic_d")
        });
        _combatObject = new GameObject("Phase4 CombatManager");
        _combat = _combatObject.AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        _runObject = new GameObject("Phase4 RunManager");
        _run = _runObject.AddComponent<RunManager>();
        _run.thiefType = CreateEnemy("Thief", 10, 4, 5);
        _run.goblinType = CreateEnemy("Goblin", 15, 6, 7);
        _run.knightType = CreateEnemy("Knight", 25, 8, 10);
    }

    [TearDown]
    public void TearDown()
    {
        if (_runObject != null) UnityEngine.Object.DestroyImmediate(_runObject);
        if (_combatObject != null) UnityEngine.Object.DestroyImmediate(_combatObject);
        if (_cardObject != null) UnityEngine.Object.DestroyImmediate(_cardObject);
        foreach (var asset in _assets)
            if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void SameSeed_ReproducesHeadlinersDeckAndMap()
    {
        _run.StartRunWithSeed("REPEATABLE");
        string firstHeadliners = HeadlinerSignature();
        string firstDeck = DeckSignature();
        string firstMap = MapSignature(_run.currentPath);

        for (int i = 0; i < 5; i++) _cards.ShuffleDeck();
        string arbitraryMapAfterCardConsumption = MapSignature(_run.GenerateMapForIndex(22));

        _run.StartRunWithSeed("REPEATABLE");

        Assert.That(HeadlinerSignature(), Is.EqualTo(firstHeadliners));
        Assert.That(DeckSignature(), Is.EqualTo(firstDeck));
        Assert.That(MapSignature(_run.currentPath), Is.EqualTo(firstMap));
        Assert.That(MapSignature(_run.GenerateMapForIndex(22)), Is.EqualTo(arbitraryMapAfterCardConsumption));
    }

    [Test]
    public void DifferentSeed_ChangesGeneratedRun()
    {
        _run.StartRunWithSeed("SEED-A");
        string first = $"{HeadlinerSignature()}::{DeckSignature()}::{MapSignature(_run.currentPath)}";

        _run.StartRunWithSeed("SEED-B");
        string second = $"{HeadlinerSignature()}::{DeckSignature()}::{MapSignature(_run.currentPath)}";

        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public void RiskEffects_CannotApplyLethalHealthCost()
    {
        _run.StartRunWithSeed("RISK-SAFETY");
        var lethal = new RunEventChoice
        {
            effects = new[] { new RunEffect { type = RunEffectType.LoseHealth, amount = 30 } }
        };
        var safe = new RunEventChoice
        {
            effects = new[]
            {
                new RunEffect { type = RunEffectType.LoseHealth, amount = 5 },
                new RunEffect { type = RunEffectType.GainCredits, amount = 10 }
            }
        };

        Assert.That(RunEffectResolver.CanApply(lethal, _cards, _combat), Is.False);
        Assert.That(RunEffectResolver.Apply(lethal, _cards, _combat), Is.False);
        Assert.That(RunEffectResolver.Apply(safe, _cards, _combat), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(25));
        Assert.That(_cards.credits, Is.EqualTo(10));
    }

    [Test]
    public void ShopOffers_AreDeterministicForSeedMapAndNode()
    {
        _run.StartRunWithSeed("SHOP-SEED");
        var firstShop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        firstShop.accessible = true;
        _run.OnPathChosen(firstShop.id);
        string firstOffers = string.Join(",", _run.GetCurrentShopOffers().Select(offer => offer.StableId));

        _run.RestartRun();
        var repeatedShop = _run.currentPath.First(node => node.id == firstShop.id);
        repeatedShop.accessible = true;
        _run.OnPathChosen(repeatedShop.id);
        string repeatedOffers = string.Join(",", _run.GetCurrentShopOffers().Select(offer => offer.StableId));

        Assert.That(firstOffers, Is.Not.Empty);
        Assert.That(repeatedOffers, Is.EqualTo(firstOffers));
    }

    [Test]
    public void RestartRun_RestoresConfiguredMaximumHealth()
    {
        _run.StartRunWithSeed("HEALTH-RESET");
        _combat.IncreasePlayerMaxHealth(7);
        _combat.TakeRunDamage(5);

        _run.RestartRun();

        Assert.That(_combat.player.maxHealth, Is.EqualTo(30));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
    }

    [Test]
    public void PlayerRuntime_RunUpgradesHealAndIncreaseMaximumHealth()
    {
        var player = new PlayerRuntime(30);
        player.TakeDamage(12);

        Assert.That(player.Heal(5), Is.EqualTo(5));
        Assert.That(player.currentHealth, Is.EqualTo(23));
        Assert.That(player.IncreaseMaxHealth(4), Is.EqualTo(4));
        Assert.That(player.maxHealth, Is.EqualTo(34));
        Assert.That(player.currentHealth, Is.EqualTo(27));
    }

    string HeadlinerSignature() => string.Join(",", _run.headlinerDeck.Select(headliner => headliner.sourceCard.DisplayName));
    string DeckSignature() => string.Join(",", _cards.deck.Select(card => card.DisplayName));
    static string MapSignature(IEnumerable<PathNode> nodes) => string.Join("|", nodes.Select(node =>
        $"{node.id}:{node.kind}:{node.contentId}:{node.hidden}:{string.Join(",", node.next)}"));

    GearData CreateGear(string id)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = id;
        gear.displayName = id;
        gear.price = 10;
        _assets.Add(gear);
        return gear;
    }

    EnemyTypeData CreateEnemy(string name, int hp, int attack, int credits)
    {
        var enemy = EnemyTypeData.Create(name, hp, attack, credits);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }
}
