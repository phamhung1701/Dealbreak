using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "RunResultUI")]
public class RunResultUI : MonoBehaviour
{
    [Header("Encounter Feedback")]
    public GameObject rewardBanner;
    public TMP_Text rewardLabel;

    [Header("Reusable Results Screen")]
    public GameplayMenuCoordinator coordinator;
    public GameObject resultPanel;
    public TMP_Text resultTitleLabel;
    public TMP_Text resultBodyLabel;
    public Button retrySameSeedButton;
    public Button newRunButton;
    public Button mainMenuButton;

    string _lastHeadlinerRewardName;
    AttackCardPresentationUI _presentation;
    bool _pendingResult;
    bool _pendingResultVictory;
    bool _aborted;
    ScrollRect _summaryScroll;

    public static bool IsShowingResult { get; private set; }

    void OnEnable()
    {
        EnsurePresentationSubscription();
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEncounterResult += HandleEncounterResult;
            CombatManager.Instance.OnEnemyChanged += HandleEnemyChanged;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnHeadlinerRewardGranted += HandleHeadlinerRewardGranted;
            RunManager.Instance.OnRunCompleted += HandleRunCompleted;
            RunManager.Instance.OnRunStarted += HandleRunStarted;
        }
        if (retrySameSeedButton) { retrySameSeedButton.onClick.AddListener(RetrySameSeed); KeyButtonBorderStyle.Apply(retrySameSeedButton); }
        if (newRunButton) { newRunButton.onClick.AddListener(NewRun); KeyButtonBorderStyle.Apply(newRunButton); }
        if (mainMenuButton) mainMenuButton.onClick.AddListener(MainMenu);
        HideAll();
    }

    void OnDisable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEncounterResult -= HandleEncounterResult;
            CombatManager.Instance.OnEnemyChanged -= HandleEnemyChanged;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnHeadlinerRewardGranted -= HandleHeadlinerRewardGranted;
            RunManager.Instance.OnRunCompleted -= HandleRunCompleted;
            RunManager.Instance.OnRunStarted -= HandleRunStarted;
        }
        if (retrySameSeedButton) retrySameSeedButton.onClick.RemoveListener(RetrySameSeed);
        if (newRunButton) newRunButton.onClick.RemoveListener(NewRun);
        if (mainMenuButton) mainMenuButton.onClick.RemoveListener(MainMenu);
        UnsubscribeFromPresentation();
        HideAll();
    }

    void EnsurePresentationSubscription()
    {
        var current = AttackCardPresentationUI.Instance;
        if (_presentation == current) return;
        UnsubscribeFromPresentation();
        _presentation = current;
        if (_presentation != null)
        {
            _presentation.OnPresentationCompleted += HandlePresentationCompleted;
            _presentation.OnPresentationCancelled += HandlePresentationCancelled;
        }
    }

    void UnsubscribeFromPresentation()
    {
        if (_presentation == null) return;
        _presentation.OnPresentationCompleted -= HandlePresentationCompleted;
        _presentation.OnPresentationCancelled -= HandlePresentationCancelled;
        _presentation = null;
    }

    void HandleEncounterResult(EncounterResult result)
    {
        if (result == EncounterResult.Defeat) ShowWhenPresentationIdle(false);
        else if (result == EncounterResult.Aborted)
        {
            _aborted = true;
            ShowWhenPresentationIdle(false);
        }
    }

    void HandleHeadlinerRewardGranted(CardInstance reward)
    {
        _lastHeadlinerRewardName = reward != null ? reward.DisplayName : null;
        if (rewardBanner) rewardBanner.SetActive(reward != null);
        if (rewardLabel)
            rewardLabel.text = reward != null
                ? $"BOSS REWARD  •  {reward.DisplayName} added to your draw pile"
                : string.Empty;
    }

    void HandleRunCompleted() => ShowWhenPresentationIdle(true);

    void ShowWhenPresentationIdle(bool victory)
    {
        if (IsShowingResult || _pendingResult) return;
        EnsurePresentationSubscription();
        if (_presentation != null && _presentation.IsBusy)
        {
            _pendingResult = true;
            _pendingResultVictory = victory;
            return;
        }
        ShowResult(victory);
    }

    void HandlePresentationCompleted() => ShowPendingResult();
    void HandlePresentationCancelled() => ShowPendingResult();

    void ShowPendingResult()
    {
        if (!_pendingResult) return;
        bool victory = _pendingResultVictory;
        _pendingResult = false;
        _pendingResultVictory = false;
        ShowResult(victory);
    }

    void HandleEnemyChanged()
    {
        if (CombatManager.Instance != null && CombatManager.Instance.currentEnemy != null && rewardBanner)
            rewardBanner.SetActive(false);
    }

    void HandleRunStarted(string _)
    {
        _pendingResult = false;
        _pendingResultVictory = false;
        HideAll();
    }

    public void RetrySameSeed()
    {
        var run = RunManager.Instance;
        bool continueInfinite = run != null && run.CanContinueInfiniteMode;
        HideAll();
        if (continueInfinite) run.ContinueInfiniteMode();
        else run?.RestartRun();
    }

    public void ShowAbandonedRun() => ShowWhenPresentationIdle(false);

    public void NewRun()
    {
        HideAll();
        RunManager.Instance?.AbandonRun();
        GameFlowService.Instance?.ShowMainMenu(MainMenuDestination.RunSetup);
    }

    public void MainMenu()
    {
        // An aborted encounter keeps the saved run so Continue resumes from the last checkpoint.
        bool keepSavedRun = _aborted;
        HideAll();
        if (!keepSavedRun) RunManager.Instance?.AbandonRun();
        GameFlowService.Instance?.ShowMainMenu(MainMenuDestination.Home);
    }

    void ShowResult(bool victory)
    {
        if (IsShowingResult) return;
        var run = RunManager.Instance;
        var cards = CardManager.Instance;
        int totalHeadliners = run != null ? run.HeadlinerTotal : 12;
        int headlinersDefeated = run != null ? Mathf.Clamp(run.headlinerIndex, 0, totalHeadliners) : 0;
        int mapReached = victory ? totalHeadliners : run != null ? Mathf.Clamp(run.CurrentCycle, 1, totalHeadliners) : 0;
        string seed = run != null ? run.RunSeed : string.Empty;
        string gearItems = cards == null || cards.ownedGear.Count == 0
            ? "None"
            : string.Join(", ", cards.ownedGear.Where(value => value != null).Select(value => value.displayName));
        int enhanced = cards != null ? cards.ownedCards.Count(card => card.Enhancement != null) : 0;
        string creditsDisplay = cards == null ? "0" : cards.HasInfiniteMoney ? "∞" : cards.credits.ToString();

        if (resultTitleLabel)
        {
            resultTitleLabel.text = _aborted ? "ENCOUNTER HALTED" : victory ? "RUN VICTORY" : "RUN DEFEAT";
            resultTitleLabel.color = DealbreakUIStyle.Theme
                ? _aborted || victory ? DealbreakUIStyle.Theme.ochre : DealbreakUIStyle.Theme.paper
                : _aborted ? new Color(0.95f, 0.73f, 0.37f, 1f)
                : victory ? new Color(0.46f, 0.88f, 0.63f, 1f) : new Color(1f, 0.50f, 0.43f, 1f);
        }
        if (retrySameSeedButton)
        {
            retrySameSeedButton.gameObject.SetActive(run != null && run.CanContinueInfiniteMode);
            var retryLabel = retrySameSeedButton.GetComponentInChildren<TMP_Text>();
            if (retryLabel) retryLabel.text = run != null && run.CanContinueInfiniteMode
                ? "CONTINUE INFINITE MODE" : "RETRY SAME SEED";
        }
        if (resultBodyLabel)
        {
            resultBodyLabel.alignment = TextAlignmentOptions.TopLeft;
            resultBodyLabel.overflowMode = TextOverflowModes.Overflow;
            resultBodyLabel.text =
                (_aborted ? "A combat rule error stopped this fight. Main Menu keeps your run: Continue resumes from before the encounter.\n\n" : string.Empty) +
                $"<size=85%><b><color=#F2B95E>SEED</color></b></size>\n{EscapeRichText(seed)}\n\n" +
                $"<size=85%><b><color=#F2B95E>PROGRESS</color></b></size>\nDistrict {mapReached}/{totalHeadliners}  ·  Headliners defeated {headlinersDefeated}/{totalHeadliners}\n\n" +
                $"<size=85%><b><color=#F2B95E>RUN TOTALS</color></b></size>\nCredits  {creditsDisplay}\nGear  {EscapeRichText(gearItems)}\nMods applied  {enhanced}" +
                (victory && !string.IsNullOrEmpty(_lastHeadlinerRewardName)
                    ? $"\nFinal reward  {EscapeRichText(_lastHeadlinerRewardName)}" : string.Empty);
        }

        IsShowingResult = true;
        if (coordinator != null)
            coordinator.Push(this, resultPanel,
                retrySameSeedButton && retrySameSeedButton.gameObject.activeSelf ? retrySameSeedButton : newRunButton,
                GameplayInputBlockReason.RunResult);
        else if (resultPanel)
            resultPanel.SetActive(true);
        RefreshSummaryScroll();
    }

    static string EscapeRichText(string value) => string.IsNullOrEmpty(value)
        ? string.Empty
        : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    void RefreshSummaryScroll()
    {
        if (resultBodyLabel == null) return;
        _summaryScroll = resultBodyLabel.GetComponentInParent<ScrollRect>(true);
        if (_summaryScroll == null) return;
        _summaryScroll.content = resultBodyLabel.rectTransform;
        Canvas.ForceUpdateCanvases();
        _summaryScroll.verticalNormalizedPosition = 1f;
    }

    static string BuildTimingSummary(RunTimingStatistics timing, bool victory)
    {
        if (timing == null) return "Total Run Time  —";

        var summary = new StringBuilder();
        summary.Append("Total Run Time  ")
            .Append(RunDurationFormatter.Format(timing.TotalElapsedSeconds));
        for (int i = 0; i < timing.CompletedSplits.Count; i++)
        {
            var split = timing.CompletedSplits[i];
            summary.Append(i % 2 == 0 ? "\n" : "    ")
                .Append("District ")
                .Append(split.MapNumber)
                .Append("  ")
                .Append(RunDurationFormatter.Format(split.ElapsedSeconds));
        }

        if (!victory)
        {
            var incomplete = timing.CurrentIncompleteSplit;
            summary.Append("\nDistrict ")
                .Append(incomplete.MapNumber)
                .Append("  ")
                .Append(RunDurationFormatter.Format(incomplete.ElapsedSeconds))
                .Append(" (incomplete)");
        }
        return summary.ToString();
    }

    void HideAll()
    {
        _aborted = false;
        _pendingResult = false;
        _pendingResultVictory = false;
        _lastHeadlinerRewardName = null;
        if (rewardBanner) rewardBanner.SetActive(false);
        if (coordinator != null && coordinator.IsTop(this)) coordinator.Pop(this);
        else if (resultPanel) resultPanel.SetActive(false);
        IsShowingResult = false;
        GameplayInputGate.Set(GameplayInputBlockReason.RunResult, false);
    }
}
