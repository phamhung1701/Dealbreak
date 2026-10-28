using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A Gear's tier (its authored rarity) on its Black Market tile, drawn over the item art: Common stays
/// plain; Rare, Epic and Legendary each get their own look and a small named tag on the art's foot.
/// Presentation only; it reads the offer and the clock.
/// </summary>
public sealed class ItemTierFxUI : MonoBehaviour
{
    public enum Tier { Common, Rare, Epic, Legendary }

    const string IconName = "Authored Icon";

    Tier _tier;
    RectTransform _layer, _icon;
    readonly List<Graphic> _g = new();
    TMP_Text _tagText;
    Image _tag;
    Color _red, _lime, _paper, _ink;
    float S, h, th;

    public static Tier Of(GearData gear)
    {
        string rarity = gear != null ? gear.rarity : null;
        if (string.Equals(rarity, "Legendary", StringComparison.OrdinalIgnoreCase)) return Tier.Legendary;
        if (string.Equals(rarity, "Epic", StringComparison.OrdinalIgnoreCase)) return Tier.Epic;
        if (string.Equals(rarity, "Rare", StringComparison.OrdinalIgnoreCase)) return Tier.Rare;
        return Tier.Common;
    }

    /// <summary>Shows <paramref name="gear"/>'s tier over the item art in <paramref name="tile"/>; null clears it.</summary>
    public static void Show(Transform tile, GearData gear)
    {
        var tier = Of(gear);
        if (!tile.TryGetComponent(out ItemTierFxUI fx))
        {
            if (tier == Tier.Common) return;
            fx = tile.gameObject.AddComponent<ItemTierFxUI>();
        }
        fx.Set(tier);
    }

    void Set(Tier tier)
    {
        if (tier == _tier && _layer) return;
        if (_layer)
        {
            if (Application.isPlaying) Destroy(_layer.gameObject);
            else DestroyImmediate(_layer.gameObject);
        }
        _layer = null;
        _g.Clear();
        _tier = tier;
        if (tier == Tier.Common) return;

        var theme = DealbreakUIStyle.Theme;
        _red = theme ? theme.brick : new Color(.9f, .2f, .17f);
        _lime = theme ? theme.ochre : new Color(.8f, 1f, .2f);
        _paper = theme ? theme.paper : new Color(.94f, .92f, .86f);
        _ink = theme ? theme.inkPrint : new Color(.05f, .05f, .06f);

        _layer = (RectTransform)new GameObject("Tier FX", typeof(RectTransform)).transform;
        _layer.SetParent(transform, false);
        // Frame 0..3, glint 4..5, then per tier: Epic one wire (6..9), Legendary two wires (6..13) and sparks (14..17).
        Parts(6);
        if (tier == Tier.Epic) Parts(4);
        if (tier == Tier.Legendary) Parts(12);

        _tag = Part();
        var text = new GameObject("Tier Tag", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(_layer, false);
        _tagText = text.GetComponent<TMP_Text>();
        if (theme) DealbreakUIStyle.Text(_tagText, theme.labelFont ? theme.labelFont : theme.headingFont, 13, _ink);
        _tagText.fontSize = 13;
        _tagText.alignment = TextAlignmentOptions.Center;
        _tagText.textWrappingMode = TextWrappingModes.NoWrap;
        _tagText.overflowMode = TextOverflowModes.Overflow;
        _tagText.raycastTarget = false;
        _tagText.text = tier.ToString().ToUpperInvariant();
        _tag.color = tier switch { Tier.Rare => _paper, Tier.Epic => _lime, _ => _red };
        _tagText.color = tier == Tier.Legendary ? _paper : _ink;
        foreach (var graphic in _g) if (graphic != _tag) graphic.color = Color.clear;
    }

    void LateUpdate()
    {
        if (!_layer) return;
        if (!_icon) _icon = transform.Find(IconName) as RectTransform;
        bool shown = _icon && _icon.gameObject.activeInHierarchy;
        _layer.gameObject.SetActive(shown);
        if (!shown) return;

        // The layer sits on the art, just above it.
        _layer.SetSiblingIndex(_icon.GetSiblingIndex() + 1);
        _layer.anchorMin = _icon.anchorMin;
        _layer.anchorMax = _icon.anchorMax;
        _layer.pivot = _icon.pivot;
        _layer.sizeDelta = _icon.sizeDelta;
        _layer.anchoredPosition = _icon.anchoredPosition;
        S = Mathf.Min(_icon.rect.width, _icon.rect.height);
        h = S / 2f;
        th = Mathf.Max(1.5f, S * .022f);
        float t = Time.unscaledTime;
        var centre = Vector2.zero; // parts are placed from the layer's centre, which is the art's

        switch (_tier)
        {
            case Tier.Rare:
                Frame(0, centre, _paper, .5f);
                Glint(4, centre, t, 3.6f, .3f);
                break;
            case Tier.Epic:
                Frame(0, centre, _lime, .35f + .15f * Mathf.Sin(t * 2f));
                Glint(4, centre, t, 2.8f, .32f);
                Wire(6, centre, t * S * 1.1f, S * .6f, _lime, 1f);
                break;
            case Tier.Legendary:
                float beat = Beat(Mathf.Repeat(t / 1.1f, 1f), 0f) + .7f * Beat(Mathf.Repeat(t / 1.1f, 1f), .2f);
                Frame(0, centre, _red, .4f + .5f * beat);
                Glint(4, centre, t, 2.4f, .36f);
                Wire(6, centre, t * S * 1.3f, S * .55f, _red, 1f);
                Wire(10, centre, -t * S * 1.3f + S * 2f, S * .4f, _lime, 1f);
                int flick = Mathf.FloorToInt(t / .14f);
                for (int i = 0; i < 4; i++)
                {
                    float sx = i < 2 ? -1f : 1f, sy = i % 2 == 0 ? -1f : 1f;
                    float size = th * 2.2f;
                    var at = centre + new Vector2(sx * (h + th * 2.5f), sy * (h + th * 2.5f));
                    Put(_g[14 + i], at.x, at.y, size, size, 45f);
                    Tint(_g[14 + i], i % 2 == 0 ? _red : _lime, Hash(flick * 4 + i) > .5f ? .95f : 0f);
                }
                break;
        }

        float tagWidth = _tagText.GetPreferredValues(_tagText.text).x + 10f, tagHeight = 16f;
        var tagAt = centre + new Vector2(-h + tagWidth / 2f, -h);
        Put(_tag, tagAt.x, tagAt.y, tagWidth, tagHeight);
        Put(_tagText, tagAt.x, tagAt.y + .5f, tagWidth, tagHeight);
    }

    // ---- Looks.

    // A hairline box round the art.
    void Frame(int first, Vector2 c, Color color, float alpha)
    {
        Put(_g[first], c.x, c.y + h - th / 2f, S, th);
        Put(_g[first + 1], c.x, c.y - h + th / 2f, S, th);
        Put(_g[first + 2], c.x - h + th / 2f, c.y, th, S);
        Put(_g[first + 3], c.x + h - th / 2f, c.y, th, S);
        for (int i = 0; i < 4; i++) Tint(_g[first + i], color, alpha);
    }

    // A shine that crosses the art now and then, kept inside its box.
    void Glint(int first, Vector2 c, float t, float period, float alpha)
    {
        float p = Mathf.Repeat(t / period, 1f) / .4f;
        for (int k = 0; k < 2; k++)
        {
            float width = S * (k == 0 ? .16f : .06f);
            float x = Mathf.Lerp(-h - width, h + width + S * .14f, p) - k * S * .14f;
            float left = Mathf.Max(x - width / 2f, -h), right = Mathf.Min(x + width / 2f, h);
            bool on = p <= 1f && right > left;
            Put(_g[first + k], c.x + (left + right) / 2f, c.y, Mathf.Max(0f, right - left), S);
            Tint(_g[first + k], _paper, on ? alpha * Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI) : 0f);
        }
    }

    // A lit segment running round the box: `head` is its distance along the perimeter, clockwise from top-left.
    void Wire(int first, Vector2 c, float head, float length, Color color, float alpha)
    {
        float perimeter = S * 4f;
        float start = Mathf.Repeat(head, perimeter);
        float thick = th * 1.6f;
        for (int edge = 0; edge < 4; edge++)
        {
            float edgeStart = edge * S;
            float a = -1f, b = -1f;
            foreach (float shift in new[] { 0f, -perimeter })
            {
                float from = Mathf.Max(start + shift, edgeStart), to = Mathf.Min(start + shift + length, edgeStart + S);
                if (to > from) { a = from - edgeStart; b = to - edgeStart; }
            }
            var bar = _g[first + edge];
            if (a < 0f)
            {
                Tint(bar, color, 0f);
                continue;
            }
            float mid = (a + b) / 2f - h, span = b - a;
            switch (edge)
            {
                case 0: Put(bar, c.x + mid, c.y + h, span, thick); break;
                case 1: Put(bar, c.x + h, c.y - mid, thick, span); break;
                case 2: Put(bar, c.x - mid, c.y - h, span, thick); break;
                default: Put(bar, c.x - h, c.y + mid, thick, span); break;
            }
            Tint(bar, color, alpha);
        }
    }

    // ---- Helpers.

    void Parts(int count)
    {
        for (int i = 0; i < count; i++) Part();
    }

    Image Part()
    {
        var go = new GameObject("Part", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(_layer, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        _g.Add(image);
        return image;
    }

    static void Put(Graphic graphic, float x, float y, float width, float height, float angle = 0f)
    {
        var rect = graphic.rectTransform;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    static void Tint(Graphic graphic, Color color, float alpha)
    {
        color.a *= Mathf.Clamp01(alpha);
        graphic.color = color;
    }

    static float Beat(float phase, float centre)
    {
        float d = (Mathf.Repeat(phase - centre + .5f, 1f) - .5f) / .07f;
        return Mathf.Exp(-d * d);
    }

    static float Hash(int n) => Mathf.Repeat(Mathf.Sin(n * 12.9898f) * 43758.5453f, 1f);
}
