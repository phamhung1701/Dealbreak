using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Cut Sheet action bar (HUD slice S10b, design/ux/hud.md section C, visual-design 3a/3b): its
/// rect, its buttons' rects, the sort column inside its left 18%, and the five buttons' recipe. The
/// authoring script and the runtime placer both call this, so each rect has one writer.
/// Presentation only.
/// </summary>
public static class ActionBarLayout
{
    // Canvas fractions at the 1920x1080 reference: x .25-.69 (centre .47, 845px), y .016-.086 (76px).
    public static readonly Vector2 BarMin = new(.25f, .016f), BarMax = new(.69f, .086f);
    // Bar fractions. The sort column is the first 18% (152px); ATTACK 317px, BLOCK 241px, PASS 97px,
    // with 13px gaps, so the longest labels ("ATTACK ROYAL FLUSH", "CONFIRM BLOCK") fit at Anton 32.
    public const float SortColumn = .18f;
    public const float SortLeft = .01f, SortRight = .17f;
    public const float ButtonBottom = .06f, ButtonTop = .94f;
    public static readonly Vector2 Attack = new(.19f, .565f), Block = new(.58f, .865f), Pass = new(.88f, .995f);
    // Two 29px sort buttons stacked in the 76px bar (.384 of its height each, 8px apart).
    public static readonly Vector2 RankY = new(.556f, .94f), SuitY = new(.06f, .444f);
    public const float ActionLabelSize = 32, SortLabelSize = 22;

    /// <summary>Writes the bar's anchors (in its parent's space) and its three buttons' anchors.</summary>
    public static void ApplyBar(RectTransform bar, Button attack, Button block, Button pass)
    {
        if (!bar) return;
        Parent(bar, out var parentMin, out var parentMax);
        var size = parentMax - parentMin;
        var min = new Vector2((BarMin.x - parentMin.x) / size.x, (BarMin.y - parentMin.y) / size.y);
        var max = new Vector2((BarMax.x - parentMin.x) / size.x, (BarMax.y - parentMin.y) / size.y);
        DealbreakUIStyle.Place(bar, min.x, min.y, max.x, max.y);
        PlaceChild(attack, bar, Attack);
        PlaceChild(block, bar, Block);
        PlaceChild(pass, bar, Pass);
    }

    /// <summary>Stacks the sort buttons inside the bar's left 18%. They are the bar's siblings.</summary>
    public static void PlaceSorts(RectTransform bar, Button rank, Button suit)
    {
        if (!bar) return;
        var min = bar.anchorMin;
        var size = bar.anchorMax - min;
        Sort(rank, bar, min, size, RankY);
        Sort(suit, bar, min, size, SuitY);
    }

    /// <summary>Opts the bar's five buttons into the Cut Sheet sprite recipe.</summary>
    public static void Style(ActionButtonsUI actions)
    {
        var theme = DealbreakUIStyle.Theme;
        if (!actions || !theme) return;
        RetireBacking(actions.playButton ? actions.playButton.transform.parent as RectTransform : null);
        DealbreakUIStyle.CutSheetButton(actions.playButton, EditorialButton.Role.Primary, CutSheetMetrics.ShadowButton, theme.headingFont, ActionLabelSize, false);
        DealbreakUIStyle.CutSheetButton(actions.blockButton, EditorialButton.Role.Paper, CutSheetMetrics.ShadowButton, theme.headingFont, ActionLabelSize, false);
        DealbreakUIStyle.CutSheetButton(actions.passButton, EditorialButton.Role.Paper, CutSheetMetrics.ShadowButton, theme.headingFont, ActionLabelSize, false);
        var sortFont = theme.labelFont ? theme.labelFont : theme.headingFont;
        DealbreakUIStyle.CutSheetButton(actions.rankSortButton, EditorialButton.Role.Paper, CutSheetMetrics.ShadowSlot, sortFont, SortLabelSize, true);
        DealbreakUIStyle.CutSheetButton(actions.suitSortButton, EditorialButton.Role.Paper, CutSheetMetrics.ShadowSlot, sortFont, SortLabelSize, true);
    }

    /// <summary>
    /// The buttons sit directly on the board: the bar's neo-punk strip (its Image and any Editorial
    /// Frame) stops drawing. The rect, and the Image's raycast, stay as they were.
    /// </summary>
    public static void RetireBacking(RectTransform bar)
    {
        if (!bar) return;
        if (bar.TryGetComponent<Image>(out var image) && image.color != Color.clear) image.color = Color.clear;
        foreach (var effect in bar.GetComponents<Shadow>()) if (effect.enabled) effect.enabled = false;
        foreach (Transform child in bar)
            if (child.TryGetComponent<EditorialFrame>(out var frame) && frame.enabled) frame.enabled = false;
    }

    static void PlaceChild(Button button, RectTransform bar, Vector2 x)
    {
        if (button && button.transform.parent == bar) DealbreakUIStyle.Place(button.transform, x.x, ButtonBottom, x.y, ButtonTop);
    }

    static void Sort(Button button, RectTransform bar, Vector2 min, Vector2 size, Vector2 y)
    {
        if (!button || button.transform.parent != bar.parent) return;
        DealbreakUIStyle.Place(button.transform, min.x + size.x * SortLeft, min.y + size.y * y.x,
            min.x + size.x * SortRight, min.y + size.y * y.y);
    }

    // The parent's anchors in canvas fractions, composed up to the canvas (stretched rects, zero offsets).
    static void Parent(RectTransform rect, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero; max = Vector2.one;
        for (var r = rect.parent as RectTransform; r && !r.TryGetComponent<Canvas>(out _); r = r.parent as RectTransform)
        {
            var a = r.anchorMin; var b = r.anchorMax;
            min = a + Vector2.Scale(b - a, min);
            max = a + Vector2.Scale(b - a, max);
        }
    }
}
