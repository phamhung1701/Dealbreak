using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shakes the gameplay canvas when the player is hit: one shake per resolved hit, played back to
/// back, so a multi-hit attack reads as several impacts. A hit that costs HP also flashes a red
/// border around the screen. Presentation only. The offset is added to the canvas' top-level
/// panels just before the canvas renders and removed first thing next frame, so layout code never
/// sees a shaken position.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class ScreenShakeUI : MonoBehaviour
{
    const float ShakeTime = .2f;
    const float HitStrength = 14f;
    const float ShieldStrength = 6f;
    const float MaxStrength = 26f;
    const int MaxQueued = 8;

    const string VignetteName = "Hit Vignette";
    // Above gameplay and the drag layer, below the hover detail (90) and menus (100).
    const int VignetteSortingOrder = 85;

    readonly Queue<float> _queued = new();
    Image _vignette;
    float _vignettePeak;
    readonly List<RectTransform> _targets = new();
    readonly List<Vector3> _applied = new();
    CombatManager _combat;
    float _strength;
    float _elapsed = ShakeTime;
    int _seed;

    public static ScreenShakeUI Install(Canvas canvas)
    {
        if (!canvas) return null;
        if (!canvas.TryGetComponent(out ScreenShakeUI shake)) shake = canvas.gameObject.AddComponent<ScreenShakeUI>();
        return shake;
    }

    void OnEnable()
    {
        Canvas.willRenderCanvases += Apply;
        Subscribe();
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= Apply;
        Restore();
        if (_combat != null) _combat.OnDamageResolved -= HandleDamageResolved;
        _combat = null;
        _queued.Clear();
        _elapsed = ShakeTime;
    }

    void Subscribe()
    {
        if (_combat != null || CombatManager.Instance == null) return;
        _combat = CombatManager.Instance;
        _combat.OnDamageResolved += HandleDamageResolved;
    }

    void HandleDamageResolved(DamageResult result)
    {
        if (!ReferenceEquals(result.Request.Target, _combat.player) || !Application.isPlaying) return;
        float strength = result.ActualHpLost > 0
            ? Mathf.Min(MaxStrength, HitStrength + result.ActualHpLost * .6f)
            : result.ShieldConsumed ? ShieldStrength : 0f;
        if (strength <= 0f || _queued.Count >= MaxQueued) return;
        _queued.Enqueue(strength);
    }

    void Update()
    {
        Restore();
        Subscribe();
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed >= ShakeTime && _queued.Count > 0)
        {
            _strength = _queued.Dequeue();
            _elapsed = 0f;
            _seed++;
            // Only a hit that costs HP flashes red; a Shield-absorbed hit just shakes.
            _vignettePeak = _strength >= HitStrength ? Mathf.Lerp(.5f, .8f, (_strength - HitStrength) / (MaxStrength - HitStrength)) : 0f;
        }
        UpdateVignette();
    }

    void UpdateVignette()
    {
        float t = Mathf.Clamp01(_elapsed / (ShakeTime * 1.6f));
        float alpha = _vignettePeak * (1f - t) * (1f - t);
        if (alpha <= .001f)
        {
            if (_vignette && _vignette.enabled) _vignette.enabled = false;
            return;
        }
        EnsureVignette();
        _vignette.enabled = true;
        var color = _vignette.color;
        color.a = alpha;
        _vignette.color = color;
    }

    void EnsureVignette()
    {
        if (_vignette) return;
        var go = new GameObject(VignetteName, typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var layer = go.GetComponent<Canvas>();
        layer.overrideSorting = true;
        layer.sortingOrder = VignetteSortingOrder;
        _vignette = go.GetComponent<Image>();
        _vignette.raycastTarget = false;
        _vignette.sprite = BorderSprite();
        var theme = DealbreakUIStyle.Theme;
        var red = theme ? theme.brick : new Color(.66f, .27f, .22f);
        _vignette.color = new Color(Mathf.Min(1f, red.r * 1.35f), red.g * .6f, red.b * .6f, 0f);
        _vignette.enabled = false;
    }

    // A transparent centre fading to opaque along every edge.
    static Sprite BorderSprite()
    {
        const int Size = 128;
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float u = Mathf.Abs(x / (Size - 1f) * 2f - 1f), v = Mathf.Abs(y / (Size - 1f) * 2f - 1f);
            // A band about a tenth of the screen deep along each edge, deepest in the corners.
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.8f, 1f, Mathf.Max(u, v)));
            edge = Mathf.Max(edge, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, u * v)) * .8f);
            byte a = (byte)(Mathf.Clamp01(edge) * 255f);
            pixels[y * Size + x] = new Color32(255, 255, 255, a);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f));
    }

    void Apply()
    {
        // The red edge still flashes; only the movement honours the shake and Reduce Motion settings.
        if (!CutSheetMotion.ShakeEnabled || CutSheetMotion.ReduceMotion) return;
        if (!isActiveAndEnabled || _applied.Count > 0 || _elapsed >= ShakeTime) return;
        float t = _elapsed / ShakeTime;
        float decay = (1f - t) * (1f - t);
        float phase = _elapsed * 70f + _seed * 1.7f;
        var offset = new Vector3(Mathf.Sin(phase), Mathf.Cos(phase * 1.3f), 0f) * _strength * decay;
        _targets.Clear();
        for (int i = 0; i < transform.childCount; i++)
            if (transform.GetChild(i) is RectTransform child && child.gameObject.activeSelf &&
                child.name != "Background" && child.name != VignetteName)
                _targets.Add(child);
        foreach (var target in _targets)
        {
            target.localPosition += offset;
            _applied.Add(offset);
        }
    }

    void Restore()
    {
        for (int i = 0; i < _applied.Count && i < _targets.Count; i++)
            if (_targets[i]) _targets[i].localPosition -= _applied[i];
        _applied.Clear();
    }
}
