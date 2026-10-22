using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class NobleFamilyEventTests
{
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    CombatManager _combat;

    [SetUp]
    public void SetUp()
    {
        var cardObject = new GameObject("Noble Family Test Cards");
        _objects.Add(cardObject);
        _cards = cardObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, System.Array.Empty<GearData>());

        var combatObject = new GameObject("Noble Family Test Combat");
        _objects.Add(combatObject);
        _combat = combatObject.AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var gameObject in _objects)
            if (gameObject != null) Object.DestroyImmediate(gameObject);
        _objects.Clear();
    }

    [Test]
    public void CanonicalEvent_IsRegisteredWithCorrectChoicesAndEffects()
    {
        var definition = AssetDatabase.LoadAssetAtPath<RunEventDefinition>("Assets/Data/Run/Incidents/VIPParty.asset");
        var catalog = AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/Data/Run/PrototypeRunContent.asset");

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.id, Is.EqualTo("evt_009"));
        Assert.That(definition.title, Is.EqualTo("VIP Party"));
        Assert.That(definition.category, Is.EqualTo(MapNodeType.Event));
        Assert.That(catalog.events, Does.Contain(definition));
        Assert.That(definition.choices, Has.Length.EqualTo(2));
        Assert.That(definition.choices[0].effects, Is.Empty, "Refusing has no gameplay effect.");
        Assert.That(definition.choices[1].effects, Has.Length.EqualTo(1));
        Assert.That(definition.choices[1].effects[0].type, Is.EqualTo(RunEffectType.GainCredits));
        Assert.That(definition.choices[1].effects[0].amount, Is.EqualTo(10));
    }

    [Test]
    public void RefuseDoesNothing_AndAcceptGrantsTenCreditsThroughExistingResolver()
    {
        var definition = AssetDatabase.LoadAssetAtPath<RunEventDefinition>("Assets/Data/Run/Incidents/VIPParty.asset");
        Assert.That(RunEffectResolver.Apply(definition.choices[0], _cards, _combat), Is.True);
        Assert.That(_cards.credits, Is.Zero);

        Assert.That(RunEffectResolver.Apply(definition.choices[1], _cards, _combat), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(10));
    }
}
