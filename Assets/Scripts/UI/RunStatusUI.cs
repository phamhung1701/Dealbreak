using System.Text;
using TMPro;
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "RunStatusUI")]
public class RunStatusUI : MonoBehaviour
{
    [Header("Run Resources")]
    [UnityEngine.Serialization.FormerlySerializedAs("goldLabel")]
    public TMP_Text creditsLabel;
    public TMP_Text pileSummaryLabel;
    public TMP_Text runProgressLabel;
    [UnityEngine.Serialization.FormerlySerializedAs("bossProgressLabel")]
    public TMP_Text headlinerProgressLabel;
    public TMP_Text timingLabel;
    [UnityEngine.Serialization.FormerlySerializedAs("ownedArtifactsLabel")]
    public TMP_Text ownedGearLabel;
    public TMP_Text[] backpackSlotLabels = new TMP_Text[CardManager.BACKPACK_CAPACITY];

    readonly DisplayedNumber _shownCredits = new();
    Color _creditsLabelColor;
    bool _creditsLabelColorCaptured;
    bool _creditsHeld;

    void OnEnable()
    {
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCreditsChanged += HandleCreditsChanged;
            CardManager.Instance.OnDeckChanged += Refresh;
            CardManager.Instance.OnBuildChanged += Refresh;
            CardManager.Instance.OnConsumablesChanged += Refresh;
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnCycleStarted += HandleCycleStarted;
            RunManager.Instance.OnShowPathScreen += Refresh;
            RunManager.Instance.OnHidePathScreen += Refresh;
            RunManager.Instance.OnRunCompleted += Refresh;
        }

        if (CombatManager.Instance != null)
            CombatManager.Instance.OnEncounterResult += HandleEncounterResult;

        Refresh();
    }

    void OnDisable()
    {
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCreditsChanged -= HandleCreditsChanged;
            CardManager.Instance.OnDeckChanged -= Refresh;
            CardManager.Instance.OnBuildChanged -= Refresh;
            CardManager.Instance.OnConsumablesChanged -= Refresh;
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnCycleStarted -= HandleCycleStarted;
            RunManager.Instance.OnShowPathScreen -= Refresh;
            RunManager.Instance.OnHidePathScreen -= Refresh;
            RunManager.Instance.OnRunCompleted -= Refresh;
        }

        if (CombatManager.Instance != null)
            CombatManager.Instance.OnEncounterResult -= HandleEncounterResult;
    }

    void HandleCreditsChanged(int _) => Refresh();
    void HandleCycleStarted(string _) => Refresh();
    void HandleEncounterResult(EncounterResult _) => Refresh();

    void Update()
    {
        RefreshTiming();
        if (_shownCredits.Tick(Time.unscaledDeltaTime) || _creditsHeld && !CreditsHudUI.HoldsCredits)
            RefreshCredits(CardManager.Instance);
    }

    void Refresh()
    {
        var cards = CardManager.Instance;
        var run = RunManager.Instance;

        RefreshCredits(cards);

        RefreshGear(cards);
        RefreshBackpack(cards);

        if (pileSummaryLabel)
        {
            if (cards == null)
            {
                pileSummaryLabel.text = "Hand  —";
            }
            else
            {
                int enhancedCount = 0;
                foreach (var card in cards.ownedCards)
                    if (card.Enhancement != null) enhancedCount++;
                pileSummaryLabel.text =
                    $"HAND {cards.HandCount}/{cards.HandCapacity}  ·  DRAW PILE {cards.deck.Count}  ·  MOD {enhancedCount}";
            }
        }

        if (run == null)
        {
            if (runProgressLabel) runProgressLabel.text = "Run —";
            if (headlinerProgressLabel) headlinerProgressLabel.text = "Headliner —";
            return;
        }

        int completedNodes = 0;
        foreach (var node in run.currentPath)
            if (node.completed && !node.isShopCheckpoint && node.kind != MapNodeType.Headliner) completedNodes++;

        if (runProgressLabel)
        {
            string mapProgress = run.IsInfiniteMode
                ? $"District {run.CurrentCycle}/∞"
                : $"District {Mathf.Clamp(run.CurrentCycle, 1, run.HeadlinerTotal)}/{run.HeadlinerTotal}";
            string routeProgress = run.IsRunCompleted
                ? "Run Complete"
                : $"{mapProgress}  •  Depth {completedNodes}/{RunMapGenerator.RouteColumnCount}";
            runProgressLabel.text = routeProgress;
            if (run.ActiveContract != null)
                runProgressLabel.text += $"\n{FormatContractProgress(run.ActiveContract)}";
        }

        if (headlinerProgressLabel)
        {
            string headlinerName = run.CurrentHeadliner != null ? run.CurrentHeadliner.enemyName : "Complete";
            headlinerProgressLabel.text = run.IsInfiniteMode
                ? $"District {run.CurrentCycle} Headliner  •  {headlinerName}"
                : $"Headliner {Mathf.Min(run.headlinerIndex + 1, run.HeadlinerTotal)}/{run.HeadlinerTotal}  •  {headlinerName}";
        }

        RefreshTiming();
    }

    static string FormatContractProgress(RunContractState contract)
    {
        long lastMap = (long)contract.deadlineMapExclusive + 1L;
        return contract.objective switch
        {
            RunContractObjective.WinTwoEncounters =>
                $"Contract • Encounters {contract.encounterWins}/2 • due before District {lastMap}",
            RunContractObjective.DefeatEliteThisMap =>
                $"Contract • Enforcer 0/1 • District {(long)contract.startedAtMapIndex + 1L}",
            RunContractObjective.WinCombatWithoutHpLoss =>
                $"Contract • Flawless 0/1 • due before District {lastMap}",
            _ => "Contract active"
        };
    }

    void RefreshBackpack(CardManager cards)
    {
        if (backpackSlotLabels == null) return;
        for (int i = 0; i < backpackSlotLabels.Length; i++)
        {
            var label = backpackSlotLabels[i];
            if (!label) continue;
            bool visible = cards != null && i < Mathf.Max(cards.BackpackCapacity, cards.BackpackSlotsUsed);
            label.transform.parent.gameObject.SetActive(visible);
            if (!visible)
            {
                label.gameObject.SetActive(false);
                continue;
            }
            var slot = cards.GetConsumableInstanceAtSlot(i);
            int stackCount = cards.GetConsumableStackCountAtSlot(i);
            label.text = slot == null ? string.Empty :
                DealbreakUIStyle.Theme != null ? $"{slot.Definition.icon}" +
                (stackCount > 1 ? $" ×{stackCount}" : string.Empty) +
                (slot.Definition.uses > 1 ? $" {slot.RemainingCharges}/{slot.Definition.uses}" : string.Empty) :
                $"{slot.Definition.icon} {slot.Definition.displayName}" +
                (stackCount > 1 ? $" ×{stackCount}" : string.Empty) +
                (slot.Definition.uses > 1
                    ? $" ({slot.RemainingCharges}/{slot.Definition.uses} first)" : string.Empty);
            label.gameObject.SetActive(slot != null);
        }
    }

    void RefreshGear(CardManager cards)
    {
        if (!ownedGearLabel) return;
        int used = cards != null ? cards.GearSlotsUsed : 0;
        int capacity = cards != null ? cards.GearCapacity : CardManager.BASE_GEAR_CAPACITY;
        var text = new StringBuilder($"<b>GEAR  •  {used}/{capacity}</b>\n");
        if (cards == null || cards.ownedGear.Count == 0)
        {
            text.Append("None owned");
        }
        else
        {
            bool first = true;
            foreach (var gear in cards.ownedGear)
            {
                if (gear == null) continue;
                if (!first) text.Append("    •    ");
                if (!string.IsNullOrWhiteSpace(gear.icon)) text.Append(gear.icon).Append(' ');
                text.Append(gear.displayName);
                first = false;
            }
        }
        ownedGearLabel.text = text.ToString();
    }

    // Credits count toward their new total with a spend/earn tint in play mode. Beside the coin the
    // label is the bare number; while defeated enemies' Credits are still flying in, it waits for them.
    void RefreshCredits(CardManager cards)
    {
        if (!creditsLabel) return;
        if (!_creditsLabelColorCaptured)
        {
            _creditsLabelColor = creditsLabel.color;
            _creditsLabelColorCaptured = true;
        }
        if (cards == null || cards.HasInfiniteMoney)
        {
            creditsLabel.text = cards == null ? "—" : "∞";
            creditsLabel.color = _creditsLabelColor;
            return;
        }
        bool animate = Application.isPlaying && isActiveAndEnabled;
        _creditsHeld = animate && CreditsHudUI.HoldsCredits;
        if (!_creditsHeld) _shownCredits.SetTarget(cards.credits, animate);
        creditsLabel.text = $"{_shownCredits.Shown}";
        creditsLabel.color = _shownCredits.Tint(_creditsLabelColor);
    }

    void RefreshTiming()
    {
        if (!timingLabel) return;
        timingLabel.gameObject.SetActive(false);
        var timing = RunManager.Instance?.Timing;
        timingLabel.text = timing == null
            ? "RUN —"
            : $"RUN {RunDurationFormatter.Format(timing.TotalElapsedSeconds)}  ·  DISTRICT {RunDurationFormatter.Format(timing.CurrentMapElapsedSeconds)}";
    }
}
