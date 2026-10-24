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
}
