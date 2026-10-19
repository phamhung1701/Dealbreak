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
    // A snapshot is captured only after gameplay reports a change, plus a slow fallback poll,
    // instead of serializing the whole run four times per second.
    const float PollInterval = 0.25f;
    const float FallbackInterval = 5f;
    RunManager _run;
    CombatManager _combat;
    CardManager _cards;
    string _lastContent;
    float _nextPoll;
    float _nextFallbackPoll;
    bool _dirty = true;
    bool _terminal;
    void OnEnable()
    {
        _run = GetComponent<RunManager>(); _combat = CombatManager.Instance; _cards = CardManager.Instance;
        _run.OnRunCompleted += Terminal;
        _run.OnRunStarted += Started;
        _run.OnShowPathScreen += MarkDirty; _run.OnShowShop += MarkDirty; _run.OnHideShop += MarkDirty;
        _run.OnShowEvent += MarkDirty; _run.OnHideEvent += MarkDirty;
        _run.OnShopOffersChanged += MarkDirty; _run.OnMapRevealChanged += MarkDirty;
        _run.OnConsumableRewardChanged += MarkDirty; _run.OnCycleStarted += MarkDirty;
        if (_combat != null)
        {
            _combat.OnEncounterResult += EncounterEnded;
            _combat.OnStateChanged += MarkDirty; _combat.OnPlayerHealthChanged += MarkDirty;
        }
        if (_cards != null)
        {
            _cards.OnCreditsChanged += MarkDirty; _cards.OnDeckChanged += MarkDirty;
            _cards.OnBuildChanged += MarkDirty; _cards.OnConsumablesChanged += MarkDirty;
        }
    }
    void OnDisable()
    {
        if (_run != null)
        {
            _run.OnRunCompleted -= Terminal; _run.OnRunStarted -= Started;
            _run.OnShowPathScreen -= MarkDirty; _run.OnShowShop -= MarkDirty; _run.OnHideShop -= MarkDirty;
            _run.OnShowEvent -= MarkDirty; _run.OnHideEvent -= MarkDirty;
            _run.OnShopOffersChanged -= MarkDirty; _run.OnMapRevealChanged -= MarkDirty;
            _run.OnConsumableRewardChanged -= MarkDirty; _run.OnCycleStarted -= MarkDirty;
        }
        if (_combat != null)
        {
            _combat.OnEncounterResult -= EncounterEnded;
            _combat.OnStateChanged -= MarkDirty; _combat.OnPlayerHealthChanged -= MarkDirty;
        }
        if (_cards != null)
        {
            _cards.OnCreditsChanged -= MarkDirty; _cards.OnDeckChanged -= MarkDirty;
            _cards.OnBuildChanged -= MarkDirty; _cards.OnConsumablesChanged -= MarkDirty;
        }
    }
    internal bool IsDirty { get => _dirty; set => _dirty = value; }
    void MarkDirty() => _dirty = true;
    void MarkDirty(int _) => _dirty = true;
    void MarkDirty(int _, int __) => _dirty = true;
    void MarkDirty(string _) => _dirty = true;
    void MarkDirty(GameState _) => _dirty = true;
    void MarkDirty(RunEventDefinition _) => _dirty = true;
    void Started(string _) => StartSession();
    public void StartSession() { _terminal = false; _lastContent = null; _dirty = true; }
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
        float now = Time.unscaledTime;
        if (now < _nextPoll) return;
        bool fallbackDue = now >= _nextFallbackPoll;
        if (!_dirty && !fallbackDue) return;
        _nextPoll = now + PollInterval;
        if (fallbackDue) _nextFallbackPoll = now + FallbackInterval;
        // Stay dirty while the run is not at a safe checkpoint (for example mid-combat), and retry.
        if (Checkpoint(false)) _dirty = false;
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
