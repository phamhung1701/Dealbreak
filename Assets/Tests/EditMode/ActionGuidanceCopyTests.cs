using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class ActionGuidanceCopyTests
{
    readonly List<Object> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    ActionButtonsUI _actions;
    TMP_Text _selection;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cards = Make<CardManager>("Copy Audit Cards");
        _cards.Configure(null, null, null, System.Array.Empty<GearData>());
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = Make<CombatManager>("Copy Audit Combat");
        _combat.ConfigurePlayer(30);
        var enemyType = EnemyTypeData.Create("Copy Audit Enemy", 100, 3, 0);
        _objects.Add(enemyType);
        _combat.StartEnemy(new EnemyRuntime(enemyType));
        _actions = Make<ActionButtonsUI>("Copy Audit Actions");
        _selection = Make<TextMeshProUGUI>("Copy Audit Selection");
        _actions.selectionLabel = _selection;
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        _cards?.CancelCardInteractions();
        foreach (var item in _objects.AsEnumerable().Reverse())
            if (item) Object.DestroyImmediate(item);
    }

    // In-game guidance was removed in favour of the tutorial: idle phases show no hint, and the
    // selection line only states what the current selection will do.
    [Test]
    public void IdlePhasesShowNoHint_SelectionsShowWhatTheyWillDo()
    {
        Refresh();
        Assert.That(_selection.text, Is.Empty);

        typeof(CombatManager).GetMethod("BeginEnemyAttack", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_combat, null);
        Refresh();
        Assert.That(_selection.text, Is.Empty);
    }

    [Test]
    public void TargetingModesShowNoInstructionText()
    {
        _cards.BeginEnemyTargeting(_ => { }, _ => true);
        Refresh();
        Assert.That(_selection.text, Is.Empty);

        _cards.CancelCardInteractions();
        _cards.BeginHandEnhancementTargeting(_ => { });
        Refresh();
        Assert.That(_selection.text, Is.Empty);
    }

    void Refresh() => typeof(ActionButtonsUI).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(_actions, null);

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _objects.Add(go);
        return go.AddComponent<T>();
    }
}
