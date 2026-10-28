using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// HUD slice S8 (visual-design 3b, hud.md section C): the enemy row's crop marks and the one INCOMING tag.
public sealed class EnemyRowCutSheetTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;
    Canvas _canvas;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        CutSheetMotion.ReduceMotionOverride = null;
        _canvas = Make("S8 Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = Make("S8 Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(_canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var prefabObject = Make("S8 Card Prefab", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = Make("S8 CardManager").AddComponent<CardManager>();
        _cards.Configure(_field, _canvas, prefab, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = Make("S8 CombatManager").AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        CutSheetMotion.ReduceMotionOverride = null;
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void IncomingTag_ThreeEnemiesTwoRetaliating_ShowsOneAggregateTotal()
    {
        // Arrange: three enemies; killing one leaves two retaliating in one pending hit.
        var enemies = StartScrappers(3);
        var killer = HandViews().OrderByDescending(view => _combat.CalculateCardAttackDamage(view.data)).First();
        Assert.That(_combat.TryPlayCards(new[] { killer }, enemies[2]), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        var label = Label("pendingDamageLabel", _canvas.transform);

        // Act
        var tag = IncomingTagUI.Dress(label);
        var again = IncomingTagUI.Dress(label);
        tag.Show(IncomingTagUI.ShownFor(_combat), IncomingTagUI.ValueFor(_combat), true);

        // Assert
        Assert.That(again, Is.SameAs(tag));
        Assert.That(_canvas.GetComponentsInChildren<IncomingTagUI>(true), Has.Length.EqualTo(1), "One INCOMING tag, never one per enemy.");
        Assert.That(IncomingTagUI.ValueFor(_combat), Is.EqualTo(_combat.IncomingDamageAfterModifiers));
        Assert.That(IncomingTagUI.ValueFor(_combat), Is.EqualTo(enemies[0].EffectiveAttack + enemies[1].EffectiveAttack));
        Assert.That(tag.Visible, Is.True);
        Assert.That(tag.WordLabel.text, Is.EqualTo("INCOMING"));
        Assert.That(tag.NumberLabel.text, Is.EqualTo(_combat.IncomingDamageAfterModifiers.ToString()));
    }

    [Test]
    public void IncomingTag_HiddenOutsideDefense()
    {
        StartScrappers(2);
        var tag = IncomingTagUI.Dress(Label("pendingDamageLabel", _canvas.transform));

        tag.Show(IncomingTagUI.ShownFor(_combat), IncomingTagUI.ValueFor(_combat), false);

        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(tag.Visible, Is.False);
    }

    [Test]
    public void IncomingTag_FullyBlocked_ReadsAllBlockedAndGoesFlat()
    {
        var tag = IncomingTagUI.Dress(Label("pendingDamageLabel", _canvas.transform));

        tag.Show(true, 5, true);
        Assert.That(tag.IsFlat, Is.False);
        Assert.That(Mathf.Abs(tag.TiltDegrees), Is.EqualTo(CutSheetMetrics.TagTiltRest).Within(.01f));
        Assert.That(tag.OffsetDepth, Is.EqualTo(IncomingTagUI.OffsetDefense).Within(.01f));
        Assert.That(tag.Halftone.enabled, Is.True);

        tag.Show(true, 0, true);

        Assert.That(tag.IsFlat, Is.True);
        Assert.That(tag.WordLabel.text, Is.EqualTo("ALL BLOCKED"));
        Assert.That(tag.NumberLabel.gameObject.activeSelf, Is.False, "ALL BLOCKED replaces both the word and the number.");
        Assert.That(tag.TiltDegrees, Is.EqualTo(0f).Within(.01f));
        Assert.That(tag.OffsetDepth, Is.EqualTo(0f).Within(.01f));
        Assert.That(tag.Halftone.enabled, Is.False);
    }

    [Test]
    public void IncomingTag_TiltSignAlternatesPerAppearance()
    {
        var tag = IncomingTagUI.Dress(Label("pendingDamageLabel", _canvas.transform));
        tag.Show(true, 3, true);
        float first = tag.TiltDegrees;
        tag.Show(false, 0, false);
        tag.Show(true, 3, true);
        Assert.That(Mathf.Sign(tag.TiltDegrees), Is.EqualTo(-Mathf.Sign(first)));
    }

    [Test]
    public void CropMarks_HiddenWithOneEnemy()
    {
        var enemy = new EnemyRuntime(Type("Loner", 20, 3));
        _combat.StartEncounter(new[] { enemy });
        var view = CreateView("Lone View", enemy);

        view.SetEnemyTargeting(true, true);
        view.RefreshTargetMarks();

        Assert.That(view.TargetMarks.Current, Is.EqualTo(CropMarksUI.State.Hidden));
        Assert.That(view.TargetMarks.Mark(0).enabled, Is.False);
        Assert.That(view.TargetStripShown, Is.False);
    }

    [Test]
    public void CropMarks_TwoOrMoreEnemies_ThinOnValidThickOnTargeted()
    {
        var enemies = StartScrappers(3);
        var views = enemies.Select((enemy, i) => CreateView($"View {i}", enemy)).ToArray();
        foreach (var view in views) view.RefreshTargetMarks();
        int current = System.Array.IndexOf(enemies, _combat.currentEnemy);
        Assert.That(current, Is.GreaterThanOrEqualTo(0));
        Assert.That(views[current].TargetMarks.Current, Is.EqualTo(CropMarksUI.State.Thick), "The enemy the next action goes to.");
        Assert.That(views[current].TargetStripShown, Is.True);
        var other = views[(current + 1) % views.Length];
        Assert.That(other.TargetMarks.Current, Is.EqualTo(CropMarksUI.State.Hidden));

        other.SetEnemyTargeting(true, true);
        Assert.That(other.TargetMarks.Current, Is.EqualTo(CropMarksUI.State.Thin));
        Assert.That(other.TargetMarks.MarkScale, Is.EqualTo(CropMarksUI.ThinScale).Within(.001f));
        Assert.That(other.TargetStripShown, Is.False);

        other.OnPointerEnter(new PointerEventData(null));
        Assert.That(other.TargetMarks.Current, Is.EqualTo(CropMarksUI.State.Thick));
        Assert.That(other.TargetMarks.MarkScale, Is.EqualTo(CropMarksUI.ThickScale).Within(.001f));
        Assert.That(other.TargetStripShown, Is.True);

        var ineligible = views[(current + 2) % views.Length];
        ineligible.SetEnemyTargeting(true, false);
        Assert.That(ineligible.TargetMarks.Current, Is.EqualTo(CropMarksUI.State.Hidden), "Illegal targets get no marks.");
    }

    [Test]
    public void EnemyRow_NoValidOrInvalidTargetText()
    {
        var enemies = StartScrappers(2);
        var views = enemies.Select((enemy, i) => CreateView($"View {i}", enemy)).ToArray();
        _cards.SelectOnlyCard(HandViews()[0]);

        views[0].SetEnemyTargeting(true, true);
        views[1].SetEnemyTargeting(true, false);
        foreach (var view in views) { view.RefreshStateGuidance(); view.RefreshTargetMarks(); }

        var texts = views.SelectMany(view => view.GetComponentsInChildren<TMP_Text>(true)).Select(text => text.text ?? string.Empty);
        Assert.That(texts.Where(text => text.ToUpperInvariant().Contains("VALID")), Is.Empty);
    }

    [Test]
    public void EnemyDecorations_AreNotRaycastTargets()
    {
        var enemies = StartScrappers(2);
        var view = CreateView("Dressed View", enemies[0]);
        view.RefreshTargetMarks();

        Assert.That(view.Decorations, Is.Not.Empty);
        for (int i = 0; i < view.TargetMarks.MarkCount; i++)
            Assert.That(view.Decorations, Does.Contain(view.TargetMarks.Mark(i)));
        foreach (var graphic in view.Decorations)
            Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        Assert.That(view.GetComponent<Image>().raycastTarget, Is.True, "The view root keeps the hit area.");
    }

    [Test]
    public void IncomingTagDecorations_AreNotRaycastTargets()
    {
        var tag = IncomingTagUI.Dress(Label("pendingDamageLabel", _canvas.transform));
        foreach (var graphic in tag.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.False, graphic.name);
    }

    [Test]
    public void HpMeter_IsAtLeast14PxWithFrameGapAndSolidFullFill()
    {
        var enemies = StartScrappers(2);
        var view = CreateView("Meter View", enemies[0]);

        var meter = view.HpMeter;
        var track = (RectTransform)meter.Find("Track");
        var fill = (RectTransform)track.Find("Fill");

        Assert.That(meter.rect.height, Is.GreaterThanOrEqualTo(14f));
        Assert.That(track.offsetMin.y, Is.EqualTo(2f).Within(.01f), "2px ink frame.");
        Assert.That(fill.offsetMin.y, Is.EqualTo(2f).Within(.01f), "2px cream gap between frame and fill.");
        Assert.That(meter.rect.height - 8f, Is.GreaterThanOrEqualTo(6f), "The ink fill has visible height.");
        Assert.That(fill.anchorMax.x, Is.EqualTo(1f).Within(.001f), "Full HP fills the track.");
        Assert.That(fill.GetComponent<Image>().color, Is.EqualTo(DealbreakUIStyle.Theme.paper), "Cream fill (owner 2026-10-29).");
        Assert.That(track.GetComponent<Image>().color, Is.EqualTo(DealbreakUIStyle.Theme.inkPrint), "Lost HP reads dark.");
    }

    [Test]
    public void IntentLine_HiddenWhenItHasNothingToShow()
    {
        var enemies = StartScrappers(2);
        var view = CreateView("Plain View", enemies[0]);
        view.statusText.text = "Choose your next encounter";

        view.RefreshStateGuidance();

        Assert.That(view.statusText.enabled, Is.False);
    }

    [Test]
    public void CropMarks_SitOutsideThePortraitBoundsThinAndThick()
    {
        var enemies = StartScrappers(2);
        var view = CreateView("Marked View", enemies[0]);
        foreach (var state in new[] { CropMarksUI.State.Thin, CropMarksUI.State.Thick })
        {
            view.TargetMarks.Set(state);
            float distance = CropMarksUI.DistanceFor(view.TargetMarks.MarkScale);
            Assert.That(distance, Is.GreaterThanOrEqualTo(CropMarksUI.StrokeWeight * view.TargetMarks.MarkScale + 6f - .001f));
            for (int i = 0; i < view.TargetMarks.MarkCount; i++)
            {
                var mark = view.TargetMarks.Mark(i).rectTransform;
                var outward = new Vector2(mark.anchorMin.x * 2f - 1f, mark.anchorMin.y * 2f - 1f);
                Assert.That(mark.anchoredPosition.x * outward.x, Is.GreaterThanOrEqualTo(distance - .001f), $"{state} mark {i} x");
                Assert.That(mark.anchoredPosition.y * outward.y, Is.GreaterThanOrEqualTo(distance - .001f), $"{state} mark {i} y");
            }
        }
    }

    [Test]
    public void PortraitFrame_IsRetiredOnTheDressedView()
    {
        var enemies = StartScrappers(2);
        var view = CreateView("Frameless View", enemies[0]);

        Assert.That(view.transform.Find("PortraitMount").gameObject.activeSelf, Is.False);
        Assert.That(view.transform.Find("PortraitWindow").GetComponent<Image>().enabled, Is.False);
        Assert.That(view.transform.Find("PortraitWindow").GetComponent<RectMask2D>().enabled, Is.True);
    }

    [Test]
    public void IncomingTag_PaperEdgeAndMaskedHalftoneOffsetAreBuilt()
    {
        var tag = IncomingTagUI.Dress(Label("pendingDamageLabel", _canvas.transform));
        tag.Show(true, 7, true);

        var edge = tag.Edge.rectTransform;
        Assert.That(tag.Edge.enabled, Is.True);
        Assert.That(tag.Edge.color, Is.EqualTo(DealbreakUIStyle.Theme.paper));
        Assert.That(edge.offsetMin.x, Is.LessThanOrEqualTo(-2f - IncomingTagUI.SpriteBorder));
        Assert.That(edge.offsetMax.x, Is.GreaterThanOrEqualTo(2f + IncomingTagUI.SpriteBorder));
        Assert.That(tag.Face.rectTransform.offsetMin, Is.EqualTo(Vector2.zero), "The red tag is the body; the edge is larger.");
        Assert.That(tag.Shadow.transform.parent.GetSiblingIndex(), Is.LessThan(edge.GetSiblingIndex()));
        Assert.That(edge.GetSiblingIndex(), Is.LessThan(tag.HalftoneOffset.GetSiblingIndex()));
        Assert.That(tag.HalftoneOffset.GetSiblingIndex(), Is.LessThan(tag.Face.rectTransform.GetSiblingIndex()));

        var offset = tag.HalftoneOffset;
        Assert.That((offset.anchorMin, offset.anchorMax), Is.EqualTo((Vector2.zero, Vector2.one)), "The stencil spans the tag.");
        Assert.That(offset.offsetMin, Is.EqualTo(new Vector2(IncomingTagUI.OffsetDefense, -IncomingTagUI.OffsetDefense)));
        Assert.That(offset.TryGetComponent(out Mask mask), Is.True);
        Assert.That(mask.showMaskGraphic, Is.False);
        Assert.That(offset.GetComponent<Image>().sprite, Is.SameAs(tag.Face.sprite), "Clipped to the tag shape.");
        Assert.That(tag.Halftone.enabled, Is.True);
        Assert.That(tag.Halftone.sprite, Is.SameAs(DealbreakUIStyle.Theme.halftoneSprite));
    }

    [Test]
    public void AbilityTags_OnePaperTagPerAbilityWithInkAuthoredName()
    {
        var enemy = new EnemyRuntime(TypeWithAbilities("Duelist", "Suit Callout", "Called suit rules.", "Guarded", "Guard rules."));
        _combat.StartEncounter(new[] { enemy, new EnemyRuntime(Type("Second", 20, 2)) });

        var view = CreateView("Ability View", enemy);

        Assert.That(view.AbilityTagCount, Is.EqualTo(2));
        var theme = DealbreakUIStyle.Theme;
        for (int i = 0; i < 2; i++)
        {
            var face = view.AbilityTag(i).Find("Face").GetComponent<Image>();
            Assert.That(face.color, Is.EqualTo(theme.paper));
            Assert.That(face.sprite, Is.SameAs(theme.tagSprite));
            Assert.That(face.raycastTarget, Is.True, "The tag takes its own click.");
            Assert.That(view.AbilityTagLabel(i).color, Is.EqualTo(theme.inkPrint));
            Assert.That(view.AbilityTag(i).TryGetComponent(out Selectable _), Is.True, "Keyboard focus reaches the tag.");
        }
        Assert.That(view.AbilityTagLabel(0).text, Is.EqualTo("Suit Callout"), "The name exactly as authored.");
    }

    [Test]
    public void AbilityTag_Click_PinsItsRulesAndLeavesTheTargetAlone()
    {
        var tooltip = Make("S8 Tooltip").AddComponent<ContextualTooltipUI>();
        Invoke(tooltip, "OnEnable");
        try
        {
            var first = new EnemyRuntime(Type("First", 20, 2));
            var duelist = new EnemyRuntime(TypeWithAbilities("Duelist", "Suit Callout", "Called suit rules."));
            _combat.StartEncounter(new[] { first, duelist });
            var target = _combat.currentEnemy;
            var view = CreateView("Clicked View", duelist);
            var drop = view.gameObject.AddComponent<CardActionDropTarget>();
            drop.enemyDisplay = view;
            var face = view.AbilityTag(0).Find("Face").gameObject;

            var handler = ExecuteEvents.ExecuteHierarchy(face, new PointerEventData(null) { button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler);

            var detail = view.AbilityTagDetail(0);
            Assert.That(handler, Is.SameAs(view.AbilityTag(0).gameObject), "The tag consumes the click.");
            Assert.That(tooltip.IsPinned(detail), Is.True);
            Assert.That(ContextualTooltipUI.TryDescribe(detail, out var info), Is.True);
            Assert.That((info.Title, info.Body), Is.EqualTo(("Suit Callout", "Called suit rules.")));
            Assert.That(_combat.currentEnemy, Is.SameAs(target), "The enemy target did not change.");
        }
        finally { Invoke(tooltip, "OnDisable"); }
    }

    [Test]
    public void EnemyDetail_NoLongerCarriesAbilityText()
    {
        var duelist = new EnemyRuntime(TypeWithAbilities("Duelist", "Test Trait", "Test trait rules."));
        _combat.StartEncounter(new[] { duelist, new EnemyRuntime(Type("Second", 20, 2)) });
        var view = CreateView("Detail View", duelist);
        view.RefreshStateGuidance();

        Assert.That(view.DetailGuidance ?? string.Empty, Does.Not.Contain("Test Trait").IgnoreCase.And.Not.Contain("Test trait rules"));
        Assert.That(view.statusText.text, Does.Not.Contain("Test Trait").IgnoreCase);
        if (ContextualTooltipUI.TryDescribe(view, out var info))
            Assert.That(info.Body, Does.Not.Contain("Test Trait").IgnoreCase.And.Not.Contain("Test trait rules"));
    }

    [Test]
    public void ViewWithoutRectTransform_BindsAndRefreshesWithoutThrowing()
    {
        var enemies = StartScrappers(2);
        var view = Make("Plain Transform View").AddComponent<EnemyDisplayUI>();
        view.statusText = Label("Plain Status", _canvas.transform);

        Assert.DoesNotThrow(() =>
        {
            view.Bind(enemies[0]);
            view.SetEnemyTargeting(true, true);
            view.RefreshTargetMarks();
            view.RefreshStateGuidance();
        });
        Assert.That(view.TargetMarks, Is.Null, "No dressing without a rect.");
    }

    [Test]
    public void IncomingTag_PendingHitReducedToZero_ReadsIncomingZeroNotAllBlocked()
    {
        var tag = IncomingTagUI.Dress(Label("pendingDamageLabel", _canvas.transform));
        tag.Show(true, 6, true, false);

        // Armour reduces a still-pending hit to 0: nothing was blocked.
        tag.Show(true, 0, true, false);

        Assert.That(tag.IsFlat, Is.False);
        Assert.That(tag.WordLabel.text, Is.EqualTo("INCOMING"));
        Assert.That(tag.NumberLabel.gameObject.activeSelf, Is.True);
        Assert.That(tag.NumberLabel.text, Is.EqualTo("0"));

        tag.Show(true, 0, true, true);
        Assert.That(tag.IsFlat, Is.True);
        Assert.That(tag.WordLabel.text, Is.EqualTo("ALL BLOCKED"));
    }

    [Test]
    public void IncomingTag_FullyBlockedFor_OnlyWhenNothingIsPending()
    {
        var enemies = StartScrappers(3);
        Assert.That(IncomingTagUI.FullyBlockedFor(_combat), Is.EqualTo(_combat.pendingDamage <= 0));
        var killer = HandViews().OrderByDescending(view => _combat.CalculateCardAttackDamage(view.data)).First();
        Assert.That(_combat.TryPlayCards(new[] { killer }, enemies[2]), Is.True);
        Assert.That(_combat.pendingDamage, Is.GreaterThan(0));
        Assert.That(IncomingTagUI.FullyBlockedFor(_combat), Is.False);
    }

    [Test]
    public void WrappedTags_KeepTheIntentLineInsideTheView()
    {
        var enemy = new EnemyRuntime(TypeWithAbilities("Wrapper",
            "Extremely Long Ability Name Alpha", "Rules A.", "Extremely Long Ability Name Beta", "Rules B."));
        _combat.StartEncounter(new[] { enemy, new EnemyRuntime(Type("Second", 20, 2)) });

        var view = CreateView("Wrapped View", enemy);

        float lowestTagBottom = float.MaxValue;
        for (int i = 0; i < view.AbilityTagCount; i++)
        {
            var tag = view.AbilityTag(i);
            lowestTagBottom = Mathf.Min(lowestTagBottom, tag.anchoredPosition.y - tag.sizeDelta.y);
        }
        Assume.That(view.AbilityTag(1).anchoredPosition.y, Is.LessThan(view.AbilityTag(0).anchoredPosition.y), "The tags wrapped.");
        var status = view.statusText.rectTransform;
        float height = ((RectTransform)view.transform).rect.height;
        Assert.That(status.anchorMin.y, Is.EqualTo(0f));
        Assert.That(status.offsetMin.y, Is.GreaterThanOrEqualTo(0f), "The intent line stays inside the view's bottom.");
        Assert.That(status.offsetMax.y, Is.LessThanOrEqualTo(height));
        Assert.That(lowestTagBottom, Is.GreaterThanOrEqualTo(status.offsetMax.y), "Tags stay above the intent line.");
        Assert.That(view.HpMeter.offsetMin.y, Is.GreaterThan(lowestTagBottom), "The HP row moved up with the wrap.");
    }

    EnemyTypeData TypeWithAbilities(string name, params string[] namesAndRules)
    {
        var type = Type(name, 20, 2);
        var abilities = new EnemyAbility[namesAndRules.Length / 2];
        for (int i = 0; i < abilities.Length; i++)
        {
            abilities[i] = ScriptableObject.CreateInstance<EnemyAbility>();
            abilities[i].effect = EnemyAbilityEffect.HealAfterPlayerCard;
            abilities[i].amount = 0;
            abilities[i].displayName = namesAndRules[i * 2];
            abilities[i].description = namesAndRules[i * 2 + 1];
            _created.Add(abilities[i]);
        }
        type.abilities = abilities;
        return type;
    }

    static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(target, null);

    EnemyRuntime[] StartScrappers(int count)
    {
        var type = Type("Scrapper", 3, 2);
        type.encounterCount = count;
        var enemies = Enumerable.Range(1, count).Select(i => new EnemyRuntime(type, i, count)).ToArray();
        _combat.StartEncounter(enemies);
        return enemies;
    }

    EnemyTypeData Type(string name, int health, int attack)
    {
        var type = EnemyTypeData.Create(name, health, attack, 3);
        _created.Add(type);
        return type;
    }

    EnemyDisplayUI CreateView(string name, EnemyRuntime enemy)
    {
        var root = Make(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(_canvas.transform, false);
        ((RectTransform)root.transform).sizeDelta = new Vector2(350, 384);
        var mount = Make("PortraitMount", typeof(RectTransform), typeof(Image));
        mount.transform.SetParent(root.transform, false);
        var window = Make("PortraitWindow", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        window.transform.SetParent(root.transform, false);
        var windowRect = (RectTransform)window.transform;
        windowRect.anchorMin = new Vector2(.065f, .31f);
        windowRect.anchorMax = new Vector2(.935f, .945f);
        windowRect.offsetMin = windowRect.offsetMax = Vector2.zero;
        var portrait = Make("EnemyPortrait", typeof(RectTransform), typeof(Image));
        portrait.transform.SetParent(window.transform, false);
        var display = root.AddComponent<EnemyDisplayUI>();
        display.portrait = portrait.GetComponent<Image>();
        display.nameText = Label("EnemyNameLabel", root.transform);
        display.hpText = Label("EnemyHealthLabel", root.transform);
        display.atkText = Label("EnemyAttackLabel", root.transform);
        display.statusText = Label("EnemyStateLabel", root.transform);
        display.Bind(enemy);
        return display;
    }

    TMP_Text Label(string name, Transform parent)
    {
        var item = Make(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);
        return item.GetComponent<TMP_Text>();
    }

    List<CardView> HandViews()
    {
        var result = new List<CardView>();
        for (int i = 0; i < _field.cardsHolder.childCount; i++)
            if (_field.cardsHolder.GetChild(i).TryGetComponent<CardView>(out var view)) result.Add(view);
        return result;
    }

    GameObject Make(string name, params System.Type[] components)
    {
        var item = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(item);
        return item;
    }
}
