using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "RunEventUI")]
public class RunEventUI : MonoBehaviour
{
    public bool editorialLayout;
    public GameObject panel;
    public TMP_Text titleLabel;
    public TMP_Text descriptionLabel;
    public Transform choicesContainer;
    public GameObject choiceButtonPrefab;
    public EnhancementTargetUI enhancementTargetUI;

    readonly HashSet<int> _selectedDiscardCardIds = new();
    int _discardChoiceIndex = -1;
    Action<int> _gearDiscardChoiceResolved;
    RunEventChoiceInteraction _discardInteraction = RunEventChoiceInteraction.Immediate;
    bool _choicesLayoutPrepared;
    bool _descriptionLayoutPrepared;
    ScrollRect _choicesScrollRect;
    ScrollRect _descriptionScrollRect;
    RectTransform _choicesViewport;
    float _lastChoiceViewportWidth = -1f;
    float _lastChoiceViewportHeight = -1f;
    float _baseChoiceSpacing;


    void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowEvent += Show;
            RunManager.Instance.OnHideEvent += Hide;
            RunManager.Instance.OnShowGearDiscardChoice += ShowGearDiscardChoice;
        }
        Hide();
        Canvas.willRenderCanvases += HandleCanvasWillRender;
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleCanvasWillRender;
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowEvent -= Show;
            RunManager.Instance.OnHideEvent -= Hide;
            RunManager.Instance.OnShowGearDiscardChoice -= ShowGearDiscardChoice;
        }
    }

    void Show(RunEventDefinition definition)
    {
        if (definition == null || panel == null || choicesContainer == null || choiceButtonPrefab == null)
            return;

        PrepareChoicesLayout();
        PrepareDescriptionLayout();
        if (titleLabel) titleLabel.text = definition.title;
        SetDescription(definition.description);

        ClearChoices();

        for (int i = 0; i < definition.choices.Length; i++)
        {
            int choiceIndex = i;
            var choice = definition.choices[i];
            var buttonObject = Instantiate(choiceButtonPrefab, choicesContainer);
            if(editorialLayout){var surface=buttonObject.GetComponent<EditorialSurface>();if(!surface)surface=buttonObject.AddComponent<EditorialSurface>();surface.treatment=EditorialSurface.Treatment.Paper;}
            buttonObject.name = $"EventChoice_{choiceIndex + 1}";
            string unavailableReason = RunManager.Instance.GetEventOptionUnavailableReason(choiceIndex);
            if (string.IsNullOrEmpty(unavailableReason) && !HasRequiredInteractionUI(choice.interaction))
                unavailableReason = "Card selection UI unavailable";
            bool available = string.IsNullOrEmpty(unavailableReason);
            var label = buttonObject.GetComponentInChildren<TMP_Text>();
            if (label)
            {
                string status = available ? string.Empty : $"\n<color=#DFA0A0>{unavailableReason}</color>";
                string description = choice.description ?? string.Empty;
                label.text = $"<size=125%><b>{choice.label}</b></size>\n{description}{status}";
                var detail=buttonObject.GetComponent<ContextualInfoTrigger>()??buttonObject.AddComponent<ContextualInfoTrigger>();
                detail.category="Incident choice";detail.title=choice.label;detail.description=description+"\n"+unavailableReason;detail.enabled=false;
            }

            var button = buttonObject.GetComponent<Button>();
            if (button)
            {
                button.interactable = available;
                button.onClick.AddListener(() => HandleChoice(choiceIndex, choice.interaction));
            }
        }

        BoardPanelTransition.Show(panel);
        DealbreakUIStyle.ApplyHierarchy(panel.transform);
        FinalizeChoiceList();
        panel.transform.SetAsLastSibling();
    }

    bool HasRequiredInteractionUI(RunEventChoiceInteraction interaction)
    {
        if (interaction == RunEventChoiceInteraction.DiscardTwoForRandomEnhancement ||
            interaction == RunEventChoiceInteraction.DiscardCardsForTotalValue)
        {
            var cards = CardManager.Instance;
            return choicesContainer != null && choiceButtonPrefab != null && cards != null &&
                cards.handField != null && cards.handField.cardsHolder != null;
        }
        return true;
    }

    void PrepareChoicesLayout()
    {
        if (_choicesLayoutPrepared || choicesContainer is not RectTransform content) return;
        var viewport = CreateScrollViewport(content, "EventChoicesViewport");
        if (viewport == null) return;
        _choicesLayoutPrepared = true;
        _choicesViewport = viewport;
        _choicesScrollRect = viewport.GetComponent<ScrollRect>();
        _choicesScrollRect.content = content;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            _baseChoiceSpacing = layout.spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
        }
        var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void PrepareDescriptionLayout()
    {
        if (_descriptionLayoutPrepared || descriptionLabel == null) return;
        var content = descriptionLabel.rectTransform;
        var viewport = CreateScrollViewport(content, "EventDescriptionViewport");
        if (viewport == null) return;
        _descriptionLayoutPrepared = true;
        _descriptionScrollRect = viewport.GetComponent<ScrollRect>();
        _descriptionScrollRect.content = content;
        var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        descriptionLabel.overflowMode = TextOverflowModes.Overflow;
    }

    static RectTransform CreateScrollViewport(RectTransform content, string viewportName)
    {
        var parent = content.parent as RectTransform;
        if (parent == null) return null;
        var viewportObject = new GameObject(viewportName, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        var viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(parent, false);
        viewport.anchorMin = content.anchorMin;
        viewport.anchorMax = content.anchorMax;
        viewport.pivot = content.pivot;
        viewport.anchoredPosition = content.anchoredPosition;
        viewport.sizeDelta = content.sizeDelta;
        viewport.localRotation = content.localRotation;
        viewport.localScale = content.localScale;
        var image = viewportObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return viewport;
    }

    void SetDescription(string text)
    {
        if (descriptionLabel == null) return;
        descriptionLabel.text = text;
    }

    void FinalizeChoiceList()
    {
        ConfigureChoicesViewport();
        RefreshChoiceHeights();
        Canvas.ForceUpdateCanvases();
        if (_choicesScrollRect != null)
        {
            _choicesScrollRect.velocity = Vector2.zero;
            _choicesScrollRect.verticalNormalizedPosition = 1f;
        }
        if (_descriptionScrollRect != null)
        {
            _descriptionScrollRect.velocity = Vector2.zero;
            _descriptionScrollRect.verticalNormalizedPosition = 1f;
        }
        _lastChoiceViewportWidth = _choicesViewport != null ? _choicesViewport.rect.width : -1f;
        _lastChoiceViewportHeight = _choicesViewport != null ? _choicesViewport.rect.height : -1f;
    }

    void HandleCanvasWillRender()
    {
        if (!_choicesLayoutPrepared || panel == null || !panel.activeInHierarchy || _choicesViewport == null) return;
        float width = _choicesViewport.rect.width;
        float height = _choicesViewport.rect.height;
        if (Mathf.Abs(width - _lastChoiceViewportWidth) <= 0.5f &&
            Mathf.Abs(height - _lastChoiceViewportHeight) <= 0.5f) return;
        ConfigureChoicesViewport();
        RefreshChoiceHeights();
        _lastChoiceViewportWidth = _choicesViewport.rect.width;
        _lastChoiceViewportHeight = _choicesViewport.rect.height;
    }

    void RefreshChoiceHeights()
    {
        ConfigureChoicesViewport();
        float availableWidth = _choicesViewport != null ? _choicesViewport.rect.width :
            choicesContainer is RectTransform content ? content.rect.width : 0f;
        if (availableWidth <= 0f || choicesContainer == null) return;
        foreach (Transform child in choicesContainer)
        {
            if (child == null || !child.gameObject.activeSelf) continue;
            var label = child.GetComponentInChildren<TMP_Text>();
            if (label != null) ConfigureChoiceHeight(child.gameObject, label, availableWidth);
        }
        FitChoiceSpacing(_choicesViewport != null ? _choicesViewport.rect.height : 0f);
    }

    void ConfigureChoicesViewport()
    {
        if(editorialLayout&&_choicesViewport){DealbreakUIStyle.Place(_choicesViewport,.46f,.12f,.94f,.80f);return;}
        if (_choicesViewport == null || _choicesViewport.parent is not RectTransform card) return;
        Rect cardRect = card.rect;
        if (cardRect.width <= 0f || cardRect.height <= 0f) return;

        RectTransform header = _descriptionScrollRect != null ? _descriptionScrollRect.viewport as RectTransform : null;
        if (header == null && descriptionLabel != null) header = descriptionLabel.rectTransform;
        if (header == null && titleLabel != null) header = titleLabel.rectTransform;

        float horizontalInset = cardRect.width * 0.04f;
        float bottom = cardRect.yMin + cardRect.height * 0.025f;
        float top = cardRect.yMax - cardRect.height * 0.08f;
        if (header != null)
        {
            Bounds headerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(card, header);
            top = Mathf.Min(top, headerBounds.min.y - cardRect.height * 0.02f);
        }
        if (top <= bottom) return;

        _choicesViewport.anchorMin = new Vector2(
            Mathf.InverseLerp(cardRect.xMin, cardRect.xMax, cardRect.xMin + horizontalInset),
            Mathf.InverseLerp(cardRect.yMin, cardRect.yMax, bottom));
        _choicesViewport.anchorMax = new Vector2(
            Mathf.InverseLerp(cardRect.xMin, cardRect.xMax, cardRect.xMax - horizontalInset),
            Mathf.InverseLerp(cardRect.yMin, cardRect.yMax, top));
        _choicesViewport.pivot = new Vector2(0.5f, 0.5f);
        _choicesViewport.offsetMin = Vector2.zero;
        _choicesViewport.offsetMax = Vector2.zero;
        _choicesViewport.anchoredPosition = Vector2.zero;
        _choicesViewport.sizeDelta = Vector2.zero;
    }

    void FitChoiceSpacing(float viewportHeight)
    {
        if (choicesContainer == null || viewportHeight <= 0f) return;
        var layout = choicesContainer.GetComponent<VerticalLayoutGroup>();
        if (layout == null) return;

        float requiredHeight = 0f;
        int activeCount = 0;
        foreach (Transform child in choicesContainer)
        {
            if (child == null || !child.gameObject.activeSelf) continue;
            var element = child.GetComponent<LayoutElement>();
            float height = element != null && element.preferredHeight > 0f
                ? element.preferredHeight
                : (child as RectTransform)?.rect.height ?? 0f;
            requiredHeight += height;
            activeCount++;
        }

        layout.spacing = activeCount > 1
            ? Mathf.Clamp((viewportHeight - requiredHeight) / (activeCount - 1), 0f, _baseChoiceSpacing)
            : _baseChoiceSpacing;
    }

    static void ConfigureChoiceHeight(GameObject buttonObject, TMP_Text label, float availableWidth)
    {
        var rect = buttonObject.GetComponent<RectTransform>();
        if (rect == null) return;
        float textWidth = Mathf.Max(1f, availableWidth - 32f);
        float textHeight = label.GetPreferredValues(label.text, textWidth, 0f).y;
        var layout = buttonObject.GetComponent<LayoutElement>() ?? buttonObject.AddComponent<LayoutElement>();
        layout.minWidth = 0f;
        layout.preferredWidth = -1f;
        layout.flexibleWidth = 1f;
        layout.minHeight = Mathf.Max(92f, textHeight + 24f);
        layout.preferredHeight = layout.minHeight;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    void HandleChoice(int choiceIndex, RunEventChoiceInteraction interaction)
    {
        switch (interaction)
        {
            case RunEventChoiceInteraction.HammerRetry:
                RunManager.Instance.RetryHammerReward();
                break;
            case RunEventChoiceInteraction.HammerDecline:
                RunManager.Instance.DeclineHammerReward();
                break;
            case RunEventChoiceInteraction.ChooseEnhancementTarget:
                ShowModChoices(choiceIndex);
                break;
            case RunEventChoiceInteraction.DiscardTwoForRandomEnhancement:
            case RunEventChoiceInteraction.DiscardCardsForTotalValue:
                ShowDiscardSelection(choiceIndex);
                break;
            default:
                RunManager.Instance.ChooseEventOption(choiceIndex);
                break;
        }
    }

    // A Mod choice lists its Mods; the one taken goes to the Trick slots as its Mod Trick.
    void ShowModChoices(int choiceIndex)
    {
        var run = RunManager.Instance;
        if (run == null || run.ActiveEvent == null) return;
        var definition = run.ActiveEvent;
        PrepareChoicesLayout();
        PrepareDescriptionLayout();
        ClearChoices();
        SetDescription(definition.choices[choiceIndex].description);
        foreach (var mod in run.GetEventEnhancementsForChoice(choiceIndex))
        {
            if (mod == null) continue;
            var chosen = mod;
            string reason = run.GetEventModUnavailableReason(choiceIndex, chosen);
            var buttonObject = Instantiate(choiceButtonPrefab, choicesContainer);
            buttonObject.name = $"ModChoice_{chosen.id}";
            if (editorialLayout)
            {
                var surface = buttonObject.GetComponent<EditorialSurface>();
                if (!surface) surface = buttonObject.AddComponent<EditorialSurface>();
                surface.treatment = EditorialSurface.Treatment.Paper;
            }
            var label = buttonObject.GetComponentInChildren<TMP_Text>();
            if (label)
                label.text = $"<size=125%><b>{chosen.displayName}</b></size>\n{chosen.description}" +
                    (string.IsNullOrEmpty(reason) ? string.Empty : $"\n<color=#DFA0A0>{reason}</color>");
            var button = buttonObject.GetComponent<Button>();
            if (button)
            {
                button.interactable = string.IsNullOrEmpty(reason);
                button.onClick.AddListener(() => run.ChooseEventMod(choiceIndex, chosen));
            }
        }
        var backObject = Instantiate(choiceButtonPrefab, choicesContainer);
        backObject.name = "ModChoiceBack";
        var backLabel = backObject.GetComponentInChildren<TMP_Text>();
        if (backLabel) backLabel.text = "<b>Back</b>";
        var back = backObject.GetComponent<Button>();
        if (back) back.onClick.AddListener(() => Show(definition));
        DealbreakUIStyle.ApplyHierarchy(panel.transform);
        FinalizeChoiceList();
    }

    void ShowGearDiscardChoice(string title, string description,
        IReadOnlyList<CardInstance> choices, Action<int> onResolved)
    {
        PrepareChoicesLayout();
        PrepareDescriptionLayout();
        if (panel == null || choicesContainer == null || choiceButtonPrefab == null || choices == null || onResolved == null)
        {
            onResolved?.Invoke(0);
            return;
        }
        _gearDiscardChoiceResolved = onResolved;
        ClearChoices();
        if (titleLabel) titleLabel.text = title;
        SetDescription(description);
        foreach (var card in choices)
        {
            if (card == null) continue;
            int id = card.Id;
            var buttonObject = Instantiate(choiceButtonPrefab, choicesContainer);
            buttonObject.name = $"GearDiscard_{id}";
            var label = buttonObject.GetComponentInChildren<TMP_Text>();
            if (label) label.text = $"{card.SuitSymbol} {card.DisplayName}  Value {card.BaseAttackValue}  #{id}";
            var button = buttonObject.GetComponent<Button>();
            if (button) button.onClick.AddListener(() => ResolveGearDiscardChoice(id));
        }
        var cancelObject = Instantiate(choiceButtonPrefab, choicesContainer);
        cancelObject.name = "GearDiscardCancel";
        var cancelLabel = cancelObject.GetComponentInChildren<TMP_Text>();
        if (cancelLabel) cancelLabel.text = "Cancel (skip this draw)";
        var cancel = cancelObject.GetComponent<Button>();
        if (cancel) cancel.onClick.AddListener(() => ResolveGearDiscardChoice(0));
        BoardPanelTransition.Show(panel);
        FinalizeChoiceList();
        panel.transform.SetAsLastSibling();
    }

    void ResolveGearDiscardChoice(int cardId)
    {
        var callback = _gearDiscardChoiceResolved;
        _gearDiscardChoiceResolved = null;
        ClearChoices();
        BoardPanelTransition.Hide(panel);
        callback?.Invoke(cardId);
    }

    void ShowDiscardSelection(int choiceIndex)
    {
        var run = RunManager.Instance;
        var cards = CardManager.Instance;
        PrepareChoicesLayout();
        PrepareDescriptionLayout();
        if (run == null || cards == null ||
            !string.IsNullOrEmpty(run.GetEventOptionUnavailableReason(choiceIndex))) return;
        _discardChoiceIndex = choiceIndex;
        var choice = run.ActiveEvent.choices[choiceIndex];
        _discardInteraction = choice.interaction;
        _selectedDiscardCardIds.Clear();
        if (titleLabel) titleLabel.text = run.ActiveEvent.title;
        if (descriptionLabel)
        {
            string prompt = _discardInteraction == RunEventChoiceInteraction.DiscardCardsForTotalValue
                ? $"Select cards with a total value of exactly {choice.requiredDiscardValue}."
                : "Choose exactly two cards to discard.";
            SetDescription($"{choice.description} {prompt}");
        }
        ClearChoices();

        var views = cards.handField != null && cards.handField.cardsHolder != null
            ? cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
            : System.Array.Empty<CardView>();
        foreach (var view in views.Where(view => view != null && view.data != null && cards.hand.Contains(view.data)))
        {
            int id = view.data.Id;
            var buttonObject = Instantiate(choiceButtonPrefab, choicesContainer);
            buttonObject.name = $"DiscardCard_{id}";
            var label = buttonObject.GetComponentInChildren<TMP_Text>();
            if (label) label.text = $"{view.data.SuitSymbol} {view.data.DisplayName}  Value {view.data.BaseAttackValue}  #{id}";
            var button = buttonObject.GetComponent<Button>();
            if (button) button.onClick.AddListener(() => ToggleDiscardCard(id));
        }

        var confirmObject = Instantiate(choiceButtonPrefab, choicesContainer);
        confirmObject.name = "ConfirmEventDiscard";
        var confirmLabel = confirmObject.GetComponentInChildren<TMP_Text>();
        if (confirmLabel)
            confirmLabel.text = _discardInteraction == RunEventChoiceInteraction.DiscardCardsForTotalValue
                ? $"Confirm Discard (0/{choice.requiredDiscardValue})" : "Confirm Discard (0/2)";
        var confirm = confirmObject.GetComponent<Button>();
        if (confirm) { confirm.interactable = false; confirm.onClick.AddListener(ConfirmDiscard); }

        var backObject = Instantiate(choiceButtonPrefab, choicesContainer);
        backObject.name = "BackToEventChoices";
        var backLabel = backObject.GetComponentInChildren<TMP_Text>();
        if (backLabel) backLabel.text = "Back";
        var back = backObject.GetComponent<Button>();
        if (back) back.onClick.AddListener(() => Show(RunManager.Instance != null ? RunManager.Instance.ActiveEvent : null));
        BoardPanelTransition.Show(panel);
        FinalizeChoiceList();
        panel.transform.SetAsLastSibling();
    }

    void ToggleDiscardCard(int id)
    {
        if (_selectedDiscardCardIds.Remove(id)) { RefreshDiscardSelection(); return; }
        if (_discardInteraction == RunEventChoiceInteraction.DiscardTwoForRandomEnhancement &&
            _selectedDiscardCardIds.Count >= 2) return;
        if (_discardInteraction == RunEventChoiceInteraction.DiscardCardsForTotalValue)
        {
            var cards = CardManager.Instance;
            var run = RunManager.Instance;
            int selectedValue = 0;
            if (cards != null)
                foreach (int selectedId in _selectedDiscardCardIds)
                    selectedValue += cards.FindOwnedCard(selectedId)?.BaseAttackValue ?? 0;
            int cardValue = cards != null ? cards.FindOwnedCard(id)?.BaseAttackValue ?? 0 : 0;
            int requiredValue = run?.ActiveEvent?.choices?[_discardChoiceIndex]?.requiredDiscardValue ?? 0;
            if (requiredValue <= 0 || selectedValue + cardValue > requiredValue) return;
        }
        _selectedDiscardCardIds.Add(id);
        RefreshDiscardSelection();
    }

    void RefreshDiscardSelection()
    {
        foreach (Transform child in choicesContainer)
        {
            if (!child.name.StartsWith("DiscardCard_")) continue;
            if (!int.TryParse(child.name.Substring("DiscardCard_".Length), out int id)) continue;
            var label = child.GetComponentInChildren<TMP_Text>();
            if (label)
            {
                string prefix = label.text.StartsWith("SELECTED  ") ? label.text.Substring("SELECTED  ".Length) : label.text;
                label.text = _selectedDiscardCardIds.Contains(id) ? $"SELECTED  {prefix}" : prefix;
            }
        }
        var run = RunManager.Instance;
        var interaction = _discardInteraction;
        int selectedValue = 0;
        int requiredValue = run?.ActiveEvent?.choices?[_discardChoiceIndex]?.requiredDiscardValue ?? 0;
        var cards = CardManager.Instance;
        if (cards != null)
            foreach (int id in _selectedDiscardCardIds)
                selectedValue += cards.FindOwnedCard(id)?.BaseAttackValue ?? 0;

        var confirmObject = choicesContainer.Find("ConfirmEventDiscard");
        if (confirmObject == null) return;
        var labelText = confirmObject.GetComponentInChildren<TMP_Text>();
        if (labelText) labelText.text = interaction == RunEventChoiceInteraction.DiscardCardsForTotalValue
            ? $"Confirm Discard ({selectedValue}/{requiredValue})"
            : $"Confirm Discard ({_selectedDiscardCardIds.Count}/2)";
        bool canConfirm = interaction == RunEventChoiceInteraction.DiscardCardsForTotalValue
            ? run != null && run.CanChooseEventDiscardForTotalValue(_discardChoiceIndex, _selectedDiscardCardIds.ToArray())
            : _selectedDiscardCardIds.Count == 2;
        var button = confirmObject.GetComponent<Button>();
        if (button) button.interactable = canConfirm;
    }

    void ConfirmDiscard()
    {
        if (_discardChoiceIndex < 0) return;
        var run = RunManager.Instance;
        bool success = run != null && (_discardInteraction == RunEventChoiceInteraction.DiscardCardsForTotalValue
            ? run.ChooseEventDiscardForTotalValue(_discardChoiceIndex, _selectedDiscardCardIds.ToArray())
            : _selectedDiscardCardIds.Count == 2 &&
                run.ChooseEventDiscardAndReward(_discardChoiceIndex, _selectedDiscardCardIds.ToArray()));
        if (success)
        {
            _discardChoiceIndex = -1;
            _selectedDiscardCardIds.Clear();
        }
    }

    void ClearChoices()
    {
        if (choicesContainer == null) return;
        var children = new List<GameObject>();
        foreach (Transform child in choicesContainer)
            if (child != null) children.Add(child.gameObject);
        foreach (var child in children)
        {
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    void Hide()
    {
        _gearDiscardChoiceResolved = null;
        _discardChoiceIndex = -1;
        _discardInteraction = RunEventChoiceInteraction.Immediate;
        _selectedDiscardCardIds.Clear();
        BoardPanelTransition.Hide(panel);
    }
}
