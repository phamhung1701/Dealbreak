using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps a Repeat-wrapped overlay at one texel per screen pixel under the Expand scaler, so the grain,
/// halftone and hatch never rescale into moire. On an Image (small tiled hatch) it sets
/// pixelsPerUnitMultiplier to the canvas scale; on a RawImage (large overlays, four vertices at any
/// size) it sets uvRect. It recomputes only when the screen size, canvas scale or its own rect
/// changes, and overlays never take raycasts.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class ScreenPixelTile : MonoBehaviour
{
    static readonly HashSet<ScreenPixelTile> Live = new();
    static Vector2Int _screen;
    static bool _watching;

    Graphic _graphic;
    float _scale = -1;
    Vector2 _size = new(-1, -1);

    public static float MultiplierFor(float scaleFactor) => scaleFactor;
    public static Rect UvRectFor(Vector2 size, float scaleFactor, Vector2 textureSize) =>
        new(0, 0, size.x * scaleFactor / textureSize.x, size.y * scaleFactor / textureSize.y);

    void OnEnable()
    {
        Live.Add(this);
        if (!_watching) { Canvas.willRenderCanvases += CheckScreen; _watching = true; }
        if (TryGetComponent(out _graphic) && _graphic.raycastTarget) _graphic.raycastTarget = false;
        Refresh();
    }

    void OnDisable() => Live.Remove(this);
    void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) Recompute(); }
    void OnCanvasHierarchyChanged() { if (isActiveAndEnabled) Refresh(); }

    // One handler for every tile; a uniform resolution change keeps the reference rect but moves the
    // scale, so the rect callback alone misses it.
    static void CheckScreen()
    {
        var screen = new Vector2Int(Screen.width, Screen.height);
        bool resized = screen != _screen;
        _screen = screen;
        foreach (var tile in Live)
            if (tile && (resized || !Mathf.Approximately(tile.CanvasScale(), tile._scale))) tile.Recompute();
    }

    float CanvasScale()
    {
        var canvas = _graphic ? _graphic.canvas : null;
        return canvas ? canvas.rootCanvas.scaleFactor : 1f;
    }

    /// <summary>Recomputes even when nothing seems to have changed (a new texture or sprite).</summary>
    public void Refresh() { _scale = -1; Recompute(); }

    void Recompute()
    {
        if (!_graphic && !TryGetComponent(out _graphic)) return;
        Apply(CanvasScale());
    }

    public void Apply(float scaleFactor)
    {
        if (scaleFactor <= 0f || (!_graphic && !TryGetComponent(out _graphic))) return;
        var size = ((RectTransform)transform).rect.size;
        if (Mathf.Approximately(scaleFactor, _scale) && size == _size) return;
        _scale = scaleFactor; _size = size;
        if (_graphic is Image image)
        {
            float multiplier = MultiplierFor(scaleFactor);
            if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, multiplier)) image.pixelsPerUnitMultiplier = multiplier;
        }
        else if (_graphic is RawImage raw && raw.texture)
        {
            var uv = UvRectFor(size, scaleFactor, new Vector2(raw.texture.width, raw.texture.height));
            if (raw.uvRect != uv) raw.uvRect = uv;
        }
    }
}
