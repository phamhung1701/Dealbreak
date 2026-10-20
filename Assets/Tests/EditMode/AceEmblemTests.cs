using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class AceEmblemTests
{
    readonly List<UnityEngine.Object> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    GearData _aceEmblem;
    GearData _hands;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _aceEmblem = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/AceEmblem.asset");
        _hands = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/HandsEmblem.asset");
        Assert.That(_aceEmblem, Is.Not.Null);
        Assert.That(_hands, Is.Not.Null);

        var canvas = Make<Canvas>("Ace Emblem Test Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var handObject = new GameObject("Ace Emblem Test Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _objects.Add(handObject);
        handObject.transform.SetParent(canvas.transform, false);
        var field = handObject.AddComponent<Field>();
        field.cardsHolder = (RectTransform)handObject.transform;

        var dragCanvas = Make<Canvas>("Ace Emblem Test Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = new GameObject("Ace Emblem Test Card", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        _objects.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();

        _cards = Make<CardManager>("Ace Emblem Test Cards");
        _cards.Configure(field, dragCanvas, prefab, new[] { _aceEmblem, _hands },
            System.Array.Empty<CardEnhancementData>());
        _cards.ConfigureRandom(new DeterministicRandom(41));
        _combat = Make<CombatManager>("Ace Emblem Test Combat");
        _combat.ConfigurePlayer(30);
        _cards.BuildDeck();
        _cards.DealHand();
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
    public void Asset_UsesCanonicalTierOneTypedAceRankOverride()
    {
        Assert.That(_aceEmblem.id, Is.EqualTo("rel_014"));
        Assert.That(_aceEmblem.canonicalId, Is.EqualTo("rel_014"));
        Assert.That(_aceEmblem.displayName, Is.EqualTo("Ace Protocol"));
        Assert.That(_aceEmblem.rarity, Is.EqualTo("Common"));
        Assert.That(_aceEmblem.tier, Is.EqualTo(1));
        Assert.That(_aceEmblem.capacityCategory, Is.EqualTo(GearCapacityCategory.Persistent));
        Assert.That(_aceEmblem.price, Is.EqualTo(20), "Temporary prototype price; workbook price is blank.");
        Assert.That(_aceEmblem.effects, Has.Length.EqualTo(1));
        var effect = _aceEmblem.effects[0];
        Assert.That(effect.kind, Is.EqualTo(GameplayEffectKind.CriticalChanceOverride));
        Assert.That(effect.trigger, Is.EqualTo(GameplayEffectTrigger.CriticalChanceCalculated));
        Assert.That(effect.amount, Is.EqualTo(50));
        Assert.That(effect.conditions, Has.Length.EqualTo(1));
        Assert.That(effect.conditions[0].kind, Is.EqualTo(GameplayConditionKind.CardRank));
        Assert.That(effect.conditions[0].rank, Is.EqualTo(CardData.Rank.Ace));
    }

    [TestCase(0.49f, true, 2)]
    [TestCase(0.50f, false, 1)]
    public void SingleAce_UsesFiftyPercentCriticalBoundary(float roll, bool expectedCritical, int expectedDamage)
    {
        Buy(_aceEmblem);
        var random = new CountingRandom(roll);
        _combat.ConfigureCriticalRandom(random);
        var target = StartTarget();
        var ace = FindView(card => card.Rank == CardData.Rank.Ace);

        Assert.That(_combat.TryPlayCards(new[] { ace }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(500 - expectedDamage));
        Assert.That(random.Calls, Is.EqualTo(1));
    }

    [Test]
    public void NonAceSingleCard_DoesNotUseAceOverrideOrConsumeCriticalRandom()
    {
        Buy(_aceEmblem);
        var random = new CountingRandom(0f);
        _combat.ConfigureCriticalRandom(random);
        var target = StartTarget();
        var two = FindView(card => card.Rank == CardData.Rank.Two);

        Assert.That(_combat.TryPlayCards(new[] { two }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(498));
        Assert.That(random.Calls, Is.Zero);
    }

    [Test]
    public void AcePair_UsesEmblemOverrideAndRollsOnceForTheWholeAction()
    {
        Buy(_aceEmblem);
        var random = new CountingRandom(0.49f); // 49% succeeds at 50% but not the default 25%.
        _combat.ConfigureCriticalRandom(random);
        var target = StartTarget();
        var ace = FindView(card => card.Rank == CardData.Rank.Ace);
        var four = FindView(card => card.Rank == CardData.Rank.Four, ace.data);

        Assert.That(_combat.TryPlayCards(new[] { ace, four }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(490));
        Assert.That(random.Calls, Is.EqualTo(1), "The pair receives one action-level critical roll, not one per card.");
    }

    [Test]
    public void ThreeCardHandsAttackContainingAces_CanCritOnceForTheAction()
    {
        Buy(_hands);
        Buy(_aceEmblem);
        _combat.CriticalChancePercent = 0f;
        var random = new CountingRandom(0.49f);
        _combat.ConfigureCriticalRandom(random);
        var aces = EnsureRankInHand(CardData.Rank.Ace, 3);
        var aceIds = aces.Select(view => view.data.Id).ToArray();
        var target = StartTarget();

        Assert.That(_combat.TryPlayCards(aces, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(494), "Three Ace cards deal 3, then the Ace Emblem critical doubles the action to 6.");
        Assert.That(random.Calls, Is.EqualTo(1), "Multiple committed Aces still produce one roll for the action.");
        foreach (int id in aceIds)
            Assert.That(_cards.deck.Count(card => card.Id == id), Is.EqualTo(1), "Each committed Ace returns to the draw pile exactly once.");
        Assert.That(aceIds.Distinct().Count(), Is.EqualTo(3));
    }

    [Test]
    public void WithoutAceEmblem_SingleAceKeepsExistingNonCriticalBehavior()
    {
        var random = new CountingRandom(0f);
        _combat.ConfigureCriticalRandom(random);
        var target = StartTarget();
        var ace = FindView(card => card.Rank == CardData.Rank.Ace);

        Assert.That(_combat.TryPlayCards(new[] { ace }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(499));
        Assert.That(random.Calls, Is.Zero);
    }

    void Buy(GearData gear) => Assert.That(_cards.BuyGear(gear, 0), Is.True);

    EnemyRuntime StartTarget(int hp = 500, int attack = 1)
    {
        var definition = EnemyTypeData.Create("Ace Emblem Test Enemy", hp, attack, 0);
        _objects.Add(definition);
        var target = new EnemyRuntime(definition);
        _combat.StartEnemy(target);
        return target;
    }

    CardView FindView(System.Func<CardInstance, bool> predicate, params CardInstance[] preserve)
    {
        for (int pass = 0; pass < 120; pass++)
        {
            var found = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
                .FirstOrDefault(view => view.data != null && _cards.hand.Contains(view.data) &&
                    predicate(view.data) && !preserve.Contains(view.data));
            if (found != null) return found;
            var discard = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
                .FirstOrDefault(view => view.data != null && _cards.hand.Contains(view.data) &&
                    !preserve.Contains(view.data));
            if (discard == null) break;
            _cards.TryDiscard(discard);
            _cards.DrawToHand(1);
        }
        Assert.Fail("Required card could not be found in the 40-card collection.");
        return null;
    }

    CardView[] EnsureRankInHand(CardData.Rank rank, int count)
    {
        var found = new List<CardView>();
        for (int i = 0; i < count; i++)
            found.Add(FindView(card => card.Rank == rank, found.Select(view => view.data).ToArray()));
        return found.ToArray();
    }

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _objects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    sealed class CountingRandom : IRandomSource
    {
        readonly float _roll;
        public int Calls { get; private set; }
        public CountingRandom(float roll) => _roll = roll;
        public int NextInt(int minInclusive, int maxExclusive) => throw new System.NotImplementedException();
        public float NextFloat() { Calls++; return _roll; }
    }
}
