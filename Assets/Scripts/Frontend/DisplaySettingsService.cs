using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct DisplayResolution
{
    public readonly int Width;
    public readonly int Height;

    public DisplayResolution(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public override string ToString() => $"{Width} × {Height}";
}

public readonly struct DisplaySettingsData
{
    public readonly int Width;
    public readonly int Height;
    public readonly FullScreenMode Mode;
    public readonly int VSyncCount;

    public DisplaySettingsData(int width, int height, FullScreenMode mode, int vSyncCount)
    {
        Width = width;
        Height = height;
        Mode = mode;
        VSyncCount = Mathf.Clamp(vSyncCount, 0, 1);
    }
}

public static class DisplaySettingsService
{
    const string WidthKey = "display.width";
    const string HeightKey = "display.height";
    const string ModeKey = "display.mode";
    const string VSyncKey = "display.vsync";

    public static readonly FullScreenMode[] SupportedModes =
    {
        FullScreenMode.Windowed,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.ExclusiveFullScreen
    };

    public static IReadOnlyList<DisplayResolution> GetAvailableResolutions()
    {
        var values = Screen.resolutions
            .Select(value => new DisplayResolution(value.width, value.height))
            .GroupBy(value => (value.Width, value.Height))
            .Select(group => group.First())
            .OrderBy(value => value.Width)
            .ThenBy(value => value.Height)
            .ToList();

        if (!values.Any(value => value.Width == Screen.width && value.Height == Screen.height))
            values.Add(new DisplayResolution(Mathf.Max(640, Screen.width), Mathf.Max(480, Screen.height)));
        if (values.Count == 0)
            values.Add(new DisplayResolution(1280, 720));
        return values;
    }

    public static DisplaySettingsData Load()
    {
        var resolutions = GetAvailableResolutions();
        int savedWidth = PlayerPrefs.GetInt(WidthKey, Screen.width);
        int savedHeight = PlayerPrefs.GetInt(HeightKey, Screen.height);
        var resolution = resolutions.FirstOrDefault(value => value.Width == savedWidth && value.Height == savedHeight);
        if (resolution.Width <= 0 || resolution.Height <= 0)
            resolution = resolutions.FirstOrDefault(value => value.Width == Screen.width && value.Height == Screen.height);
        if (resolution.Width <= 0 || resolution.Height <= 0)
            resolution = resolutions[^1];

        int rawMode = PlayerPrefs.GetInt(ModeKey, (int)Screen.fullScreenMode);
        var mode = SupportedModes.Contains((FullScreenMode)rawMode)
            ? (FullScreenMode)rawMode
            : Screen.fullScreenMode;
        if (!SupportedModes.Contains(mode)) mode = FullScreenMode.Windowed;
        int vSync = Mathf.Clamp(PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0), 0, 1);

        var normalized = new DisplaySettingsData(resolution.Width, resolution.Height, mode, vSync);
        Save(normalized);
        return normalized;
    }

    public static void LoadAndApply() => Apply(Load());

    public static void Apply(DisplaySettingsData settings)
    {
        var resolutions = GetAvailableResolutions();
        var resolution = resolutions.FirstOrDefault(value => value.Width == settings.Width && value.Height == settings.Height);
        if (resolution.Width <= 0 || resolution.Height <= 0)
            resolution = resolutions[^1];
        var mode = SupportedModes.Contains(settings.Mode) ? settings.Mode : FullScreenMode.Windowed;
        int vSync = Mathf.Clamp(settings.VSyncCount, 0, 1);

        QualitySettings.vSyncCount = vSync;
        Screen.SetResolution(resolution.Width, resolution.Height, mode);
        Save(new DisplaySettingsData(resolution.Width, resolution.Height, mode, vSync));
    }

    static void Save(DisplaySettingsData settings)
    {
        PlayerPrefs.SetInt(WidthKey, settings.Width);
        PlayerPrefs.SetInt(HeightKey, settings.Height);
        PlayerPrefs.SetInt(ModeKey, (int)settings.Mode);
        PlayerPrefs.SetInt(VSyncKey, settings.VSyncCount);
        PlayerPrefs.Save();
    }

    // Motion and readability preferences (HUD slice S17). Each setter persists at once and raises
    // MotionSettingsChanged when the value changes, so open screens update live. Values are cached
    // after the first read because presenters read them every tween.
    public const string ReduceMotionKey = "display.reduceMotion";
    public const string ScreenShakeKey = "display.screenShake";
    public const string TriggerSpeedKey = "display.triggerSpeed";
    public const string TooltipTextSizeKey = "display.tooltipTextSize";
    public static readonly string[] MotionSettingKeys = { ReduceMotionKey, ScreenShakeKey, TriggerSpeedKey, TooltipTextSizeKey };

    /// <summary>Trigger playback speeds by option index: 1x, 1.5x, 2x.</summary>
    public static readonly float[] PlaybackSpeedOptions = { 1f, 1.5f, 2f };
    /// <summary>Tooltip text scales by option index: 100%, 115%, 130%.</summary>
    public static readonly float[] TooltipTextScaleOptions = { 1f, 1.15f, 1.3f };

    public static event Action MotionSettingsChanged;

    static bool _motionLoaded;
    static bool _reduceMotion;
    static bool _screenShake = true;
    static int _playbackSpeedIndex;
    static int _tooltipTextSizeIndex;

    /// <summary>The player's Reduce Motion choice. Default off.</summary>
    public static bool ReduceMotion
    {
        get { EnsureMotionLoaded(); return _reduceMotion; }
        set { EnsureMotionLoaded(); if (_reduceMotion == value) return; _reduceMotion = value; SaveMotion(); }
    }

    /// <summary>The stored Screen Shake preference (default on). Kept as chosen while Reduce Motion is on.</summary>
    public static bool ScreenShake
    {
        get { EnsureMotionLoaded(); return _screenShake; }
        set { EnsureMotionLoaded(); if (_screenShake == value) return; _screenShake = value; SaveMotion(); }
    }

    /// <summary>Whether hits shake: the Screen Shake preference, forced off by Reduce Motion.</summary>
    public static bool EffectiveScreenShake => ScreenShake && !ReduceMotion;

    public static int PlaybackSpeedIndex
    {
        get { EnsureMotionLoaded(); return _playbackSpeedIndex; }
        set
        {
            EnsureMotionLoaded();
            int index = Mathf.Clamp(value, 0, PlaybackSpeedOptions.Length - 1);
            if (_playbackSpeedIndex == index) return;
            _playbackSpeedIndex = index; SaveMotion();
        }
    }

    /// <summary>Trigger playback speed multiplier (1, 1.5 or 2). Default 1.</summary>
    public static float PlaybackSpeed => PlaybackSpeedOptions[PlaybackSpeedIndex];

    public static int TooltipTextSizeIndex
    {
        get { EnsureMotionLoaded(); return _tooltipTextSizeIndex; }
        set
        {
            EnsureMotionLoaded();
            int index = Mathf.Clamp(value, 0, TooltipTextScaleOptions.Length - 1);
            if (_tooltipTextSizeIndex == index) return;
            _tooltipTextSizeIndex = index; SaveMotion();
        }
    }

    /// <summary>Tooltip text scale (1, 1.15 or 1.3). Default 1.</summary>
    public static float TooltipTextScale => TooltipTextScaleOptions[TooltipTextSizeIndex];

    /// <summary>Drops the cache so the next read comes from PlayerPrefs (tests, or after prefs are reset).</summary>
    public static void ReloadMotionSettings()
    {
        _motionLoaded = false;
        EnsureMotionLoaded();
        MotionSettingsChanged?.Invoke();
    }

    static bool _motionIsolated;

    /// <summary>
    /// Test isolation: while set, motion settings start at their defaults and are never read from or
    /// written to PlayerPrefs, so test runs ignore (and keep) the player's own choices.
    /// </summary>
    public static bool MotionSettingsIsolated
    {
        get => _motionIsolated;
        set { if (_motionIsolated == value) return; _motionIsolated = value; ReloadMotionSettings(); }
    }

    static void EnsureMotionLoaded()
    {
        if (_motionLoaded) return;
        _motionLoaded = true;
        if (_motionIsolated)
        {
            _reduceMotion = false; _screenShake = true; _playbackSpeedIndex = 0; _tooltipTextSizeIndex = 0;
            return;
        }
        _reduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) != 0;
        _screenShake = PlayerPrefs.GetInt(ScreenShakeKey, 1) != 0;
        _playbackSpeedIndex = Mathf.Clamp(PlayerPrefs.GetInt(TriggerSpeedKey, 0), 0, PlaybackSpeedOptions.Length - 1);
        _tooltipTextSizeIndex = Mathf.Clamp(PlayerPrefs.GetInt(TooltipTextSizeKey, 0), 0, TooltipTextScaleOptions.Length - 1);
    }

    static void SaveMotion()
    {
        if (_motionIsolated) { MotionSettingsChanged?.Invoke(); return; }
        PlayerPrefs.SetInt(ReduceMotionKey, _reduceMotion ? 1 : 0);
        PlayerPrefs.SetInt(ScreenShakeKey, _screenShake ? 1 : 0);
        PlayerPrefs.SetInt(TriggerSpeedKey, _playbackSpeedIndex);
        PlayerPrefs.SetInt(TooltipTextSizeKey, _tooltipTextSizeIndex);
        PlayerPrefs.Save();
        MotionSettingsChanged?.Invoke();
    }
}
