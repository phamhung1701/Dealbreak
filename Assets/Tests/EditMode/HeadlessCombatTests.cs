using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Combat rules run on committed card data, so they behave the same with or without hand views.
public sealed class HeadlessCombatTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cards = Make<CardManager>("Headless Cards");
        _cards.Configure(null, null, null, System.Array.Empty<GearData>());
        _cards.ConfigureRandom(new DeterministicRandom(9001));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = Make<CombatManager>("Headless Combat");
        _combat.ConfigurePlayer(30);
        _combat.CriticalChancePercent = 0;
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
    public void PlayAndDefend_WorkOnCardInstancesWithoutViews()
    {
        var enemy = StartEnemy(500, 3);
        var attack = _cards.hand.First(card => card.Rank != CardData.Rank.Ace);
        Assert.That(_combat.TryPlayCards(new[] { attack }, enemy), Is.True);
        Assert.That(_cards.deck, Does.Contain(attack));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));

        var block = _cards.hand.OrderByDescending(card => _combat.CalculateCardDefense(card)).First();
        Assert.That(_combat.TryDefendWithCards(new[] { block }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void OverflowDraws_AutoPlayEvenWithoutHandViews()
    {
        var overflow = Gear("headless_overflow", GearSpecialRule.OverflowCommon);
        var drawer = Gear("headless_drawer", GearSpecialRule.None);
        drawer.effects = new[] { new GameplayEffectDefinition
        {
            kind = GameplayEffectKind.Draw,
            trigger = GameplayEffectTrigger.CardCommitted,
            amount = 2,
            conditions = System.Array.Empty<GameplayEffectCondition>()
        } };
        Assert.That(_cards.BuyGear(overflow, 0), Is.True);
        Assert.That(_cards.BuyGear(drawer, 0), Is.True);
        var enemy = StartEnemy(1000, 0);
        int capacity = _cards.HandCapacity;
        Assert.That(_cards.HandCount, Is.EqualTo(capacity));

        Assert.That(_combat.TryPlayCards(new[] { _cards.hand[0] }, enemy), Is.True);

        Assert.That(_cards.HandCount, Is.EqualTo(capacity), "The card drawn past the limit auto-plays.");
        Assert.That(_cards.discardPile, Is.Empty, "The played card returns to the draw pile.");
    }

    EnemyRuntime StartEnemy(int hp, int attack)
    {
        var type = EnemyTypeData.Create("Headless Target", hp, attack, 0);
        _created.Add(type);
        var enemy = new EnemyRuntime(type);
        _combat.StartEncounter(new[] { enemy });
        return enemy;
    }

    GearData Gear(string id, GearSpecialRule rule)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = id;
        gear.displayName = id;
        gear.specialRule = rule;
        _created.Add(gear);
        _cards.gearCatalog.Add(gear);
        return gear;
    }

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
