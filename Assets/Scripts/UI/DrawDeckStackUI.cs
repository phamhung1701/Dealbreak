using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Visual-only indicator for the remaining draw deck. The button's click behavior belongs to DeckViewerUI.</summary>
public sealed class DrawDeckStackUI : MonoBehaviour
{
    [Header("References")]
    public RectTransform stackRoot;
    [Tooltip("The existing DeckViewerUI toggle button. This component does not add click listeners.")]
    public Button deckButton;
    public Image cardBackImage;
    public Sprite redCardBack;
    public TMP_Text countLabel;

    const int MaxLayers = 4;
    readonly Image[] _layers = new Image[MaxLayers];
    CardManager _cards;

    /// <summary>The last remaining draw-deck count rendered by Refresh.</summary>
    public int DisplayedCount { get; private set; }
    /// <summary>Number of opaque card layers in the last refresh; zero shows only the faded empty ghost.</summary>
    public int DisplayedDepth { get; private set; }

    /// <summary>Visual depth: 1–9 cards show one layer, 10–19 two, 20–29 three, 30+ four.</summary>
    public static int GetDepthForCount(int count) => count <= 0 ? 0 : Mathf.Min(MaxLayers, 1 + count / 10);

    void OnEnable()
    {
        // Do not instantiate runtime card layers into the saved scene in Edit Mode.
        if (!Application.isPlaying) return;
        // A manual Refresh while disabled must not prevent re-subscribing on enable.
        if (_cards != null) _cards.OnDeckChanged -= Refresh;
        _cards = null;
        Refresh();
    }

    void Start()
    {
        // Also handles a manager initialized after this component's OnEnable.
        Refresh();
    }

    void OnDisable()
    {
        if (_cards != null)
            _cards.OnDeckChanged -= Refresh;
        _cards = null;
    }

    public void Refresh()
    {
        var cards = CardManager.Instance;
        if (_cards != cards)
        {
            if (_cards != null) _cards.OnDeckChanged -= Refresh;
            _cards = cards;
            if (_cards != null && isActiveAndEnabled) _cards.OnDeckChanged += Refresh;
        }

        int count = cards != null ? cards.deck.Count : 0;
        DisplayedCount = count;
        DisplayedDepth = GetDepthForCount(count);
        if (countLabel != null) countLabel.text = count.ToString();
        if (stackRoot == null) return;

        EnsureLayers();
        var sprite = redCardBack != null ? redCardBack : cardBackImage != null ? cardBackImage.sprite : null;
        for (int i = 0; i < MaxLayers; i++)
        {
            var image = _layers[i];
            if (image == null) continue;
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            // Keep a faint card outline when the draw pile is exhausted.
            image.gameObject.SetActive(i < DisplayedDepth || (count == 0 && i == 0));
            image.color = count == 0
                ? new Color(0.75f, 0.18f, 0.18f, 0.22f)
                : sprite == null
                    ? new Color(0.65f - i * 0.08f, 0.08f, 0.1f, 1f)
                    : new Color(1f - i * 0.09f, 1f - i * 0.09f, 1f - i * 0.09f, 1f);
        }
        // The existing button stays interactable even at zero cards, so DeckViewerUI can show the empty pile.
    }

    void EnsureLayers()
    {
        for (int i = 0; i < MaxLayers; i++)
        {
            if (_layers[i] != null) continue;
            var layer = new GameObject($"Draw Deck Card {i + 1}", typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(stackRoot, false);
            layer.transform.SetAsFirstSibling();
            var rect = (RectTransform)layer.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(5f - i * 2f, 5f + i * 3f);
            rect.offsetMax = new Vector2(-5f - i * 2f, -5f + i * 3f);
            _layers[i] = layer.GetComponent<Image>();
            _layers[i].raycastTarget = false;
        }
    }
}
