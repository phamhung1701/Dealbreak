using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// HUD slice S10b (visual-design 3a/3b): the action bar's Cut Sheet sprite recipe in EditorialButton.
public sealed class ActionBarCutSheetTests
{
    GameObject _root;
    DealbreakUITheme Theme => DealbreakUIStyle.Theme;

    [SetUp]
    public void SetUp()
    {
        Assume.That(DealbreakUIStyle.Theme, Is.Not.Null);
        CutSheetMotion.ReduceMotionOverride = true;
        _root = new GameObject("Action Bar Test", typeof(RectTransform), typeof(Canvas));
    }

    [TearDown]
    public void TearDown()
    {
        CutSheetMotion.ReduceMotionOverride = null;
        if (_root) Object.DestroyImmediate(_root);
    }

    (Button button, EditorialButton style, TMP_Text label) Make(string name, EditorialButton.Role role, float shadow, bool sort = false)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        item.transform.SetParent(_root.transform, false);
        ((RectTransform)item.transform).sizeDelta = new Vector2(240, 66);
        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(item.transform, false);
        var label = text.GetComponent<TMP_Text>();
        label.text = name;
        var button = item.GetComponent<Button>();
        DealbreakUIStyle.CutSheetButton(button, role, shadow, sort ? Theme.labelFont : Theme.headingFont, sort ? 22 : 32, sort);
        Assert.That(item.TryGetComponent<EditorialButton>(out var style), Is.True);
        return (button, style, label);
    }

    static PointerEventData Left() => new(null) { button = PointerEventData.InputButton.Left };

    [Test]
    public void Attack_IsABrickPanelWithPaperAnton32AndA4pxShadow()
    {
        var (_, style, label) = Make("PlayButton", EditorialButton.Role.Primary, CutSheetMetrics.ShadowButton);
        Assert.That(style.cutSheet, Is.True);
        Assert.That(style.frame.spriteMode, Is.True);
        Assert.That(style.frame.SpriteFace.sprite, Is.SameAs(Theme.panelSprite));
        Assert.That(style.frame.SpriteFace.type, Is.EqualTo(Image.Type.Sliced), "Sliced on the 4px border: no tile seams.");
        Assert.That(style.frame.SpriteShadow.Image.type, Is.EqualTo(Image.Type.Sliced), "The shadow matches the face.");
        Assert.That(style.frame.SpriteFace.color, Is.EqualTo(Theme.brick));
        Assert.That(label.color, Is.EqualTo(Theme.paper));
        Assert.That(label.font, Is.SameAs(Theme.headingFont));
        Assert.That(label.fontSize, Is.EqualTo(32f));
        Assert.That(style.frame.SpriteShadow.Depth, Is.EqualTo(CutSheetMetrics.ShadowButton));
        Assert.That(style.VisibleShadowDepth, Is.EqualTo(4f));
    }

    [Test]
    public void BlockAndPass_ArePaperPanelsWithInkType()
    {
        foreach (var name in new[] { "BlockButton", "PassButton" })
        {
            var (_, style, label) = Make(name, EditorialButton.Role.Paper, CutSheetMetrics.ShadowButton);
            Assert.That(style.frame.SpriteFace.color, Is.EqualTo(Theme.paper), name);
            Assert.That(style.frame.SpriteFace.color, Is.Not.EqualTo(Theme.brick), name + " is never red.");
            Assert.That(label.color, Is.EqualTo(Theme.inkPrint), name);
            Assert.That(label.fontSize, Is.EqualTo(32f), name);
        }
    }

    [Test]
    public void Disabled_FillsTowardTheBoardWithHatchAndNoShadow()
    {
        var (attack, attackStyle, attackLabel) = Make("PlayButton", EditorialButton.Role.Primary, CutSheetMetrics.ShadowButton);
        attack.interactable = false;
        attackStyle.Refresh();
        Assert.That(attackStyle.frame.SpriteFace.color, Is.EqualTo(EditorialButton.DisabledRed));
        Assert.That((Color32)EditorialButton.DisabledRed, Is.EqualTo(new Color32(0x72, 0x2A, 0x22, 0xFF)));
        Assert.That(attackLabel.color, Is.EqualTo(Theme.paper), "Paper on the disabled red.");
        Assert.That(attackStyle.HatchShown, Is.True);
        Assert.That(attackStyle.VisibleShadowDepth, Is.EqualTo(0f));

        var (block, blockStyle, blockLabel) = Make("BlockButton", EditorialButton.Role.Paper, CutSheetMetrics.ShadowButton);
        block.interactable = false;
        blockStyle.Refresh();
        Assert.That(blockStyle.frame.SpriteFace.color, Is.EqualTo(Theme.muted));
        Assert.That(blockLabel.color, Is.EqualTo(Theme.inkPrint));
        Assert.That(blockStyle.HatchShown, Is.True);
        Assert.That(blockStyle.VisibleShadowDepth, Is.EqualTo(0f));

        block.interactable = true;
        blockStyle.Refresh();
        Assert.That(blockStyle.HatchShown, Is.False, "The hatch leaves when the button is available again.");
        Assert.That(blockStyle.VisibleShadowDepth, Is.EqualTo(4f));
    }

    [Test]
    public void Pressed_MovesTheFaceDownRightWhileTheShadowStaysPut()
    {
        var (button, style, _) = Make("PlayButton", EditorialButton.Role.Primary, CutSheetMetrics.ShadowButton);
        var shadowRect = (RectTransform)style.frame.SpriteShadow.transform;
        var shadowRest = shadowRect.offsetMin;
        var rootRest = ((RectTransform)button.transform).anchoredPosition;
        var frameRest = style.frame.rectTransform.anchoredPosition;

        style.OnPointerDown(Left());
        style.Refresh();

        Assert.That(style.FaceOffset, Is.EqualTo(CutSheetMetrics.PressedFaceOffset));
        Assert.That(style.frame.SpriteFace.rectTransform.offsetMin, Is.EqualTo(new Vector2(4, -4)));
        Assert.That(style.VisibleShadowDepth, Is.EqualTo(0f));
        Assert.That(shadowRect.offsetMin, Is.EqualTo(shadowRest), "The shadow does not move.");
        Assert.That(((RectTransform)button.transform).anchoredPosition, Is.EqualTo(rootRest));
        Assert.That(style.frame.rectTransform.anchoredPosition, Is.EqualTo(frameRest), "The frame (hit area) does not move.");

        style.OnPointerUp(Left());
        style.Refresh();
        Assert.That(style.FaceOffset, Is.EqualTo(Vector2.zero));
        Assert.That(style.VisibleShadowDepth, Is.EqualTo(4f));
    }

    [Test]
    public void Hover_LiftsTheFaceUpLeftSoTheShadowReads6()
    {
        var (_, style, label) = Make("BlockButton", EditorialButton.Role.Paper, CutSheetMetrics.ShadowButton);
        var face = style.frame.SpriteFace.color;
        style.OnPointerEnter(Left());
        style.Refresh();
        Assert.That(style.FaceOffset, Is.EqualTo(CutSheetMetrics.HoverFaceOffset));
        Assert.That(style.VisibleShadowDepth, Is.EqualTo(6f));
        Assert.That(style.frame.SpriteFace.color, Is.EqualTo(face), "Hover changes no tint.");
        Assert.That(label.color, Is.EqualTo(Theme.inkPrint));
    }

    [Test]
    public void Focus_ShowsThePaperRing()
    {
        var (_, style, _) = Make("PassButton", EditorialButton.Role.Paper, CutSheetMetrics.ShadowButton);
        Assert.That(style.FocusRingShown, Is.False);
        style.OnSelect(null);
        style.Refresh();
        Assert.That(style.FocusRingShown, Is.True);
        style.OnDeselect(null);
        style.Refresh();
        Assert.That(style.FocusRingShown, Is.False);
    }

    [Test]
    public void LatchedSort_IsDownWithAnInkFacePaperLabelAndNoShadow()
    {
        var (_, style, label) = Make("RankSortButton", EditorialButton.Role.Paper, CutSheetMetrics.ShadowSlot, true);
        Assert.That(style.frame.SpriteFace.color, Is.EqualTo(Theme.paper));
        Assert.That(style.VisibleShadowDepth, Is.EqualTo(3f));
        Assert.That(label.fontSize, Is.EqualTo(22f));

        style.selected = true;
        style.Refresh();
        Assert.That(style.frame.SpriteFace.color, Is.EqualTo(Theme.inkPrint));
        Assert.That(label.color, Is.EqualTo(Theme.paper));
        Assert.That(style.FaceOffset, Is.EqualTo(CutSheetMetrics.PressedFaceOffset));
        Assert.That(style.VisibleShadowDepth, Is.EqualTo(0f));
    }

    [Test]
    public void WithoutReduceMotion_ThePressTweens_WithItTheStateSnaps()
    {
        CutSheetMotion.ReduceMotionOverride = false;
        var (_, style, _) = Make("PlayButton", EditorialButton.Role.Primary, CutSheetMetrics.ShadowButton);
        style.OnPointerDown(Left());
        style.Refresh();
        Assert.That(style.FaceOffset, Is.Not.EqualTo(CutSheetMetrics.PressedFaceOffset), "The press starts a 60ms tween.");

        CutSheetMotion.ReduceMotionOverride = true;
        var (_, snapped, _) = Make("BlockButton", EditorialButton.Role.Paper, CutSheetMetrics.ShadowButton);
        snapped.OnPointerDown(Left());
        snapped.Refresh();
        Assert.That(snapped.FaceOffset, Is.EqualTo(CutSheetMetrics.PressedFaceOffset), "Reduce Motion snaps.");
    }

    [Test]
    public void ActionBarBacking_StopsDrawingButKeepsItsRect()
    {
        var bar = new GameObject("ActionBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(_root.transform, false);
        var image = bar.GetComponent<Image>();
        image.color = Theme.charcoal;
        var frame = DealbreakUIStyle.Frame(bar.transform, false, true);
        var rect = (RectTransform)bar.transform;
        var anchors = (rect.anchorMin, rect.anchorMax);
        bool raycast = image.raycastTarget;

        ActionBarLayout.RetireBacking(rect);

        Assert.That(image.color.a, Is.EqualTo(0f));
        Assert.That(image.raycastTarget, Is.EqualTo(raycast));
        Assert.That(frame.enabled, Is.False);
        Assert.That((rect.anchorMin, rect.anchorMax), Is.EqualTo(anchors));
    }

    [Test]
    public void LegacyButtons_KeepTheFlatFrame()
    {
        var item = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
        item.transform.SetParent(_root.transform, false);
        DealbreakUIStyle.Button(item.GetComponent<Button>(), EditorialButton.Role.Paper);
        Assert.That(item.TryGetComponent<EditorialButton>(out var style), Is.True);
        Assert.That(style.cutSheet, Is.False);
        Assert.That(style.frame.spriteMode, Is.False);
    }
}
