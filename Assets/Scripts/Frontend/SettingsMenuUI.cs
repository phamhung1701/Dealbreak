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

    DisplayResolution[] _resolutions = Array.Empty<DisplayResolution>();
    GameplayMenuCoordinator _coordinator;
    Action _onBack;

    public bool IsOpen => panel != null && panel.activeSelf;

    void OnEnable()
    {
        if (applyButton) { applyButton.onClick.AddListener(Apply); KeyButtonBorderStyle.Apply(applyButton); }
        if (backButton) backButton.onClick.AddListener(Back);
        if (panel) panel.SetActive(false);
    }

    void OnDisable()
    {
        if (applyButton) applyButton.onClick.RemoveListener(Apply);
        if (backButton) backButton.onClick.RemoveListener(Back);
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
    }
}
