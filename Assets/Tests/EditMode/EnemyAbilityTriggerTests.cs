using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Enemy abilities announce when they take effect (for the on-card callout), and enemies report
// which suits they resist (for the glitching hand cards). Both are presentation reads only.
public sealed class EnemyAbilityTriggerTests
{
    readonly List<Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var created in _created) if (created != null) Object.DestroyImmediate(created);
        _created.Clear();
    }

    [Test]
    public void Regeneration_AnnouncesOnlyWhenItHeals()
    {
        var regeneration = Ability(EnemyAbilityEffect.Regeneration);
        var enemy = new EnemyRuntime(Type("Mender", 40, regeneration));
        var triggered = new List<EnemyAbility>();
        enemy.OnAbilityTriggered += triggered.Add;

        enemy.CompleteEnemyResponse();
        Assert.That(triggered, Is.Empty, "At full HP nothing heals, so nothing is announced.");

        enemy.currentHp = 20;
        enemy.CompleteEnemyResponse();
        Assert.That(triggered, Is.EqualTo(new[] { regeneration }));
        Assert.That(enemy.currentHp, Is.EqualTo(22));
    }

    [Test]
    public void SuitCall_AnnouncesTheCallAndResistsOnlyTheCalledSuit()
    {
        var call = Ability(EnemyAbilityEffect.SuitCall);
        var enemy = new EnemyRuntime(Type("Challenger", 30, call));
        var triggered = new List<EnemyAbility>();
        enemy.OnAbilityTriggered += triggered.Add;

        var suit = enemy.AnnounceSuit(new DeterministicRandom(7));

        Assert.That(triggered, Is.EqualTo(new[] { call }));
        foreach (CardData.Suit candidate in System.Enum.GetValues(typeof(CardData.Suit)))
            Assert.That(enemy.ResistsSuit(candidate), Is.EqualTo(candidate == suit));
    }

    [Test]
    public void Headliner_ResistsItsOwnSuitUntilDefeated()
    {
        var type = Type("J Diamonds");
        var source = Track(ScriptableObject.CreateInstance<CardData>());
        source.suit = CardData.Suit.Diamonds;
        type.sourceCard = source;
        var enemy = new EnemyRuntime(type);

        Assert.That(enemy.ResistsSuit(CardData.Suit.Diamonds), Is.True);
        Assert.That(enemy.ResistsSuit(CardData.Suit.Spades), Is.False);
        enemy.DefeatInstantly();
        Assert.That(enemy.ResistsSuit(CardData.Suit.Diamonds), Is.False);
    }

    EnemyTypeData Type(string name, int hp = 30, params EnemyAbility[] abilities)
    {
        var type = Track(EnemyTypeData.Create(name, hp, 3, 1));
        type.abilities = abilities;
        return type;
    }

    EnemyAbility Ability(EnemyAbilityEffect effect)
    {
        var ability = Track(ScriptableObject.CreateInstance<EnemyAbility>());
        ability.displayName = effect.ToString();
        ability.effect = effect;
        return ability;
    }

    T Track<T>(T value) where T : Object
    {
        _created.Add(value);
        return value;
    }
}
