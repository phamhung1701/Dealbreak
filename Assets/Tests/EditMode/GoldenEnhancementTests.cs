using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class GoldenEnhancementTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;
    CardEnhancementData _golden;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _golden = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Enhancements/Golden.asset");
        Assert.That(_golden, Is.Not.Null);

        var canvas = CreateComponent<Canvas>("Golden Test Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Golden Test Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var dragCanvas = CreateComponent<Canvas>("Golden Test Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = CreateGameObject("Golden Test Card Prefab", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = CreateComponent<CardManager>("Golden Test Cards");
        _cards.Configure(_field, dragCanvas, prefab, System.Array.Empty<GearData>(),
            new[] { _golden, AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Enhancements/Quickdraw.asset") });
        _cards.ConfigureRandom(new DeterministicRandom(701));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = CreateComponent<CombatManager>("Golden Test Combat");
        _combat.ConfigurePlayer(30);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void DoubleStrikeEffect_DerivesExactlyTwoHitsFromSelectedEnhancement()
    {
        var doubleStrike = ScriptableObject.CreateInstance<CardEnhancementData>();
        _created.Add(doubleStrike);
        doubleStrike.effects = new[] { new GameplayEffectDefinition
        {
            kind = GameplayEffectKind.DoubleStrike,
            trigger = GameplayEffectTrigger.AttackCalculated,
            amount = 1
        } };
        var card = HandViews().First().data;
        Assert.That(_cards.ApplyEnhancement(card.Id, doubleStrike), Is.True);
        var target = StartTarget();
        _combat.CriticalChancePercent = 0f;
        int damage = _combat.CalculateCardAttackDamage(card);

        Assert.That(_combat.TryPlayCards(new[] { HandViews().First(view => view.data == card) }, target), Is.True);

        Assert.That(target.currentHp, Is.EqualTo(1000 - damage * 2));
        Assert.That(_cards.deck.Count(instance => instance.Id == card.Id), Is.EqualTo(1));
    }

    [Test]
    public void Asset_UsesCanonicalRareTierOneBonusCreditsAttackCommittedEffect()
    {
        Assert.That(_golden.id, Is.EqualTo("enh_007"));
        Assert.That(_golden.canonicalId, Is.EqualTo("enh_007"));
        Assert.That(_golden.displayName, Is.EqualTo("Profitable"));
        Assert.That(_golden.rarity, Is.EqualTo("Rare"));
        Assert.That(_golden.tier, Is.EqualTo(1));
        Assert.That(_golden.price, Is.EqualTo(20), "Temporary prototype price; workbook price is blank.");
        Assert.That(_golden.effects, Has.Length.EqualTo(1));
        Assert.That(_golden.effects[0].kind, Is.EqualTo(GameplayEffectKind.BonusCredits));
        Assert.That(_golden.effects[0].trigger, Is.EqualTo(GameplayEffectTrigger.AttackCommitted));
        Assert.That(_golden.effects[0].amount, Is.EqualTo(2));
    }

    [Test]
    public void TwoGoldenCardsInHand_GainGoldOnceEachPerAttackActionNotPerHit()
    {
        var sources = HandViews().Take(2).ToArray();
        Assert.That(sources, Has.Length.EqualTo(2));
        foreach (var source in sources)
            Assert.That(_cards.ApplyEnhancement(source.data.Id, _golden), Is.True);
        string definitionBefore = JsonUtility.ToJson(_golden);
        var attack = FindView(card => card.Rank != CardData.Rank.Ace,
            sources.Select(view => view.data).ToArray());
        var target = StartTarget();
        var random = new CountingRandom();
        _combat.ConfigureCriticalRandom(random);
        int initialCredits = _cards.credits;

        Assert.That(_combat.TryPlayCards(new[] { attack }, target, hitCount: 3), Is.True);

        Assert.That(_cards.credits, Is.EqualTo(initialCredits + 4), "Each in-hand Golden pays once for the action, not once per hit.");
        Assert.That(random.Calls, Is.Zero, "The gold reaction consumes no RNG.");
        Assert.That(_cards.deck.Count(card => card.Id == attack.data.Id), Is.EqualTo(1));
        Assert.That(JsonUtility.ToJson(_golden), Is.EqualTo(definitionBefore), "The shared definition stays immutable during the run.");
    }

    [Test]
    public void PlayedGolden_IsRemovedBeforeInHandSnapshotAndDoesNotPayItself()
    {
        var goldenCard = HandViews().First();
        Assert.That(_cards.ApplyEnhancement(goldenCard.data.Id, _golden), Is.True);
        var target = StartTarget();
        int initialCredits = _cards.credits;

        Assert.That(_combat.TryPlayCards(new[] { goldenCard }, target), Is.True);

        Assert.That(_cards.credits, Is.EqualTo(initialCredits));
    }

    [Test]
    public void GoldenInDiscard_DoesNotPayWhenAnotherCardAttacks()
    {
        var goldenCard = HandViews().First();
        Assert.That(_cards.TryDiscard(goldenCard), Is.True);
        Assert.That(_cards.ApplyEnhancement(goldenCard.data.Id, _golden), Is.True);
        var target = StartTarget();
        var attack = FindView(card => card.Rank != CardData.Rank.Ace);
        int initialCredits = _cards.credits;

        Assert.That(_combat.TryPlayCards(new[] { attack }, target), Is.True);

        Assert.That(_cards.credits, Is.EqualTo(initialCredits));
    }

    [Test]
    public void AcePair_IsOneAttackForInHandGolden()
    {
        var goldenCard = FindView(card => card.Rank != CardData.Rank.Ace);
        Assert.That(_cards.ApplyEnhancement(goldenCard.data.Id, _golden), Is.True);
        var ace = FindView(card => card.Rank == CardData.Rank.Ace, goldenCard.data);
        var other = FindView(card => card.Rank != CardData.Rank.Ace, goldenCard.data, ace.data);
        var target = StartTarget();
        _combat.CriticalChancePercent = 0f;
        int initialCredits = _cards.credits;

        Assert.That(_combat.TryPlayCards(new[] { ace, other }, target), Is.True);

        Assert.That(_cards.credits, Is.EqualTo(initialCredits + 2));
    }

    EnemyRuntime StartTarget()
    {
        var definition = EnemyTypeData.Create("Golden Test Enemy", 1000, 0, 0);
        _created.Add(definition);
        var target = new EnemyRuntime(definition);
        _combat.StartEnemy(target);
        return target;
    }

    CardView FindView(System.Func<CardInstance, bool> predicate, params CardInstance[] preserve)
    {
        for (int pass = 0; pass < 160; pass++)
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

    sealed class CountingRandom : IRandomSource
    {
        public int Calls { get; private set; }
        public int NextInt(int minInclusive, int maxExclusive) => throw new System.NotImplementedException();
        public float NextFloat() { Calls++; return 0f; }
    }
}
