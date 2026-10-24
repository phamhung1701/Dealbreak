using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The 2026-10-24 UI QA corrections: the 16:9 layout holds at other aspect ratios, details carry no
// fake scrollbar or repeated facts, and the header rows keep their pixel geometry.
public sealed class UiQaLayoutTests
{
    readonly List<Scene> _opened = new();
    readonly List<Object> _objects = new();

    Scene Open(string path)
    {
        var scene = SceneManager.GetSceneByPath(path);
        if (scene.IsValid() && scene.isLoaded) return scene;
        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        _opened.Add(scene);
        return scene;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var item in _objects) if (item) Object.DestroyImmediate(item);
        _objects.Clear();
        foreach (var scene in _opened) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        _opened.Clear();
    }

    [TestCase("Assets/Scenes/Game.unity")]
    [TestCase("Assets/Scenes/MainMenu.unity")]
    public void EveryCanvasOnlyGrowsPast1080p(string path)
    {
        var scalers = Open(path).GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CanvasScaler>(true)).ToList();
        Assert.That(scalers, Is.Not.Empty);
        foreach (var scaler in scalers)
        {
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize), scaler.name);
            Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand), scaler.name);
        }
    }

    [Test]
    public void DetailScrollbarOnlyShowsWhenTheDetailOverflows()
    {
        var tooltip = Open("Assets/Scenes/Game.unity").GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ContextualTooltipUI>(true)).Single();
        Assert.That(tooltip.scroll.verticalScrollbar, Is.Not.Null);
        Assert.That(tooltip.scroll.verticalScrollbarVisibility, Is.EqualTo(ScrollRect.ScrollbarVisibility.AutoHide));
    }

    [Test]
    public void HeaderRowsArePinnedAndSixTrickSlotsExist()
    {
        var root = Open("Assets/Scenes/Game.unity").GetRootGameObjects().First(g => g.name == "Canvas");
        var actions = root.GetComponent<ActionButtonsUI>();
        Assert.That(actions.backpackSlotButtons, Has.Length.EqualTo(6), "Cargo Case's five plus a character's extra slot.");
        Assert.That(actions.backpackSlotButtons.All(button => button && button.TryGetComponent<CanvasPin>(out _)), Is.True);
        Assert.That(root.GetComponentInChildren<GearRailUI>(true).TryGetComponent<CanvasPin>(out _), Is.True);
        var slot = actions.backpackSlotButtons[0].GetComponent<CanvasPin>();
        Assert.That(slot.size, Is.EqualTo(new Vector2(100, 100)), "Slots stay square.");
    }

    [Test]
    public void CanvasPin_KeepsItsReferenceBoxUnderAnyParent()
    {
        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); _objects.Add(canvasObject);
        var root = (RectTransform)canvasObject.transform;
        root.sizeDelta = new Vector2(2560, 1080);
        var parent = new GameObject("Area", typeof(RectTransform)).GetComponent<RectTransform>();
        parent.SetParent(root, false);
        parent.anchorMin = Vector2.zero; parent.anchorMax = new Vector2(1, .39f); parent.offsetMin = parent.offsetMax = Vector2.zero;
        var pinned = new GameObject("Slot", typeof(RectTransform), typeof(CanvasPin)).GetComponent<RectTransform>();
        pinned.SetParent(parent, false);
        var pin = pinned.GetComponent<CanvasPin>();
        pin.SetFromReference(1146, 18, 1246, 118);
        pin.Apply();

        var corners = new Vector3[4];
        pinned.GetWorldCorners(corners);
        Vector2 min = root.InverseTransformPoint(corners[0]), max = root.InverseTransformPoint(corners[2]);
        // At 2560 wide the box keeps its 100 px size and its offset from the top centre (x 1146 at 1920).
        Assert.That(max.x - min.x, Is.EqualTo(100).Within(.01f));
        Assert.That(max.y - min.y, Is.EqualTo(100).Within(.01f));
        Assert.That(min.x - root.rect.xMin, Is.EqualTo(1146 + (2560 - 1920) / 2f).Within(.01f));
        Assert.That(root.rect.yMax - max.y, Is.EqualTo(18).Within(.01f));
    }

    [Test]
    public void FrameHairlinesStayOneScreenPixelBelow1080p()
    {
        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); _objects.Add(canvasObject);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.scaleFactor = .5f;
        var frame = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorialFrame)).GetComponent<EditorialFrame>();
        frame.transform.SetParent(canvasObject.transform, false);
        frame.rectTransform.sizeDelta = new Vector2(200, 60);
        frame.border = 1;
        var method = typeof(EditorialFrame).GetMethod("ScreenPixelBorder", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That((float)method.Invoke(frame, null), Is.EqualTo(2f).Within(.001f), "One screen pixel at half scale is two canvas units.");
    }
}
