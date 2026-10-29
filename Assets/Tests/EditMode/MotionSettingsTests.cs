using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class MotionSettingsTests
{
    readonly Dictionary<string, int?> _saved = new();
    bool? _override;
    int _changes;
    void Count() => _changes++;

    [SetUp]
    public void SetUp()
    {
        // These cases test persistence itself, so they step out of the assembly-wide isolation.
        DisplaySettingsService.MotionSettingsIsolated = false;
        _saved.Clear();
        foreach (var key in DisplaySettingsService.MotionSettingKeys)
        {
            _saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            PlayerPrefs.DeleteKey(key);
        }
        _override = CutSheetMotion.ReduceMotionOverride;
        CutSheetMotion.ReduceMotionOverride = null;
        DisplaySettingsService.ReloadMotionSettings();
        _changes = 0;
        DisplaySettingsService.MotionSettingsChanged += Count;
    }

    [TearDown]
    public void TearDown()
    {
        DisplaySettingsService.MotionSettingsChanged -= Count;
        foreach (var pair in _saved)
        {
            if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value);
            else PlayerPrefs.DeleteKey(pair.Key);
        }
        PlayerPrefs.Save();
        CutSheetMotion.ReduceMotionOverride = _override;
        DisplaySettingsService.ReloadMotionSettings();
        DisplaySettingsService.MotionSettingsIsolated = true;
    }

    [Test]
    public void Defaults_ReduceMotionOff_ShakeOn_SpeedOne_TextHundredPercent()
    {
        Assert.IsFalse(DisplaySettingsService.ReduceMotion);
        Assert.IsTrue(DisplaySettingsService.ScreenShake);
        Assert.IsTrue(DisplaySettingsService.EffectiveScreenShake);
        Assert.AreEqual(1f, DisplaySettingsService.PlaybackSpeed);
        Assert.AreEqual(1f, DisplaySettingsService.TooltipTextScale);
    }

    [Test]
    public void Settings_PersistUnderDisplayKeys_AndSurviveAReload()
    {
        DisplaySettingsService.ReduceMotion = true;
        DisplaySettingsService.ScreenShake = false;
        DisplaySettingsService.PlaybackSpeedIndex = 2;
        DisplaySettingsService.TooltipTextSizeIndex = 1;
        foreach (var key in DisplaySettingsService.MotionSettingKeys) StringAssert.StartsWith("display.", key);
        Assert.AreEqual(1, PlayerPrefs.GetInt(DisplaySettingsService.ReduceMotionKey));
        Assert.AreEqual(0, PlayerPrefs.GetInt(DisplaySettingsService.ScreenShakeKey));

        DisplaySettingsService.ReloadMotionSettings();
        Assert.IsTrue(DisplaySettingsService.ReduceMotion);
        Assert.IsFalse(DisplaySettingsService.ScreenShake);
        Assert.AreEqual(2f, DisplaySettingsService.PlaybackSpeed);
        Assert.AreEqual(1.15f, DisplaySettingsService.TooltipTextScale, 1e-4f);
    }

    [Test]
    public void OutOfRangeIndices_Clamp()
    {
        PlayerPrefs.SetInt(DisplaySettingsService.TriggerSpeedKey, 9);
        PlayerPrefs.SetInt(DisplaySettingsService.TooltipTextSizeKey, -3);
        DisplaySettingsService.ReloadMotionSettings();
        Assert.AreEqual(2f, DisplaySettingsService.PlaybackSpeed);
        Assert.AreEqual(1f, DisplaySettingsService.TooltipTextScale);
    }

    [Test]
    public void ReduceMotion_ForcesEffectiveShakeOff_AndKeepsTheShakePreference()
    {
        DisplaySettingsService.ReduceMotion = true;
        Assert.IsTrue(DisplaySettingsService.ScreenShake);
        Assert.IsFalse(DisplaySettingsService.EffectiveScreenShake);
        Assert.IsFalse(CutSheetMotion.ShakeEnabled);
        DisplaySettingsService.ReduceMotion = false;
        Assert.IsTrue(DisplaySettingsService.EffectiveScreenShake);
        Assert.IsTrue(CutSheetMotion.ShakeEnabled);
    }

    [Test]
    public void ChangeEvent_FiresOncePerChange_AndNotForTheSameValue()
    {
        int seen = 0;
        void Handler() => seen++;
        CutSheetMotion.Changed += Handler;
        try
        {
            DisplaySettingsService.PlaybackSpeedIndex = 1;
            DisplaySettingsService.PlaybackSpeedIndex = 1;
            DisplaySettingsService.ScreenShake = false;
        }
        finally { CutSheetMotion.Changed -= Handler; }
        Assert.AreEqual(2, seen);
        Assert.AreEqual(2, _changes);
    }

    [Test]
    public void CutSheetMotion_ReadsTheStore_AndTheTestOverrideStillWins()
    {
        DisplaySettingsService.ReduceMotion = true;
        DisplaySettingsService.PlaybackSpeedIndex = 2;
        DisplaySettingsService.TooltipTextSizeIndex = 2;
        Assert.IsTrue(CutSheetMotion.ReduceMotion);
        Assert.AreEqual(0f, CutSheetMotion.Seconds(.2f));
        Assert.AreEqual(1.3f, CutSheetMotion.TooltipTextScale, 1e-4f);

        CutSheetMotion.ReduceMotionOverride = false;
        Assert.IsFalse(CutSheetMotion.ReduceMotion);
        Assert.AreEqual(.2f, CutSheetMotion.Seconds(.2f), 1e-5f, "Playback speed does not scale general tweens");
        Assert.AreEqual(.1f, CutSheetMotion.PlaybackSeconds(.2f), 1e-5f);
        Assert.AreEqual(2f, CutSheetMotion.PlaybackSpeed);
    }

    [Test]
    public void SettingsMenu_MotionRows_ShowStateOnlyValues_AndPersistChoices()
    {
        var root = new GameObject("Settings");
        try
        {
            var menu = root.AddComponent<SettingsMenuUI>();
            menu.reduceMotionDropdown = Dropdown(root, "ReduceMotionDropdown");
            menu.screenShakeDropdown = Dropdown(root, "ScreenShakeDropdown");
            menu.triggerSpeedDropdown = Dropdown(root, "TriggerSpeedDropdown");
            menu.tooltipTextDropdown = Dropdown(root, "TooltipTextDropdown");

            menu.RefreshMotionControls();
            CollectionAssert.AreEqual(new[] { "Off", "On" }, Texts(menu.reduceMotionDropdown));
            CollectionAssert.AreEqual(new[] { "1x", "1.5x", "2x" }, Texts(menu.triggerSpeedDropdown));
            CollectionAssert.AreEqual(new[] { "100%", "115%", "130%" }, Texts(menu.tooltipTextDropdown));
            Assert.AreEqual(0, menu.reduceMotionDropdown.value);
            Assert.AreEqual(1, menu.screenShakeDropdown.value);

            for (int i = 0; i < 3; i++)
            {
                menu.SelectTriggerSpeed(i);
                menu.SelectTooltipText(i);
                Assert.AreEqual(DisplaySettingsService.PlaybackSpeedOptions[i], DisplaySettingsService.PlaybackSpeed);
                Assert.AreEqual(DisplaySettingsService.TooltipTextScaleOptions[i], DisplaySettingsService.TooltipTextScale);
                Assert.AreEqual(i, PlayerPrefs.GetInt(DisplaySettingsService.TriggerSpeedKey));
            }
            menu.SelectReduceMotion(1);
            menu.SelectScreenShake(0);
            DisplaySettingsService.ReloadMotionSettings();
            menu.RefreshMotionControls();
            Assert.AreEqual(1, menu.reduceMotionDropdown.value);
            Assert.AreEqual(0, menu.screenShakeDropdown.value);
            Assert.AreEqual(2, menu.triggerSpeedDropdown.value);
            Assert.AreEqual(2, menu.tooltipTextDropdown.value);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static TMP_Dropdown Dropdown(GameObject root, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        return go.AddComponent<TMP_Dropdown>();
    }

    static List<string> Texts(TMP_Dropdown dropdown) => dropdown.options.ConvertAll(o => o.text);
}
