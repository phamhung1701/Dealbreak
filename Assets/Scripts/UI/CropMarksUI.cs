using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Four teal crop marks around a rect (visual-design 3b, Target marks): the theme's markSprite (one
/// top-left bracket) four times, rotated 0/90/180/270, each bracket corner sitting <see cref="DistanceFor"/>
/// pixels outside the rect's corner so the marks never cover what they frame. Thin = scale 1, thick =
/// scale 1.6, snapping over 90ms; a one-frame hop outward when targeting begins. Shared by the enemy
/// row and the drop targets. Presentation only; every mark ignores raycasts. Motion runs in LateUpdate
/// on unscaled time and is instant under Reduce Motion or outside Play mode.
/// </summary>
public sealed class CropMarksUI : MonoBehaviour
{
    public enum State { Hidden, Thin, Thick }

    public const string ObjectName = "Crop Marks";
    public const float ThinScale = 1f, ThickScale = 1.6f, SnapSeconds = .09f;
    // The bracket's arms run inward from its corner, 7px of stroke and outline at scale 1, so the
    // corner sits ClearGap plus that weight (scaled) outside the bounds: the whole mark stays outside.
    public const float MarkSize = 32f, ClearGap = 6f, StrokeWeight = 7f, HopDistance = 4f;
    /// <summary>How far each bracket corner sits outside the bounds, on both axes, at <paramref name="scale"/>.</summary>
    public static float DistanceFor(float scale) => ClearGap + StrokeWeight * scale;
    // The sprite's bracket corner sits 4px in from its top-left (visual-design 2, sprite 5).
    const float CornerInset = 4f;
    const int Count = 4;

    static readonly Vector2[] Corners = { new(0, 1), new(1, 1), new(1, 0), new(0, 0) };
    static readonly float[] Angles = { 0f, -90f, -180f, -270f };

    readonly Image[] _marks = new Image[Count];
    State _state = State.Hidden;
    float _scale = ThinScale, _from = ThinScale, _to = ThinScale, _snapStart = -1f;
    int _hopFrame = -1;
    bool _hopShown, _built;

    public State Current => _state;
    public float MarkScale => _scale;
    public int MarkCount => Count;
    public Image Mark(int index) => index >= 0 && index < Count ? _marks[index] : null;
    public bool IsShown => _state != State.Hidden;

    /// <summary>A holder stretched over <paramref name="parent"/> carrying the four marks.</summary>
    public static CropMarksUI Create(Transform parent, string name = ObjectName)
    {
        if (!parent) return null;
        var existing = parent.Find(name);
        RectTransform holder;
        if (existing) holder = (RectTransform)existing;
        else
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.layer = parent.gameObject.layer;
            holder = (RectTransform)item.transform;
            holder.SetParent(parent, false);
        }
        holder.anchorMin = Vector2.zero;
        holder.anchorMax = Vector2.one;
        holder.pivot = new Vector2(.5f, .5f);
        holder.offsetMin = holder.offsetMax = Vector2.zero;
        return Attach(holder);
    }

    /// <summary>Marks around <paramref name="bounds"/> itself (they are added as its children).</summary>
    public static CropMarksUI Attach(RectTransform bounds)
    {
        if (!bounds) return null;
        if (!bounds.TryGetComponent(out CropMarksUI marks)) marks = bounds.gameObject.AddComponent<CropMarksUI>();
        marks.Build();
        return marks;
    }

    void Build()
    {
        if (_built) return;
        _built = true;
        var theme = DealbreakUIStyle.Theme;
        Color teal = theme ? theme.ochre : new Color(.2f, .62f, .58f);
        for (int i = 0; i < Count; i++)
        {
            string name = $"Mark {i}";
            var existing = transform.Find(name);
            RectTransform rect;
            if (existing) rect = (RectTransform)existing;
            else
            {
                var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                item.layer = gameObject.layer;
                rect = (RectTransform)item.transform;
                rect.SetParent(transform, false);
            }
            rect.anchorMin = rect.anchorMax = Corners[i];
            rect.pivot = new Vector2(CornerInset / MarkSize, 1f - CornerInset / MarkSize);
            rect.sizeDelta = new Vector2(MarkSize, MarkSize);
            rect.localRotation = Quaternion.Euler(0f, 0f, Angles[i]);
            rect.TryGetComponent(out Image image);
            image.sprite = theme ? theme.markSprite : null;
            image.type = Image.Type.Simple;
            image.color = teal;
            image.raycastTarget = false;
            image.enabled = false;
            _marks[i] = image;
        }
        Place(false);
        ApplyScale();
    }

    public void Set(State state)
    {
        Build();
        if (state == _state) return;
        var previous = _state;
        _state = state;
        bool show = state != State.Hidden;
        for (int i = 0; i < Count; i++)
            if (_marks[i] && _marks[i].enabled != show) _marks[i].enabled = show;
        if (!show) return;
        float target = state == State.Thick ? ThickScale : ThinScale;
        if (previous == State.Hidden || !Application.isPlaying || CutSheetMotion.Seconds(SnapSeconds) <= 0f)
        {
            _scale = _from = _to = target;
            _snapStart = -1f;
            ApplyScale();
        }
        else
        {
            _from = _scale;
            _to = target;
            _snapStart = Time.unscaledTime;
        }
    }

    /// <summary>The one-frame hop outward when targeting begins (none under Reduce Motion).</summary>
    public void Hop()
    {
        if (!Application.isPlaying || CutSheetMotion.ReduceMotion) return;
        _hopFrame = Time.frameCount;
    }

    void LateUpdate()
    {
        if (_snapStart >= 0f)
        {
            float duration = CutSheetMotion.Seconds(SnapSeconds);
            float t = duration > 0f ? Mathf.Clamp01((Time.unscaledTime - _snapStart) / duration) : 1f;
            float eased = 1f - (1f - t) * (1f - t);
            _scale = Mathf.LerpUnclamped(_from, _to, eased);
            if (t >= 1f) { _scale = _to; _snapStart = -1f; }
            ApplyScale();
        }
        bool hop = _hopFrame == Time.frameCount && _state != State.Hidden;
        if (hop != _hopShown) Place(hop);
    }

    void Place(bool hop)
    {
        _hopShown = hop;
        float distance = DistanceFor(_scale) + (hop ? HopDistance : 0f);
        for (int i = 0; i < Count; i++)
        {
            if (!_marks[i]) continue;
            var outward = new Vector2(Corners[i].x * 2f - 1f, Corners[i].y * 2f - 1f);
            _marks[i].rectTransform.anchoredPosition = outward * distance;
        }
    }

    void ApplyScale()
    {
        var scale = new Vector3(_scale, _scale, 1f);
        for (int i = 0; i < Count; i++)
            if (_marks[i] && _marks[i].rectTransform.localScale != scale) _marks[i].rectTransform.localScale = scale;
        Place(_hopShown);
    }
}
