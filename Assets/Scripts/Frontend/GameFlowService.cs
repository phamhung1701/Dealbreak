using UnityEngine;
using UnityEngine.SceneManagement;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "GameFlowService")]
public sealed class GameFlowService : MonoBehaviour
{
    public const string MainMenuSceneName = "MainMenu";
    public const string GameSceneName = "Game";

    public static GameFlowService Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        DisplaySettingsService.LoadAndApply();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void StartRun(string seed)
    {
        if (SavePromptUI.IsOpen) return;
        if (SaveGameService.Store.HasActiveFiles)
        {
            SavePromptUI.AskReplacement(() => StartConfirmedRun(seed));
            return;
        }
        StartConfirmedRun(seed);
    }

    void StartConfirmedRun(string seed)
    {
        if (!SaveGameService.DeleteActiveSave()) { SavePromptUI.ShowError(SaveGameService.LastError); return; }
        DevModeRuntime.Disable();
        LoadRun(seed);
    }

    public bool ContinueRun()
    {
        if (SavePromptUI.IsOpen) return false;
        if (!SaveGameService.TryRead(out var data, out string error))
        { SavePromptUI.ShowError(error); return false; }
        DevModeRuntime.Disable();
        RestoreGlobalRuntimeState();
        FrontendLaunchContext.RequestContinue(data);
        LoadingScreenUI.Show();
        SceneManager.LoadScene(GameSceneName);
        return true;
    }

    /// <summary>Starts the scripted tutorial run. It never reads, writes or deletes the run save.</summary>
    public void StartTutorial()
    {
        if (SavePromptUI.IsOpen) return;
        DevModeRuntime.Disable();
        RestoreGlobalRuntimeState();
        FrontendLaunchContext.RequestTutorial();
        LoadingScreenUI.Show();
        SceneManager.LoadScene(GameSceneName);
    }

    public void StartDevRun(string seed, bool infiniteHealth, bool infiniteMoney, bool unlimitedShopOffers)
    {
        DevModeRuntime.Configure(true, infiniteHealth, infiniteMoney, unlimitedShopOffers);
        LoadRun(seed);
    }

    void LoadRun(string seed)
    {
        RestoreGlobalRuntimeState();
        FrontendLaunchContext.RequestRun(seed);
        LoadingScreenUI.Show();
        SceneManager.LoadScene(GameSceneName);
    }

    public void ShowMainMenu(MainMenuDestination destination = MainMenuDestination.Home)
    {
        if (!TryCheckpointBeforeExit()) return;
        DevModeRuntime.Disable();
        RestoreGlobalRuntimeState();
        FrontendLaunchContext.RequestMainMenu(destination);
        LoadingScreenUI.Show();
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public void Quit()
    {
        if (!TryCheckpointBeforeExit()) return;
        RestoreGlobalRuntimeState();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static bool TryCheckpointBeforeExit()
    {
        // A false result at an unsafe combat boundary is expected: keep the last safe save
        // and allow exit if no prior save failure remains outstanding. A failed attempted
        // write leaves LastError set, so do not discard that warning before checking it.
        bool checkpointed = SaveGameService.Checkpoint(RunManager.Instance, true);
        string error = SaveGameService.LastError;
        if (!ShouldAbortExit(checkpointed, error)) return true;
        SavePromptUI.ShowError(error);
        return false;
    }

    internal static bool ShouldAbortExit(bool checkpointed, string error) =>
        !checkpointed && !string.IsNullOrEmpty(error);

    public static void RestoreGlobalRuntimeState()
    {
        Time.timeScale = 1f;
        GameplayInputGate.Clear();
    }
}
