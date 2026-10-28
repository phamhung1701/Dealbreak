using UnityEngine;

/// <summary>
/// The Cut Sheet look's fixed numbers in one place (production/ui/hud/visual-design.md sections 1b
/// and 3, design/ux/hud.md). Pixels are 1080p reference pixels; times are seconds of unscaled time.
/// </summary>
public static class CutSheetMetrics
{
    // Hard offset shadow depth (d, -d): the element's own sprite tinted Theme.inkPrint.
    public const float ShadowPanel = 6, ShadowButton = 4, ShadowSlot = 3, ShadowMeter = 0;
    public const float ShadowCardRest = 8, ShadowCardMax = 12;

    // State recipes (visual-design 3a). Hover moves the face up-left, pressed down-right; the shadow
    // stays put, so it reads rest + hover and 0 when pressed.
    public const float HoverLift = 2, PressDepth = 4, CardSelectedLift = 24;
    public static readonly Vector2 HoverFaceOffset = new(-HoverLift, HoverLift);
    public static readonly Vector2 PressedFaceOffset = new(PressDepth, -PressDepth);
    public const float PressSeconds = .06f, ReleaseSeconds = .09f;

    // Selected cards: shadow = rest + lift / 2 (8 + 12 = 20).
    public static float SelectedCardShadow(float lift) => ShadowCardRest + lift * .5f;

    // Tags and the tooltip (hud.md).
    public const float StatusCrossfadeSeconds = .25f;
    public const float TriggerTagSeconds = .8f, StickerHopSeconds = .22f;
    public const float TooltipOpenSeconds = .11f, TooltipCloseSeconds = .07f, TooltipSlide = 6;
    public const float TooltipMaxWidth = 360, TooltipPadding = 16;

    // Text floors: at least 18px, 20px on kraft.
    public const float TextFloor = 18, TextFloorOnKraft = 20;

    // Tilt in degrees. Slots stay within +/-1, tags settle to +/-1.5 (INCOMING starts at 3), the status
    // tag never tilts. Slots read a fixed table by index, never RNG or instance IDs.
    public const float SlotTiltMax = 1, TagTiltRest = 1.5f, IncomingTiltStart = 3, StatusTagTilt = 0;
    static readonly float[] SlotTilts = { -.6f, .8f, -.3f, 1f, -.9f, .4f, -.5f, .7f };
    public static int SlotTiltCount => SlotTilts.Length;
    public static float SlotTilt(int index)
    {
        int n = SlotTilts.Length;
        return SlotTilts[((index % n) + n) % n];
    }
}
