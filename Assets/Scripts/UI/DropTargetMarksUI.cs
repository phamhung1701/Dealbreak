using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drop-target marks while an item is held (HUD slice S11, visual-design 3b): thin teal crop marks
/// framing every legal target, thick marks plus a 4px lift on the hovered legal one, nothing on an
/// illegal target. The marks live on the drag layer (so they draw over the HUD and never enter the
/// targets' layout groups), each holder covering its target's bounds. Pooled; a frame is
/// <see cref="Begin"/>, one <see cref="Mark"/> per candidate, <see cref="End"/>. Presentation only:
/// legality comes from the caller (ItemDropRules).
/// </summary>
public sealed class DropTargetMarksUI : MonoBehaviour
{
    public const float HoverLift = 4f;

    readonly List<CropMarksUI> _pool = new();
    readonly List<RectTransform> _covered = new();
    readonly Vector3[] _corners = new Vector3[4];
    int _used;
    RectTransform _lifted, _liftedTarget, _hoveredThisFrame;
    Vector2 _liftRest;

    public int ShownCount => _used;
    /// <summary>The visual raised 4px (a child of the hovered target, never its layout-driven root).</summary>
    public RectTransform Lifted => _lifted;
    /// <summary>The target whose visual is raised.</summary>
    public RectTransform LiftedTarget => _liftedTarget;

    public static DropTargetMarksUI Create(RectTransform layer)
    {
        if (!layer) return null;
        var existing = layer.Find("Drop Target Marks");
        RectTransform rect;
        if (existing) rect = (RectTransform)existing;
        else
        {
            var item = new GameObject("Drop Target Marks", typeof(RectTransform));
            item.layer = layer.gameObject.layer;
            rect = (RectTransform)item.transform;
            rect.SetParent(layer, false);
        }
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (!rect.TryGetComponent(out DropTargetMarksUI marks)) marks = rect.gameObject.AddComponent<DropTargetMarksUI>();
        return marks;
    }

    public void Begin()
    {
        _used = 0;
        _hoveredThisFrame = null;
    }

    /// <summary>
    /// Frames <paramref name="target"/> if <paramref name="legal"/>: thin, or thick when hovered. A hovered
    /// target raises <paramref name="liftVisual"/> 4px: a child visual (a slot's Face, an enemy's portrait),
    /// so a layout group that owns the target's root never discards or doubles the lift, and the target's
    /// hit area stays put. Null lifts nothing (a hand card lifts itself).
    /// </summary>
    public void Mark(RectTransform target, bool legal, bool hovered, RectTransform liftVisual = null)
    {
        if (!legal || !target || !target.gameObject.activeInHierarchy) return;
        if (hovered && liftVisual) { Lift(liftVisual, target); _hoveredThisFrame = liftVisual; }
        var marks = Holder(_used);
        _covered[_used] = target;
        _used++;
        Cover((RectTransform)marks.transform, target);
        if (!marks.gameObject.activeSelf) marks.gameObject.SetActive(true);
        bool appeared = marks.Current == CropMarksUI.State.Hidden;
        marks.Set(hovered ? CropMarksUI.State.Thick : CropMarksUI.State.Thin);
        if (appeared) marks.Hop();
    }

    public void End()
    {
        for (int i = _used; i < _pool.Count; i++)
        {
            _covered[i] = null;
            if (_pool[i] && _pool[i].IsShown) _pool[i].Set(CropMarksUI.State.Hidden);
        }
        if (_lifted && _lifted != _hoveredThisFrame) Drop();
        else if (!_hoveredThisFrame) Drop();
    }

    /// <summary>Hides every mark and lowers a lifted target.</summary>
    public void Clear()
    {
        Begin();
        End();
    }

    /// <summary>The marks framing <paramref name="target"/> this frame, or null if it has none.</summary>
    public CropMarksUI MarksFor(RectTransform target)
    {
        for (int i = 0; i < _used; i++)
            if (_covered[i] == target) return _pool[i];
        return null;
    }

    void OnDisable() => Drop();

    CropMarksUI Holder(int index)
    {
        while (_pool.Count <= index)
        {
            var item = new GameObject($"Target Marks {_pool.Count}", typeof(RectTransform));
            item.layer = gameObject.layer;
            var rect = (RectTransform)item.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            _pool.Add(CropMarksUI.Attach(rect));
            _covered.Add(null);
        }
        return _pool[index];
    }

    // The rest position is remembered; it is put back only while the visual still sits at rest + 4,
    // so anything that wrote the visual meanwhile (a layout rebuild, a tween) is never pushed off.
    void Lift(RectTransform visual, RectTransform target)
    {
        if (_lifted == visual) return;
        Drop();
        _lifted = visual;
        _liftedTarget = target;
        _liftRest = visual.anchoredPosition;
        visual.anchoredPosition = _liftRest + new Vector2(0f, HoverLift);
    }

    void Drop()
    {
        if (_lifted && _lifted.anchoredPosition == _liftRest + new Vector2(0f, HoverLift)) _lifted.anchoredPosition = _liftRest;
        _lifted = null;
        _liftedTarget = null;
    }

    /// <summary>
    /// Whether <paramref name="screen"/> is inside the lifted target's rest rect: the pointer stays on
    /// it even where the raised visual no longer is (its bottom 4px), so the target does not bob.
    /// </summary>
    public bool RestContains(Vector2 screen)
    {
        if (!_liftedTarget || !_liftedTarget.gameObject.activeInHierarchy) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_liftedTarget, screen, null, out var local)) return false;
        // Only when the visual is the target itself does its rect move with the lift.
        if (_lifted == _liftedTarget) local += new Vector2(0f, HoverLift);
        return _liftedTarget.rect.Contains(local);
    }

    void Cover(RectTransform cover, RectTransform target)
    {
        var root = (RectTransform)transform;
        target.GetWorldCorners(_corners);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 4; i++)
        {
            Vector2 point = root.InverseTransformPoint(_corners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        var size = max - min;
        var centre = (min + max) * .5f - root.rect.center;
        if (cover.sizeDelta != size) cover.sizeDelta = size;
        if (cover.anchoredPosition != centre) cover.anchoredPosition = centre;
    }
}
