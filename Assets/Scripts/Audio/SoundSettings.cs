using UnityEngine;

public enum AudioBus { Music, Sfx, Ui }

/// <summary>
/// The player's Music, SFX and UI volumes, in 10% steps (accessibility requirement: independent
/// controls). Setters persist at once. Gain is the square of the level, so each step sounds even.
/// </summary>
public static class SoundSettings
{
    public const string MusicKey = "audio.music";
    public const string SfxKey = "audio.sfx";
    public const string UiKey = "audio.ui";
    public const int MaxStep = 10;
    public static readonly string[] StepLabels =
        { "0%", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%" };

    static readonly string[] Keys = { MusicKey, SfxKey, UiKey };
    static readonly int[] Defaults = { 7, 8, 8 };
    static readonly int[] _steps = new int[Keys.Length];
    static bool _loaded;
    static bool _isolated;

    /// <summary>Test isolation: while set, volumes start at their defaults and never touch PlayerPrefs.</summary>
    public static bool Isolated
    {
        get => _isolated;
        set { _isolated = value; _loaded = false; }
    }

    public static int Step(AudioBus bus)
    {
        EnsureLoaded();
        return _steps[(int)bus];
    }

    public static void SetStep(AudioBus bus, int step)
    {
        EnsureLoaded();
        step = Mathf.Clamp(step, 0, MaxStep);
        if (_steps[(int)bus] == step) return;
        _steps[(int)bus] = step;
        if (_isolated) return;
        PlayerPrefs.SetInt(Keys[(int)bus], step);
        PlayerPrefs.Save();
    }

    public static float Gain(AudioBus bus) => GainForStep(Step(bus));

    public static float GainForStep(int step)
    {
        float level = Mathf.Clamp(step, 0, MaxStep) / (float)MaxStep;
        return level * level;
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        for (int i = 0; i < Keys.Length; i++)
            _steps[i] = _isolated ? Defaults[i] : Mathf.Clamp(PlayerPrefs.GetInt(Keys[i], Defaults[i]), 0, MaxStep);
    }
}
