using UnityEngine;

/// <summary>
/// Eases a displayed integer toward its gameplay value and reports a fading change pulse.
/// Presentation only: callers keep reading the authoritative value from gameplay state.
/// </summary>
public sealed class DisplayedNumber
{
    const float Duration = 0.4f;
    static readonly Color LossColor = new(1f, 0.42f, 0.36f, 1f);
    static readonly Color GainColor = new(0.45f, 1f, 0.6f, 1f);

    float _shown;
    float _from;
    int _target;
    float _elapsed = Duration;
    bool _initialized;
    Color _pulseColor;

    public int Shown => Mathf.RoundToInt(_shown);
    /// <summary>The unrounded eased value, for bars that should glide between integers.</summary>
    public float Value => _shown;
    public bool IsAnimating => _elapsed < Duration;

    /// <summary>Sets the value to show; with <paramref name="animate"/> false it is shown at once.</summary>
    public void SetTarget(int value, bool animate)
    {
        if (_initialized && value == _target) return;
        if (!_initialized || !animate)
        {
            _shown = value;
            _elapsed = Duration;
        }
        else
        {
            _pulseColor = value < _target ? LossColor : GainColor;
            _from = _shown;
            _elapsed = 0f;
        }
        _target = value;
        _initialized = true;
    }

    /// <summary>Advances the ease; returns true while the label needs redrawing.</summary>
    public bool Tick(float deltaTime)
    {
        if (!IsAnimating) return false;
        _elapsed = Mathf.Min(Duration, _elapsed + deltaTime);
        float t = _elapsed / Duration;
        _shown = Mathf.Lerp(_from, _target, 1f - (1f - t) * (1f - t) * (1f - t));
        return true;
    }

    /// <summary>The label tint: the change color fading back to <paramref name="baseColor"/>.</summary>
    public Color Tint(Color baseColor) =>
        IsAnimating ? Color.Lerp(baseColor, _pulseColor, 1f - _elapsed / Duration) : baseColor;
}
