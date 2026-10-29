using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// HUD slice S10c: hand card shadows, lifts, selection crop marks and the "can't block" look.
public sealed class HandCardsCutSheetTests
{
    const float Frame = 1f / 60f;
    readonly List<Object> _created = new();
    RectTransform _holder;
    Field _field;

    [SetUp]
    public void SetUp()
    {
        CutSheetMotion.ReduceMotionOverride = null;
        var canvas = Create("S10c Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _holder = Create("S10c Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Field)).GetComponent<RectTransform>();
        _holder.SetParent(canvas.transform, false);
        _holder.sizeDelta = new Vector2(560f, 220f);
        _field = _holder.GetComponent<Field>();
        _field.cardsHolder = _holder;
    }

    [TearDown]
    public void TearDown()
    {
        CutSheetMotion.ReduceMotionOverride = null;
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void RestCard_HasInkPrintShadowAtEightBehindTheArtwork()
    {
        var card = Add();
        Settle(card);
        var ui = card.CutSheet;
        Assert.That(ui, Is.Not.Null);
        Assert.That(card.CurrentLift, Is.EqualTo(0f).Within(.01f));
        Assert.That(ui.ShadowDepth, Is.EqualTo(8f).Within(.01f));
        Assert.That(ui.Shadow.color, Is.EqualTo(CutSheetShadow.Tint));
        Assert.That(ui.Shadow.sprite, Is.EqualTo(card.face.sprite));
        Assert.That(ui.Shadow.transform.parent, Is.EqualTo(card.transform), "the shadow sits under the unlifted root");
        Assert.That(ui.Shadow.transform.GetSiblingIndex(), Is.LessThan(card.visualRoot.GetSiblingIndex()));
        var offset = ui.Shadow.rectTransform.anchoredPosition - card.visualRoot.anchoredPosition;
        Assert.That(offset.x, Is.EqualTo(8f).Within(.01f));
        Assert.That(offset.y, Is.EqualTo(-8f).Within(.01f));
        Assert.That(ui.Marks.IsShown, Is.False);
        Assert.That(ui.IsHatched, Is.False);
    }

    [Test]
    public void Hover_LiftsEightAndDeepensShadowToTwelve()
    {
        var card = Add();
        Settle(card);
        // The rest pose already carries the Field's fan arc offset; the hover lift adds to it.
        float restY = card.visualRoot.anchoredPosition.y;
        card.OnPointerEnter(null);
        Settle(card);
        Assert.That(card.CurrentLift, Is.EqualTo(8f).Within(.01f));
        Assert.That(card.visualRoot.anchoredPosition.y - restY, Is.EqualTo(8f).Within(.5f));
        Assert.That(card.CutSheet.ShadowDepth, Is.EqualTo(12f).Within(.01f));
        Assert.That(card.CutSheet.Marks.IsShown, Is.False);
    }

    [Test]
    public void Selected_LiftsAtLeast24WithThickCropMarksAndShadow20_DeselectRemovesMarks()
    {
        var card = Add();
        card.SetSelected(true);
        Settle(card);
        var ui = card.CutSheet;
        Assert.That(card.CurrentLift, Is.EqualTo(24f).Within(.01f), "selection alone lifts exactly 24");
        Assert.That(ui.ShadowDepth, Is.EqualTo(20f).Within(.01f));
        Assert.That(ui.Marks.Current, Is.EqualTo(CropMarksUI.State.Thick));
        for (int i = 0; i < ui.Marks.MarkCount; i++) Assert.That(ui.Marks.Mark(i).enabled, Is.True);

        card.SetSelected(false);
        Settle(card);
        Assert.That(ui.Marks.IsShown, Is.False);
        for (int i = 0; i < ui.Marks.MarkCount; i++) Assert.That(ui.Marks.Mark(i).enabled, Is.False);
        Assert.That(card.CurrentLift, Is.EqualTo(0f).Within(.01f));
        Assert.That(ui.ShadowDepth, Is.EqualTo(8f).Within(.01f));
    }

    [Test]
    public void Defense_CardThatCannotBlock_IsDarkenedHatchedAndNeverLiftsOnHover()
    {
        var card = Add();
        card.SetDefenseBlockState(true, false);
        card.OnPointerEnter(null);
        Settle(card);
        Assert.That(card.CannotBlock, Is.True);
        Assert.That(card.face.color, Is.EqualTo(HandCardCutSheetUI.DarkenedFace));
        Assert.That(HandCardCutSheetUI.DarkenedFace.r, Is.LessThan(.75f));
        Assert.That(card.CutSheet.IsHatched, Is.True);
        Assert.That(card.CutSheet.Hatch.color.a, Is.EqualTo(.2f).Within(.001f));
        Assert.That(card.CurrentLift, Is.EqualTo(0f).Within(.01f));
        Assert.That(card.CutSheet.ShadowDepth, Is.EqualTo(8f).Within(.01f));

        var blocker = Add();
        blocker.SetDefenseBlockState(true, true);
        Settle(blocker);
        Assert.That(blocker.CannotBlock, Is.False);
        Assert.That(blocker.CutSheet.IsHatched, Is.False);

        card.SetDefenseBlockState(false, true);
        Settle(card);
        Assert.That(card.CutSheet.IsHatched, Is.False);
        Assert.That(card.CurrentLift, Is.EqualTo(8f).Within(.01f), "out of defense the hovered card lifts again");
    }

    [Test]
    public void Lift_TweensOverAbout90ms_AndReduceMotionSnaps()
    {
        var card = Add();
        card.OnPointerEnter(null);
        card.TickPresentation(Frame);
        Assert.That(card.CurrentLift, Is.GreaterThan(0f).And.LessThan(8f));
        for (int i = 0; i < 6; i++) card.TickPresentation(Frame);
        Assert.That(card.CurrentLift, Is.EqualTo(8f).Within(.01f), "settled within ~100ms");

        CutSheetMotion.ReduceMotionOverride = true;
        card.SetSelected(true);
        Assert.That(card.CurrentLift, Is.EqualTo(32f).Within(.01f), "Reduce Motion snaps the lift");
        card.TickPresentation(Frame);
        Assert.That(card.CutSheet.ShadowDepth, Is.EqualTo(24f).Within(.01f));
    }

    [Test]
    public void ShadowMarksAndHatch_AreNotRaycastTargets_AndHitAreasAreUnchanged()
    {
        var card = Add();
        card.SetSelected(true);
        card.SetDefenseBlockState(true, false);
        Settle(card);
        var ui = card.CutSheet;
        Assert.That(ui.Shadow.raycastTarget, Is.False);
        Assert.That(ui.Hatch.raycastTarget, Is.False);
        for (int i = 0; i < ui.Marks.MarkCount; i++) Assert.That(ui.Marks.Mark(i).raycastTarget, Is.False);
        Assert.That(card.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(card.visualRoot.GetComponent<Image>().raycastTarget, Is.True);
    }

    [Test]
    public void Visuals_LeaveTheCardInstanceAndSelectionStateUntouched()
    {
        var card = Add();
        var instance = card.data;
        int id = instance.Id;
        var enhancement = instance.Enhancement;
        card.OnPointerEnter(null);
        Settle(card);
        Assert.That(card.IsSelected, Is.False, "hover never selects");
        card.SetSelected(true);
        card.SetDefenseBlockState(true, false);
        Settle(card);
        Assert.That(card.IsSelected, Is.True);
        Assert.That(card.CannotBlock, Is.False, "a selected card is never shown as unable to block");
        card.SetSelected(false);
        card.OnPointerExit(null);
        Settle(card);
        Assert.That(card.IsSelected, Is.False);
        Assert.That(card.data, Is.SameAs(instance));
        Assert.That(card.data.Id, Is.EqualTo(id));
        Assert.That(card.data.Enhancement, Is.SameAs(enhancement));
    }

    [Test]
    public void RefreshDefenseBlockState_ReadsTheRealDefenseWindow_DarkensAndHatchesThenClears()
    {
        // Arrange: a real CardManager hand (cards with a visual root) and a Full Coverage attack of 8.
        GameplayInputGate.Clear();
        var dragCanvas = Create("S10c Drag Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = Create("S10c Card Prefab", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        var visual = Create("Visual", typeof(RectTransform), typeof(Image));
        visual.transform.SetParent(prefabObject.transform, false);
        prefab.visualRoot = (RectTransform)visual.transform;
        prefab.face = visual.GetComponent<Image>();
        var cards = Create("S10c CardManager").AddComponent<CardManager>();
        cards.Configure(_field, dragCanvas, prefab, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        cards.BuildDeck();
        cards.DealHand();
        var combat = Create("S10c CombatManager").AddComponent<CombatManager>();
        combat.ConfigurePlayer(30);
        var bossType = EnemyTypeData.Create("S10c Full Coverage Boss", 100, 8, 0);
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        ability.effect = EnemyAbilityEffect.FullCoverageBlock;
        bossType.abilities = new[] { ability };
        _created.Add(bossType);
        _created.Add(ability);
        var boss = new EnemyRuntime(bossType);
        combat.StartEncounter(new[] { boss }, MapNodeType.Headliner);
        Assert.That(combat.TryPlayCards(new[] { HandViews()[0] }, boss), Is.True);
        Assume.That(combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assume.That(combat.pendingDamage, Is.EqualTo(8));
        CardView weak = null;
        foreach (var view in HandViews())
            if (combat.CalculateCardDefense(view.data) < 8) { weak = view; break; }
        Assume.That(weak, Is.Not.Null, "the dealt hand holds a card below 8 Block");
        int hp = combat.player.currentHealth, pending = combat.pendingDamage, handCount = cards.HandCount;

        // Act: the real gate and preview probe.
        weak.RefreshDefenseBlockState();
        weak.TickPresentation(Frame);

        // Assert
        Assert.That(weak.CannotBlock, Is.True);
        Assert.That(weak.face.color, Is.EqualTo(HandCardCutSheetUI.DarkenedFace));
        Assert.That(weak.CutSheet, Is.Not.Null);
        Assert.That(weak.CutSheet.IsHatched, Is.True);
        Assert.That(combat.pendingDamage, Is.EqualTo(pending), "the probe is a preview");
        Assert.That(combat.player.currentHealth, Is.EqualTo(hp));
        Assert.That(cards.HandCount, Is.EqualTo(handCount));

        // Act: the defense window changes (Block +10 Gear lets the card cover the attack alone).
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.effects = new[] { TestEffects.FlatBlock(10) };
        _created.Add(gear);
        cards.ownedGear.Add(gear);
        weak.RefreshDefenseBlockState();
        weak.TickPresentation(Frame);

        // Assert: the look clears.
        Assert.That(weak.CannotBlock, Is.False);
        Assert.That(weak.CutSheet.IsHatched, Is.False);
        Assert.That(weak.face.color, Is.Not.EqualTo(HandCardCutSheetUI.DarkenedFace));
        GameplayInputGate.Clear();
    }

    List<CardView> HandViews()
    {
        var views = new List<CardView>();
        for (int i = 0; i < _holder.childCount; i++)
            if (_holder.GetChild(i).TryGetComponent(out CardView view) && view.data != null) views.Add(view);
        return views;
    }

    static void Settle(CardView card)
    {
        for (int i = 0; i < 30; i++) card.TickPresentation(Frame);
    }

    CardView Add()
    {
        var go = Create("S10c Card", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(CardView));
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(136f, 194f);
        var element = go.GetComponent<LayoutElement>();
        element.preferredWidth = 136f;
        element.preferredHeight = 194f;
        var definition = CardData.Create(CardData.Suit.Spades, CardData.Rank.Seven);
        _created.Add(definition);
        var card = go.GetComponent<CardView>();
        var visual = Create("Visual", typeof(RectTransform), typeof(Image));
        visual.transform.SetParent(go.transform, false);
        card.visualRoot = (RectTransform)visual.transform;
        card.visualRoot.anchorMin = Vector2.zero;
        card.visualRoot.anchorMax = Vector2.one;
        card.visualRoot.offsetMin = card.visualRoot.offsetMax = Vector2.zero;
        card.face = visual.GetComponent<Image>();
        _field.AddCard(card, new CardInstance(definition, _created.Count));
        return card;
    }

    GameObject Create(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        _created.Add(go);
        return go;
    }
}
