using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the CardManager Gear build as a row of icons, with on-demand text.</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "ArtifactRailUI")]
public sealed class GearRailUI : MonoBehaviour
{
    [Header("Source and icon rail")]
    public CardManager source;
    public RectTransform slotsRoot;
    public TMP_Text capacityLabel;
    public float slotSize = 44f;

    [Header("Hover tooltip")]
    public GameObject tooltipPanel;
    public TMP_Text tooltipText;

    [Header("Visual Style Test")]
    public Sprite occupiedSlotSprite;
    public Sprite emptySlotSprite;
    public Sprite selectedSlotSprite;

    [Header("Manual merge")]
    public Button mergeModeButton;
    public Button mergeConfirmButton;
    public Button mergeCancelButton;
    public TMP_Text mergeStatusText;

    public GameObject detailPanel;
    public TMP_Text detailText;
    public Button detailCloseButton;

    readonly List<GearIconSlotUI> _slots = new();
    CardManager _subscribedSource;
    GearData _hovered;
    long _selectedInstanceId;
    bool _mergeSelecting;
    bool _mergeAwaitingConfirmation;
    long _firstMergeId;
    long _secondMergeId;
    int _ownedGearCount;
    GearData _mergePreview;
    PathNode _mergeNode;
    GameState _mergeCombatState;
    Vector2 _lastDetailParentSize;
    RectTransform _generatedControlsViewport;
    bool _hasLastDetailParentSize;
    RectTransform _sellPanel;
    TMP_Text _sellText;
    Button _sellButton;
    readonly Vector3[] _sellSlotCorners = new Vector3[4];

    /// <summary>The Sell button shown beside the selected Gear while a Black Market is open.</summary>
    public Button SellButton => _sellButton;

    void OnEnable()
    {
        EnsureMergeControls();
        if (detailCloseButton) detailCloseButton.onClick.AddListener(CloseDetail);
        if (mergeModeButton) mergeModeButton.onClick.AddListener(BeginMergeSelection);
        if (mergeConfirmButton) mergeConfirmButton.onClick.AddListener(ConfirmMerge);
        if (mergeCancelButton) mergeCancelButton.onClick.AddListener(CancelMerge);
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += HandleRunStarted;
            RunManager.Instance.OnHideShop += HandleContextEnded;
            RunManager.Instance.OnShowPathScreen += HandleContextEnded;
            RunManager.Instance.OnHidePathScreen += HandleContextEnded;
        }
        if (CombatManager.Instance != null) CombatManager.Instance.OnEncounterResult += HandleEncounterResult;
        HideTooltip();
        CloseDetail();
        Refresh();
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= HandleRunStarted;
            RunManager.Instance.OnHideShop -= HandleContextEnded;
            RunManager.Instance.OnShowPathScreen -= HandleContextEnded;
            RunManager.Instance.OnHidePathScreen -= HandleContextEnded;
        }
        if (CombatManager.Instance != null) CombatManager.Instance.OnEncounterResult -= HandleEncounterResult;
        if (_subscribedSource)
        {
            _subscribedSource.OnBuildChanged -= Refresh;
            _subscribedSource.OnEnemyTargetingChanged -= HandleEnemyTargetingChanged;
            _subscribedSource.OnCardSelected -= HandleCardSelected;
        }
        _subscribedSource = null;
        if (detailCloseButton) detailCloseButton.onClick.RemoveListener(CloseDetail);
        if (mergeModeButton) mergeModeButton.onClick.RemoveListener(BeginMergeSelection);
        if (mergeConfirmButton) mergeConfirmButton.onClick.RemoveListener(ConfirmMerge);
        if (mergeCancelButton) mergeCancelButton.onClick.RemoveListener(CancelMerge);
        HideTooltip();
        CloseDetail();
        _mergeSelecting = false;
        _mergeAwaitingConfirmation = false;
        _firstMergeId = _secondMergeId = 0;
        _mergePreview = null;
        UpdateMergeControls();
    }

    // A rail enabled before CardManager is created can still attach as soon as it appears.
    void Update()
    {
        if (!_subscribedSource && (source || CardManager.Instance)) Refresh();
        if (_mergeSelecting && (GameplayInputGate.IsBlocked ||
            _subscribedSource != null && (_subscribedSource.IsHandEnhancementTargeting || _subscribedSource.IsEnemyTargeting) ||
            RunManager.Instance?.ActiveNode != _mergeNode ||
            CombatManager.Instance != null && CombatManager.Instance.currentState != _mergeCombatState)) CancelMerge();
        if (_selectedInstanceId != 0 && detailPanel && detailPanel.activeSelf &&
            detailPanel.transform.parent is RectTransform detailParent &&
            (!_hasLastDetailParentSize || (detailParent.rect.size - _lastDetailParentSize).sqrMagnitude > 0.25f))
            PositionDetailBesideSlot(_selectedInstanceId);
        // Leaving the Black Market or losing the Gear closes the sale panel without a rail refresh.
        if (_sellPanel && _sellPanel.gameObject.activeSelf) RefreshSellContext();
    }

    public void Refresh()
    {
        var cards = source ? source : CardManager.Instance;
        if (_subscribedSource != cards)
        {
            if (_subscribedSource)
            {
                _subscribedSource.OnBuildChanged -= Refresh;
                _subscribedSource.OnEnemyTargetingChanged -= HandleEnemyTargetingChanged;
                _subscribedSource.OnCardSelected -= HandleCardSelected;
            }
            _subscribedSource = cards;
            if (_subscribedSource)
            {
                _subscribedSource.OnBuildChanged += Refresh;
                _subscribedSource.OnEnemyTargetingChanged += HandleEnemyTargetingChanged;
                _subscribedSource.OnCardSelected += HandleCardSelected;
            }
        }

        if (_mergeSelecting && cards != null && (cards.IsEnemyTargeting || cards.IsHandEnhancementTargeting)) CancelMerge();
        if (_mergeSelecting && (!cards || cards.ownedGear.Count < 2 ||
            (_firstMergeId != 0 && cards.GetGearInstanceById(_firstMergeId) == null) ||
            (_secondMergeId != 0 && cards.GetGearInstanceById(_secondMergeId) == null)))
        {
            _mergeSelecting = false;
            _firstMergeId = _secondMergeId = 0;
            _mergeAwaitingConfirmation = false;
            _mergePreview = null;
        }

        if (capacityLabel)
            capacityLabel.text = $"{(cards ? cards.GearSlotsUsed : 0)}/{(cards ? cards.GearCapacity : CardManager.BASE_GEAR_CAPACITY)}";

        int count = 0;
        int gearIndex = 0;
        if (cards && cards.ownedGear != null)
        {
            foreach (var gear in cards.ownedGear)
            {
                int currentGearIndex = gearIndex++;
                if (!gear) continue;
                if (slotsRoot)
                {
                    if (count == _slots.Count) _slots.Add(CreateSlot());
                    var slot = _slots[count];
                    slot.SetSlotSprites(occupiedSlotSprite, emptySlotSprite, selectedSlotSprite);
                    var instanceId = cards.GetGearInstanceAt(currentGearIndex)?.Id ?? 0;
                    slot.Bind(gear, ShowTooltip, HideTooltipFor,
                        selectedGear => SelectGear(instanceId, selectedGear),
                        instanceId, SelectForMerge, _firstMergeId == instanceId || _secondMergeId == instanceId,
                        _mergeSelecting, IsMergeEligible(cards, instanceId), _selectedInstanceId == instanceId);
                    slot.gameObject.SetActive(true);
                }
                count++;
            }
        }

        int emptySlotCount = cards ? cards.GearCapacity : CardManager.BASE_GEAR_CAPACITY;
        int visibleSlotCount = Mathf.Max(count, emptySlotCount);
        _ownedGearCount = count;
        for (int i = count; i < visibleSlotCount; i++)
        {
            if (i == _slots.Count) _slots.Add(CreateSlot());
            var slot = _slots[i];
            slot.SetSlotSprites(occupiedSlotSprite, emptySlotSprite, selectedSlotSprite);
            slot.BindEmpty();
            slot.gameObject.SetActive(true);
        }
        for (int i = visibleSlotCount; i < _slots.Count; i++)
            _slots[i].gameObject.SetActive(false);

        if (_hovered && (!cards || !cards.ownedGear.Contains(_hovered))) HideTooltip();
        if (_selectedInstanceId != 0 && (!cards || cards.GetGearInstanceById(_selectedInstanceId) == null)) CloseDetail();
        UpdateMergeControls();
        RefreshSellContext();
    }

    // Gear sells like a Trick: select it in a Black Market, then Sell and confirm.
    void RefreshSellContext()
    {
        var run = RunManager.Instance;
        var cards = CardManager.Instance;
        var gear = _selectedInstanceId != 0 && cards ? cards.GetGearInstanceById(_selectedInstanceId) : null;
        bool show = gear != null && gear.Definition != null && run != null && run.CanSellItems && !_mergeSelecting;
        if (!show)
        {
            if (_sellPanel) _sellPanel.gameObject.SetActive(false);
            return;
        }
        EnsureSellPanel();
        if (!_sellPanel) return;
        bool sellable = gear.Definition.price >= 0;
        _sellText.text = sellable
            ? $"<b>{gear.Definition.displayName}</b>\nSells for {gear.Definition.price / 2} Credits"
            : $"<b>{gear.Definition.displayName}</b>\nCannot be sold";
        _sellButton.interactable = sellable && !GameplayInputGate.IsBlocked;
        _sellPanel.gameObject.SetActive(true);
        PositionSellPanel();
    }

    void EnsureSellPanel()
    {
        if (_sellPanel || !slotsRoot) return;
        var parent = slotsRoot.GetComponentInParent<Canvas>()?.transform as RectTransform;
        if (!parent) return;
        var panel = new GameObject("Selected Gear Sale", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Canvas), typeof(GraphicRaycaster));
        _sellPanel = (RectTransform)panel.transform;
        _sellPanel.SetParent(parent, false);
        var canvas = panel.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 65;
        _sellPanel.anchorMin = _sellPanel.anchorMax = _sellPanel.pivot = new Vector2(0.5f, 0.5f);
        _sellPanel.sizeDelta = new Vector2(250f, 118f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.16f, 0.96f);

        var textObject = new GameObject("Gear Sale Details", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var textRect = (RectTransform)textObject.transform;
        textRect.SetParent(_sellPanel, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 58f);
        textRect.offsetMax = new Vector2(-12f, -10f);
        _sellText = textObject.GetComponent<TMP_Text>();
        CopyTextStyle(_sellText);
        _sellText.fontSize = 18f;
        _sellText.alignment = TextAlignmentOptions.TopLeft;
        _sellText.raycastTarget = false;

        var buttonObject = new GameObject("Sell Gear", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.SetParent(_sellPanel, false);
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 10f);
        buttonRect.sizeDelta = new Vector2(110f, 44f);
        var buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.22f, 0.43f, 0.34f, 1f);
        _sellButton = buttonObject.GetComponent<Button>();
        _sellButton.targetGraphic = buttonImage;
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(buttonRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var label = labelObject.GetComponent<TMP_Text>();
        CopyTextStyle(label);
        label.text = "SELL";
        label.fontSize = 16f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        _sellButton.onClick.AddListener(SellSelectedGear);
        DealbreakUIStyle.Frame(_sellPanel);
        DealbreakUIStyle.Button(_sellButton);
        panel.SetActive(false);
    }

    void PositionSellPanel()
    {
        var parent = _sellPanel ? _sellPanel.parent as RectTransform : null;
        GearIconSlotUI selected = null;
        foreach (var slot in _slots)
            if (slot && slot.InstanceId == _selectedInstanceId) { selected = slot; break; }
        if (!parent || !selected) return;
        var slotRect = (RectTransform)selected.transform;
        var canvas = parent.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        slotRect.GetWorldCorners(_sellSlotCorners);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, _sellSlotCorners[3]);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, camera, out var local)) return;
        float halfWidth = _sellPanel.rect.width * 0.5f;
        float halfHeight = _sellPanel.rect.height * 0.5f;
        local.x = Mathf.Clamp(local.x + halfWidth + 12f, parent.rect.xMin + halfWidth, parent.rect.xMax - halfWidth);
        local.y = Mathf.Clamp(local.y + slotRect.rect.height * 0.5f, parent.rect.yMin + halfHeight, parent.rect.yMax - halfHeight);
        _sellPanel.anchoredPosition = local;
    }

    void SellSelectedGear()
    {
        var run = RunManager.Instance;
        var gear = CardManager.Instance ? CardManager.Instance.GetGearInstanceById(_selectedInstanceId) : null;
        if (run == null || gear?.Definition == null || !run.CanSellItems || gear.Definition.price < 0 ||
            GameplayInputGate.IsBlocked) return;
        long instanceId = gear.Id;
        int authoredPrice = gear.Definition.price;
        void Sell()
        {
            // The sale revalidates identity and price in RunManager, as Trick sales do.
            if (RunManager.Instance != null && RunManager.Instance.SellGear(instanceId, authoredPrice))
            {
                CloseDetail();
                Refresh();
            }
        }
        var dialog = FindFirstObjectByType<ConfirmationDialogUI>();
        var coordinator = FindFirstObjectByType<GameplayMenuCoordinator>();
        if (dialog && coordinator)
            dialog.Open(coordinator, "Sell Gear?", $"Sell {gear.Definition.displayName} for {authoredPrice / 2} Credits?",
                "SELL", Sell);
        else Sell();
    }

    void EnsureMergeControls()
    {
        if (mergeModeButton && mergeConfirmButton && mergeCancelButton && mergeStatusText) return;
        var controlsRoot = slotsRoot && slotsRoot.parent ? slotsRoot.parent.parent as RectTransform : null;
        if (!controlsRoot) controlsRoot = transform as RectTransform;
        if (!controlsRoot) return;

        if (!mergeModeButton)
            mergeModeButton = CreateMergeButton(controlsRoot, "Merge Gear", "Merge", new Vector2(0f, -72f), new Vector2(176f, 40f), 20f, 16f);
        if (!mergeConfirmButton)
            mergeConfirmButton = CreateMergeButton(controlsRoot, "Confirm Gear Merge", "Confirm", new Vector2(-48f, -120f), new Vector2(108f, 40f), 20f, 16f);
        if (!mergeCancelButton)
            mergeCancelButton = CreateMergeButton(controlsRoot, "Cancel Gear Merge", "Cancel", new Vector2(66f, -120f), new Vector2(84f, 40f), 20f, 16f);
        if (!mergeStatusText)
        {
            var statusObject = new GameObject("Gear Merge Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = (RectTransform)statusObject.transform;
            rect.SetParent(controlsRoot, false);
            SetTopAnchoredRect(rect, new Vector2(0f, -180f), new Vector2(200f, 66f));
            mergeStatusText = statusObject.GetComponent<TMP_Text>();
            CopyTextStyle(mergeStatusText);
            mergeStatusText.fontSize = 16f;
            mergeStatusText.enableAutoSizing = true;
            mergeStatusText.fontSizeMin = 16f;
            mergeStatusText.fontSizeMax = 16f;
            mergeStatusText.alignment = TextAlignmentOptions.Center;
            mergeStatusText.raycastTarget = false;
        }

        // Reserve a strip above the scrollable Gear icons for the generated controls.
        if (slotsRoot && slotsRoot.parent is RectTransform viewport && viewport.parent == controlsRoot)
        {
            _generatedControlsViewport = viewport;
            viewport.offsetMin = new Vector2(viewport.offsetMin.x, slotSize >= 60 ? 0f : 10f);
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, -222f);
        }
    }

    Button CreateMergeButton(RectTransform parent, string objectName, string label, Vector2 position, Vector2 size, float fontSize, float minFontSize)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        SetTopAnchoredRect(rect, position, size);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.24f, 0.25f, 0.45f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(3f, 1f);
        labelRect.offsetMax = new Vector2(-3f, -1f);
        var text = labelObject.GetComponent<TMP_Text>();
        CopyTextStyle(text);
        text.text = label;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = minFontSize;
        text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return button;
    }

    void CopyTextStyle(TMP_Text text)
    {
        if (capacityLabel && capacityLabel.font) text.font = capacityLabel.font;
        text.color = Color.white;
    }

    static void SetTopAnchoredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    GearIconSlotUI CreateSlot()
    {
        var go = new GameObject("Gear Icon", typeof(RectTransform), typeof(Image), typeof(GearIconSlotUI), typeof(CombatInspectUI), typeof(Selectable));
        go.GetComponent<Selectable>().transition = Selectable.Transition.None;
        var rect = (RectTransform)go.transform;
        rect.SetParent(slotsRoot, false);
        rect.sizeDelta = new Vector2(slotSize, slotSize);
        var image = go.GetComponent<Image>();
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;

        var textObject = new GameObject("Icon", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 30f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 30f;
        label.raycastTarget = false;
        return go.GetComponent<GearIconSlotUI>();
    }

    void HandleRunStarted(string _)
    {
        CancelMerge();
        CloseDetail();
    }

    void HandleContextEnded()
    {
        CloseDetail();
        CancelMerge();
    }

    void HandleCardSelected(CardView card)
    {
        if (card == null) return;
        CloseDetail();
        HideTooltip();
        if (_mergeSelecting) CancelMerge();
    }

    void HandleEnemyTargetingChanged(bool active)
    {
        if (!active) return;
        CloseDetail();
        HideTooltip();
        if (_mergeSelecting) CancelMerge();
    }

    void HandleEncounterResult(EncounterResult _) => CloseDetail();

    bool IsMergeEligible(CardManager cards, long id)
    {
        if (!_mergeSelecting) return true;
        if (id == _firstMergeId || id == _secondMergeId) return true;
        long anchor = _firstMergeId != 0 ? _firstMergeId : _secondMergeId;
        if (anchor != 0) return cards.GetGearMergeResult(anchor, id) != null;
        foreach (var candidate in cards.OwnedGearInstances)
            if (candidate != null && cards.GetGearMergeResult(id, candidate.Id) != null) return true;
        return false;
    }

    void BeginMergeSelection()
    {
        var cards = source ? source : CardManager.Instance;
        if (_ownedGearCount < 2 || GameplayInputGate.IsBlocked ||
            cards != null && (cards.IsHandEnhancementTargeting || cards.IsEnemyTargeting)) return;
        CloseDetail();
        HideTooltip();
        FindFirstObjectByType<ActionButtonsUI>()?.ClearSelectedConsumable();
        cards?.ClearSelection();
        _mergeNode = RunManager.Instance?.ActiveNode;
        _mergeCombatState = CombatManager.Instance != null ? CombatManager.Instance.currentState : GameState.Idle;
        _mergeSelecting = true;
        _mergeAwaitingConfirmation = false;
        _firstMergeId = _secondMergeId = 0;
        _mergePreview = null;
        Refresh();
    }

    void SelectForMerge(long instanceId, GearData gear)
    {
        if (!_mergeSelecting || instanceId == 0 || GameplayInputGate.IsBlocked ||
            (_subscribedSource != null && _subscribedSource.IsEnemyTargeting)) return;
        if (_firstMergeId == instanceId) _firstMergeId = 0;
        else if (_secondMergeId == instanceId) _secondMergeId = 0;
        else if (_firstMergeId == 0) _firstMergeId = instanceId;
        else if (_secondMergeId == 0) _secondMergeId = instanceId;
        _mergeAwaitingConfirmation = false;
        _mergePreview = null;
        var cards = source ? source : CardManager.Instance;
        _mergePreview = cards ? cards.GetGearMergeResult(_firstMergeId, _secondMergeId) : null;
        _mergeAwaitingConfirmation = _mergePreview != null;
        Refresh();
    }

    void ConfirmMerge()
    {
        if (!_mergeAwaitingConfirmation || !_mergePreview || GameplayInputGate.IsBlocked ||
            (_subscribedSource != null && _subscribedSource.IsEnemyTargeting)) return;
        var cards = source ? source : CardManager.Instance;
        if (!cards || cards.GetGearInstanceById(_firstMergeId) == null ||
            cards.GetGearInstanceById(_secondMergeId) == null || _firstMergeId == _secondMergeId ||
            !cards.MergeGear(_firstMergeId, _secondMergeId))
        {
            CancelMerge();
            return;
        }
        CancelMerge();
    }

    void CancelMerge()
    {
        _mergeSelecting = false;
        _mergeAwaitingConfirmation = false;
        _firstMergeId = _secondMergeId = 0;
        _mergePreview = null;
        UpdateMergeControls();
        Refresh();
    }

    void UpdateMergeControls()
    {
        bool canMergeMode = _ownedGearCount >= 2;
        if (_generatedControlsViewport)
            _generatedControlsViewport.offsetMax = new Vector2(_generatedControlsViewport.offsetMax.x,
                slotSize >= 60 ? _mergeSelecting ? -160f : canMergeMode ? -45f : 0f
                    : _mergeSelecting ? -222f : canMergeMode ? -104f : -62f);
        if (mergeModeButton)
        {
            mergeModeButton.gameObject.SetActive(_mergeSelecting || canMergeMode);
            mergeModeButton.interactable = !_mergeSelecting && canMergeMode;
        }
        if (mergeConfirmButton)
        {
            mergeConfirmButton.gameObject.SetActive(_mergeSelecting);
            mergeConfirmButton.interactable = _mergeAwaitingConfirmation;
        }
        if (mergeCancelButton)
        {
            mergeCancelButton.gameObject.SetActive(_mergeSelecting);
            mergeCancelButton.interactable = _mergeSelecting;
        }
        if (mergeStatusText)
        {
            mergeStatusText.gameObject.SetActive(_mergeSelecting);
            if (!_mergeSelecting) mergeStatusText.text = string.Empty;
            else if (_mergePreview) mergeStatusText.text = $"Merge into {_mergePreview.displayName}? Confirm or Cancel.";
            else if (_firstMergeId != 0 && _secondMergeId != 0)
                mergeStatusText.text = "These copies cannot be merged. Select a matching pair.";
            else mergeStatusText.text = $"Select two matching Gear copies ({(_firstMergeId != 0 ? 1 : 0) + (_secondMergeId != 0 ? 1 : 0)}/2).";
        }
    }

    void ShowTooltip(GearData gear)
    {
        _hovered = gear;
        if (ContextualTooltipUI.Instance || CombatDetailPanelUI.IsCombatActive) return;
        if (_selectedInstanceId != 0 && detailPanel && detailPanel.activeSelf) HideTooltip();
        else
        {
            if (tooltipText) tooltipText.text = Describe(gear);
            if (tooltipPanel) tooltipPanel.SetActive(true);
        }
    }

    void HideTooltipFor(GearData gear)
    {
        if (_hovered == gear) HideTooltip();
    }

    void HideTooltip()
    {
        _hovered = null;
        if (ContextualTooltipUI.Instance || CombatDetailPanelUI.IsCombatActive) return;
        if (tooltipPanel) tooltipPanel.SetActive(false);
    }

    void SelectGear(long instanceId, GearData gear)
    {
        if (instanceId == 0 || gear == null) return;
        if (_selectedInstanceId == instanceId)
        {
            CloseDetail();
            Refresh();
            return;
        }
        CloseDetail();
        CardManager.Instance?.ClearSelection();
        FindFirstObjectByType<ActionButtonsUI>()?.ClearSelectedConsumable();
        _selectedInstanceId = instanceId;
        HideTooltip();
        if (ContextualTooltipUI.Instance || CombatDetailPanelUI.IsCombatActive) { Refresh(); return; }
        if (detailText) detailText.text = $"<size=22><b>{gear.displayName}</b></size>\n{gear.description}";
        PositionDetailBesideSlot(instanceId);
        if (detailPanel)
        {
            detailPanel.SetActive(true);
            detailPanel.transform.SetAsLastSibling();
        }
        Refresh();
    }

    void PositionDetailBesideSlot(long instanceId)
    {
        if (!detailPanel || !slotsRoot || !(detailPanel.transform is RectTransform detailRect)) return;
        var detailCanvas = detailPanel.GetComponent<Canvas>();
        if (detailCanvas)
        {
            detailCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            detailCanvas.enabled = true;
            detailCanvas.overrideSorting = true;
            detailCanvas.sortingOrder = 60;
        }
        var raycaster = detailPanel.GetComponent<GraphicRaycaster>();
        if (raycaster) raycaster.enabled = true;
        detailRect.anchorMin = detailRect.anchorMax = new Vector2(0.5f, 0.5f);
        detailRect.pivot = new Vector2(0.5f, 0.5f);
        detailRect.sizeDelta = new Vector2(190f, 96f);
        if (detailText)
        {
            var textRect = detailText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 8f);
            textRect.offsetMax = new Vector2(-38f, -8f);
            detailText.fontSize = 18f;
            detailText.enableAutoSizing = false;
            detailText.fontSizeMin = 16f;
            detailText.fontSizeMax = 20f;
            detailText.fontStyle = FontStyles.Normal;
            detailText.alignment = TextAlignmentOptions.TopLeft;
            detailText.overflowMode = TextOverflowModes.Ellipsis;
            float textHeight = detailText.GetPreferredValues(detailText.text, 192f, 0f).y;
            detailRect.sizeDelta = new Vector2(240f, Mathf.Max(112f, Mathf.Ceil(textHeight + 18f)));
        }
        var closeRect = detailCloseButton ? detailCloseButton.transform as RectTransform : null;
        if (closeRect)
        {
            closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.sizeDelta = new Vector2(32f, 32f);
            closeRect.anchoredPosition = new Vector2(-4f, -4f);
            var closeLabel = detailCloseButton.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel)
            {
                closeLabel.text = "Ă—";
                closeLabel.fontSize = 20f;
                closeLabel.enableAutoSizing = false;
            }
        }
        var discardText = detailPanel.transform.Find("DiscardPileText");
        if (discardText) discardText.gameObject.SetActive(false);
        var drawViewport = detailPanel.transform.Find("DrawDeckViewport");
        if (drawViewport) drawViewport.gameObject.SetActive(false);
        var panelImage = detailPanel.GetComponent<Image>();
        if (panelImage) panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.96f);
        var parentRect = detailRect.parent as RectTransform;
        if (parentRect == null) return;
        GearIconSlotUI selectedSlot = null;
        foreach (var slot in _slots)
            if (slot && slot.InstanceId == instanceId) { selectedSlot = slot; break; }
        if (!selectedSlot) return;
        var canvas = parentRect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var slotRect = selectedSlot.transform as RectTransform;
        var corners = new Vector3[4];
        slotRect.GetWorldCorners(corners);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[3]);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, camera, out var local)) return;
        float halfWidth = detailRect.rect.width * 0.5f;
        float halfHeight = detailRect.rect.height * 0.5f;
        local.x += halfWidth + 12f;
        if (local.x + halfWidth > parentRect.rect.xMax)
            local.x -= detailRect.rect.width + slotRect.rect.width + 24f;
        local.x = Mathf.Clamp(local.x, parentRect.rect.xMin + halfWidth, parentRect.rect.xMax - halfWidth);
        local.y += slotRect.rect.height * 0.5f;
        local.y = Mathf.Clamp(local.y, parentRect.rect.yMin + halfHeight, parentRect.rect.yMax - halfHeight);
        detailRect.anchoredPosition = local;
        _lastDetailParentSize = parentRect.rect.size;
        _hasLastDetailParentSize = true;
    }

    public void ClearObjectSelection()
    {
        CloseDetail();
        HideTooltip();
        if (_mergeSelecting) CancelMerge();
    }

    public void CloseDetail()
    {
        _selectedInstanceId = 0;
        _hasLastDetailParentSize = false;
        if (detailText) detailText.text = string.Empty;
        foreach (var slot in _slots)
            if (slot) slot.SetDetailSelected(false);
        if (detailPanel) detailPanel.SetActive(false);
        if (_sellPanel) _sellPanel.gameObject.SetActive(false);
    }

    static string Describe(GearData gear) => gear
        ? $"{gear.displayName}\n{gear.description}" : string.Empty;
}
