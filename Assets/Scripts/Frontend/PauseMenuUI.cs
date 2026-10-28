using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PauseMenuUI : MonoBehaviour
{
    public GameplayMenuCoordinator coordinator;
    public GameObject panel;
    public Button pauseButton;
    public Button resumeButton;
    public Button settingsButton;
    public Button restartButton;
    public Button abandonButton;
    public SettingsMenuUI settingsMenu;
    public ConfirmationDialogUI confirmationDialog;

    InputAction _pauseAction;
    int _lastHandledFrame = -1;

    public bool IsOpen => coordinator != null && coordinator.Contains(this);

    void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevModePanelUI.InstallGame(this);
#endif
        if (pauseButton)
        {
            pauseButton.onClick.AddListener(OpenPause);
            KeyButtonBorderStyle.Apply(pauseButton);
            var style=pauseButton.GetComponent<EditorialButton>();
            if(style)style.emphasized=true;
        }
        if (resumeButton) { resumeButton.onClick.AddListener(Resume); KeyButtonBorderStyle.Apply(resumeButton); }
        if (settingsButton) settingsButton.onClick.AddListener(OpenSettings);
        if (restartButton) restartButton.onClick.AddListener(BackToMainMenu);
        if (abandonButton) abandonButton.onClick.AddListener(ConfirmAbandon);
        if (panel) panel.SetActive(false);

        _pauseAction = new InputAction("Pause", InputActionType.Button);
        _pauseAction.AddBinding("<Keyboard>/escape");
        _pauseAction.AddBinding("<Gamepad>/start");
        _pauseAction.performed += HandlePausePerformed;
        _pauseAction.Enable();
    }

    void OnDisable()
    {
        if (pauseButton) pauseButton.onClick.RemoveListener(OpenPause);
        if (resumeButton) resumeButton.onClick.RemoveListener(Resume);
        if (settingsButton) settingsButton.onClick.RemoveListener(OpenSettings);
        if (restartButton) restartButton.onClick.RemoveListener(BackToMainMenu);
        if (abandonButton) abandonButton.onClick.RemoveListener(ConfirmAbandon);
        if (_pauseAction != null)
        {
            _pauseAction.performed -= HandlePausePerformed;
            _pauseAction.Disable();
            _pauseAction.Dispose();
            _pauseAction = null;
        }
        if (coordinator != null && coordinator.Contains(this)) coordinator.ClearAll();
    }

    void HandlePausePerformed(InputAction.CallbackContext _)
    {
        if (_lastHandledFrame == Time.frameCount) return;
        _lastHandledFrame = Time.frameCount;

        if (confirmationDialog != null && confirmationDialog.IsOpen)
        {
            confirmationDialog.Cancel();
            return;
        }
        if (settingsMenu != null && settingsMenu.IsOpen && coordinator != null && coordinator.IsTop(settingsMenu))
        {
            settingsMenu.Back();
            return;
        }
        if (IsOpen && coordinator.IsTop(this))
        {
            Resume();
            return;
        }
        if (coordinator != null && !coordinator.HasBlockingMenu)
        {
            var market=Object.FindFirstObjectByType<ShopUI>();
            if(market&&market.storeComposition&&market.panel&&market.panel.activeInHierarchy&&!string.IsNullOrEmpty(market.SelectedOfferId))
            {market.ClearOfferSelection();return;}
            if (ContextualTooltipUI.Instance && ContextualTooltipUI.Instance.DismissPinned()) return;
            OpenPause();
        }
    }

    public void OpenPause()
    {
        if (coordinator == null || panel == null || coordinator.HasBlockingMenu) return;
        if (RunManager.Instance == null || RunManager.Instance.IsRunCompleted) return;
        if (CombatManager.Instance != null && CombatManager.Instance.currentState == GameState.GameOver) return;
        coordinator.Push(this, panel, resumeButton);
    }

    public void Resume()
    {
        if (coordinator != null && coordinator.IsTop(this)) coordinator.Pop(this);
    }

    public void OpenSettings()
    {
        if (!IsOpen || settingsMenu == null || coordinator == null) return;
        settingsMenu.Open(coordinator, () => { });
    }

    public void ConfirmRestart()
    {
        BackToMainMenu(); // Retain the legacy callback for serialized bindings.
    }

    public void BackToMainMenu()
    {
        if (!IsOpen || GameFlowService.Instance == null) return;
        var run = RunManager.Instance;
        if (run != null && run.CanCaptureSave)
        {
            if (!SaveGameService.Checkpoint(run, true))
            { SavePromptUI.ShowError(SaveGameService.LastError ?? "Could not save this checkpoint."); return; }
            GameFlowService.Instance.ShowMainMenu(MainMenuDestination.Home);
        }
        else if (confirmationDialog)
            confirmationDialog.Open(coordinator, "BACK TO MAIN MENU?",
                "This encounter cannot be saved. Continue will return to the last saved checkpoint.",
                "MAIN MENU", () => GameFlowService.Instance.ShowMainMenu(MainMenuDestination.Home));
    }

    public void ConfirmAbandon()
    {
        if (!IsOpen || confirmationDialog == null) return;
        confirmationDialog.Open(
            coordinator,
            "ABANDON RUN?",
            "End this run in defeat? The current run save will be removed.",
            "ABANDON RUN",
            AbandonRun);
    }

    void AbandonRun()
    {
        if (coordinator != null) coordinator.ClearAll();
        AttackCardPresentationUI.Instance?.CancelAll();
        RunManager.Instance?.AbandonRun();
        var result = Object.FindFirstObjectByType<RunResultUI>();
        if (result != null) result.ShowAbandonedRun();
    }
}
