using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A Mod's own ambient look on a card that carries it, picked by <see cref="CardEnhancementData.visual"/>:
/// flat shapes drawn in code over (for an echo, behind) the card art, sized to the drawn art.
/// A Mod trigger replays the look louder. While the card plays its Signature (Overclock), the look runs
/// faster and brighter and two wires race round the card. Presentation only; it reads the card and the
/// clock, never gameplay RNG.
/// </summary>
[RequireComponent(typeof(CardView))]
public sealed class CardModFxUI : MonoBehaviour
{
    CardView _view;
    RectTransform _front, _backMirror, _back;
    CardModVisual _built;
    bool _signature;
    int _wire = -1;
    readonly List<Graphic> _g = new();
    readonly List<RectTransform> _groups = new();
    Color _red, _lime, _paper, _ink;
    float _clock, _burst, _roam, _dim = 1f;
    float W, H, hx, hy, th;

    static Sprite _disc, _ring;

    public static void Attach(CardView view)
    {
        if (view && view.face && !view.TryGetComponent(out CardModFxUI _))
            view.gameObject.AddComponent<CardModFxUI>();
    }

    /// <summary>One of the card's Mods triggered: the look replays from its start, louder for a moment.</summary>
    public void Burst()
    {
        _burst = 1f;
        _clock = 0f;
    }

    void Awake()
    {
        _view = GetComponent<CardView>();
        // A hand of the same Mod should not pulse in unison.
        _clock = (float)new System.Random().NextDouble() * 10f;
    }

    void OnDestroy() => Clear();

    void LateUpdate()
    {
        var enhancement = _view.data != null ? _view.data.Enhancement : null;
        var visual = enhancement != null ? enhancement.visual : CardModVisual.None;
        bool signature = enhancement != null && ModSignature.Plays(enhancement);
        if (visual != _built || signature != _signature || visual != CardModVisual.None && _front == null)
            Build(visual, signature);
        if (visual == CardModVisual.None || _view.face == null) return;

        float dt = Time.unscaledDeltaTime * (_signature ? 1.5f : 1f);
        _clock += dt;
        _burst = Mathf.Max(0f, _burst - dt * 1.6f);
        _dim = _view.face.color.a;
        Fit();
        switch (visual)
        {
            case CardModVisual.EchoFrame: EchoFrame(); break;
            case CardModVisual.BoostChevrons: BoostChevrons(); break;
            case CardModVisual.Reticle: Reticle(dt); break;
            case CardModVisual.TwinSlash: TwinSlash(); break;
            case CardModVisual.Overheat: Overheat(); break;
            case CardModVisual.ArmorPlates: ArmorPlates(); break;
            case CardModVisual.ClawMarks: ClawMarks(); break;
            case CardModVisual.CreditChips: CreditChips(); break;
            case CardModVisual.LoadBar: LoadBar(); break;
            case CardModVisual.HealPulse: HealPulse(); break;
            case CardModVisual.Shockwave: Shockwave(); break;
            case CardModVisual.SuitCycle: SuitCycle(); break;
        }
        if (_signature) Wires();
    }

    // ---- Signature (Overclock): a red and a lime wire race round the card, just outside its edge.

    void Wires()
    {
        float perimeter = 2f * (W + H);
        Wire(_wire, _clock * (W + H) * .9f, perimeter * .16f, _red);
        Wire(_wire + 4, -_clock * (W + H) * .9f + perimeter * .5f, perimeter * .11f, _lime);
    }

    // `head` is the segment's distance along the edge, clockwise from the top-left corner.
    void Wire(int first, float head, float length, Color color)
    {
        float perimeter = 2f * (W + H);
        float start = Mathf.Repeat(head, perimeter);
        float gap = th * 1.5f, thick = th * 1.2f;
        float edgeStart = 0f;
        for (int edge = 0; edge < 4; edge++)
        {
            float edgeLength = edge % 2 == 0 ? W : H;
            float a = -1f, b = -1f;
            for (int pass = 0; pass < 2; pass++)
            {
                float shift = pass == 0 ? 0f : -perimeter;
                float from = Mathf.Max(start + shift, edgeStart), to = Mathf.Min(start + shift + length, edgeStart + edgeLength);
                if (to > from) { a = from - edgeStart; b = to - edgeStart; }
            }
            edgeStart += edgeLength;
            var bar = _g[first + edge];
            if (a < 0f)
            {
                Tint(bar, color, 0f);
                continue;
            }
            float mid = (a + b) / 2f, span = b - a;
            switch (edge)
            {
                case 0: Put(bar, -hx + mid, hy + gap, span, thick); break;
                case 1: Put(bar, hx + gap, hy - mid, thick, span); break;
                case 2: Put(bar, hx - mid, -hy - gap, span, thick); break;
                default: Put(bar, -hx - gap, -hy + mid, thick, span); break;
            }
            Tint(bar, color, .95f);
        }
    }

    // ---- Looks. Coordinates are from the art's centre; W x H is the drawn art.

    // Backup: two spare cards stand behind it, red-edged, breathing out from under it.
    void EchoFrame()
    {
        float breathe = .5f + .5f * Mathf.Sin(_clock * 2.4f);
        for (int k = 0; k < 2; k++)
        {
            int i = (1 - k) * 5; // the far echo was built first, so it sits behind
            float step = (k + 1) * (W * .045f + breathe * W * .015f) * (1f + _burst * .8f);
            var offset = new Vector2(-step * .6f, step);
            Put(_g[i], offset.x, offset.y, W, H);
            Tint(_g[i], _ink, k == 0 ? .9f : .65f);
            Frame(i + 1, offset, W, H, th * .8f, _red, (k == 0 ? .9f : .45f) * (.7f + .3f * breathe) + _burst * .3f);
        }
    }

    // Boosted: red chevrons climb off the top edge like thrust.
    void BoostChevrons()
    {
        float speed = .9f + _burst * 2.5f;
        for (int i = 0; i < 3; i++)
        {
            float p = Mathf.Repeat(_clock * speed + i / 3f, 1f);
            var group = _groups[i];
            group.anchoredPosition = new Vector2(0f, hy + W * .04f + p * W * .3f);
            group.localScale = Vector3.one * (1f - p * .3f);
            Chevron(i * 2, W * .15f * (1f + _burst * .4f), th * 1.5f, _red, Mathf.Sin(p * Mathf.PI) * (.85f + _burst * .15f));
        }
    }

    // Deadeye: a lime reticle roams the art, then snaps tight and red on its mark.
    void Reticle(float dt)
    {
        const float period = 2.8f;
        float p = Mathf.Repeat(_clock / period, 1f);
        float locked = Mathf.Max(Smooth(.62f, .72f, p) * (1f - Smooth(.92f, 1f, p)), _burst);
        _roam += dt * (1f - locked);
        var at = new Vector2(Mathf.Sin(_roam * 1.3f) * W * .22f, Mathf.Sin(_roam * .9f + 1f) * H * .22f);
        at = Vector2.Lerp(at, Vector2.zero, _burst);
        float size = Mathf.Lerp(W * .46f, W * .24f, locked);
        float blink = p > .72f && p < .92f && Mathf.Repeat(_clock * 12f, 1f) < .5f ? .55f : 1f;
        var color = Color.Lerp(_lime, _red, locked);
        float arm = size * .32f;
        int g = 0;
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        {
            Put(_g[g], at.x + sx * (size / 2f - arm / 2f), at.y + sy * size / 2f, arm, th);
            Tint(_g[g++], color, (.75f + .25f * locked) * blink);
            Put(_g[g], at.x + sx * size / 2f, at.y + sy * (size / 2f - arm / 2f), th, arm);
            Tint(_g[g++], color, (.75f + .25f * locked) * blink);
        }
        float dot = th * 2.4f * locked;
        Put(_g[g], at.x, at.y, dot, dot);
        Tint(_g[g], _red, locked);
    }

    // Double Strike: two quick slashes cross the art, one after the other.
    void TwinSlash()
    {
        float s = Mathf.Repeat(_clock, 2.4f);
        for (int k = 0; k < 2; k++)
        {
            float since = s - k * .16f;
            const float life = .55f;
            if (since < 0f || since > life)
            {
                Tint(_g[k * 2], _red, 0f);
                Tint(_g[k * 2 + 1], _paper, 0f);
                continue;
            }
            float angle = k == 0 ? 32f : -32f;
            var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float full = W * 1.05f / dir.x;
            float length = full * Smooth(0f, .1f, since);
            float fade = 1f - Smooth(.18f, life, since);
            var centre = -dir * full / 2f + dir * length / 2f;
            float thick = th * (1f + 1.4f * fade);
            Put(_g[k * 2], centre.x + 2f, centre.y - 2f, length, thick * 1.7f, angle);
            Tint(_g[k * 2], _red, .85f * fade);
            Put(_g[k * 2 + 1], centre.x, centre.y, length, thick, angle);
            Tint(_g[k * 2 + 1], _paper, fade);
        }
    }

    // Overclocked: a red edge beats like a racing heart; heat bars flicker at the foot.
    void Overheat()
    {
        float q = Mathf.Repeat(_clock / 1.05f, 1f);
        float heat = Mathf.Clamp01(Beat(q, 0f, .07f) + .7f * Beat(q, .2f, .07f) + _burst);
        float thick = th * (1.2f + heat * 1.3f);
        float jitter = heat > .5f ? Mathf.Sin(_clock * 90f) : 0f;
        Frame(0, new Vector2(jitter, 0f), W - th, H - th, thick, _red, .3f + .6f * heat);
        for (int i = 0; i < 5; i++)
        {
            float height = W * (.05f + .13f * Mathf.PerlinNoise(i * 3.7f, _clock * 6f)) * (.6f + .4f * heat);
            Put(_g[4 + i], (i - 2) * W * .075f, -hy + th + thick + height / 2f + 2f, W * .04f, height);
            Tint(_g[4 + i], _red, .75f);
        }
    }

    // Plated: off-white armour brackets clamp the corners; a glint runs across the art.
    void ArmorPlates()
    {
        float plate = th * 2.2f * (1f + _burst * .3f);
        float arm = W * .2f * (1f + _burst * .15f);
        float gap = th * 1.5f;
        int g = 0;
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        {
            float edgeX = hx + gap + plate, edgeY = hy + gap + plate;
            Put(_g[g], sx * (edgeX - arm / 2f), sy * (edgeY - plate / 2f), arm, plate);
            Tint(_g[g++], _paper, .92f);
            Put(_g[g], sx * (edgeX - plate / 2f), sy * (edgeY - arm / 2f), plate, arm);
            Tint(_g[g++], _paper, .92f);
            Put(_g[g], sx * (edgeX - plate / 2f), sy * (edgeY - plate / 2f), plate * .45f, plate * .45f);
            Tint(_g[g++], _ink, 1f);
        }
        float p = Mathf.Repeat(_clock / 3.4f, 1f) / .35f;
        if (_burst > 0f) p = 1f - _burst;
        for (int k = 0; k < 2; k++, g++)
        {
            float width = W * (k == 0 ? .14f : .05f);
            float centre = Mathf.Lerp(-hx - width, hx + width + W * .12f, p) - k * W * .12f;
            float left = Mathf.Max(centre - width / 2f, -hx), right = Mathf.Min(centre + width / 2f, hx);
            bool on = p <= 1f && right > left;
            Put(_g[g], (left + right) / 2f, 0f, Mathf.Max(0f, right - left), H);
            Tint(_g[g], _paper, on ? .3f * Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI) + _burst * .3f : 0f);
        }
    }

    // Predatory: three red claw marks rake the upper art, then fade.
    void ClawMarks()
    {
        float s = Mathf.Repeat(_clock, 3.2f);
        float fade = 1f - Smooth(1.6f, 2.6f, s);
        const float angle = 242f;
        var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        for (int k = 0; k < 3; k++)
        {
            float length = W * .42f * Smooth(0f, .12f, s - .1f - k * .09f);
            var top = new Vector2(W * .02f + k * W * .1f, hy - H * .1f);
            var centre = top + dir * length / 2f;
            float thick = th * (1.6f + _burst);
            Put(_g[k * 2], centre.x + 1.5f, centre.y - 1.5f, length, thick, angle);
            Tint(_g[k * 2], _ink, .5f * fade);
            Put(_g[k * 2 + 1], centre.x, centre.y, length, thick, angle);
            Tint(_g[k * 2 + 1], _red, fade);
        }
    }

    // Profitable: credit chips flip up off the top edge.
    void CreditChips()
    {
        for (int i = 0; i < 3; i++)
        {
            const float cycle = 2.6f;
            float local = _clock + i * .87f;
            int round = Mathf.FloorToInt(local / cycle);
            float p = Mathf.Repeat(local / cycle, 1f) / .55f;
            var chip = _g[i];
            if (p > 1f)
            {
                Tint(chip, Color.white, 0f);
                continue;
            }
            float lane = Hash(round * 3 + i);
            float size = W * .17f * (1f + _burst * .3f);
            Put(chip, (lane - .5f) * W * .6f + (Hash(round * 3 + i + 101) - .5f) * W * .12f * p,
                hy - W * .05f + W * .32f * (1f - (1f - p) * (1f - p)), size, size);
            chip.rectTransform.localScale = new Vector3(Mathf.Max(.15f, Mathf.Abs(Mathf.Cos(p * Mathf.PI * 3f))), 1f, 1f);
            Tint(chip, _disc == ((Image)chip).sprite ? _lime : Color.white, Smooth(0f, .15f, p) * (1f - Smooth(.65f, 1f, p)));
        }
    }

    // Quickload: a segmented lime bar loads along the top edge, flashes full, reloads.
    void LoadBar()
    {
        float p = Mathf.Repeat(_clock / 2f, 1f);
        float fill = _burst > 0f ? 8f : Smooth(0f, .7f, p) * 8f;
        float flash = _burst > 0f ? _burst : p > .7f ? 1f - (p - .7f) / .3f : 0f;
        float y = hy + th * 3.5f, height = th * 2f, gap = th * .8f;
        float segment = (W - gap * 7f) / 8f;
        Put(_g[0], 0f, y, W, height);
        Tint(_g[0], _lime, .18f);
        for (int j = 0; j < 8; j++)
        {
            Put(_g[1 + j], -hx + segment / 2f + j * (segment + gap), y, segment, height * (1f + flash * .5f));
            float alpha = j < Mathf.FloorToInt(fill + .001f) ? .85f + .15f * flash
                : j == Mathf.FloorToInt(fill) && Mathf.Repeat(_clock * 8f, 1f) < .5f ? .35f : 0f;
            Tint(_g[1 + j], _lime, alpha);
        }
    }

    // Regenerative: a calm lime edge breathes while plus signs drift up through the art.
    void HealPulse()
    {
        float breathe = .5f + .5f * Mathf.Sin(_clock * 1.6f);
        Frame(0, Vector2.zero, W - th * 2f, H - th * 2f, th, _lime, .12f + .2f * breathe + _burst * .5f);
        float[] lanes = { -.28f, .12f, -.05f, .3f };
        for (int i = 0; i < 4; i++)
        {
            float p = Mathf.Repeat(_clock / 2.8f + i * .25f, 1f);
            var group = _groups[i];
            group.anchoredPosition = new Vector2(lanes[i] * W, Mathf.Lerp(-hy * .7f, hy * .75f, p));
            float size = W * .11f * (1f + _burst * .5f) * (.7f + .3f * Mathf.Sin(p * Mathf.PI));
            float alpha = Mathf.Sin(p * Mathf.PI) * .9f;
            Put(_g[4 + i * 2], 0f, 0f, size, size * .32f);
            Tint(_g[4 + i * 2], _lime, alpha);
            Put(_g[5 + i * 2], 0f, 0f, size * .32f, size);
            Tint(_g[5 + i * 2], _lime, alpha);
        }
    }

    // Volatile: red shockwaves burst out past the card; sparks spit from its edge.
    void Shockwave()
    {
        float s = Mathf.Repeat(_clock, 2.2f);
        for (int k = 0; k < 2; k++)
        {
            float q = (s - k * .22f) / 1f;
            bool on = q >= 0f && q <= 1f;
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(q), 3f);
            float size = Mathf.Lerp(W * .25f, W * 1.7f, ease) * (1f + _burst * .3f);
            Put(_g[k], 0f, 0f, size, size);
            Tint(_g[k], _red, on ? (1f - q) * .8f : 0f);
        }
        int flick = Mathf.FloorToInt(_clock / .15f);
        for (int i = 0; i < 4; i++)
        {
            float u = Hash(flick * 4 + i) * 4f;
            int side = Mathf.FloorToInt(u);
            float along = (u - side) * 2f - 1f;
            var at = side switch
            {
                0 => new Vector2(along * hx, hy),
                1 => new Vector2(hx, along * hy),
                2 => new Vector2(along * hx, -hy),
                _ => new Vector2(-hx, along * hy)
            };
            float size = th * (1.6f + _burst * 1.5f);
            Put(_g[2 + i], at.x, at.y, size, size, 45f);
            Tint(_g[2 + i], _red, Hash(flick * 4 + i + 57) > .45f ? .9f : 0f);
        }
    }

    // Wildcard: a tag on the top edge flips through all four suits.
    void SuitCycle()
    {
        float step = .55f / (1f + _burst * 5f);
        int round = Mathf.FloorToInt(_clock / step);
        float local = _clock - round * step;
        int suit = ((round % 4) + 4) % 4;
        float size = W * .24f;
        var at = new Vector2(0f, hy + size * .1f);
        Put(_g[0], at.x, at.y, size, size);
        Tint(_g[0], _ink, .95f);
        Frame(1, at, size, size, Mathf.Max(1.5f, th * .7f), _paper, .9f);
        var glyph = (TMP_Text)_g[5];
        glyph.text = suit switch { 0 => "\u2660", 1 => "\u2665", 2 => "\u2666", _ => "\u2663" };
        glyph.fontSize = size * .72f;
        Put(glyph, at.x, at.y, size, size);
        glyph.rectTransform.localScale = new Vector3(1f, Smooth(0f, .09f, local), 1f);
        Tint(glyph, suit == 1 || suit == 2 ? _red : _paper, 1f);
    }

    // ---- Building.

    void Build(CardModVisual visual, bool signature)
    {
        Clear();
        _built = visual;
        _signature = signature;
        if (visual == CardModVisual.None || _view.face == null) return;

        var theme = DealbreakUIStyle.Theme;
        _red = theme ? theme.brick : new Color(.9f, .2f, .17f);
        _lime = theme ? theme.ochre : new Color(.8f, 1f, .2f);
        _paper = theme ? theme.paper : new Color(.94f, .92f, .86f);
        _ink = theme ? theme.ink : new Color(.05f, .05f, .06f);

        _front = Layer("Mod FX", _view.face.rectTransform, -1);
        switch (visual)
        {
            case CardModVisual.EchoFrame:
                var faceRect = _view.face.rectTransform;
                _backMirror = Layer("Mod FX Back", (RectTransform)faceRect.parent, faceRect.GetSiblingIndex());
                _back = Layer("Mod FX Echoes", _backMirror, -1);
                for (int k = 1; k >= 0; k--) Parts(_back, 5);
                break;
            case CardModVisual.BoostChevrons:
                for (int i = 0; i < 3; i++) Parts(Group(_front), 2);
                break;
            case CardModVisual.Reticle: Parts(_front, 9); break;
            case CardModVisual.TwinSlash: Parts(_front, 4); break;
            case CardModVisual.Overheat: Parts(_front, 9); break;
            case CardModVisual.ArmorPlates: Parts(_front, 14); break;
            case CardModVisual.ClawMarks: Parts(_front, 6); break;
            case CardModVisual.CreditChips:
                var chip = theme && theme.creditsIcon ? theme.creditsIcon : Disc;
                for (int i = 0; i < 3; i++) Part(_front, chip).preserveAspect = true;
                break;
            case CardModVisual.LoadBar: Parts(_front, 9); break;
            case CardModVisual.HealPulse:
                Parts(_front, 4);
                for (int i = 0; i < 4; i++) Parts(Group(_front), 2);
                break;
            case CardModVisual.Shockwave:
                Part(_front, Ring);
                Part(_front, Ring);
                Parts(_front, 4);
                break;
            case CardModVisual.SuitCycle:
                Parts(_front, 5);
                var go = new GameObject("Suit", typeof(RectTransform), typeof(TextMeshProUGUI));
                var rect = (RectTransform)go.transform;
                rect.SetParent(_front, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                var text = go.GetComponent<TextMeshProUGUI>();
                if (theme && theme.symbolFont) text.font = theme.symbolFont;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.raycastTarget = false;
                _g.Add(text);
                break;
        }
        if (signature)
        {
            _wire = _g.Count;
            Parts(_front, 8);
        }
        foreach (var graphic in _g) graphic.color = Color.clear;
    }

    void Clear()
    {
        if (_front) Destroy(_front.gameObject);
        if (_backMirror) Destroy(_backMirror.gameObject);
        _front = _backMirror = _back = null;
        _g.Clear();
        _groups.Clear();
        _built = CardModVisual.None;
        _signature = false;
        _wire = -1;
    }

    static RectTransform Layer(string name, RectTransform parent, int siblingIndex)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        if (siblingIndex >= 0) rect.SetSiblingIndex(siblingIndex);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        return rect;
    }

    RectTransform Group(RectTransform parent)
    {
        var rect = Layer("Group", parent, -1);
        _groups.Add(rect);
        return rect;
    }

    void Parts(RectTransform parent, int count)
    {
        for (int i = 0; i < count; i++) Part(parent, null);
    }

    Image Part(RectTransform parent, Sprite sprite)
    {
        var go = new GameObject("Part", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        _g.Add(image);
        return image;
    }

    // The layers sit on the drawn art: the face keeps its aspect inside its rect.
    void Fit()
    {
        var face = _view.face;
        var faceRect = face.rectTransform;
        Vector2 size = faceRect.rect.size;
        if (face.sprite && face.preserveAspect && size.x > 0f && size.y > 0f)
        {
            float aspect = face.sprite.rect.width / face.sprite.rect.height;
            size = size.x / size.y > aspect ? new Vector2(size.y * aspect, size.y) : new Vector2(size.x, size.x / aspect);
        }
        W = size.x; H = size.y; hx = W / 2f; hy = H / 2f;
        th = Mathf.Max(2f, W * .016f);
        _front.anchoredPosition = Vector2.zero;
        _front.sizeDelta = size;
        if (_backMirror)
        {
            _backMirror.anchorMin = faceRect.anchorMin;
            _backMirror.anchorMax = faceRect.anchorMax;
            _backMirror.pivot = faceRect.pivot;
            _backMirror.sizeDelta = faceRect.sizeDelta;
            _backMirror.anchoredPosition = faceRect.anchoredPosition;
            _backMirror.localRotation = faceRect.localRotation;
            _backMirror.localScale = faceRect.localScale;
            _back.anchoredPosition = Vector2.zero;
            _back.sizeDelta = size;
        }
    }

    // ---- Drawing helpers.

    static void Put(Graphic graphic, float x, float y, float width, float height, float angle = 0f)
    {
        var rect = graphic.rectTransform;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    void Tint(Graphic graphic, Color color, float alpha)
    {
        color.a *= Mathf.Clamp01(alpha * (_signature ? 1.25f : 1f)) * _dim;
        graphic.color = color;
    }

    // Four bars whose outer edge is the given box.
    void Frame(int first, Vector2 centre, float width, float height, float thick, Color color, float alpha)
    {
        Put(_g[first], centre.x, centre.y + height / 2f - thick / 2f, width, thick);
        Put(_g[first + 1], centre.x, centre.y - height / 2f + thick / 2f, width, thick);
        Put(_g[first + 2], centre.x - width / 2f + thick / 2f, centre.y, thick, height);
        Put(_g[first + 3], centre.x + width / 2f - thick / 2f, centre.y, thick, height);
        for (int i = 0; i < 4; i++) Tint(_g[first + i], color, alpha);
    }

    // A "^" of two bars meeting at the group's centre line.
    void Chevron(int first, float length, float thick, Color color, float alpha)
    {
        const float angle = 40f;
        float half = length * Mathf.Cos(angle * Mathf.Deg2Rad) / 2f;
        Put(_g[first], -half, 0f, length, thick, angle);
        Put(_g[first + 1], half, 0f, length, thick, -angle);
        Tint(_g[first], color, alpha);
        Tint(_g[first + 1], color, alpha);
    }

    static float Smooth(float from, float to, float x) => Mathf.SmoothStep(0f, 1f, (x - from) / (to - from));

    // A pulse around `centre` on a looping 0..1 phase.
    static float Beat(float phase, float centre, float width)
    {
        float d = Mathf.Repeat(phase - centre + .5f, 1f) - .5f;
        return Mathf.Exp(-(d / width) * (d / width));
    }

    // A stable 0..1 scatter for presentation (not gameplay randomness).
    static float Hash(int n) => Mathf.Repeat(Mathf.Sin(n * 12.9898f) * 43758.5453f, 1f);

    static Sprite Disc => _disc ? _disc : _disc = MakeRound(64, 0f);
    static Sprite Ring => _ring ? _ring : _ring = MakeRound(128, .9f);

    static Sprite MakeRound(int size, float hole)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp
        };
        float c = (size - 1) * .5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
            float outer = Mathf.Clamp01((1f - d) * c);
            float inner = hole > 0f ? Mathf.Clamp01((d - hole) * c) : 1f;
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * outer * inner));
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
