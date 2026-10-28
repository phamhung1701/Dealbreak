using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Dealbreak/UI/Design Theme")]
public sealed class DealbreakUITheme : ScriptableObject
{
    public TMP_FontAsset headingFont, bodyFont, numericFont, symbolFont;
    [Tooltip("Condensed caps for small section heads, labels and secondary buttons.")]
    public TMP_FontAsset labelFont;
    public Texture2D paperTexture, slateTexture, darkTexture, dryAccents, cleanAccents;
    public Material inkAccentMaterial;
    [Tooltip("UI/Dealbreak Halftone: flat fills printed with a dot screen, like the item and character art.")]
    public Material halftoneMaterial;
    [Tooltip("The Credits coin beside the HP bar; enemy Credits fly into it.")]
    public Sprite creditsIcon;
    // Legacy names, Cut Sheet values (production/ui/hud/visual-design.md section 1a): ink = the board,
    // charcoal = a raised dark surface, slate = warm hairline, paper = cream stock, brick = signal red,
    // ochre = teal (selection and valid target only).
    public Color ink = new Color32(0x1C, 0x18, 0x14, 0xFF);
    public Color charcoal = new Color32(0x24, 0x1E, 0x18, 0xFF);
    public Color slate = new Color32(0x5C, 0x52, 0x47, 0xFF);
    public Color paper = new Color32(0xEF, 0xE4, 0xCC, 0xFF);
    public Color brick = new Color32(0xA8, 0x45, 0x38, 0xFF);
    public Color ochre = new Color32(0x4A, 0xA6, 0xB8, 0xFF);

    // Cut Sheet tokens (production/ui/hud/visual-design.md section 1b), appended after the legacy fields.
    [Tooltip("Type on stock, baked-border colour and every Cut Sheet shadow tint.")]
    public Color inkPrint = new Color32(0x12, 0x12, 0x12, 0xFF);
    [Tooltip("Tricks heading strip and secondary surfaces.")]
    public Color kraft = new Color32(0xB8, 0x95, 0x6A, 0xFF);
    [Tooltip("Credits only.")]
    public Color mustard = new Color32(0xE6, 0xAB, 0x55, 0xFF);
    [Tooltip("Disabled fill and the enemy Shield fill.")]
    public Color muted = new Color32(0x8C, 0x82, 0x70, 0xFF);
    [Tooltip("The board tone while the enemy attacks.")]
    public Color boardDefense = new Color32(0x15, 0x11, 0x0E, 0xFF);
    [Tooltip("Cut Sheet sprites for EditorialFrame sprite mode.")]
    public Sprite panelSprite, tagSprite, chipSprite, stickerSprite, markSprite;
    [Tooltip("The flat dashed outline of an empty Gear or Trick slot.")]
    public Sprite slotDashedSprite;
    [Tooltip("Repeat-wrapped overlays, set one texel per screen pixel by ScreenPixelTile.")]
    public Sprite grainSprite, halftoneSprite, hatchSprite;
}
