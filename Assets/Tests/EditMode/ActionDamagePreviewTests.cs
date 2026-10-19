using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// Preview and execution share one calculation, including action-level and primed Gear bonuses.
public sealed class ActionDamagePreviewTests
{
    readonly List<Object> _created = new();
    readonly List<DamageResult> _hits = new();
    CardManager _cards;
    CombatManager _combat;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        var canvas = Make<Canvas>("Preview Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var hand = new GameObject("Preview Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _created.Add(hand);
        hand.transform.SetParent(canvas.transform, false);
        var field = hand.AddComponent<Field>();
        field.cardsHolder = (RectTransform)hand.transform;
        var drag = Make<Canvas>("Preview Drag Canvas");
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefab = new GameObject("Preview View", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        _created.Add(prefab);
        var view = prefab.AddComponent<CardView>();
        view.face = prefab.GetComponent<Image>();
        view.canvasGroup = prefab.GetComponent<CanvasGroup>();
        _cards = Make<CardManager>("Preview Cards");
        _cards.Configure(field, drag, view, System.Array.Empty<GearData>());
        _cards.ConfigureRandom(new DeterministicRandom(77));
        _combat = Make<CombatManager>("Preview Combat");
        _combat.ConfigurePlayer(30);
        _combat.CriticalChancePercent = 0;
        _combat.OnDamageResolved += _hits.Add;
        _cards.BuildDeck();
        _cards.DealHand();
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
        _hits.Clear();
    }

    [Test]
    public void ActionFlatDamage_IsInPreviewAndMatchesExecution()
    {
        var gear = Gear("preview_flat", GearSpecialRule.None);
        gear.effects = new[] { new GameplayEffectDefinition
        {
            kind = GameplayEffectKind.FlatActionDamage,
            trigger = GameplayEffectTrigger.ActionDamageCalculated,
            amount = 4,
            conditions = System.Array.Empty<GameplayEffectCondition>()
        } };
        Assert.That(_cards.BuyGear(gear, 0), Is.True);
        var card = HandView(view => view.data.Rank != CardData.Rank.Ace);
        var enemy = StartEnemy();

        int preview = _combat.PreviewActionDamage(new[] { card.data }, enemy);

        Assert.That(preview, Is.EqualTo(_combat.CalculateCardAttackDamage(card.data) + 4));
        Assert.That(_combat.TryPlayCards(new[] { card }, enemy), Is.True);
        Assert.That(FirstPlayerHit().RequestedDamage, Is.EqualTo(preview));
    }

    [Test]
    public void PrimedBalancerBonus_IsInPreviewAndMatchesExecution()
    {
        var balancer = Gear("preview_balancer", GearSpecialRule.BalancersScale);
        Assert.That(_cards.BuyGear(balancer, 0), Is.True);
        var state = _cards.GetGearInstance(balancer).State;
        state.SetCounter(GearCounterKeys.BalancersScalePreviousGroup, 1);
        state.SetCounter(GearCounterKeys.BalancersScaleBonus, 2);
        var card = HandView(view => view.data.Rank is >= CardData.Rank.Five and <= CardData.Rank.Ten);
        var enemy = StartEnemy();

        int preview = _combat.PreviewActionDamage(new[] { card.data }, enemy);

        Assert.That(preview, Is.EqualTo(_combat.CalculateCardAttackDamage(card.data) + 2),
            "The card face previously omitted primed action bonuses.");
        Assert.That(_combat.TryPlayCards(new[] { card }, enemy), Is.True);
        Assert.That(FirstPlayerHit().RequestedDamage, Is.EqualTo(preview));
    }

    [Test]
    public void Preview_DoesNotResetBalancerState()
    {
        var balancer = Gear("preview_balancer_pure", GearSpecialRule.BalancersScale);
        Assert.That(_cards.BuyGear(balancer, 0), Is.True);
        var state = _cards.GetGearInstance(balancer).State;
        state.SetCounter(GearCounterKeys.BalancersScalePreviousGroup, 1);
        state.SetCounter(GearCounterKeys.BalancersScaleBonus, 2);
        var low = HandView(view => view.data.Rank < CardData.Rank.Five);
        StartEnemy();

        _combat.PreviewActionDamage(new[] { low.data });

        Assert.That(state.GetCounter(GearCounterKeys.BalancersScaleBonus), Is.EqualTo(2),
            "Executing a repeated group resets the bonus; previewing it must not.");
    }

    EnemyRuntime StartEnemy()
    {
        var type = EnemyTypeData.Create("Preview Target", 500, 0, 0);
        _created.Add(type);
        var enemy = new EnemyRuntime(type);
        _combat.StartEnemy(enemy);
        _hits.Clear();
        return enemy;
    }

    DamageResult FirstPlayerHit() => _hits.First(hit => hit.Request.Origin == CombatDamageOrigin.Card);

    CardView HandView(System.Func<CardView, bool> predicate)
    {
        var view = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
            .FirstOrDefault(value => value.data != null && _cards.hand.Contains(value.data) && predicate(value));
        Assert.That(view, Is.Not.Null, "The seeded hand must contain a qualifying card.");
        return view;
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
