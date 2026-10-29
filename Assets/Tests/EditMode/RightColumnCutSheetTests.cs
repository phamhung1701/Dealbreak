using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD slice S11 (visual-design 3b, hud.md section C): DISCARD strip, drop-target crop marks, MAP button.
public sealed class RightColumnCutSheetTests
{
    readonly List<Object> _created = new();
    Canvas _canvas;

    [SetUp]
    public void SetUp()
    {
        CutSheetMotion.ReduceMotionOverride = null;
        var host = Create("Right Column Canvas", typeof(RectTransform), typeof(Canvas));
        _canvas = host.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ((RectTransform)host.transform).sizeDelta = new Vector2(1920f, 1080f);
    }

    [TearDown]
    public void TearDown()
    {
        CutSheetMotion.ReduceMotionOverride = null;
        var drag = ItemDragUI.Get();
        if (drag) Object.DestroyImmediate(drag.gameObject);
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void DiscardStrip_IsBrickPanelWithRotatedAntonLabelAndShadow6()
    {
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null);
        CutSheetMotion.ReduceMotionOverride = true;
        var drag = ItemDragUI.Get();

        drag.PresentDiscard(false);

        Assert.That(drag.DiscardStrip.gameObject.activeSelf, Is.True);
        Assert.That(drag.DiscardImage.sprite, Is.EqualTo(theme.panelSprite));
        Assert.That(drag.DiscardImage.type, Is.EqualTo(theme.panelSprite ? Image.Type.Sliced : Image.Type.Simple));
        Assert.That(drag.DiscardImage.color, Is.EqualTo(theme.brick));
        Assert.That(drag.DiscardLabel.text, Is.EqualTo("DISCARD"));
        Assert.That(drag.DiscardLabel.font, Is.EqualTo(theme.headingFont));
        Assert.That(drag.DiscardLabel.fontSize, Is.EqualTo(32f));
        Assert.That(drag.DiscardLabel.color, Is.EqualTo(theme.paper));
        Assert.That(drag.DiscardLabel.rectTransform.localEulerAngles.z, Is.EqualTo(90f).Within(.01f), "The label runs up the 90px strip.");
        Assert.That(drag.DiscardShadowView.Depth, Is.EqualTo(6f));
        Assert.That(drag.DiscardShadowView.transform.GetSiblingIndex(), Is.LessThan(drag.DiscardFace.GetSiblingIndex()), "Shadow under the face.");
    }

    [Test]
    public void DiscardStrip_HoveredGetsThickMarksAndLifts4px()
    {
        CutSheetMotion.ReduceMotionOverride = true;
        var drag = ItemDragUI.Get();

        drag.PresentDiscard(false);
        Assert.That(drag.DiscardMarks.Current, Is.EqualTo(CropMarksUI.State.Thin));
        Assert.That(drag.DiscardFace.offsetMin, Is.EqualTo(Vector2.zero));

        drag.PresentDiscard(true);
        Assert.That(drag.DiscardMarks.Current, Is.EqualTo(CropMarksUI.State.Thick));
        Assert.That(drag.DiscardFace.offsetMin, Is.EqualTo(new Vector2(0f, 4f)));
        Assert.That(drag.DiscardFace.offsetMax, Is.EqualTo(new Vector2(0f, 4f)));

        drag.HideDiscard();
        Assert.That(drag.DiscardStrip.gameObject.activeSelf, Is.False);
        Assert.That(drag.DiscardMarks.IsShown, Is.False);
    }

    [Test]
    public void DiscardStrip_SlidesInFromTheRight_ButNotUnderReduceMotion()
    {
        CutSheetMotion.ReduceMotionOverride = false;
        var drag = ItemDragUI.Get();
        drag.PresentDiscard(false);
        Assert.That(drag.DiscardSlideOffset, Is.GreaterThan(0f), "The strip starts past the right edge.");
        drag.HideDiscard();

        CutSheetMotion.ReduceMotionOverride = true;
        drag.PresentDiscard(false);
        Assert.That(drag.DiscardSlideOffset, Is.Zero, "Reduce Motion: no slide.");
    }

    [Test]
    public void TargetMarks_LegalTargetsThin_IllegalNone_HoveredThickAndLifted()
    {
        var layer = Rect("Layer", _canvas.transform, Vector2.zero, new Vector2(1920f, 1080f));
        var marks = DropTargetMarksUI.Create(layer);
        var legal = Rect("Legal", _canvas.transform, new Vector2(-300f, 0f), new Vector2(100f, 100f));
        var hovered = Rect("Hovered", _canvas.transform, new Vector2(0f, 0f), new Vector2(100f, 100f));
        var illegal = Rect("Illegal", _canvas.transform, new Vector2(300f, 0f), new Vector2(100f, 100f));
        var face = Rect("Face", hovered, Vector2.zero, new Vector2(100f, 100f));
        var rest = face.anchoredPosition;

        marks.Begin();
        marks.Mark(legal, true, false);
        marks.Mark(hovered, true, true, face);
        marks.Mark(illegal, false, false);
        marks.End();

        Assert.That(marks.MarksFor(legal).Current, Is.EqualTo(CropMarksUI.State.Thin));
        Assert.That(marks.MarksFor(hovered).Current, Is.EqualTo(CropMarksUI.State.Thick));
        Assert.That(marks.MarksFor(illegal), Is.Null, "An illegal target gets nothing.");
        Assert.That(marks.ShownCount, Is.EqualTo(2));
        Assert.That(face.anchoredPosition - rest, Is.EqualTo(new Vector2(0f, 4f)), "The hovered target's face lifts 4px.");
        Assert.That(hovered.anchoredPosition, Is.EqualTo(Vector2.zero), "Its root (layout-owned) never moves.");
        var theme = DealbreakUIStyle.Theme;
        if (theme) Assert.That(marks.MarksFor(legal).Mark(0).color, Is.EqualTo(theme.ochre), "Teal marks.");

        marks.Begin();
        marks.Mark(legal, true, true);
        marks.Mark(hovered, true, false);
        marks.End();
        Assert.That(face.anchoredPosition, Is.EqualTo(rest), "Leaving drops the lift.");
        Assert.That(marks.MarksFor(hovered).Current, Is.EqualTo(CropMarksUI.State.Thin));

        marks.Clear();
        Assert.That(marks.ShownCount, Is.Zero);
        Assert.That(legal.anchoredPosition, Is.EqualTo(new Vector2(-300f, 0f)));
    }

    [Test]
    public void TargetMarks_LayoutRebuildDuringHover_LeavesTheSlotAtRestAfterTheDrop()
    {
        var layer = Rect("Layer", _canvas.transform, Vector2.zero, new Vector2(1920f, 1080f));
        var marks = DropTargetMarksUI.Create(layer);
        var row = Rect("Gear Row", _canvas.transform, Vector2.zero, new Vector2(400f, 100f));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var slots = new RectTransform[3];
        var faces = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            slots[i] = Rect($"Slot {i}", row, Vector2.zero, new Vector2(100f, 100f));
            faces[i] = Rect("Face", slots[i], Vector2.zero, new Vector2(100f, 100f));
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        var slotRest = slots[1].anchoredPosition;
        var faceRest = faces[1].anchoredPosition;

        marks.Begin();
        for (int i = 0; i < 3; i++) marks.Mark(slots[i], true, i == 1, faces[i]);
        marks.End();
        Assert.That(faces[1].anchoredPosition - faceRest, Is.EqualTo(new Vector2(0f, 4f)), "Hover raises the face.");

        // A rebuild during hover (a slot added or resized) re-places every slot root.
        LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        Assert.That(slots[1].anchoredPosition, Is.EqualTo(slotRest), "The layout-owned root was never lifted.");
        Assert.That(faces[1].anchoredPosition - faceRest, Is.EqualTo(new Vector2(0f, 4f)), "The lift survives the rebuild.");

        marks.Clear();
        LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        Assert.That(slots[1].anchoredPosition, Is.EqualTo(slotRest), "The slot is back at rest after the drop.");
        Assert.That(faces[1].anchoredPosition, Is.EqualTo(faceRest), "The face is back at rest after the drop.");
    }

    [Test]
    public void TargetMarks_LiftedTargetKeepsItsRestRectForHitTesting()
    {
        var layer = Rect("Layer", _canvas.transform, Vector2.zero, new Vector2(1920f, 1080f));
        var marks = DropTargetMarksUI.Create(layer);
        var slot = Rect("Slot", _canvas.transform, Vector2.zero, new Vector2(100f, 100f));
        var face = Rect("Face", slot, Vector2.zero, new Vector2(100f, 100f));
        marks.Begin();
        marks.Mark(slot, true, true, face);
        marks.End();

        // 2px above the slot's rest bottom edge: inside the rest rect, under the raised face's bottom.
        var corners = new Vector3[4];
        slot.GetWorldCorners(corners);
        var nearBottom = new Vector2((corners[0].x + corners[2].x) * .5f, corners[0].y + 2f);

        Assert.That(marks.LiftedTarget, Is.EqualTo(slot));
        Assert.That(marks.RestContains(nearBottom), Is.True);
    }

    [Test]
    public void TargetMarks_HandCardHoverThickensWithoutMovingTheCard()
    {
        var layer = Rect("Layer", _canvas.transform, Vector2.zero, new Vector2(1920f, 1080f));
        var marks = DropTargetMarksUI.Create(layer);
        var card = Rect("Card", _canvas.transform, Vector2.zero, new Vector2(100f, 140f));

        marks.Begin();
        marks.Mark(card, true, true);
        marks.End();

        Assert.That(marks.MarksFor(card).Current, Is.EqualTo(CropMarksUI.State.Thick));
        Assert.That(card.anchoredPosition, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void MapButton_UsesTheCutSheetPaperRecipeWithBarlow22Ink()
    {
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null);
        var button = Create("MapVisibilityButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        button.transform.SetParent(_canvas.transform, false);
        ((RectTransform)button.transform).sizeDelta = PathScreenUI.MapButtonSize;
        var label = Create("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(button.transform, false);
        label.GetComponent<TMP_Text>().text = "CLOSE MAP";

        PathScreenUI.StyleMapButton(button.GetComponent<Button>());

        Assert.That(button.TryGetComponent(out EditorialButton style), Is.True);
        Assert.That(style.cutSheet, Is.True);
        Assert.That(style.cutSheetRole, Is.EqualTo(EditorialButton.Role.Paper));
        Assert.That(style.restShadow, Is.EqualTo(CutSheetMetrics.ShadowButton));
        var text = button.GetComponentInChildren<TMP_Text>(true);
        Assert.That(text.font, Is.EqualTo(theme.labelFont ? theme.labelFont : theme.headingFont));
        Assert.That(text.fontSize, Is.EqualTo(22f));
        Assert.That(text.color, Is.EqualTo(theme.inkPrint));
    }

    [Test]
    public void MapButton_LabelReadsInkOverThePaperFace_AfterTheHudTextPassTintsItPaper()
    {
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null);
        var button = Create("MapVisibilityButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        button.transform.SetParent(_canvas.transform, false);
        var label = Create("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.transform.SetParent(button.transform, false);
        label.text = "MAP";
        PathScreenUI.StyleMapButton(button.GetComponent<Button>());
        button.GetComponent<EditorialButton>().Refresh();

        // As captured: the HUD's hierarchy text pass leaves the label paper (blank on the paper face)
        // with another face's material; the button's styler has already cached its label colour.
        label.color = theme.paper;
        if (theme.headingFont) label.fontSharedMaterial = theme.headingFont.material;
        label.transform.SetAsFirstSibling();
        button.GetComponent<EditorialButton>().Refresh();
        PathScreenUI.StyleMapLabel(label);

        Assert.That(label.text, Is.EqualTo("MAP"));
        Assert.That(label.enabled && label.gameObject.activeInHierarchy, Is.True);
        Assert.That(label.color, Is.EqualTo(theme.inkPrint), "Ink on the paper face.");
        Assert.That(label.font, Is.EqualTo(theme.labelFont ? theme.labelFont : theme.headingFont));
        Assert.That(label.fontSharedMaterial, Is.EqualTo(label.font.material));
        Assert.That(label.fontSize, Is.EqualTo(22f));
        Assert.That(label.transform.GetSiblingIndex(), Is.EqualTo(button.transform.childCount - 1), "Drawn over the face.");
        Assert.That(label.color, Is.Not.EqualTo(theme.paper));
    }

    RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)Create(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    GameObject Create(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        _created.Add(go);
        return go;
    }
}
