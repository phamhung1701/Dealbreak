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
    public Color ink = new(0.055f, .065f, .075f);
    public Color charcoal = new(.105f, .13f, .155f);
    public Color slate = new(.29f, .36f, .41f);
    public Color paper = new(.94f, .90f, .81f);
    public Color brick = new(.66f, .27f, .22f);
    public Color ochre = new(.82f, .61f, .26f);

    // Cut Sheet tokens (production/ui/hud/visual-design.md section 1b). Appended after the legacy
    // fields; nothing reads them until the combat zones move over, so today's look is unchanged.
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
