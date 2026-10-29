using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Visual-only indicator for the remaining draw deck (HUD slice S11, visual-design 3b). The top card
/// back sits over up to three shadow backs (the card back sprite tinted inkPrint) at 2/4/6px; the pile
/// thins as the deck runs down. The count prints Courier Prime Bold 28 ink on a paper chip disc at the
/// pile's lower right, over a 3px shadow. Hover lifts the top card 4px; press drops the whole stack 2px
/// and collapses the shadows to 2px; focus shows the ring. The click itself belongs to whatever already
/// listens on <see cref="deckButton"/> (the detail opens beside the pile); this adds no listeners.
/// </summary>
public sealed class DrawDeckStackUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [Header("References")]
    public RectTransform stackRoot;
    [Tooltip("The existing pile button. This component does not add click listeners.")]
    public Button deckButton;
    public Image cardBackImage;
    public Sprite redCardBack;
    public TMP_Text countLabel;

    const int MaxLayers = 4;
    /// <summary>Shadow back offsets (px, down-right) for layers 1-3; layer 0 is the top card.</summary>
    public static readonly float[] ShadowOffsets = { 0f, 2f, 4f, 6f };
    public const float HoverLift = 4f, PressDrop = 2f, PressedShadow = 2f, CardInset = 5f;
    public const float CountSize = 28f, DiscShadow = 3f;
    public const string DiscName = "Count Disc", DiscShadowName = "Count Disc Shadow", FocusRingName = "Draw Pile Focus Ring";
    const float FocusRingOutset = 4f;

    readonly Image[] _layers = new Image[MaxLayers];
    CardManager _cards;
    Image _disc, _ring;
    CutSheetShadow _discShadow;
    bool _hover, _pressed, _focus;

    /// <summary>The last remaining draw-deck count rendered by Refresh.</summary>
    public int DisplayedCount { get; private set; }
    /// <summary>Number of opaque card layers in the last refresh; zero shows only the faded empty ghost.</summary>
    public int DisplayedDepth { get; private set; }
    public int LayerCount => MaxLayers;
    /// <summary>Layer 0 is the top card; 1-3 are the shadow backs.</summary>
    public Image Layer(int index) => index >= 0 && index < MaxLayers ? _layers[index] : null;
    public Image CountDisc => _disc;
    public CutSheetShadow CountDiscShadow => _discShadow;
    public bool FocusRingShown => _ring && _ring.enabled;
    public bool Hovered => _hover;
    public bool Pressed => _pressed;

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
        _hover = _pressed = _focus = false;
        ApplyPose();
    }

    // The HUD's hierarchy styling may retouch the count label after Refresh; restore only on drift.
    void LateUpdate()
    {
        StyleCount();
        RetireBacking();
    }

    /// <summary>True while the old neo-punk backing (the button's frame or opaque root image) still draws.</summary>
    public bool BackingDraws
    {
        get
        {
            var host = deckButton ? deckButton.transform : transform;
            if (host.TryGetComponent(out Image root) && root.enabled && root.color.a > 0f) return true;
            foreach (Transform child in host)
                if (child.name == BackingFrameName && child.gameObject.activeSelf) return true;
            return false;
        }
    }

    const string BackingFrameName = "Editorial Frame";
    // The legacy stylers on the pile button, looked up once (no per-frame GetComponents).
    readonly System.Collections.Generic.List<EditorialButton> _stylers = new();
    Transform _stylersHost;

    // S11: the pile sits on the board itself. The button's legacy styler and its frame (a charcoal
    // rect) are switched off; the root image goes clear but keeps its rect and raycast. On drift only.
    void RetireBacking()
    {
        var host = deckButton ? deckButton.transform : transform;
        if (!host) return;
        if (_stylersHost != host)
        {
            _stylersHost = host;
            host.GetComponents(_stylers);
        }
        for (int i = 0; i < _stylers.Count; i++)
            if (_stylers[i] && _stylers[i].enabled) _stylers[i].enabled = false;
        if (host.TryGetComponent(out Image root) && root.color.a > 0f) root.color = Color.clear;
        for (int i = 0; i < host.childCount; i++)
        {
            var child = host.GetChild(i);
            if (child.name == BackingFrameName && child.gameObject.activeSelf) child.gameObject.SetActive(false);
        }
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
        string countText = CountText(count);
        if (countLabel != null && countLabel.text != countText) countLabel.text = countText;
        EnsureDisc();
        StyleCount();
        RetireBacking();
        if (stackRoot == null) return;

        EnsureLayers();
        EnsureRing();
        var sprite = redCardBack != null ? redCardBack : cardBackImage != null ? cardBackImage.sprite : null;
        var theme = DealbreakUIStyle.Theme;
        Color ink = theme ? theme.inkPrint : CutSheetShadow.FallbackTint;
        for (int i = 0; i < MaxLayers; i++)
        {
            var image = _layers[i];
            if (image == null) continue;
            if (image.sprite != sprite) image.sprite = sprite;
            if (image.type != Image.Type.Simple) image.type = Image.Type.Simple;
            if (!image.preserveAspect) image.preserveAspect = true;
            // Keep a faint card outline when the draw pile is exhausted.
            bool active = i < DisplayedDepth || (count == 0 && i == 0);
            if (image.gameObject.activeSelf != active) image.gameObject.SetActive(active);
            Color color = count == 0
                ? new Color(0.75f, 0.18f, 0.18f, 0.22f)
                : i == 0
                    ? sprite == null ? new Color(0.65f, 0.08f, 0.1f, 1f) : Color.white
                    : ink;
            if (image.color != color) image.color = color;
        }
        ApplyPose();
        // The button stays interactable even at zero cards, so the empty pile's detail still opens.
    }

    static string CountText(int count) => count.ToString();

    /// <summary>Pointer hover (or a test) lifts the top card.</summary>
    public void SetHovered(bool hovered) { _hover = hovered; if (!hovered) _pressed = false; ApplyPose(); }
    public void SetPressed(bool pressed) { _pressed = pressed; ApplyPose(); }
    public void SetFocused(bool focused) { _focus = focused; ApplyPose(); }

    public void OnPointerEnter(PointerEventData _) => SetHovered(true);
    public void OnPointerExit(PointerEventData _) => SetHovered(false);
    public void OnPointerDown(PointerEventData _) => SetPressed(true);
    public void OnPointerUp(PointerEventData _) => SetPressed(false);
    public void OnSelect(BaseEventData _) => SetFocused(true);
    public void OnDeselect(BaseEventData _) { _focus = false; _pressed = false; ApplyPose(); }

    /// <summary>The top card's offset from rest: +4 up on hover, -2 when pressed.</summary>
    public Vector2 TopCardOffset => _pressed ? new Vector2(0f, -PressDrop) : _hover ? new Vector2(0f, HoverLift) : Vector2.zero;
    /// <summary>Shadow layer <paramref name="index"/>'s depth below the top card's rest (2/4/6, or 2 pressed).</summary>
    public float ShadowDepth(int index) => index <= 0 || index >= MaxLayers ? 0f : _pressed ? PressedShadow : ShadowOffsets[index];

    void ApplyPose()
    {
        float drop = _pressed ? -PressDrop : 0f;
        for (int i = 0; i < MaxLayers; i++)
        {
            var image = _layers[i];
            if (image == null) continue;
            var rect = image.rectTransform;
            Vector2 offset = i == 0 ? TopCardOffset : new Vector2(ShadowDepth(i), -ShadowDepth(i) + drop);
            var min = new Vector2(CardInset, CardInset) + offset;
            var max = new Vector2(-CardInset, -CardInset) + offset;
            if (rect.offsetMin != min) rect.offsetMin = min;
            if (rect.offsetMax != max) rect.offsetMax = max;
        }
        // Never builds here: OnDisable runs this while the object may be being destroyed.
        bool ring = _focus && deckButton != null && deckButton.IsInteractable();
        if (_ring && _ring.enabled != ring) _ring.enabled = ring;
    }

    void EnsureLayers()
    {
        for (int i = 0; i < MaxLayers; i++)
        {
            if (_layers[i] != null) continue;
            var layer = new GameObject($"Draw Deck Card {i + 1}", typeof(RectTransform), typeof(Image));
            layer.layer = stackRoot.gameObject.layer;
            layer.transform.SetParent(stackRoot, false);
            // Layer 0 (the top card) is created first and ends up last, drawn over the shadows.
            layer.transform.SetAsFirstSibling();
            var rect = (RectTransform)layer.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            _layers[i] = layer.GetComponent<Image>();
            _layers[i].raycastTarget = false;
        }
    }

    // The disc sits on the count label's authored rect, directly behind it, over its own 3px shadow.
    void EnsureDisc()
    {
        if (_disc != null || countLabel == null || countLabel.transform.parent == null) return;
        var label = countLabel.rectTransform;
        var parent = label.parent;
        var existing = parent.Find(DiscName);
        RectTransform rect;
        if (existing) rect = (RectTransform)existing;
        else
        {
            var item = new GameObject(DiscName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.layer = label.gameObject.layer;
            rect = (RectTransform)item.transform;
            rect.SetParent(parent, false);
        }
        rect.SetSiblingIndex(label.GetSiblingIndex());
        rect.anchorMin = label.anchorMin;
        rect.anchorMax = label.anchorMax;
        rect.pivot = label.pivot;
        rect.anchoredPosition = label.anchoredPosition;
        rect.sizeDelta = label.sizeDelta;
        // A minimal setup (no theme, a non-UI parent) skips the dressing rather than throwing.
        if (!rect.TryGetComponent(out _disc) || !_disc) { _disc = null; return; }
        _disc.raycastTarget = false;
        var theme = DealbreakUIStyle.Theme;
        _disc.sprite = theme ? theme.chipSprite : null;
        _disc.type = Image.Type.Simple;
        _disc.preserveAspect = true;
        _disc.color = theme ? theme.paper : Color.white;
        var shadowObject = rect.Find(CutSheetShadow.ObjectName);
        _discShadow = shadowObject && shadowObject.TryGetComponent(out CutSheetShadow found) ? found : CutSheetShadow.Create(rect);
        if (!_discShadow || !_discShadow.Image) return;
        // A child draws over its parent, so the shadow is a sibling placed just behind the disc.
        var shadowRect = (RectTransform)_discShadow.transform;
        shadowRect.SetParent(parent, false);
        shadowRect.name = DiscShadowName;
        shadowRect.SetSiblingIndex(rect.GetSiblingIndex());
        _discShadow.Match(_disc, CutSheetShadow.Tint, 0f);
        _discShadow.Image.preserveAspect = true;
        // SetDepth stretches over the parent; offset it from the disc's own rect instead.
        shadowRect.anchorMin = rect.anchorMin;
        shadowRect.anchorMax = rect.anchorMax;
        shadowRect.pivot = rect.pivot;
        shadowRect.sizeDelta = rect.sizeDelta;
        shadowRect.anchoredPosition = rect.anchoredPosition + new Vector2(DiscShadow, -DiscShadow);
        _discShadow.Image.enabled = true;
    }

    /// <summary>The disc shadow's offset from the disc, in px (3 down-right).</summary>
    public Vector2 DiscShadowOffset => _disc && _discShadow
        ? ((RectTransform)_discShadow.transform).anchoredPosition - _disc.rectTransform.anchoredPosition
        : Vector2.zero;

    void StyleCount()
    {
        if (countLabel == null) return;
        var theme = DealbreakUIStyle.Theme;
        if (!theme) return;
        if (theme.numericFont && countLabel.font != theme.numericFont) countLabel.font = theme.numericFont;
        // A material left from another face or an outline preset draws nothing on the disc.
        if (countLabel.font && countLabel.fontSharedMaterial != countLabel.font.material) countLabel.fontSharedMaterial = countLabel.font.material;
        if (!Mathf.Approximately(countLabel.fontSize, CountSize)) countLabel.fontSize = CountSize;
        if (countLabel.characterSpacing != 0) countLabel.characterSpacing = 0;
        if (countLabel.margin != Vector4.zero) countLabel.margin = Vector4.zero;
        if (countLabel.textWrappingMode != TextWrappingModes.NoWrap) countLabel.textWrappingMode = TextWrappingModes.NoWrap;
        if (countLabel.overflowMode != TextOverflowModes.Overflow) countLabel.overflowMode = TextOverflowModes.Overflow;
        if (!countLabel.enabled) countLabel.enabled = true;
        if (!countLabel.gameObject.activeSelf) countLabel.gameObject.SetActive(true);
        // The number prints over its disc.
        if (_disc && _disc.transform.parent == countLabel.transform.parent &&
            countLabel.transform.GetSiblingIndex() < _disc.transform.GetSiblingIndex())
            countLabel.transform.SetSiblingIndex(_disc.transform.GetSiblingIndex());
        if (countLabel.enableAutoSizing) countLabel.enableAutoSizing = false;
        if (countLabel.color != theme.inkPrint) countLabel.color = theme.inkPrint;
        if (countLabel.fontStyle != FontStyles.Bold) countLabel.fontStyle = FontStyles.Bold;
        if (countLabel.alignment != TextAlignmentOptions.Center) countLabel.alignment = TextAlignmentOptions.Center;
        if (countLabel.raycastTarget) countLabel.raycastTarget = false;
    }

    void EnsureRing()
    {
        if (_ring || stackRoot == null) return;
        var existing = stackRoot.Find(FocusRingName);
        _ring = existing && existing.TryGetComponent<Image>(out var found)
            ? found : CutSheetHoverRing.CreateRing(stackRoot, FocusRingName, FocusRingOutset);
        if (_ring) { _ring.raycastTarget = false; _ring.enabled = false; }
    }
}
