using UnityEngine;

/// <summary>
/// Keeps an element at a fixed size and offset, in 1080p reference pixels, from a point of the root
/// canvas (the header rows use its top centre), whatever the window's aspect ratio and whatever its
/// parent. The canvas only grows past 1920×1080 (CanvasScaler Expand), so pinned rows keep their
/// square slots instead of stretching with fractional anchors. Written by authoring; layout only.
/// </summary>
[DisallowMultipleComponent]
public sealed class CanvasPin : MonoBehaviour
{
    [Tooltip("Point of the root canvas the element keeps its offset from (0..1; top centre = .5, 1).")]
    public Vector2 canvasPoint = new(.5f, 1f);
    [Tooltip("The element's centre relative to that point, in reference pixels (y up).")]
    public Vector2 offset;
    [Tooltip("The element's size in reference pixels.")]
    public Vector2 size;

    Rect _rootFor, _parentFor;

    /// <summary>Pins to a box given as the 1920×1080 layout's pixels (x right, y down from the top).</summary>
    public void SetFromReference(float left, float top, float right, float bottom, float width = 1920f)
    {
        canvasPoint = new Vector2(.5f, 1f);
        offset = new Vector2((left + right) * .5f - width * .5f, -(top + bottom) * .5f);
        size = new Vector2(right - left, bottom - top);
        _rootFor = default;
    }

    void LateUpdate() => Apply();

    public void Apply()
    {
        var rect = (RectTransform)transform;
        if (rect.parent is not RectTransform parent) return;
        var canvas = rect.GetComponentInParent<Canvas>();
        if (!canvas || canvas.rootCanvas.transform is not RectTransform root) return;
        Rect rootRect = root.rect, parentRect = parent.rect;
        if (rootRect == _rootFor && parentRect == _parentFor) return;
        if (parentRect.width <= 0f || parentRect.height <= 0f) return;
        _rootFor = rootRect; _parentFor = parentRect;
        Vector2 point = rootRect.min + Vector2.Scale(rootRect.size, canvasPoint) + offset;
        Vector2 local = parent.InverseTransformPoint(root.TransformPoint(point));
        var anchor = new Vector2((local.x - parentRect.xMin) / parentRect.width, (local.y - parentRect.yMin) / parentRect.height);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }
}
