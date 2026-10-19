using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A thick HP bar drawn behind an HP label, so the bar covers the "HP n/m" text. It sits as the
/// label's previous sibling and follows the rendered text every frame, because the HP panel and
/// enemy cards change shape between turns. Presentation only: callers pass the shown fraction.
/// </summary>
public sealed class HealthBarBacking : MonoBehaviour
{
    const float HorizontalPadding = 10f;
    const float VerticalPadding = 7f;

    TMP_Text _label;
    RectTransform _bar;
    RectTransform _fill;
    float _fraction = 1f;
    bool _fitLabelWidth;

    public static HealthBarBacking Attach(TMP_Text label, bool fitLabelWidth)
    {
        if (!label) return null;
        if (!label.TryGetComponent(out HealthBarBacking backing)) backing = label.gameObject.AddComponent<HealthBarBacking>();
        backing._fitLabelWidth = fitLabelWidth;
        backing.Build();
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
        var bar = new GameObject($"{name} Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _bar = (RectTransform)bar.transform;
        _bar.SetParent(transform.parent, false);
        _bar.SetSiblingIndex(transform.GetSiblingIndex());
        _bar.anchorMin = _bar.anchorMax = _bar.pivot = new Vector2(.5f, .5f);
        var track = bar.GetComponent<Image>();
        track.raycastTarget = false;
        track.color = theme ? new Color(theme.ink.r, theme.ink.g, theme.ink.b, .88f) : new Color(.08f, .08f, .08f, .88f);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _fill = (RectTransform)fill.transform;
        _fill.SetParent(_bar, false);
        _fill.anchorMin = Vector2.zero;
        _fill.offsetMin = _fill.offsetMax = Vector2.zero;
        var fillImage = fill.GetComponent<Image>();
        fillImage.raycastTarget = false;
        fillImage.color = theme ? theme.brick : new Color(.66f, .27f, .22f, 1f);
        SetFraction(_fraction);
    }

    void LateUpdate()
    {
        if (!_bar || !_label) return;
        _bar.gameObject.SetActive(_label.isActiveAndEnabled && _label.alpha > 0f);
        DealbreakUIStyle.Outline(_label);
        var label = _label.rectTransform;
        var parent = (RectTransform)_bar.parent;
        Bounds text = _label.textBounds;
        bool hasText = text.size.x > 0f;
        Rect area = label.rect;
        float xMin = (_fitLabelWidth || !hasText ? area.xMin : text.min.x) - HorizontalPadding;
        float xMax = (_fitLabelWidth || !hasText ? area.xMax : text.max.x) + HorizontalPadding;
        float yMin = hasText ? text.min.y - VerticalPadding : area.yMin;
        float yMax = hasText ? text.max.y + VerticalPadding : area.yMax;
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
