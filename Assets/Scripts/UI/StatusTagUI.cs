using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The status band's one tag (visual-design 3b, hud.md sections C and E): PLAYER TURN, BONUS ACTION,
/// DEFENSE, the selection or block preview and the auto-Recover notice all print on this one paper
/// ticket stub, never tilted, with a 3px ink shadow. Barlow Condensed Bold 22 ink, uppercase; digit
/// runs print in Courier Prime Bold through a font tag. The tag sizes to its text inside the band
/// (the root keeps the authored rect). A new line swaps with a fade, instant under Reduce Motion.
/// Presentation only: <see cref="ActionButtonsUI"/> is the one writer, through <see cref="SetLine"/>.
/// </summary>
public sealed class StatusTagUI : MonoBehaviour
{
    public const string RootName = "Status Tag", BodyName = "Body", FaceName = "Face";
    public const float FontSize = 22f, PaddingX = 20f, InsetY = 4f;

    RectTransform _body;
    Image _face;
    CutSheetShadow _shadow;
    TMP_Text _label;
    bool _built, _visible = true;
    string _line = string.Empty, _shown = string.Empty, _pending;
    float _swapStart = -1f;
    string _numberOpen;
    readonly StringBuilder _markup = new(96);

    public TMP_Text Label => _label;
    public Image Face => _face;
    public CutSheetShadow Shadow => _shadow;
    public RectTransform Body => _body;
    /// <summary>The line the tag shows (or is fading to).</summary>
    public string Line => _line;
    /// <summary>The line currently printed on the tag.</summary>
    public string ShownLine => _shown;
    public bool BodyVisible => _body && _body.gameObject.activeSelf;
    public float TiltDegrees => _body ? Mathf.DeltaAngle(0f, _body.localEulerAngles.z) : 0f;

    /// <summary>The one line: the selection preview stands in for the state text while it has content.</summary>
    public static string Compose(string state, string preview) =>
        string.IsNullOrEmpty(preview) ? state ?? string.Empty : preview;

    /// <summary>Builds the tag around <paramref name="label"/> once: a root takes the label's rect and the label prints on the body.</summary>
    public static StatusTagUI Dress(TMP_Text label)
    {
        if (!label) return null;
        var existing = label.GetComponentInParent<StatusTagUI>(true);
        if (existing) return existing;
        var labelRect = label.rectTransform;
        var item = new GameObject(RootName, typeof(RectTransform));
        item.layer = label.gameObject.layer;
        var root = (RectTransform)item.transform;
        root.SetParent(labelRect.parent, false);
        root.SetSiblingIndex(labelRect.GetSiblingIndex());
        root.anchorMin = labelRect.anchorMin;
        root.anchorMax = labelRect.anchorMax;
        root.pivot = labelRect.pivot;
        root.anchoredPosition = labelRect.anchoredPosition;
        root.sizeDelta = labelRect.sizeDelta;
        var tag = item.AddComponent<StatusTagUI>();
        tag.Build(label);
        return tag;
    }

    void Build(TMP_Text label)
    {
        if (_built) return;
        _built = true;
        var theme = DealbreakUIStyle.Theme;
        _body = Part(BodyName, transform, false);
        _body.anchorMin = new Vector2(.5f, 0f);
        _body.anchorMax = new Vector2(.5f, 1f);
        _body.pivot = new Vector2(.5f, .5f);
        _body.anchoredPosition = Vector2.zero;
        _body.sizeDelta = new Vector2(PaddingX * 2f, -InsetY * 2f);
        _body.localRotation = Quaternion.Euler(0f, 0f, CutSheetMetrics.StatusTagTilt);
        _shadow = CutSheetShadow.Create(_body);
        _face = Part(FaceName, _body, true).GetComponent<Image>();
        Stretch(_face.rectTransform, 0f);
        _face.sprite = theme ? theme.tagSprite : null;
        _face.type = Image.Type.Sliced;
        _face.color = theme ? theme.paper : new Color(.94f, .89f, .8f);
        _face.raycastTarget = false;
        _shadow.Match(_face, CutSheetShadow.Tint, CutSheetMetrics.ShadowSlot);

        _label = label;
        _label.transform.SetParent(_body, false);
        if (!_label.gameObject.activeSelf) _label.gameObject.SetActive(true);
        var rect = _label.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(PaddingX, 0f); rect.offsetMax = new Vector2(-PaddingX, 0f);
        rect.localRotation = Quaternion.identity; rect.localScale = Vector3.one;
        if (theme && theme.numericFont)
        {
            // The <font> tag finds the asset through TMP's registry, keyed by the asset name.
            MaterialReferenceManager.AddFontAsset(theme.numericFont);
            _numberOpen = "<font=\"" + theme.numericFont.name + "\">";
        }
        Restyle();
        _label.alpha = 1f;
        Apply(string.Empty);
    }

    /// <summary>Asserts the owned type: Barlow Condensed Bold 22 ink, uppercase, one line. Writes only what differs.</summary>
    public void Restyle()
    {
        if (!_label) return;
        var theme = DealbreakUIStyle.Theme;
        var font = theme ? (theme.labelFont ? theme.labelFont : theme.headingFont) : null;
        Color ink = theme ? theme.inkPrint : CutSheetShadow.FallbackTint;
        if (font && _label.font != font) _label.font = font;
        if (_label.font && _label.fontSharedMaterial != _label.font.material) _label.fontSharedMaterial = _label.font.material;
        if (_label.enableAutoSizing) _label.enableAutoSizing = false;
        if (!Mathf.Approximately(_label.fontSize, FontSize)) _label.fontSize = FontSize;
        if (!Mathf.Approximately(_label.fontSizeMax, FontSize)) _label.fontSizeMax = FontSize;
        Color color = new(ink.r, ink.g, ink.b, _label.color.a);
        if (_label.color != color) _label.color = color;
        if (_label.fontStyle != FontStyles.UpperCase) _label.fontStyle = FontStyles.UpperCase;
        if (_label.alignment != TextAlignmentOptions.Center) _label.alignment = TextAlignmentOptions.Center;
        if (_label.textWrappingMode != TextWrappingModes.NoWrap) _label.textWrappingMode = TextWrappingModes.NoWrap;
        if (_label.overflowMode != TextOverflowModes.Ellipsis) _label.overflowMode = TextOverflowModes.Ellipsis;
        if (_label.characterSpacing != 0f) _label.characterSpacing = 0f;
        if (_label.raycastTarget) _label.raycastTarget = false;
        if (_label.richText != true) _label.richText = true;
    }

    /// <summary>Shows or hides the whole tag (the band is for encounters only).</summary>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        UpdateBodyActive();
    }

    /// <summary>The one writer path: sets the line; a change swaps with a fade (instant under Reduce Motion).</summary>
    public void SetLine(string line)
    {
        if (!_built) return;
        line ??= string.Empty;
        if (line == _line) return;
        _line = line;
        bool fade = Animating && _shown.Length > 0 && line.Length > 0 && BodyVisible;
        if (!fade) { _swapStart = -1f; _pending = null; _label.alpha = 1f; Apply(line); return; }
        _pending = line;
        // A swap already fading out keeps its start; one fading in restarts from the current alpha.
        if (_swapStart < 0f || Time.unscaledTime - _swapStart >= Half) _swapStart = Time.unscaledTime - Half * (1f - _label.alpha);
    }

    static bool Animating => Application.isPlaying && !CutSheetMotion.ReduceMotion;
    static float Half => CutSheetMotion.Seconds(CutSheetMetrics.StatusCrossfadeSeconds) * .5f;

    void LateUpdate()
    {
        if (_swapStart < 0f) return;
        float half = Half;
        if (half <= 0f)
        {
            if (_pending != null) { Apply(_pending); _pending = null; }
            _label.alpha = 1f; _swapStart = -1f; return;
        }
        float t = (Time.unscaledTime - _swapStart) / half;
        if (t < 1f) { _label.alpha = 1f - t; return; }
        if (_pending != null) { Apply(_pending); _pending = null; }
        if (t >= 2f) { _label.alpha = 1f; _swapStart = -1f; return; }
        _label.alpha = t - 1f;
    }

    // Prints the line: digit runs in Courier Prime through the font tag; sizes the body to the text.
    void Apply(string line)
    {
        _shown = line;
        _markup.Clear();
        bool inNumber = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            bool digit = c >= '0' && c <= '9';
            if (_numberOpen != null && digit != inNumber)
            {
                _markup.Append(digit ? _numberOpen : "</font>");
                inNumber = digit;
            }
            _markup.Append(c);
        }
        if (inNumber) _markup.Append("</font>");
        Restyle();
        _label.SetText(_markup);
        float band = ((RectTransform)transform).rect.width;
        float width = line.Length == 0 ? PaddingX * 2f : _label.GetPreferredValues().x + PaddingX * 2f;
        if (band > 0f) width = Mathf.Min(width, band);
        var size = new Vector2(width, -InsetY * 2f);
        if (_body.sizeDelta != size) _body.sizeDelta = size;
        UpdateBodyActive();
    }

    void UpdateBodyActive()
    {
        if (!_body) return;
        bool show = _visible && (_shown.Length > 0 || _pending != null);
        if (_body.gameObject.activeSelf != show) _body.gameObject.SetActive(show);
    }

    static void Stretch(RectTransform rect, float outset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(-outset, -outset);
        rect.offsetMax = new Vector2(outset, outset);
    }

    RectTransform Part(string name, Transform parent, bool image)
    {
        var existing = parent.Find(name);
        if (existing) return (RectTransform)existing;
        var item = image
            ? new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            : new GameObject(name, typeof(RectTransform));
        item.layer = gameObject.layer;
        var rect = (RectTransform)item.transform;
        rect.SetParent(parent, false);
        return rect;
    }
}
