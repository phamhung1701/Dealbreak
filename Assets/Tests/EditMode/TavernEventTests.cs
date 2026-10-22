using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class TavernEventTests
{
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    RunEventDefinition _tavern;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _tavern = AssetDatabase.LoadAssetAtPath<RunEventDefinition>("Assets/Data/Run/Incidents/LateNightDiner.asset");
        Assert.That(_tavern, Is.Not.Null);

        var cardObject = new GameObject("Tavern Test Cards");
        _objects.Add(cardObject);
        _cards = cardObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, System.Array.Empty<GearData>());

        var combatObject = new GameObject("Tavern Test Combat");
        _objects.Add(combatObject);
        _combat = combatObject.AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);

        var runObject = new GameObject("Tavern Test Run");
        _objects.Add(runObject);
        _run = runObject.AddComponent<RunManager>();
        _run.contentCatalog = AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/Data/Run/PrototypeRunContent.asset");
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }

    [Test]
    public void CanonicalDefinition_IsRegisteredAndUsesExistingChoiceEffects()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/Data/Run/PrototypeRunContent.asset");
        Assert.That(_tavern.id, Is.EqualTo("evt_006"));
        Assert.That(_tavern.category, Is.EqualTo(MapNodeType.Event));
        Assert.That(_tavern.title, Is.EqualTo("Late-Night Diner"));
        Assert.That(catalog.events, Does.Contain(_tavern));
        Assert.That(_tavern.choices, Has.Length.EqualTo(2));
        Assert.That(_tavern.choices[0].effects, Has.Length.EqualTo(1));
        Assert.That(_tavern.choices[0].effects[0].type, Is.EqualTo(RunEffectType.RevealMapNode));
        Assert.That(_tavern.choices[1].effects, Has.Length.EqualTo(2));
        Assert.That(_tavern.choices[1].effects[0].type, Is.EqualTo(RunEffectType.LoseCredits));
        Assert.That(_tavern.choices[1].effects[0].amount, Is.EqualTo(2));
        Assert.That(_tavern.choices[1].effects[1].type, Is.EqualTo(RunEffectType.Heal));
        Assert.That(_tavern.choices[1].effects[1].amount, Is.EqualTo(5));
    }

    [Test]
    public void Guidance_RevealsExactlyOneReachableUnknownNode_AndNotifiesMap()
    {
        var entry = new PathNode
        {
            id = 1, mapIndex = 0, kind = MapNodeType.Event, contentId = "evt_006",
            accessible = true, revealed = true, col = 0, row = 0, next = new List<int> { 3, 2 }
        };
        var nearer = new PathNode
        {
            id = 2, mapIndex = 0, kind = MapNodeType.Risk, hidden = true,
            revealed = false, col = 1, row = 1
        };
        var farther = new PathNode
        {
            id = 3, mapIndex = 0, kind = MapNodeType.Event, hidden = true,
            revealed = false, col = 2, row = 0
        };
        _run.currentPath.AddRange(new[] { entry, farther, nearer });
        var originalConnections = string.Join(",", entry.next);
        int mapNotifications = 0;
        _run.OnMapRevealChanged += () => mapNotifications++;

        _run.OnPathChosen(entry.id);
        Assert.That(_run.GetEventOptionUnavailableReason(0), Is.Empty);
        Assert.That(_run.ChooseEventOption(0), Is.True);

        Assert.That(new[] { nearer, farther }.Count(node => node.revealed), Is.EqualTo(1));
        Assert.That(_run.LastScoutedNode, Is.SameAs(nearer.revealed ? nearer : farther));
        Assert.That(mapNotifications, Is.EqualTo(1));
        Assert.That(string.Join(",", entry.next), Is.EqualTo(originalConnections));
        Assert.That(_run.currentPath, Has.Count.EqualTo(3));
        Assert.That(entry.completed, Is.True);
    }

    [Test]
    public void GuidanceIsUnavailableWhenNoEligibleHiddenNodeExists()
    {
        var entry = new PathNode
        {
            id = 1, mapIndex = 0, kind = MapNodeType.Event, contentId = "evt_006",
            accessible = true, revealed = true, col = 0, row = 0
        };
        _run.currentPath.Add(entry);
        _run.OnPathChosen(entry.id);

        Assert.That(_run.GetEventOptionUnavailableReason(0), Is.EqualTo("No eligible hidden node to reveal"));
        Assert.That(_run.ChooseEventOption(0), Is.False);
        Assert.That(entry.completed, Is.False);
        Assert.That(entry.revealed, Is.True);
    }

    [Test]
    public void TavernRest_UsesExistingOrderedCreditsAndHealEffects()
    {
        var entry = new PathNode
        {
            id = 1, mapIndex = 0, kind = MapNodeType.Event, contentId = "evt_006",
            accessible = true, revealed = true, col = 0, row = 0
        };
        _run.currentPath.Add(entry);
        _cards.AddCredits(2);
        _combat.TakeRunDamage(10);
        _run.OnPathChosen(entry.id);

        Assert.That(_run.GetEventOptionUnavailableReason(1), Is.Empty);
        Assert.That(_run.ChooseEventOption(1), Is.True);
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(25));
        Assert.That(entry.completed, Is.True);
    }
}
