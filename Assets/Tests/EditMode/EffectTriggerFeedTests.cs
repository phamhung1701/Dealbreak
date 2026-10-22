using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The trigger feed reports each Mod and Gear activation exactly as it executes, for its animation,
/// and previews never report.
/// </summary>
public sealed class EffectTriggerFeedTests
{
    readonly List<Object> _created = new();
    readonly List<EffectTrigger> _reported = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        var canvas = CreateComponent<Canvas>("Feed Test Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Feed Test Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var dragCanvas = CreateComponent<Canvas>("Feed Test Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = CreateGameObject("Feed Test Card Prefab", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = CreateComponent<CardManager>("Feed Test Cards");
        _cards.Configure(_field, dragCanvas, prefab, System.Array.Empty<GearData>(),
            System.Array.Empty<CardEnhancementData>());
        _cards.ConfigureRandom(new DeterministicRandom(911));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = CreateComponent<CombatManager>("Feed Test Combat");
        _combat.ConfigurePlayer(30);
        _combat.CriticalChancePercent = 0f;
        EffectTriggerFeed.Triggered += Record;
    }

    [TearDown]
    public void TearDown()
    {
        EffectTriggerFeed.Triggered -= Record;
        _reported.Clear();
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    void Record(EffectTrigger trigger) => _reported.Add(trigger);

    [Test]
    public void ProfitableInHand_ReportsItsCreditsOncePerAttack()
    {
        var profitable = HandViews().First().data;
        Assert.That(_cards.ApplyEnhancement(profitable.Id, Mod("Profitable")), Is.True);
        var attack = FindView(card => card.Rank != CardData.Rank.Ace, profitable);
        var target = StartTarget();

        Assert.That(_combat.TryPlayCards(new[] { attack }, target), Is.True);

        var credits = _reported.Where(t => t.Cue == EffectCue.Credits).ToArray();
        Assert.That(credits, Has.Length.EqualTo(1));
        Assert.That(credits[0].Card, Is.SameAs(profitable));
        Assert.That(credits[0].Amount, Is.EqualTo(2));
    }

    [Test]
    public void OverclockedAndBoosted_ReportTheirHealthCostAndBonusOnThePlayedCard()
    {
        var overclocked = FindView(card => card.Rank != CardData.Rank.Ace).data;
        Assert.That(_cards.ApplyEnhancement(overclocked.Id, Mod("Overclocked")), Is.True);
        var target = StartTarget();

        Assert.That(_combat.TryPlayCards(new[] { HandViews().First(view => view.data == overclocked) }, target), Is.True);

        var cost = _reported.Single(t => t.Cue == EffectCue.HealthCost);
        Assert.That(cost.Card, Is.SameAs(overclocked));
        Assert.That(cost.Amount, Is.EqualTo(5));

        _reported.Clear();
        var boosted = FindView(card => card.Rank != CardData.Rank.Ace).data;
        Assert.That(_cards.ApplyEnhancement(boosted.Id, Mod("Boosted")), Is.True);
        if (_combat.currentState == GameState.EnemyAttacking) _combat.TakeRemainingDamage();
        Assert.That(_combat.TryPlayCards(new[] { HandViews().First(view => view.data == boosted) }, target), Is.True);

        var bonus = _reported.Single(t => t.Cue == EffectCue.AttackBonus);
        Assert.That(bonus.Card, Is.SameAs(boosted));
        Assert.That(bonus.Amount, Is.EqualTo(3));
    }

    [Test]
    public void OwnedGearThatChangesTheAttack_ReportsItself()
    {
        var baton = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/PowerBaton.asset");
        Assert.That(_cards.BuyGear(baton, 0), Is.True);
        var ten = FindView(card => card.Rank == CardData.Rank.Ten);
        var target = StartTarget();

        Assert.That(_combat.TryPlayCards(new[] { ten }, target), Is.True);

        var report = _reported.Single(t => t.Gear != null);
        Assert.That(report.Gear.Definition, Is.SameAs(baton));
        Assert.That(report.Cue, Is.EqualTo(EffectCue.AttackBonus));
        Assert.That(report.Amount, Is.EqualTo(baton.effects[0].amount));
    }

    [Test]
    public void Previews_ReportNothing()
    {
        var baton = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/PowerBaton.asset");
        Assert.That(_cards.BuyGear(baton, 0), Is.True);
        var card = FindView(card => card.Rank == CardData.Rank.Ten).data;
        Assert.That(_cards.ApplyEnhancement(card.Id, Mod("Overclocked")), Is.True);
        var target = StartTarget();

        _combat.CalculateCardAttackDamage(card);
        _combat.CalculateCardDefense(card);
        _combat.PreviewActionDamage(new[] { card }, target);
        _combat.PreviewActionTotalDamage(new[] { card }, target);

        Assert.That(_reported, Is.Empty);
    }

    static CardEnhancementData Mod(string name)
    {
        var mod = AssetDatabase.LoadAssetAtPath<CardEnhancementData>($"Assets/Data/Mods/{name}.asset");
        Assert.That(mod, Is.Not.Null, name);
        return mod;
    }

    EnemyRuntime StartTarget()
    {
        var definition = EnemyTypeData.Create("Feed Test Enemy", 1000, 0, 0);
        _created.Add(definition);
        var target = new EnemyRuntime(definition);
        _combat.StartEnemy(target);
        return target;
    }

    CardView FindView(System.Func<CardInstance, bool> predicate, params CardInstance[] preserve)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var found = HandViews().FirstOrDefault(view => predicate(view.data) && !preserve.Contains(view.data));
            if (found != null) return found;
            var discard = HandViews().FirstOrDefault(view => !preserve.Contains(view.data));
            if (discard == null) break;
            Assert.That(_cards.TryDiscard(discard), Is.True);
            _cards.DrawToHand(1);
        }
        Assert.Fail("Required card could not be found in the 40-card collection.");
        return null;
    }

    CardView[] HandViews() => _field.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(view => view.data != null && _cards.hand.Contains(view.data)).ToArray();

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var gameObject = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }
}
