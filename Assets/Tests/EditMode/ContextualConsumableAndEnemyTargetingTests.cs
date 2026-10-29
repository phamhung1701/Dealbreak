using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ContextualConsumableAndEnemyTargetingTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    ActionButtonsUI _actions;
    Button _attackButton;
    Button _backpackButton;
    ConsumableData _heal;
    ConsumableData _knife;
    ConsumableData _other;
    ConsumableData[] _extras;
    GearData _stackGear;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _knife = UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Tricks/ThrowingStar.asset");
        Assert.That(_knife, Is.Not.Null);
        _heal = ScriptableObject.CreateInstance<ConsumableData>();
        _created.Add(_heal);
        _heal.id = "ui_test_heal";
        _heal.displayName = "Test Tonic";
        _heal.description = "Restore 5 health.";
        _heal.price = 10;
        _heal.uses = 3;
        _heal.effectType = ConsumableEffectType.Heal;
        _heal.healAmount = 5;
        _other = ScriptableObject.CreateInstance<ConsumableData>();
        _created.Add(_other);
        _other.id = "ui_test_other";
        _other.displayName = "Other Trick";
        _other.price = 7;
        _other.effectType = ConsumableEffectType.Heal;
        _other.healAmount = 1;
        _extras = Enumerable.Range(0, 2).Select(index =>
        {
            var item = ScriptableObject.CreateInstance<ConsumableData>();
            _created.Add(item);
            item.id = "ui_test_extra_" + index;
            item.displayName = "Extra Trick " + index;
            item.effectType = ConsumableEffectType.Heal;
            item.healAmount = 1;
            return item;
        }).ToArray();
        _stackGear = ScriptableObject.CreateInstance<GearData>();
        _created.Add(_stackGear);
        _stackGear.id = "test_stack_relic";
        _stackGear.displayName = "Test Stack Pack";
        _stackGear.price = 0;
        _stackGear.specialRule = GearSpecialRule.TravelerPackRare;

        var fieldRoot = Create("Targeting Test Hand", typeof(RectTransform), typeof(Field));
        var field = fieldRoot.GetComponent<Field>();
        field.cardsHolder = fieldRoot.GetComponent<RectTransform>();
        var dragCanvasObject = Create("Targeting Test Drag Canvas", typeof(Canvas));
        var cardPrefabObject = Create("Targeting Test Card Prefab", typeof(RectTransform), typeof(Image), typeof(CardView));
        _cards = Create("Targeting Test Cards", typeof(CardManager)).GetComponent<CardManager>();
        _cards.Configure(field, dragCanvasObject.GetComponent<Canvas>(), cardPrefabObject.GetComponent<CardView>(),
            new[] { _stackGear }, System.Array.Empty<CardEnhancementData>(), new[] { _knife, _heal, _other }.Concat(_extras).ToArray());
        _cards.ConfigureRandom(new DeterministicRandom(540021));
        _cards.BuildDeck();
        Assert.That(_cards.BuyGear(_stackGear, 0), Is.True);

        _combat = Create("Targeting Test Combat", typeof(CombatManager)).GetComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        _run = Create("Targeting Test Run", typeof(RunManager)).GetComponent<RunManager>();
        CreateActionButtons();
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        foreach (var item in _created.AsEnumerable().Reverse())
            if (item) Object.DestroyImmediate(item);
        _created.Clear();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ClearingCardState_CancelsEnemyTargeting(bool rebuildDeck)
    {
        bool notified = false;
        _cards.BeginEnemyTargeting(_ => Assert.Fail("Stale target callback invoked."), _ => true);
        _cards.OnEnemyTargetingChanged += active => { if (!active) notified = true; };

        if (rebuildDeck) _cards.BuildDeck();
        else _cards.Reset();

        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(notified, Is.True);
        Assert.That(_cards.HandleEnemyTargetClick(null), Is.False);
    }

    [Test]
    public void TrickSlotClickNeverUsesTheTrick()
    {
        _combat.player.TakeDamage(10);
        Assert.That(_cards.AddConsumable(_heal), Is.True);
        _backpackButton.onClick.Invoke();

        Assert.That(_combat.player.currentHealth, Is.EqualTo(20), "Clicking a Trick shows it; dragging uses it.");
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(_heal.uses));
    }

    [Test]
    public void HealDroppedOnTheHpBar_HealsOnceAndPreviewsTheHpRestored()
    {
        _combat.player.TakeDamage(3);
        Assert.That(_cards.AddConsumable(_heal), Is.True);

        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.PlayerHealth, out var preview), Is.True);
        Assert.That(preview, Is.EqualTo("+3 HP"), "The preview is the HP actually restored, not the authored amount.");
        Assert.That(ItemDropRules.DropTrick(0, ItemDropTarget.PlayerHealth), Is.True);

        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(_heal.uses - 1));
        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.PlayerHealth, out _), Is.False, "Nothing to heal at full HP.");
    }

    [Test]
    public void DamageTrickDroppedOnAnEnemy_HitsExactlyThatEnemy()
    {
        var enemies = StartEncounter(2);
        Assert.That(_cards.AddConsumable(_knife), Is.True);
        var stranger = EnemyTypeData.Create("Not in this encounter", 50, 0, 0);
        _created.Add(stranger);

        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.OnEnemy(new EnemyRuntime(stranger)), out _), Is.False);
        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.PlayerHealth, out _), Is.False);
        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.OnEnemy(enemies[1]), out var preview), Is.True);
        Assert.That(preview, Is.EqualTo($"−{_knife.damageAmount}"));
        Assert.That(ItemDropRules.DropTrick(0, ItemDropTarget.OnEnemy(enemies[1])), Is.True);

        Assert.That(enemies[0].currentHp, Is.EqualTo(enemies[0].maxHp));
        Assert.That(enemies[1].currentHp, Is.EqualTo(enemies[1].maxHp - _knife.damageAmount));
        Assert.That(_cards.GetConsumableAtSlot(0), Is.Null);
    }

    [Test]
    public void SellDrop_WorksOnlyInABlackMarketAndPaysHalfOnce()
    {
        Assert.That(_cards.AddConsumable(_heal), Is.True);
        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.BlackMarket, out _), Is.False);
        Assert.That(ItemDropRules.DropTrick(0, ItemDropTarget.BlackMarket), Is.False);

        var shop = new PathNode { kind = MapNodeType.Shop, completed = false };
        typeof(RunManager).GetField("_activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_run, shop);
        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.BlackMarket, out var preview), Is.True);
        Assert.That(preview, Is.EqualTo("SELL FOR 5 CREDITS"));
        Assert.That(ItemDropRules.DropTrick(0, ItemDropTarget.BlackMarket), Is.True);
        Assert.That(ItemDropRules.DropTrick(0, ItemDropTarget.BlackMarket), Is.False);

        Assert.That(_cards.GetConsumableAtSlot(0), Is.Null);
        Assert.That(_cards.credits, Is.EqualTo(5));
    }

    [Test]
    public void AttackButtonResolvesDirectlyWhenOnlyOneEnemyIsValid()
    {
        var enemy = StartEncounter(1)[0];
        var card = SelectHandCard();

        _attackButton.onClick.Invoke();

        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(enemy.currentHp, Is.LessThan(enemy.maxHp));
        Assert.That(_cards.hand.Contains(card.data), Is.False);
    }

    [Test]
    public void AttackButtonUsesActualEnemyViewWhenMultipleTargetsAreValid_AndCancelRestoresInput()
    {
        var enemies = StartEncounter(2);
        SelectHandCard();

        _attackButton.onClick.Invoke();

        Assert.That(_cards.IsEnemyTargeting, Is.True);
        Assert.That(enemies[0].currentHp, Is.EqualTo(enemies[0].maxHp));
        Assert.That(enemies[1].currentHp, Is.EqualTo(enemies[1].maxHp));
        var clickTarget = CreateEnemyView(enemies[1]);
        clickTarget.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });

        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(enemies[0].currentHp, Is.EqualTo(enemies[0].maxHp));
        Assert.That(enemies[1].currentHp, Is.LessThan(enemies[1].maxHp));

        var selected = SelectHandCard();
        _attackButton.onClick.Invoke();
        Assert.That(_cards.IsEnemyTargeting, Is.True);
        _cards.EndEnemyTargeting();
        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(_cards.hand.Contains(selected.data), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void RemovingAttackSourceClearsEnemyTargetingWithoutResolving()
    {
        var enemies = StartEncounter(2);
        var card = SelectHandCard();
        _attackButton.onClick.Invoke();
        Assert.That(_cards.IsEnemyTargeting, Is.True);
        Assert.That(_cards.TryDiscard(card), Is.True);
        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(enemies.All(enemy => enemy.currentHp == enemy.maxHp), Is.True);
    }

    [Test]
    public void PrimaryActionBecomesClickableCancelAndPreservesAttackSelection()
    {
        StartEncounter(2);
        var card = SelectHandCard();
        _attackButton.onClick.Invoke();
        Assert.That(_attackButton.interactable, Is.True);
        Assert.That(_attackButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("CANCEL"), "S13: upper case like the other action buttons.");
        _attackButton.onClick.Invoke();
        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(_cards.SelectedCards, Does.Contain(card));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_attackButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("ATTACK"));
    }

    EnemyRuntime[] StartEncounter(int count)
    {
        var definition = EnemyTypeData.Create("Direct target test enemy", 100, 0, 0);
        _created.Add(definition);
        var enemies = Enumerable.Range(0, count).Select(i => new EnemyRuntime(definition, i + 1)).ToArray();
        _combat.StartEncounter(enemies);
        _cards.DealHand();
        return enemies;
    }

    CardView SelectHandCard()
    {
        var instance = _cards.hand[0];
        var view = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>(true)
            .Single(card => card.data == instance);
        _cards.SelectOnlyCard(view);
        Assert.That(_cards.SelectedCards, Does.Contain(view));
        return view;
    }

    CardActionDropTarget CreateEnemyView(EnemyRuntime enemy)
    {
        var go = Create("Actual Enemy View", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(EnemyDisplayUI), typeof(CardActionDropTarget));
        var display = go.GetComponent<EnemyDisplayUI>();
        display.portrait = go.GetComponent<Image>();
        display.Bind(enemy);
        var target = go.GetComponent<CardActionDropTarget>();
        target.enemyDisplay = display;
        return target;
    }

    void CreateActionButtons()
    {
        var root = Create("Targeting Test Player Area", typeof(RectTransform));
        root.SetActive(false);
        _actions = root.AddComponent<ActionButtonsUI>();
        var attackObject = Create("Attack Action", typeof(RectTransform), typeof(Image), typeof(Button));
        attackObject.transform.SetParent(root.transform, false);
        _attackButton = attackObject.GetComponent<Button>();
        _attackButton.targetGraphic = attackObject.GetComponent<Image>();
        var attackLabelObject = Create("Attack Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        attackLabelObject.transform.SetParent(attackObject.transform, false);
        _actions.playButton = _attackButton;
        var backpackObject = Create("Backpack Slot", typeof(RectTransform), typeof(Image), typeof(Button));
        backpackObject.transform.SetParent(root.transform, false);
        _backpackButton = backpackObject.GetComponent<Button>();
        _backpackButton.targetGraphic = backpackObject.GetComponent<Image>();
        var backpackLabel = Create("Backpack Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        backpackLabel.transform.SetParent(backpackObject.transform, false);
        var backpackObject2 = Create("Backpack Slot 2", typeof(RectTransform), typeof(Image), typeof(Button));
        backpackObject2.transform.SetParent(root.transform, false);
        var backpackButton2 = backpackObject2.GetComponent<Button>();
        backpackButton2.targetGraphic = backpackObject2.GetComponent<Image>();
        var backpackLabel2 = Create("Backpack Label 2", typeof(RectTransform), typeof(TextMeshProUGUI));
        backpackLabel2.transform.SetParent(backpackObject2.transform, false);
        _actions.backpackSlotButtons = new[] { _backpackButton, backpackButton2 };
        root.SetActive(true);
        InvokePrivate(_actions, "OnDisable");
        InvokePrivate(_actions, "OnEnable");
    }

    T GetPrivate<T>(object target, string name) where T : class =>
        typeof(ActionButtonsUI).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target) as T;

    static void InvokePrivate(object target, string name) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    GameObject Create(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        _created.Add(go);
        return go;
    }
}
