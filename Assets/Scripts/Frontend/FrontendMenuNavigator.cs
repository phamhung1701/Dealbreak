using UnityEngine;

public sealed class FrontendMenuNavigator : MonoBehaviour
{
    public GameObject homePanel;
    public GameObject runSetupPanel;
    public GameObject creditsPanel;
    public RunSetupUI runSetupUI;
    public SettingsMenuUI settingsMenu;

    void Start()
    {
        switch (FrontendLaunchContext.ConsumeMainMenuDestination())
        {
            case MainMenuDestination.RunSetup:
                ShowRunSetup();
                break;
            default:
                ShowHome();
                break;
        }
    }

    public void ShowHome()
    {
        HideBasePanels();
        if (homePanel) homePanel.SetActive(true);
    }

    public void ShowRunSetup()
    {
        HideBasePanels();
        if (runSetupPanel) runSetupPanel.SetActive(true);
        runSetupUI?.Prepare();
    }

    public void ShowCredits()
    {
        HideBasePanels();
        if (creditsPanel) creditsPanel.SetActive(true);
    }

    public void ShowSettings()
    {
        HideBasePanels();
        settingsMenu?.Open(ShowHome);
    }

    void HideBasePanels()
    {
        if (homePanel) homePanel.SetActive(false);
        if (runSetupPanel) runSetupPanel.SetActive(false);
        if (creditsPanel) creditsPanel.SetActive(false);
    }
}
