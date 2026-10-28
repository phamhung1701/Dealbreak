using UnityEngine;

/// <summary>
/// A flat shield glyph drawn in code (a plain geometric shape, no artwork): off-white face, ink
/// rim. Used to count an enemy's Shield charges, one glyph per charge.
/// </summary>
public static class ShieldGlyph
{
    const int Size = 64;
    const int Samples = 4;
    static Sprite _sprite;

    // Heater-shield outline in unit space, y up: a slightly peaked top, straight sides, a point.
    static readonly Vector2[] Outline =
    {
        new(.10f, .88f), new(.50f, .98f), new(.90f, .88f), new(.90f, .52f), new(.50f, .03f), new(.10f, .52f)
    };

    public static Sprite Sprite
    {
        get
        {
            if (_sprite) return _sprite;
            var theme = DealbreakUIStyle.Theme;
            Color face = theme ? theme.paper : new Color(.93f, .92f, .89f);
            Color rim = theme ? theme.inkPrint : new Color(.04f, .04f, .05f);
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Shield Glyph", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int inside = 0, core = 0;
                for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                {
                    var point = new Vector2((x + (sx + .5f) / Samples) / Size, (y + (sy + .5f) / Samples) / Size);
                    if (!Contains(point, 0f)) continue;
                    inside++;
                    if (Contains(point, .09f)) core++;
                }
                float coverage = inside / (float)(Samples * Samples);
                float faceShare = inside > 0 ? core / (float)inside : 0f;
                var color = Color.Lerp(rim, face, faceShare);
                color.a = coverage;
                pixels[y * Size + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100f);
            _sprite.name = "Shield Glyph";
            return _sprite;
        }
    }

    // Inside the outline shrunk towards its centre by `inset` (the rim's width).
    static bool Contains(Vector2 point, float inset)
    {
        var centre = new Vector2(.5f, .58f);
        if (inset > 0f) point = centre + (point - centre) / (1f - inset * 2f);
        bool inside = false;
        for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
        {
            var a = Outline[i];
            var b = Outline[j];
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }
}
