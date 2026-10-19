using System;
using System.IO;
using UnityEngine;

public static class SaveGameService
{
    internal static RunSaveStore StoreOverride;
    static RunSaveStore _store;
    public static RunSaveStore Store => StoreOverride ?? (_store ??= new RunSaveStore(Path.Combine(Application.persistentDataPath, "active-run.json")));
    public static string LastError { get; private set; }
    public static bool IsRestoring { get; private set; }
    public static bool HasValidSave => TryRead(out _, out _);
    public static bool TryRead(out RunSaveData data, out string error) => Store.TryRead(SaveContentCatalog.Load(), out data, out error);
    public static bool DeleteActiveSave()
    {
        bool success = Store.Delete(out string error);
        LastError = error;
        if (!success) Debug.LogWarning(error);
        return success;
    }
    public static void Attach(RunManager run)
    {
        if (Application.isPlaying && run != null && run.GetComponent<RunSaveAutosave>() == null)
            run.gameObject.AddComponent<RunSaveAutosave>();
    }
    public static bool Checkpoint(RunManager run, bool force = false)
    {
        var writer = run != null ? run.GetComponent<RunSaveAutosave>() : null;
        return writer != null && writer.Checkpoint(force);
    }
    public static void EndRun(RunManager run) => run?.GetComponent<RunSaveAutosave>()?.EndRun();
    public static void ResumeSaving(RunManager run) => run?.GetComponent<RunSaveAutosave>()?.StartSession();
    public static bool BeginNewRun(RunManager run)
    {
        if (run == null || run.GetComponent<RunSaveAutosave>() == null || DevModeRuntime.Enabled) return true;
        if (DeleteActiveSave()) return true;
        SavePromptUI.ShowError(LastError);
        return false;
    }
    public static bool Restore(RunManager run, RunSaveData data, out string error)
    {
        var catalog = SaveContentCatalog.Load();
        if (!RunSaveValidation.Validate(data, catalog, out error)) return false;
        IsRestoring = true;
        try { run.RestoreSave(data, catalog); run.PresentRestoredSave(); LastError = null; return true; }
        catch (Exception e)
        {
            run?.GetComponent<RunSaveAutosave>()?.Suspend();
            error = "Could not restore saved run: " + e.Message; LastError = error; return false;
        }
        finally { IsRestoring = false; }
    }
    internal static void ReportError(string error)
    {
        if (LastError != error)
        {
            Debug.LogWarning(error);
            if (Application.isPlaying) SavePromptUI.ShowError(error);
        }
        LastError = error;
    }
    internal static void ClearError() => LastError = null;
}

// Runs after synchronous gameplay commands. Never saves combat, resolver work or unfinished prompts.
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "RunSaveAutosave")]
public sealed class RunSaveAutosave : MonoBehaviour
{
    RunManager _run;
    CombatManager _combat;
    string _lastContent;
    float _nextPoll;
    bool _terminal;
    void OnEnable()
    {
        _run = GetComponent<RunManager>(); _combat = CombatManager.Instance;
        _run.OnRunCompleted += Terminal;
        _run.OnRunStarted += Started;
        if (_combat != null) _combat.OnEncounterResult += EncounterEnded;
    }
    void OnDisable()
    {
        if (_run != null) { _run.OnRunCompleted -= Terminal; _run.OnRunStarted -= Started; }
        if (_combat != null) _combat.OnEncounterResult -= EncounterEnded;
    }
    void Started(string _) => StartSession();
    public void StartSession() { _terminal = false; _lastContent = null; }
    public void Suspend() => _terminal = true;
    public void EndRun() => Terminal();
    // Defeat ends the run; an aborted encounter keeps the last checkpoint and stops writing.
    void EncounterEnded(EncounterResult result)
    {
        if (result == EncounterResult.Defeat) Terminal();
        else if (result == EncounterResult.Aborted) Suspend();
    }
    void Terminal()
    {
        _terminal = true;
        if (!DevModeRuntime.Enabled) SaveGameService.DeleteActiveSave();
    }
    void LateUpdate()
    {
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + 0.25f;
        Checkpoint(false);
    }
    void OnApplicationPause(bool paused) { if (paused) Checkpoint(true); }
    void OnApplicationQuit() => Checkpoint(true);
    public bool Checkpoint(bool force)
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != GameFlowService.GameSceneName ||
            _terminal || SaveGameService.IsRestoring || DevModeRuntime.Enabled || _run == null || !_run.CanCaptureSave) return false;
        try
        {
            var snapshot = _run.CaptureSave();
            var timing = snapshot.timing;
            snapshot.timing = null;
            string content = JsonUtility.ToJson(snapshot);
            snapshot.timing = timing;
            if (!force && content == _lastContent) return true;
            if (!SaveGameService.Store.Write(snapshot, SaveContentCatalog.Load(), out string error))
            { SaveGameService.ReportError(error); return false; }
            _lastContent = content; SaveGameService.ClearError(); return true;
        }
        catch (Exception e) { SaveGameService.ReportError("Run save failed: " + e.Message); return false; }
    }
}
