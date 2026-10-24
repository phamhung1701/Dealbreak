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

    [Header("Legacy hover tooltip (kept hidden: details open on click)")]
    public GameObject tooltipPanel;
    public TMP_Text tooltipText;

    [Header("Visual Style Test")]
    public Sprite occupiedSlotSprite;
    public Sprite emptySlotSprite;
    public Sprite selectedSlotSprite;

    public GameObject detailPanel;
    public TMP_Text detailText;
    public Button detailCloseButton;

    readonly List<GearIconSlotUI> _slots = new();
    CardManager _subscribedSource;
    long _selectedInstanceId;
    Vector2 _lastDetailParentSize;
    bool _hasLastDetailParentSize;


    void OnEnable()
    {
        if (detailCloseButton) detailCloseButton.onClick.AddListener(CloseDetail);
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
        HideTooltip();
        CloseDetail();
    }

    // A rail enabled before CardManager is created can still attach as soon as it appears.
    void Update()
    {
        if (!_subscribedSource && (source || CardManager.Instance)) Refresh();
        if (_selectedInstanceId != 0 && detailPanel && detailPanel.activeSelf &&
            detailPanel.transform.parent is RectTransform detailParent &&
            (!_hasLastDetailParentSize || (detailParent.rect.size - _lastDetailParentSize).sqrMagnitude > 0.25f))
            PositionDetailBesideSlot(_selectedInstanceId);
        // Leaving the Black Market or losing the Gear closes the sale panel without a rail refresh.
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
                    slot.Bind(gear, selectedGear => SelectGear(instanceId, selectedGear),
                        instanceId, _selectedInstanceId == instanceId);
                    slot.gameObject.SetActive(true);
                }
                count++;
            }
        }

        int emptySlotCount = cards ? cards.GearCapacity : CardManager.BASE_GEAR_CAPACITY;
        int visibleSlotCount = Mathf.Max(count, emptySlotCount);
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
        FitSlots();

        if (_selectedInstanceId != 0 && (!cards || cards.GetGearInstanceById(_selectedInstanceId) == null)) CloseDetail();
    }

    // More slots than the row was laid out for (a character's extra Gear slot) shrink to fit it; the
    // fit is redone when the row's width changes (another window size).
    float _fittedWidth = -1f;
    void FitSlots()
    {
        int count = 0;
        foreach (var slot in _slots) if (slot && slot.gameObject.activeSelf) count++;
        float size = FittedSlotSize(count);
        foreach (var slot in _slots) if (slot && slot.gameObject.activeSelf) ((RectTransform)slot.transform).sizeDelta = new Vector2(size, size);
        _fittedWidth = slotsRoot && slotsRoot.parent is RectTransform viewport ? viewport.rect.width : -1f;
    }

    void LateUpdate()
    {
        if (slotsRoot && slotsRoot.parent is RectTransform viewport && !Mathf.Approximately(viewport.rect.width, _fittedWidth)) FitSlots();
    }

    float FittedSlotSize(int count)
    {
        if (count <= 0 || !slotsRoot || slotsRoot.parent is not RectTransform viewport) return slotSize;
        float width = viewport.rect.width;
        if (width <= 0f) return slotSize;
        var row = slotsRoot.GetComponent<HorizontalLayoutGroup>();
        float spacing = row ? row.spacing : 0f, padding = row ? row.padding.horizontal : 0f;
        return Mathf.Min(slotSize, Mathf.Floor((width - padding - spacing * (count - 1)) / count));
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

    void HandleRunStarted(string _) => CloseDetail();

    void HandleContextEnded() => CloseDetail();

    void HandleCardSelected(CardView card)
    {
        if (card == null) return;
        CloseDetail();
        HideTooltip();
    }

    void HandleEnemyTargetingChanged(bool active)
    {
        if (!active) return;
        CloseDetail();
        HideTooltip();
    }

    void HandleEncounterResult(EncounterResult _) => CloseDetail();

    void HideTooltip()
    {
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
        _selectedInstanceId = instanceId;
        HideTooltip();
        if (ContextualTooltipUI.Instance || CombatDetailPanelUI.IsCombatActive) { Refresh(); return; }
        if (detailText) detailText.text = $"<size=22><b>{gear.displayName}</b></size>\n{GearRules.OwnedDescription(CardManager.Instance, gear, instanceId)}";
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

    /// <summary>The visible slot showing the Gear instance, or null.</summary>
    public GearIconSlotUI SlotFor(long instanceId)
    {
        if (instanceId == 0) return null;
        foreach (var slot in _slots)
            if (slot && slot.isActiveAndEnabled && slot.InstanceId == instanceId) return slot;
        return null;
    }

    public void ClearObjectSelection()
    {
        CloseDetail();
        HideTooltip();
    }

    public void CloseDetail()
    {
        _selectedInstanceId = 0;
        _hasLastDetailParentSize = false;
        if (detailText) detailText.text = string.Empty;
        foreach (var slot in _slots)
            if (slot) slot.SetDetailSelected(false);
        if (detailPanel) detailPanel.SetActive(false);
    }
}
