using System.Collections;
using UnityEngine;

/// <summary>Small fade/scale transition for the shared central board presentation panels.</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "BoardPanelTransition")]
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
