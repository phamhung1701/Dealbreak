using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Procedural print textures: paper grain, poster paper, ink accents, the city backdrop and Gear slots.
/// Every pixel comes from seeded noise and hand-placed shapes, so a rerun reproduces the files exactly.
/// Sizes match the existing imports so their sprite slices and scene references stay valid.
/// </summary>
public static class DealbreakPrintTextureAuthoring
{
    public const string TextureFolder = "Assets/Art/UI/Textures";
    public const string SpriteFolder = "Assets/Art/UI/Sprites";
    public const string PaperGrain = TextureFolder + "/Paper_Grain.png";
    public const string PaperPoster = TextureFolder + "/Paper_Poster.png";
    public const string CityBackdrop = TextureFolder + "/Backdrop_City.png";
    // Near-white, so the tinted panel veils keep their tone and only gain print grain.
    public const string SlateGrain = TextureFolder + "/Slate_Grain.png";
    // Atlases sampled through RawImage.uvRect; only the top-left cell of each is drawn (see DealbreakUIStyle.Frame).
    public const string RoughAccents = TextureFolder + "/InkAccents_Rough.png";
    public const string CleanAccents = TextureFolder + "/InkAccents_Clean.png";
    public const string SlotEmpty = SpriteFolder + "/Slot_Empty.png";
    public const string SlotSelected = SpriteFolder + "/Slot_Selected.png";
    // Shown in the enemy portrait window until an enemy has authored art.
    public const string PortraitUnknown = SpriteFolder + "/Portrait_Unknown.png";

    static readonly Color Charcoal = new(.105f, .13f, .155f), Slate = new(.29f, .36f, .41f), Ochre = new(.82f, .61f, .26f);

    [MenuItem("Dealbreak/Art/Regenerate Print Textures")]
    public static void RegenerateAll()
    {
        Write(PaperGrain, 1792, 2400, PaperGrainPixel);
        Write(PaperPoster, 1792, 2400, PaperPosterPixel);
        Write(CityBackdrop, 1020, 572, CityPixel);
        Write(SlateGrain, 1024, 1024, SlateGrainPixel);
        Write(RoughAccents, 1792, 2400, (x, y, w, h) => InkOnWhite(RoughCorner(x, y, w, h)));
        Write(CleanAccents, 2912, 1440, (x, y, w, h) => InkOnWhite(CleanCorner(x, y, w, h)));
        Write(SlotEmpty, 129, 129, (x, y, w, h) => SlotPixel(x, y, Slate, 3));
        Write(SlotSelected, 129, 129, (x, y, w, h) => SlotPixel(x, y, Ochre, 5));
        Write(PortraitUnknown, 512, 512, UnknownPortraitPixel);
        AsSprite(PortraitUnknown);
    }

    static void AsSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    // x, y are pixel coordinates with y = 0 at the top of the image.
    delegate Color PixelFn(int x, int y, int w, int h);

    static void Write(string path, int w, int h, PixelFn pixel)
    {
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[(h - 1 - y) * w + x] = pixel(x, y, w, h);
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        try
        {
            texture.SetPixels32(pixels);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    // ---- Paper ----

    static Color PaperGrainPixel(int x, int y, int w, int h)
    {
        float v = .965f + (Fbm(x / 3f, y / 3f, 2, 11) - .5f) * .06f;
        float field = .10f + .22f * Fbm(x / 320f, y / 320f, 3, 12);
        if (Halftone(x, y, 14, 45, field)) v -= .38f;
        v = Speck(x, y, v, 13, .88f);
        return Gray(v);
    }

    static Color PaperPosterPixel(int x, int y, int w, int h)
    {
        float v = .975f + (Fbm(x / 3f, y / 3f, 2, 21) - .5f) * .05f;
        // Halftone gathers toward the edges, heaviest on the left, like an over-inked poster margin.
        float edge = Mathf.Min(Mathf.Min(x * .7f, w - x), Mathf.Min(y, h - y)) / (w * .16f);
        float dark = Mathf.Clamp01(1 - edge) * (.35f + .4f * Fbm(x / 180f, y / 180f, 3, 22));
        if (Halftone(x, y, 12, 45, dark)) v -= .42f;
        v = Speck(x, y, v, 23, .86f);
        return Gray(v);
    }

    static Color SlateGrainPixel(int x, int y, int w, int h)
    {
        float v = .95f + (Fbm(x / 2.5f, y / 2.5f, 2, 71) - .5f) * .08f - Fbm(x / 200f, y / 200f, 3, 72) * .05f;
        // Sparse, patchy dots: grain on the panel veils, never a visible grid.
        float patch = Mathf.Clamp01((Fbm(x / 180f, y / 180f, 3, 73) - .45f) * 2.2f);
        if (Halftone(x, y, 9, 15, .10f * patch)) v -= .07f;
        v = Speck(x, y, v, 74, .9f);
        return Gray(v);
    }

    static float Speck(int x, int y, float v, int seed, float threshold)
    {
        bool cluster = Fbm(x / 70f, y / 70f, 2, seed) > .58f;
        return cluster && Fbm(x / 2.2f, y / 2.2f, 2, seed + 1) > threshold ? v * .3f : v;
    }

    // ---- City backdrop: screen-printed skyline in greys; the scene tints it near-black ----

    static Color CityPixel(int x, int y, int w, int h)
    {
        float u = (float)x / w, t = (float)y / h;
        float v = .82f;                                       // sky
        if (Halftone(x, y, 7, 45, Mathf.Clamp01(.55f - t) * .9f)) v = .70f;
        if (Skyline(u, t, 31, .26f, .55f, .045f, .09f)) v = .66f;  // far towers
        if (Skyline(u, t, 32, .42f, .68f, .06f, .14f)) v = .50f;   // mid blocks
        if (RooftopKit(u, t, 33, .58f, .78f, .08f, .16f)) v = .36f; // near roofs with tanks and antennas
        if (PowerLine(x, y, w, h)) v = .30f;
        if (t > .90f + (Fbm(x / 9f, 0, 2, 34) - .5f) * .04f) v = .30f;
        if (v < .6f && Fbm(x / 2f, y / 2f, 2, 35) > .9f) v += .12f;   // worn ink
        return Gray(v);
    }

    static bool Skyline(float u, float t, int seed, float minTop, float maxTop, float minW, float maxW)
    {
        float edge = 0; int i = 0;
        while (edge <= u)
        {
            float next = edge + Mathf.Lerp(minW, maxW, Hash(i, 0, seed));
            if (u < next) return t > Mathf.Lerp(minTop, maxTop, Hash(i, 1, seed));
            edge = next; i++;
        }
        return false;
    }

    static bool RooftopKit(float u, float t, int seed, float minTop, float maxTop, float minW, float maxW)
    {
        float edge = -.02f; int i = 0;
        while (edge <= u)
        {
            float width = Mathf.Lerp(minW, maxW, Hash(i, 0, seed)), next = edge + width;
            if (u < next)
            {
                float top = Mathf.Lerp(minTop, maxTop, Hash(i, 1, seed)), local = (u - edge) / width;
                if (t > top) return true;
                float kind = Hash(i, 2, seed);
                if (kind < .35f)                                  // water tank on legs
                    return local > .2f && local < .45f && t > top - .09f && (t < top - .035f || local < .24f || local > .41f);
                if (kind < .6f)                                   // antenna
                    return Mathf.Abs(local - .7f) < .006f / width && t > top - .16f;
                if (kind < .8f)                                   // rooftop box
                    return local > .55f && local < .85f && t > top - .04f;
                return false;
            }
            edge = next; i++;
        }
        return false;
    }

    static bool PowerLine(int x, int y, int w, int h)
    {
        for (int line = 0; line < 3; line++)
        {
            float span = w * .38f, start = -w * .1f + line * w * .27f, poleY = h * (.36f + .05f * line);
            float local = Mathf.Repeat(x - start, span) / span;
            float sag = h * .07f * 4 * local * (1 - local);
            if (Mathf.Abs(y - (poleY + sag)) < 1.2f) return true;
        }
        return false;
    }

    // ---- Unknown enemy: an ink silhouette on a slate print, like an unsolved wanted poster ----

    static readonly Color Ink = new(.055f, .065f, .075f), Brick = new(.66f, .27f, .22f), Paper = new(.94f, .90f, .81f);

    static Color UnknownPortraitPixel(int x, int y, int w, int h)
    {
        var c = Slate;
        if (Halftone(x, y, 10, 45, (float)y / h * .55f)) c = Charcoal;
        if (Fbm(x / 2.5f, y / 2.5f, 2, 81) > .86f) c = Color.Lerp(c, Paper, .25f);
        // Hazard stripes across the top-right corner.
        if (x - (h - y) > w * .62f && Mathf.Repeat(x + y, 44) < 18) c = Ink;
        float jx = (Fbm(x / 7f, y / 7f, 2, 82) - .5f) * 6, jy = (Fbm(x / 7f, y / 7f, 2, 83) - .5f) * 6;
        if (Bust(x - 14 + jx, y - 10 + jy, w, h)) c = Brick;                 // misregistered red pass
        if (Bust(x + jx, y + jy, w, h)) c = Fbm(x / 2.2f, y / 2.2f, 2, 84) > .84f ? Color.Lerp(Ink, Slate, .5f) : Ink;
        c.a = 1; return c;
    }

    static bool Bust(float x, float y, int w, int h)
    {
        float cx = w * .5f;
        float hx = (x - cx) / (w * .145f), hy = (y - h * .40f) / (h * .175f);
        if (hx * hx + hy * hy < 1) return true;                                // head
        if (Mathf.Abs(x - cx) < w * .075f && y > h * .5f && y < h * .66f) return true;   // neck
        float top = h * .63f, spread = (y - top) / (h - top);
        if (y < top || y > h) return false;
        float half = w * (.17f + .26f * Mathf.Sqrt(Mathf.Clamp01(spread * 1.6f)));  // rounded shoulders
        return Mathf.Abs(x - cx) < half;
    }

    // ---- Ink accents (black on white; the Ink Accent shader turns white into transparency) ----

    // Cell: uvRect (.01, .79, .30, .20) of a 1792x2400 atlas.
    static float RoughCorner(int x, int y, int w, int h)
    {
        float cx = (x / (float)w - .01f) / .30f, cy = (y / (float)h - .01f) / .20f;
        if (cx < -.05f || cx > 1.05f || cy < -.05f || cy > 1.05f) return 0;
        float jx = (Fbm(x / 9f, y / 9f, 3, 41) - .5f) * .07f, jy = (Fbm(x / 9f, y / 9f, 3, 42) - .5f) * .07f;
        float px = cx + jx, py = cy + jy;
        bool bar = py > .06f && py < .30f && px > .04f && px < .96f - py * .5f;          // top bar, cut on a diagonal
        bool post = px > .04f && px < .20f && py > .06f && py < .92f - px * .4f;          // left post
        bool wedge = px > .04f && py > .06f && px + py < .58f;                             // filled inner corner
        if (!(bar || post || wedge)) return 0;
        return Fbm(x / 2.5f, y / 2.5f, 2, 43) > .8f || Fbm(x / 40f, y / 40f, 2, 44) > .72f ? 0 : 1;
    }

    // Cell: uvRect (.015, .69, .18, .29) of a 2912x1440 atlas.
    static float CleanCorner(int x, int y, int w, int h)
    {
        float cx = (x / (float)w - .015f) / .18f, cy = (y / (float)h - .02f) / .29f;
        if (cx < -.05f || cx > 1.05f || cy < -.05f || cy > 1.05f) return 0;
        float j = (Fbm(x / 14f, y / 14f, 2, 51) - .5f) * .025f;
        float px = cx + j, py = cy + j;
        bool bar = py > .08f && py < .20f && px > .08f && px < .92f;
        bool post = px > .08f && px < .20f && py > .08f && py < .92f;
        if (!(bar || post)) return 0;
        return Fbm(x / 3f, y / 3f, 2, 52) > .86f ? 0 : 1;
    }

    static Color InkOnWhite(float ink) => Gray(Mathf.Lerp(1f, .05f, ink));

    // ---- Gear slots: the sprite slices (8..121) of a 129px image ----

    static Color SlotPixel(int x, int y, Color rim, int border)
    {
        const int min = 8, max = 121;
        if (x < min || x > max || y < min || y > max) return Color.clear;
        int lx = x - min, ly = y - min, size = max - min;
        if (lx + (size - ly) > size * 2 - 18) return Color.clear;   // clipped top-right corner
        bool rimPixel = lx < border || ly < border || lx > size - border || ly > size - border
                        || lx + (size - ly) > size * 2 - 18 - border * 1.4f;
        var c = rimPixel ? rim : Charcoal;
        if (!rimPixel && Halftone(x, y, 6, 45, ly / (float)size * .35f)) c = Color.Lerp(c, Slate, .35f);
        if (Fbm(x / 2f, y / 2f, 2, rim == Ochre ? 61 : 62) > .88f) c *= .8f;
        c.a = 1; return c;
    }

    // ---- Shared primitives ----

    static bool Halftone(float x, float y, float cell, float angleDeg, float darkness)
    {
        if (darkness <= 0) return false;
        float a = angleDeg * Mathf.Deg2Rad;
        float u = (x * Mathf.Cos(a) + y * Mathf.Sin(a)) / cell, v = (-x * Mathf.Sin(a) + y * Mathf.Cos(a)) / cell;
        float du = u - Mathf.Floor(u) - .5f, dv = v - Mathf.Floor(v) - .5f;
        return du * du + dv * dv < Mathf.Clamp01(darkness) / Mathf.PI;
    }

    static float Fbm(float x, float y, int octaves, int seed)
    {
        float sum = 0, amp = 1, total = 0;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * ValueNoise(x, y, seed + o * 101);
            total += amp; amp *= .5f; x *= 2; y *= 2;
        }
        return sum / total;
    }

    static float ValueNoise(float x, float y, int seed)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }
    }

    static Color Gray(float v) { v = Mathf.Clamp01(v); return new Color(v, v, v, 1); }
}
