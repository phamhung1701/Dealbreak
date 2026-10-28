using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A Gear's tier (its authored rarity) as an aura round its item art, the one tier path for both the
/// Black Market tiles and the header Gear slots (owner 2026-10-28). Every part is the tier's own
/// colour (owner 2026-10-29: Rare cobalt, Epic violet, Legendary orange; theme rareAura/epicAura/
/// legendaryAura); teal stays selection only and red danger only. Common: nothing. Rare: a static frame. Epic: the
/// Each tier reads from its structure alone (owner 2026-10-28): Rare one solid frame; Epic a double
/// frame that breathes, with a long wire racing round; Legendary the double frame at full strength,
/// a soft halo outside it, a fast pulse, two wires and eight twinkling sparks. Under Reduce Motion
/// each tier keeps its static structure (Rare one frame, Epic the double frame, Legendary the double
/// frame and halo) with no breathing, wires, sparks or pulse. Black Market tiles also carry a small named tag
/// (ticket stub in the tier colour, inkPrint Barlow: ink reads 4.9:1 on cobalt and violet and 7.4:1
/// on orange, where paper would be 3.1:1 on the first two); header slots are too small for it. Parts stay inside the art's
/// margin, so a masked row never clips them, and none takes raycasts. Presentation only; it reads
/// the Gear and the clock, and allocates nothing per frame.
/// </summary>
public sealed class ItemTierFxUI : MonoBehaviour
{
    public enum Tier { Common, Rare, Epic, Legendary }

    const string IconName = "Authored Icon";
    public const string LayerName = "Tier FX", TagName = "Tier Tag";
    // Parts in order: outer frame (all tiers); inner frame (Epic, Legendary); halo (Legendary);
    // wires (Epic one, Legendary two); sparks (Legendary eight). Each frame or wire is four bars.
    public const int BarsPerFrame = 4, LegendarySparks = 8;
    // Thicknesses in pixels for art 84px wide (a 100px slot with its 8% margin), scaled with the art,
    // so the bigger Black Market tiles get proportionally thicker frames.
    const float ReferenceArt = 84f, Outer = 3f, Gap = 2f, Inner = 1.5f, Halo = 4f, Spark = 3f;
    const float RareAlpha = .85f, HaloAlpha = .25f, WireShare = .4f, TagHeight = 16f, TagTextSize = 13f;

    /// <summary>The structure each tier draws under Reduce Motion: (frames, halo).</summary>
    public static int StaticBars(Tier tier) => tier switch
    {
        Tier.Rare => BarsPerFrame,
        Tier.Epic => BarsPerFrame * 2,
        Tier.Legendary => BarsPerFrame * 3,
        _ => 0
    };

    /// <summary>Every aura part a tier draws (the Black Market tag aside).</summary>
    public static int PartCount(Tier tier) => tier switch
    {
        Tier.Rare => BarsPerFrame,
        Tier.Epic => BarsPerFrame * 3,
        Tier.Legendary => BarsPerFrame * 5 + LegendarySparks,
        _ => 0
    };

    int _inner = -1, _halo = -1, _wire = -1, _wire2 = -1, _sparks = -1;

    Tier _tier;
    bool _withTag;
    RectTransform _layer, _icon;
    readonly List<Graphic> _g = new();
    TMP_Text _tagText;
    Image _tag;
    Color _aura;
    static readonly Color RareFallback = new Color32(0x5B, 0x7F, 0xD6, 0xFF);
    static readonly Color EpicFallback = new Color32(0x9B, 0x6A, 0xD8, 0xFF);
    static readonly Color LegendaryFallback = new Color32(0xEE, 0x8A, 0x3A, 0xFF);

    /// <summary>The tier's aura colour from the theme (the same hexes when there is no theme); clear for Common.</summary>
    public static Color AuraColor(Tier tier)
    {
        var theme = DealbreakUIStyle.Theme;
        return tier switch
        {
            Tier.Rare => theme ? theme.rareAura : RareFallback,
            Tier.Epic => theme ? theme.epicAura : EpicFallback,
            Tier.Legendary => theme ? theme.legendaryAura : LegendaryFallback,
            _ => Color.clear
        };
    }
    float S, h, k;

    public Tier CurrentTier => _layer ? _tier : Tier.Common;
    public RectTransform Layer => _layer;

    public static Tier Of(GearData gear)
    {
        string rarity = gear != null ? gear.rarity : null;
        if (string.Equals(rarity, "Legendary", StringComparison.OrdinalIgnoreCase)) return Tier.Legendary;
        if (string.Equals(rarity, "Epic", StringComparison.OrdinalIgnoreCase)) return Tier.Epic;
        if (string.Equals(rarity, "Rare", StringComparison.OrdinalIgnoreCase)) return Tier.Rare;
        return Tier.Common;
    }

    /// <summary>
    /// Shows <paramref name="gear"/>'s tier round the item art in <paramref name="tile"/> (the parent
    /// of its "Authored Icon"); null or Common clears it. <paramref name="withTag"/> adds the named tag.
    /// </summary>
    public static void Show(Transform tile, GearData gear, bool withTag = true)
    {
        if (!tile) return;
        var tier = Of(gear);
        if (!tile.TryGetComponent(out ItemTierFxUI fx))
        {
            if (tier == Tier.Common) return;
            fx = tile.gameObject.AddComponent<ItemTierFxUI>();
        }
        fx.Set(tier, withTag);
    }

    void Set(Tier tier, bool withTag)
    {
        if (tier == _tier && withTag == _withTag && (_layer || tier == Tier.Common)) return;
        if (_layer)
        {
            if (Application.isPlaying) Destroy(_layer.gameObject);
            else DestroyImmediate(_layer.gameObject);
        }
        _layer = null;
        _tag = null;
        _tagText = null;
        _g.Clear();
        _tier = tier;
        _withTag = withTag;
        if (tier == Tier.Common) return;

        var theme = DealbreakUIStyle.Theme;
        _aura = AuraColor(tier);
        var ink = theme ? theme.inkPrint : new Color(.07f, .07f, .07f);

        var layerObject = new GameObject(LayerName, typeof(RectTransform));
        layerObject.layer = gameObject.layer;
        _layer = (RectTransform)layerObject.transform;
        _layer.SetParent(transform, false);
        _inner = _halo = _wire = _wire2 = _sparks = -1;
        Parts(BarsPerFrame);
        if (tier >= Tier.Epic) { _inner = _g.Count; Parts(BarsPerFrame); }
        if (tier == Tier.Legendary) { _halo = _g.Count; Parts(BarsPerFrame); }
        if (tier >= Tier.Epic) { _wire = _g.Count; Parts(BarsPerFrame); }
        if (tier == Tier.Legendary)
        {
            _wire2 = _g.Count; Parts(BarsPerFrame);
            _sparks = _g.Count; Parts(LegendarySparks);
        }
        foreach (var graphic in _g) graphic.color = Color.clear;

        if (!withTag) return;
        _tag = Part();
        if (theme && theme.tagSprite)
        {
            _tag.sprite = theme.tagSprite;
            _tag.type = Image.Type.Sliced;
        }
        _tag.color = _aura;
        var text = new GameObject(TagName, typeof(RectTransform), typeof(TextMeshProUGUI));
        text.layer = gameObject.layer;
        text.transform.SetParent(_layer, false);
        _tagText = text.GetComponent<TMP_Text>();
        if (theme) DealbreakUIStyle.Text(_tagText, theme.labelFont ? theme.labelFont : theme.headingFont, TagTextSize, ink);
        _tagText.fontSize = TagTextSize;
        _tagText.color = ink;
        _tagText.alignment = TextAlignmentOptions.Center;
        _tagText.textWrappingMode = TextWrappingModes.NoWrap;
        _tagText.overflowMode = TextOverflowModes.Overflow;
        _tagText.raycastTarget = false;
        _tagText.text = tier.ToString().ToUpperInvariant();
    }

    void LateUpdate()
    {
        if (!_layer) return;
        if (!_icon) _icon = transform.Find(IconName) as RectTransform;
        bool shown = _icon && _icon.gameObject.activeInHierarchy;
        if (_layer.gameObject.activeSelf != shown) _layer.gameObject.SetActive(shown);
        if (!shown) return;

        // The layer sits on the art, just above it.
        int icon = _icon.GetSiblingIndex(), layer = _layer.GetSiblingIndex();
        if (layer != icon + 1) _layer.SetSiblingIndex(layer < icon ? icon : icon + 1);
        if (_layer.anchorMin != _icon.anchorMin) _layer.anchorMin = _icon.anchorMin;
        if (_layer.anchorMax != _icon.anchorMax) _layer.anchorMax = _icon.anchorMax;
        if (_layer.pivot != _icon.pivot) _layer.pivot = _icon.pivot;
        if (_layer.sizeDelta != _icon.sizeDelta) _layer.sizeDelta = _icon.sizeDelta;
        if (_layer.anchoredPosition != _icon.anchoredPosition) _layer.anchoredPosition = _icon.anchoredPosition;
        S = Mathf.Min(_icon.rect.width, _icon.rect.height);
        h = S / 2f;
        k = S / ReferenceArt;
        float t = Time.unscaledTime;
        bool still = CutSheetMotion.ReduceMotion;
        var centre = Vector2.zero; // parts are placed from the layer's centre, which is the art's
        float outer = Outer * k, inner = Inner * k, halo = Halo * k;

        switch (_tier)
        {
            case Tier.Rare:
                Frame(0, centre, 0f, outer, RareAlpha);
                break;
            case Tier.Epic:
            {
                float breath = still ? 1f : .8f + .2f * Mathf.Sin(t * 2f);
                Frame(0, centre, 0f, outer, breath);
                Frame(_inner, centre, outer + Gap * k, inner, breath);
                Wire(_wire, centre, outer, t * S * 1.1f, S * 4f * WireShare, still ? 0f : 1f);
                break;
            }
            case Tier.Legendary:
            {
                float phase = Mathf.Repeat(t / .7f, 1f);
                float beat = still ? 0f : Beat(phase, 0f) + .7f * Beat(phase, .25f);
                Frame(0, centre, 0f, outer, 1f);
                Frame(_inner, centre, outer + Gap * k, inner, 1f);
                // The halo: a wider, faint band just outside the outer frame; the pulse lifts it.
                Frame(_halo, centre, -halo, halo, HaloAlpha + .3f * Mathf.Clamp01(beat));
                Wire(_wire, centre, outer, t * S * 1.5f, S * 4f * WireShare, still ? 0f : 1f);
                Wire(_wire2, centre, outer, -t * S * 1.5f + S * 2f, S * 4f * WireShare * .6f, still ? 0f : .85f);
                int flick = Mathf.FloorToInt(t / .12f);
                float size = Spark * k;
                // Eight sparks round the frame: the corners and the edge midpoints, inside the halo.
                float ring = h + halo * .5f;
                for (int i = 0; i < LegendarySparks; i++)
                {
                    float sx = i switch { 0 or 6 or 7 => -1f, 2 or 3 or 4 => 1f, _ => 0f };
                    float sy = i switch { 0 or 1 or 2 => 1f, 4 or 5 or 6 => -1f, _ => 0f };
                    Put(_g[_sparks + i], centre.x + sx * ring, centre.y + sy * ring, size, size, 45f);
                    Tint(_g[_sparks + i], still ? 0f : Hash(flick * LegendarySparks + i) > .45f ? 1f : 0f);
                }
                break;
            }
        }

        if (!_tag) return;
        float tagWidth = _tagText.preferredWidth + 10f;
        var tagAt = centre + new Vector2(-h + tagWidth / 2f, -h);
        Put(_tag, tagAt.x, tagAt.y, tagWidth, TagHeight);
        Put(_tagText, tagAt.x, tagAt.y + .5f, tagWidth, TagHeight);
    }

    // ---- Looks.

    // A box of four bars `thickness` wide, its outer edge `inset` inside the art's edge (a negative
    // inset puts it outside, in the art's margin). The side bars stop short of the top and bottom
    // bars, so the corners never print twice.
    void Frame(int first, Vector2 c, float inset, float thickness, float alpha)
    {
        if (first < 0) return;
        float edge = h - inset, size = edge * 2f, mid = edge - thickness / 2f;
        Put(_g[first], c.x, c.y + mid, size, thickness);
        Put(_g[first + 1], c.x, c.y - mid, size, thickness);
        Put(_g[first + 2], c.x - mid, c.y, thickness, size - thickness * 2f);
        Put(_g[first + 3], c.x + mid, c.y, thickness, size - thickness * 2f);
        for (int i = 0; i < BarsPerFrame; i++) Tint(_g[first + i], alpha);
    }

    // A lit segment running round the outer frame: `head` is its distance along the art's perimeter,
    // clockwise from top-left; it rides the outer frame's centre line, a little thicker than it.
    void Wire(int first, Vector2 c, float frame, float head, float length, float alpha)
    {
        if (first < 0) return;
        float perimeter = S * 4f;
        float start = Mathf.Repeat(head, perimeter);
        float thick = frame * 1.4f, line = h - frame / 2f;
        for (int edge = 0; edge < 4; edge++)
        {
            float edgeStart = edge * S;
            float a = -1f, b = -1f;
            // The segment, and its wrap past the perimeter's end (no per-frame array).
            for (int pass = 0; pass < 2; pass++)
            {
                float shift = pass == 0 ? 0f : -perimeter;
                float from = Mathf.Max(start + shift, edgeStart), to = Mathf.Min(start + shift + length, edgeStart + S);
                if (to > from) { a = from - edgeStart; b = to - edgeStart; }
            }
            var bar = _g[first + edge];
            if (a < 0f || alpha <= 0f)
            {
                Tint(bar, 0f);
                continue;
            }
            float mid = (a + b) / 2f - h, span = b - a;
            switch (edge)
            {
                case 0: Put(bar, c.x + mid, c.y + line, span, thick); break;
                case 1: Put(bar, c.x + line, c.y - mid, thick, span); break;
                case 2: Put(bar, c.x - mid, c.y - line, span, thick); break;
                default: Put(bar, c.x - line, c.y + mid, thick, span); break;
            }
            Tint(bar, alpha);
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
        go.layer = gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(_layer, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        _g.Add(image);
        return image;
    }

    // Writes only what changed: each write dirties the part's layout or mesh.
    static void Put(Graphic graphic, float x, float y, float width, float height, float angle = 0f)
    {
        var rect = graphic.rectTransform;
        var position = new Vector2(x, y);
        var size = new Vector2(width, height);
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        var rotation = Quaternion.Euler(0f, 0f, angle);
        if (rect.localRotation != rotation) rect.localRotation = rotation;
    }

    // Every aura part is the tier's colour; only its alpha moves.
    void Tint(Graphic graphic, float alpha)
    {
        var color = _aura;
        color.a *= Mathf.Clamp01(alpha);
        if (graphic.color != color) graphic.color = color;
    }

    static float Beat(float phase, float centre)
    {
        float d = (Mathf.Repeat(phase - centre + .5f, 1f) - .5f) / .07f;
        return Mathf.Exp(-d * d);
    }

    static float Hash(int n) => Mathf.Repeat(Mathf.Sin(n * 12.9898f) * 43758.5453f, 1f);

    /// <summary>Runs one frame of the aura now (tests and immediate rebinds).</summary>
    public void Tick() => LateUpdate();
}
