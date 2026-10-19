using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class PassPlayerTurnUITests
{
    readonly List<Object> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    ActionButtonsUI _actions;
    Button _passButton;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cards = Make<CardManager>("Pass UI Cards");
        _cards.Configure(null, null, null, System.Array.Empty<GearData>());
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = Make<CombatManager>("Pass UI Combat");
        _combat.ConfigurePlayer(30);

        var root = MakeInactive("Pass UI Action Root");
        _actions = root.AddComponent<ActionButtonsUI>();
        var play = MakeButton(root.transform, "Play Action");
        _passButton = MakeButton(root.transform, "Pass Action");
        _actions.playButton = play;
        _actions.passButton = _passButton;
        root.SetActive(true);
        typeof(ActionButtonsUI).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_actions, null);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        if (_actions != null)
            typeof(ActionButtonsUI).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_actions, null);
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i]) Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }

    [Test]
    public void PassButtonCommitsEnemyResponseThenAcceptsIncomingDamage()
    {
        Assert.That(_passButton.gameObject.activeSelf, Is.False, "No Pass action is shown before the player turn.");
        var type = EnemyTypeData.Create("Pass UI Enemy", 100, 4, 0);
        _objects.Add(type);
        _combat.StartEnemy(new EnemyRuntime(type));
        Assert.That(_passButton.gameObject.activeSelf, Is.True);
        Assert.That(_passButton.interactable, Is.True);

        _passButton.onClick.Invoke();

        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(_combat.pendingDamage, Is.EqualTo(4));
        Assert.That(_passButton.gameObject.activeSelf, Is.True);
        Assert.That(_passButton.interactable, Is.True, "The shared button accepts incoming damage during retaliation.");
        Assert.That(_combat.CompletedPlayerTurnCount, Is.EqualTo(1));
        int hp = _combat.player.currentHealth;
        _passButton.onClick.Invoke();
        Assert.That(_combat.player.currentHealth, Is.EqualTo(hp - 4));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_passButton.gameObject.activeSelf, Is.True);
        Assert.That(_passButton.interactable, Is.True);
    }

    Button MakeButton(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        _objects.Add(go);
        go.transform.SetParent(parent, false);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        return button;
    }

    GameObject MakeInactive(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        _objects.Add(go);
        go.SetActive(false);
        return go;
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _objects.Add(go);
        return go.AddComponent<T>();
    }
}
