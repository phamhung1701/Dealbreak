using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The INCOMING n tag under the enemy row (visual-design 3b, hud.md section C): one aggregate total
/// of the pending hit (<see cref="CombatManager.IncomingDamageAfterModifiers"/>), shown in defense
/// only. A red ticket stub on a cream outer edge, a 3px ink shadow, and a red halftone offset masked
/// by the tag shape peeking out down-right; the word and the numerals sit on the solid face.
/// It pops on change and its tilt settles from 3 to 1.5 degrees, with a sign that alternates per
/// appearance (never RNG). When everything is blocked it counts to 0, then reads ALL BLOCKED, flat
/// (no offset, no tilt). The root stays untilted and keeps the authored rect; the Body takes the motion.
/// Presentation only; motion runs in LateUpdate on unscaled time and is static under Reduce Motion.
/// </summary>
public sealed class IncomingTagUI : MonoBehaviour
{
    public const string RootName = "Incoming Tag", BodyName = "Body", EdgeName = "Cream Edge",
        OffsetName = "Halftone Offset", FaceName = "Face", NumberName = "Incoming Number";
    public const string Word = "INCOMING", BlockedWord = "ALL BLOCKED";
    // The tag sprite bakes a 2px ink border, so the paper backing reaches 2px past the face plus its
    // own 2px border: a 2px cream band shows around the red tag, the backing's ink line outside it.
    public const float CreamEdge = 2f, SpriteBorder = 2f, EdgeOutset = CreamEdge + SpriteBorder;
    public const float OffsetRest = 4f, OffsetDefense = 8f;
    public const float PopSeconds = .1f, PopScale = .12f, SettleSeconds = .3f, CountSeconds = .3f;
    const float WordSize = 22f, NumberSize = 32f;

    // Alternates the tilt sign per appearance: deterministic, never RNG or instance IDs.
    static int _appearances;

    RectTransform _body, _offset;
    Image _edge, _face, _mask, _halftone;
    CutSheetShadow _shadow;
    TMP_Text _word, _number;
    bool _built, _defense, _flat, _blocked, _blockedWords, _wordsSet;
    int _value = -1, _shownNumber = -1;
    float _sign = 1f, _countFrom, _countStart = -1f, _popStart = -1f, _appearStart = -1f;

    public TMP_Text WordLabel => _word;
    public TMP_Text NumberLabel => _number;
    public Image Face => _face;
    public Image Edge => _edge;
    public Image Halftone => _halftone;
    public RectTransform HalftoneOffset => _offset;
    public CutSheetShadow Shadow => _shadow;
    public RectTransform Body => _body;
    public bool Visible => gameObject.activeSelf;
    public int Value => _value;
    public bool IsFlat => _flat;
    public float TiltDegrees => _body ? Mathf.DeltaAngle(0f, _body.localEulerAngles.z) : 0f;
    public float OffsetDepth => _offset ? _offset.offsetMin.x : 0f;

    /// <summary>The one total the tag shows: the aggregate pending hit after modifiers, 0 when nothing is pending.</summary>
    public static int ValueFor(CombatManager combat) =>
        combat != null && combat.pendingDamage > 0 ? combat.IncomingDamageAfterModifiers : 0;

    /// <summary>Everything blocked: nothing is pending. A hit armour reduces to 0 is still pending and reads INCOMING 0.</summary>
    public static bool FullyBlockedFor(CombatManager combat) => combat == null || combat.pendingDamage <= 0;

    /// <summary>The tag shows in defense only.</summary>
    public static bool ShownFor(CombatManager combat) => combat != null && combat.currentState == GameState.EnemyAttacking;

    /// <summary>Builds the tag around <paramref name="label"/> once: a root takes the label's rect and the label becomes its word.</summary>
    public static IncomingTagUI Dress(TMP_Text label)
    {
        if (!label) return null;
        var existing = label.GetComponentInParent<IncomingTagUI>(true);
        if (existing) return existing;
        var labelRect = label.rectTransform;
        var parent = labelRect.parent;
        var item = new GameObject(RootName, typeof(RectTransform));
        item.layer = label.gameObject.layer;
        var root = (RectTransform)item.transform;
        root.SetParent(parent, false);
        root.SetSiblingIndex(labelRect.GetSiblingIndex());
        root.anchorMin = labelRect.anchorMin;
        root.anchorMax = labelRect.anchorMax;
        root.pivot = labelRect.pivot;
        root.anchoredPosition = labelRect.anchoredPosition;
        root.sizeDelta = labelRect.sizeDelta;
        var tag = item.AddComponent<IncomingTagUI>();
        tag.Build(label);
        item.SetActive(false);
        return tag;
    }

    void Build(TMP_Text label)
    {
        if (_built) return;
        _built = true;
        var theme = DealbreakUIStyle.Theme;
        Color red = theme ? theme.brick : new Color(.75f, .2f, .16f);
        Color paper = theme ? theme.paper : new Color(.94f, .89f, .8f);
        Sprite tag = theme ? theme.tagSprite : null;

        _body = Part(BodyName, transform);
        Stretch(_body, 0f);
        // The 3px shadow falls from the outer (cream) edge, so it stays visible outside it.
        var shadowHolder = Part("Shadow", _body);
        Stretch(shadowHolder, EdgeOutset);
        _shadow = CutSheetShadow.Create(shadowHolder);
        _edge = Picture(Part(EdgeName, _body, true), tag, paper);
        Stretch(_edge.rectTransform, EdgeOutset);

        // Red halftone misregistration: a tag-shaped stencil (Mask, graphic hidden) offset down-right
        // of the face, under it, over the cream edge. Stretched first: an unstretched part is a
        // zero-size rect once its offsets are written.
        _offset = Part(OffsetName, _body, true);
        Stretch(_offset, 0f);
        _mask = _offset.GetComponent<Image>();
        _mask.sprite = tag;
        _mask.type = Image.Type.Sliced;
        _mask.raycastTarget = false;
        if (!_offset.TryGetComponent(out Mask mask)) mask = _offset.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        var halftoneRect = Part("Halftone", _offset, true);
        Stretch(halftoneRect, 0f);
        _halftone = halftoneRect.GetComponent<Image>();
        _halftone.sprite = theme ? theme.halftoneSprite : null;
        _halftone.type = Image.Type.Tiled;
        _halftone.color = red;
        _halftone.raycastTarget = false;
        if (!halftoneRect.TryGetComponent(out ScreenPixelTile _)) halftoneRect.gameObject.AddComponent<ScreenPixelTile>();

        _face = Picture(Part(FaceName, _body, true), tag, red);
        Stretch(_face.rectTransform, 0f);
        _shadow.Match(_edge, CutSheetShadow.Tint, CutSheetMetrics.ShadowSlot);

        _word = label;
        _word.transform.SetParent(_body, false);
        if (!_word.gameObject.activeSelf) _word.gameObject.SetActive(true);
        var numberRect = Part(NumberName, _body);
        if (!numberRect.TryGetComponent(out _number)) _number = numberRect.gameObject.AddComponent<TextMeshProUGUI>();
        Style(_word, theme ? (theme.labelFont ? theme.labelFont : theme.headingFont) : null, WordSize, paper);
        _word.fontStyle = FontStyles.UpperCase;
        Style(_number, theme ? theme.headingFont : null, NumberSize, paper);
        _number.alignment = TextAlignmentOptions.MidlineLeft;
        SetWords(false);
        ApplyPose(0f);
    }

    /// <summary>Shows or hides the tag; a total of 0 reads as fully blocked.</summary>
    public void Show(bool visible, int value, bool defense) => Show(visible, value, defense, value <= 0);

    /// <summary>
    /// Shows or hides the tag and sets its total; called on state and pending-damage changes. Only
    /// <paramref name="fullyBlocked"/> (nothing pending) counts down to ALL BLOCKED and goes flat.
    /// </summary>
    public void Show(bool visible, int value, bool defense, bool fullyBlocked)
    {
        if (!_built) return;
        if (!visible)
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            _value = -1;
            return;
        }
        value = Mathf.Max(0, value);
        bool blocked = fullyBlocked && value == 0;
        _defense = defense;
        bool appearing = !gameObject.activeSelf;
        if (appearing)
        {
            _appearances++;
            _sign = (_appearances & 1) == 1 ? 1f : -1f;
            _appearStart = Animating ? Time.unscaledTime : -1f;
            _value = -1;
            gameObject.SetActive(true);
        }
        if (value == _value && blocked == _blocked) { if (appearing) ApplyPose(0f); return; }
        int previous = _value;
        bool wasBlocked = _blocked;
        _value = value;
        _blocked = blocked;
        if (!blocked)
        {
            _flat = false;
            _countStart = -1f;
            SetWords(false);
            SetNumber(value);
            if (previous >= 0 && Animating) _popStart = Time.unscaledTime;
        }
        else if (previous > 0 && !wasBlocked && Animating)
        {
            // Fully blocked: count the shown total down to 0, then go flat.
            _countFrom = _shownNumber > 0 ? _shownNumber : previous;
            _countStart = Time.unscaledTime;
        }
        else GoFlat();
        ApplyPose(0f);
    }

    static bool Animating => Application.isPlaying && !CutSheetMotion.ReduceMotion;

    void GoFlat()
    {
        _countStart = -1f;
        _popStart = -1f;
        _flat = true;
        SetWords(true);
    }

    void LateUpdate()
    {
        if (_countStart < 0f && _popStart < 0f && _appearStart < 0f) return;
        float now = Time.unscaledTime;
        if (_countStart >= 0f)
        {
            float t = Mathf.Clamp01((now - _countStart) / Mathf.Max(.0001f, CutSheetMotion.Seconds(CountSeconds)));
            SetNumber(Mathf.RoundToInt(Mathf.Lerp(_countFrom, 0f, t)));
            if (t >= 1f) GoFlat();
        }
        float pop = 0f;
        if (_popStart >= 0f)
        {
            float t = Mathf.Clamp01((now - _popStart) / Mathf.Max(.0001f, CutSheetMotion.Seconds(PopSeconds)));
            pop = 1f - t;
            if (t >= 1f) _popStart = -1f;
        }
        ApplyPose(pop);
    }

    void ApplyPose(float pop)
    {
        float tilt = 0f;
        if (!_flat)
        {
            float settle = 1f;
            if (_appearStart >= 0f)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _appearStart) / Mathf.Max(.0001f, CutSheetMotion.Seconds(SettleSeconds)));
                settle = 1f - (1f - t) * (1f - t);
                if (t >= 1f) _appearStart = -1f;
            }
            tilt = _sign * Mathf.Lerp(CutSheetMetrics.IncomingTiltStart, CutSheetMetrics.TagTiltRest, settle);
        }
        else _appearStart = -1f;
        var rotation = Quaternion.Euler(0f, 0f, tilt);
        if (_body.localRotation != rotation) _body.localRotation = rotation;
        float s = 1f + PopScale * pop;
        var scale = new Vector3(s, s, 1f);
        if (_body.localScale != scale) _body.localScale = scale;
        float depth = _flat ? 0f : _defense ? OffsetDefense : OffsetRest;
        var offset = new Vector2(depth, -depth);
        if (_offset.offsetMin != offset) { _offset.offsetMin = offset; _offset.offsetMax = offset; }
        bool showOffset = depth > 0f;
        if (_halftone.enabled != showOffset) _halftone.enabled = showOffset;
    }

    void SetWords(bool blocked)
    {
        if (_wordsSet && _blockedWords == blocked) return;
        _wordsSet = true;
        _blockedWords = blocked;
        _word.text = blocked ? BlockedWord : Word;
        var rect = _word.rectTransform;
        if (blocked)
        {
            rect.anchorMin = new Vector2(.04f, 0f); rect.anchorMax = new Vector2(.96f, 1f);
            _word.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            rect.anchorMin = new Vector2(.04f, 0f); rect.anchorMax = new Vector2(.64f, 1f);
            _word.alignment = TextAlignmentOptions.MidlineRight;
        }
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var numberRect = _number.rectTransform;
        numberRect.anchorMin = new Vector2(.68f, 0f); numberRect.anchorMax = new Vector2(.96f, 1f);
        numberRect.offsetMin = numberRect.offsetMax = Vector2.zero;
        if (_number.gameObject.activeSelf == blocked) _number.gameObject.SetActive(!blocked);
    }

    void SetNumber(int value)
    {
        if (value == _shownNumber) return;
        _shownNumber = value;
        _number.SetText("{0}", value);
    }

    static void Style(TMP_Text text, TMP_FontAsset font, float size, Color color)
    {
        if (font) DealbreakUIStyle.Text(text, font, size, color);
        else { text.fontSize = size; text.color = color; }
        // A label restyled from an outlined material keeps that outline unless its own font material returns.
        if (text.font && text.fontSharedMaterial != text.font.material) text.fontSharedMaterial = text.font.material;
        text.characterSpacing = 0;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
    }

    static Image Picture(RectTransform rect, Sprite sprite, Color color)
    {
        var image = rect.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static void Stretch(RectTransform rect, float outset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(-outset, -outset);
        rect.offsetMax = new Vector2(outset, outset);
    }

    RectTransform Part(string name, Transform parent, bool image = false)
    {
        var existing = parent.Find(name);
        if (existing) return (RectTransform)existing;
        var item = image
            ? new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            : new GameObject(name, typeof(RectTransform));
        item.layer = gameObject.layer;
        var rect = (RectTransform)item.transform;
        rect.SetParent(parent, false);
        return rect;
    }
}
