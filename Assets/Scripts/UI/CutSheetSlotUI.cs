using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The Cut Sheet dressing of one header inventory slot, shared by the Gear and Trick rows
/// (visual-design 3b). The slot root stays untilted and keeps the hit area, Button, CanvasPin,
/// tooltip anchor and drop-target rect; it draws nothing itself. Under it:
/// the sticker's shadow (the icon's own alpha tinted ink), the tilted Face carrying the item art and
/// glyph, the cut-line ring (hover, focus, selected), the dashed outline of an empty capacity slot,
/// and an untilted Badges parent for the tier, stack and charge discs.
/// Presentation only; motion runs in LateUpdate on unscaled time and settles instantly under
/// Reduce Motion.
/// </summary>
public sealed class CutSheetSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public const string FaceName = "Face", ShadowName = "Sticker Shadow", RingName = "Slot Ring",
        DashedName = "Empty Slot", BadgesName = "Badges";
    public const int BadgeCount = 2;
    // Every dressing graphic stays inside the slot rect: the Gear row's viewport masks (RectMask2D)
    // and the slots are pinned, so an overhang is clipped. Discs sit 4px in from the corner, which
    // also keeps their 3px shadow inside; the ring is drawn on the slot's own edge.
    public const float DiscSize = 32, DiscText = 20, RingOutset = 0, DiscInset = 4;
    // The item art's inset in the slot, shared with ItemIconUI so the shadow matches the art.
    public static readonly Vector2 ArtMin = new(.08f, .08f), ArtMax = new(.92f, .92f);
    const float SlapSeconds = .2f, SlapScale = .15f, HopScale = .2f;

    RectTransform _face, _shadowHolder, _badges;
    CutSheetShadow _shadow;
    Image _ring, _dashed;
    readonly RectTransform[] _discs = new RectTransform[BadgeCount];
    readonly TMP_Text[] _discLabels = new TMP_Text[BadgeCount];
    readonly int[] _discValues = new int[BadgeCount];
    readonly bool[] _discCharges = new bool[BadgeCount];
    int _index = int.MinValue;
    bool _hovered, _focused, _selected, _occupied;
    // A Trick's detail is the tooltip's pinned source; while it stays open the ring stays.
    MonoBehaviour _pinSource;
    float _hopStart = -1f, _slapStart = -1f;
    Image _art;

    public RectTransform Face => _face;
    public RectTransform Badges => _badges;
    public CutSheetShadow Shadow => _shadow;
    public Image Ring => _ring;
    public Image Dashed => _dashed;
    public int SlotIndex => _index;
    public bool RingShown => _ring && _ring.enabled;
    public bool Selected { get => _selected; set { _selected = value; SyncRing(); } }
    public bool IsHopping => _hopStart >= 0f;
    public bool IsSlapping => _slapStart >= 0f;

    /// <summary>The dressing on <paramref name="root"/>, built once; <paramref name="glyph"/> moves onto the face.</summary>
    public static CutSheetSlotUI Dress(Transform root, int slotIndex, TMP_Text glyph)
    {
        if (!root) return null;
        if (!root.TryGetComponent(out CutSheetSlotUI slot)) slot = root.gameObject.AddComponent<CutSheetSlotUI>();
        slot.Build();
        slot.SetIndex(slotIndex);
        if (glyph && glyph.transform.parent != slot._face)
        {
            glyph.transform.SetParent(slot._face, false);
            glyph.raycastTarget = false;
            // Inset like the art, so the tilted glyph never crosses the slot's (masked) edge.
            var rect = glyph.rectTransform;
            rect.anchorMin = ArtMin; rect.anchorMax = ArtMax;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        return slot;
    }

    /// <summary>Where a slot's item art and glyph go: its face when dressed, else the slot itself.</summary>
    public static Transform ArtParent(Transform slot) =>
        slot && slot.TryGetComponent(out CutSheetSlotUI dressed) && dressed._face ? dressed._face : slot;

    void Build()
    {
        if (_face) return;
        // A slot dressed in an earlier session (or the scene) reuses its parts by name.
        _shadowHolder = Part(ShadowName, false);
        _face = Part(FaceName, false);
        _ring = PartImage(RingName);
        _dashed = PartImage(DashedName);
        _badges = Part(BadgesName, false);
        if (!_shadowHolder.TryGetComponent(out _shadow)) _shadow = CutSheetShadow.Create(_shadowHolder);
        _shadowHolder.anchorMin = ArtMin; _shadowHolder.anchorMax = ArtMax;
        _shadowHolder.offsetMin = _shadowHolder.offsetMax = Vector2.zero;
        if (!_shadow.transform.IsChildOf(_shadowHolder)) _shadow.transform.SetParent(_shadowHolder, false);
        _shadowHolder.SetSiblingIndex(0);
        _face.SetSiblingIndex(1);
        Stretch(_face, Vector2.zero);
        Stretch(_badges, Vector2.zero);

        var theme = DealbreakUIStyle.Theme;
        Color paper = theme ? theme.paper : new Color(.94f, .89f, .8f);
        _ring.sprite = theme ? theme.stickerSprite : null;
        _ring.type = Image.Type.Sliced;
        _ring.fillCenter = false;
        _ring.color = paper;
        Stretch(_ring.rectTransform, Vector2.zero);
        _ring.rectTransform.offsetMin = -Vector2.one * RingOutset;
        _ring.rectTransform.offsetMax = Vector2.one * RingOutset;
        _ring.enabled = false;
        _dashed.sprite = theme ? theme.slotDashedSprite : null;
        _dashed.type = Image.Type.Tiled;
        _dashed.color = paper;
        Stretch(_dashed.rectTransform, Vector2.zero);
        _dashed.enabled = false;
        _badges.SetAsLastSibling();
        for (int i = 0; i < BadgeCount; i++) BuildDisc(i, theme, paper);
    }

    void BuildDisc(int index, DealbreakUITheme theme, Color paper)
    {
        var disc = Part($"Disc {index}", false, _badges);
        // Disc 0 sits top-right (tier, charges), disc 1 bottom-right (stack), both overhanging the slot.
        var corner = index == 0 ? new Vector2(1, 1) : new Vector2(1, 0);
        disc.anchorMin = disc.anchorMax = disc.pivot = corner;
        disc.sizeDelta = new Vector2(DiscSize, DiscSize);
        disc.anchoredPosition = new Vector2(-DiscInset, index == 0 ? -DiscInset : DiscInset);
        var faceRect = Part("Disc Face", true, disc);
        var face = faceRect.GetComponent<Image>();
        face.sprite = theme ? theme.chipSprite : null;
        face.type = Image.Type.Simple;
        face.color = paper;
        Stretch(faceRect, Vector2.zero);
        CutSheetShadow shadow = null;
        foreach (Transform child in disc) if (child.TryGetComponent(out shadow)) break;
        if (!shadow) shadow = CutSheetShadow.Create(disc);
        shadow.transform.SetSiblingIndex(0);
        shadow.Match(face, CutSheetShadow.Tint, CutSheetMetrics.ShadowSlot);
        var labelRect = disc.Find("Disc Number") as RectTransform;
        TMP_Text label;
        if (!labelRect)
        {
            var labelObject = new GameObject("Disc Number", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.layer = gameObject.layer;
            labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(disc, false);
        }
        labelRect.TryGetComponent(out label);
        Stretch(labelRect, Vector2.zero);
        StyleDisc(label);
        _discs[index] = disc;
        _discLabels[index] = label;
        _discValues[index] = int.MinValue;
        disc.gameObject.SetActive(false);
    }

    // Disc numbers: Courier Prime Bold 20 ink on the paper disc, no outline. The dressing owns them:
    // ApplyHierarchy skips dressed slots, the slot's EditorialButton is switched off, and LateUpdate
    // puts the style back should anything else write it.
    static void StyleDisc(TMP_Text label)
    {
        var theme = DealbreakUIStyle.Theme;
        if (!label || !theme) return;
        if (theme.numericFont && label.font != theme.numericFont) label.font = theme.numericFont;
        if (label.font && label.fontSharedMaterial != label.font.material) label.fontSharedMaterial = label.font.material;
        if (label.color != theme.inkPrint) label.color = theme.inkPrint;
        if (!Mathf.Approximately(label.fontSize, DiscText)) label.fontSize = DiscText;
        if (label.enableAutoSizing) label.enableAutoSizing = false;
        if (label.fontStyle != FontStyles.Normal) label.fontStyle = FontStyles.Normal;
        if (label.characterSpacing != 0) label.characterSpacing = 0;
        if (label.margin != Vector4.zero) label.margin = Vector4.zero;
        if (label.alignment != TextAlignmentOptions.Center) label.alignment = TextAlignmentOptions.Center;
        if (label.textWrappingMode != TextWrappingModes.NoWrap) label.textWrappingMode = TextWrappingModes.NoWrap;
        if (label.overflowMode != TextOverflowModes.Overflow) label.overflowMode = TextOverflowModes.Overflow;
        if (label.raycastTarget) label.raycastTarget = false;
    }

    static bool DiscStyled(TMP_Text label, DealbreakUITheme theme) =>
        label.color == theme.inkPrint && (!theme.numericFont || label.font == theme.numericFont) &&
        (!label.font || label.fontSharedMaterial == label.font.material);

    public TMP_Text BadgeLabel(int index) => index >= 0 && index < BadgeCount ? _discLabels[index] : null;

    RectTransform Part(string name, bool image, Transform parent = null)
    {
        parent = parent ? parent : transform;
        var existing = parent.Find(name) as RectTransform;
        if (existing) return existing;
        var item = image
            ? new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            : new GameObject(name, typeof(RectTransform));
        item.layer = gameObject.layer;
        var rect = (RectTransform)item.transform;
        rect.SetParent(parent, false);
        if (image) item.GetComponent<Image>().raycastTarget = false;
        return rect;
    }

    Image PartImage(string name)
    {
        var rect = Part(name, true);
        rect.TryGetComponent(out Image image);
        image.raycastTarget = false;
        return image;
    }

    static void Stretch(RectTransform rect, Vector2 offset)
    {
        if (rect.anchorMin != Vector2.zero) rect.anchorMin = Vector2.zero;
        if (rect.anchorMax != Vector2.one) rect.anchorMax = Vector2.one;
        if (rect.offsetMin != offset) rect.offsetMin = offset;
        if (rect.offsetMax != offset) rect.offsetMax = offset;
    }

    /// <summary>The slot's place in its row; it picks the rest tilt from the fixed table.</summary>
    public void SetIndex(int slotIndex)
    {
        if (_index == slotIndex) return;
        _index = slotIndex;
        var rest = Quaternion.Euler(0, 0, CutSheetMetrics.SlotTilt(slotIndex));
        _face.localRotation = rest;
        _shadowHolder.localRotation = rest;
    }

    /// <summary>
    /// Shows the slot as holding an item, or empty: the dashed outline only for an empty slot inside
    /// the row's capacity. The sticker shadow follows the item art (none for a glyph-only item).
    /// </summary>
    public void SetContents(bool occupied, bool emptyCapacitySlot)
    {
        _occupied = occupied;
        HideRootGraphic();
        bool dashed = !occupied && emptyCapacitySlot && _dashed.sprite;
        if (_dashed.enabled != dashed) _dashed.enabled = dashed;
        if (!occupied)
        {
            _hovered = false;
            _selected = false;
            _pinSource = null;
            for (int i = 0; i < BadgeCount; i++) SetBadge(i, false, 0, false);
        }
        SyncShadow();
        SyncRing();
    }

    /// <summary>The Trick detail whose pinned tooltip keeps the ring up.</summary>
    public void SetPinSource(MonoBehaviour source) => _pinSource = source;

    /// <summary>A disc number: tier or charges on disc 0, stack (×n) on disc 1. Writes only on change.</summary>
    public void SetBadge(int index, bool shown, int value, bool stack)
    {
        if (index < 0 || index >= BadgeCount || !_discs[index]) return;
        if (_discs[index].gameObject.activeSelf != shown) _discs[index].gameObject.SetActive(shown);
        if (!shown || (_discValues[index] == value && _discCharges[index] == stack)) return;
        _discValues[index] = value;
        _discCharges[index] = stack;
        if (stack) _discLabels[index].SetText("×{0:0}", value);
        else _discLabels[index].SetText("{0:0}", value);
    }

    public bool BadgeShown(int index) => index >= 0 && index < BadgeCount && _discs[index] && _discs[index].gameObject.activeSelf;
    public RectTransform Badge(int index) => index >= 0 && index < BadgeCount ? _discs[index] : null;

    /// <summary>The 220ms trigger hop, on the face only; instant (none) under Reduce Motion.</summary>
    public void Hop()
    {
        if (CutSheetMotion.ReduceMotion || !_face) return;
        _hopStart = Time.unscaledTime;
    }

    /// <summary>The slap of an item landing in the slot; instant (none) under Reduce Motion.</summary>
    public void Slap()
    {
        if (CutSheetMotion.ReduceMotion || !_face) return;
        _slapStart = Time.unscaledTime;
    }

    // The root keeps its Image for raycasts but draws nothing, and the neo-punk button styling
    // (EditorialButton, which recolours every child label each frame, and its "Editorial Frame"
    // child) is switched off: on a dressed slot only the art, the discs and the ring draw.
    void HideRootGraphic()
    {
        if (TryGetComponent(out Image image) && image.color != Color.clear) image.color = Color.clear;
        if (TryGetComponent(out EditorialFrame frame) && frame.enabled) frame.enabled = false;
        if (TryGetComponent(out Outline outline) && outline.enabled) outline.enabled = false;
        if (TryGetComponent(out EditorialButton button) && button.enabled) button.enabled = false;
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name == "Editorial Frame" && child.gameObject.activeSelf) child.gameObject.SetActive(false);
        }
    }

    void SyncShadow()
    {
        if (!_art)
        {
            var art = _face.Find("Authored Icon");
            if (art) art.TryGetComponent(out _art);
        }
        bool sticker = _occupied && _art && _art.gameObject.activeSelf && _art.sprite;
        if (sticker)
        {
            _shadow.Match(_art, CutSheetShadow.Tint, CutSheetMetrics.ShadowSlot);
            if (!_shadow.Image.preserveAspect) _shadow.Image.preserveAspect = true;
        }
        else _shadow.SetDepth(0);
    }

    void SyncRing()
    {
        if (!_ring) return;
        bool pinned = _pinSource && ContextualTooltipUI.Instance && ContextualTooltipUI.Instance.IsPinned(_pinSource);
        bool shown = _occupied && (_hovered || _focused || _selected || pinned) || !_occupied && _focused;
        if (_ring.enabled != shown) _ring.enabled = shown;
        var lift = _occupied && _hovered ? CutSheetMetrics.HoverFaceOffset : Vector2.zero;
        Stretch(_face, lift);
    }

    void LateUpdate()
    {
        if (!_face) return;
        SyncRing();
        var theme = DealbreakUIStyle.Theme;
        if (theme)
            for (int i = 0; i < BadgeCount; i++)
                if (_discLabels[i] && _discs[i].gameObject.activeSelf && !DiscStyled(_discLabels[i], theme)) StyleDisc(_discLabels[i]);
        float scale = 1f;
        float now = Time.unscaledTime;
        if (_hopStart >= 0f)
        {
            float duration = CutSheetMotion.Seconds(CutSheetMetrics.StickerHopSeconds);
            float t = duration > 0f ? (now - _hopStart) / duration : 1f;
            if (t >= 1f) _hopStart = -1f;
            else scale *= 1f + Mathf.Sin(t * Mathf.PI) * HopScale;
        }
        if (_slapStart >= 0f)
        {
            float duration = CutSheetMotion.Seconds(SlapSeconds);
            float t = duration > 0f ? (now - _slapStart) / duration : 1f;
            if (t >= 1f) _slapStart = -1f;
            else scale *= 1f + SlapScale * (1f - t) * (1f - t);
        }
        var target = Vector3.one * scale;
        if (_face.localScale != target) _face.localScale = target;
        if (_shadowHolder.localScale != target) _shadowHolder.localScale = target;
    }

    public void OnPointerEnter(PointerEventData eventData) { _hovered = true; SyncRing(); }
    public void OnPointerExit(PointerEventData eventData) { _hovered = false; SyncRing(); }
    public void OnSelect(BaseEventData eventData) { _focused = true; SyncRing(); }
    public void OnDeselect(BaseEventData eventData) { _focused = false; SyncRing(); }
}
