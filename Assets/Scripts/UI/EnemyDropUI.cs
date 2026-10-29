using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// An enemy's Trick drop. With room in the backpack it flies straight into its slot. With the
/// backpack full it waits at the top of the stage, and the run waits with it: make room (use, sell or
/// discard a Trick), then drag the drop onto the Trick slots, or press LEAVE IT to go on without it.
/// Presentation only; RunManager owns the pending drop.
/// </summary>
public sealed class EnemyDropUI : MonoBehaviour
{
    // Above the board and hand cards, below the drag layer (80) and the hover detail (90).
    const int SortingOrder = 70;
    // The drop row: the top band of the stage (x .06-.88), the pair centred in it (hud.md section C).
    // It sits over the enemy row's band, but no enemy stands then: the drop is granted only on victory.
    public static readonly Vector2 RowMin = new(.06f, .74f), RowMax = new(.88f, .82f);
    public const float TokenSize = 150f, LeaveWidth = 190f, LeaveHeight = 50f, Gap = 24f, LeaveLabelSize = 22f;
    // Slap on arrival: 120ms in from a larger size to a slight undershoot, then an 80ms settle (hud.md motion table).
    public const float SlapInSeconds = .12f, SlapSettleSeconds = .08f, SlapScale = .15f, SlapUndershoot = .04f;

    RunManager _run;
    RectTransform _root;
    RectTransform _row;
    RectTransform _token;
    TMP_Text _tokenLabel;
    Button _leave;
    Vector2 _home;
    Vector3 _lastEnemyArea;
    float _slapStart = -1f;

    internal RectTransform Row => _row;
    internal RectTransform Token => _token;
    internal Button LeaveButton => _leave;
    internal Vector2 TokenHome => _home;
    public bool IsSlapping => _slapStart >= 0f;
    public bool Showing => _token && _token.gameObject.activeSelf;

    public static void Install(Canvas canvas)
    {
        if (!canvas || canvas.GetComponentInChildren<EnemyDropUI>(true)) return;
        var host = new GameObject("Enemy Drop Layer", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var rect = (RectTransform)host.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var layer = host.GetComponent<Canvas>();
        layer.overrideSorting = true;
        layer.sortingOrder = SortingOrder;
        var ui = host.AddComponent<EnemyDropUI>();
        ui._root = rect;
        ui.Build();
    }

    /// <summary>Binds to <paramref name="run"/> now (Update otherwise follows RunManager.Instance).</summary>
    internal void Bind(RunManager run) => Subscribe(run);

    void Build()
    {
        var theme = DealbreakUIStyle.Theme;
        _row = new GameObject("Drop Row", typeof(RectTransform)).GetComponent<RectTransform>();
        _row.SetParent(_root, false);
        _row.anchorMin = RowMin;
        _row.anchorMax = RowMax;
        _row.offsetMin = _row.offsetMax = Vector2.zero;

        // The token and LEAVE IT side by side, the pair centred in the row.
        float half = (TokenSize + Gap + LeaveWidth) * .5f;
        _home = new Vector2(-half + TokenSize * .5f, 0f);
        var leaveCentre = new Vector2(half - LeaveWidth * .5f, 0f);

        var token = new GameObject("Enemy Drop", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorialFrame),
            typeof(DropToken), typeof(ContextualInfoTrigger));
        _token = (RectTransform)token.transform;
        _token.SetParent(_row, false);
        _token.anchorMin = _token.anchorMax = new Vector2(.5f, .5f);
        _token.sizeDelta = new Vector2(TokenSize, TokenSize);
        _token.anchoredPosition = _home;
        var frame = token.GetComponent<EditorialFrame>();
        frame.border = 2f;
        frame.corner = 0f;
        frame.showAccent = false;
        // An item sticker with a cream cut line (teal is for selection and valid targets only).
        if (theme) { frame.fill = theme.charcoal; frame.ink = theme.inkPrint; frame.rim = theme.paper; }
        token.GetComponent<DropToken>().owner = this;
        var detail = token.GetComponent<ContextualInfoTrigger>();
        detail.allowWhileBlocked = true;
        detail.describe = () => _run != null && _run.PendingEnemyDrop != null
            ? new ContextualInfo("Enemy drop", _run.PendingEnemyDrop.displayName, ModSignature.TrickDescription(_run.PendingEnemyDrop))
            : (ContextualInfo?)null;

        _tokenLabel = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
            .GetComponent<TMP_Text>();
        var labelRect = _tokenLabel.rectTransform;
        labelRect.SetParent(_token, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 10f);
        labelRect.offsetMax = new Vector2(-10f, -10f);
        if (theme) DealbreakUIStyle.Text(_tokenLabel, theme.headingFont, 26, theme.paper);
        _tokenLabel.alignment = TextAlignmentOptions.Center;
        _tokenLabel.raycastTarget = false;

        var leave = new GameObject("LeaveDrop", typeof(RectTransform), typeof(Image), typeof(Button));
        var leaveRect = (RectTransform)leave.transform;
        leaveRect.SetParent(_row, false);
        leaveRect.anchorMin = leaveRect.anchorMax = new Vector2(.5f, .5f);
        leaveRect.anchoredPosition = leaveCentre;
        leaveRect.sizeDelta = new Vector2(LeaveWidth, LeaveHeight);
        _leave = leave.GetComponent<Button>();
        _leave.targetGraphic = leave.GetComponent<Image>();
        var leaveLabel = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
            .GetComponent<TMP_Text>();
        leaveLabel.rectTransform.SetParent(leaveRect, false);
        leaveLabel.rectTransform.anchorMin = Vector2.zero;
        leaveLabel.rectTransform.anchorMax = Vector2.one;
        leaveLabel.rectTransform.offsetMin = leaveLabel.rectTransform.offsetMax = Vector2.zero;
        leaveLabel.text = "LEAVE IT";
        leaveLabel.alignment = TextAlignmentOptions.Center;
        _leave.onClick.AddListener(() => _run?.LeavePendingEnemyDrop());
        // A cream paper button, styled as MAP and BLOCK are.
        if (theme)
            DealbreakUIStyle.CutSheetButton(_leave, EditorialButton.Role.Paper, CutSheetMetrics.ShadowButton,
                theme.labelFont ? theme.labelFont : theme.headingFont, LeaveLabelSize, false);
        _token.gameObject.SetActive(false);
        _leave.gameObject.SetActive(false);
        Refresh();
    }

    void Update()
    {
        if (_run != RunManager.Instance) Subscribe(RunManager.Instance);
        // A waiting drop appears once the last enemy's defeat has finished playing.
        bool waiting = _run != null && _run.PendingEnemyDrop != null && !DealbreakUIRuntime.BoardPresentationBusy;
        if (_token && _token.gameObject.activeSelf != waiting) Refresh();
        var enemies = FindFirstObjectByType<EnemyGroupUI>();
        if (enemies && enemies.isActiveAndEnabled) _lastEnemyArea = enemies.transform.position;
    }

    void LateUpdate() => TickSlap(Time.unscaledTime);

    void Subscribe(RunManager run)
    {
        if (_run != null)
        {
            _run.OnEnemyDropChanged -= Refresh;
            _run.OnEnemyDropClaimed -= HandleClaimed;
        }
        _run = run;
        if (_run != null)
        {
            _run.OnEnemyDropChanged += Refresh;
            _run.OnEnemyDropClaimed += HandleClaimed;
        }
        Refresh();
    }

    void OnDestroy() => Subscribe(null);

    void Refresh()
    {
        if (!_token) return;
        var drop = _run != null ? _run.PendingEnemyDrop : null;
        bool waiting = drop != null && !DealbreakUIRuntime.BoardPresentationBusy;
        bool arriving = waiting && !_token.gameObject.activeSelf;
        _token.gameObject.SetActive(waiting);
        _leave.gameObject.SetActive(waiting);
        if (!waiting) { EndSlap(); return; }
        _token.anchoredPosition = _home;
        // A sprite fills the token above the name; without one the glyph text sits over the name.
        bool hasSprite = ItemIconUI.Show(_token, drop.iconSprite, new Vector2(.14f, .26f), new Vector2(.86f, .94f));
        string dropName = drop.displayName.ToUpperInvariant();
        _tokenLabel.alignment = hasSprite ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Center;
        _tokenLabel.text = hasSprite ? $"<size=70%>{dropName}</size>"
            : $"<size=140%>{drop.icon}</size>\n<size=80%>{dropName}</size>";
        if (arriving) StartSlap(Time.unscaledTime);
    }

    // The token and LEAVE IT slap in together; under Reduce Motion they are simply there.
    void StartSlap(float now)
    {
        if (CutSheetMotion.ReduceMotion) { EndSlap(); return; }
        _slapStart = now;
        SetScale(1f + SlapScale);
    }

    void EndSlap()
    {
        _slapStart = -1f;
        SetScale(1f);
    }

    internal void TickSlap(float now)
    {
        if (_slapStart < 0f) return;
        float inTime = CutSheetMotion.Seconds(SlapInSeconds), settle = CutSheetMotion.Seconds(SlapSettleSeconds);
        float elapsed = now - _slapStart;
        if (inTime + settle <= 0f || elapsed >= inTime + settle) { EndSlap(); return; }
        float scale;
        if (elapsed < inTime)
        {
            float t = elapsed / inTime;
            scale = Mathf.Lerp(1f + SlapScale, 1f - SlapUndershoot, 1f - (1f - t) * (1f - t));
        }
        else
        {
            float t = settle > 0f ? (elapsed - inTime) / settle : 1f;
            scale = Mathf.Lerp(1f - SlapUndershoot, 1f, t);
        }
        SetScale(scale);
    }

    // The only writer of the token's and LEAVE IT's scale.
    void SetScale(float scale)
    {
        var target = Vector3.one * scale;
        if (_token && _token.localScale != target) _token.localScale = target;
        if (_leave && _leave.transform.localScale != target) _leave.transform.localScale = target;
    }

    void HandleClaimed(ConsumableData drop)
    {
        var slot = ItemDragUI.LatestTrickSlot(drop, out bool isNew);
        if (!slot) return;
        Vector3 from = _token && _token.gameObject.activeInHierarchy ? _token.position
            : _lastEnemyArea != Vector3.zero ? _lastEnemyArea : _root.position;
        string icon = string.IsNullOrEmpty(drop.icon) ? drop.displayName : drop.icon;
        ItemDragUI.Get()?.FlyIntoSlot(from, slot, icon.Length > 6 ? icon.Substring(0, 6) : icon, drop.iconSprite,
            revealOnLanding: isNew);
    }

    // The drop is claimed by releasing it over the Trick slots (or clicking it when there is room).
    internal void Drag(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_token.parent, screen, null, out var local);
        _token.anchoredPosition = local - ((RectTransform)_token.parent).rect.size * (_token.anchorMin - new Vector2(.5f, .5f))
            - ((RectTransform)_token.parent).rect.center;
    }

    internal void Release(Vector2 screen)
    {
        if (OverTrickSlots(screen) && _run != null && _run.ClaimPendingEnemyDrop()) return;
        StartCoroutine(ReturnHome());
    }

    internal void Click()
    {
        if (_run == null || !_run.ClaimPendingEnemyDrop()) StartCoroutine(Shake());
    }

    static bool OverTrickSlots(Vector2 screen)
    {
        var actions = FindFirstObjectByType<ActionButtonsUI>();
        if (!actions || actions.backpackSlotButtons == null) return false;
        foreach (var slot in actions.backpackSlotButtons)
            if (slot && slot.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint((RectTransform)slot.transform, screen, null))
                return true;
        return false;
    }

    IEnumerator ReturnHome()
    {
        // Under Reduce Motion the token is simply back home.
        if (CutSheetMotion.ReduceMotion)
        {
            if (_token) _token.anchoredPosition = _home;
            yield break;
        }
        Vector2 start = _token.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < .18f && _token)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / .18f);
            _token.anchoredPosition = Vector2.Lerp(start, _home, 1f - (1f - t) * (1f - t));
            yield return null;
        }
        yield return Shake();
    }

    // A full backpack: the drop shakes to show it cannot go in yet.
    IEnumerator Shake()
    {
        // A local wobble, not screen shake: only Reduce Motion settles it (read live), so a full
        // backpack still answers with Screen Shake off.
        if (CutSheetMotion.ReduceMotion)
        {
            if (_token) _token.anchoredPosition = _home;
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < .25f && _token)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / .25f;
            _token.anchoredPosition = _home + new Vector2(Mathf.Sin(t * 40f) * 10f * (1f - t), 0f);
            yield return null;
        }
        if (_token) _token.anchoredPosition = _home;
    }

    sealed class DropToken : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public EnemyDropUI owner;
        bool _dragging;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || GameplayInputGate.IsBlocked) return;
            _dragging = true;
            if (ContextualTooltipUI.Instance) ContextualTooltipUI.Instance.Release(this);
            owner.StopAllCoroutines();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging) owner.Drag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            owner.Release(eventData.position);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!eventData.dragging && eventData.button == PointerEventData.InputButton.Left && !GameplayInputGate.IsBlocked)
                owner.Click();
        }
    }
}
