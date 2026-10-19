using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Every combat command ends through one terminal check; no live state may be left without a legal exit.
public sealed class CombatCompletionTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    readonly List<EncounterResult> _results = new();

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cards = Make<CardManager>("Completion Cards");
        _cards.Configure(null, null, null, System.Array.Empty<GearData>());
        _cards.ConfigureRandom(new DeterministicRandom(4411));
        _cards.BuildDeck();
        _cards.DrawToHand(5);
        _combat = Make<CombatManager>("Completion Combat");
        _combat.ConfigurePlayer(30);
        _combat.OnEncounterResult += _results.Add;
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
        _results.Clear();
    }

    [Test]
    public void LethalTrickWhileBlocking_LastEnemy_ResolvesVictoryAndDiscardsPendingAttacks()
    {
        var enemy = new EnemyRuntime(Enemy("Blocking Victim", 5, 3, 4));
        _combat.StartEncounter(new[] { enemy });
        Assert.That(_combat.PassPlayerTurn(), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(_combat.pendingDamage, Is.EqualTo(3));
        int creditsBefore = _cards.credits;

        Assert.That(_combat.TryUseConsumableDamage(10, enemy), Is.True);

        Assert.That(enemy.IsDefeated, Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameWon));
        Assert.That(_combat.pendingDamage, Is.Zero);
        Assert.That(_combat.PendingAttackCount, Is.Zero);
        Assert.That(_results, Is.EqualTo(new[] { EncounterResult.Victory }), "Victory resolves exactly once.");
        Assert.That(_cards.credits, Is.EqualTo(creditsBefore + 4));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30), "Discarded pending attacks deal no damage.");
    }

    [Test]
    public void LethalTrickWhileBlocking_WithSurvivor_KeepsBlockingTheRemainingAttacks()
    {
        var victim = new EnemyRuntime(Enemy("Victim", 5, 3, 0), 1, 2);
        var survivor = new EnemyRuntime(Enemy("Survivor", 30, 4, 0), 2, 2);
        _combat.StartEncounter(new[] { victim, survivor });
        Assert.That(_combat.PassPlayerTurn(), Is.True);

        Assert.That(_combat.TryUseConsumableDamage(10, victim), Is.True);

        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(_results, Is.Empty);
        _combat.TakeRemainingDamage();
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn),
            "With a survivor, the response completes normally and the player acts again.");
    }

    [Test]
    public void LethalTrickOnPlayerTurn_StillResolvesVictory()
    {
        var enemy = new EnemyRuntime(Enemy("Turn Victim", 5, 3, 0));
        _combat.StartEncounter(new[] { enemy });

        Assert.That(_combat.TryUseConsumableDamage(10, enemy), Is.True);

        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameWon));
        Assert.That(_results, Is.EqualTo(new[] { EncounterResult.Victory }));
    }

    EnemyTypeData Enemy(string name, int hp, int attack, int credits)
    {
        var type = EnemyTypeData.Create(name, hp, attack, credits);
        _created.Add(type);
        return type;
    }

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
