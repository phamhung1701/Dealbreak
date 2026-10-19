using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "DeckViewerUI")]
public class DeckViewerUI : MonoBehaviour
{
    [Header("References")]
    public GameObject panel;
    public TMP_Text deckText;
    public TMP_Text discardText;
    public Button toggleButton;
    public Button closeButton;

    [Header("Runtime card presentation")]
    public CardView cardPrefab;
    [Min(1)] public int cardsPerRow = 5;
    [Min(0f)] public float horizontalSpacing = 12f;
    [Min(0f)] public float verticalSpacing = 16f;
    [Range(0.1f, 1f)] public float cardScale = 0.62f;

    readonly List<GameObject> _cardSnapshots = new();
    RectTransform _content;
    VerticalLayoutGroup _verticalLayout;
    ContentSizeFitter _contentFitter;
    ScrollRect _scrollRect;
    float _lastLayoutWidth = -1f;

    bool _isOpen;

    void OnEnable()
    {
        ResolveVisualReferences();
        if (toggleButton) toggleButton.onClick.AddListener(Toggle);
        if (closeButton) closeButton.onClick.AddListener(Close);
        if (CardManager.Instance != null)
            CardManager.Instance.OnDeckChanged += Refresh;
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += CloseForRunStart;
            RunManager.Instance.OnShowPathScreen += Close;
            RunManager.Instance.OnHidePathScreen += Close;
            RunManager.Instance.OnShowShop += Close;
            RunManager.Instance.OnShowEvent += CloseForEvent;
            RunManager.Instance.OnShowUpgrade += Close;
            RunManager.Instance.OnRunCompleted += Close;
        }
        if (CombatManager.Instance != null)
            CombatManager.Instance.OnEncounterResult += CloseForEncounterResult;
        Close();
    }

    void OnDisable()
    {
        if (toggleButton) toggleButton.onClick.RemoveListener(Toggle);
        if (closeButton) closeButton.onClick.RemoveListener(Close);
        if (CardManager.TryGetInstance(out var cards))
            cards.OnDeckChanged -= Refresh;
        ClearSnapshots();
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= CloseForRunStart;
            RunManager.Instance.OnShowPathScreen -= Close;
            RunManager.Instance.OnHidePathScreen -= Close;
            RunManager.Instance.OnShowShop -= Close;
            RunManager.Instance.OnShowEvent -= CloseForEvent;
            RunManager.Instance.OnShowUpgrade -= Close;
            RunManager.Instance.OnRunCompleted -= Close;
        }
        if (CombatManager.Instance != null)
            CombatManager.Instance.OnEncounterResult -= CloseForEncounterResult;
    }

    void OnRectTransformDimensionsChange()
    {
        if (!_isOpen || _content == null || Mathf.Abs(_content.rect.width - _lastLayoutWidth) < 0.5f) return;
        var cards = CardManager.Instance;
        if (cards != null) RebuildSnapshots(cards.deck);
    }

    void Toggle()
    {
        if (_isOpen)
        {
            // Closing through the toggle must release snapshots exactly like Close().
            Close();
            return;
        }
        _isOpen = true;
        if (panel) BoardPanelTransition.FadeIn(panel);
        Refresh();
        if (panel) panel.transform.SetAsLastSibling();
    }

    void Close()
    {
        _isOpen = false;
        if (panel) panel.SetActive(false);
        ClearSnapshots();
    }

    void Refresh()
    {
        var cards = CardManager.Instance;
        if (cards == null) return;
        ResolveVisualReferences();
        if (deckText)
        {
            deckText.text = cards.deck.Count == 0
                ? $"REMAINING DRAW DECK  •  0\nNo cards remaining in the draw deck"
                : $"REMAINING DRAW DECK  •  {cards.deck.Count}";
            deckText.gameObject.SetActive(true);
            PositionCountHeader();
        }
        if (discardText) discardText.gameObject.SetActive(false);
        if (_isOpen) RebuildSnapshots(cards.deck);
    }

    void ResolveVisualReferences()
    {
        if (deckText != null && _content == null)
        {
            _content = deckText.transform.parent as RectTransform;
            if (_content != null)
            {
                _verticalLayout = _content.GetComponent<VerticalLayoutGroup>();
                _contentFitter = _content.GetComponent<ContentSizeFitter>();
                _scrollRect = _content.parent != null ? _content.parent.GetComponent<ScrollRect>() : null;
            }
        }
        if (cardPrefab == null && CardManager.Instance != null)
            cardPrefab = CardManager.Instance.cardPrefab;
    }

    void PositionCountHeader()
    {
        if (deckText == null || _content == null) return;
        var header = deckText.rectTransform;
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta = new Vector2(0f, 34f);
        deckText.alignment = TextAlignmentOptions.Center;
    }

    void RebuildSnapshots(IReadOnlyList<CardInstance> cards)
    {
        ClearSnapshots();
        if (_content == null || cardPrefab == null) return;
        if (_verticalLayout) _verticalLayout.enabled = false;
        if (_contentFitter) _contentFitter.enabled = false;

        float width = _content.rect.width;
        if (width <= 0f && _scrollRect != null) width = _scrollRect.viewport.rect.width;
        _lastLayoutWidth = width;
        float effectiveScale = width > 0f ? Mathf.Min(cardScale, width / 136f) : cardScale;
        float cardWidth = 136f * effectiveScale;
        float cardHeight = 194f * effectiveScale;
        int columns = Mathf.Clamp(Mathf.FloorToInt((width + horizontalSpacing) / (cardWidth + horizontalSpacing)), 1, Mathf.Max(1, cardsPerRow));
        float rowHeight = cardHeight + verticalSpacing;
        int rows = (cards.Count + columns - 1) / columns;
        float groupWidth = columns * cardWidth + (columns - 1) * horizontalSpacing;
        float startX = -groupWidth * 0.5f + cardWidth * 0.5f;
        _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(_scrollRect != null ? _scrollRect.viewport.rect.height : 0, rows * rowHeight + 42f));

        for (int i = 0; i < cards.Count; i++)
        {
            var snapshot = Instantiate(cardPrefab, _content, false);
            snapshot.name = $"DeckCardSnapshot_{i}";
            snapshot.enabled = false;
            snapshot.homeField = null;
            snapshot.data = cards[i];
            snapshot.RefreshContent();
            var rect = (RectTransform)snapshot.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(136f, 194f);
            rect.localScale = Vector3.one * effectiveScale;
            int row = i / columns;
            int column = i % columns;
            float centeredColumn = column - (Mathf.Min(columns, cards.Count - row * columns) - 1) * 0.5f;
            rect.anchoredPosition = new Vector2(startX + column * (cardWidth + horizontalSpacing), -42f - row * rowHeight);
            rect.localRotation = Quaternion.Euler(0f, 0f, centeredColumn * -2f);
            foreach (var canvas in snapshot.GetComponentsInChildren<Canvas>(true))
                canvas.overrideSorting = false;
            // Snapshot artwork must join the clipped modal Canvas. The hand's nested canvases
            // sort independently for live interaction; leaving that override on puts their order
            // behind this higher-sorting modal Canvas and hides every card behind the backing.
            // Preserve each nested Canvas for drawing, but disable snapshot input below.
            foreach (var graphic in snapshot.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            var raycaster = snapshot.GetComponentInChildren<GraphicRaycaster>(true);
            if (raycaster) raycaster.enabled = false;
            var canvasGroup = snapshot.canvasGroup;
            if (canvasGroup) { canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }
            if (ContextualTooltipUI.Instance)
            {
                if (snapshot.statsPlate) snapshot.statsPlate.SetActive(false);
                if (snapshot.nameLabel) snapshot.nameLabel.enabled = false;
                // Inspect through a separate control; the card snapshot remains incapable of play or drag.
                var inspection = new GameObject($"DeckInspect_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ContextualInfoTrigger));
                var wrapper = (RectTransform)inspection.transform;
                wrapper.SetParent(_content, false);
                wrapper.anchorMin = rect.anchorMin; wrapper.anchorMax = rect.anchorMax; wrapper.pivot = rect.pivot;
                wrapper.anchoredPosition = rect.anchoredPosition;
                wrapper.sizeDelta = rect.sizeDelta * effectiveScale;
                inspection.GetComponent<Image>().color = Color.clear;
                inspection.AddComponent<EditorialSurface>().treatment = EditorialSurface.Treatment.Open;
                inspection.GetComponent<Button>().transition = Selectable.Transition.None;
                var detail = inspection.GetComponent<ContextualInfoTrigger>();
                detail.pinOnClick = true;
                detail.describe = () => ContextualTooltipUI.TryDescribe(snapshot, out var info) ? info : null;
                rect.SetParent(wrapper, false);
                rect.anchoredPosition = Vector2.zero;
                _cardSnapshots.Add(inspection);
            }
            else _cardSnapshots.Add(snapshot.gameObject);
        }
        if (_scrollRect) _scrollRect.verticalNormalizedPosition = 1f;
    }

    void ClearSnapshots()
    {
        for (int i = 0; i < _cardSnapshots.Count; i++)
        {
            var snapshot = _cardSnapshots[i];
            if (!snapshot) continue;
            snapshot.SetActive(false);
            if (Application.isPlaying) Destroy(snapshot);
            else DestroyImmediate(snapshot);
        }
        _cardSnapshots.Clear();
    }

    void CloseForRunStart(string _) => Close();
    void CloseForEvent(RunEventDefinition _) => Close();
    void CloseForEncounterResult(EncounterResult _) => Close();

}
