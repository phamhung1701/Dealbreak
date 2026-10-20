using TMPro;
using UnityEngine;

/// <summary>
/// A flat shape that follows a label's rendered text every frame, because the HP panel and enemy
/// views change shape between turns. It sits as the label's previous sibling. Three looks:
/// a bar behind the text that the text sits on, a thin bar under the text (Underline), and a solid
/// label block behind the text (Block, used for name tags). Presentation only: callers pass the
/// shown fraction.
/// </summary>
public sealed class HealthBarBacking : MonoBehaviour
{
    public enum Look { Behind, Underline, Block }

    const float HorizontalPadding = 10f;
    const float VerticalPadding = 7f;
    const float UnderlineGap = 4f;
    const float UnderlineHeight = 6f;

    TMP_Text _label;
    RectTransform _bar;
    RectTransform _fill;
    EditorialFrame _track;
    float _fraction = 1f;
    bool _fitLabelWidth;
    Look _look;

    public static HealthBarBacking Attach(TMP_Text label, bool fitLabelWidth, Look look = Look.Behind)
    {
        if (!label) return null;
        if (!label.TryGetComponent(out HealthBarBacking backing)) backing = label.gameObject.AddComponent<HealthBarBacking>();
        backing._fitLabelWidth = fitLabelWidth;
        backing._look = look;
        backing.Build();
        return backing;
    }

    /// <summary>A solid block of <paramref name="color"/> behind the label's text, like a printed name tag.</summary>
    public static HealthBarBacking AttachBlock(TMP_Text label, Color color)
    {
        var backing = Attach(label, false, Look.Block);
        if (backing == null) return null;
        backing.SetFraction(0f);
        if (backing._track) { backing._track.ink = backing._track.rim = backing._track.fill = color; backing._track.Refresh(); }
        return backing;
    }

    public RectTransform Bar => _bar;

    public void SetFraction(float fraction)
    {
        _fraction = Mathf.Clamp01(fraction);
        if (_fill) _fill.anchorMax = new Vector2(_fraction, 1f);
    }

    void Build()
    {
        _label = GetComponent<TMP_Text>();
        if (_bar || !_label || !transform.parent) return;
        var theme = DealbreakUIStyle.Theme;
        var bar = new GameObject($"{name} Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorialFrame));
        _bar = (RectTransform)bar.transform;
        _bar.SetParent(transform.parent, false);
        _bar.SetSiblingIndex(transform.GetSiblingIndex());
        _bar.anchorMin = _bar.anchorMax = _bar.pivot = new Vector2(.5f, .5f);
        var trackColor = theme ? theme.slate : new Color(.23f, .23f, .25f, 1f);
        if (_look == Look.Behind && theme) trackColor = new Color(theme.ink.r, theme.ink.g, theme.ink.b, .88f);
        _track = bar.GetComponent<EditorialFrame>();
        Strip(_track, trackColor);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorialFrame));
        _fill = (RectTransform)fill.transform;
        _fill.SetParent(_bar, false);
        _fill.anchorMin = Vector2.zero;
        _fill.offsetMin = _fill.offsetMax = Vector2.zero;
        Strip(fill.GetComponent<EditorialFrame>(), theme ? theme.brick : new Color(.91f, .2f, .17f, 1f));
        SetFraction(_fraction);
    }

    static void Strip(EditorialFrame strip, Color color)
    {
        strip.raycastTarget = false;
        strip.ink = strip.rim = strip.fill = color;
        strip.border = 0;
        strip.corner = 0;
        strip.showAccent = false;
        strip.showShadow = false;
        strip.Refresh();
    }

    void LateUpdate()
    {
        if (!_bar || !_label) return;
        _bar.gameObject.SetActive(_label.isActiveAndEnabled && _label.alpha > 0f);
        // Numbers over or beside a bar keep a black rim; a name on its own block needs none.
        if (_look != Look.Block) DealbreakUIStyle.Outline(_label);
        var label = _label.rectTransform;
        var parent = (RectTransform)_bar.parent;
        Bounds text = _label.textBounds;
        bool hasText = text.size.x > 0f;
        Rect area = label.rect;
        float padding = _look == Look.Underline ? 0f : HorizontalPadding;
        float xMin = (_fitLabelWidth || !hasText ? area.xMin : text.min.x) - padding;
        float xMax = (_fitLabelWidth || !hasText ? area.xMax : text.max.x) + padding;
        float yMin = hasText ? text.min.y - VerticalPadding : area.yMin;
        float yMax = hasText ? text.max.y + VerticalPadding : area.yMax;
        if (_look == Look.Block && hasText) { yMin = text.min.y - 4f; yMax = text.max.y + 4f; }
        if (_look == Look.Underline)
        {
            yMax = (hasText ? text.min.y : area.yMin) - UnderlineGap;
            yMin = yMax - UnderlineHeight;
        }
        Vector2 min = parent.InverseTransformPoint(label.TransformPoint(new Vector3(xMin, yMin)));
        Vector2 max = parent.InverseTransformPoint(label.TransformPoint(new Vector3(xMax, yMax)));
        _bar.sizeDelta = max - min;
        _bar.anchoredPosition = (min + max) * .5f - parent.rect.center;
    }

    void OnDestroy()
    {
        if (_bar) Destroy(_bar.gameObject);
    }
}
