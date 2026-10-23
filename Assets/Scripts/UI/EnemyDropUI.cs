using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// An enemy's Trick drop. With room in the backpack it flies straight into its slot. With the
/// backpack full it waits on the board, and the run waits with it: make room (use, sell or discard
/// a Trick), then drag the drop onto the Trick slots, or press LEAVE IT to go on without it.
/// Presentation only; RunManager owns the pending drop.
/// </summary>
public sealed class EnemyDropUI : MonoBehaviour
{
    // Above the board and hand cards, below the drag layer (80) and the hover detail (90).
    const int SortingOrder = 70;

    RunManager _run;
    RectTransform _root;
    RectTransform _token;
    TMP_Text _tokenLabel;
    RectTransform _tokenRest;
    Button _leave;
    Vector3 _lastEnemyArea;

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

    void Build()
    {
        var theme = DealbreakUIStyle.Theme;
        _tokenRest = new GameObject("Drop Rest", typeof(RectTransform)).GetComponent<RectTransform>();
        _tokenRest.SetParent(_root, false);
        _tokenRest.anchorMin = _tokenRest.anchorMax = new Vector2(.5f, .62f);

        var token = new GameObject("Enemy Drop", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorialFrame),
            typeof(DropToken), typeof(ContextualInfoTrigger));
        _token = (RectTransform)token.transform;
        _token.SetParent(_root, false);
        _token.anchorMin = _token.anchorMax = new Vector2(.5f, .62f);
        _token.sizeDelta = new Vector2(150f, 150f);
        var frame = token.GetComponent<EditorialFrame>();
        frame.border = 2f;
        frame.corner = 0f;
        frame.showAccent = false;
        if (theme) { frame.fill = theme.charcoal; frame.ink = theme.ink; frame.rim = theme.ochre; }
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
        leaveRect.SetParent(_root, false);
        leaveRect.anchorMin = leaveRect.anchorMax = new Vector2(.5f, .62f);
        leaveRect.anchoredPosition = new Vector2(0f, -112f);
        leaveRect.sizeDelta = new Vector2(190f, 50f);
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
        DealbreakUIStyle.Button(_leave, EditorialButton.Role.Secondary);
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
        _token.gameObject.SetActive(waiting);
        _leave.gameObject.SetActive(waiting);
        if (!waiting) return;
        _token.anchoredPosition = Vector2.zero;
        // A sprite fills the token above the name; without one the glyph text sits over the name.
        bool hasSprite = ItemIconUI.Show(_token, drop.iconSprite, new Vector2(.14f, .26f), new Vector2(.86f, .94f));
        string dropName = drop.displayName.ToUpperInvariant();
        _tokenLabel.alignment = hasSprite ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Center;
        _tokenLabel.text = hasSprite ? $"<size=70%>{dropName}</size>"
            : $"<size=140%>{drop.icon}</size>\n<size=80%>{dropName}</size>";
    }

    void HandleClaimed(ConsumableData drop)
    {
        var slot = SlotHolding(drop);
        if (!slot) return;
        Vector3 from = _token && _token.gameObject.activeInHierarchy ? _token.position
            : _lastEnemyArea != Vector3.zero ? _lastEnemyArea : _root.position;
        string icon = string.IsNullOrEmpty(drop.icon) ? drop.displayName : drop.icon;
        ItemDragUI.Get()?.FlyIntoSlot(from, slot, icon.Length > 6 ? icon.Substring(0, 6) : icon, drop.iconSprite,
            revealOnLanding: true);
    }

    static RectTransform SlotHolding(ConsumableData drop)
    {
        var cards = CardManager.Instance;
        var actions = FindFirstObjectByType<ActionButtonsUI>();
        if (cards == null || actions == null || actions.backpackSlotButtons == null) return null;
        for (int i = 0; i < actions.backpackSlotButtons.Length; i++)
            if (cards.GetConsumableAtSlot(i) == drop && actions.backpackSlotButtons[i])
                return (RectTransform)actions.backpackSlotButtons[i].transform;
        return null;
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
        Vector2 start = _token.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < .18f && _token)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / .18f);
            _token.anchoredPosition = Vector2.Lerp(start, Vector2.zero, 1f - (1f - t) * (1f - t));
            yield return null;
        }
        yield return Shake();
    }

    // A full backpack: the drop shakes to show it cannot go in yet.
    IEnumerator Shake()
    {
        float elapsed = 0f;
        while (elapsed < .25f && _token)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / .25f;
            _token.anchoredPosition = new Vector2(Mathf.Sin(t * 40f) * 10f * (1f - t), 0f);
            yield return null;
        }
        if (_token) _token.anchoredPosition = Vector2.zero;
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
