using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Owns contextual hand targeting. Separate entries are reserved for choices not visible in hand.
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "EnhancementTargetUI")]
public sealed class EnhancementTargetUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text titleLabel;
    public TMP_Text feedbackLabel;
    public Transform cardsContainer;
    public GameObject cardButtonPrefab;
    public Button confirmButton;
    public Button backButton;

    string _shopOfferId;
    int _upgradeIndex = -1;
    int _eventChoiceIndex = -1;
    int _consumableSlotIndex = -1;
    int _mutationSlotIndex = -1;
    readonly List<int> _mutationCardIds = new();
    CardEnhancementData _eventEnhancement;
    CardEnhancementData _acquisitionEnhancement;
    int _selectedCardId;
    bool _replacementConfirmed;
    bool _acquisitionTargetMode;
    GameObject _originPanel;
    bool _restoreOriginOnClose;
    bool _hasSavedPanelLayout;
    CardView _targetedHandView;
    CardEnhancementData _replacementToConfirm;
    RectTransform _dialogCardRect;
    RectTransform _titleRect;
    RectTransform _feedbackRect;
    RectTransform _confirmRect;
    RectTransform _backRect;
    GameObject _targetListViewport;
    bool _targetListViewportWasActive;
    RectTransformSnapshot _dialogCardSnapshot, _titleSnapshot, _feedbackSnapshot, _confirmSnapshot, _backSnapshot;
    Vector2 _savedAnchorMin, _savedAnchorMax, _savedPivot, _savedSizeDelta;
    Vector3 _savedAnchoredPosition;
    ConsumableInstance _sourceConsumable;
    GameState _targetingCombatState;
    PathNode _targetingNode;
    bool _resolving;
    RectTransformSnapshot _viewportSnapshot;
    TMP_Text _confirmLabel, _backLabel;
    string _savedConfirmText, _savedBackText;
    public bool IsTargeting => _acquisitionTargetMode || _sourceConsumable != null;
    public int SelectedCardId => _selectedCardId;

    struct RectTransformSnapshot
    {
        public Vector2 anchorMin, anchorMax, pivot, sizeDelta;
        public Vector3 anchoredPosition;

        public static RectTransformSnapshot Capture(RectTransform rect) => rect == null ? default : new RectTransformSnapshot
        {
            anchorMin = rect.anchorMin,
            anchorMax = rect.anchorMax,
            pivot = rect.pivot,
            sizeDelta = rect.sizeDelta,
            anchoredPosition = rect.anchoredPosition3D
        };

        public void Restore(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition3D = anchoredPosition;
        }
    }

    void OnEnable()
    {
        var run = RunManager.Instance;
        if (run != null)
        {
            run.OnHideShop += Hide;
            run.OnHideUpgrade += Hide;
            run.OnHideEvent += Hide;
            run.OnRunStarted += HandleRunStarted;
            run.OnShowPathScreen += Hide;
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnBuildChanged += RefreshCards;
            CardManager.Instance.OnDeckChanged += RefreshCards;
            CardManager.Instance.OnConsumablesChanged += ValidateTargeting;
        }
        if (confirmButton) { confirmButton.onClick.AddListener(Confirm); KeyButtonBorderStyle.Apply(confirmButton); }
        if (backButton) backButton.onClick.AddListener(Back);
        Hide();
    }

    void OnDisable()
    {
        var run = RunManager.Instance;
        if (run != null)
        {
            run.OnHideShop -= Hide;
            run.OnHideUpgrade -= Hide;
            run.OnHideEvent -= Hide;
            run.OnRunStarted -= HandleRunStarted;
            run.OnShowPathScreen -= Hide;
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnBuildChanged -= RefreshCards;
            CardManager.Instance.OnDeckChanged -= RefreshCards;
            CardManager.Instance.OnConsumablesChanged -= ValidateTargeting;
        }
        if (confirmButton) confirmButton.onClick.RemoveListener(Confirm);
        if (backButton) backButton.onClick.RemoveListener(Back);
        Hide();
    }

    void HandleRunStarted(string seed) => Hide();

    public void OpenShop(string stableId)
    {
        var run = RunManager.Instance;
        if (run == null || GameplayInputGate.IsBlocked ||
            !string.IsNullOrEmpty(run.GetShopOfferUnavailableReason(stableId))) return;
        ShopOffer offer = null;
        foreach (var candidate in run.GetCurrentShopOffers())
            if (candidate.StableId == stableId) { offer = candidate; break; }
        if (offer == null || offer.kind != ShopOfferKind.Enhancement) return;
        _shopOfferId = stableId;
        _upgradeIndex = -1;
        _eventChoiceIndex = -1;
        _consumableSlotIndex = -1;
        BeginAcquisitionTargeting(offer);
    }

    public void OpenUpgrade(int index)
    {
        var run = RunManager.Instance;
        if (run == null || GameplayInputGate.IsBlocked ||
            run.ActiveNode == null || run.ActiveNode.kind != MapNodeType.Upgrade ||
            index < 0 || index >= run.GetCurrentUpgradeOffers().Count) return;
        var offer = run.GetCurrentUpgradeOffers()[index];
        if (offer.kind != ShopOfferKind.Enhancement || offer.enhancement == null) return;
        _shopOfferId = null;
        _upgradeIndex = index;
        _eventChoiceIndex = -1;
        _consumableSlotIndex = -1;
        BeginAcquisitionTargeting(offer);
    }

    public bool CanOpenEventEnhancementPicker => panel != null && cardsContainer != null &&
        cardButtonPrefab != null && confirmButton != null;
    public bool CanOpenConsumableTarget => panel != null && confirmButton != null && backButton != null;
    public bool CanOpenDeckMutationTarget => CanOpenConsumableTarget;

    public void OpenDeckMutationTarget(int slotIndex) => OpenConsumableTarget(slotIndex);

    public void OpenConsumableTarget(int slotIndex)
    {
        var cards = CardManager.Instance;
        var item = cards != null ? cards.GetConsumableAtSlot(slotIndex) : null;
        if (!CanOpenConsumableTarget || cards == null || item == null || GameplayInputGate.IsBlocked ||
            cards.GetConsumableTargetMaximum(slotIndex) == 0 ||
            !cards.CanUseConsumableAtSlot(slotIndex, CombatManager.Instance)) return;
        Hide();
        _sourceConsumable = cards.GetConsumableInstanceAtSlot(slotIndex);
        _consumableSlotIndex = slotIndex;
        _mutationSlotIndex = item.effectType == ConsumableEffectType.ApplyEnhancement ? -1 : slotIndex;
        _targetingCombatState = CombatManager.Instance.currentState;
        _targetingNode = RunManager.Instance?.ActiveNode;
        ClearTargetCards();
        if (titleLabel) titleLabel.text = item.displayName.ToUpperInvariant();
        SaveAndCompactPanel();
        // Outside combat the existing rules also allow deck/discard targets. Present only those
        // off-board cards, while the real hand remains clickable below this bounded panel.
        if (!cards.ConsumableRequiresHandTarget(slotIndex)) ShowOffHandChoicesLayout();
        if (panel) { BoardPanelTransition.FadeIn(panel); panel.transform.SetAsLastSibling(); }
        cards.BeginHandTargeting(view => SelectCard(view.data.Id), id => cards.CanTargetConsumableAtSlot(slotIndex, id));
        RefreshCards();
        UpdateFeedback();
    }

    void Update()
    {
        if (!IsTargeting) return;
        ValidateTargeting();
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
    }

    void ValidateTargeting()
    {
        if (_resolving || !IsTargeting) return;
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (cards == null || combat == null || combat.player.IsDefeated ||
            !cards.IsHandEnhancementTargeting || RunManager.Instance?.ActiveNode != _targetingNode ||
            combat.currentState != _targetingCombatState)
        { Hide(); return; }
        if (_sourceConsumable != null)
        {
            if (cards.GetConsumableInstanceAtSlot(_consumableSlotIndex) != _sourceConsumable ||
                (!GameplayInputGate.IsBlocked && !cards.CanUseConsumableAtSlot(_consumableSlotIndex, combat)))
            { Hide(); return; }
            foreach (int id in _mutationCardIds)
                if (!GameplayInputGate.IsBlocked && !cards.CanTargetConsumableAtSlot(_consumableSlotIndex, id))
                { Hide(); return; }
            if (_selectedCardId != 0 && !GameplayInputGate.IsBlocked &&
                !cards.CanTargetConsumableAtSlot(_consumableSlotIndex, _selectedCardId))
            { Hide(); return; }
        }
        else if (_selectedCardId != 0 && !cards.hand.Contains(cards.FindOwnedCard(_selectedCardId)))
        { Hide(); return; }
    }

    void ShowOffHandChoicesLayout()
    {
        var root = panel.transform as RectTransform;
        if (root) { root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.64f); root.sizeDelta = new Vector2(680f, 360f); }
        if (_dialogCardRect) _dialogCardRect.sizeDelta = new Vector2(660f, 340f);
        CompactRect(_titleRect, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.98f), Vector2.zero);
        CompactRect(_feedbackRect, new Vector2(0.04f, 0.69f), new Vector2(0.96f, 0.85f), Vector2.zero);
        CompactRect(_confirmRect, new Vector2(0.05f, 0.02f), new Vector2(0.48f, 0.13f), Vector2.zero);
        CompactRect(_backRect, new Vector2(0.52f, 0.02f), new Vector2(0.95f, 0.13f), Vector2.zero);
        if (_targetListViewport)
        {
            _targetListViewport.SetActive(true);
            CompactRect(_targetListViewport.transform as RectTransform, new Vector2(0.04f, 0.15f), new Vector2(0.96f, 0.67f), Vector2.zero);
        }
    }

    public void OpenEventEnhancement(int choiceIndex)
    {
        var run = RunManager.Instance;
        if (run == null || IsInputBlockedForCurrentChoice() ||
            !string.IsNullOrEmpty(run.GetEventOptionUnavailableReason(choiceIndex))) return;
        _shopOfferId = null;
        _upgradeIndex = -1;
        _eventChoiceIndex = choiceIndex;
        _eventEnhancement = null;
        _consumableSlotIndex = -1;
        _selectedCardId = 0;
        ShowEventEnhancements();
    }

    void ShowEventEnhancements()
    {
        _mutationSlotIndex = -1;
        _mutationCardIds.Clear();
        var run = RunManager.Instance;
        var candidates = run != null ? run.GetEventEnhancementsForChoice(_eventChoiceIndex) : null;
        if (!panel || !cardsContainer || !cardButtonPrefab || candidates == null) return;
        if (titleLabel) titleLabel.text = "CHOOSE AN ENHANCEMENT";
        if (feedbackLabel) feedbackLabel.text = "Choose one Mod, then click a card in your hand to receive it.";
        if (confirmButton) confirmButton.interactable = false;
        ClearTargetCards();
        foreach (var enhancement in candidates)
        {
            if (enhancement == null) continue;
            var obj = Instantiate(cardButtonPrefab, cardsContainer);
            obj.name = $"EventEnhancement_{enhancement.id}";
            var label = obj.GetComponentInChildren<TMP_Text>();
            if (label) label.text = $"{enhancement.icon} {enhancement.displayName}\n{enhancement.description}";
            var button = obj.GetComponent<Button>();
            if (button) button.onClick.AddListener(() => SelectEventEnhancement(enhancement));
        }
        BoardPanelTransition.FadeIn(panel);
        panel.transform.SetAsLastSibling();
    }

    void SelectEventEnhancement(CardEnhancementData enhancement)
    {
        if (IsInputBlockedForCurrentChoice() || enhancement == null || _eventChoiceIndex < 0) return;
        _eventEnhancement = enhancement;
        BeginAcquisitionTargeting(new ShopOffer { kind = ShopOfferKind.Enhancement, enhancement = enhancement, price = 0 });
    }

    void BeginAcquisitionTargeting(ShopOffer offer)
    {
        if (offer == null || offer.enhancement == null || CardManager.Instance == null) return;
        _acquisitionTargetMode = true;
        _targetingCombatState = CombatManager.Instance.currentState;
        _targetingNode = RunManager.Instance?.ActiveNode;
        _acquisitionEnhancement = offer.enhancement;
        _selectedCardId = 0;
        _replacementConfirmed = false;
        _replacementToConfirm = null;
        _mutationSlotIndex = -1;
        _mutationCardIds.Clear();
        _targetedHandView = null;
        _originPanel = _shopOfferId != null ? FindFirstObjectByType<ShopUI>()?.panel :
            _eventChoiceIndex >= 0 || _upgradeIndex >= 0 ? FindFirstObjectByType<RunEventUI>()?.panel : null;
        if (_originPanel) _originPanel.SetActive(false);
        if (titleLabel) titleLabel.text = $"{offer.Icon} {offer.DisplayName}";
        if (feedbackLabel) feedbackLabel.text = "Click a card in your hand to target this Mod.";
        if (confirmButton) confirmButton.interactable = false;
        SaveAndCompactPanel();
        if (panel) { BoardPanelTransition.FadeIn(panel); panel.transform.SetAsLastSibling(); }
        ClearTargetCards();
        CardManager.Instance.BeginHandTargeting(HandleHandCardTargeted, id =>
            string.IsNullOrEmpty(RunManager.Instance.GetEnhancementAcquisitionTargetUnavailableReason(id, _acquisitionEnhancement)));
    }

    void HandleHandCardTargeted(CardView view)
    {
        if (!_acquisitionTargetMode || view == null || view.data == null || IsInputBlockedForCurrentChoice()) return;
        int id = view.data.Id;
        var run = RunManager.Instance;
        string unavailableReason = run != null
            ? run.GetEnhancementAcquisitionTargetUnavailableReason(id, _acquisitionEnhancement)
            : "Mod unavailable";
        if (!string.IsNullOrEmpty(unavailableReason))
        {
            if (_targetedHandView) _targetedHandView.SetSelected(false);
            _targetedHandView = null;
            _selectedCardId = 0;
            _replacementConfirmed = false;
            _replacementToConfirm = null;
            UpdateFeedback(unavailableReason);
            return;
        }
        if (_targetedHandView && _targetedHandView != view) _targetedHandView.SetSelected(false);
        _targetedHandView = view;
        _targetedHandView.SetSelected(true);
        _selectedCardId = id;
        _replacementConfirmed = false;
        _replacementToConfirm = null;
        UpdateFeedback();
    }

    void SaveAndCompactPanel()
    {
        if (!panel || _hasSavedPanelLayout) return;
        var rect = panel.transform as RectTransform;
        if (rect == null) return;
        _savedAnchorMin = rect.anchorMin;
        _savedAnchorMax = rect.anchorMax;
        _savedPivot = rect.pivot;
        _savedSizeDelta = rect.sizeDelta;
        _savedAnchoredPosition = rect.anchoredPosition3D;
        _dialogCardRect = titleLabel ? titleLabel.rectTransform.parent as RectTransform : null;
        _titleRect = titleLabel ? titleLabel.rectTransform : null;
        _feedbackRect = feedbackLabel ? feedbackLabel.rectTransform : null;
        _confirmRect = confirmButton ? confirmButton.transform as RectTransform : null;
        _backRect = backButton ? backButton.transform as RectTransform : null;
        _dialogCardSnapshot = RectTransformSnapshot.Capture(_dialogCardRect);
        _titleSnapshot = RectTransformSnapshot.Capture(_titleRect);
        _feedbackSnapshot = RectTransformSnapshot.Capture(_feedbackRect);
        _confirmSnapshot = RectTransformSnapshot.Capture(_confirmRect);
        _backSnapshot = RectTransformSnapshot.Capture(_backRect);
        // Acquisition starts while this overlay is still hidden; include inactive parents so the
        // authored CardViewport, not just its content, is disabled during hand-field targeting.
        var scroll = cardsContainer ? cardsContainer.GetComponentInParent<ScrollRect>(true) : null;
        _targetListViewport = scroll ? scroll.gameObject : cardsContainer ? cardsContainer.gameObject : null;
        _targetListViewportWasActive = _targetListViewport && _targetListViewport.activeSelf;
        _viewportSnapshot = RectTransformSnapshot.Capture(_targetListViewport ? _targetListViewport.transform as RectTransform : null);
        _confirmLabel = confirmButton ? confirmButton.GetComponentInChildren<TMP_Text>(true) : null;
        _backLabel = backButton ? backButton.GetComponentInChildren<TMP_Text>(true) : null;
        _savedConfirmText = _confirmLabel ? _confirmLabel.text : null;
        _savedBackText = _backLabel ? _backLabel.text : null;
        if (_confirmLabel) _confirmLabel.text = "CONFIRM";
        if (_backLabel) _backLabel.text = "CANCEL";
        if (_targetListViewport) _targetListViewport.SetActive(false);
        _hasSavedPanelLayout = true;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.46f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(460f, 190f);
        rect.anchoredPosition3D = Vector3.zero;
        CompactRect(_dialogCardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(440f, 170f));
        CompactRect(_titleRect, new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.96f), Vector2.zero);
        CompactRect(_feedbackRect, new Vector2(0.05f, 0.33f), new Vector2(0.95f, 0.68f), Vector2.zero);
        CompactRect(_confirmRect, new Vector2(0.05f, 0.05f), new Vector2(0.48f, 0.28f), Vector2.zero);
        CompactRect(_backRect, new Vector2(0.52f, 0.05f), new Vector2(0.95f, 0.28f), Vector2.zero);
    }

    static void CompactRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        if (rect == null) return;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition3D = Vector3.zero;
    }

    void RestorePanelLayout()
    {
        if (!_hasSavedPanelLayout || !panel) return;
        var rect = panel.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = _savedAnchorMin;
            rect.anchorMax = _savedAnchorMax;
            rect.pivot = _savedPivot;
            rect.sizeDelta = _savedSizeDelta;
            rect.anchoredPosition3D = _savedAnchoredPosition;
        }
        _dialogCardSnapshot.Restore(_dialogCardRect);
        _titleSnapshot.Restore(_titleRect);
        _feedbackSnapshot.Restore(_feedbackRect);
        _confirmSnapshot.Restore(_confirmRect);
        _backSnapshot.Restore(_backRect);
        _viewportSnapshot.Restore(_targetListViewport ? _targetListViewport.transform as RectTransform : null);
        if (_confirmLabel) _confirmLabel.text = _savedConfirmText;
        if (_backLabel) _backLabel.text = _savedBackText;
        if (_targetListViewport) _targetListViewport.SetActive(_targetListViewportWasActive);
        _dialogCardRect = _titleRect = _feedbackRect = _confirmRect = _backRect = null;
        _targetListViewport = null;
        _targetListViewportWasActive = false;
        _hasSavedPanelLayout = false;
    }

    public void Hide()
    {
        var cards = CardManager.Instance;
        bool ownedTargeting = IsTargeting;
        _sourceConsumable = null;
        if (ownedTargeting && cards != null && cards.IsHandEnhancementTargeting) cards.EndHandEnhancementTargeting();
        bool restoreOrigin = _restoreOriginOnClose;
        _restoreOriginOnClose = false;
        _acquisitionTargetMode = false;
        _acquisitionEnhancement = null;
        RestorePanelLayout();
        if (cardsContainer) cardsContainer.gameObject.SetActive(true);
        _shopOfferId = null;
        _upgradeIndex = -1;
        _eventChoiceIndex = -1;
        _eventEnhancement = null;
        _consumableSlotIndex = -1;
        _mutationSlotIndex = -1;
        _mutationCardIds.Clear();
        _selectedCardId = 0;
        _replacementConfirmed = false;
        _replacementToConfirm = null;
        if (_targetedHandView) _targetedHandView.SetSelected(false);
        _targetedHandView = null;
        if (panel) panel.SetActive(false);
        if (confirmButton) confirmButton.interactable = false;
        var origin = _originPanel;
        _originPanel = null;
        if (restoreOrigin && origin)
        {
            origin.SetActive(true);
            origin.transform.SetAsLastSibling();
        }
    }

    bool IsInputBlockedForCurrentChoice() => GameplayInputGate.IsBlocked &&
        !(RunManager.Instance != null && RunManager.Instance.IsPendingHammerReward &&
          GameplayInputGate.Reasons == GameplayInputBlockReason.GearChoice);

    void Back()
    {
        if (IsInputBlockedForCurrentChoice()) return;
        if (_acquisitionTargetMode)
        {
            _restoreOriginOnClose = true;
            Hide();
            return;
        }
        if (_eventChoiceIndex >= 0 && _eventEnhancement != null)
        {
            _eventEnhancement = null;
            _selectedCardId = 0;
            ShowEventEnhancements();
            return;
        }
        Hide();
    }

    void ClearTargetCards()
    {
        if (!cardsContainer) return;
        foreach (Transform child in cardsContainer)
        {
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }

    void RefreshCards()
    {
        if (!panel || !panel.activeSelf) return;
        ValidateTargeting();
        if (!panel.activeSelf || _resolving) return;
        CardManager.Instance?.RefreshHandTargetPresentation();
        if (_acquisitionTargetMode) { UpdateFeedback(); return; }
        if (_sourceConsumable == null || !cardsContainer || !cardButtonPrefab) return;
        ClearTargetCards();
        var cards = CardManager.Instance;
        if (cards != null && !cards.ConsumableRequiresHandTarget(_consumableSlotIndex))
        {
            foreach (var card in cards.ownedCards)
            {
                if (card == null || cards.hand.Contains(card)) continue;
                int id = card.Id;
                bool eligible = cards.CanTargetConsumableAtSlot(_consumableSlotIndex, id);
                var obj = Instantiate(cardButtonPrefab, cardsContainer);
                obj.name = $"OffHandCard_{id}";
                var label = obj.GetComponentInChildren<TMP_Text>();
                bool selected = _mutationSlotIndex >= 0 ? _mutationCardIds.Contains(id) : id == _selectedCardId;
                if (label) label.text = (selected ? "SELECTED  " : "") +
                    $"{card.SuitSymbol} {card.DisplayName}" +
                    (card.Enhancement != null ? $" · {card.Enhancement.displayName}" : "");
                var button = obj.GetComponent<Button>();
                if (button)
                {
                    button.interactable = eligible;
                    button.onClick.AddListener(() => SelectCard(id));
                    var colors = button.colors;
                    colors.normalColor = selected ? new Color(0.52f, 0.86f, 1f) : Color.white;
                    button.colors = colors;
                }
            }
        }
        RefreshHandSelection();
        UpdateFeedback();
    }

    void SelectCard(int id)
    {
        if (IsInputBlockedForCurrentChoice() || !panel || !panel.activeSelf) return;
        var cards = CardManager.Instance;
        if (_sourceConsumable == null || cards == null ||
            !cards.CanTargetConsumableAtSlot(_consumableSlotIndex, id)) return;
        if (_mutationSlotIndex >= 0)
        {
            if (!_mutationCardIds.Remove(id))
            {
                int maximum = cards.GetConsumableTargetMaximum(_consumableSlotIndex);
                if (maximum == 1) _mutationCardIds.Clear();
                if (_mutationCardIds.Count < maximum) _mutationCardIds.Add(id);
            }
        }
        else
        {
            _selectedCardId = _selectedCardId == id ? 0 : id;
            _replacementConfirmed = false;
            _replacementToConfirm = null;
        }
        RefreshCards();
    }

    void RefreshHandSelection()
    {
        var cards = CardManager.Instance;
        if (cards == null || !cards.handField) return;
        foreach (var view in cards.handField.GetComponentsInChildren<CardView>(true))
            if (view.data != null)
                view.SetSelected(_mutationSlotIndex >= 0 ? _mutationCardIds.Contains(view.data.Id) : view.data.Id == _selectedCardId);
    }

    void UpdateFeedback(string failure = null)
    {
        if (_sourceConsumable != null)
        {
            var cards = CardManager.Instance;
            IReadOnlyList<int> ids = _mutationSlotIndex >= 0 ? _mutationCardIds :
                _selectedCardId == 0 ? System.Array.Empty<int>() : new[] { _selectedCardId };
            bool valid = cards != null && cards.CanConfirmConsumableTargets(_consumableSlotIndex, ids);
            int maximum = cards != null ? cards.GetConsumableTargetMaximum(_consumableSlotIndex) : 1;
            string range = maximum == 1 ? "a card" : $"1–{maximum} cards";
            string scope = cards != null && cards.ConsumableRequiresHandTarget(_consumableSlotIndex)
                ? "in your hand" : "in your hand, or an off-hand card below";
            if (feedbackLabel) feedbackLabel.text = failure ?? $"Choose {range} {scope}\n{ids.Count}/{maximum} selected · Confirm or cancel";
            if (confirmButton) confirmButton.interactable = valid;
            return;
        }
        string reason = failure ?? (_selectedCardId == 0 ?
            (_acquisitionTargetMode ? "Click a card in your hand to target this Mod." : "Select an owned card, then confirm.") :
            _acquisitionTargetMode ? RunManager.Instance.GetEnhancementAcquisitionTargetUnavailableReason(_selectedCardId, _acquisitionEnhancement) :
                RunManager.Instance.GetEnhancementTargetUnavailableReason(_selectedCardId));
        var selectedCard = CardManager.Instance != null ? CardManager.Instance.FindOwnedCard(_selectedCardId) : null;
        if (feedbackLabel) feedbackLabel.text = !string.IsNullOrEmpty(reason) ? reason :
            selectedCard != null && selectedCard.Enhancement != null && _acquisitionTargetMode
                ? (_replacementConfirmed ? $"Replace {selectedCard.Enhancement.displayName}? Confirm again to replace it." :
                    $"Hand target: {selectedCard.DisplayName}. Confirm to review replacement.")
                : $"Hand target: {selectedCard?.DisplayName}. Confirm to apply.";
        if (confirmButton) confirmButton.interactable = _selectedCardId != 0 && string.IsNullOrEmpty(reason);
    }

    void Confirm()
    {
        if (IsInputBlockedForCurrentChoice() || !panel || !panel.activeSelf) return;
        ValidateTargeting();
        if (!panel.activeSelf) return;
        if (_sourceConsumable != null)
        {
            var cards = CardManager.Instance;
            IReadOnlyList<int> ids = _mutationSlotIndex >= 0 ? _mutationCardIds.ToArray() :
                _selectedCardId == 0 ? System.Array.Empty<int>() : new[] { _selectedCardId };
            if (cards == null || !cards.CanConfirmConsumableTargets(_consumableSlotIndex, ids)) return;
            _resolving = true;
            bool consumed;
            try
            {
                consumed = _mutationSlotIndex >= 0
                    ? cards.UseDeckMutationConsumableAtSlot(_consumableSlotIndex, ids)
                    : cards.UseEnhancementConsumableAtSlot(_consumableSlotIndex, _selectedCardId);
            }
            finally { _resolving = false; }
            if (consumed) Hide();
            else { ValidateTargeting(); UpdateFeedback("Trick or target unavailable"); }
            return;
        }
        if (_selectedCardId == 0) return;
        var run = RunManager.Instance;
        if (run == null) return;
        string offerId = _shopOfferId;
        int index = _upgradeIndex;
        int targetId = _selectedCardId;
        var target = CardManager.Instance != null ? CardManager.Instance.FindOwnedCard(targetId) : null;
        bool isAcquisition = _acquisitionTargetMode;
        if (isAcquisition)
        {
            string targetReason = run.GetEnhancementAcquisitionTargetUnavailableReason(targetId, _acquisitionEnhancement);
            if (!string.IsNullOrEmpty(targetReason))
            {
                UpdateFeedback(targetReason);
                return;
            }
        }
        if (isAcquisition && target != null && target.Enhancement != null && !_replacementConfirmed)
        {
            _replacementConfirmed = true;
            _replacementToConfirm = target.Enhancement;
            UpdateFeedback();
            return;
        }
        if (_replacementConfirmed && target != null && target.Enhancement != _replacementToConfirm)
        {
            _replacementConfirmed = false;
            _replacementToConfirm = null;
            UpdateFeedback("The existing Mod changed. Review the replacement again.");
            return;
        }
        bool replaceExisting = isAcquisition && target != null && target.Enhancement != null;
        bool shopAcquisition = offerId != null;
        bool success = _consumableSlotIndex >= 0
            ? CardManager.Instance != null && CardManager.Instance.UseEnhancementConsumableAtSlot(_consumableSlotIndex, targetId)
            : _eventChoiceIndex >= 0
                ? run.ChooseEventEnhancementTarget(_eventChoiceIndex, targetId, _eventEnhancement, replaceExisting)
                : shopAcquisition
                    ? run.PurchaseShopEnhancement(offerId, targetId, replaceExisting)
                    : run.ChooseUpgradeOffer(index, targetId, replaceExisting);
        if (success)
        {
            _restoreOriginOnClose = shopAcquisition;
            if (panel && panel.activeSelf) Hide();
            return;
        }
        string reason = isAcquisition
            ? run.GetEnhancementAcquisitionTargetUnavailableReason(targetId, _acquisitionEnhancement)
            : run.GetEnhancementTargetUnavailableReason(targetId);
        if (string.IsNullOrEmpty(reason))
            reason = _consumableSlotIndex >= 0 ? "Trick or target unavailable" :
                _eventChoiceIndex >= 0 ? "Incident Mod unavailable" :
                offerId != null ? run.GetShopOfferUnavailableReason(offerId) : "Upgrade unavailable";
        UpdateFeedback(string.IsNullOrEmpty(reason) ? "Mod unavailable" : reason);
    }
}
