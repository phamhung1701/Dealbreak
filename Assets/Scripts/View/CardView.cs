using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class CardView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum State { Idle, Hover, Click, Drag, Release }

    [Header("References")]
    public Image face;
    public TMP_Text nameLabel;
    public TMP_Text enhancementLabel;
    public TMP_Text stateLabel;
    public CanvasGroup canvasGroup;
    public RectTransform visualRoot;
    public Outline selectionOutline;
    public GameObject statsPlate;
    public GameObject enhancementPlate;
    public GameObject selectionBadge;

    [Header("Card Artwork")]
    [Tooltip("Sprites indexed by CardData.Suit enum order, then Ace through King.")]
    public Sprite[] cardSprites = new Sprite[52];

    [Header("Presentation Motion")]
    // Legacy serialized values, no longer read: the Cut Sheet lifts are HandCardCutSheetUI.HoverLift
    // and HandCardCutSheetUI.SelectedLift (S10c), tweened over HandCardCutSheetUI.LiftSeconds.
    [Min(0f)] public float hoverRaise = 10f;
    [Min(0f)] public float selectedRaise = 18f;
    [Min(0f)] public float dragRaise = 6f;
    [Min(0.001f)] public float positionSmoothTime = 0.085f;
    [Min(0.001f)] public float layoutSmoothTime = 0.11f;
    [Min(0.001f)] public float scaleSmoothTime = 0.08f;

    [Header("State")]
    [NonSerialized] public CardInstance data;
    public Field homeField;
    public int index;

    State _state = State.Idle;
    bool _isSelected;
    bool _compactPresentation;
    bool _isPointerOver;
    bool _isFocused;
    bool _isDragging;
    bool _isHandTargeting;
    bool _isHandTargetEligible;
    int _selectionOrder;
    Vector3 _originalPosition;
    Transform _originalParent;
    Field _dragHomeField;

    Vector2 _layoutOffset;
    Vector2 _layoutVelocity;
    float _lift, _liftFrom, _liftTo, _liftElapsed = -1f;
    bool _defending, _canBlock = true;
    HandCardCutSheetUI _cutSheet;
    static readonly CardInstance[] BlockProbe = new CardInstance[1];
    float _visualScale = 1f;
    float _scaleVelocity;
    float _targetRaise;
    float _targetScale = 1f;
    Vector2 _fanOffset;
    Vector2 _presentFanOffset;
    Vector2 _fanVelocity;
    float _fanRotation;
    float _visualRotation;
    float _rotationVelocity;
    Canvas _visualCanvas;
    Vector3 _layoutTargetPosition;
    Vector3 _rootVelocity;
    float _layoutTargetScale = 1f;
    float _rootScaleVelocity;
    bool _hasLayoutTarget;
    float _entryDelay;
    bool _hasGrabPoint;
    bool _suppressClick;
    Vector2 _grabLocalPoint;
    Vector2 _dragPointer;
    RectTransform _dragRect;
    Camera _dragCamera;

    public Vector3 LayoutTargetPosition => _layoutTargetPosition;
    public float LayoutTargetScale => _layoutTargetScale;
    public bool IsDragging => _isDragging;
    public bool IsHovered => _isPointerOver;
    public void SetInspectFocus(bool focused) { _isFocused = focused; RefreshState(); }

    public State CurrentState => _state;
    public bool IsSelected => _isSelected;
    public bool IsHandTargetEligible => _isHandTargeting && _isHandTargetEligible;
    /// <summary>The artwork's current Cut Sheet lift in pixels (tweened; snaps under Reduce Motion).</summary>
    public float CurrentLift => _lift;
    public HandCardCutSheetUI CutSheet => _cutSheet;
    /// <summary>In defense, an unselected card that cannot block on its own: darkened, hatched, no hover lift.</summary>
    public bool CannotBlock => _defending && !_canBlock && !_isSelected;

    /// <summary>Presentation input, like <see cref="SetHandTargetState"/>; changes no game state.</summary>
    public void SetDefenseBlockState(bool defending, bool canBlock)
    {
        _defending = defending;
        _canBlock = !defending || canBlock;
        RefreshState();
    }

    /// <summary>
    /// Reads, without changing anything, whether this hand card could block the pending attack alone
    /// (CombatManager.PreviewBlockedAttackers). Runs on combat events, never per frame.
    /// </summary>
    public void RefreshDefenseBlockState()
    {
        bool defending = false, canBlock = true;
        if (data != null && homeField != null && CombatManager.TryGetInstance(out var combat) &&
            CardManager.TryGetInstance(out var cards) && cards.handField == homeField &&
            combat.currentState == GameState.EnemyAttacking && combat.pendingDamage > 0)
        {
            defending = true;
            BlockProbe[0] = data;
            canBlock = combat.PreviewBlockedAttackers(BlockProbe) != null;
            BlockProbe[0] = null;
        }
        if (defending != _defending || canBlock != _canBlock) SetDefenseBlockState(defending, canBlock);
    }

    void HandleCombatStateChanged(GameState _) => RefreshDefenseBlockState();
    void HandlePendingDamageChanged(int _) => RefreshDefenseBlockState();

    public void SetHandTargetState(bool targeting, bool eligible)
    {
        _isHandTargeting = targeting;
        _isHandTargetEligible = targeting && eligible;
        if (targeting && !_isHandTargetEligible) _isPointerOver = false;
        RefreshState();
    }

    static readonly Color _selectedColor = new(1f, 0.72f, 0.2f, 1f);
    static readonly Color _dragColor = new(0.35f, 0.72f, 1f, 1f);

    void OnEnable()
    {
        if (Application.isPlaying && CardManager.Instance != null)
            CardManager.Instance.OnBuildChanged += RefreshContent;
        if (Application.isPlaying && CombatManager.TryGetInstance(out var combat))
        {
            combat.OnStateChanged += HandleCombatStateChanged;
            combat.OnPendingDamageChanged += HandlePendingDamageChanged;
            RefreshDefenseBlockState();
        }
    }

    void OnDisable()
    {
        CancelActiveDrag();
        _isPointerOver = false;
        _isFocused = false;
        _isHandTargeting = false;
        _isHandTargetEligible = false;
        RefreshState();
        if (CardManager.TryGetInstance(out var manager))
            manager.OnBuildChanged -= RefreshContent;
        if (CombatManager.TryGetInstance(out var combat))
        {
            combat.OnStateChanged -= HandleCombatStateChanged;
            combat.OnPendingDamageChanged -= HandlePendingDamageChanged;
        }
    }

    public void Setup(CardInstance cardData)
    {
        data = cardData;
        if (Application.isPlaying)
        {
            CardGlitchUI.Attach(this);
            CardModFxUI.Attach(this);
        }
        RefreshContent();
        if (Application.isPlaying) RefreshDefenseBlockState();
        RefreshState();
    }

    public void RefreshContent()
    {
        Sprite sprite = ResolveCardSprite(data);
        if (face)
        {
            face.sprite = sprite;
            face.preserveAspect = true;
            face.color = Color.white;
        }

        bool hasData = data != null;
        bool hasEnhancement = hasData && data.Enhancement != null;
        bool compactCombat = homeField != null && (CombatDetailPanelUI.IsCombatActive || ContextualTooltipUI.Instance) && sprite != null;
        _compactPresentation = compactCombat;
        if (statsPlate) statsPlate.SetActive(hasData && !compactCombat);
        if (nameLabel) nameLabel.enabled = !compactCombat;
        if (enhancementPlate) enhancementPlate.SetActive(hasEnhancement);

        if (nameLabel)
        {
            if (!hasData)
            {
                nameLabel.text = string.Empty;
            }
            else
            {
                var combat = CombatManager.Instance;
                // A card's face shows what playing it alone would request, including primed Gear.
                int attack = combat != null ? combat.PreviewActionDamage(new[] { data }) : data.AttackValue;
                int defense = combat != null ? combat.CalculateCardDefense(data) : data.DefenseValue;
                string identityFallback = sprite == null
                    ? $"{data.Definition.RankLabel}{data.SuitSymbol}  "
                    : string.Empty;
                nameLabel.text = $"{identityFallback}<b>ATK {attack}</b>   DEF {defense}";
            }
        }

        if (enhancementLabel)
            enhancementLabel.text = hasEnhancement
                ? $"{data.Enhancement.icon}  {ModSignature.Name(data.Enhancement)}"
                : string.Empty;
        RefreshState();
    }

    public Sprite ResolveCardSprite(CardInstance card)
    {
        if (card == null || cardSprites == null) return null;
        int index = (int)card.Suit * 13 + (int)card.Rank;
        return index >= 0 && index < cardSprites.Length ? cardSprites[index] : null;
    }

    public Sprite ResolveCardSprite(CardData card)
    {
        if (card == null || cardSprites == null) return null;
        int index = (int)card.suit * 13 + (int)card.rank;
        return index >= 0 && index < cardSprites.Length ? cardSprites[index] : null;
    }

    public void SetSelected(bool selected) => SetSelectionOrder(selected ? 1 : 0);

    public void SetSelectionOrder(int selectionOrder)
    {
        _selectionOrder = Mathf.Max(0, selectionOrder);
        _isSelected = _selectionOrder > 0;
        RefreshState();
    }

    void RefreshState()
    {
        State next = _isDragging
            ? State.Drag
            : _isSelected
                ? State.Click
                : _isPointerOver || _isFocused
                    ? State.Hover
                    : State.Idle;

        ChangeState(next);
    }

    void ChangeState(State next)
    {
        if (_state == next)
        {
            ApplyState(next);
            return;
        }

        _state = next;
        ApplyState(next);
    }

    void ApplyState(State state)
    {
        EnsureVisualSorting();
        if (_visualCanvas != null)
            _visualCanvas.sortingOrder = state switch
            {
                State.Drag => 21,
                State.Click => _isPointerOver || _isFocused ? 4 : 3,
                State.Hover => 2,
                _ => 1
            };
        EnsureCutSheet();
        // A card that cannot block never answers hover; the selection lift reads without its shadow.
        bool hoverLift = (_isPointerOver || _isFocused) && !CannotBlock;
        _targetRaise = (_isDragging ? 0f : (_isSelected ? HandCardCutSheetUI.SelectedLift : 0f) + (hoverLift ? HandCardCutSheetUI.HoverLift : 0f)) +
            (_isHandTargetEligible ? 8f : 0f);
        RetargetLift(_targetRaise);
        _targetScale = _isDragging ? 1.065f : 1f + (_isSelected ? 0.045f : 0f) + (hoverLift ? 0.035f : 0f);

        if (face)
        {
            if (_isHandTargeting)
                face.color = _isHandTargetEligible
                    ? (ContextualTooltipUI.Instance ? new Color(0.94f, 0.89f, 0.77f, 1f) : new Color(0.88f, 1f, 0.9f, 1f))
                    : new Color(0.48f, 0.48f, 0.48f, 0.58f);
            else if (CannotBlock)
                face.color = HandCardCutSheetUI.DarkenedFace;
            else
                face.color = state == State.Idle ? new Color(0.97f, 0.97f, 0.97f, 1f) : Color.white;
        }
        if (selectionOutline)
        {
            bool selected = state == State.Click || state == State.Drag;
            // With Cut Sheet crop marks, the selection is the marks and the lift, not the outline.
            bool marked = _cutSheet != null && state == State.Click;
            selectionOutline.enabled = selected && !marked || IsHandTargetEligible || _isFocused;
            selectionOutline.effectColor = CombatDetailPanelUI.IsCombatActive || ContextualTooltipUI.Instance
                ? selected && state != State.Drag ? new Color(0.87f, 0.67f, 0.28f) : new Color(0.94f, 0.89f, 0.77f)
                : state == State.Drag ? _dragColor : selected ? _selectedColor : new Color(0.28f, 1f, 0.52f);
        }
        if (selectionBadge) selectionBadge.SetActive(false);
        if (stateLabel) stateLabel.text = string.Empty;
        ApplyCutSheet();
    }

    // Hand cards only (a home Field and a visual root under the card root); built once.
    void EnsureCutSheet()
    {
        if (_cutSheet != null || homeField == null || visualRoot == null) return;
        _cutSheet = HandCardCutSheetUI.Attach(this);
    }

    void ApplyCutSheet()
    {
        if (_cutSheet != null)
            _cutSheet.Apply(face, _lift, _isSelected && !_isDragging, CannotBlock && !_isHandTargeting);
    }

    void RetargetLift(float target)
    {
        if (Mathf.Approximately(target, _liftTo)) return;
        _liftFrom = _lift;
        _liftTo = target;
        if (CutSheetMotion.Seconds(HandCardCutSheetUI.LiftSeconds) > 0f) _liftElapsed = 0f;
        else { _lift = target; _liftElapsed = -1f; }
    }

    // Ease-out over ~90ms on the presentation clock; Reduce Motion snaps, also mid-tween.
    void TickLift(float deltaTime)
    {
        if (_liftElapsed < 0f) return;
        float duration = CutSheetMotion.Seconds(HandCardCutSheetUI.LiftSeconds);
        _liftElapsed += deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(_liftElapsed / duration) : 1f;
        _lift = Mathf.LerpUnclamped(_liftFrom, _liftTo, 1f - (1f - t) * (1f - t));
        if (t >= 1f) { _lift = _liftTo; _liftElapsed = -1f; }
    }

    void EnsureVisualSorting()
    {
        if (visualRoot == null || _visualCanvas != null) return;
        _visualCanvas = visualRoot.GetComponent<Canvas>();
        if (_visualCanvas == null) _visualCanvas = visualRoot.gameObject.AddComponent<Canvas>();
        _visualCanvas.overrideSorting = true;
        // Keep the base footprint for stable edge hover; raised/rotated artwork must also be clickable.
        var artworkHit = face != null && face.transform.IsChildOf(visualRoot)
            ? (Graphic)face : visualRoot.GetComponent<Graphic>();
        if (artworkHit != null) artworkHit.raycastTarget = true;
        // A nested sorting canvas needs its own raycaster for card art and text to stay clickable.
        if (!visualRoot.TryGetComponent<GraphicRaycaster>(out _))
            visualRoot.gameObject.AddComponent<GraphicRaycaster>();
    }

    void LateUpdate() => TickPresentation(Time.unscaledDeltaTime);

    // One owner follows the base root target and layers artwork state on top.
    public void TickPresentation(float deltaTime)
    {
        if (_compactPresentation != (homeField != null && (CombatDetailPanelUI.IsCombatActive || ContextualTooltipUI.Instance) && ResolveCardSprite(data) != null))
            RefreshContent();
        if (deltaTime <= 0f) return;
        // A dealt card waits at the draw-pile side for its turn in the deal.
        if (_entryDelay > 0f) _entryDelay = Mathf.Max(0f, _entryDelay - deltaTime);
        if (_hasLayoutTarget && _entryDelay <= 0f && !_isDragging && homeField != null &&
            transform.parent == homeField.cardsHolder)
        {
            transform.localPosition = Vector3.SmoothDamp(transform.localPosition,
                _layoutTargetPosition, ref _rootVelocity, layoutSmoothTime, Mathf.Infinity, deltaTime);
            float scale = Mathf.SmoothDamp(transform.localScale.x, _layoutTargetScale,
                ref _rootScaleVelocity, layoutSmoothTime, Mathf.Infinity, deltaTime);
            transform.localScale = Vector3.one * scale;
        }
        if (visualRoot == null)
        {
            if (_isDragging) AlignGrabPoint();
            return;
        }
        _layoutOffset = Vector2.SmoothDamp(
            _layoutOffset, Vector2.zero, ref _layoutVelocity,
            layoutSmoothTime, Mathf.Infinity, deltaTime);
        TickLift(deltaTime);
        _visualScale = Mathf.SmoothDamp(
            _visualScale, _targetScale, ref _scaleVelocity,
            scaleSmoothTime, Mathf.Infinity, deltaTime);
        _visualRotation = Mathf.SmoothDampAngle(_visualRotation,
            _isDragging ? 0f : _fanRotation * (_isPointerOver && !CannotBlock ? 0.35f : _isSelected ? 0.65f : 1f), ref _rotationVelocity,
            layoutSmoothTime, Mathf.Infinity, deltaTime);
        _presentFanOffset = Vector2.SmoothDamp(_presentFanOffset,
            _isDragging ? Vector2.zero : _fanOffset, ref _fanVelocity,
            layoutSmoothTime, Mathf.Infinity, deltaTime);
        // A Mod trigger hops the artwork up and back with a small wiggle, on top of its other state.
        Vector2 pulseOffset = Vector2.zero;
        float pulseScale = 1f, pulseAngle = 0f;
        if (_triggerPulse >= 0f)
        {
            _triggerPulse += deltaTime;
            float t = Mathf.Clamp01(_triggerPulse / TriggerPulseTime);
            float hop = Mathf.Sin(t * Mathf.PI);
            pulseOffset = new Vector2(0f, hop * 34f);
            pulseScale = 1f + hop * .14f;
            pulseAngle = Mathf.Sin(t * Mathf.PI * 3f) * 7f * (1f - t);
            if (t >= 1f) _triggerPulse = -1f;
        }
        visualRoot.anchoredPosition = _layoutOffset + new Vector2(0f, _lift) + _presentFanOffset + pulseOffset;
        visualRoot.localRotation = Quaternion.Euler(0f, 0f, _visualRotation + pulseAngle);
        visualRoot.localScale = Vector3.one * (_visualScale * pulseScale);
        EnsureCutSheet();
        ApplyCutSheet();
        // Keep the grabbed artwork point locked even while rotation/lift/scale settle.
        if (_isDragging) AlignGrabPoint();
    }

    public const float TriggerPulseTime = .42f;
    float _triggerPulse = -1f;

    /// <summary>Presentation only: the card hops once, as one of its Mods triggers.</summary>
    public void PlayTriggerPulse()
    {
        _triggerPulse = 0f;
        if (TryGetComponent(out CardModFxUI modFx)) modFx.Burst();
    }

    public void SetLayoutTarget(Vector3 position, Vector2 fanOffset, float rotation, float scale)
    {
        _layoutTargetPosition = position;
        _layoutTargetScale = scale;
        _hasLayoutTarget = true;
        SetFanPose(fanOffset, rotation);
    }

    public void BeginHandEntry(Vector3 localPosition, float delay = 0f)
    {
        _entryDelay = Mathf.Max(0f, delay);
        transform.localPosition = localPosition;
        transform.localScale = Vector3.one * _layoutTargetScale * 0.94f;
        _rootVelocity = Vector3.zero;
        _rootScaleVelocity = 0f;
    }

    public void SetFanPose(Vector2 offset, float rotation)
    {
        _fanOffset = offset;
        _fanRotation = rotation;
    }

    public void ApplyLayoutWorldOffset(Vector3 worldOffset)
    {
        Vector3 localOffset = transform.InverseTransformVector(worldOffset);
        _layoutOffset += new Vector2(localOffset.x, localOffset.y);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isDragging || GameplayInputGate.IsBlocked ||
            (_isHandTargeting && !_isHandTargetEligible) ||
            (CardManager.TryGetInstance(out var manager) && manager.ActiveDragCard != null)) return;
        _isPointerOver = true;
        RefreshState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
        RefreshState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        _suppressClick = false;
        CaptureGrabPoint(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isDragging || _suppressClick) return;
        var manager = CardManager.Instance;
        if (manager == null || GameplayInputGate.IsBlocked && !manager.CanRouteHandEnhancementTargetClick ||
            !DealbreakUIRuntime.CombatActive && !manager.CanRouteHandEnhancementTargetClick ||
            eventData.button != PointerEventData.InputButton.Left) return;
        manager.HandleCardClick(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Selection is persistent and is owned by CardManager. A normal release does not deselect.
        if (!_isDragging) _hasGrabPoint = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || _isDragging) return;
        var manager = CardManager.Instance;
        if (GameplayInputGate.IsBlocked && (manager == null || !manager.CanRouteHandEnhancementTargetClick) ||
            manager == null || !DealbreakUIRuntime.CombatActive && !manager.CanRouteHandEnhancementTargetClick ||
            manager.dragCanvas == null || homeField == null)
            return;

        if (!_hasGrabPoint) CaptureGrabPoint(eventData);
        if (!manager.PrepareDragSelection(this))
            return;
        _suppressClick = true;
        eventData.eligibleForClick = false;
        _originalParent = transform.parent;
        _originalPosition = transform.position;
        _dragHomeField = homeField;
        _dragHomeField.BeginVisualDrag(this);
        _isDragging = true;
        _entryDelay = 0f;
        manager.BeginCardDrag(this);

        transform.SetParent(manager.dragCanvas.transform, true);
        transform.SetAsLastSibling();
        _dragHomeField.RefreshLayout();
        _rootVelocity = Vector3.zero;
        _dragRect = manager.dragCanvas.transform as RectTransform;
        _dragCamera = manager.dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : manager.dragCanvas.worldCamera;
        _isPointerOver = false;
        MoveToPointer(eventData);

        if (canvasGroup) canvasGroup.blocksRaycasts = false;
        RefreshState();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        MoveToPointer(eventData);
        _dragHomeField?.UpdateVisualDrag(this, eventData.position, eventData.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        var target = FindDropTarget(eventData);
        bool committed = target != null && target.TryCommit(this);

        if (committed)
        {
            _dragHomeField?.RemoveVisualDragSlot(this);
        }
        else if (_dragHomeField != null)
        {
            bool commitReorder = _dragHomeField.ContainsScreenPoint(eventData.position, eventData.pressEventCamera);
            _dragHomeField.EndVisualDrag(this, commitReorder);
        }
        else
        {
            RestoreOriginalTransform();
        }

        FinishDrag();
    }

    public void CancelActiveDrag()
    {
        if (!_isDragging) return;
        if (_dragHomeField != null)
        {
            _dragHomeField.EndVisualDrag(this, false);
        }
        else
        {
            RestoreOriginalTransform();
        }
        FinishDrag();
    }

    public void ForceToIdle()
    {
        CancelActiveDrag();
        _selectionOrder = 0;
        _isSelected = false;
        _isPointerOver = false;
        _hasGrabPoint = false;
        // Targets reset immediately; the current pose settles rather than snapping.
        RefreshState();
    }

    void CaptureGrabPoint(PointerEventData eventData)
    {
        var grabbedRect = visualRoot != null ? visualRoot : transform as RectTransform;
        _hasGrabPoint = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            grabbedRect, eventData.position, eventData.pressEventCamera, out _grabLocalPoint);
    }

    void MoveToPointer(PointerEventData eventData)
    {
        _dragPointer = eventData.position;
        AlignGrabPoint();
    }

    void AlignGrabPoint()
    {
        if (_dragRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _dragRect, _dragPointer, _dragCamera, out var pointerLocal)) return;
        var grabbedRect = visualRoot != null ? visualRoot : transform as RectTransform;
        Vector3 grip = _dragRect.InverseTransformPoint(grabbedRect.TransformPoint(_grabLocalPoint));
        transform.localPosition += new Vector3(pointerLocal.x - grip.x, pointerLocal.y - grip.y, 0f);
    }

    CardActionDropTarget FindDropTarget(PointerEventData eventData)
    {
        if (EventSystem.current == null) return null;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        foreach (var result in results)
        {
            var behaviours = result.gameObject.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is CardActionDropTarget target)
                    return target;
            }
        }
        return null;
    }

    void RestoreOriginalTransform()
    {
        var parent = _originalParent;
        if (parent == null && homeField != null)
            parent = homeField.cardsHolder;
        if (parent == null) return;

        transform.SetParent(parent, true);
        transform.SetSiblingIndex(Mathf.Clamp(index, 0, parent.childCount - 1));
        transform.position = _originalPosition;
    }

    void FinishDrag()
    {
        _isDragging = false;
        _isPointerOver = false;
        _hasGrabPoint = false;
        _dragRect = null;
        _dragCamera = null;
        _rootVelocity = Vector3.zero;
        if (canvasGroup) canvasGroup.blocksRaycasts = true;
        CardManager.Instance?.EndCardDrag(this);
        _originalParent = null;
        _dragHomeField = null;
        RefreshState();
    }
}
