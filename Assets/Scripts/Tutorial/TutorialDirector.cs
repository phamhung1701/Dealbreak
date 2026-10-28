using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the tutorial: starts the scripted run, walks TutorialScript's steps one at a time, sets
/// each step's command filter, and shows it through the coach once the board is idle. Finishing
/// or skipping marks the tutorial done and returns to the main menu. It only observes gameplay
/// through manager events and state; every command still goes through the normal routes.
/// </summary>
public sealed class TutorialDirector : MonoBehaviour
{
    public static TutorialDirector Instance { get; private set; }

    readonly Dictionary<ConsumableEffectType, int> _tricksUsed = new();
    List<TutorialStep> _steps = new();
    TutorialCoachUI _coach;
    int _index;
    bool _entered, _ended;

    public RunManager Run { get; private set; }
    public TutorialRunDefinition Definition { get; private set; }
    public CombatManager Combat => CombatManager.Instance;
    public CardManager Cards => CardManager.Instance;
    public IReadOnlyList<TutorialStep> Steps => _steps;
    public TutorialStep CurrentStep => _index < _steps.Count ? _steps[_index] : null;
    public int StepIndex => _index;
    public TutorialCoachUI Coach => _coach;

    public int PerfectBlocks { get; private set; }
    public int Victories { get; private set; }
    public bool HeadlinerRewardGranted { get; private set; }
    public CardInstance HeadlinerReward { get; private set; }
    public int TricksUsed(ConsumableEffectType type) => _tricksUsed.TryGetValue(type, out int used) ? used : 0;

    /// <summary>Starts the tutorial run and its director; on failure, returns to the main menu.</summary>
    public static TutorialDirector Begin(RunManager run, TutorialRunDefinition definition)
    {
        if (run == null || !run.StartTutorialRun(definition))
        {
            GameFlowService.Instance?.ShowMainMenu();
            return null;
        }
        if (Instance) Instance.End();
        var director = new GameObject("[TutorialDirector]").AddComponent<TutorialDirector>();
        director.Initialize(run, definition);
        return director;
    }

    void Initialize(RunManager run, TutorialRunDefinition definition)
    {
        Instance = this;
        Run = run;
        Definition = definition;
        _steps = TutorialScript.Build(this);
        Subscribe(true);
        var actions = FindFirstObjectByType<ActionButtonsUI>();
        var canvas = actions != null ? actions.GetComponentInParent<Canvas>() : null;
        _coach = TutorialCoachUI.Install(canvas);
        if (_coach)
        {
            _coach.NextPressed += Next;
            _coach.SkipPressed += Skip;
        }
        TutorialGate.Refused += HandleRefused;
    }

    void Subscribe(bool on)
    {
        var combat = Combat;
        if (combat != null)
        {
            if (on) { combat.OnDefenseAttackResolved += HandleDefense; combat.OnEncounterResult += HandleEncounterResult; }
            else { combat.OnDefenseAttackResolved -= HandleDefense; combat.OnEncounterResult -= HandleEncounterResult; }
        }
        if (Cards != null)
        {
            if (on) Cards.OnConsumableUsed += HandleConsumableUsed;
            else Cards.OnConsumableUsed -= HandleConsumableUsed;
        }
        if (Run != null)
        {
            if (on) Run.OnHeadlinerRewardGranted += HandleHeadlinerReward;
            else Run.OnHeadlinerRewardGranted -= HandleHeadlinerReward;
        }
    }

    void HandleDefense(DefenseAttackResult result) { if (result.IsPerfectBlock) PerfectBlocks++; }
    void HandleConsumableUsed(ConsumableData trick) { if (trick) _tricksUsed[trick.effectType] = TricksUsed(trick.effectType) + 1; }
    void HandleHeadlinerReward(CardInstance reward) { HeadlinerRewardGranted = true; HeadlinerReward = reward; }
    void HandleRefused(TutorialCommand _) { if (_coach && CurrentStep?.Refusal != null) _coach.Refuse(CurrentStep.Refusal()); }

    void HandleEncounterResult(EncounterResult result)
    {
        if (result == EncounterResult.Victory) Victories++;
        // A lost or halted fight ends the lesson; the run result screen takes over.
        else End();
    }

    /// <summary>Nothing is animating and no menu holds the board.</summary>
    public static bool BoardIdle
    {
        get
        {
            var enemies = FindFirstObjectByType<EnemyGroupUI>();
            return !GameplayInputGate.IsBlocked && !DealbreakUIRuntime.BoardPresentationBusy &&
                !(enemies && enemies.IsDefeatPresentationBusy);
        }
    }

    void Update() => Tick();

    /// <summary>One frame of the tutorial: advance finished steps, then show or hide the current one.</summary>
    internal void Tick()
    {
        if (_ended) return;
        // Steps that complete or no longer apply fall through in the same frame.
        for (int guard = 0; guard < _steps.Count && _index < _steps.Count; guard++)
        {
            var step = _steps[_index];
            if (!_entered)
            {
                if (step.Skip?.Invoke() == true) { Advance(); continue; }
                _entered = true;
                step.Enter?.Invoke();
                TutorialGate.SetFilter(step.Filter);
            }
            if (step.Skip?.Invoke() == true || step.Done?.Invoke() == true) { Advance(); continue; }
            break;
        }
        if (_index >= _steps.Count) { End(); return; }
        if (!_coach) return;
        var current = _steps[_index];
        if (BoardIdle && (current.Ready?.Invoke() ?? true))
        {
            if (!_coach.IsVisible || _coach.Step != current) _coach.Show(current, _index, _steps.Count);
        }
        else if (_coach.IsVisible) _coach.Hide();
    }

    void Advance()
    {
        _index++;
        _entered = false;
        TutorialGate.Clear();
    }

    /// <summary>Closes the current explanation; the last one finishes the tutorial.</summary>
    public void Next()
    {
        var step = CurrentStep;
        if (step == null || !step.IsModal) return;
        if (_index == _steps.Count - 1) { Finish(); return; }
        Advance();
    }

    public void Skip() => Finish();

    void Finish()
    {
        TutorialProgress.MarkCompleted();
        End();
        GameFlowService.Instance?.ShowMainMenu();
    }

    public bool HasEnded => _ended;

    public void End()
    {
        if (_ended) return;
        _ended = true;
        TutorialGate.Clear();
        TutorialGate.Refused -= HandleRefused;
        Subscribe(false);
        if (_coach)
        {
            _coach.NextPressed -= Next;
            _coach.SkipPressed -= Skip;
            if (Application.isPlaying) Destroy(_coach.gameObject);
            else DestroyImmediate(_coach.gameObject);
            _coach = null;
        }
    }

    void OnDestroy()
    {
        End();
        if (Instance == this) Instance = null;
    }
}
