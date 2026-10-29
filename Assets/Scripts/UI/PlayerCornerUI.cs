using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player's HP panel in the Cut Sheet look (HUD slice S10a, hud.md section C, visual-design 3b):
/// an ink frame (panel sprite) around a paper track with a brick fill and ink ticks every 5 HP (every
/// 10 when they would sit closer than 6px), the "HP n/max" number in Courier Prime Bold 20 ink with
/// the paper-outline preset, a paper ring on hover/focus, and the Block/Shield line beside the
/// Credits chip, hidden at 0. It owns its labels' type, so the style pass skips everything under it
/// (the Credits chip and portrait included). Callers pass the shown values; this never reads or
/// writes game state. Every write happens only when its value changes.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCornerUI : MonoBehaviour
{
    public const string MeterName = "HP Meter", TrackName = "Track", FillName = "Fill", TicksName = "Ticks",
        RingName = "HP Ring", DefenseName = "Player Defense Line";
    public const float TrackInset = 2f, TickWidth = 2f, MinTickSpacing = 6f, RingOutset = 2f;
    public const int TickStep = 5, DenseTickStep = 10;
    public const float NumberSize = 20f, BlockSize = 22f, ShieldNumberSize = 28f, ShieldGlyphSize = 24f;
    // Pop on change (hud.md): 100ms, instant under Reduce Motion. The scale matches the Credits stamp.
    public const float PopSeconds = .1f, PopScale = 1.12f;

    [Tooltip("Courier Prime UI - Paper Outline: the HP number's 2px paper outline (shared, no per-label instance).")]
    public Material hpNumberMaterial;
    [Tooltip("The Credits chip's coin art (Assets/Art/UI/Sprites/Credits_Coin.png).")]
    public Sprite coinSprite;

    TMP_Text _label;
    Image _frame, _track, _fill, _ring;
    RectTransform _fillRect, _ticks;
    readonly List<Image> _tickPool = new();
    int _activeTicks, _tickStep = TickStep;
    float _tickLayoutWidth = -1f;
    int _shownHealth = int.MinValue, _shownMax = int.MinValue;
    bool? _shownInfinite;
    float _fraction = -1f;
    RectTransform _defense;
    TMP_Text _block, _shieldCount;
    GameObject _shieldGlyph;
    int _shownBlock = int.MinValue, _shownShield = int.MinValue;
    float _popStart = float.NegativeInfinity;
    bool _bound;

    public TMP_Text Label => _label;
    public Image Frame => _frame;
    public Image Track => _track;
    public Image Fill => _fill;
    public Image Ring => _ring;
    public RectTransform DefenseLine => _defense;
    public TMP_Text BlockLabel => _block;
    public TMP_Text ShieldCountLabel => _shieldCount;
    public int ActiveTickCount => _activeTicks;
    public int CurrentTickStep => _tickStep;
    public IReadOnlyList<Image> Ticks => _tickPool;
    /// <summary>Tests: the player whose Block and Shield the line follows (else the combat's player).</summary>
    public PlayerRuntime PlayerOverride { get; set; }

    /// <summary>The corner on <paramref name="panel"/>, dressed around <paramref name="label"/> once.</summary>
    public static PlayerCornerUI For(RectTransform panel, TMP_Text label)
    {
        if (!panel) return null;
        if (!panel.TryGetComponent<PlayerCornerUI>(out var corner)) corner = panel.gameObject.AddComponent<PlayerCornerUI>();
        if (label) corner.Bind(label);
        return corner;
    }

    void Bind(TMP_Text label)
    {
        if (_bound && _label == label) return;
        _label = label;
        _bound = true;
        var theme = DealbreakUIStyle.Theme;
        // The meter is the panel: the old charcoal frame and its hairline stop drawing.
        var oldFrame = transform.Find("Editorial Frame");
        if (oldFrame && oldFrame.gameObject.activeSelf) oldFrame.gameObject.SetActive(false);
        if (TryGetComponent<Image>(out var rootImage)) { rootImage.sprite = null; rootImage.color = Color.clear; rootImage.raycastTarget = false; }

        var meter = DealbreakUIStyle.Child(transform, MeterName, typeof(CanvasRenderer), typeof(Image));
        Stretch(meter, 0f);
        meter.SetAsFirstSibling();
        _frame = meter.GetComponent<Image>();
        _frame.sprite = theme ? theme.panelSprite : null;
        _frame.type = _frame.sprite && _frame.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        _frame.color = theme ? theme.inkPrint : CutSheetShadow.FallbackTint;
        _frame.raycastTarget = true;

        var track = DealbreakUIStyle.Child(meter, TrackName, typeof(CanvasRenderer), typeof(Image));
        Stretch(track, TrackInset);
        _track = track.GetComponent<Image>();
        _track.sprite = null;
        _track.color = theme ? theme.paper : Color.white;
        _track.raycastTarget = false;

        _fillRect = DealbreakUIStyle.Child(track, FillName, typeof(CanvasRenderer), typeof(Image));
        _fillRect.anchorMin = Vector2.zero;
        _fillRect.anchorMax = Vector2.one;
        _fillRect.offsetMin = _fillRect.offsetMax = Vector2.zero;
        _fill = _fillRect.GetComponent<Image>();
        _fill.sprite = null;
        _fill.color = theme ? theme.brick : Color.red;
        _fill.raycastTarget = false;

        _ticks = DealbreakUIStyle.Child(track, TicksName);
        Stretch(_ticks, 0f);

        _ring = CutSheetHoverRing.CreateRing(transform, RingName, RingOutset);
        _ring.transform.SetSiblingIndex(meter.GetSiblingIndex() + 1);
        CutSheetHoverRing.Attach(meter.gameObject, _ring, true, true);

        // The number sits centred on the meter: Courier Prime Bold 20, ink, 2px paper outline.
        DealbreakUIStyle.Place(label.transform, 0f, 0f, 1f, 1f);
        if (theme) DealbreakUIStyle.Text(label, theme.numericFont, NumberSize, theme.inkPrint);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        ApplyNumberMaterial(label, hpNumberMaterial);
        label.transform.SetAsLastSibling();

        BuildDefenseLine(theme);
        SetDefense(0, 0);
    }

    /// <summary>The preset when it samples the label's font atlas; else the same outline on the label's own material.</summary>
    public static void ApplyNumberMaterial(TMP_Text label, Material preset)
    {
        if (!label) return;
        var theme = DealbreakUIStyle.Theme;
        if (preset && label.font && preset.mainTexture == label.font.atlasTexture)
        {
            if (label.fontSharedMaterial != preset) label.fontSharedMaterial = preset;
        }
        else DealbreakUIStyle.Outline(label, .24f, theme ? (Color32)theme.paper : new Color32(239, 228, 204, 255));
    }

    void BuildDefenseLine(DealbreakUITheme theme)
    {
        _defense = DealbreakUIStyle.Child(transform, DefenseName, typeof(HorizontalLayoutGroup));
        _defense.anchorMin = new Vector2(0f, 1f);
        _defense.anchorMax = Vector2.one;
        _defense.pivot = new Vector2(1f, 0f);
        _defense.offsetMin = new Vector2(PlayerCornerLayout.DefenseLineLeft, PlayerCornerLayout.ChipGap);
        _defense.offsetMax = new Vector2(0f, PlayerCornerLayout.ChipGap + PlayerCornerLayout.DefenseLineHeight);
        var layout = _defense.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.spacing = 8f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        _block = DealbreakUIStyle.Child(_defense, "Block", typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        if (theme) DealbreakUIStyle.Text(_block, theme.labelFont ? theme.labelFont : theme.headingFont, BlockSize, theme.paper);
        _block.fontStyle = FontStyles.UpperCase;
        _block.textWrappingMode = TextWrappingModes.NoWrap;
        _block.alignment = TextAlignmentOptions.MidlineRight;
        _block.raycastTarget = false;

        var glyph = DealbreakUIStyle.Child(_defense, "Shield Glyph", typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        var glyphImage = glyph.GetComponent<Image>();
        glyphImage.sprite = ShieldGlyph.Sprite;
        glyphImage.preserveAspect = true;
        glyphImage.raycastTarget = false;
        var glyphSize = glyph.GetComponent<LayoutElement>();
        glyphSize.preferredWidth = glyphSize.minWidth = ShieldGlyphSize;
        glyphSize.preferredHeight = glyphSize.minHeight = ShieldGlyphSize;
        _shieldGlyph = glyph.gameObject;

        _shieldCount = DealbreakUIStyle.Child(_defense, "Shield Count", typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        if (theme) DealbreakUIStyle.Text(_shieldCount, theme.numericFont, ShieldNumberSize, theme.paper);
        _shieldCount.textWrappingMode = TextWrappingModes.NoWrap;
        _shieldCount.alignment = TextAlignmentOptions.MidlineRight;
        _shieldCount.raycastTarget = false;
    }

    /// <summary>The shown HP (counting toward its target), the max and the fill fraction.</summary>
    public void SetHealth(int shown, int max, float fraction, bool infinite)
    {
        if (_label && (shown != _shownHealth || max != _shownMax || _shownInfinite != infinite))
        {
            if (infinite) _label.SetText("HP ∞");
            else _label.SetText("HP {0}/{1}", shown, max);
        }
        bool maxChanged = max != _shownMax || _shownInfinite != infinite;
        _shownHealth = shown;
        _shownMax = max;
        _shownInfinite = infinite;
        fraction = Mathf.Clamp01(infinite || max <= 0 ? 1f : fraction);
        if (_fillRect && !Mathf.Approximately(fraction, _fraction))
        {
            _fraction = fraction;
            _fillRect.anchorMax = new Vector2(fraction, 1f);
        }
        if (maxChanged) LayoutTicks();
    }

    /// <summary>The line above the panel: "BLOCK n" and/or the Shield glyph and count; hidden at 0.</summary>
    public void SetDefense(int block, int shield)
    {
        if (!_defense || block == _shownBlock && shield == _shownShield) return;
        bool wasShown = _shownBlock > 0 || _shownShield > 0;
        _shownBlock = block;
        _shownShield = shield;
        bool show = block > 0 || shield > 0;
        if (_defense.gameObject.activeSelf != show) _defense.gameObject.SetActive(show);
        if (!show) { _defense.localScale = Vector3.one; return; }
        if (_block.gameObject.activeSelf != block > 0) _block.gameObject.SetActive(block > 0);
        if (block > 0) _block.SetText("BLOCK {0}", block);
        if (_shieldGlyph.activeSelf != shield > 0) _shieldGlyph.SetActive(shield > 0);
        if (_shieldCount.gameObject.activeSelf != shield > 0) _shieldCount.gameObject.SetActive(shield > 0);
        if (shield > 0) _shieldCount.SetText("{0}", shield);
        if (Application.isPlaying && (wasShown || show) && CutSheetMotion.Seconds(PopSeconds) > 0f) _popStart = Time.unscaledTime;
    }

    void LateUpdate()
    {
        // The line follows the live values every frame (int compares only): not every Block or Shield
        // gain raises an event the HUD hears.
        var player = PlayerOverride ?? (CombatManager.Instance != null ? CombatManager.Instance.player : null);
        if (player != null) SetDefense(player.EncounterBlock, player.ShieldCharges);
        else SetDefense(0, 0);
        // The number draws above the meter (track, fill, ticks) and everything else on the panel.
        if (_label && _label.transform.parent == transform && _label.transform.GetSiblingIndex() != transform.childCount - 1)
            _label.transform.SetAsLastSibling();
        // The panel changes width beside the compact hand; ticks keep their HP spacing.
        if (_track && !Mathf.Approximately(_track.rectTransform.rect.width, _tickLayoutWidth)) LayoutTicks();
        if (_defense && !float.IsNegativeInfinity(_popStart))
        {
            float duration = CutSheetMotion.Seconds(PopSeconds);
            float t = duration > 0f ? (Time.unscaledTime - _popStart) / duration : 1f;
            if (t >= 1f) { _popStart = float.NegativeInfinity; _defense.localScale = Vector3.one; }
            else _defense.localScale = Vector3.one * Mathf.Lerp(PopScale, 1f, t);
        }
    }

    /// <summary>Ticks every 5 HP, or every 10 when 5 would sit closer than 6px; none for infinite HP or
    /// when even every 10 would sit closer than 6px.</summary>
    public static int TickStepFor(int max, float trackWidth) =>
        max > 0 && trackWidth * TickStep / max < MinTickSpacing ? DenseTickStep : TickStep;

    void LayoutTicks()
    {
        if (!_track || !_ticks) return;
        float width = _track.rectTransform.rect.width;
        _tickLayoutWidth = width;
        int max = _shownMax;
        int needed = 0;
        if (_shownInfinite != true && max > 0 && width > 0f)
        {
            _tickStep = TickStepFor(max, width);
            // Even every 10 HP can be denser than 6px (a dev or test run at huge max HP): draw no ticks
            // then, so the canvas never holds thousands of them; the fill length carries HP.
            if (width * _tickStep / max >= MinTickSpacing) needed = (max - 1) / _tickStep;
        }
        var theme = DealbreakUIStyle.Theme;
        while (_tickPool.Count < needed)
        {
            var tick = new GameObject("Tick", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tick.layer = gameObject.layer;
            tick.transform.SetParent(_ticks, false);
            var image = tick.GetComponent<Image>();
            image.color = theme ? theme.inkPrint : CutSheetShadow.FallbackTint;
            image.raycastTarget = false;
            _tickPool.Add(image);
        }
        for (int i = 0; i < _tickPool.Count; i++)
        {
            var image = _tickPool[i];
            bool active = i < needed;
            if (image.gameObject.activeSelf != active) image.gameObject.SetActive(active);
            if (!active) continue;
            float x = (i + 1) * _tickStep / (float)max;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(TickWidth, 0f);
            rect.anchoredPosition = Vector2.zero;
        }
        _activeTicks = needed;
    }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
