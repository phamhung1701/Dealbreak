using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class LowerEffortEnhancementTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    CardEnhancementData _devouring, _lethal, _explosive, _vampiric, _shifting;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _devouring = Load("Predatory");
        _lethal = Load("Deadeye");
        _explosive = Load("Volatile");
        _vampiric = Load("Overclocked");
        _shifting = Load("Wildcard");
        Assert.That(new[] { _devouring, _lethal, _explosive, _vampiric, _shifting }, Has.All.Not.Null);

        var canvas = Make<Canvas>("Enhancement Test Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var hand = new GameObject("Enhancement Test Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _created.Add(hand);
        hand.transform.SetParent(canvas.transform, false);
        var field = hand.AddComponent<Field>();
        field.cardsHolder = (RectTransform)hand.transform;
        var drag = Make<Canvas>("Enhancement Test Drag Canvas");
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = new GameObject("Enhancement Test Card Prefab", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        _created.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = Make<CardManager>("Enhancement Test Cards");
        _cards.Configure(field, drag, prefab, Array.Empty<GearData>(),
            new[] { _devouring, _lethal, _explosive, _vampiric, _shifting });
        _cards.ConfigureRandom(new DeterministicRandom(4051));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = Make<CombatManager>("Enhancement Test Combat");
        _combat.ConfigurePlayer(30);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void ContentAssets_HaveCanonicalEffectsAndExpectedBalance()
    {
        Assert.That(_devouring.effects.Single().kind, Is.EqualTo(GameplayEffectKind.PermanentKillAttack));
        Assert.That(_lethal.effects.Single().kind, Is.EqualTo(GameplayEffectKind.CriticalChanceOverride));
        Assert.That(_lethal.effects.Single().amount, Is.EqualTo(25));
        Assert.That(_explosive.effects.Single().kind, Is.EqualTo(GameplayEffectKind.ExplosiveAreaDamage));
        Assert.That(_vampiric.effects.Single().amount, Is.EqualTo(5));
        Assert.That(_shifting.effects.Single().kind, Is.EqualTo(GameplayEffectKind.WildSuit));
    }

    [Test]
    public void Lethal_RollsTwentyFivePercentAndDoublesAttackDamage()
    {
        var card = FindView(c => c.Rank != CardData.Rank.Ace);
        Assert.That(_cards.ApplyEnhancement(card.data.Id, _lethal), Is.True);
        var target = Target("Lethal Target", 1000, 0);
        _combat.StartEnemy(target);
        var random = new FixedFloatRandom(0.24f);
        _combat.ConfigureCriticalRandom(random);
        int damage = _combat.CalculateCardAttackDamage(card.data);

        Assert.That(_combat.TryPlayCards(new[] { card }, target), Is.True);

        Assert.That(target.currentHp, Is.EqualTo(1000 - damage * 2));
        Assert.That(random.Calls, Is.EqualTo(1));
    }

    [Test]
    public void Explosive_DealsModifiedAttackDamageOnceToEveryOtherEnemy()
    {
        var card = FindView(c => c.Rank != CardData.Rank.Ace);
        Assert.That(_cards.ApplyEnhancement(card.data.Id, _explosive), Is.True);
        var primary = Target("Primary", 1000, 0);
        var secondary = Target("Secondary", 1000, 0);
        _combat.StartEncounter(new[] { primary, secondary });
        _combat.CriticalChancePercent = 0;
        int damage = _combat.CalculateCardAttackDamage(card.data);

        Assert.That(_combat.TryPlayCards(new[] { card }, primary), Is.True);

        Assert.That(primary.currentHp, Is.EqualTo(1000 - damage));
        Assert.That(secondary.currentHp, Is.EqualTo(1000 - damage));
    }

    [Test]
    public void Devouring_GainsPermanentAttackForEachKillByTheEnhancedAction()
    {
        var card = FindView(c => c.Rank != CardData.Rank.Ace);
        Assert.That(_cards.ApplyEnhancement(card.data.Id, _devouring), Is.True);
        var target = Target("Devouring Target", 1, 0);
        _combat.StartEnemy(target);
        int baseDamage = _combat.CalculateCardAttackDamage(card.data);
        Assert.That(baseDamage, Is.GreaterThan(0));

        Assert.That(_combat.TryPlayCards(new[] { card }, target), Is.True);

        Assert.That(card.data.PermanentAttackBonus, Is.EqualTo(1));
        Assert.That(_combat.CalculateCardAttackDamage(card.data), Is.EqualTo(baseDamage + 1));
    }

    [Test]
    public void Vampiric_PaysFiveHpForFiveDamageAndRequiresSurvival()
    {
        var card = FindView(c => c.Rank != CardData.Rank.Ace);
        Assert.That(_cards.ApplyEnhancement(card.data.Id, _vampiric), Is.True);
        _combat.TakeRunDamage(24);
        var target = Target("Vampiric Target", 1000, 0);
        _combat.StartEnemy(target);
        int expectedDamage = _combat.CalculateCardAttackDamage(card.data) + 5;
        Assert.That(_combat.CanPlayCards(new[] { card }, target), Is.True);

        Assert.That(_combat.TryPlayCards(new[] { card }, target), Is.True);

        Assert.That(_combat.player.currentHealth, Is.EqualTo(1));
        Assert.That(target.currentHp, Is.EqualTo(1000 - expectedDamage));
    }

    [Test]
    public void Vampiric_CannotBePlayedWhenCostWouldDefeatPlayer()
    {
        var card = FindView(c => c.Rank != CardData.Rank.Ace);
        Assert.That(_cards.ApplyEnhancement(card.data.Id, _vampiric), Is.True);
        _combat.TakeRunDamage(25);
        var target = Target("Vampiric Boundary Target", 1000, 0);
        _combat.StartEnemy(target);

        Assert.That(_combat.CanPlayCards(new[] { card }, target), Is.False);
        Assert.That(_combat.TryPlayCards(new[] { card }, target), Is.False);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(5));
        Assert.That(_cards.hand, Does.Contain(card.data));
    }

    [Test]
    public void Shifting_MatchesEverySuitWithoutChangingItsPrintedSuit()
    {
        var card = FindView(_ => true);
        var originalSuit = card.data.Suit;
        Assert.That(_cards.ApplyEnhancement(card.data.Id, _shifting), Is.True);

        foreach (CardData.Suit suit in Enum.GetValues(typeof(CardData.Suit)))
            Assert.That(card.data.MatchesSuit(suit), Is.True);
        Assert.That(card.data.Suit, Is.EqualTo(originalSuit));
    }

    CardEnhancementData Load(string file) =>
        AssetDatabase.LoadAssetAtPath<CardEnhancementData>($"Assets/Data/Mods/{file}.asset");

    EnemyRuntime Target(string name, int hp, int attack)
    {
        var type = EnemyTypeData.Create(name, hp, attack, 0);
        _created.Add(type);
        return new EnemyRuntime(type);
    }

    CardView[] Views() => _cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(v => v.data != null && _cards.hand.Contains(v.data)).ToArray();

    CardView FindView(Func<CardInstance, bool> predicate) => Views().First(v => predicate(v.data));

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go.AddComponent<T>();
    }

    sealed class FixedFloatRandom : IRandomSource
    {
        readonly float _value;
        public int Calls { get; private set; }
        public FixedFloatRandom(float value) => _value = value;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        public float NextFloat() { Calls++; return _value; }
    }
}
