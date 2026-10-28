using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Pins the Cut Sheet UI sprites (visual-design section 2): borders and import settings applied by
/// CutSheetAssetAuthoring, the CutSheetUI atlas, and the generator's pixels (Tools/PrintArt/make_ui_sprites.py
/// prints the same hashes). Also pins the Cut Sheet text fonts built by CutSheetFontAuthoring (section 2d).
/// Values are written out here on purpose: the test assembly cannot see the editor scripts.
/// </summary>
public sealed class CutSheetAssetsTests
{
    const string Folder = "Assets/Art/UI/CutSheet/";
    const string AtlasPath = Folder + "CutSheetUI.spriteatlasv2";

    // path, width, height, border L, B, R, T, overlay (Repeat, not atlased), pixel hash (SHA-256 of GetPixels32 bytes)
    static readonly object[] Sprites =
    {
        new object[] { Folder + "ui_panel_stock_9s.png", 64, 64, 4, 4, 4, 4, false, "603dc984dc9636f6a72d93fd4792ebff90d015bb9b89d7803152cb2e75807930" },
        new object[] { Folder + "ui_tag_notch_9s.png", 64, 40, 14, 12, 14, 12, false, "e1b43132a504e63435da04ec3a64eecf86fb6aff959142eed026eee0ff3d4d76" },
        new object[] { Folder + "ui_chip_stamp.png", 96, 96, 0, 0, 0, 0, false, "475a2950d7d583025fe0bc67afe7da6f4a703f01fdccc3780881dc20f28cde92" },
        new object[] { Folder + "ui_sticker_cutline_9s.png", 64, 64, 10, 10, 10, 10, false, "976d94f7f772dddfd73df22009ad2649315d6da95b0fce190600072bf16684a5" },
        new object[] { Folder + "ui_mark_crop.png", 32, 32, 0, 0, 0, 0, false, "80c920e5d24e88a5ef07b09e97e95a5732bd50c58c35603dca676186ca77fb43" },
        new object[] { Folder + "ui_slot_dashed_9s.png", 70, 70, 10, 10, 10, 10, false, "7d4f553f93819495cb39b6c6ad2c6b70e3147d940c7d2a2c66338ba4e639edb3" },
        new object[] { Folder + "ui_overlay_grain.png", 256, 256, 0, 0, 0, 0, true, "886b02d15d236ce100fbc6e1242b1866b34c24351584953876f2d1e5ca9850ef" },
        new object[] { Folder + "ui_overlay_halftone.png", 16, 16, 0, 0, 0, 0, true, "2b9ab8132770016b669072b9d8c38b1f02b99dc4403152ab03106abd47878903" },
        new object[] { Folder + "ui_overlay_hatch.png", 12, 12, 0, 0, 0, 0, true, "339fe01532ee259a76e2f39410a48405384e512ad8f8205d052e205d604b5814" },
        new object[] { "Assets/Art/UI/Sprites/Credits_Coin.png", 64, 64, 0, 0, 0, 0, false, "3cda8e77d30702e43d533f6036beee4dbabd92afc462228ecd89ddc2676a3e54" },
    };

    static readonly string[] AtlasedSprites =
    {
        "ui_panel_stock_9s", "ui_tag_notch_9s", "ui_chip_stamp", "ui_sticker_cutline_9s", "ui_mark_crop", "ui_slot_dashed_9s",
    };

    [TestCaseSource(nameof(Sprites))]
    public void Sprite_HasAuthoredBorderAndImportSettings(string path, int width, int height, int left, int bottom, int right, int top, bool overlay, string hash)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.That(importer, Is.Not.Null, $"{path} is missing; run Tools/PrintArt/make_ui_sprites.py, then Dealbreak/Art/Apply Cut Sheet Import Settings.");
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        Assert.That(settings.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
        Assert.That(settings.spriteMode, Is.EqualTo((int)SpriteImportMode.Single), path);
        Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), path);
        Assert.That(settings.spritePixelsPerUnit, Is.EqualTo(100f), path);
        Assert.That(settings.mipmapEnabled, Is.False, path);
        Assert.That(settings.sRGBTexture, Is.True, path);
        Assert.That(settings.alphaIsTransparency, Is.True, path);
        Assert.That(settings.readable, Is.False, path);
        Assert.That(settings.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
        Assert.That(settings.wrapMode, Is.EqualTo(overlay ? TextureWrapMode.Repeat : TextureWrapMode.Clamp), path);
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
        Assert.That(importer.maxTextureSize, Is.EqualTo(256), path);

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Assert.That(sprite, Is.Not.Null, path);
        Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(width, height)), path);
        Assert.That(sprite.border, Is.EqualTo(new Vector4(left, bottom, right, top)), path);
    }

    [TestCaseSource(nameof(Sprites))]
    public void Sprite_PixelsMatchTheGenerator(string path, int width, int height, int left, int bottom, int right, int top, bool overlay, string hash)
    {
        Assert.That(File.Exists(path), Is.True, path);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
            Assert.That(texture.width, Is.EqualTo(width), path);
            Assert.That(texture.height, Is.EqualTo(height), path);
            var pixels = texture.GetPixels32();
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r;
                bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b;
                bytes[i * 4 + 3] = pixels[i].a;
            }
            using var sha = SHA256.Create();
            var actual = string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            Assert.That(actual, Is.EqualTo(hash), $"{path} differs from Tools/PrintArt/make_ui_sprites.py output.");
        }
        finally { Object.DestroyImmediate(texture); }
    }

    [Test]
    public void Atlas_PacksTheSixSlicedSprites_WithTightPackingOff()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
        Assert.That(atlas, Is.Not.Null, $"{AtlasPath} is missing; run Dealbreak/Art/Apply Cut Sheet Import Settings.");
        var packed = atlas.GetPackables().Select(p => p.name).OrderBy(n => n).ToArray();
        Assert.That(packed, Is.EqualTo(AtlasedSprites.OrderBy(n => n).ToArray()));

        var importer = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.packingSettings.enableTightPacking, Is.False);
        Assert.That(importer.packingSettings.enableRotation, Is.False);
        Assert.That(importer.packingSettings.padding, Is.EqualTo(4));
        Assert.That(importer.textureSettings.generateMipMaps, Is.False);
        Assert.That(importer.GetPlatformSettings("DefaultTexturePlatform").textureCompression,
            Is.EqualTo(TextureImporterCompression.Uncompressed));
    }

    // Cut Sheet text fonts (visual-design section 2d), built by Dealbreak/Art/Build Cut Sheet Fonts.
    static readonly object[] TextFonts =
    {
        new object[] { "Assets/Art/Fonts/Zilla Slab UI.asset", "ZillaSlab-Medium", "Assets/Art/Fonts/ZillaSlab-OFL.txt" },
        new object[] { "Assets/Art/Fonts/Zilla Slab SemiBold UI.asset", "ZillaSlab-SemiBold", "Assets/Art/Fonts/ZillaSlab-OFL.txt" },
        new object[] { "Assets/Art/Fonts/Courier Prime UI.asset", "CourierPrime-Bold", "Assets/Art/Fonts/CourierPrime-OFL.txt" },
    };

    static TMP_FontAsset LoadFont(string path)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        Assert.That(font, Is.Not.Null, $"{path} is missing; run Dealbreak/Art/Build Cut Sheet Fonts.");
        return font;
    }

    [TestCaseSource(nameof(TextFonts))]
    public void TextFont_IsStaticWithItsSourceAndCharacterSet(string path, string source, string licence)
    {
        var font = LoadFont(path);
        Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static), path);
        // A static asset drops its runtime sourceFontFile; the Editor keeps the source by GUID.
        string sourceGuid = new SerializedObject(font).FindProperty("m_SourceFontFileGUID").stringValue;
        Assert.That(AssetDatabase.GUIDToAssetPath(sourceGuid), Is.EqualTo($"Assets/Art/Fonts/{source}.ttf"), path);
        Assert.That(font.atlasTextures, Is.Not.Empty, path);
        Assert.That(font.atlasTexture.width, Is.EqualTo(1024), path);
        Assert.That(font.faceInfo.pointSize, Is.EqualTo(90), path);
        Assert.That(font.creationSettings.pointSize, Is.EqualTo(90), "Creation settings let TMP's Font Asset Creator rebuild it.");
        Assert.That(font.atlasPadding, Is.EqualTo(9), path);
        Assert.That(font.faceInfo.scale, Is.EqualTo(1.2f).Within(.0001f), "Same label scale as the other UI fonts.");
        Assert.That(File.Exists(licence), Is.True, $"{licence}: the OFL text ships beside the font.");
        string set = new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray()) + "•×‘’“”–—";
        foreach (char c in set)
            Assert.That(font.HasCharacter(c, false, false), Is.True, $"{font.name}: U+{(int)c:X4}");
    }

    [TestCaseSource(nameof(TextFonts))]
    public void TextFont_ReachesSuitsThroughSymbolsThenLiberation(string path, string source, string licence)
    {
        var font = LoadFont(path);
        Assert.That(font.fallbackFontAssetTable.Select(f => f.name),
            Is.EqualTo(new[] { "Dealbreak Symbols", "LiberationSans SDF - Fallback" }), path);
        foreach (char suit in "♥♦♣♠")
            Assert.That(font.HasCharacter(suit, true, true), Is.True, $"{font.name}: {suit}");
    }

    [Test]
    public void CourierPrimePaperOutline_IsASharedPresetOnTheFontAtlas()
    {
        var font = LoadFont("Assets/Art/Fonts/Courier Prime UI.asset");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Fonts/Courier Prime UI - Paper Outline.mat");
        Assert.That(material, Is.Not.Null);
        Assert.That(material.shader, Is.EqualTo(font.material.shader));
        Assert.That(material.GetTexture(ShaderUtilities.ID_MainTex), Is.EqualTo(font.atlasTexture));
        Assert.That((Color32)material.GetColor(ShaderUtilities.ID_OutlineColor), Is.EqualTo(new Color32(0xEF, 0xE4, 0xCC, 0xFF)));
        Assert.That(material.GetFloat(ShaderUtilities.ID_OutlineWidth), Is.EqualTo(.24f).Within(.0001f));
        Assert.That(material.IsKeywordEnabled(ShaderUtilities.Keyword_Outline), Is.True);
    }
}
