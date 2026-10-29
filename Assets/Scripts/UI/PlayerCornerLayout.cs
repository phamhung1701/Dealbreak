using UnityEngine;

/// <summary>
/// The player corner's placement (hud.md section C, owner ruling 2026-10-27), in canvas fractions of
/// the 1920x1080 reference. <see cref="Apply"/> is the only writer of the HP panel's anchors: the
/// runtime placer calls it every frame (it writes only on change) and the authoring scripts call it
/// to bake the same rect into Game.unity. The Credits chip and the portrait are children of the HP
/// panel, offset in reference pixels above its top edge, so they show wherever it does.
/// </summary>
public static class PlayerCornerLayout
{
    public const float RefWidth = 1920f, RefHeight = 1080f;
    public const float PanelMinX = .025f, PanelMaxX = .235f, PanelMinY = .014f, PanelMaxY = .082f;
    // Beside the compact hand (route, Black Market, Incident) the panel keeps its rows but ends
    // before the compact hand strip (x .17).
    public const float CompactPanelMaxX = .165f;
    // Credits chip: x .025-.134, y .09-.13 (210x46: a 46px disc over a 38px plate).
    public const float ChipGap = 9f, ChipWidth = 210f, ChipHeight = 46f, PlateHeight = 38f;
    // Portrait: x .025-.145, y .14-.26, above the chip.
    public const float PortraitGap = 8f, PortraitWidth = 230f, PortraitHeight = 130f;
    public const float PortraitBottom = ChipGap + ChipHeight + PortraitGap;
    // Block/Shield line: right of the chip, on the chip's band above the panel.
    public const float DefenseLineHeight = 32f, DefenseLineLeft = ChipWidth + 8f;

    public static Vector2 PanelMin => new(PanelMinX, PanelMinY);
    public static Vector2 PanelMax(bool compact) => new(compact ? CompactPanelMaxX : PanelMaxX, PanelMaxY);

    /// <summary>Places the HP panel at its canvas rect; returns true when anything was written.</summary>
    public static bool Apply(RectTransform panel, bool compact)
    {
        if (!panel) return false;
        ParentAnchors(panel, out var parentMin, out var parentMax);
        var size = parentMax - parentMin;
        if (size.x <= 0f || size.y <= 0f) return false;
        var min = Divide(PanelMin - parentMin, size);
        var max = Divide(PanelMax(compact) - parentMin, size);
        bool changed = false;
        if (panel.anchorMin != min) { panel.anchorMin = min; changed = true; }
        if (panel.anchorMax != max) { panel.anchorMax = max; changed = true; }
        if (panel.offsetMin != Vector2.zero) { panel.offsetMin = Vector2.zero; changed = true; }
        if (panel.offsetMax != Vector2.zero) { panel.offsetMax = Vector2.zero; changed = true; }
        return changed;
    }

    // The panel's parent rect in canvas fractions: anchors composed up to the canvas (or topmost) rect.
    static void ParentAnchors(RectTransform panel, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.one;
        for (var r = panel.parent as RectTransform; r && r.parent && !r.TryGetComponent<Canvas>(out _); r = r.parent as RectTransform)
        {
            var a = r.anchorMin;
            var b = r.anchorMax;
            min = a + Vector2.Scale(b - a, min);
            max = a + Vector2.Scale(b - a, max);
        }
    }

    static Vector2 Divide(Vector2 value, Vector2 by) => new(value.x / by.x, value.y / by.y);
}
