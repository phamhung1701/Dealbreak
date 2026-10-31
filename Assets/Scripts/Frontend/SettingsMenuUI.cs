using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingsMenuUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown displayModeDropdown;
    public Toggle vSyncToggle;
    public Button applyButton;
    public Button backButton;
    public TMP_Text statusLabel;
    public TMP_Dropdown reduceMotionDropdown;
    public TMP_Dropdown screenShakeDropdown;
    public TMP_Dropdown triggerSpeedDropdown;
    public TMP_Dropdown tooltipTextDropdown;

    public static readonly string[] OnOffOptions = { "Off", "On" };
    public static readonly string[] TriggerSpeedOptions = { "1x", "1.5x", "2x" };
    public static readonly string[] TooltipTextOptions = { "100%", "115%", "130%" };

    // The audio rows stand in their own column right of the card's rows (which end at x 325), built
    // at runtime from the Tooltip Text row so both scenes share one layout. 1080p-canvas pixels.
    public const float AudioLabelX = 500f, AudioDropdownX = 800f, AudioDropdownWidth = 300f;
    static readonly (string name, string label, AudioBus bus, float y)[] AudioRowLayout =
    {
        ("MusicVolume", "Music Volume", AudioBus.Music, 155f),
        ("SfxVolume", "SFX Volume", AudioBus.Sfx, 85f),
        ("UiVolume", "UI Volume", AudioBus.Ui, 15f),
    };
    readonly TMP_Dropdown[] _audioRows = new TMP_Dropdown[AudioRowLayout.Length];

    DisplayResolution[] _resolutions = Array.Empty<DisplayResolution>();
    GameplayMenuCoordinator _coordinator;
    Action _onBack;

    public bool IsOpen => panel != null && panel.activeSelf;

    void OnEnable()
    {
        if (applyButton) { applyButton.onClick.AddListener(Apply); KeyButtonBorderStyle.Apply(applyButton); }
        if (backButton) backButton.onClick.AddListener(Back);
        if (reduceMotionDropdown) reduceMotionDropdown.onValueChanged.AddListener(SelectReduceMotion);
        if (screenShakeDropdown) screenShakeDropdown.onValueChanged.AddListener(SelectScreenShake);
        if (triggerSpeedDropdown) triggerSpeedDropdown.onValueChanged.AddListener(SelectTriggerSpeed);
        if (tooltipTextDropdown) tooltipTextDropdown.onValueChanged.AddListener(SelectTooltipText);
        if (panel) panel.SetActive(false);
    }

    void OnDisable()
    {
        if (applyButton) applyButton.onClick.RemoveListener(Apply);
        if (backButton) backButton.onClick.RemoveListener(Back);
        if (reduceMotionDropdown) reduceMotionDropdown.onValueChanged.RemoveListener(SelectReduceMotion);
        if (screenShakeDropdown) screenShakeDropdown.onValueChanged.RemoveListener(SelectScreenShake);
        if (triggerSpeedDropdown) triggerSpeedDropdown.onValueChanged.RemoveListener(SelectTriggerSpeed);
        if (tooltipTextDropdown) tooltipTextDropdown.onValueChanged.RemoveListener(SelectTooltipText);
        _onBack = null;
        _coordinator = null;
    }

    public void Open(Action onBack) => Open(null, onBack);

    public void Open(GameplayMenuCoordinator coordinator, Action onBack)
    {
        if (panel == null) return;
        _coordinator = coordinator;
        _onBack = onBack;
        RefreshControls();
        if (coordinator != null) coordinator.Push(this, panel, backButton);
        else panel.SetActive(true);
    }

    public void Back()
    {
        var callback = _onBack;
        _onBack = null;
        if (_coordinator != null && _coordinator.IsTop(this)) _coordinator.Pop(this);
        else if (panel) panel.SetActive(false);
        _coordinator = null;
        callback?.Invoke();
    }

    public void Apply()
    {
        if (_resolutions.Length == 0) RefreshControls();
        if (_resolutions.Length == 0) return;
        int resolutionIndex = Mathf.Clamp(resolutionDropdown != null ? resolutionDropdown.value : 0, 0, _resolutions.Length - 1);
        int modeIndex = Mathf.Clamp(displayModeDropdown != null ? displayModeDropdown.value : 0, 0, DisplaySettingsService.SupportedModes.Length - 1);
        var resolution = _resolutions[resolutionIndex];
        var settings = new DisplaySettingsData(
            resolution.Width,
            resolution.Height,
            DisplaySettingsService.SupportedModes[modeIndex],
            vSyncToggle != null && vSyncToggle.isOn ? 1 : 0);
        DisplaySettingsService.Apply(settings);
        if (statusLabel) statusLabel.text = "Display settings applied.";
    }

    void RefreshControls()
    {
        _resolutions = DisplaySettingsService.GetAvailableResolutions().ToArray();
        var current = DisplaySettingsService.Load();

        if (resolutionDropdown)
        {
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(_resolutions.Select(value => value.ToString()).ToList());
            int index = Array.FindIndex(_resolutions, value => value.Width == current.Width && value.Height == current.Height);
            resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, index));
            resolutionDropdown.RefreshShownValue();
        }

        if (displayModeDropdown)
        {
            displayModeDropdown.ClearOptions();
            displayModeDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Windowed",
                "Borderless Fullscreen",
                "Exclusive Fullscreen"
            });
            int index = Array.IndexOf(DisplaySettingsService.SupportedModes, current.Mode);
            displayModeDropdown.SetValueWithoutNotify(Mathf.Max(0, index));
            displayModeDropdown.RefreshShownValue();
        }

        if (vSyncToggle) vSyncToggle.SetIsOnWithoutNotify(current.VSyncCount > 0);
        if (statusLabel) statusLabel.text = string.Empty;
        RefreshMotionControls();
        RefreshAudioControls();
    }

    public void RefreshAudioControls()
    {
        EnsureAudioRows();
        foreach (var (_, _, bus, _) in AudioRowLayout)
            Fill(_audioRows[(int)bus], SoundSettings.StepLabels, SoundSettings.Step(bus));
    }

    void EnsureAudioRows()
    {
        if (!tooltipTextDropdown || _audioRows[0]) return;
        var card = tooltipTextDropdown.transform.parent;
        var label = card.Find("TooltipTextLabel");
        if (!label) return;
        foreach (var (name, text, bus, y) in AudioRowLayout)
        {
            var rowLabel = Instantiate(label.gameObject, card, false);
            rowLabel.name = name + "Label";
            if (rowLabel.TryGetComponent(out TMP_Text labelText)) labelText.text = text;
            ((RectTransform)rowLabel.transform).anchoredPosition = new Vector2(AudioLabelX, y);
            var row = Instantiate(tooltipTextDropdown, card, false);
            row.name = name + "Dropdown";
            var rect = (RectTransform)row.transform;
            rect.anchoredPosition = new Vector2(AudioDropdownX, y);
            rect.sizeDelta = new Vector2(AudioDropdownWidth, rect.sizeDelta.y);
            row.onValueChanged.AddListener(step => SelectVolume(bus, step));
            _audioRows[(int)bus] = row;
        }
    }

    // A volume applies and persists as soon as it is picked; a sample plays at the new level.
    public void SelectVolume(AudioBus bus, int step)
    {
        SoundSettings.SetStep(bus, step);
        if (bus == AudioBus.Sfx) AudioService.Play(AudioCue.ItemSlap);
        else if (bus == AudioBus.Ui) AudioService.Play(AudioCue.UiPress);
    }

    // Motion rows apply and persist as soon as a value is picked; Apply only covers the display rows.
    public void SelectReduceMotion(int index) => DisplaySettingsService.ReduceMotion = index > 0;
    public void SelectScreenShake(int index) => DisplaySettingsService.ScreenShake = index > 0;
    public void SelectTriggerSpeed(int index) => DisplaySettingsService.PlaybackSpeedIndex = index;
    public void SelectTooltipText(int index) => DisplaySettingsService.TooltipTextSizeIndex = index;

    public void RefreshMotionControls()
    {
        Fill(reduceMotionDropdown, OnOffOptions, DisplaySettingsService.ReduceMotion ? 1 : 0);
        Fill(screenShakeDropdown, OnOffOptions, DisplaySettingsService.ScreenShake ? 1 : 0);
        Fill(triggerSpeedDropdown, TriggerSpeedOptions, DisplaySettingsService.PlaybackSpeedIndex);
        Fill(tooltipTextDropdown, TooltipTextOptions, DisplaySettingsService.TooltipTextSizeIndex);
    }

    static void Fill(TMP_Dropdown dropdown, string[] options, int index)
    {
        if (!dropdown) return;
        dropdown.ClearOptions();
        dropdown.AddOptions(options.ToList());
        dropdown.SetValueWithoutNotify(Mathf.Clamp(index, 0, options.Length - 1));
        dropdown.RefreshShownValue();
    }
}
