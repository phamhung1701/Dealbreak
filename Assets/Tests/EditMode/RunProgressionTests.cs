using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;


public sealed class RunProgressionTests
{
    readonly List<ScriptableObject> _testOwnedAssets = new();

    GameObject _cardManagerObject;
    GameObject _combatManagerObject;
    GameObject _runManagerObject;
    CardManager _cardManager;
    CombatManager _combatManager;
    RunManager _runManager;

    [SetUp]
    public void SetUp()
    {
        _cardManagerObject = new GameObject("Test CardManager");
        _cardManager = _cardManagerObject.AddComponent<CardManager>();

        _combatManagerObject = new GameObject("Test CombatManager");
        _combatManager = _combatManagerObject.AddComponent<CombatManager>();

        _runManagerObject = new GameObject("Test RunManager");
        _runManager = _runManagerObject.AddComponent<RunManager>();
        _runManager.thiefType = CreateEnemyType("Test Thief", 10, 2, 1);
        _runManager.goblinType = CreateEnemyType("Test Goblin", 15, 3, 2);
        _runManager.knightType = CreateEnemyType("Test Knight", 20, 4, 3);

        Assert.That(CardManager.Instance, Is.SameAs(_cardManager));
        Assert.That(CombatManager.Instance, Is.SameAs(_combatManager));
        Assert.That(RunManager.Instance, Is.SameAs(_runManager));
    }

    [TearDown]
    public void TearDown()
    {
        // RunManager owns and destroys the generated boss types and their source cards.
        DestroyGameObject(_runManagerObject);
        DestroyGameObject(_combatManagerObject);
        DestroyGameObject(_cardManagerObject);

        foreach (var asset in _testOwnedAssets)
        {
            if (asset != null)
                UnityEngine.Object.DestroyImmediate(asset);
        }

        _testOwnedAssets.Clear();
        _runManager = null;
        _combatManager = null;
        _cardManager = null;
    }

    [Test]
    public void BuildDeck_CreatesAceThroughTenInEverySuit_AndConservesOwnedCardsAcrossZones()
    {
        _cardManager.BuildDeck();

        Assert.That(_cardManager.ownedCards, Has.Count.EqualTo(40));
        Assert.That(_cardManager.deck, Has.Count.EqualTo(40));
        Assert.That(_cardManager.hand, Is.Empty);
        Assert.That(_cardManager.discardPile, Is.Empty);
        Assert.That(_cardManager.ownedCards.Any(card => card.Definition.IsFaceCard), Is.False);

        foreach (CardData.Suit suit in Enum.GetValues(typeof(CardData.Suit)))
        {
            var suitCards = _cardManager.ownedCards.Where(card => card.Suit == suit).ToArray();
            Assert.That(suitCards, Has.Length.EqualTo(10));
            CollectionAssert.AreEquivalent(
                Enumerable.Range((int)CardData.Rank.Ace, 10).Select(value => (CardData.Rank)value),
                suitCards.Select(card => card.Rank));
        }

        Assert.That(_cardManager.DrawToHand(CardManager.HAND_SIZE), Is.EqualTo(CardManager.HAND_SIZE));
        Assert.That(_cardManager.hand, Has.Count.EqualTo(CardManager.HAND_SIZE));
        Assert.That(_cardManager.deck, Has.Count.EqualTo(40 - CardManager.HAND_SIZE));
        AssertCardStateIsConserved();
    }

    [Test]
    public void AddHeadlinerReward_AddsExactFaceCardOnce_WithUniqueId_AndRejectsInvalidRewards()
    {
        _cardManager.BuildDeck();
        var sourceCard = CreateCard(CardData.Suit.Diamonds, CardData.Rank.Queen);
        var nonFaceCard = CreateCard(CardData.Suit.Spades, CardData.Rank.Ten);
        var idsBefore = _cardManager.ownedCards.Select(card => card.Id).ToHashSet();

        var reward = _cardManager.AddHeadlinerReward(sourceCard);

        Assert.That(reward, Is.Not.Null);
        Assert.That(reward.Suit, Is.EqualTo(CardData.Suit.Diamonds));
        Assert.That(reward.Rank, Is.EqualTo(CardData.Rank.Queen));
        Assert.That(reward.Definition, Is.Not.SameAs(sourceCard));
        Assert.That(idsBefore.Contains(reward.Id), Is.False);
        Assert.That(_cardManager.ownedCards, Has.Count.EqualTo(41));
        Assert.That(_cardManager.deck, Has.Count.EqualTo(41));
        Assert.That(_cardManager.ownedCards.Count(card =>
            card.Suit == sourceCard.suit && card.Rank == sourceCard.rank), Is.EqualTo(1));
        Assert.That(_cardManager.ownedCards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(41));
        AssertCardStateIsConserved();

        Assert.That(_cardManager.AddHeadlinerReward(sourceCard), Is.Null);
        Assert.That(_cardManager.AddHeadlinerReward(nonFaceCard), Is.Null);
        Assert.That(_cardManager.AddHeadlinerReward(null), Is.Null);
        Assert.That(_cardManager.ownedCards, Has.Count.EqualTo(41));
        Assert.That(_cardManager.deck, Has.Count.EqualTo(41));
        AssertCardStateIsConserved();
    }

    [Test]
    public void StartRun_BuildsTwelveHeadliners_InRankGroupsWithFourUniqueSuitsAndSourceCards()
    {
        _runManager.StartRun();

        Assert.That(_runManager.headlinerDeck, Has.Count.EqualTo(12));
        AssertHeadlinerGroup(0, CardData.Rank.Jack);
        AssertHeadlinerGroup(4, CardData.Rank.Queen);
        AssertHeadlinerGroup(8, CardData.Rank.King);
        Assert.That(_runManager.headlinerDeck.Select(headliner => headliner.sourceCard).Distinct().Count(), Is.EqualTo(12));
    }

    [Test]
    public void HeadlinerVictory_RecordsCompletedMapSplitAndStartsFreshNextMapTimer()
    {
        _runManager.StartRunWithSeed("TIMING-SPLIT");
        _runManager.Timing.Advance(42.5d);

        typeof(RunManager).GetMethod("HandleHeadlinerVictory", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(_runManager, null);

        Assert.That(_runManager.headlinerIndex, Is.EqualTo(1));
        Assert.That(_runManager.Timing.CompletedSplits, Has.Count.EqualTo(1));
        Assert.That(_runManager.Timing.CompletedSplits[0].MapNumber, Is.EqualTo(1));
        Assert.That(_runManager.Timing.CompletedSplits[0].ElapsedSeconds, Is.EqualTo(42.5d));
        Assert.That(_runManager.Timing.CurrentMapIndex, Is.EqualTo(1));
        Assert.That(_runManager.Timing.CurrentMapElapsedSeconds, Is.Zero);
        Assert.That(_runManager.Timing.IsActive, Is.True);
    }

    [Test]
    public void MissingEncounterContentRollsBackSelectionWithoutCompletingOrUnlockingNode()
    {
        _runManager.StartRunWithSeed("WORKFLOW-MISSING-ENEMY");
        var selectedNode = _runManager.currentPath.First(node => node.accessible);
        var columnNodes = _runManager.currentPath.Where(node => Mathf.Approximately(node.col, selectedNode.col)).ToArray();
        var accessibleBefore = columnNodes.Select(node => node.accessible).ToArray();
        var revealedBefore = columnNodes.Select(node => node.revealed).ToArray();
        selectedNode.contentId = "missing-normal-enemy";
        int hideCount = 0;
        int showCount = 0;
        _runManager.OnHidePathScreen += () => hideCount++;
        _runManager.OnShowPathScreen += () => showCount++;
        LogAssert.Expect(LogType.Error,
            $"RunManager: failed to start {selectedNode.kind} node {selectedNode.id}; route selection was rolled back.");

        _runManager.OnPathChosen(selectedNode.id);

        Assert.That(_runManager.ActiveNode, Is.Null);
        Assert.That(selectedNode.completed, Is.False);
        Assert.That(_combatManager.currentEnemy, Is.Null);
        Assert.That(hideCount, Is.EqualTo(1));
        Assert.That(showCount, Is.EqualTo(1));
        CollectionAssert.AreEqual(accessibleBefore, columnNodes.Select(node => node.accessible));
        CollectionAssert.AreEqual(revealedBefore, columnNodes.Select(node => node.revealed));
        Assert.That(selectedNode.next.Select(id => _runManager.currentPath.Single(node => node.id == id))
            .All(node => !node.accessible && !node.completed), Is.True);
    }

    [Test]
    public void MissingEventContentRollsBackInsteadOfAutoCompletingNode()
    {
        _runManager.StartRunWithSeed("WORKFLOW-MISSING-EVENT");
        var selectedNode = _runManager.currentPath.First(node => node.kind == MapNodeType.Event);
        selectedNode.accessible = true;
        selectedNode.contentId = "missing-event-definition";
        bool wasRevealed = selectedNode.revealed;
        int showCount = 0;
        _runManager.OnShowPathScreen += () => showCount++;
        LogAssert.Expect(LogType.Error,
            $"RunManager: missing or empty Event content '{selectedNode.contentId}' for node {selectedNode.id}; route selection was rolled back.");

        _runManager.OnPathChosen(selectedNode.id);

        Assert.That(_runManager.ActiveNode, Is.Null);
        Assert.That(_runManager.ActiveEvent, Is.Null);
        Assert.That(selectedNode.accessible, Is.True);
        Assert.That(selectedNode.revealed, Is.EqualTo(wasRevealed));
        Assert.That(selectedNode.completed, Is.False);
        Assert.That(showCount, Is.EqualTo(1));
        Assert.That(selectedNode.next.Select(id => _runManager.currentPath.Single(node => node.id == id))
            .All(node => !node.accessible && !node.completed), Is.True);
    }

    [Test]
    public void RouteSelectionWhileEncounterIsActiveDoesNotCommitNode()
    {
        _runManager.StartRunWithSeed("WORKFLOW-ACTIVE-COMBAT");
        var selectedNode = _runManager.currentPath.First(node => node.accessible);
        var encounterEnemy = new EnemyRuntime(_runManager.goblinType);
        _combatManager.StartEnemy(encounterEnemy);
        int hideCount = 0;
        _runManager.OnHidePathScreen += () => hideCount++;
        LogAssert.Expect(LogType.Error,
            "RunManager: route selection rejected while the previous encounter is not resolved.");

        _runManager.OnPathChosen(selectedNode.id);

        Assert.That(_combatManager.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(ReferenceEquals(_combatManager.currentEnemy, encounterEnemy), Is.True);
        Assert.That(_runManager.ActiveNode, Is.Null);
        Assert.That(selectedNode.accessible, Is.True);
        Assert.That(selectedNode.completed, Is.False);
        Assert.That(hideCount, Is.Zero);
    }

    [Test]
    public void EncounterStartupReactionFaultReportsOneAbortWithoutCompletingNode()
    {
        var runaway = ScriptableObject.CreateInstance<EnemyAbility>();
        _testOwnedAssets.Add(runaway);
        var runawayAbilities = Enumerable.Repeat(runaway, CombatReactionQueue.DefaultOperationLimit + 1).ToArray();
        _runManager.thiefType.abilities = runawayAbilities;
        _runManager.goblinType.abilities = runawayAbilities;
        _runManager.knightType.abilities = runawayAbilities;
        _runManager.StartRunWithSeed("WORKFLOW-STARTUP-FAULT");
        var selectedNode = _runManager.currentPath.First(node => node.accessible);
        int resultCount = 0;
        EncounterResult result = EncounterResult.Victory;
        _combatManager.OnEncounterResult += value => { resultCount++; result = value; };
        LogAssert.Expect(LogType.Error,
            $"Combat reaction limit ({CombatReactionQueue.DefaultOperationLimit}) exceeded. Remaining reactions were discarded.");

        _runManager.OnPathChosen(selectedNode.id);

        Assert.That(_combatManager.currentState, Is.EqualTo(GameState.GameOver));
        Assert.That(result, Is.EqualTo(EncounterResult.Aborted), "A rule fault aborts the encounter; it is not a defeat.");
        Assert.That(resultCount, Is.EqualTo(1));
        Assert.That(_runManager.ActiveNode, Is.Null);
        Assert.That(selectedNode.completed, Is.False);
        Assert.That(_runManager.Timing.IsActive, Is.False);
        Assert.That(_cardManager.credits, Is.Zero);
        Assert.That(_combatManager.ForceDefeatForDevelopment(), Is.False);
        Assert.That(resultCount, Is.EqualTo(1), "A faulted encounter must report a single terminal result.");
    }

    [Test]
    public void RunStartClearsContextualConsumableTargetSelection()
    {
        _runManager.StartRunWithSeed("WORKFLOW-BEFORE-TARGET");
        var item = ScriptableObject.CreateInstance<ConsumableData>();
        _testOwnedAssets.Add(item);
        item.id = "workflow-target";
        item.effectType = ConsumableEffectType.DuplicateCard;
        item.uses = 1;
        _cardManager.consumableCatalog.Add(item);
        Assert.That(_cardManager.AddConsumable(item), Is.True);

        var uiObject = new GameObject("Workflow Target UI", typeof(RectTransform));
        uiObject.SetActive(false);
        var ui = uiObject.AddComponent<EnhancementTargetUI>();
        ui.panel = new GameObject("Contextual prompt", typeof(RectTransform));
        ui.panel.transform.SetParent(uiObject.transform);
        var confirm = new GameObject("Confirm", typeof(RectTransform), typeof(UnityEngine.UI.Button));
        confirm.transform.SetParent(ui.panel.transform);
        ui.confirmButton = confirm.GetComponent<UnityEngine.UI.Button>();
        var cancel = new GameObject("Cancel", typeof(RectTransform), typeof(UnityEngine.UI.Button));
        cancel.transform.SetParent(ui.panel.transform);
        ui.backButton = cancel.GetComponent<UnityEngine.UI.Button>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(EnhancementTargetUI).GetMethod("OnEnable", flags).Invoke(ui, null);
        ui.OpenConsumableTarget(0);
        _cardManager.DrawToHand(1);
        typeof(EnhancementTargetUI).GetMethod("SelectCard", flags)
            .Invoke(ui, new object[] { _cardManager.hand[0].Id });
        var targetIds = (List<int>)typeof(EnhancementTargetUI).GetField("_mutationCardIds", flags).GetValue(ui);
        Assert.That(ui.IsTargeting, Is.True);
        Assert.That(targetIds.Count, Is.EqualTo(1));

        try
        {
            _runManager.StartRunWithSeed("WORKFLOW-CLEAR-TARGET");

            Assert.That(ui.IsTargeting, Is.False);
            Assert.That(ui.panel.activeSelf, Is.False);
            Assert.That(targetIds, Is.Empty);
            Assert.That(_cardManager.IsHandEnhancementTargeting, Is.False);
        }
        finally
        {
            typeof(EnhancementTargetUI).GetMethod("OnDisable", flags).Invoke(ui, null);
            UnityEngine.Object.DestroyImmediate(uiObject);
        }
    }

    [Test]
    public void OnPathChosen_AccessibleNodeIsRevealedAndCommitted_WithoutCompletingOrUnlockingSuccessors()
    {
        _runManager.StartRun();
        var selectedNode = _runManager.currentPath.First(node => node.accessible);
        var successors = selectedNode.next
            .Select(id => _runManager.currentPath.Single(node => node.id == id))
            .ToArray();

        Assert.That(selectedNode.completed, Is.False);
        Assert.That(successors, Is.Not.Empty);
        Assert.That(successors.All(node => !node.accessible), Is.True);

        _runManager.OnPathChosen(selectedNode.id);

        Assert.That(selectedNode.revealed, Is.True);
        Assert.That(selectedNode.accessible, Is.False);
        Assert.That(selectedNode.completed, Is.False);
        Assert.That(_runManager.ActiveNode, Is.SameAs(selectedNode));
        Assert.That(successors.All(node => !node.accessible), Is.True);
        Assert.That(successors.All(node => !node.completed), Is.True);
        Assert.That(_combatManager.currentEnemy, Is.Not.Null);
    }

    void AssertHeadlinerGroup(int startIndex, CardData.Rank expectedRank)
    {
        var group = _runManager.headlinerDeck.Skip(startIndex).Take(4).ToArray();
        var expectedSuits = Enum.GetValues(typeof(CardData.Suit)).Cast<CardData.Suit>().ToArray();

        Assert.That(group, Has.Length.EqualTo(4));
        Assert.That(group.All(headliner => headliner != null && headliner.sourceCard != null), Is.True);
        Assert.That(group.All(headliner => headliner.sourceCard.rank == expectedRank), Is.True);
        CollectionAssert.AreEquivalent(expectedSuits, group.Select(headliner => headliner.sourceCard.suit));
    }

    void AssertCardStateIsConserved()
    {
        var zoneCards = _cardManager.deck
            .Concat(_cardManager.hand)
            .Concat(_cardManager.discardPile)
            .ToArray();

        Assert.That(zoneCards, Has.Length.EqualTo(_cardManager.ownedCards.Count));
        Assert.That(zoneCards.Distinct().Count(), Is.EqualTo(zoneCards.Length));
        CollectionAssert.AreEquivalent(_cardManager.ownedCards, zoneCards);
    }

    EnemyTypeData CreateEnemyType(string name, int hp, int attack, int credits)
    {
        var enemyType = EnemyTypeData.Create(name, hp, attack, credits);
        _testOwnedAssets.Add(enemyType);
        return enemyType;
    }

    CardData CreateCard(CardData.Suit suit, CardData.Rank rank)
    {
        var card = CardData.Create(suit, rank);
        _testOwnedAssets.Add(card);
        return card;
    }

    static void DestroyGameObject(GameObject gameObject)
    {
        if (gameObject != null)
            UnityEngine.Object.DestroyImmediate(gameObject);
    }
}
