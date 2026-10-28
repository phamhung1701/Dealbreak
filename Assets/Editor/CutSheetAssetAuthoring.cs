using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Import settings for the Cut Sheet UI sprites (production/ui/hud/visual-design.md section 2) and the
/// CutSheetUI sprite atlas. The PNGs come from Tools/PrintArt/make_ui_sprites.py; this script only imports
/// them: Sprite, Single, Full Rect, PPU 100, the authored 9-slice border, Bilinear, no mipmaps, uncompressed.
/// Overlays wrap (Repeat) and stay out of the atlas. Running it twice changes nothing.
/// </summary>
public static class CutSheetAssetAuthoring
{
    public const string Folder = "Assets/Art/UI/CutSheet";
    public const string AtlasPath = Folder + "/CutSheetUI.spriteatlasv2";
    public const string CoinPath = "Assets/Art/UI/Sprites/Credits_Coin.png";
    public const float PixelsPerUnit = 100f;
    public const int MaxTextureSize = 256;
    public const int AtlasPadding = 4;

    public readonly struct Entry
    {
        public readonly string Path;
        public readonly Vector4 Border;   // L, B, R, T (Unity's Sprite.border order)
        public readonly bool Repeat;      // overlays tile through texture wrap
        public readonly bool InAtlas;

        public Entry(string path, Vector4 border, bool repeat, bool inAtlas)
        {
            Path = path; Border = border; Repeat = repeat; InAtlas = inAtlas;
        }
    }

    public static readonly Entry[] Entries =
    {
        new(Folder + "/ui_panel_stock_9s.png", new Vector4(4, 4, 4, 4), false, true),
        new(Folder + "/ui_tag_notch_9s.png", new Vector4(14, 12, 14, 12), false, true),
        new(Folder + "/ui_chip_stamp.png", Vector4.zero, false, true),
        new(Folder + "/ui_sticker_cutline_9s.png", new Vector4(10, 10, 10, 10), false, true),
        new(Folder + "/ui_mark_crop.png", Vector4.zero, false, true),
        new(Folder + "/ui_slot_dashed_9s.png", new Vector4(10, 10, 10, 10), false, true),
        new(Folder + "/ui_overlay_grain.png", Vector4.zero, true, false),
        new(Folder + "/ui_overlay_halftone.png", Vector4.zero, true, false),
        new(Folder + "/ui_overlay_hatch.png", Vector4.zero, true, false),
        new(CoinPath, Vector4.zero, false, false),
    };

    [MenuItem("Dealbreak/Art/Apply Cut Sheet Import Settings")]
    public static void ApplyAll()
    {
        foreach (var entry in Entries) ApplyImport(entry);
        ApplyAtlas();
        AssetDatabase.SaveAssets();
    }

    /// <summary>Applies the shared settings to one texture; reimports only when something differs.</summary>
    public static bool ApplyImport(Entry entry)
    {
        if (AssetImporter.GetAtPath(entry.Path) is not TextureImporter importer)
        {
            Debug.LogWarning($"Cut Sheet: no texture at {entry.Path}. Run Tools/PrintArt/make_ui_sprites.py first.");
            return false;
        }

        var current = new TextureImporterSettings();
        importer.ReadTextureSettings(current);
        var desired = new TextureImporterSettings();
        importer.ReadTextureSettings(desired);
        desired.textureType = TextureImporterType.Sprite;
        desired.spriteMode = (int)SpriteImportMode.Single;
        desired.spriteMeshType = SpriteMeshType.FullRect;
        desired.spriteAlignment = (int)SpriteAlignment.Center;
        desired.spritePivot = new Vector2(.5f, .5f);
        desired.spriteBorder = entry.Border;
        desired.spritePixelsPerUnit = PixelsPerUnit;
        desired.spriteExtrude = 0;
        desired.sRGBTexture = true;
        desired.alphaSource = TextureImporterAlphaSource.FromInput;
        desired.alphaIsTransparency = true;
        desired.mipmapEnabled = false;
        desired.readable = false;
        desired.filterMode = FilterMode.Bilinear;
        desired.wrapMode = entry.Repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        desired.aniso = 0;

        bool changed = !TextureImporterSettings.Equal(current, desired)
                       || importer.textureCompression != TextureImporterCompression.Uncompressed
                       || importer.maxTextureSize != MaxTextureSize;
        if (!changed) return false;

        importer.SetTextureSettings(desired);
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = MaxTextureSize;
        importer.SaveAndReimport();
        return true;
    }

    /// <summary>Creates or updates the CutSheetUI atlas: sprites 1-5 plus the dashed slot, tight packing off, padding 4.</summary>
    public static void ApplyAtlas()
    {
        var sprites = Entries.Where(e => e.InAtlas)
            .Select(e => AssetDatabase.LoadAssetAtPath<Sprite>(e.Path))
            .Where(s => s != null)
            .ToArray();

        var existing = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
        var packed = existing != null ? new HashSet<Object>(existing.GetPackables()) : new HashSet<Object>();
        var missing = sprites.Where(s => !packed.Contains(s)).Cast<Object>().ToArray();
        if (existing == null || missing.Length > 0)
        {
            var atlasAsset = existing != null ? SpriteAtlasAsset.Load(AtlasPath) : new SpriteAtlasAsset();
            atlasAsset.Add(missing);
            SpriteAtlasAsset.Save(atlasAsset, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        }

        if (AssetImporter.GetAtPath(AtlasPath) is not SpriteAtlasImporter importer) return;
        bool changed = false;

        var packing = importer.packingSettings;
        if (packing.enableTightPacking || packing.enableRotation || packing.padding != AtlasPadding)
        {
            packing.enableTightPacking = false;
            packing.enableRotation = false;
            packing.padding = AtlasPadding;
            importer.packingSettings = packing;
            changed = true;
        }

        var texture = importer.textureSettings;
        if (texture.generateMipMaps || texture.readable || !texture.sRGB || texture.filterMode != FilterMode.Bilinear)
        {
            texture.generateMipMaps = false;
            texture.readable = false;
            texture.sRGB = true;
            texture.filterMode = FilterMode.Bilinear;
            importer.textureSettings = texture;
            changed = true;
        }

        var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
        if (platform.textureCompression != TextureImporterCompression.Uncompressed)
        {
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformSettings(platform);
            changed = true;
        }

        if (changed) importer.SaveAndReimport();
    }
}
