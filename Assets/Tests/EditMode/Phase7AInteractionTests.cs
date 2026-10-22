using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7AInteractionTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;
    Canvas _dragCanvas;
    CardView _cardPrefab;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        CreateComponent<UnityEngine.EventSystems.EventSystem>("Phase7A EventSystem");
        _field = CreateHandField();
        _dragCanvas = CreateComponent<Canvas>("Phase7A Drag Canvas");
        _dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _cardPrefab = CreateCardView("Phase7A Card Prefab");

        _cards = CreateComponent<CardManager>("Phase7A CardManager");
        _cards.Configure(_field, _dragCanvas, _cardPrefab, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        _cards.BuildDeck();
        _cards.DealHand();

        _combat = CreateComponent<CombatManager>("Phase7A CombatManager");
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
    public void PlayerTurnSelection_ReplacesPreviousCard_AndClickingSelectedCardDeselects()
    {
        var enemy = CreateEnemyRuntime("Selection Target", 30, 4);
        _combat.StartEnemy(enemy);
        var views = HandViews();

        _cards.ToggleCardSelection(views[2]);
        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { views[2] }));

        _cards.ToggleCardSelection(views[0]);
        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { views[0] }));
        Assert.That(views[2].IsSelected, Is.False);
        Assert.That(views[0].IsSelected, Is.True);

        _cards.ToggleCardSelection(views[0]);
        Assert.That(_cards.SelectedCards, Is.Empty);
        Assert.That(views[0].IsSelected, Is.False);
    }

    [Test]
    public void DefenseSelection_RequiresDistinctFullyBlockedAttacks_AndReopensAfterDeselect()
    {
        var type = EnemyTypeData.Create("Defense Selection", 50, 3, 0);
        _created.Add(type);
        var enemies = new[] { new EnemyRuntime(type, 1, 2), new EnemyRuntime(type, 2, 2) };
        _combat.StartEncounter(enemies);
        Assert.That(_combat.TryPlayCards(new[] { HandViews()[0] }, enemies[0]), Is.True);
        var bonus = ScriptableObject.CreateInstance<GearData>();
        bonus.effects = new[] { TestEffects.FlatBlock(3) };
        _created.Add(bonus);
        _cards.ownedGear.Add(bonus);

        var candidates = HandViews().Where(view => _combat.CalculateCardDefense(view.data) >= 3).Take(3).ToArray();
        Assert.That(candidates.Length, Is.EqualTo(3));
        _cards.ToggleCardSelection(candidates[0]);
        _cards.ToggleCardSelection(candidates[1]);
        Assert.That(_cards.SelectedCards.Count, Is.EqualTo(2));
        _cards.ToggleCardSelection(candidates[2]);
        Assert.That(_cards.SelectedCards.Count, Is.EqualTo(2), "Only two pending attacks can be blocked.");
        _cards.ToggleCardSelection(candidates[1]);
        _cards.ToggleCardSelection(candidates[2]);
        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { candidates[0], candidates[2] }));
    }

    [Test]
    public void EnemyDropTarget_ForwardsContextualAttackAndDefenseActions()
    {
        var enemy = CreateEnemyRuntime("Drop Target", 30, 5);
        _combat.StartEnemy(enemy);
        var card = HandViews()[0];
        int expectedDamage = _combat.CalculateCardAttackDamage(card.data);
        _cards.SelectOnlyCard(card);
        _cards.BeginCardDrag(card);

        var targetObject = CreateGameObject("Enemy Drop Target", typeof(RectTransform), typeof(Image));
        var display = targetObject.AddComponent<EnemyDisplayUI>();
        var target = targetObject.AddComponent<CardActionDropTarget>();
        target.enemyDisplay = display;
        target.feedbackGraphic = targetObject.GetComponent<Image>();

        Assert.That(target.CanAccept(card), Is.True);
        Assert.That(target.TryCommit(card), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(enemy.maxHp - expectedDamage));
        Assert.That(_cards.HandCount, Is.EqualTo(7));
        Assert.That(_cards.discardPile, Is.Empty, "The played card returns to the draw pile.");
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));

        var bonus = ScriptableObject.CreateInstance<GearData>();
        bonus.effects = new[] { TestEffects.FlatBlock(5) };
        _created.Add(bonus);
        _cards.ownedGear.Add(bonus);
        var defender = HandViews().First(view => _combat.CalculateCardDefense(view.data) >= 5);
        int discardBeforeBlock = _cards.discardPile.Count;
        int deckBeforeBlock = _cards.deck.Count;
        _cards.ToggleCardSelection(defender);
        _cards.BeginCardDrag(defender);

        Assert.That(target.CanAccept(defender), Is.True);
        Assert.That(target.TryCommit(defender), Is.True);
        Assert.That(_cards.discardPile.Count, Is.EqualTo(discardBeforeBlock), "Blocking returns the selected card to the draw deck rather than the discard pile.");
        Assert.That(_cards.deck.Count, Is.EqualTo(deckBeforeBlock + 1));
        Assert.That(_cards.deck, Does.Contain(defender.data));
        Assert.That(_combat.pendingDamage, Is.Zero);
    }

    [Test]
    public void MultiCardDefense_CombinesPartialBlockAgainstOneOrdinaryAttack()
    {
        var enemy = CreateEnemyRuntime("Defense Target", 50, 20);
        _combat.StartEnemy(enemy);
        Assert.That(_combat.TryPlayCards(new[] { HandViews()[0] }, enemy), Is.True);
        var remaining = HandViews();
        var firstSet = new[] { remaining[0], remaining[1] };
        int expectedBlocked = firstSet.Sum(card => _combat.CalculateCardDefense(card.data));
        int handBefore = _cards.HandCount;
        int discardedBefore = _cards.discardPile.Count;
        int deckBefore = _cards.deck.Count;

        Assert.That(_combat.TryDefendWithCards(firstSet), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(Mathf.Max(0, 20 - expectedBlocked)));
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore - 2));
        Assert.That(_cards.discardPile.Count, Is.EqualTo(discardedBefore));
        Assert.That(_cards.deck.Count, Is.EqualTo(deckBefore + 2));
    }

    [Test]
    public void InvalidBatchDefense_DoesNotPartiallyConsumeCards()
    {
        var enemy = CreateEnemyRuntime("Invalid Defense", 50, 10);
        _combat.StartEnemy(enemy);
        Assert.That(_combat.TryPlayCards(new[] { HandViews()[0] }, enemy), Is.True);

        var valid = HandViews()[0];
        var foreignObject = CreateGameObject("Foreign Card", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var foreign = foreignObject.AddComponent<CardView>();
        foreign.data = valid.data;
        int handBefore = _cards.HandCount;
        int discardBefore = _cards.discardPile.Count;

        Assert.That(_combat.TryDefendWithCards(new[] { valid, foreign }), Is.False);
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore));
        Assert.That(_cards.discardPile.Count, Is.EqualTo(discardBefore));
        Assert.That(_combat.pendingDamage, Is.EqualTo(10));
    }

    [Test]
    public void HandVisualDrag_UsesPlaceholderAndCancelRestoresOriginalOrder()
    {
        var views = HandViews();
        var dragged = views[3];
        int originalIndex = dragged.transform.GetSiblingIndex();
        int holderChildren = _field.cardsHolder.childCount;

        var slot = _field.BeginVisualDrag(dragged);
        dragged.transform.SetParent(_dragCanvas.transform, true);

        Assert.That(slot, Is.Not.Null);
        Assert.That(_field.cardsHolder.childCount, Is.EqualTo(holderChildren), "The placeholder must preserve hand layout on pickup.");
        Assert.That(slot.transform.GetSiblingIndex(), Is.EqualTo(originalIndex));

        _field.EndVisualDrag(dragged, false);
        Assert.That(dragged.transform.parent, Is.SameAs(_field.cardsHolder));
        Assert.That(dragged.transform.GetSiblingIndex(), Is.EqualTo(originalIndex));
        Assert.That(_field.cardsHolder.childCount, Is.EqualTo(holderChildren));
    }

    [TestCase(1f, 1f)]
    [TestCase(0.75f, 1.25f)]
    public void PointerDownDrag_KeepsOffCenterVisualGripUnderPointerAcrossCanvasScales_AndCancelRestores(
        float handCanvasScale, float dragCanvasScale)
    {
        _combat.StartEnemy(CreateEnemyRuntime("Drag Regression Target", 30, 4));
        var views = HandViews();
        var dragged = views[3];
        var visualObject = CreateGameObject("Off-center Card Visual", typeof(RectTransform), typeof(Image));
        visualObject.transform.SetParent(dragged.transform, false);
        dragged.visualRoot = (RectTransform)visualObject.transform;
        dragged.visualRoot.sizeDelta = new Vector2(132f, 184f);
        _field.transform.root.localScale = Vector3.one * handCanvasScale;
        _dragCanvas.transform.localScale = Vector3.one * dragCanvasScale;

        var originalParent = dragged.transform.parent;
        int originalIndex = dragged.transform.GetSiblingIndex();
        _cards.ToggleCardSelection(dragged);
        for (int i = 0; i < 60; i++) dragged.TickPresentation(1f / 60f);
        Vector2 gripLocal = new(dragged.visualRoot.rect.width * 0.31f, -dragged.visualRoot.rect.height * 0.23f);
        Vector2 pointerPosition = RectTransformUtility.WorldToScreenPoint(null,
            dragged.visualRoot.TransformPoint(gripLocal));
        var pointer = new UnityEngine.EventSystems.PointerEventData(
            UnityEngine.EventSystems.EventSystem.current)
        {
            button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
            position = pointerPosition,
            pressPosition = pointerPosition
        };

        dragged.OnPointerDown(pointer);
        dragged.OnBeginDrag(pointer);
        Assert.That(dragged.IsDragging, Is.True);
        Assert.That(dragged.transform.parent, Is.SameAs(_dragCanvas.transform));
        AssertGripAtPointer(dragged, gripLocal, pointerPosition, "Begin drag must not jump the artwork grip.");

        pointerPosition += new Vector2(140f, 95f);
        pointer.position = pointerPosition;
        dragged.OnDrag(pointer);
        for (int i = 0; i < 24; i++)
            dragged.TickPresentation(1f / 60f);
        AssertGripAtPointer(dragged, gripLocal, pointerPosition,
            "The captured visual grip must remain under the pointer while presentation rotation/scale settle.");

        dragged.CancelActiveDrag();
        Assert.That(dragged.IsDragging, Is.False);
        Assert.That(dragged.transform.parent, Is.SameAs(originalParent));
        Assert.That(dragged.transform.GetSiblingIndex(), Is.EqualTo(originalIndex));
        Assert.That(dragged.canvasGroup.blocksRaycasts, Is.True);
        Assert.That(_cards.ActiveDragCard, Is.Null);
        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { dragged }));

        dragged.OnPointerClick(pointer);
        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { dragged }),
            "A cancelled drag must suppress the trailing click rather than toggling selection.");
    }

    [Test]
    public void SortingPresentation_DoesNotChangeHandOrSelectedCardOrder()
    {
        _combat.StartEnemy(CreateEnemyRuntime("Sort Regression Target", 30, 4));
        var views = HandViews();
        var selected = views[1];
        _cards.ToggleCardSelection(selected);
        var handBefore = _cards.hand.ToArray();
        var selectedBefore = _cards.SelectedCards.ToArray();

        _field.SortByRank();

        Assert.That(_cards.hand, Is.EqualTo(handBefore));
        Assert.That(_cards.SelectedCards, Is.EqualTo(selectedBefore));
        _field.SortBySuit();
        Assert.That(_cards.hand, Is.EqualTo(handBefore));
        Assert.That(_cards.SelectedCards, Is.EqualTo(selectedBefore));
    }

    static void AssertGripAtPointer(CardView card, Vector2 localGrip, Vector2 screenPointer, string message)
    {
        Vector2 gripScreen = RectTransformUtility.WorldToScreenPoint(null,
            card.visualRoot.TransformPoint(localGrip));
        Assert.That(Vector2.Distance(gripScreen, screenPointer), Is.LessThan(0.1f), message);
    }

    [Test]
    public void CancelAndReset_ClearTransientSelectionWithoutCorruptingZones()
    {
        var enemy = CreateEnemyRuntime("Reset Target", 30, 4);
        _combat.StartEnemy(enemy);
        var views = HandViews();
        _cards.ToggleCardSelection(views[0]);
        _cards.ToggleCardSelection(views[1]);
        int handBefore = _cards.HandCount;

        _cards.CancelCardInteractions();
        Assert.That(_cards.SelectedCards, Is.Empty);
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore));
        Assert.That(_cards.discardPile, Is.Empty);

        _cards.ToggleCardSelection(views[2]);
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, views[2].transform.position)
        };
        views[2].OnBeginDrag(pointer);
        Assert.That(_cards.ActiveDragCard, Is.SameAs(views[2]));

        _cards.Reset();
        Assert.That(_cards.SelectedCards, Is.Empty);
        Assert.That(_cards.ownedCards, Is.Empty);
        Assert.That(_cards.hand, Is.Empty);
        Assert.That(_field.cardsHolder.childCount, Is.Zero);
    }

    List<CardView> HandViews()
    {
        var views = new List<CardView>();
        for (int i = 0; i < _field.cardsHolder.childCount; i++)
            if (_field.cardsHolder.GetChild(i).TryGetComponent<CardView>(out var view))
                views.Add(view);
        return views;
    }

    Field CreateHandField()
    {
        var canvas = CreateComponent<Canvas>("Phase7A Hand Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Hand Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)holder.transform;
        rect.sizeDelta = new Vector2(1200f, 220f);
        var layout = holder.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var field = holder.AddComponent<Field>();
        field.cardsHolder = rect;
        return field;
    }

    CardView CreateCardView(string name)
    {
        var gameObject = CreateGameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var rect = (RectTransform)gameObject.transform;
        rect.sizeDelta = new Vector2(132f, 184f);
        var layout = gameObject.GetComponent<LayoutElement>();
        layout.minWidth = 112f;
        layout.minHeight = 156f;
        layout.preferredWidth = 132f;
        layout.preferredHeight = 184f;
        var view = gameObject.AddComponent<CardView>();
        view.face = gameObject.GetComponent<Image>();
        view.canvasGroup = gameObject.GetComponent<CanvasGroup>();
        return view;
    }

    EnemyRuntime CreateEnemyRuntime(string name, int health, int attack)
    {
        var definition = EnemyTypeData.Create(name, health, attack, 0);
        _created.Add(definition);
        return new EnemyRuntime(definition);
    }

    T CreateComponent<T>(string name) where T : Component
    {
        return CreateGameObject(name).AddComponent<T>();
    }

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var gameObject = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }
}
