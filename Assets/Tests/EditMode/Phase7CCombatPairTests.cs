using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7CCombatPairTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        var canvas = CreateComponent<Canvas>("Phase7C Pair Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var dragCanvas = CreateComponent<Canvas>("Phase7C Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = CreateGameObject("Card Prefab", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = CreateComponent<CardManager>("Phase7C Card Manager");
        _cards.Configure(_field, dragCanvas, prefab, Array.Empty<GearData>(), Array.Empty<CardEnhancementData>());
        _cards.ConfigureRandom(new DeterministicRandom(1));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = CreateComponent<CombatManager>("Phase7C Combat Manager");
        _combat.ConfigurePlayer(30);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
    }

    [Test]
    public void PairedAceAction_SumsDamageAndConsumesBothCardsAsOneAction()
    {
        var target = new EnemyRuntime(EnemyTypeData.Create("Pair Target", 100, 0, 0));
        _combat.StartEnemy(target);
        _cards.RefillHand();
        var views = HandViews();
        var ace = views.First(view => view.data.Rank == CardData.Rank.Ace);
        var other = views.First(view => view.data.Rank != CardData.Rank.Ace);
        var pair = new[] { ace, other };
        int expected = pair.Sum(view => _combat.CalculateCardAttackDamage(view.data));
        int before = _cards.HandCount;
        _combat.CriticalChancePercent = 0;

        Assert.That(_combat.CanPlayCards(pair, target), Is.True);
        Assert.That(_combat.TryPlayCards(pair, target), Is.True);

        Assert.That(target.currentHp, Is.EqualTo(100 - expected));
        Assert.That(_cards.HandCount, Is.EqualTo(before - 2));
        Assert.That(pair.All(view => _cards.deck.Contains(view.data)), Is.True, "Both cards return to the draw pile.");
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [TestCase(0f, false)]
    [TestCase(100f, true)]
    public void PairHitFeedback_ReportsOneResolvedHitWithAuthoritativeCriticalAndHpLoss(float chance, bool critical)
    {
        var definition = EnemyTypeData.Create("Feedback Target", 3, 0, 0);
        _created.Add(definition);
        var target = new EnemyRuntime(definition);
        _combat.StartEnemy(target);
        _cards.RefillHand();
        var views = HandViews();
        var pair = new[] { views.First(view => view.data.Rank == CardData.Rank.Ace),
            views.First(view => view.data.Rank != CardData.Rank.Ace) };
        _combat.CriticalChancePercent = chance;
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;

        Assert.That(_combat.TryPlayCards(pair, target), Is.True);

        Assert.That(results.Count, Is.EqualTo(1), "Two animated cards must not report the same hit twice.");
        Assert.That(results[0].Request.IsCritical, Is.EqualTo(critical));
        Assert.That(results[0].ActualHpLost, Is.EqualTo(3));
        Assert.That(AttackCardPresentationUI.FormatHitNumber(results[0]), Is.EqualTo(critical ? "Crit 3" : "3"));
    }

    [Test]
    public void ShieldHitFeedback_ReportsBlockedRatherThanRequestedDamage()
    {
        var definition = EnemyTypeData.Create("Shield Feedback", 100, 0, 0);
        _created.Add(definition);
        var target = new EnemyRuntime(definition);
        _combat.StartEnemy(target);
        target.GainShield(1);
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;
        Assert.That(_combat.TryPlayCards(new[] { HandViews()[0] }, target), Is.True);
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].ActualHpLost, Is.Zero);
        Assert.That(AttackCardPresentationUI.FormatHitNumber(results[0]), Is.EqualTo("Blocked"));
    }

    [Test]
    public void MultiHitFeedback_ReportsEachResolvedHitOnce()
    {
        var definition = EnemyTypeData.Create("Multi-hit Feedback", 1000, 0, 0);
        _created.Add(definition);
        var target = new EnemyRuntime(definition);
        _combat.StartEnemy(target);
        var card = HandViews().First(view => view.data.Rank != CardData.Rank.Ace);
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;
        Assert.That(_combat.TryPlayCards(new[] { card }, target, 2), Is.True);
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(results.Select(result => result.Request.HitIndex), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(results.All(result => !result.Request.IsCritical), Is.True);
        Assert.That(results.Sum(result => result.ActualHpLost), Is.EqualTo(1000 - target.currentHp));
    }

    [Test]
    public void CriticalExplosiveFeedback_InheritsCriticalForSecondaryResolvedHit()
    {
        var definition = EnemyTypeData.Create("Explosive Feedback", 1000, 0, 0);
        _created.Add(definition);
        var target = new EnemyRuntime(definition);
        var secondary = new EnemyRuntime(definition);
        _combat.StartEncounter(new[] { target, secondary });
        _cards.RefillHand();
        var views = HandViews();
        var pair = new[] { views.First(view => view.data.Rank == CardData.Rank.Ace),
            views.First(view => view.data.Rank != CardData.Rank.Ace) };
        var explosive = UnityEditor.AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Enhancements/Explosive.asset");
        Assert.That(explosive, Is.Not.Null);
        Assert.That(_cards.ApplyEnhancement(pair[1].data.Id, explosive), Is.True);
        _combat.CriticalChancePercent = 100f;
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;
        Assert.That(_combat.TryPlayCards(pair, target), Is.True);
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(results.All(result => result.Request.IsCritical), Is.True);
        Assert.That(results[1].Request.Target, Is.SameAs(secondary));
        Assert.That(AttackCardPresentationUI.FormatHitNumber(results[1]), Does.StartWith("Crit "));
    }

    [Test]
    public void NonAcePair_IsRejectedWithoutConsumingCards()
    {
        var target = new EnemyRuntime(EnemyTypeData.Create("Invalid Pair", 100, 0, 0));
        _combat.StartEnemy(target);
        _cards.RefillHand();
        var pair = HandViews().Where(view => view.data.Rank != CardData.Rank.Ace).Take(2).ToArray();
        int before = _cards.HandCount;

        Assert.That(pair.Length, Is.EqualTo(2));
        Assert.That(_combat.CanPlayCards(pair, target), Is.False);
        Assert.That(_combat.TryPlayCards(pair, target), Is.False);
        Assert.That(_cards.HandCount, Is.EqualTo(before));
    }

    [Test]
    public void SingleCard_DoesNotConsumeCriticalRandomStream()
    {
        var random = new CountingRandom();
        _combat.ConfigureCriticalRandom(random);
        _combat.CriticalChancePercent = 25;
        var target = new EnemyRuntime(EnemyTypeData.Create("Single", 100, 0, 0));
        _combat.StartEnemy(target);

        Assert.That(_combat.TryPlayCards(new[] { HandViews()[0] }, target), Is.True);
        Assert.That(random.Calls, Is.Zero);
    }

    [Test]
    public void PairCommittedEnhancementEffects_RunOnceInSelectedCardOrder()
    {
        var target = new EnemyRuntime(EnemyTypeData.Create("Ordered Effects", 100, 0, 0));
        _combat.StartEnemy(target);
        _cards.RefillHand();
        var views = HandViews();
        var ace = views.First(view => view.data.Rank == CardData.Rank.Ace);
        var other = views.First(view => view.data.Rank != CardData.Rank.Ace);
        var first = ScriptableObject.CreateInstance<CardEnhancementData>();
        first.id = "phase7c_first";
        first.displayName = "First selected";
        first.healOnPlay = 1;
        var second = ScriptableObject.CreateInstance<CardEnhancementData>();
        second.id = "phase7c_second";
        second.displayName = "Second selected";
        second.healOnPlay = 2;
        _created.Add(first);
        _created.Add(second);
        Assert.That(_cards.ApplyEnhancement(ace.data.Id, first), Is.True);
        Assert.That(_cards.ApplyEnhancement(other.data.Id, second), Is.True);
        _combat.TakeRunDamage(5);
        var healLogs = new List<string>();
        _combat.OnCombatLog += message =>
        {
            if (message.Contains("selected: Healed")) healLogs.Add(message);
        };

        Assert.That(_combat.TryPlayCards(new[] { ace, other }, target), Is.True);

        Assert.That(_combat.player.currentHealth, Is.EqualTo(28));
        Assert.That(healLogs, Is.EqualTo(new[] { "First selected: Healed 1 HP.", "Second selected: Healed 2 HP." }));
    }

    [Test]
    public void GuaranteedCritical_DoublesSummedPairDamageWithoutRandomDraw()
    {
        var random = new CountingRandom();
        _combat.ConfigureCriticalRandom(random);
        _combat.CriticalChancePercent = 100;
        var target = new EnemyRuntime(EnemyTypeData.Create("Critical", 100, 0, 0));
        _combat.StartEnemy(target);
        _cards.RefillHand();
        var views = HandViews();
        var ace = views.First(view => view.data.Rank == CardData.Rank.Ace);
        var other = views.First(view => view.data.Rank != CardData.Rank.Ace);
        int expected = 2 * (_combat.CalculateCardAttackDamage(ace.data) + _combat.CalculateCardAttackDamage(other.data));

        Assert.That(_combat.TryPlayCards(new[] { ace, other }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(100 - expected));
        Assert.That(random.Calls, Is.Zero);
    }

    [Test]
    public void PairGearEffects_ApplyToEachCardOnceAndUsePerCardDamage()
    {
        var target = new EnemyRuntime(EnemyTypeData.Create("Artifact Pair", 100, 30, 0));
        _combat.StartEnemy(target);
        _cards.RefillHand();
        var views = HandViews();
        var ace = views.First(view => view.data.Rank == CardData.Rank.Ace);
        var other = views.First(view => view.data.Rank != CardData.Rank.Ace);
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = "phase7c_pair_artifact";
        gear.displayName = "Paired Artifact";
        gear.damageMultiplier = 2;
        gear.flatDamageBonus = 3;
        gear.reduceEnemyAttackByCardValue = true;
        _created.Add(gear);
        _cards.ownedGear.Add(gear);
        _combat.CriticalChancePercent = 0;
        int expectedDamage = _combat.CalculateCardAttackDamage(ace.data) + _combat.CalculateCardAttackDamage(other.data);
        int expectedReduction = ace.data.BaseAttackValue + other.data.BaseAttackValue;
        var reductions = new List<string>();
        _combat.OnCombatLog += message =>
        {
            if (message.StartsWith("Paired Artifact:")) reductions.Add(message);
        };

        Assert.That(_combat.TryPlayCards(new[] { ace, other }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(100 - expectedDamage));
        Assert.That(target.currentAttack, Is.EqualTo(30 - expectedReduction));
        Assert.That(reductions.Count, Is.EqualTo(2));
    }

    [Test]
    public void InvalidPair_DoesNotConsumeCriticalRandom()
    {
        var random = new CountingRandom();
        _combat.ConfigureCriticalRandom(random);
        var target = new EnemyRuntime(EnemyTypeData.Create("Invalid Crit", 100, 0, 0));
        _combat.StartEnemy(target);
        var nonAces = HandViews().Where(view => view.data.Rank != CardData.Rank.Ace).Take(2).ToArray();

        Assert.That(_combat.TryPlayCards(nonAces, target), Is.False);
        Assert.That(random.Calls, Is.Zero);
    }

    [Test]
    public void PrototypeChance_UsesOneRollPerPairAtConfiguredThreshold()
    {
        var random = new CountingRandom();
        _combat.ConfigureCriticalRandom(random);
        _combat.CriticalChancePercent = 25;
        var target = new EnemyRuntime(EnemyTypeData.Create("Chance", 100, 0, 0));
        _combat.StartEnemy(target);
        _cards.RefillHand();
        var views = HandViews();
        var ace = views.First(view => view.data.Rank == CardData.Rank.Ace);
        var other = views.First(view => view.data.Rank != CardData.Rank.Ace);
        int damage = _combat.CalculateCardAttackDamage(ace.data) + _combat.CalculateCardAttackDamage(other.data);

        Assert.That(_combat.TryPlayCards(new[] { ace, other }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(100 - 2 * damage));
        Assert.That(random.Calls, Is.EqualTo(1));
    }

    [Test]
    public void CriticalStream_ReplaysForSameRunSeed()
    {
        var first = new RunRandomContext("phase7c-replay").CreateStream("combat-critical");
        var second = new RunRandomContext("phase7c-replay").CreateStream("combat-critical");
        Assert.That(first.NextFloat(), Is.EqualTo(second.NextFloat()));
        Assert.That(first.NextFloat(), Is.EqualTo(second.NextFloat()));
    }

    List<CardView> HandViews()
    {
        var result = new List<CardView>();
        for (int i = 0; i < _field.cardsHolder.childCount; i++)
            if (_field.cardsHolder.GetChild(i).TryGetComponent(out CardView view)) result.Add(view);
        return result;
    }

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();
    GameObject CreateGameObject(string name, params Type[] components)
    {
        var go = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(go);
        return go;
    }

    sealed class CountingRandom : IRandomSource
    {
        public int Calls { get; private set; }
        public int NextInt(int minInclusive, int maxExclusive) => throw new NotImplementedException();
        public float NextFloat() { Calls++; return 0f; }
    }
}
