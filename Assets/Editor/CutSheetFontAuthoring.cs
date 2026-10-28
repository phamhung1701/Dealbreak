using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Builds the Cut Sheet text fonts (production/ui/hud/visual-design.md section 2d, engine-notes 1.3):
/// Zilla Slab Medium and SemiBold for body text, Courier Prime Bold for numbers. Each is a static SDF
/// (sampling 90, padding 9, SDFAA, one 1024 page) holding ASCII plus a few typographic marks, with the
/// source font kept. Suits and other symbols come through the fallbacks: Dealbreak Symbols, then the
/// LiberationSans fallback. Also writes the HP number's paper-outline material preset. Nothing points at
/// these yet; S6b swaps the theme over. Running it twice changes nothing.
/// </summary>
public static class CutSheetFontAuthoring
{
    public const string Folder = "Assets/Art/Fonts";
    public const int SamplingPointSize = 90;
    public const int Padding = 9;
    public const int AtlasSize = 1024;
    public const float OutlineWidth = .24f;   // DealbreakUIStyle.Outline's default
    public static readonly string Characters =
        new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray()) + "•×‘’“”–—";

    public static readonly (string Source, string Name)[] Fonts =
    {
        (Folder + "/ZillaSlab-Medium.ttf", "Zilla Slab UI"),
        (Folder + "/ZillaSlab-SemiBold.ttf", "Zilla Slab SemiBold UI"),
        (Folder + "/CourierPrime-Bold.ttf", "Courier Prime UI"),
    };

    public const string SymbolsPath = Folder + "/Dealbreak Symbols.asset";
    public const string LiberationFallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";
    public const string PaperOutlinePath = Folder + "/Courier Prime UI - Paper Outline.mat";
    static readonly Color Paper = new Color32(0xEF, 0xE4, 0xCC, 0xFF);

    [MenuItem("Dealbreak/Art/Build Cut Sheet Fonts")]
    public static void BuildAll()
    {
        var fallbacks = new List<TMP_FontAsset>
        {
            Load(SymbolsPath),
            Load(LiberationFallbackPath),
        };
        TMP_FontAsset numeric = null;
        foreach (var (source, name) in Fonts)
        {
            var font = BuildFont(source, name, fallbacks);
            if (name == "Courier Prime UI") numeric = font;
        }
        BuildPaperOutline(numeric);
        AssetDatabase.SaveAssets();
    }

    static TMP_FontAsset Load(string path)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font == null) throw new System.InvalidOperationException("Missing font asset: " + path);
        return font;
    }

    static TMP_FontAsset BuildFont(string sourcePath, string name, List<TMP_FontAsset> fallbacks)
    {
        string path = $"{Folder}/{name}.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null) throw new System.InvalidOperationException("Missing font source: " + sourcePath);
            font = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            if (font == null) throw new System.InvalidOperationException("TMP could not load: " + sourcePath);
            font.name = name;
            if (!font.TryAddCharacters(Characters, out string missing))
                Debug.LogWarning($"{name}: no glyph for '{missing}'; it falls back.");
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            font.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
            {
                atlas.name = name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
        }

        bool changed = false;
        // CreateFontAsset leaves these empty; record them so TMP's Font Asset Creator can rebuild the asset.
        var creation = font.creationSettings;
        if (creation.pointSize != SamplingPointSize)
        {
            creation.sourceFontFileGUID = AssetDatabase.AssetPathToGUID(sourcePath);
            creation.sourceFontFileName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            creation.pointSizeSamplingMode = 1;   // custom size
            creation.pointSize = SamplingPointSize;
            creation.padding = Padding;
            creation.paddingMode = 2;             // padding in pixels
            creation.packingMode = 4;             // optimum
            creation.atlasWidth = AtlasSize;
            creation.atlasHeight = AtlasSize;
            creation.characterSetSelectionMode = 7;   // custom characters
            creation.characterSequence = Characters;
            creation.renderMode = (int)GlyphRenderMode.SDFAA;
            font.creationSettings = creation;
            changed = true;
        }
        // The same label scale the other UI fonts carry, so swapping the theme keeps authored sizes.
        var face = font.faceInfo;
        if (!Mathf.Approximately(face.scale, DealbreakUIOverhaulAuthoring.TextScale))
        {
            face.scale = DealbreakUIOverhaulAuthoring.TextScale;
            font.faceInfo = face;
            changed = true;
        }
        if (font.fallbackFontAssetTable == null || !font.fallbackFontAssetTable.SequenceEqual(fallbacks))
        {
            font.fallbackFontAssetTable = new List<TMP_FontAsset>(fallbacks);
            changed = true;
        }
        if (changed) EditorUtility.SetDirty(font);
        return font;
    }

    // A shared preset (fontSharedMaterial), so outlined HP numbers batch and nothing allocates per label.
    static void BuildPaperOutline(TMP_FontAsset font)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(PaperOutlinePath);
        if (material == null)
        {
            material = new Material(font.material) { name = "Courier Prime UI - Paper Outline" };
            AssetDatabase.CreateAsset(material, PaperOutlinePath);
        }
        bool changed = false;
        if (material.GetTexture(ShaderUtilities.ID_MainTex) != font.atlasTexture)
        {
            material.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            changed = true;
        }
        if (material.GetColor(ShaderUtilities.ID_OutlineColor) != Paper)
        {
            material.SetColor(ShaderUtilities.ID_OutlineColor, Paper);
            changed = true;
        }
        if (!Mathf.Approximately(material.GetFloat(ShaderUtilities.ID_OutlineWidth), OutlineWidth))
        {
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            changed = true;
        }
        // The mobile SDF shader draws an outline only with this keyword on.
        if (!material.IsKeywordEnabled(ShaderUtilities.Keyword_Outline))
        {
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            changed = true;
        }
        if (changed) EditorUtility.SetDirty(material);
    }
}
