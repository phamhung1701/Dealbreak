using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// An Incident choice is validated as a whole, so it can never be applied part-way.
public sealed class RunEffectAtomicityTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    ConsumableData _first, _second;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _first = Consumable("atomic_first");
        _second = Consumable("atomic_second");
        _cards = Make<CardManager>("Atomic Cards");
        _cards.Configure(null, null, null, System.Array.Empty<GearData>(),
            System.Array.Empty<CardEnhancementData>(), new[] { _first, _second });
        _combat = Make<CombatManager>("Atomic Combat");
        _combat.ConfigurePlayer(30);
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
    public void TwoTrickRewardsThatOnlyFitSeparately_AreRejectedWithoutGrantingEither()
    {
        Assert.That(_cards.AddConsumable(_first, CardManager.BASE_BACKPACK_CAPACITY - 1), Is.True);
        var choice = new RunEventChoice
        {
            effects = new[]
            {
                new RunEffect { type = RunEffectType.GainCredits, amount = 5 },
                new RunEffect { type = RunEffectType.GainConsumable, consumableId = _first.id, amount = 1 },
                new RunEffect { type = RunEffectType.GainConsumable, consumableId = _second.id, amount = 1 }
            }
        };
        Assert.That(_cards.CanAddConsumable(_first), Is.True, "Each reward fits on its own.");
        Assert.That(_cards.CanAddConsumable(_second), Is.True);

        Assert.That(RunEffectResolver.GetFailureReason(choice, _cards, _combat), Is.EqualTo("Backpack Full"));
        Assert.That(RunEffectResolver.Apply(choice, _cards, _combat), Is.False);

        Assert.That(_cards.credits, Is.Zero, "No earlier effect may be applied when the choice fails.");
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(CardManager.BASE_BACKPACK_CAPACITY - 1));
    }

    ConsumableData Consumable(string id)
    {
        var consumable = ScriptableObject.CreateInstance<ConsumableData>();
        consumable.id = id;
        consumable.eventAvailable = true;
        _created.Add(consumable);
        return consumable;
    }

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
