using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD slice S12 (visual-design 3b, hud.md): the shared contextual tooltip in the Cut Sheet look.
public sealed class ContextualTooltipCutSheetTests
{
    GameObject _root;
    RectTransform _stage;
    ContextualTooltipUI _tooltip;
    int _savedTextSize;
    DealbreakUITheme Theme => DealbreakUIStyle.Theme;

    [SetUp]
    public void SetUp()
    {
        Assume.That(DealbreakUIStyle.Theme, Is.Not.Null);
        CutSheetMotion.ReduceMotionOverride = true;
        _savedTextSize = DisplaySettingsService.TooltipTextSizeIndex;
        DisplaySettingsService.TooltipTextSizeIndex = 0;
        _root = new GameObject("S12 Canvas", typeof(RectTransform), typeof(Canvas));
        _root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        _stage = Rect("S12 Stage", _root.transform, new Vector2(1920, 1080));

        var panel = Rect("S12 Panel", _stage, new Vector2(360, 200));
        var title = Rect("S12 Title", panel, new Vector2(300, 40)).gameObject.AddComponent<TextMeshProUGUI>();
        var scrollRect = Rect("S12 Scroll", panel, Vector2.zero);
        scrollRect.anchorMin = Vector2.zero; scrollRect.anchorMax = Vector2.one;
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        var viewport = Rect("S12 Viewport", scrollRect, Vector2.zero);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        var body = Rect("S12 Body", viewport, new Vector2(300, 100)).gameObject.AddComponent<TextMeshProUGUI>();
        scroll.viewport = viewport; scroll.content = body.rectTransform;
        var bar = Rect("Detail Scrollbar", panel, new Vector2(8, 100));
        bar.gameObject.AddComponent<Image>();
        var handle = Rect("Handle", bar, Vector2.zero);
        handle.gameObject.AddComponent<Image>();
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        panel.gameObject.SetActive(false);

        _tooltip = _root.AddComponent<ContextualTooltipUI>();
        _tooltip.panel = panel.gameObject; _tooltip.title = title; _tooltip.body = body; _tooltip.scroll = scroll;
        Invoke("OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        if (_tooltip) Invoke("OnDisable");
        if (_root) Object.DestroyImmediate(_root);
        CutSheetMotion.ReduceMotionOverride = null;
        DisplaySettingsService.TooltipTextSizeIndex = _savedTextSize;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        return rect;
    }

    void Invoke(string method) =>
        typeof(ContextualTooltipUI).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_tooltip, null);

    ContextualInfoTrigger Source(float x, float y = 0, string title = "Scanner", string body = "Deals 12 damage.")
    {
        var rect = Rect("S12 Source", _stage, new Vector2(100, 100));
        rect.anchoredPosition = new Vector2(x, y);
        var trigger = rect.gameObject.AddComponent<ContextualInfoTrigger>();
        trigger.category = "Gear"; trigger.title = title; trigger.description = body;
        return trigger;
    }

    void Open(ContextualInfoTrigger source)
    {
        _tooltip.Pin(source);
        Invoke("LateUpdate");
        Assert.That(_tooltip.panel.activeSelf, Is.True, "The pinned detail opens.");
    }

    RectTransform Panel => (RectTransform)_tooltip.panel.transform;

    [Test]
    public void Panel_IsSlicedPaperOverASixPixelInkShadow()
    {
        Open(Source(-700));
        Assert.That(_tooltip.Face.sprite, Is.SameAs(Theme.panelSprite));
        Assert.That(_tooltip.Face.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(_tooltip.Face.color, Is.EqualTo(Theme.paper));
        Assert.That(_tooltip.Shadow.Image.color, Is.EqualTo(Theme.inkPrint));
        Assert.That(_tooltip.Shadow.Depth, Is.EqualTo(CutSheetMetrics.ShadowPanel));
        Assert.That(_tooltip.Shadow.transform.GetSiblingIndex(), Is.LessThan(_tooltip.Face.transform.GetSiblingIndex()));
    }

    [Test]
    public void Notch_SitsUnderThePanel_AndPointsLeftAtASourceOnTheLeft()
    {
        Open(Source(-700));
        Assert.That(_tooltip.Notch, Is.Not.Null);
        Assert.That(_tooltip.Notch.GetSiblingIndex(), Is.LessThan(_tooltip.Face.transform.GetSiblingIndex()), "Drawn under the face.");
        Assert.That(_tooltip.Notch.localEulerAngles.z, Is.EqualTo(45).Within(.01f));
        Assert.That(_tooltip.NotchDirection, Is.EqualTo(Vector2.left));
        Assert.That(_tooltip.Notch.anchoredPosition.x, Is.EqualTo(-Panel.sizeDelta.x * .5f).Within(.01f), "On the left edge.");
    }

    [Test]
    public void Notch_PointsRightAtASourceOnTheRightEdge()
    {
        Open(Source(850));
        Assert.That(_tooltip.NotchDirection, Is.EqualTo(Vector2.right));
        Assert.That(_tooltip.Notch.anchoredPosition.x, Is.EqualTo(Panel.sizeDelta.x * .5f).Within(.01f));
    }

    [Test]
    public void NotchPlacement_PointsUpAndDownAtSourcesAboveAndBelow()
    {
        var target = new Rect(-50, -50, 100, 100);
        var below = ContextualTooltipUI.NotchPlacement(new Rect(-180, -262, 360, 200), target, out var up);
        Assert.That(up, Is.EqualTo(Vector2.up), "A panel under its source points up.");
        Assert.That(below, Is.EqualTo(new Vector2(0, 100)));
        var above = ContextualTooltipUI.NotchPlacement(new Rect(-100, 62, 360, 200), target, out var down);
        Assert.That(down, Is.EqualTo(Vector2.down), "A panel over its source points down.");
        Assert.That(above.y, Is.EqualTo(-100));
        Assert.That(above.x, Is.EqualTo(-80), "Slid along the edge to sit under the source centre.");
    }

    [Test]
    public void Type_IsZillaBodyAndBarlowHeading_InInk()
    {
        Open(Source(-700));
        Assert.That(_tooltip.body.font, Is.SameAs(Theme.bodyFont));
        Assert.That(_tooltip.body.fontSize, Is.EqualTo(20));
        Assert.That(_tooltip.body.color, Is.EqualTo(Theme.inkPrint));
        Assert.That(_tooltip.title.gameObject.activeSelf, Is.True);
        Assert.That(_tooltip.title.font, Is.SameAs(Theme.labelFont ? Theme.labelFont : Theme.headingFont));
        Assert.That(_tooltip.title.fontSize, Is.EqualTo(22));
        Assert.That(_tooltip.title.color, Is.EqualTo(Theme.inkPrint));
    }

    [Test]
    public void Heading_IsHiddenWhenTheSourceGivesNone()
    {
        var source = Source(-700);
        source.describe = () => new ContextualInfo(null, null, "Hand 3/8");
        Open(source);
        Assert.That(_tooltip.title.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void BodyDigits_PrintInCourierThroughTheFontTag()
    {
        Assume.That(Theme.numericFont, Is.Not.Null);
        Open(Source(-700, 0, "Scanner", "<b>Deals</b> 12 damage."));
        Assert.That(_tooltip.body.text, Does.Contain("<font=\"" + Theme.numericFont.name + "\">12</font>"));
        Assert.That(_tooltip.body.text, Does.Contain("<b>Deals</b>"), "Tags stay intact.");
    }

    [Test]
    public void Width_IsAtMost360_AndGrowsWithTextScale()
    {
        var source = Source(-700);
        Open(source);
        Assert.That(Panel.sizeDelta.x, Is.LessThanOrEqualTo(360));
        DisplaySettingsService.TooltipTextSizeIndex = 2;
        Invoke("LateUpdate");
        Assert.That(Panel.sizeDelta.x, Is.EqualTo(360 * 1.3f).Within(.01f));
        Assert.That(_tooltip.body.fontSize, Is.EqualTo(20 * 1.3f).Within(.01f));
        Assert.That(_tooltip.title.fontSize, Is.EqualTo(22 * 1.3f).Within(.01f));
    }

    // HUD slice S20: the Tooltip text size setting (100/115/130%) drives the type and the width.
    [TestCase(1, 1.15f)]
    [TestCase(2, 1.3f)]
    public void TextSizeSetting_ScalesTypeAndMaxWidth(int index, float factor)
    {
        DisplaySettingsService.TooltipTextSizeIndex = index;
        Open(Source(-700));
        Assert.That(ContextualTooltipUI.TextScale, Is.EqualTo(factor).Within(1e-4f));
        Assert.That(ContextualTooltipUI.MaxWidth, Is.EqualTo(360 * factor).Within(.01f));
        Assert.That(Panel.sizeDelta.x, Is.EqualTo(360 * factor).Within(.01f));
        Assert.That(_tooltip.body.fontSize, Is.EqualTo(20 * factor).Within(.01f));
        Assert.That(_tooltip.title.fontSize, Is.EqualTo(22 * factor).Within(.01f));
    }

    [Test]
    public void TextSizeChange_WhileOpen_RelaysOutAtOnce()
    {
        Open(Source(-700));
        Assert.That(_tooltip.body.fontSize, Is.EqualTo(20));

        DisplaySettingsService.TooltipTextSizeIndex = 2;

        Assert.That(_tooltip.body.fontSize, Is.EqualTo(26).Within(.01f), "No frame needed: the change event re-lays it out.");
        Assert.That(_tooltip.title.fontSize, Is.EqualTo(22 * 1.3f).Within(.01f));
        Assert.That(Panel.sizeDelta.x, Is.EqualTo(360 * 1.3f).Within(.01f));
        Assert.That(_tooltip.NotchDirection, Is.EqualTo(Vector2.left), "Still opens next to the source.");
    }

    [Test]
    public void LongBody_At130Percent_StaysOnScreenAndScrolls()
    {
        DisplaySettingsService.TooltipTextSizeIndex = 2;
        string longBody = string.Concat(System.Linq.Enumerable.Repeat("Deals 12 damage to the target and draws a card when it kills. ", 60));
        Open(Source(-700, 0, "Scanner", longBody));

        Assert.That(_tooltip.body.fontSize, Is.EqualTo(26).Within(.01f));
        float half = Panel.sizeDelta.y * .5f;
        Assert.That(Panel.sizeDelta.y, Is.LessThanOrEqualTo(_stage.rect.height));
        Assert.That(_tooltip.RestPosition.y + half, Is.LessThanOrEqualTo(_stage.rect.yMax));
        Assert.That(_tooltip.RestPosition.y - half, Is.GreaterThanOrEqualTo(_stage.rect.yMin));
        Assert.That(_tooltip.scroll.vertical, Is.True);
        Assert.That(_tooltip.body.rectTransform.rect.height, Is.GreaterThan(_tooltip.scroll.viewport.rect.height),
            "The body is taller than its viewport, so it scrolls.");
    }

    [Test]
    public void ReduceMotion_OpensAtRestWithTheFullShadow()
    {
        Open(Source(-700));
        Assert.That(Panel.anchoredPosition, Is.EqualTo(_tooltip.RestPosition));
        Assert.That(_tooltip.panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
        Assert.That(_tooltip.Shadow.Depth, Is.EqualTo(CutSheetMetrics.ShadowPanel));
    }

    [Test]
    public void Motion_OpensSixPixelsTowardTheSourceWithNoShadow()
    {
        CutSheetMotion.ReduceMotionOverride = false;
        Open(Source(-700));
        Assert.That(_tooltip.Shadow.Depth, Is.EqualTo(0));
        Assert.That(Panel.anchoredPosition, Is.EqualTo(_tooltip.RestPosition + Vector2.left * CutSheetMetrics.TooltipSlide));
        Assert.That(Panel.localEulerAngles.z, Is.EqualTo(0), "The tooltip never tilts.");
    }

    [Test]
    public void ReduceMotion_ClosesInstantly()
    {
        var source = Source(-700);
        Open(source);
        _tooltip.Pin(source);
        Invoke("LateUpdate");
        Assert.That(_tooltip.panel.activeSelf, Is.False);
    }

    [Test]
    public void Closing_StopsBlockingClicks_AndReopeningRestoresThem()
    {
        CutSheetMotion.ReduceMotionOverride = false;
        var source = Source(-700);
        Open(source);
        var group = _tooltip.panel.GetComponent<CanvasGroup>();
        Assert.That(group.blocksRaycasts, Is.True);

        _tooltip.Pin(source);
        Invoke("LateUpdate");

        Assert.That(_tooltip.panel.activeSelf, Is.True, "Still fading out.");
        Assert.That(_tooltip.IsShown, Is.False);
        Assert.That(group.blocksRaycasts, Is.False, "A click passes through the closing panel.");

        _tooltip.Pin(source);
        Invoke("LateUpdate");

        Assert.That(_tooltip.IsShown, Is.True);
        Assert.That(group.blocksRaycasts, Is.True);
    }

    [Test]
    public void SortingOrderStays90()
    {
        Open(Source(-700));
        var layer = _tooltip.panel.GetComponent<Canvas>();
        Assert.That(layer.overrideSorting, Is.True, "Set after the panel is active, so Unity keeps it.");
        Assert.That(layer.sortingOrder, Is.EqualTo(90));
    }

    [Test]
    public void Scrollbar_IsLinkedAndShowsOnlyOnOverflow()
    {
        Assert.That(_tooltip.scroll.verticalScrollbar, Is.Not.Null);
        Assert.That(_tooltip.scroll.verticalScrollbarVisibility, Is.EqualTo(ScrollRect.ScrollbarVisibility.AutoHide));
    }
}
