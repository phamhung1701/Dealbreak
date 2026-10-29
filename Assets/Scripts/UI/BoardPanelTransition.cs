using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Small fade/scale transition for the shared central board presentation panels.</summary>
public sealed class BoardPanelTransition : MonoBehaviour
{
    const float ShowDuration = 0.24f;
    const float HideDuration = 0.14f;
    const float OverlayFadeDuration = 0.16f;
    const float ShownFromScale = 0.965f;
    CanvasGroup _group;
    RectTransform _rect;
    Vector3 _shownScale;
    bool _animating;
    bool _showing;

    public static void Show(GameObject panel)
    {
        if (panel == null) return;
        if (!Application.isPlaying)
        {
            panel.SetActive(true);
            return;
        }
        var transition = For(panel);
        transition.ShowPanel();
    }

    public static void Hide(GameObject panel)
    {
        if (panel == null || !panel.activeSelf) return;
        if (!Application.isPlaying || !panel.activeInHierarchy)
        {
            panel.SetActive(false);
            return;
        }
        var transition = For(panel);
        transition.HidePanel();
    }

    /// <summary>
    /// Activates a modal overlay at once (input and ownership unchanged) and only fades its alpha in.
    /// An already visible panel is left alone, so refreshes do not flicker.
    /// </summary>
    public static void FadeIn(GameObject panel)
    {
        if (panel == null) return;
        panel.TryGetComponent<BoardPanelTransition>(out var existing);
        bool hiding = existing != null && existing._animating && !existing._showing;
        if (panel.activeSelf && !hiding) return;
        panel.SetActive(true);
        if (!Application.isPlaying || !panel.activeInHierarchy) return;
        var transition = existing != null ? existing : panel.AddComponent<BoardPanelTransition>();
        transition.FadeOverlay();
    }

    static BoardPanelTransition For(GameObject panel) =>
        panel.TryGetComponent<BoardPanelTransition>(out var transition) ? transition : panel.AddComponent<BoardPanelTransition>();

    bool _initialized;

    void Awake() => CacheComponents();

    // A panel deactivated mid-transition by other code must not stay half-faded or unclickable.
    void OnDisable()
    {
        if (!_animating) return;
        _animating = false;
        if (_group == null) return;
        _group.alpha = 1f;
        _group.interactable = true;
        _group.blocksRaycasts = true;
        if (_rect != null) _rect.localScale = _shownScale;
    }

    void CacheComponents()
    {
        if (_initialized) return;
        // Explicit checks: Unity's destroyed-object null does not work with ??.
        if (!TryGetComponent(out _group)) _group = gameObject.AddComponent<CanvasGroup>();
        _rect = transform as RectTransform;
        _shownScale = _rect != null ? _rect.localScale : Vector3.one;
        _initialized = true;
    }

    void ShowPanel()
    {
        StopAllCoroutines();
        gameObject.SetActive(true);
        CacheComponents();
        bool resuming = _animating && !_showing;
        if (!resuming)
        {
            _group.alpha = 0f;
            if (_rect != null) _rect.localScale = _shownScale * ShownFromScale;
        }
        _group.interactable = false;
        _group.blocksRaycasts = false;
        if (!Application.isPlaying)
        {
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            if (_rect != null) _rect.localScale = _shownScale;
            return;
        }
        StartCoroutine(Animate(1f, true, ShowDuration, true));
    }

    void HidePanel()
    {
        StopAllCoroutines();
        CacheComponents();
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            _animating = false;
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            gameObject.SetActive(false);
            return;
        }
        _group.interactable = false;
        _group.blocksRaycasts = false;
        StartCoroutine(Animate(0f, false, HideDuration, true));
    }

    void FadeOverlay()
    {
        StopAllCoroutines();
        CacheComponents();
        _group.alpha = 0f;
        _group.interactable = true;
        _group.blocksRaycasts = true;
        if (_rect != null) _rect.localScale = _shownScale;
        StartCoroutine(Animate(1f, true, OverlayFadeDuration, false));
    }

    IEnumerator Animate(float targetAlpha, bool activateAtEnd, float duration, bool scale)
    {
        _animating = true;
        _showing = activateAtEnd;
        float startAlpha = _group.alpha;
        Vector3 startScale = _rect != null ? _rect.localScale : Vector3.one;
        Vector3 endScale = activateAtEnd ? _shownScale : _shownScale * ShownFromScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease out when arriving, ease in when leaving: panels settle rather than stop dead.
            float eased = activateAtEnd ? 1f - (1f - t) * (1f - t) * (1f - t) : t * t;
            _group.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            if (scale && _rect != null) _rect.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }
        _group.alpha = targetAlpha;
        if (_rect != null) _rect.localScale = _shownScale;
        _animating = false;
        if (activateAtEnd)
        {
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }
        else gameObject.SetActive(false);
    }
}

/// <summary>
/// The board tone and the defense dot screen (visual-design 3b, hud.md section E). Entering or
/// leaving defense crossfades the stage background between <c>Theme.ink</c> and
/// <c>Theme.boardDefense</c> over 250ms, and a red (brick) halftone dot screen along the top edge, behind
/// the enemies, fades in over 300ms (out on leaving), never pulsing. Instant under Reduce Motion.
/// The background is a baked scene Image; its colour is set here from the theme at runtime.
/// Plain class driven by <see cref="DealbreakUIRuntime"/> with unscaled time; presentation only.
/// </summary>
public sealed class BoardTone
{
    public const string DotScreenName = "Defense Dot Screen";
    // A red printed edge, not a wash: modest alpha.
    public const float ToneSeconds = CutSheetMetrics.StatusCrossfadeSeconds, DotSeconds = .3f, DotScreenAlpha = .35f;
    // The dot screen is strongest at the screen's top edge and fades to nothing by the enemies'
    // name/HP line (owner 2026-10-29): no hard border, no wash over the portraits.
    public const float DotScreenBottom = .62f;

    readonly Image _board;
    readonly RawImage _dots;
    Color _toneFrom, _toneTo;
    float _dotFrom, _dotTo, _toneStart = -1f, _dotStart = -1f;
    bool _defense, _set;

    public Image Board => _board;
    public RawImage DotScreen => _dots;
    public bool Defense => _defense;

    /// <summary>The board tone for a phase, from the theme.</summary>
    public static Color ToneFor(bool defense)
    {
        var theme = DealbreakUIStyle.Theme;
        if (!theme) return defense ? new Color32(0x15, 0x11, 0x0E, 0xFF) : new Color32(0x1C, 0x18, 0x14, 0xFF);
        return defense ? theme.boardDefense : theme.ink;
    }

    static Color DotTint(float alpha)
    {
        // The art bible's red halftone dot screen along the top edge.
        var theme = DealbreakUIStyle.Theme;
        Color red = theme ? theme.brick : new Color(.75f, .2f, .16f);
        return new Color(red.r, red.g, red.b, alpha);
    }

    /// <summary>Takes over <paramref name="board"/>'s colour and adds the dot screen as its last child (behind later canvas siblings).</summary>
    public static BoardTone Install(Image board)
    {
        if (board == null) return null;
        var existing = board.transform.Find(DotScreenName);
        RawImage dots;
        if (existing && existing.TryGetComponent(out dots)) { }
        else
        {
            var item = new GameObject(DotScreenName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            item.layer = board.gameObject.layer;
            var rect = (RectTransform)item.transform;
            rect.SetParent(board.transform, false);
            rect.anchorMin = new Vector2(0f, DotScreenBottom);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            dots = item.GetComponent<RawImage>();
            var theme = DealbreakUIStyle.Theme;
            dots.texture = theme && theme.halftoneSprite ? theme.halftoneSprite.texture : null;
            dots.raycastTarget = false;
            item.AddComponent<ScreenPixelTile>();
            item.AddComponent<VerticalAlphaGradient>().bottomAlpha = 0f;
        }
        dots.color = DotTint(0f);
        dots.enabled = false;
        var tone = new BoardTone(board, dots);
        tone.Set(false, 0f, true);
        return tone;
    }

    BoardTone(Image board, RawImage dots) { _board = board; _dots = dots; }

    /// <summary>Sets the phase; a change starts the crossfade and dot fade (instant under Reduce Motion or when <paramref name="instant"/>).</summary>
    public void Set(bool defense, float now, bool instant = false)
    {
        if (_set && defense == _defense) return;
        _set = true;
        _defense = defense;
        _toneFrom = _board.color;
        _toneTo = ToneFor(defense);
        _dotFrom = _dots.color.a;
        _dotTo = defense ? DotScreenAlpha : 0f;
        _toneStart = _dotStart = now;
        if (instant) { _toneStart = _dotStart = -1f; Write(_toneTo, _dotTo); return; }
        Tick(now);
    }

    /// <summary>Advances the fades; writes only while one runs.</summary>
    public void Tick(float now)
    {
        if (_toneStart < 0f && _dotStart < 0f) return;
        Color tone = _board.color;
        float dot = _dots.color.a;
        if (_toneStart >= 0f)
        {
            float t = Progress(now - _toneStart, ToneSeconds);
            tone = Color.Lerp(_toneFrom, _toneTo, t);
            if (t >= 1f) _toneStart = -1f;
        }
        if (_dotStart >= 0f)
        {
            float t = Progress(now - _dotStart, DotSeconds);
            dot = Mathf.Lerp(_dotFrom, _dotTo, t);
            if (t >= 1f) _dotStart = -1f;
        }
        Write(tone, dot);
    }

    static float Progress(float elapsed, float seconds)
    {
        float duration = CutSheetMotion.Seconds(seconds);
        return duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
    }

    void Write(Color tone, float dot)
    {
        if (_board.color != tone) _board.color = tone;
        var tint = DotTint(dot);
        if (_dots.color != tint) _dots.color = tint;
        bool show = dot > 0f && _dots.texture;
        if (_dots.enabled != show) _dots.enabled = show;
    }
}
