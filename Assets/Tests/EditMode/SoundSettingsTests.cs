using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class SoundSettingsTests
{
    static readonly string[] Keys = { SoundSettings.MusicKey, SoundSettings.SfxKey, SoundSettings.UiKey };
    readonly Dictionary<string, int?> _saved = new();

    [SetUp]
    public void SetUp()
    {
        // These cases test persistence itself, so they step out of the assembly-wide isolation.
        _saved.Clear();
        foreach (var key in Keys)
        {
            _saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            PlayerPrefs.DeleteKey(key);
        }
        SoundSettings.Isolated = false;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var pair in _saved)
        {
            if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value);
            else PlayerPrefs.DeleteKey(pair.Key);
        }
        PlayerPrefs.Save();
        SoundSettings.Isolated = true;
    }

    [Test]
    public void VolumeSteps_RiseFromSilenceToFull()
    {
        Assert.That(SoundSettings.GainForStep(0), Is.EqualTo(0f));
        Assert.That(SoundSettings.GainForStep(SoundSettings.MaxStep), Is.EqualTo(1f));
        for (int step = 1; step <= SoundSettings.MaxStep; step++)
            Assert.That(SoundSettings.GainForStep(step), Is.GreaterThan(SoundSettings.GainForStep(step - 1)));
    }

    [Test]
    public void StepLabels_CoverEveryStep()
    {
        Assert.That(SoundSettings.StepLabels.Length, Is.EqualTo(SoundSettings.MaxStep + 1));
    }

    [Test]
    public void SetStep_ClampsToTheRange()
    {
        SoundSettings.SetStep(AudioBus.Music, 99);
        Assert.That(SoundSettings.Step(AudioBus.Music), Is.EqualTo(SoundSettings.MaxStep));

        SoundSettings.SetStep(AudioBus.Music, -1);
        Assert.That(SoundSettings.Step(AudioBus.Music), Is.EqualTo(0));
    }

    [Test]
    public void SetStep_PersistsPerBus_AndSurvivesAReload()
    {
        SoundSettings.SetStep(AudioBus.Music, 1);
        SoundSettings.SetStep(AudioBus.Sfx, 2);
        SoundSettings.SetStep(AudioBus.Ui, 3);

        Assert.That(PlayerPrefs.GetInt(SoundSettings.SfxKey), Is.EqualTo(2));
        SoundSettings.Isolated = false; // drops the cache
        Assert.That(SoundSettings.Step(AudioBus.Music), Is.EqualTo(1));
        Assert.That(SoundSettings.Step(AudioBus.Sfx), Is.EqualTo(2));
        Assert.That(SoundSettings.Step(AudioBus.Ui), Is.EqualTo(3));
    }

    [Test]
    public void Isolated_NeverWritesPrefs()
    {
        SoundSettings.Isolated = true;

        SoundSettings.SetStep(AudioBus.Ui, 1);

        Assert.That(PlayerPrefs.HasKey(SoundSettings.UiKey), Is.False);
    }

    [Test]
    public void SettingsMenu_BuildsOneAudioRowPerBus_AndAPickPersists()
    {
        var root = new GameObject("Settings");
        try
        {
            var card = new GameObject("SettingsCard", typeof(RectTransform));
            card.transform.SetParent(root.transform, false);
            var label = new GameObject("TooltipTextLabel", typeof(RectTransform));
            label.transform.SetParent(card.transform, false);
            label.AddComponent<TextMeshProUGUI>();
            var row = new GameObject("TooltipTextDropdown", typeof(RectTransform));
            row.transform.SetParent(card.transform, false);
            var menu = root.AddComponent<SettingsMenuUI>();
            menu.tooltipTextDropdown = row.AddComponent<TMP_Dropdown>();

            menu.RefreshAudioControls();
            int builtChildren = card.transform.childCount;
            menu.RefreshAudioControls();

            Assert.That(builtChildren, Is.EqualTo(8), "Label and dropdown per bus, beside the two source parts");
            Assert.That(card.transform.childCount, Is.EqualTo(builtChildren), "Rows are built once");
            Assert.That(card.transform.Find("SfxVolumeLabel").GetComponent<TMP_Text>().text, Is.EqualTo("SFX Volume"));
            var music = card.transform.Find("MusicVolumeDropdown").GetComponent<TMP_Dropdown>();
            var ui = card.transform.Find("UiVolumeDropdown").GetComponent<TMP_Dropdown>();
            Assert.That(music.options.Count, Is.EqualTo(SoundSettings.MaxStep + 1));

            music.value = 3;

            Assert.That(SoundSettings.Step(AudioBus.Music), Is.EqualTo(3));
            Assert.That(PlayerPrefs.GetInt(SoundSettings.MusicKey), Is.EqualTo(3));
            Assert.That(SoundSettings.Step(AudioBus.Ui), Is.EqualTo(ui.value), "Each row drives only its own bus");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
