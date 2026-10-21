using UnityEngine;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "MainMenuUI")]
public sealed class MainMenuUI : MonoBehaviour
{
    public FrontendMenuNavigator navigator;
    public Button newRunButton;
    public Button settingsButton;
    public Button creditsButton;
    public Button quitButton;
    /// <summary>Replays the tutorial; built at runtime from the NEW RUN button.</summary>
    public Button TutorialButton { get; private set; }

    void OnEnable()
    {
        EnsureTutorialButton();
        RunSaveMenuUI.Install(this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevModePanelUI.InstallMainMenu(this);
#endif
        if (newRunButton) newRunButton.onClick.AddListener(ShowRunSetup);
        if (settingsButton) settingsButton.onClick.AddListener(ShowSettings);
        if (creditsButton) creditsButton.onClick.AddListener(ShowCredits);
        if (quitButton) quitButton.onClick.AddListener(Quit);
        if (TutorialButton) TutorialButton.onClick.AddListener(StartTutorial);
    }

    void OnDisable()
    {
        if (newRunButton) newRunButton.onClick.RemoveListener(ShowRunSetup);
        if (settingsButton) settingsButton.onClick.RemoveListener(ShowSettings);
        if (creditsButton) creditsButton.onClick.RemoveListener(ShowCredits);
        if (quitButton) quitButton.onClick.RemoveListener(Quit);
        if (TutorialButton) TutorialButton.onClick.RemoveListener(StartTutorial);
    }

    void EnsureTutorialButton()
    {
        if (TutorialButton || !newRunButton) return;
        TutorialButton = Instantiate(newRunButton, newRunButton.transform.parent);
        TutorialButton.name = "TutorialButton";
        TutorialButton.onClick = new Button.ButtonClickedEvent();
        TutorialButton.transform.SetSiblingIndex(newRunButton.transform.GetSiblingIndex() + 1);
        var label = TutorialButton.GetComponentInChildren<TMPro.TMP_Text>();
        if (label) label.text = "TUTORIAL";
    }

    // A player who has never finished or skipped the tutorial is taken there by NEW RUN.
    public void ShowRunSetup()
    {
        if (!TutorialProgress.Completed && GameFlowService.Instance != null) StartTutorial();
        else navigator?.ShowRunSetup();
    }

    public void StartTutorial() => GameFlowService.Instance?.StartTutorial();
    public void ShowSettings() => navigator?.ShowSettings();
    public void ShowCredits() => navigator?.ShowCredits();
    public void Quit() => GameFlowService.Instance?.Quit();
}
