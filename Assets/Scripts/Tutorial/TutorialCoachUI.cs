using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tutorial's coach: an acid-lime rule around what the current step is about, and a flat
/// panel beside it with the lesson. Explanations dim everything but their targets and wait for
/// their button; actions leave the board live. Presentation only: the director owns the steps.
/// </summary>
public sealed class TutorialCoachUI : MonoBehaviour
{
    // Above hover details (90), below modal menus such as pause (100).
    const int SortingOrder = 95;
    const float PanelWidth = 440f, Padding = 26f, Gap = 18f, HighlightPad = 8f;

    RectTransform _root, _panel, _shadeLayer, _highlightLayer;
    CanvasGroup _panelGroup;
    TMP_Text _lesson, _counter, _title, _body, _refusal;
    Button _next, _skip;
    TMP_Text _nextLabel;
    Color _shadeColor;
    readonly List<Image> _shade = new();
    readonly List<EditorialFrame> _highlights = new();
    readonly List<RectTransform> _targets = new();
    readonly List<Rect> _bounds = new();
    readonly Vector3[] _corners = new Vector3[4];
    TutorialStep _step;
    bool _lastStep;
    float _refusedAt = float.NegativeInfinity;
    bool _visible;

    public event Action NextPressed;
    public event Action SkipPressed;

    public bool IsVisible => _visible;
    public TutorialStep Step => _step;
    public string RefusalText => _refusal ? _refusal.text : string.Empty;
    public RectTransform Panel => _panel;
    public IReadOnlyList<Rect> TargetBounds => _bounds;

    // Its own overlay canvas, scaled like the gameplay one: the gameplay canvas restyles every
    // control under it, which would repaint the coach's buttons and labels.
    public static TutorialCoachUI Install(Canvas gameplay)
    {
        if (!gameplay) return null;
        var source = gameplay.rootCanvas;
        var root = new GameObject("Tutorial Coach", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster)).GetComponent<RectTransform>();
        root.gameObject.layer = source.gameObject.layer;
        var layer = root.GetComponent<Canvas>();
        layer.renderMode = RenderMode.ScreenSpaceOverlay;
        layer.sortingOrder = SortingOrder;
        var scaler = root.GetComponent<CanvasScaler>();
        if (source.TryGetComponent(out CanvasScaler sourceScaler))
        {
            scaler.uiScaleMode = sourceScaler.uiScaleMode;
            scaler.referenceResolution = sourceScaler.referenceResolution;
            scaler.screenMatchMode = sourceScaler.screenMatchMode;
            scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
            scaler.scaleFactor = sourceScaler.scaleFactor;
        }
        var coach = root.gameObject.AddComponent<TutorialCoachUI>();
        coach.Build(root);
        return coach;
    }

    void Build(RectTransform root)
    {
        if (_panel) return;
        _root = root;
        var theme = DealbreakUIStyle.Theme;
        _shadeColor = theme ? new Color(theme.ink.r, theme.ink.g, theme.ink.b, .72f) : new Color(0, 0, 0, .72f);
        _shadeLayer = DealbreakUIStyle.Child(root, "Shade");
        DealbreakUIStyle.Place(_shadeLayer, 0, 0, 1, 1);
        _highlightLayer = DealbreakUIStyle.Child(root, "Highlights");
        DealbreakUIStyle.Place(_highlightLayer, 0, 0, 1, 1);

        _panel = DealbreakUIStyle.Child(root, "Coach Panel", typeof(Image), typeof(CanvasGroup));
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(.5f, .5f);
        _panelGroup = _panel.GetComponent<CanvasGroup>();
        var frame = DealbreakUIStyle.Frame(_panel);
        if (frame && theme) { frame.rim = theme.slate; frame.fill = theme.charcoal; frame.Refresh(); }
        _panel.GetComponent<Image>().raycastTarget = true;
        ContextualTooltipUI.RegisterObstacle(_panel);

        _lesson = Text("Lesson", theme ? theme.labelFont ? theme.labelFont : theme.headingFont : null, 18, theme ? theme.ochre : Color.yellow);
        _lesson.fontStyle = FontStyles.UpperCase;
        _lesson.characterSpacing = 6;
        _counter = Text("Counter", theme ? theme.numericFont : null, 16, theme ? Color.Lerp(theme.paper, theme.slate, .5f) : Color.gray);
        _counter.alignment = TextAlignmentOptions.TopRight;
        _title = Text("Title", theme ? theme.headingFont : null, 34, theme ? theme.paper : Color.white);
        _title.fontStyle = FontStyles.UpperCase;
        _body = Text("Body", theme ? theme.bodyFont : null, 21, theme ? theme.paper : Color.white);
        _body.lineSpacing = 6;
        _refusal = Text("Refusal", theme ? theme.bodyFont : null, 19, theme ? theme.brick : Color.red);

        _skip = MakeButton("TutorialSkip", "SKIP TUTORIAL", EditorialButton.Role.Quiet);
        _skip.onClick.AddListener(() => SkipPressed?.Invoke());
        _next = MakeButton("TutorialNext", "NEXT", EditorialButton.Role.Paper);
        _next.onClick.AddListener(() => NextPressed?.Invoke());
        _nextLabel = _next.GetComponentInChildren<TMP_Text>(true);
        Hide();
    }

    TMP_Text Text(string name, TMP_FontAsset font, float size, Color color)
    {
        var rect = DealbreakUIStyle.Child(_panel, name, typeof(TextMeshProUGUI));
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        var text = rect.GetComponent<TextMeshProUGUI>();
        if (font) DealbreakUIStyle.Text(text, font, size, color);
        else { text.fontSize = size; text.color = color; }
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        return text;
    }

    Button MakeButton(string name, string label, EditorialButton.Role role)
    {
        var rect = DealbreakUIStyle.Child(_panel, name, typeof(Image), typeof(Button));
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        var image = rect.GetComponent<Image>();
        image.color = Color.clear;
        var button = rect.GetComponent<Button>();
        button.targetGraphic = image;
        var text = DealbreakUIStyle.Child(rect, "Label", typeof(TextMeshProUGUI));
        DealbreakUIStyle.Place(text, 0, 0, 1, 1);
        var tmp = text.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        DealbreakUIStyle.Button(button, role);
        return button;
    }

    public void Show(TutorialStep step, int index, int count)
    {
        if (_step != step)
        {
            _step = step;
            _refusedAt = float.NegativeInfinity;
            _refusal.text = string.Empty;
            _panelGroup.alpha = 0;
        }
        _lastStep = index == count - 1;
        _lesson.text = step.Lesson ?? string.Empty;
        _counter.text = $"{index + 1} / {count}";
        _next.gameObject.SetActive(step.IsModal);
        _skip.gameObject.SetActive(!_lastStep);
        if (_nextLabel) _nextLabel.text = step.Button;
        _visible = true;
        _panel.gameObject.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        _visible = false;
        if (_panel) _panel.gameObject.SetActive(false);
        foreach (var shade in _shade) if (shade) shade.gameObject.SetActive(false);
        foreach (var highlight in _highlights) if (highlight) highlight.gameObject.SetActive(false);
    }

    public void Refuse(string text)
    {
        if (!_visible || string.IsNullOrEmpty(text)) return;
        _refusal.text = text;
        _refusedAt = Time.unscaledTime;
        Layout();
    }

    // Copy and targets are live (incoming damage, hand cards moving), so they are read every frame.
    void Refresh()
    {
        _title.text = _step.Title?.Invoke() ?? string.Empty;
        _body.text = _step.Body?.Invoke() ?? string.Empty;
        _targets.Clear();
        if (_step.Targets != null)
            foreach (var target in _step.Targets())
                if (target && target.gameObject.activeInHierarchy) _targets.Add(target);
        Layout();
    }

    void Update()
    {
        if (_visible && _step != null) Refresh();
    }

    void LateUpdate()
    {
        if (!_visible) return;
        _panelGroup.alpha = Mathf.MoveTowards(_panelGroup.alpha, 1, Time.unscaledDeltaTime * 6);
        float pulse = .6f + .4f * Mathf.Sin(Time.unscaledTime * 4f);
        foreach (var highlight in _highlights)
            if (highlight && highlight.gameObject.activeSelf) highlight.color = new Color(1, 1, 1, pulse);
    }

    // Panel contents top to bottom, then the panel beside its targets, then the highlights and shade.
    void Layout()
    {
        float inner = PanelWidth - Padding * 2;
        float y = -Padding;
        Place(_lesson, Padding, y, inner * .7f, 20);
        Place(_counter, Padding + inner * .7f, y, inner * .3f, 20);
        y -= 30;
        y -= Place(_title, Padding, y, inner, Preferred(_title, inner)) + 8;
        y -= Place(_body, Padding, y, inner, Preferred(_body, inner));
        if (!string.IsNullOrEmpty(_refusal.text)) { y -= 12; y -= Place(_refusal, Padding, y, inner, Preferred(_refusal, inner)); }
        if (_skip.gameObject.activeSelf || _next.gameObject.activeSelf)
        {
            y -= 22;
            PlaceRect((RectTransform)_skip.transform, Padding - 8, y, 180, 44);
            PlaceRect((RectTransform)_next.transform, PanelWidth - Padding - 150, y, 150, 44);
            y -= 44;
        }
        y -= Padding - 6;
        _panel.sizeDelta = new Vector2(PanelWidth, -y);

        _bounds.Clear();
        foreach (var target in _targets) _bounds.Add(BoundsIn(target));
        PositionPanel(_bounds);
        DrawHighlights(_bounds);
        DrawShade(_bounds);
    }

    static float Preferred(TMP_Text text, float width) =>
        string.IsNullOrEmpty(text.text) ? 0 : text.GetPreferredValues(text.text, width, 0).y;

    static float Place(TMP_Text text, float x, float y, float width, float height)
    {
        PlaceRect(text.rectTransform, x, y, width, height);
        return height;
    }

    static void PlaceRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // A label is outlined around its rendered text, not its (often stage-wide) rect.
    Rect BoundsIn(RectTransform child)
    {
        if (child.TryGetComponent(out TMP_Text text) && !string.IsNullOrEmpty(text.text) && text.textBounds.size.x > 0)
        {
            var local = text.textBounds;
            _corners[0] = child.TransformPoint(local.min);
            _corners[1] = child.TransformPoint(new Vector3(local.min.x, local.max.y));
            _corners[2] = child.TransformPoint(local.max);
            _corners[3] = child.TransformPoint(new Vector3(local.max.x, local.min.y));
        }
        else child.GetWorldCorners(_corners);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in _corners)
        {
            Vector2 point = _root.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x - HighlightPad, min.y - HighlightPad, max.x + HighlightPad, max.y + HighlightPad);
    }

    // Beside the first target: above it when it sits low (the hand, the action bar), below it when
    // it sits high, otherwise right then left, unless the step names a side. On each side the panel
    // may slide along the edge; it takes the first spot that fits without covering any target.
    // With no target it sits in the middle of the stage.
    void PositionPanel(IReadOnlyList<Rect> targets)
    {
        var area = _root.rect;
        var size = _panel.sizeDelta;
        Vector2 chosen = area.center;
        if (targets.Count > 0)
        {
            var anchor = targets[0];
            float relative = Mathf.InverseLerp(area.yMin, area.yMax, anchor.center.y);
            var order = _step != null && _step.Side != Vector2.zero
                ? new[] { _step.Side, Vector2.right, Vector2.left, Vector2.up, Vector2.down }
                : relative < .38f
                    ? new[] { Vector2.up, Vector2.right, Vector2.left, Vector2.down }
                    : relative > .66f
                        ? new[] { Vector2.down, Vector2.right, Vector2.left, Vector2.up }
                        : new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
            chosen = Clamp(Beside(anchor, order[0], size), size, area);
            bool found = false;
            foreach (var side in order)
            {
                var beside = Beside(anchor, side, size);
                if (!Fits(beside, side, size, area)) continue;
                foreach (var candidate in Slides(beside, side, size, area))
                {
                    if (Covers(candidate, size, targets)) continue;
                    chosen = candidate;
                    found = true;
                    break;
                }
                if (found) break;
            }
        }
        if (Time.unscaledTime - _refusedAt < .35f)
            chosen.x += Mathf.Sin((Time.unscaledTime - _refusedAt) * 60f) * 8f;
        _panel.anchoredPosition = chosen - area.center;
    }

    static Vector2 Beside(Rect target, Vector2 side, Vector2 size)
    {
        if (side == Vector2.right) return new(target.xMax + Gap + size.x * .5f, target.center.y);
        if (side == Vector2.left) return new(target.xMin - Gap - size.x * .5f, target.center.y);
        if (side == Vector2.up) return new(target.center.x, target.yMax + Gap + size.y * .5f);
        return new(target.center.x, target.yMin - Gap - size.y * .5f);
    }

    // Centred on the target first, then pushed to either end of the edge it sits on.
    static IEnumerable<Vector2> Slides(Vector2 beside, Vector2 side, Vector2 size, Rect area)
    {
        var centred = Clamp(beside, size, area);
        yield return centred;
        if (side.x != 0)
        {
            yield return new(centred.x, area.yMax - size.y * .5f - Gap);
            yield return new(centred.x, area.yMin + size.y * .5f + Gap);
        }
        else
        {
            yield return new(area.xMin + size.x * .5f + Gap, centred.y);
            yield return new(area.xMax - size.x * .5f - Gap, centred.y);
        }
    }

    // Only the axis that separates the panel from its target has to fit.
    static bool Fits(Vector2 center, Vector2 side, Vector2 size, Rect area) => side.x != 0
        ? center.x - size.x * .5f >= area.xMin + Gap && center.x + size.x * .5f <= area.xMax - Gap
        : center.y - size.y * .5f >= area.yMin + Gap && center.y + size.y * .5f <= area.yMax - Gap;

    static Vector2 Clamp(Vector2 center, Vector2 size, Rect area) => new(
        Mathf.Clamp(center.x, area.xMin + size.x * .5f + Gap, area.xMax - size.x * .5f - Gap),
        Mathf.Clamp(center.y, area.yMin + size.y * .5f + Gap, area.yMax - size.y * .5f - Gap));

    static bool Covers(Vector2 center, Vector2 size, IReadOnlyList<Rect> targets)
    {
        var panel = new Rect(center - size * .5f, size);
        foreach (var target in targets) if (panel.Overlaps(target)) return true;
        return false;
    }

    void DrawHighlights(IReadOnlyList<Rect> targets)
    {
        var theme = DealbreakUIStyle.Theme;
        while (_highlights.Count < targets.Count)
        {
            var rect = DealbreakUIStyle.Child(_highlightLayer, $"Highlight {_highlights.Count}", typeof(CanvasRenderer), typeof(EditorialFrame));
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var frame = rect.GetComponent<EditorialFrame>();
            frame.raycastTarget = false;
            frame.fill = Color.clear;
            frame.rim = theme ? theme.ochre : Color.yellow;
            frame.border = 2;
            frame.Refresh();
            _highlights.Add(frame);
        }
        for (int i = 0; i < _highlights.Count; i++)
        {
            bool used = i < targets.Count;
            _highlights[i].gameObject.SetActive(used);
            if (!used) continue;
            var rect = _highlights[i].rectTransform;
            rect.anchoredPosition = targets[i].center - _root.rect.center;
            rect.sizeDelta = targets[i].size;
        }
    }

    // Explanations dim everything but their targets, and the dim takes the clicks. The stage is cut
    // into a grid along the targets' edges; every cell outside all targets is dimmed, with runs of
    // cells in a row merged into one piece.
    void DrawShade(IReadOnlyList<Rect> targets)
    {
        int used = 0;
        if (_step != null && _step.IsModal)
        {
            var area = _root.rect;
            var xs = new List<float> { area.xMin, area.xMax };
            var ys = new List<float> { area.yMin, area.yMax };
            foreach (var target in targets)
            {
                xs.Add(Mathf.Clamp(target.xMin, area.xMin, area.xMax)); xs.Add(Mathf.Clamp(target.xMax, area.xMin, area.xMax));
                ys.Add(Mathf.Clamp(target.yMin, area.yMin, area.yMax)); ys.Add(Mathf.Clamp(target.yMax, area.yMin, area.yMax));
            }
            xs.Sort(); ys.Sort();
            for (int row = 0; row < ys.Count - 1; row++)
            {
                if (ys[row + 1] - ys[row] <= 0) continue;
                float start = float.NaN;
                for (int column = 0; column <= xs.Count - 1; column++)
                {
                    bool open = column < xs.Count - 1 && xs[column + 1] - xs[column] > 0 &&
                        !Inside(new Vector2((xs[column] + xs[column + 1]) * .5f, (ys[row] + ys[row + 1]) * .5f), targets);
                    if (open && float.IsNaN(start)) start = xs[column];
                    if (!open && !float.IsNaN(start))
                    {
                        ShadePiece(used++, Rect.MinMaxRect(start, ys[row], xs[column], ys[row + 1]), area);
                        start = float.NaN;
                    }
                }
            }
        }
        for (int i = used; i < _shade.Count; i++) _shade[i].gameObject.SetActive(false);
    }

    static bool Inside(Vector2 point, IReadOnlyList<Rect> targets)
    {
        foreach (var target in targets) if (target.Contains(point)) return true;
        return false;
    }

    void ShadePiece(int index, Rect piece, Rect area)
    {
        while (_shade.Count <= index)
        {
            var rect = DealbreakUIStyle.Child(_shadeLayer, $"Shade {_shade.Count}", typeof(Image));
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var image = rect.GetComponent<Image>();
            image.color = _shadeColor;
            _shade.Add(image);
        }
        var shade = _shade[index];
        shade.gameObject.SetActive(true);
        shade.rectTransform.anchoredPosition = piece.center - area.center;
        shade.rectTransform.sizeDelta = piece.size;
    }
}
