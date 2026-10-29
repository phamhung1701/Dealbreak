using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The active Contract's line above the draw pile (HUD slice S11, visual-design 3b): Zilla Slab 20
/// paper with its numerals in Courier Prime Bold 20, right-aligned, no shadow, over a 2px paper
/// underline that is its click affordance (3px on hover; the focus ring on selection). The click
/// itself stays with the label's existing ContextualInfoTrigger. Owns the label's text and type.
/// </summary>
public sealed class ContractLineUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public const float TextSize = 20f, Underline = 2f, UnderlineHover = 3f, UnderlineGap = 2f;
    public const string UnderlineName = "Contract Underline", FocusRingName = "Contract Focus Ring";
    const float FocusRingOutset = 4f;

    TMP_Text _label;
    Image _underline, _ring;
    string _plain = string.Empty;
    float _shownWidth = -1f;
    bool _hover, _focus;
    readonly StringBuilder _builder = new();
    static TMP_FontAsset _registered;

    public string PlainText => _plain;
    public Image UnderlineImage => _underline;
    public bool FocusRingShown => _ring && _ring.enabled;
    public float UnderlineHeight => _underline ? _underline.rectTransform.sizeDelta.y : 0f;

    public static ContractLineUI Attach(TMP_Text label)
    {
        if (!label) return null;
        if (!label.TryGetComponent(out ContractLineUI line)) line = label.gameObject.AddComponent<ContractLineUI>();
        line.Build();
        return line;
    }

    void Build()
    {
        if (_label) return;
        if (!TryGetComponent(out _label) || !_label) { _label = null; return; }
        var existing = transform.Find(UnderlineName);
        RectTransform rect;
        if (existing) rect = (RectTransform)existing;
        else
        {
            var item = new GameObject(UnderlineName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.layer = gameObject.layer;
            rect = (RectTransform)item.transform;
            rect.SetParent(transform, false);
        }
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.TryGetComponent(out _underline);
        _underline.raycastTarget = false;
        var theme = DealbreakUIStyle.Theme;
        _underline.color = theme ? theme.paper : Color.white;
        var ring = transform.Find(FocusRingName);
        _ring = ring && ring.TryGetComponent<Image>(out var found) ? found : CutSheetHoverRing.CreateRing(transform, FocusRingName, FocusRingOutset);
        if (_ring) { _ring.raycastTarget = false; _ring.enabled = false; }
        Style();
        Apply();
    }

    /// <summary>Shows <paramref name="plain"/> (the Contract's progress); writes only on change.</summary>
    public void SetText(string plain)
    {
        Build();
        plain ??= string.Empty;
        if (plain == _plain && _label && string.IsNullOrEmpty(_label.text) == (plain.Length == 0)) return;
        _plain = plain;
        if (_label) _label.text = Decorate(plain);
        _shownWidth = -1f;
        Apply();
    }

    // Digit runs print in the numeric face. TMP resolves <font="name"> through MaterialReferenceManager
    // first, so the theme's numeric font is registered once rather than loaded from Resources.
    string Decorate(string plain)
    {
        var theme = DealbreakUIStyle.Theme;
        var numeric = theme ? theme.numericFont : null;
        if (!numeric || string.IsNullOrEmpty(plain)) return plain;
        if (_registered != numeric) { MaterialReferenceManager.AddFontAsset(numeric); _registered = numeric; }
        _builder.Clear();
        bool inRun = false;
        foreach (char c in plain)
        {
            bool digit = char.IsDigit(c) || inRun && c == '/' ;
            if (digit && !inRun) { _builder.Append("<font=\"").Append(numeric.name).Append("\">"); inRun = true; }
            else if (!digit && inRun) { _builder.Append("</font>"); inRun = false; }
            _builder.Append(c);
        }
        if (inRun) _builder.Append("</font>");
        return _builder.ToString();
    }

    void LateUpdate()
    {
        Style();
        if (_label && !Mathf.Approximately(_label.preferredWidth, _shownWidth)) Apply();
    }

    // The HUD's hierarchy styling may retouch this label; restore the Contract type only on drift.
    void Style()
    {
        if (!_label) return;
        var theme = DealbreakUIStyle.Theme;
        if (!theme) return;
        if (theme.bodyFont && _label.font != theme.bodyFont) _label.font = theme.bodyFont;
        if (!Mathf.Approximately(_label.fontSize, TextSize)) _label.fontSize = TextSize;
        if (_label.enableAutoSizing) _label.enableAutoSizing = false;
        if (_label.color != theme.paper) _label.color = theme.paper;
        if (_label.fontStyle != FontStyles.Normal) _label.fontStyle = FontStyles.Normal;
        if (!_label.richText) _label.richText = true;
        if (_label.alignment != TextAlignmentOptions.BottomRight) _label.alignment = TextAlignmentOptions.BottomRight;
        if (!_label.raycastTarget) _label.raycastTarget = true;
    }

    public void SetHovered(bool hovered) { _hover = hovered; Apply(); }
    public void OnPointerEnter(PointerEventData _) => SetHovered(true);
    public void OnPointerExit(PointerEventData _) => SetHovered(false);
    public void OnSelect(BaseEventData _) { _focus = true; Apply(); }
    public void OnDeselect(BaseEventData _) { _focus = false; Apply(); }
    void OnDisable() { _hover = _focus = false; Apply(); }

    // The underline runs under the text itself (right-aligned), not the whole label box.
    void Apply()
    {
        if (!_underline) return;
        float width = _label && _plain.Length > 0 ? _label.preferredWidth : 0f;
        _shownWidth = width;
        var rect = _underline.rectTransform;
        float maxWidth = ((RectTransform)transform).rect.width;
        if (maxWidth > 0f) width = Mathf.Min(width, maxWidth);
        var size = new Vector2(width, _hover ? UnderlineHover : Underline);
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        var position = new Vector2(0f, -UnderlineGap);
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        bool show = width > 0f;
        if (_underline.enabled != show) _underline.enabled = show;
        if (_ring && _ring.enabled != (_focus && show)) _ring.enabled = _focus && show;
    }
}
