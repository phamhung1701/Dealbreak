using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shakes the gameplay canvas when the player is hit: one shake per resolved hit, played back to
/// back, so a multi-hit attack reads as several impacts. Presentation only. The offset is added
/// to the canvas' top-level panels just before the canvas renders and removed first thing next
/// frame, so layout code never sees a shaken position.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class ScreenShakeUI : MonoBehaviour
{
    const float ShakeTime = .2f;
    const float HitStrength = 14f;
    const float ShieldStrength = 6f;
    const float MaxStrength = 26f;
    const int MaxQueued = 8;

    readonly Queue<float> _queued = new();
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
        }
    }

    void Apply()
    {
        if (!isActiveAndEnabled || _applied.Count > 0 || _elapsed >= ShakeTime) return;
        float t = _elapsed / ShakeTime;
        float decay = (1f - t) * (1f - t);
        float phase = _elapsed * 70f + _seed * 1.7f;
        var offset = new Vector3(Mathf.Sin(phase), Mathf.Cos(phase * 1.3f), 0f) * _strength * decay;
        _targets.Clear();
        for (int i = 0; i < transform.childCount; i++)
            if (transform.GetChild(i) is RectTransform child && child.gameObject.activeSelf && child.name != "Background")
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
