using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

// HUD slice S15: the Contract line's click detail (objective and reward) and its read-only reward accessor.
public sealed class ContractDetailTests
{
    GameObject _cardsObject, _combatObject, _runObject;
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    RunRandomContext _random;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cardsObject = new GameObject("S15 CardManager");
        _cards = _cardsObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        _combatObject = new GameObject("S15 CombatManager");
        _combat = _combatObject.AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        _runObject = new GameObject("S15 RunManager");
        _run = _runObject.AddComponent<RunManager>();
        _random = new RunRandomContext("s15-tests");
        SetPrivate(_run, "_randomContext", _random);
        SetPrivate(_run, "runSeed", "s15-tests");
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        if (_runObject != null) Object.DestroyImmediate(_runObject);
        if (_combatObject != null) Object.DestroyImmediate(_combatObject);
        if (_cardsObject != null) Object.DestroyImmediate(_cardsObject);
    }

    RunContractState Activate(RunContractObjective objective, int wins = 0)
    {
        var contract = new RunContractState
        {
            objective = objective,
            startedAtMapIndex = 0,
            startedAtNodeId = 4,
            deadlineMapExclusive = 12,
            encounterWins = wins,
            stableId = $"s15:{objective}"
        };
        SetPrivate(_run, "_activeContract", contract);
        return contract;
    }

    [Test]
    public void RewardAccessor_IsEmptyWithoutAContract()
    {
        Assert.That(_run.ActiveContractRewardText, Is.Empty);
        Assert.That(RunStatusUI.ContractDetail(_run), Is.Null);
        Assert.That(RunStatusUI.ContractDetail(null), Is.Null);
    }

    [TestCase(RunContractObjective.WinTwoEncounters)]
    [TestCase(RunContractObjective.DefeatEliteThisMap)]
    [TestCase(RunContractObjective.WinCombatWithoutHpLoss)]
    public void RewardAccessor_IsPure(RunContractObjective objective)
    {
        var contract = Activate(objective, 1);
        _cards.AddCredits(7);
        string before = JsonUtility.ToJson(contract);
        int credits = _cards.credits, hp = _combat.player.currentHealth, gear = _cards.ownedGear.Count;
        int expectedRoll = _random.CreateStream("quest-rare-reward", 11).NextInt(0, 1000);

        string first = _run.ActiveContractRewardText;
        var firstDetail = RunStatusUI.ContractDetail(_run);
        string second = _run.ActiveContractRewardText;
        var secondDetail = RunStatusUI.ContractDetail(_run);

        Assert.That(first, Is.Not.Empty);
        Assert.That(second, Is.EqualTo(first));
        Assert.That(secondDetail.Value.Body, Is.EqualTo(firstDetail.Value.Body));
        Assert.That(_run.ActiveContract, Is.SameAs(contract), "Reading the reward never settles the Contract.");
        Assert.That(JsonUtility.ToJson(contract), Is.EqualTo(before));
        Assert.That((_cards.credits, _combat.player.currentHealth, _cards.ownedGear.Count), Is.EqualTo((credits, hp, gear)));
        Assert.That(GetPrivate(_run, "_randomContext"), Is.SameAs(_random), "No random context is created or replaced.");
        Assert.That(_random.CreateStream("quest-rare-reward", 11).NextInt(0, 1000), Is.EqualTo(expectedRoll));
    }

    [Test]
    public void RewardText_QuotesTheBalanceNumbersThePayoutUses()
    {
        var c = GameBalance.Current.contracts;
        Assert.That(RunManager.ContractRewardText(RunContractObjective.WinTwoEncounters),
            Is.EqualTo($"Reward: {c.combatReward} Credits."));
        Assert.That(RunManager.ContractRewardText(RunContractObjective.WinCombatWithoutHpLoss),
            Is.EqualTo($"Reward: {c.flawlessReward} Credits."));
        Assert.That(RunManager.ContractRewardText(RunContractObjective.DefeatEliteThisMap),
            Is.EqualTo($"Reward: a {c.enforcerRewardRarity} Gear item, or heal {c.enforcerFallbackHeal} HP if none is eligible."));
    }

    [Test]
    public void FlawlessContractPayout_MatchesTheQuotedReward()
    {
        var c = GameBalance.Current.contracts;
        Activate(RunContractObjective.WinCombatWithoutHpLoss);
        Assert.That(_run.ActiveContractRewardText, Does.Contain($"{c.flawlessReward} Credits"));
        int credits = _cards.credits;
        InvokePrivate(_run, "ProgressContractForVictory", new PathNode { id = 9, mapIndex = 0, kind = MapNodeType.Combat }, 0);
        Assert.That(_cards.credits - credits, Is.EqualTo(c.flawlessReward));
        Assert.That(_run.ActiveContract, Is.Null);
        Assert.That(_run.ActiveContractRewardText, Is.Empty);
    }

    [Test]
    public void ContractLineClick_PinsObjectiveAndRewardWithoutRepeatingTheLine()
    {
        var contract = Activate(RunContractObjective.WinTwoEncounters, 1);
        var tooltipObject = new GameObject("S15 Tooltip");
        var lineObject = new GameObject("S15 Contract Line");
        var tooltip = tooltipObject.AddComponent<ContextualTooltipUI>();
        InvokeInstance(tooltip, "OnEnable");
        try
        {
            var trigger = lineObject.AddComponent<ContextualInfoTrigger>();
            trigger.describe = () => RunStatusUI.ContractDetail(_run);
            trigger.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });

            Assert.That(tooltip.IsPinned(trigger), Is.True);
            Assert.That(ContextualTooltipUI.TryDescribe(trigger, out var info), Is.True);
            Assert.That(info.Title, Is.EqualTo("Combat Contract"), "The name as authored in Resources/QuestContract.");
            Assert.That(info.Body, Is.EqualTo(contract.ObjectiveDescription + "\n" + _run.ActiveContractRewardText));
            string line = (string)typeof(RunStatusUI).GetMethod("FormatContractProgress", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { contract });
            Assert.That(info.Body, Does.Not.Contain(line));
            Assert.That(info.Title, Does.Not.Contain(line));
            Assert.That(info.Body, Does.Not.Contain("1/"), "Progress stays on the line.");
        }
        finally
        {
            InvokeInstance(tooltip, "OnDisable");
            Object.DestroyImmediate(lineObject);
            Object.DestroyImmediate(tooltipObject);
        }
    }

    static void SetPrivate(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    static object GetPrivate(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    static void InvokePrivate(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    static void InvokeInstance(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
