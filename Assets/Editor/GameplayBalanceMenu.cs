using UnityEditor;
using UnityEngine;

public static class GameplayBalanceMenu
{
    const string EnableMenu = "Tools/Dealbreak/Balance/Record New Runs";
    const string EnabledPref = "Dealbreak.Balance.RecordRuns";

    [MenuItem(EnableMenu)]
    static void Toggle()
    {
        GameplayBalanceTelemetry.Enabled = !GameplayBalanceTelemetry.Enabled;
        EditorPrefs.SetBool(EnabledPref, GameplayBalanceTelemetry.Enabled);
        Debug.Log("Balance recording for new runs: " + GameplayBalanceTelemetry.Enabled);
    }

    [MenuItem(EnableMenu, true)]
    static bool ValidateToggle()
    {
        Menu.SetChecked(EnableMenu, GameplayBalanceTelemetry.Enabled);
        return true;
    }

    [MenuItem("Tools/Dealbreak/Balance/Export Current Journal")]
    static void Export()
    {
        if (GameplayBalanceTelemetry.TryExport()) Debug.Log(GameplayBalanceTelemetry.LastExportPath);
        else Debug.LogWarning("No journal exported. Enable recording, then start a new run.");
    }

    [MenuItem("Tools/Dealbreak/Balance/Open Export Folder")]
    static void OpenFolder() => EditorUtility.RevealInFinder(GameplayBalanceTelemetry.ExportDirectory);

    [InitializeOnLoadMethod]
    static void InstallLifecycle()
    {
        // The choice survives domain reloads and Play mode; recording is on until it is turned off.
        GameplayBalanceTelemetry.Enabled = EditorPrefs.GetBool(EnabledPref, true);
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) GameplayBalanceTelemetry.Finish("interrupted");
    }
}
