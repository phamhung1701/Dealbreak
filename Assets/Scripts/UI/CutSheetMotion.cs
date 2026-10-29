using System;

/// <summary>
/// The motion and readability preferences the Cut Sheet presenters read, backed by the persisted
/// store in <see cref="DisplaySettingsService"/> (HUD slice S17). Consumers: Reduce Motion and
/// ShakeEnabled (S18), PlaybackSpeed and PlaybackSeconds (S19), TooltipTextScale (S20). Subscribe to
/// <see cref="Changed"/> to update live when the player changes a setting.
/// </summary>
public static class CutSheetMotion
{
    /// <summary>Reduce Motion: the test override when set, else the player's setting.</summary>
    public static bool ReduceMotion => ReduceMotionOverride ?? DisplaySettingsService.ReduceMotion;
    /// <summary>Set by tests; null reads the player's setting.</summary>
    public static bool? ReduceMotionOverride { get; set; }
    /// <summary>Whether hits shake: the Screen Shake setting, forced off by Reduce Motion.</summary>
    public static bool ShakeEnabled => !ReduceMotion && DisplaySettingsService.ScreenShake;
    /// <summary>Trigger playback speed multiplier: 1, 1.5 or 2.</summary>
    public static float PlaybackSpeed => DisplaySettingsService.PlaybackSpeed;
    /// <summary>Tooltip text scale: 1, 1.15 or 1.3.</summary>
    public static float TooltipTextScale => DisplaySettingsService.TooltipTextScale;

    /// <summary>Raised when any motion or readability setting changes.</summary>
    public static event Action Changed
    {
        add => DisplaySettingsService.MotionSettingsChanged += value;
        remove => DisplaySettingsService.MotionSettingsChanged -= value;
    }

    // A tween's duration under the current preferences: instant under Reduce Motion. Not scaled by
    // trigger playback speed, which only governs trigger playback (use PlaybackSeconds there).
    public static float Seconds(float seconds) =>
        ReduceMotion || seconds <= 0f ? 0f : seconds;

    // A trigger-playback duration: instant under Reduce Motion, else divided by the playback speed.
    public static float PlaybackSeconds(float seconds) =>
        ReduceMotion || seconds <= 0f ? 0f : seconds / (PlaybackSpeed > 0f ? PlaybackSpeed : 1f);
}
