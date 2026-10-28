using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DealbreakUIStyle.Outline keeps a per-label material instance. When the theme changes a label's font
/// (S6b moved numbers from IBM Plex Mono to Courier Prime), that instance must follow the new font's
/// atlas, or the outlined number samples the old atlas and draws as fragments.
/// </summary>
public sealed class DealbreakUIStyleOutlineTests
{
    GameObject _label;

    [TearDown]
    public void TearDown()
    {
        if (_label == null) return;
        var text = _label.GetComponent<TMP_Text>();
        var instance = text.fontSharedMaterial;
        Object.DestroyImmediate(_label);
        if (instance != null && !AssetDatabase.Contains(instance)) Object.DestroyImmediate(instance);
    }

    [Test]
    public void Outline_AfterTheFontChanges_SamplesTheNewFontsAtlas()
    {
        var plex = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/IBM Plex Mono UI.asset");
        var courier = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Courier Prime UI.asset");
        _label = new GameObject("Number", typeof(RectTransform), typeof(TextMeshProUGUI));
        var text = _label.GetComponent<TextMeshProUGUI>();
        text.font = plex;
        DealbreakUIStyle.Outline(text);

        text.font = courier;
        DealbreakUIStyle.Outline(text);

        Assert.That(text.fontSharedMaterial.mainTexture, Is.EqualTo(courier.atlasTexture));
        Assert.That(text.fontSharedMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Outline), Is.True);
        Assert.That(text.fontSharedMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth), Is.EqualTo(.24f).Within(.001f));
        Assert.That((Color32)text.fontSharedMaterial.GetColor(ShaderUtilities.ID_OutlineColor), Is.EqualTo(new Color32(0, 0, 0, 255)));
        Assert.That(text.fontSharedMaterial.GetFloat(ShaderUtilities.ID_GradientScale),
            Is.EqualTo(courier.material.GetFloat(ShaderUtilities.ID_GradientScale)), "The SDF spread is the new font's.");
    }

    [Test]
    public void Outline_TakesTheGivenColour_BlackByDefault()
    {
        _label = new GameObject("Number", typeof(RectTransform), typeof(TextMeshProUGUI));
        var text = _label.GetComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Courier Prime UI.asset");
        DealbreakUIStyle.Outline(text);
        Assert.That(text.outlineColor, Is.EqualTo(new Color32(0, 0, 0, 255)));
        DealbreakUIStyle.Outline(text, outline: new Color32(0xEF, 0xE4, 0xCC, 0xFF));
        Assert.That(text.outlineColor, Is.EqualTo(new Color32(0xEF, 0xE4, 0xCC, 0xFF)));
    }
}
