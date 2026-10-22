using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class FireEmblemTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    GearData _fireEmblem;
    Field _field;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _fireEmblem = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/Lighter.asset");
        Assert.That(_fireEmblem, Is.Not.Null);

        var canvas = Make<Canvas>("Fire Emblem Test Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var handObject = new GameObject("Fire Emblem Test Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _created.Add(handObject);
        handObject.transform.SetParent(canvas.transform, false);
        _field = handObject.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)handObject.transform;
        var drag = Make<Canvas>("Fire Emblem Test Drag Canvas");
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = new GameObject("Fire Emblem Test Card Prefab", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        _created.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = Make<CardManager>("Fire Emblem Test Cards");
        _cards.Configure(_field, drag, prefab, new[] { _fireEmblem }, System.Array.Empty<CardEnhancementData>());
        _cards.ConfigureRandom(new DeterministicRandom(1804));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = Make<CombatManager>("Fire Emblem Test Combat");
        _combat.ConfigurePlayer(30);
        Assert.That(_cards.BuyGear(_fireEmblem, 0), Is.True);
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
    public void FireEmblem_DestroysOnlyTheFirstCardUsedToBlockEachEncounter()
    {
        var enemyType = EnemyTypeData.Create("Fire Emblem Attacker", 30, 1, 0);
        _created.Add(enemyType);
        var enemies = new[] { new EnemyRuntime(enemyType, 1, 2), new EnemyRuntime(enemyType, 2, 2) };
        _combat.StartEncounter(enemies);
        var handViews = HandViews();
        Assert.That(handViews.Length, Is.GreaterThanOrEqualTo(3));
        var firstBlock = handViews[0];
        var secondBlock = handViews[1];
        var attackCard = handViews[2];
        int firstId = firstBlock.data.Id;
        int secondId = secondBlock.data.Id;
        Assert.That(_combat.TryPlayCards(new[] { attackCard }, enemies[0]), Is.True);
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(2));
        int handBefore = _cards.HandCount;

        Assert.That(_combat.TryDefendWithCards(new[] { firstBlock }), Is.True);

        Assert.That(_cards.HandCount, Is.EqualTo(handBefore), "The destroyed block card is replaced by Lighter's draw.");

        Assert.That(_cards.FindOwnedCard(firstId), Is.Null, "The first blocked card is destroyed, not discarded.");
        Assert.That(_cards.discardPile.Any(card => card.Id == firstId), Is.False);
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(1));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));

        Assert.That(_combat.TryDefendWithCards(new[] { secondBlock }), Is.True);

        Assert.That(_cards.FindOwnedCard(secondId), Is.Not.Null, "Fire Emblem triggers only once per encounter.");
        Assert.That(_cards.deck.Any(card => card.Id == secondId), Is.True,
            "A later Block card returns to the draw deck when Fire Emblem's once-per-encounter destruction has already been used.");
        Assert.That(_cards.discardPile.Any(card => card.Id == secondId), Is.False);
        Assert.That(_combat.PendingAttackCount, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void FireEmblemAsset_UsesAttackBlockedAndOncePerEncounterState()
    {
        Assert.That(_fireEmblem.canonicalId, Is.EqualTo("rel_018"));
        Assert.That(_fireEmblem.effects, Has.Length.EqualTo(1));
        Assert.That(_fireEmblem.effects[0].kind, Is.EqualTo(GameplayEffectKind.DestroyBlockingCard));
        Assert.That(_fireEmblem.effects[0].trigger, Is.EqualTo(GameplayEffectTrigger.AttackBlocked));
        Assert.That(_fireEmblem.effects[0].amount, Is.EqualTo(1), "Cards drawn after the destroy.");
    }

    CardView[] HandViews() => _field.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(view => view != null && view.data != null && _cards.hand.Contains(view.data)).ToArray();

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
