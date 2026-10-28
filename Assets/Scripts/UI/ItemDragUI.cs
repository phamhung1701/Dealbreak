using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drags a Trick or Gear item like a card. A ghost follows the pointer; the thing under it is
/// resolved to an <see cref="ItemDropTarget"/> and, when <see cref="ItemDropRules"/> accepts it,
/// ringed with the change the drop would make. Over the Black Market the whole market darkens
/// and shows the sale price. While an item is held, a red discard strip stands down the right
/// edge; dropping there throws the item away. A Gear dropped on a matching copy
/// merges them. Releasing anywhere else returns the ghost to its slot.
/// Presentation only: every effect runs through ItemDropRules.
/// </summary>
public sealed class ItemDragUI : MonoBehaviour
{
    // Above hand cards (1-21) and attack visuals (60-66); below the hover detail (90) and menus.
    const int SortingOrder = 80;
    const float ReturnTime = .18f;

    static ItemDragUI _instance;

    RectTransform _root;
    RectTransform _ghost;
    TMP_Text _ghostLabel;
    EditorialFrame _ghostFrame;
    RectTransform _ring;
    RectTransform _tag;
    TMP_Text _tagLabel;
    RectTransform _marketShade;
    TMP_Text _marketLabel;
    RectTransform _discardStrip;
    Image _discardImage;
    TMP_Text _discardLabel;

    bool _dragging;
    bool _isGear;
    int _slot = -1;
    long _gearId;
    RectTransform _source;
    CanvasGroup _sourceGroup;
    ItemDropTarget _target;
    bool _targetValid;
    Coroutine _settle;
    readonly List<RaycastResult> _hits = new();
    readonly List<CardInstance> _cardTargets = new();
    readonly Vector3[] _corners = new Vector3[4];

    public static bool IsDragging => _instance != null && _instance._dragging;

    public static ItemDragUI Get()
    {
        if (_instance) return _instance;
        var canvas = CardManager.Instance != null ? CardManager.Instance.dragCanvas : null;
        if (!canvas) canvas = Object.FindFirstObjectByType<Canvas>();
        if (!canvas) return null;
        var host = new GameObject("Item Drag Layer", typeof(RectTransform), typeof(Canvas));
        var rect = (RectTransform)host.transform;
        rect.SetParent(canvas.rootCanvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var layer = host.GetComponent<Canvas>();
        layer.overrideSorting = true;
        layer.sortingOrder = SortingOrder;
        _instance = host.AddComponent<ItemDragUI>();
        _instance._root = rect;
        return _instance;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    public bool BeginTrick(int slot, RectTransform source, PointerEventData eventData)
    {
        var cards = CardManager.Instance;
        if (_dragging || cards == null || cards.GetConsumableInstanceAtSlot(slot) == null || GameplayInputGate.IsBlocked)
            return false;
        _isGear = false;
        _slot = slot;
        Begin(source, eventData);
        // Card-targeting Tricks light up the hand cards they can be dropped on.
        ItemDropRules.CollectCardTargets(slot, _cardTargets);
        if (_cardTargets.Count > 0 || IsCardTrick(slot)) SetHandTargetState(true);
        return true;
    }

    public bool BeginGear(long gearId, RectTransform source, PointerEventData eventData)
    {
        var cards = CardManager.Instance;
        if (_dragging || cards == null || cards.GetGearInstanceById(gearId) == null || GameplayInputGate.IsBlocked)
            return false;
        _isGear = true;
        _gearId = gearId;
        _slot = -1;
        Begin(source, eventData);
        return true;
    }

    void Begin(RectTransform source, PointerEventData eventData)
    {
        // A drop's return/vanish animation must not hide the next drag's ghost.
        if (_settle != null) StopCoroutine(_settle);
        _settle = null;
        RestoreSource();
        if (_ghost) _ghost.localScale = Vector3.one;
        _dragging = true;
        _source = source;
        // Pressing a slot selects it, and a selected slot keeps its hover detail open; picking it
        // up ends both so the detail does not linger after the drop.
        var events = EventSystem.current;
        if (events && events.currentSelectedGameObject && events.currentSelectedGameObject.transform.IsChildOf(source))
            events.SetSelectedGameObject(null);
        if (ContextualTooltipUI.Instance) ContextualTooltipUI.Instance.Release(source);
        _sourceGroup = source.GetComponent<CanvasGroup>();
        if (!_sourceGroup) _sourceGroup = source.gameObject.AddComponent<CanvasGroup>();
        _sourceGroup.blocksRaycasts = false;
        _sourceGroup.alpha = .35f;
        EnsureVisuals();
        // The ghost carries the slot's sprite, or its glyph text when the item has none.
        var sourceLabel = source.GetComponentInChildren<TMP_Text>();
        bool hasSprite = ItemIconUI.Show(_ghost, ItemIconUI.Shown(source));
        _ghostLabel.text = sourceLabel && !hasSprite ? sourceLabel.text : string.Empty;
        _ghost.sizeDelta = SizeIn(_root, source) * 1.08f;
        _ghost.gameObject.SetActive(true);
        _ghost.SetAsLastSibling();
        Drag(eventData);
    }

    public void Drag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _ghost.anchoredPosition = ToLocal(eventData.position);
        _target = ResolveTarget(eventData);
        string preview = null;
        _targetValid = _target.Kind != ItemDropKind.None && (_isGear
            ? ItemDropRules.CanDropGear(_gearId, _target, out preview)
            : ItemDropRules.CanDropTrick(_slot, _target, out preview));
        ShowFeedback(preview);
    }

    public void End(PointerEventData eventData)
    {
        if (!_dragging) return;
        Drag(eventData);
        var target = _target;
        bool valid = _targetValid;
        bool committed = valid && (_isGear ? ItemDropRules.DropGear(_gearId, target) : ItemDropRules.DropTrick(_slot, target));
        HideFeedback();
        SetHandTargetState(false);
        _dragging = false;
        if (_settle != null) StopCoroutine(_settle);
        _settle = StartCoroutine(committed || target.Kind is ItemDropKind.TrickSlot or ItemDropKind.GearPosition
            ? Vanish() : ReturnToSource());
    }

    /// <summary>
    /// Flies an item from where it was earned into its backpack slot: it rises, then drops along
    /// an arc and the slot pops as it lands. Presentation only; the item is already in the slot, so
    /// with <paramref name="revealOnLanding"/> the slot's contents stay hidden until the flight lands.
    /// </summary>
    public void FlyIntoSlot(Vector3 fromWorld, RectTransform slot, string label, Sprite icon = null,
        bool revealOnLanding = false)
    {
        if (!slot) return;
        EnsureVisuals();
        var flyer = Instantiate(_ghost.gameObject, _root).GetComponent<RectTransform>();
        flyer.name = "Item Drop";
        flyer.gameObject.SetActive(true);
        flyer.sizeDelta = SizeIn(_root, slot) * 1.1f;
        var text = flyer.GetComponentInChildren<TMP_Text>();
        bool hasSprite = ItemIconUI.Show(flyer, icon);
        if (text) text.text = hasSprite ? string.Empty : label;
        var frame = flyer.GetComponent<EditorialFrame>();
        if (frame && DealbreakUIStyle.Theme) { frame.rim = DealbreakUIStyle.Theme.ochre; frame.Refresh(); }
        var held = revealOnLanding ? HoldContents(slot) : null;
        StartCoroutine(Fly(flyer, (Vector2)_root.InverseTransformPoint(fromWorld) - _root.rect.center, slot, held));
    }

    /// <summary>
    /// The backpack slot the latest Trick went into (<see cref="CardManager.LastConsumableSlot"/>), and
    /// whether it is new there: a new slot stays hidden until the flight lands, while a stack it joined
    /// keeps showing the copies already in it.
    /// </summary>
    public static RectTransform LatestTrickSlot(ConsumableData item, out bool isNew)
    {
        isNew = false;
        var cards = CardManager.Instance;
        var actions = FindFirstObjectByType<ActionButtonsUI>();
        if (item == null || cards == null || actions == null || actions.backpackSlotButtons == null) return null;
        int index = cards.LastConsumableSlot;
        if (index < 0 || index >= actions.backpackSlotButtons.Length || cards.GetConsumableAtSlot(index) != item ||
            !actions.backpackSlotButtons[index]) return null;
        isNew = !cards.LastConsumableStacked;
        return (RectTransform)actions.backpackSlotButtons[index].transform;
    }

    // Hides what a slot shows (its icon and labels, not its frame) until the flight lands in it.
    readonly List<CanvasGroup> _held = new();

    List<CanvasGroup> HoldContents(RectTransform slot)
    {
        var held = new List<CanvasGroup>();
        foreach (Transform child in slot)
        {
            if (child.GetComponent<EditorialFrame>() || child.GetComponent<CanvasGroup>()) continue;
            var group = child.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            held.Add(group);
        }
        _held.AddRange(held);
        return held;
    }

    void Release(List<CanvasGroup> held)
    {
        if (held == null) return;
        foreach (var group in held)
        {
            _held.Remove(group);
            if (group) Destroy(group);
        }
    }

    // A flight cut short (a scene change) must not leave a slot looking empty.
    void OnDisable()
    {
        foreach (var group in _held) if (group) Destroy(group);
        _held.Clear();
    }

    const float Rise = .18f, Fall = .42f;
    /// <summary>How long a <see cref="FlyIntoSlot"/> flight takes to reach its slot.</summary>
    public const float FlightSeconds = Rise + Fall;

    IEnumerator Fly(RectTransform flyer, Vector2 start, RectTransform slot, List<CanvasGroup> held)
    {
        Vector2 peak = start + new Vector2(0f, 70f);
        float elapsed = 0f;
        while (elapsed < Rise && flyer)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Rise);
            flyer.anchoredPosition = Vector2.Lerp(start, peak, 1f - (1f - t) * (1f - t));
            flyer.localScale = Vector3.one * Mathf.Lerp(.6f, 1.2f, t);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < Fall && flyer && slot)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Fall);
            Vector2 end = CenterIn(_root, slot);
            Vector2 control = new((peak.x + end.x) * .5f, Mathf.Max(peak.y, end.y) + 120f);
            float eased = t * t;
            flyer.anchoredPosition = (1 - eased) * (1 - eased) * peak + 2 * (1 - eased) * eased * control + eased * eased * end;
            flyer.localScale = Vector3.one * Mathf.Lerp(1.2f, .9f, t);
            yield return null;
        }
        if (flyer) Destroy(flyer.gameObject);
        Release(held);
        if (!slot) yield break;
        // A Cut Sheet slot slaps its face; its pinned root never animates.
        if (slot.TryGetComponent(out CutSheetSlotUI sticker)) { sticker.Slap(); yield break; }
        elapsed = 0f;
        Vector3 rest = slot.localScale;
        while (elapsed < .16f && slot)
        {
            elapsed += Time.unscaledDeltaTime;
            slot.localScale = rest * (1f + Mathf.Sin(Mathf.Clamp01(elapsed / .16f) * Mathf.PI) * .18f);
            yield return null;
        }
        if (slot) slot.localScale = rest;
    }

    /// <summary>Ends a drag without effect, for example when a menu opens mid-drag.</summary>
    public void Cancel()
    {
        if (!_dragging) return;
        HideFeedback();
        SetHandTargetState(false);
        _dragging = false;
        RestoreSource();
        if (_ghost) _ghost.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_dragging && (GameplayInputGate.IsBlocked || !_source)) Cancel();
    }

    ItemDropTarget ResolveTarget(PointerEventData eventData)
    {
        Vector2 screen = eventData.position;
        if (TryDiscardArea(out var discard) && discard.Contains(ToLocal(screen) + _root.rect.center))
            return ItemDropTarget.Discard;
        _hits.Clear();
        if (EventSystem.current) EventSystem.current.RaycastAll(eventData, _hits);
        var cards = CardManager.Instance;
        var actions = Object.FindFirstObjectByType<ActionButtonsUI>();
        foreach (var hit in _hits)
        {
            if (!hit.gameObject || hit.gameObject.transform.IsChildOf(_root)) continue;
            var gearSlot = hit.gameObject.GetComponentInParent<GearIconSlotUI>();
            if (_isGear && gearSlot && gearSlot.Gear && gearSlot.InstanceId != 0 && gearSlot.InstanceId != _gearId)
                return ItemDropTarget.OnGear(gearSlot.InstanceId);
            if (_isGear && gearSlot && !gearSlot.Gear)
                return ItemDropTarget.GearPosition(gearSlot.transform.GetSiblingIndex());
            var view = hit.gameObject.GetComponentInParent<CardView>();
            if (!_isGear && view && view.data != null && view.homeField != null && cards.hand.Contains(view.data))
                return ItemDropTarget.HandCard(view.data);
            var enemy = hit.gameObject.GetComponentInParent<EnemyDisplayUI>();
            if (!_isGear && enemy && enemy.DisplayedEnemy != null && !enemy.DisplayedEnemy.IsDefeated)
                return ItemDropTarget.OnEnemy(enemy.DisplayedEnemy);
            if (!_isGear && actions && actions.backpackSlotButtons != null)
                for (int i = 0; i < actions.backpackSlotButtons.Length; i++)
                    if (actions.backpackSlotButtons[i] && hit.gameObject.transform.IsChildOf(actions.backpackSlotButtons[i].transform))
                        return ItemDropTarget.TrickSlot(i);
        }
        if (!_isGear && actions && actions.playerHealthLabel && actions.playerHealthLabel.isActiveAndEnabled &&
            Contains((RectTransform)actions.playerHealthLabel.transform.parent, screen))
            return ItemDropTarget.PlayerHealth;
        var market = MarketPanel();
        if (market && Contains(market, screen)) return ItemDropTarget.BlackMarket;
        return ItemDropTarget.None;
    }

    // (owner, 2026-10-23) A tall strip down the right edge, between the MAP button and the draw
    // pile, clear of the stage and the header rows; in this layer's space.
    bool TryDiscardArea(out Rect area)
    {
        var bounds = _root.rect;
        area = Rect.MinMaxRect(bounds.xMin + bounds.width * .895f, bounds.yMin + bounds.height * .53f,
            bounds.xMin + bounds.width * .985f, bounds.yMin + bounds.height * .855f);
        return true;
    }

    void ShowDiscardStrip()
    {
        bool hovered = _targetValid && _target.Kind == ItemDropKind.Discard;
        bool shown = TryDiscardArea(out var area);
        _discardStrip.gameObject.SetActive(shown);
        if (!shown) return;
        _discardStrip.sizeDelta = area.size + new Vector2(24f, 8f);
        _discardStrip.anchoredPosition = area.center - _root.rect.center;
        var theme = DealbreakUIStyle.Theme;
        Color red = theme ? theme.brick : new Color(.66f, .27f, .22f);
        _discardImage.color = new Color(red.r, red.g, red.b, hovered ? 1f : .88f);
        _discardLabel.fontSize = hovered ? 40f : 32f;
    }

    static RectTransform MarketPanel()
    {
        var shop = Object.FindFirstObjectByType<ShopUI>();
        return shop && shop.panel && shop.panel.activeInHierarchy && RunManager.Instance != null && RunManager.Instance.CanSellItems
            ? (RectTransform)shop.panel.transform : null;
    }

    void ShowFeedback(string preview)
    {
        _ghostFrame.rim = _targetValid ? DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.ochre : Color.yellow
            : DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.slate : Color.gray;
        _ghostFrame.Refresh();
        ShowDiscardStrip();
        bool market = _targetValid && _target.Kind == ItemDropKind.BlackMarket;
        _marketShade.gameObject.SetActive(market);
        if (market)
        {
            Cover(_marketShade, MarketPanel());
            _marketLabel.text = preview;
        }
        bool discard = _targetValid && _target.Kind == ItemDropKind.Discard;
        var targetRect = !_targetValid || market || discard ? null : TargetRect(_target);
        _ring.gameObject.SetActive(targetRect);
        _tag.gameObject.SetActive(targetRect && !string.IsNullOrEmpty(preview));
        if (!targetRect) return;
        Cover(_ring, targetRect, 8f);
        if (string.IsNullOrEmpty(preview)) return;
        _tagLabel.text = preview;
        _tag.sizeDelta = new Vector2(Mathf.Max(110f, _tagLabel.GetPreferredValues(preview).x + 28f), 46f);
        _tag.anchoredPosition = _ring.anchoredPosition + new Vector2(0f, _ring.sizeDelta.y * .5f + 30f);
        _ring.SetAsLastSibling();
        _tag.SetAsLastSibling();
        _ghost.SetAsLastSibling();
    }

    void HideFeedback()
    {
        if (_ring) _ring.gameObject.SetActive(false);
        if (_tag) _tag.gameObject.SetActive(false);
        if (_marketShade) _marketShade.gameObject.SetActive(false);
        if (_discardStrip) _discardStrip.gameObject.SetActive(false);
    }

    RectTransform TargetRect(ItemDropTarget target)
    {
        var actions = Object.FindFirstObjectByType<ActionButtonsUI>();
        switch (target.Kind)
        {
            case ItemDropKind.HandCard:
                foreach (var view in HandViews())
                    if (view.data == target.Card) return view.visualRoot ? view.visualRoot : (RectTransform)view.transform;
                return null;
            case ItemDropKind.Enemy:
                foreach (var enemy in Object.FindObjectsByType<EnemyDisplayUI>(FindObjectsSortMode.None))
                    if (enemy.isActiveAndEnabled && enemy.DisplayedEnemy == target.Enemy) return (RectTransform)enemy.transform;
                return null;
            case ItemDropKind.PlayerHealth:
                return actions ? actions.PlayerHealthArea : null;
            case ItemDropKind.TrickSlot:
                return actions && target.Slot < actions.backpackSlotButtons.Length && actions.backpackSlotButtons[target.Slot]
                    ? (RectTransform)actions.backpackSlotButtons[target.Slot].transform : null;
            case ItemDropKind.GearSlot:
                foreach (var slot in Object.FindObjectsByType<GearIconSlotUI>(FindObjectsSortMode.None))
                    if (slot.isActiveAndEnabled && slot.InstanceId == target.GearId) return (RectTransform)slot.transform;
                return null;
            case ItemDropKind.GearPosition:
                foreach (var slot in Object.FindObjectsByType<GearIconSlotUI>(FindObjectsSortMode.None))
                    if (slot.isActiveAndEnabled && !slot.Gear && slot.transform.GetSiblingIndex() == target.Slot)
                        return (RectTransform)slot.transform;
                return null;
            default:
                return null;
        }
    }

    static IEnumerable<CardView> HandViews()
    {
        var holder = CardManager.Instance?.handField?.cardsHolder;
        if (!holder) yield break;
        foreach (var view in holder.GetComponentsInChildren<CardView>())
            if (view.data != null) yield return view;
    }

    void SetHandTargetState(bool targeting)
    {
        foreach (var view in HandViews())
            view.SetHandTargetState(targeting, targeting && _cardTargets.Contains(view.data));
        if (!targeting) _cardTargets.Clear();
    }

    bool IsCardTrick(int slot)
    {
        var definition = CardManager.Instance.GetConsumableAtSlot(slot);
        return definition != null && definition.effectType is ConsumableEffectType.ApplyEnhancement or
            ConsumableEffectType.DuplicateCard or ConsumableEffectType.DestroyCards or ConsumableEffectType.ChangeSuit;
    }

    IEnumerator ReturnToSource()
    {
        Vector2 start = _ghost.anchoredPosition;
        Vector2 end = _source ? CenterIn(_root, _source) : start;
        float elapsed = 0f;
        while (elapsed < ReturnTime && _ghost)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / ReturnTime);
            _ghost.anchoredPosition = Vector2.LerpUnclamped(start, end, 1f - (1f - t) * (1f - t));
            yield return null;
        }
        RestoreSource();
        if (_ghost) _ghost.gameObject.SetActive(false);
    }

    IEnumerator Vanish()
    {
        RestoreSource();
        float elapsed = 0f;
        while (elapsed < .14f && _ghost)
        {
            elapsed += Time.unscaledDeltaTime;
            _ghost.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, elapsed / .14f);
            yield return null;
        }
        if (!_ghost) yield break;
        _ghost.localScale = Vector3.one;
        _ghost.gameObject.SetActive(false);
    }

    void RestoreSource()
    {
        if (_sourceGroup)
        {
            _sourceGroup.alpha = 1f;
            _sourceGroup.blocksRaycasts = true;
        }
        _source = null;
        _sourceGroup = null;
    }

    void EnsureVisuals()
    {
        if (_ghost) return;
        var theme = DealbreakUIStyle.Theme;
        _marketShade = Child("Sell Shade", typeof(Image));
        var shade = _marketShade.GetComponent<Image>();
        shade.color = theme ? new Color(theme.ink.r, theme.ink.g, theme.ink.b, .88f) : new Color(0f, 0f, 0f, .88f);
        shade.raycastTarget = false;
        _marketLabel = Label(_marketShade, 64f, theme ? theme.mustard : Color.yellow);
        _marketShade.gameObject.SetActive(false);

        _discardStrip = Child("Discard Strip", typeof(Image));
        _discardImage = _discardStrip.GetComponent<Image>();
        _discardImage.raycastTarget = false;
        _discardLabel = Label(_discardStrip, 32f, theme ? theme.paper : Color.white);
        _discardLabel.text = "DISCARD";
        _discardStrip.gameObject.SetActive(false);

        _ring = Child("Drop Ring");
        foreach (var (min, max) in new[] { (new Vector2(0, 1), new Vector2(1, 1)), (new Vector2(0, 0), new Vector2(1, 0)),
                     (new Vector2(0, 0), new Vector2(0, 1)), (new Vector2(1, 0), new Vector2(1, 1)) })
        {
            var bar = new GameObject("Edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)bar.transform;
            rect.SetParent(_ring, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = new Vector2(min.x == max.x ? 6f : 0f, min.y == max.y ? 6f : 0f);
            var image = bar.GetComponent<Image>();
            image.color = theme ? theme.ochre : Color.yellow;
            image.raycastTarget = false;
        }
        _ring.gameObject.SetActive(false);

        _tag = Child("Drop Preview", typeof(Image));
        var tagImage = _tag.GetComponent<Image>();
        tagImage.color = theme ? theme.ochre : Color.yellow;
        tagImage.raycastTarget = false;
        _tagLabel = Label(_tag, 30f, theme ? theme.inkPrint : Color.black);
        _tag.gameObject.SetActive(false);

        _ghost = Child("Item Ghost", typeof(CanvasRenderer), typeof(EditorialFrame));
        _ghostFrame = _ghost.GetComponent<EditorialFrame>();
        _ghostFrame.raycastTarget = false;
        _ghostFrame.border = 2f;
        _ghostFrame.corner = 0f;
        _ghostFrame.showAccent = false;
        if (theme) { _ghostFrame.fill = theme.charcoal; _ghostFrame.ink = theme.inkPrint; }
        _ghostLabel = Label(_ghost, 20f, theme ? theme.paper : Color.white);
        _ghost.gameObject.SetActive(false);
    }

    RectTransform Child(string name, params System.Type[] components)
    {
        var types = new System.Type[components.Length + 1];
        types[0] = typeof(RectTransform);
        components.CopyTo(types, 1);
        var go = new GameObject(name, types);
        var rect = (RectTransform)go.transform;
        rect.SetParent(_root, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        return rect;
    }

    static TMP_Text Label(RectTransform parent, float size, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);
        var label = go.GetComponent<TMP_Text>();
        var theme = DealbreakUIStyle.Theme;
        if (theme) DealbreakUIStyle.Text(label, theme.headingFont, size, color);
        else { label.fontSize = size; label.color = color; }
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    void Cover(RectTransform cover, RectTransform target, float padding = 0f)
    {
        if (!target) return;
        var bounds = BoundsIn(_root, target);
        cover.sizeDelta = bounds.size + Vector2.one * padding * 2f;
        cover.anchoredPosition = bounds.center - _root.rect.center;
    }

    Vector2 ToLocal(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
        return local - _root.rect.center;
    }

    Vector2 CenterIn(RectTransform root, RectTransform child) => BoundsIn(root, child).center - root.rect.center;

    Vector2 SizeIn(RectTransform root, RectTransform child) => BoundsIn(root, child).size;

    Rect BoundsIn(RectTransform root, RectTransform child)
    {
        child.GetWorldCorners(_corners);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in _corners)
        {
            Vector2 point = root.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static bool Contains(RectTransform rect, Vector2 screen) =>
        rect && rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, screen, null);
}
