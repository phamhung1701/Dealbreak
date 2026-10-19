using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Disposable UGUI feedback for committed attacks and defense cards; CombatManager owns gameplay results.</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "AttackCardPresentationUI")]
public sealed class AttackCardPresentationUI : MonoBehaviour
{
    public static AttackCardPresentationUI Instance { get; private set; }

    [Header("Presentation References")]
    [SerializeField] Canvas presentationCanvas;
    [SerializeField] RectTransform deckTarget;
    [SerializeField] EnemyGroupUI enemyGroup;
    [SerializeField] Sprite redCardBack;

    // Presentation timing. The commit impact delay is derived from these, so hit numbers and enemy
    // reactions land when the card arrives rather than when the command commits.
    const float WindupTime = 0.08f;
    const float AttackTravelTime = 0.2f;
    const float DraggedTravelTime = 0.16f;
    const float BlockTravelTime = 0.16f;
    const float HitSpacing = 0.065f;
    const float FlashTime = 0.12f;
    const float FlipHalfTime = 0.07f;
    const float ReturnTime = 0.26f;
    const float HitNumberTime = 0.8f;

    readonly List<RectTransform> _activeCards = new();
    readonly List<GameObject> _flashes = new();
    readonly List<GameObject> _hitNumbers = new();
    float _commitImpactDelay;
    int _pendingCommits;
    bool _wasBusy;
    CombatManager _combat;
    RunManager _run;

    public int ActiveVisualCount => _activeCards.Count;
    public int ActiveHitNumberCount => _hitNumbers.Count;
    public bool IsBusy => _pendingCommits > 0 || _activeCards.Count > 0 || _hitNumbers.Count > 0 ||
        enemyGroup != null && enemyGroup.IsDefeatPresentationBusy;
    public event Action OnPresentationCompleted;
    public event Action OnPresentationCancelled;

    void Awake()
    {
        if (Instance != null && Instance != this) return;
        Instance = this;
    }

    void OnEnable()
    {
        if (Instance != null && Instance != this) return;
        Instance = this;
        _combat = CombatManager.Instance;
        _run = RunManager.Instance;
        if (_combat != null)
        {
            _combat.OnEnemiesChanged += HandleEnemiesChanged;
            _combat.OnDamageResolved += HandleDamageResolved;
        }
        if (_run != null) _run.OnRunStarted += HandleRunStarted;
        if (enemyGroup != null) enemyGroup.OnDefeatPresentationCompleted += HandleDefeatPresentationCompleted;
    }

    void OnDisable()
    {
        if (_combat != null)
        {
            _combat.OnEnemiesChanged -= HandleEnemiesChanged;
            _combat.OnDamageResolved -= HandleDamageResolved;
        }
        if (_run != null) _run.OnRunStarted -= HandleRunStarted;
        if (enemyGroup != null) enemyGroup.OnDefeatPresentationCompleted -= HandleDefeatPresentationCompleted;
        _combat = null;
        _run = null;
        CancelAll();
        if (Instance == this) Instance = null;
    }

    void HandleRunStarted(string _) => CancelAll();
    void HandleDefeatPresentationCompleted() => NotifyIfIdle();

    void HandleEnemiesChanged()
    {
        // Enemy removal during a player attack can despawn its view before the animation plays.
        // Preserve the already-captured presentation until it completes.
        var state = _combat != null ? _combat.currentState : GameState.Idle;
        if (state == GameState.Idle || state == GameState.GameWon) CancelAll();
    }

    /// <summary>
    /// Returns the authoritative attack result, even when presentation references are missing.
    /// The caller supplies selected cards in their intended playback order.
    /// </summary>
    public bool TryPlayCards(IReadOnlyList<CardView> selection, EnemyRuntime target, CardView draggedCard = null)
    {
        var combat = CombatManager.Instance;
        if (combat == null || !combat.CanPlayCards(selection, target)) return false;
        // The caller may pass CardManager.SelectedCards, which discard clears synchronously.
        var cards = new CardView[selection.Count];
        for (int i = 0; i < cards.Length; i++) cards[i] = selection[i];

        if (!isActiveAndEnabled || !TryGetPresentationPoints(target, out var canvasRect,
                out var targetPoint, out var deckPoint) || cards.Length == 0)
            return combat.TryPlayCards(cards, target);

        if (!TryCaptureVisuals(cards, canvasRect, false, draggedCard, out var staged, out var origins, out var draggedFlags))
            return combat.TryPlayCards(cards, target);
        return CommitAndAnimate(staged, origins, draggedFlags, targetPoint, deckPoint,
            () => combat.TryPlayCards(cards, target), isBlock: false);
    }

    /// <summary>Captures the selected defense cards, commits defense once, then animates snapshots only.</summary>
    public bool TryDefendCards(IReadOnlyList<CardView> selection, EnemyRuntime target = null)
    {
        var combat = CombatManager.Instance;
        if (combat == null || !combat.CanDefendWithCards(selection, target)) return false;
        var cards = new CardView[selection.Count];
        for (int i = 0; i < cards.Length; i++) cards[i] = selection[i];
        if (!isActiveAndEnabled || !TryGetBlockPresentationPoints(out var canvasRect,
                out var blockPoint, out var deckPoint) || cards.Length == 0)
            return combat.TryDefendWithCards(cards, target);

        if (!TryCaptureVisuals(cards, canvasRect, true, null, out var staged, out var origins, out var draggedFlags))
            return combat.TryDefendWithCards(cards, target);
        return CommitAndAnimate(staged, origins, draggedFlags, blockPoint, deckPoint,
            () => combat.TryDefendWithCards(cards, target), isBlock: true);
    }

    bool TryCaptureVisuals(CardView[] cards, RectTransform canvasRect, bool isBlock, CardView draggedCard,
        out List<RectTransform> staged, out List<Vector2> origins, out List<bool> draggedFlags)
    {
        staged = new List<RectTransform>(cards.Length);
        origins = new List<Vector2>(cards.Length);
        draggedFlags = new List<bool>(cards.Length);
        for (int i = 0; i < cards.Length; i++)
        {
            var view = cards[i];
            if (view == null || view.visualRoot == null ||
                !TryPoint(canvasRect, view.visualRoot, out var origin))
            {
                DestroyStaged(staged);
                return false;
            }
            var clone = Instantiate(view.visualRoot, canvasRect, false);
            clone.name = isBlock ? "Block Card Presentation" : "Attack Card Presentation";
            var badge = clone.Find("SelectionBadge");
            if (badge != null) badge.gameObject.SetActive(false);
            clone.anchorMin = clone.anchorMax = new Vector2(0.5f, 0.5f);
            clone.pivot = new Vector2(0.5f, 0.5f);
            clone.sizeDelta = view.visualRoot.rect.size;
            clone.localScale = Vector3.one * ScreenScale(canvasRect, view.visualRoot);
            clone.localRotation = Quaternion.Euler(0f, 0f, view.visualRoot.eulerAngles.z - canvasRect.eulerAngles.z);
            clone.anchoredPosition = origin;
            MakeNonInteractive(clone);
            staged.Add(clone);
            origins.Add(origin);
            draggedFlags.Add(view == draggedCard);
        }
        return true;
    }

    bool CommitAndAnimate(List<RectTransform> staged, List<Vector2> origins, List<bool> draggedFlags,
        Vector2 targetPoint, Vector2 deckPoint, System.Func<bool> commit, bool isBlock)
    {
        _pendingCommits++;
        _commitImpactDelay = isBlock ? BlockTravelTime
            : draggedFlags.Contains(true) ? WindupTime + DraggedTravelTime : AttackTravelTime;
        _wasBusy = true;
        bool committed;
        try
        {
            committed = commit();
        }
        catch
        {
            _pendingCommits--;
            DestroyStaged(staged);
            NotifyIfIdle(cancelled: true);
            throw;
        }
        _pendingCommits--;
        if (!committed)
        {
            DestroyStaged(staged);
            NotifyIfIdle(cancelled: true);
            return false;
        }

        float stagger = isBlock ? 0.05f : HitSpacing;
        for (int i = 0; i < staged.Count; i++)
        {
            var clone = staged[i];
            _activeCards.Add(clone);
            StartCoroutine(AnimateCard(clone, origins[i], targetPoint, deckPoint,
                isBlock ? false : draggedFlags[i], i * stagger, isBlock));
        }
        NotifyIfIdle();
        return true;
    }

    public void CancelAll()
    {
        bool wasBusy = _wasBusy || IsBusy;
        StopAllCoroutines();
        _pendingCommits = 0;
        _commitImpactDelay = 0f;
        for (int i = 0; i < _hitNumbers.Count; i++)
            if (_hitNumbers[i] != null) DestroyVisual(_hitNumbers[i]);
        _hitNumbers.Clear();
        for (int i = 0; i < _activeCards.Count; i++)
            if (_activeCards[i] != null) DestroyVisual(_activeCards[i].gameObject);
        _activeCards.Clear();
        for (int i = 0; i < _flashes.Count; i++)
            if (_flashes[i] != null) DestroyVisual(_flashes[i]);
        _flashes.Clear();
        if (wasBusy)
        {
            _wasBusy = false;
            OnPresentationCancelled?.Invoke();
        }
    }

    // One label per authoritative result, never per animated card. Capture the enemy point
    // synchronously so lethal hits remain visible after the enemy view is removed.
    void HandleDamageResolved(DamageResult result)
    {
        if (result.TargetAlreadyDefeated || !(result.Request.Target is EnemyRuntime enemy) ||
            !TryGetEnemyPoint(enemy, out _, out var point)) return;
        var number = new GameObject("Combat Hit Number", typeof(RectTransform), typeof(TextMeshProUGUI));
        number.transform.SetParent(presentationCanvas.transform, false);
        var rect = (RectTransform)number.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 84f);
        point += new Vector2(0f, 135f + result.Request.HitIndex % 3 * 56f);
        rect.anchoredPosition = point;
        var label = number.GetComponent<TextMeshProUGUI>();
        label.text = FormatHitNumber(result);
        if (DealbreakUIStyle.Theme && DealbreakUIStyle.Theme.numericFont) label.font = DealbreakUIStyle.Theme.numericFont;
        label.fontSize = result.Request.IsCritical ? 60f : 52f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = result.Request.IsCritical ? new Color(1f, 0.82f, 0.2f) : Color.white;
        DealbreakUIStyle.Outline(label, 0.32f);
        label.raycastTarget = false;
        var canvas = number.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 66;
        _hitNumbers.Add(number);
        _wasBusy = true;
        StartCoroutine(AnimateHitNumber(number, label, point, ImpactDelay(result.Request.HitIndex)));
    }

    /// <summary>
    /// Seconds until the given hit of the command being committed visibly lands; zero outside a
    /// presented commit (Tricks, enemy reactions), so those reactions play at once.
    /// </summary>
    public float ImpactDelay(int hitIndex) =>
        _pendingCommits > 0 ? _commitImpactDelay + Mathf.Min(hitIndex, 7) * HitSpacing : 0f;

    public static string FormatHitNumber(DamageResult result)
    {
        string value = result.ShieldConsumed ? "Blocked" : result.ActualHpLost.ToString();
        return result.Request.IsCritical ? $"Crit {value}" : value;
    }

    IEnumerator AnimateHitNumber(GameObject number, TMP_Text label, Vector2 point, float delay)
    {
        label.enabled = false;
        if (delay > 0f) yield return Wait(delay);
        label.enabled = true;
        var rect = (RectTransform)number.transform;
        float elapsed = 0f;
        while (elapsed < HitNumberTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / HitNumberTime);
            // Pop in with a small overshoot, then drift up and fade.
            float pop = Mathf.Clamp01(elapsed / 0.16f);
            rect.localScale = Vector3.one * Mathf.LerpUnclamped(1.45f, 1f, EaseOutBack(pop));
            rect.anchoredPosition = point + Vector2.up * (55f * EaseOutCubic(t));
            label.alpha = 1f - Mathf.InverseLerp(0.6f, 1f, t);
            yield return null;
        }
        _hitNumbers.Remove(number);
        DestroyVisual(number);
        NotifyIfIdle();
    }

    void NotifyIfIdle(bool cancelled = false)
    {
        if (IsBusy)
        {
            _wasBusy = true;
            return;
        }
        if (!_wasBusy) return;
        _wasBusy = false;
        if (cancelled) OnPresentationCancelled?.Invoke();
        else OnPresentationCompleted?.Invoke();
    }

    bool TryGetPresentationPoints(EnemyRuntime target, out RectTransform canvasRect,
        out Vector2 targetPoint, out Vector2 deckPoint)
    {
        canvasRect = presentationCanvas != null ? presentationCanvas.transform as RectTransform : null;
        targetPoint = deckPoint = default;
        if (canvasRect == null || !presentationCanvas.isActiveAndEnabled ||
            presentationCanvas.sortingOrder >= 100 || deckTarget == null || !deckTarget.gameObject.activeInHierarchy ||
            enemyGroup == null || redCardBack == null ||
            !TryPoint(canvasRect, deckTarget, out deckPoint))
            return false;

        return TryGetEnemyPoint(target, out canvasRect, out targetPoint);
    }

    bool TryGetEnemyPoint(EnemyRuntime target, out RectTransform canvasRect, out Vector2 targetPoint)
    {
        canvasRect = presentationCanvas != null ? presentationCanvas.transform as RectTransform : null;
        targetPoint = default;
        if (canvasRect == null || !presentationCanvas.isActiveAndEnabled ||
            presentationCanvas.sortingOrder >= 100 || enemyGroup == null) return false;
        var views = enemyGroup.ActiveViews;
        for (int i = 0; i < views.Count; i++)
        {
            var view = views[i];
            if (view == null || !view.gameObject.activeInHierarchy ||
                !ReferenceEquals(view.DisplayedEnemy, target)) continue;
            var targetRect = view.portrait != null ? view.portrait.rectTransform : view.transform as RectTransform;
            return targetRect != null && TryPoint(canvasRect, targetRect, out targetPoint);
        }
        return false;
    }

    bool TryGetBlockPresentationPoints(out RectTransform canvasRect, out Vector2 blockPoint, out Vector2 deckPoint)
    {
        canvasRect = presentationCanvas != null ? presentationCanvas.transform as RectTransform : null;
        blockPoint = deckPoint = default;
        if (canvasRect == null || !presentationCanvas.isActiveAndEnabled ||
            presentationCanvas.sortingOrder >= 100 || deckTarget == null || !deckTarget.gameObject.activeInHierarchy ||
            enemyGroup == null || enemyGroup.enemyContainer == null || redCardBack == null ||
            !TryPoint(canvasRect, deckTarget, out deckPoint))
            return false;
        return TryPoint(canvasRect, enemyGroup.enemyContainer, out blockPoint);
    }

    bool TryPoint(RectTransform canvasRect, RectTransform source, out Vector2 point)
    {
        point = default;
        if (source == null || !source.gameObject.activeInHierarchy) return false;
        var sourceCanvas = source.GetComponentInParent<Canvas>();
        var sourceCamera = CameraFor(sourceCanvas);
        var screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, source.TransformPoint(source.rect.center));
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen,
            CameraFor(presentationCanvas), out point);
    }

    static Camera CameraFor(Canvas canvas) => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
        ? canvas.worldCamera != null ? canvas.worldCamera : Camera.main : null;

    float ScreenScale(RectTransform canvasRect, RectTransform source)
    {
        var sourceCanvas = source.GetComponentInParent<Canvas>();
        var camera = CameraFor(sourceCanvas);
        Vector3 left = source.TransformPoint(new Vector3(source.rect.xMin, source.rect.center.y));
        Vector3 right = source.TransformPoint(new Vector3(source.rect.xMax, source.rect.center.y));
        var screenLeft = RectTransformUtility.WorldToScreenPoint(camera, left);
        var screenRight = RectTransformUtility.WorldToScreenPoint(camera, right);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenLeft,
            CameraFor(presentationCanvas), out var localLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenRight,
            CameraFor(presentationCanvas), out var localRight);
        return source.rect.width > 0f ? Vector2.Distance(localLeft, localRight) / source.rect.width : 1f;
    }

    static void MakeNonInteractive(RectTransform clone)
    {
        // Keep the cloned visual's own Canvas enabled: disabling it hides its graphics
        // rather than moving them to the parent Canvas. Sorting 60 is above gameplay
        // and route overlays but below the pause/results overlay (100).
        var rootCanvas = clone.GetComponent<Canvas>();
        if (rootCanvas == null) rootCanvas = clone.gameObject.AddComponent<Canvas>();
        rootCanvas.enabled = true;
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = 60;
        foreach (var canvas in clone.GetComponentsInChildren<Canvas>(true))
            if (canvas != rootCanvas) canvas.enabled = false;
        foreach (var raycaster in clone.GetComponentsInChildren<GraphicRaycaster>(true)) raycaster.enabled = false;
        foreach (var graphic in clone.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        foreach (var group in clone.GetComponentsInChildren<CanvasGroup>(true))
        {
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        var rootGroup = clone.GetComponent<CanvasGroup>();
        if (rootGroup == null) rootGroup = clone.gameObject.AddComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;
    }

    IEnumerator AnimateCard(RectTransform card, Vector2 origin, Vector2 impact, Vector2 deck,
        bool dragged, float delay, bool isBlock)
    {
        if (delay > 0f) yield return Wait(delay);
        if (!isBlock && dragged)
        {
            Vector2 backward = (origin - impact).normalized * 32f;
            yield return Move(card, origin, origin + backward, WindupTime, Ease.InOut);
            origin += backward;
        }
        float travelTime = isBlock ? BlockTravelTime : dragged ? DraggedTravelTime : AttackTravelTime;
        // Accelerate into the target so the hit lands with weight.
        yield return Move(card, origin, impact, travelTime, Ease.In);
        yield return Flash(impact, isBlock);
        yield return FlipToBack(card);
        yield return Move(card, impact, deck, ReturnTime, Ease.InOut, true);
        _activeCards.Remove(card);
        DestroyVisual(card.gameObject);
        NotifyIfIdle();
    }

    static IEnumerator Wait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    enum Ease { In, InOut }

    static float EaseOutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    static float EaseOutBack(float t)
    {
        const float overshoot = 1.7f;
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }

    static IEnumerator Move(RectTransform rect, Vector2 from, Vector2 to, float duration, Ease ease, bool shrink = false)
    {
        float elapsed = 0f;
        Vector3 originalScale = rect.localScale;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = ease == Ease.In ? 1f - Mathf.Cos(t * Mathf.PI * 0.5f) : t * t * (3f - 2f * t);
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            if (shrink) rect.localScale = originalScale * Mathf.Lerp(1f, 0.22f, eased);
            yield return null;
        }
        rect.anchoredPosition = to;
    }

    IEnumerator Flash(Vector2 position, bool isBlock)
    {
        var flash = new GameObject("Attack Impact Flash", typeof(RectTransform), typeof(Image));
        flash.transform.SetParent(presentationCanvas.transform, false);
        var rect = (RectTransform)flash.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(85f, 85f);
        rect.anchoredPosition = position;
        var image = flash.GetComponent<Image>();
        image.raycastTarget = false;
        var flashCanvas = flash.AddComponent<Canvas>();
        flashCanvas.overrideSorting = true;
        flashCanvas.sortingOrder = 65;
        _flashes.Add(flash);
        float elapsed = 0f;
        while (elapsed < FlashTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / FlashTime);
            float fade = (1f - t) * (1f - t);
            image.color = isBlock
                ? new Color(0.2f, 0.85f, 1f, 0.7f * fade)
                : new Color(1f, 0.45f, 0.18f, 0.7f * fade);
            rect.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.45f, EaseOutCubic(t));
            yield return null;
        }
        _flashes.Remove(flash);
        DestroyVisual(flash);
    }

    IEnumerator FlipToBack(RectTransform card)
    {
        Vector3 scale = card.localScale;
        float elapsed = 0f;
        while (elapsed < FlipHalfTime)
        {
            elapsed += Time.deltaTime;
            card.localScale = new Vector3(scale.x * (1f - Mathf.Clamp01(elapsed / FlipHalfTime)), scale.y, scale.z);
            yield return null;
        }
        // Hide the complete front hierarchy, including stats/labels/badge; never edit the original view.
        for (int i = card.childCount - 1; i >= 0; i--)
            card.GetChild(i).gameObject.SetActive(false);
        var back = new GameObject("Red Card Back", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(card, false);
        var backRect = (RectTransform)back.transform;
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = backRect.offsetMax = Vector2.zero;
        var image = back.GetComponent<Image>();
        image.sprite = redCardBack;
        image.preserveAspect = true;
        image.raycastTarget = false;
        elapsed = 0f;
        while (elapsed < FlipHalfTime)
        {
            elapsed += Time.deltaTime;
            card.localScale = new Vector3(scale.x * Mathf.Clamp01(elapsed / FlipHalfTime), scale.y, scale.z);
            yield return null;
        }
        card.localScale = scale;
    }

    static void DestroyStaged(List<RectTransform> staged)
    {
        for (int i = 0; i < staged.Count; i++)
            if (staged[i] != null) DestroyVisual(staged[i].gameObject);
    }

    static void DestroyVisual(GameObject visual)
    {
        visual.SetActive(false);
        if (Application.isPlaying) Destroy(visual);
        else DestroyImmediate(visual);
    }
}
