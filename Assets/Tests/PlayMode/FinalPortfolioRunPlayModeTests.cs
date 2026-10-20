using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Real-scene progression soak. Extra HP isolates lifecycle correctness from balance.</summary>
public sealed class FinalPortfolioRunPlayModeTests
{
    static readonly FieldInfo StoreField = typeof(SaveGameService).GetField("StoreOverride", BindingFlags.Static | BindingFlags.NonPublic);
    object previousStore;
    string directory;

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (directory == null) yield break;
        Time.timeScale = 1;
        yield return SceneManager.LoadSceneAsync(GameFlowService.MainMenuSceneName);
        // Gameplay managers inherit DontDestroyOnLoad. Leaving them alive contaminates
        // subsequent isolated PlayMode fixtures with duplicate singleton instances.
        var persistent = new[]
        {
            RunManager.Instance != null ? RunManager.Instance.gameObject : null,
            CombatManager.Instance != null ? CombatManager.Instance.gameObject : null,
            CardManager.Instance != null ? CardManager.Instance.gameObject : null
        };
        foreach (var root in persistent.Where(root => root != null).Distinct())
            UnityEngine.Object.Destroy(root);
        yield return null;
        StoreField.SetValue(null, previousStore);
        FrontendLaunchContext.Clear();
        GameplayInputGate.Clear();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    static void AssertSortControlsDoNotCoverHealthOrAttack()
    {
        Canvas.ForceUpdateCanvases();
        var actions = UnityEngine.Object.FindFirstObjectByType<ActionButtonsUI>();
        Assert.That(actions, Is.Not.Null);
        var attack = new Vector3[4];
        var health = new Vector3[4];
        ((RectTransform)actions.playButton.transform).GetWorldCorners(attack);
        ((RectTransform)actions.playerHealthLabel.transform.parent).GetWorldCorners(health);
        foreach (var button in new[] { actions.rankSortButton, actions.suitSortButton })
        {
            var corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            Assert.That(corners[2].x, Is.LessThanOrEqualTo(attack[0].x), button.name + " must be left of Attack");
            Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(health[2].x), button.name + " must not obscure HP");
        }
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator InterruptedCombat_ContinueRestoresExactLastSafeCheckpoint()
    {
        previousStore = StoreField.GetValue(null);
        directory = Path.Combine(Application.temporaryCachePath, "DealbreakFinalQA", Guid.NewGuid().ToString("N"));
        StoreField.SetValue(null, new RunSaveStore(Path.Combine(directory, "active-run.json")));
        // Enter through the real frontend so its persistent GameFlowService exists,
        // even when a preceding fixture has correctly destroyed its singletons.
        yield return SceneManager.LoadSceneAsync(GameFlowService.MainMenuSceneName);
        yield return null;
        FrontendLaunchContext.RequestRun("FINAL-QA-INTERRUPTED-COMBAT");
        yield return SceneManager.LoadSceneAsync(GameFlowService.GameSceneName);
        yield return null;
        AssertSortControlsDoNotCoverHealthOrAttack();
        var run = RunManager.Instance;
        run.OnPathChosen(run.currentPath.First(n => n.accessible && n.kind == MapNodeType.Combat).id);
        Assert.That(SaveGameService.TryRead(out var saved, out var error), Is.True, error);
        saved.timing = null;
        string expected = JsonUtility.ToJson(saved);
        Assert.That(CombatManager.Instance.PassPlayerTurn(), Is.True);
        CombatManager.Instance.TakeRemainingDamage();
        Assert.That(run.CanCaptureSave, Is.False);
        Assert.That(SaveGameService.Checkpoint(run, true), Is.False);
        GameFlowService.Instance.ShowMainMenu();
        yield return null;
        Assert.That(GameFlowService.Instance.ContinueRun(), Is.True);
        yield return null;
        yield return null;
        var restored = RunManager.Instance.CaptureSave();
        restored.timing = null;
        Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(expected),
            "HP, Credits, cards/zones/order, inventory, route, effect and RNG state must roll back together.");
        Assert.That(RunManager.Instance.ActiveNode, Is.Null);
        Assert.That(GameplayInputGate.IsBlocked, Is.False);
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator TwelveDistricts_RealSceneCommands_CheckpointsAndSecondRunRemainConsistent()
    {
        previousStore = StoreField.GetValue(null);
        directory = Path.Combine(Application.temporaryCachePath, "DealbreakFinalQA", Guid.NewGuid().ToString("N"));
        StoreField.SetValue(null, new RunSaveStore(Path.Combine(directory, "active-run.json")));
        // Enter through the real frontend so its persistent GameFlowService exists,
        // even when a preceding fixture has correctly destroyed its singletons.
        yield return SceneManager.LoadSceneAsync(GameFlowService.MainMenuSceneName);
        yield return null;
        FrontendLaunchContext.RequestRun("FINAL-QA-12-DISTRICTS");
        yield return SceneManager.LoadSceneAsync(GameFlowService.GameSceneName);
        yield return null;
        Assert.That(CardManager.Instance.ownedCards.Count, Is.EqualTo(40));
        // Test-only survival allowance. No dev-mode economy/RNG/save bypass is enabled.
        CombatManager.Instance.ConfigurePlayer(100000);
        int restoredDistrict = 0, actions = 0, encounters = 0, shops = 0, elites = 0;
        while (!RunManager.Instance.IsRunCompleted && actions++ < 12000)
        {
            var run = RunManager.Instance;
            var cards = CardManager.Instance;
            var combat = CombatManager.Instance;
            var zones = cards.deck.Concat(cards.hand).Concat(cards.discardPile).ToArray();
            Assert.That(zones.Length, Is.EqualTo(cards.ownedCards.Count));
            Assert.That(zones.Select(c => c.Id).Distinct().Count(), Is.EqualTo(zones.Length));
            Assert.That(zones, Is.EquivalentTo(cards.ownedCards));
            Assert.That(cards.credits, Is.GreaterThanOrEqualTo(0));
            Assert.That(combat.player.IsDefeated, Is.False, "Survival allowance exhausted.");
            if (run.PendingConsumableReward != null)
            {
                if (!run.ClaimPendingConsumableReward(0)) Assert.That(run.DeclinePendingConsumableReward(), Is.True);
                yield return null;
                continue;
            }
            if (combat.currentState == GameState.PlayerTurn)
            {
                var view = cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
                    .Where(v => v.data != null && cards.hand.Contains(v.data))
                    .OrderByDescending(v => v.data.AttackValue).FirstOrDefault();
                if (view == null) combat.Recover();
                else Assert.That(combat.TryPlayCards(new[] { view }, combat.currentEnemy), Is.True);
                yield return null;
                continue;
            }
            if (combat.currentState == GameState.EnemyAttacking)
            {
                combat.TakeRemainingDamage();
                yield return null;
                continue;
            }
            // Let the actual final-enemy fade and overlay coroutine finish before routing.
            var group = UnityEngine.Object.FindFirstObjectByType<EnemyGroupUI>();
            if (group != null && group.IsDefeatPresentationBusy) { yield return null; continue; }
            Assert.That(GameplayInputGate.IsBlocked, Is.False, "Unexpected modal lock at route boundary.");
            if (run.PendingEnemyDrop != null)
            {
                // A drop that did not fit waits on the board; this bot never makes room, so it leaves it.
                Assert.That(run.CanCaptureSave, Is.False, "No checkpoint while a drop waits.");
                Assert.That(run.ClaimPendingEnemyDrop() || run.LeavePendingEnemyDrop(), Is.True);
                yield return null;
                continue;
            }
            if (run.CurrentMapIndex > restoredDistrict)
            {
                Assert.That(cards.ownedCards.Count(c => c.Definition.IsFaceCard), Is.EqualTo(run.CurrentMapIndex));
                Assert.That(SaveGameService.Checkpoint(run, true), Is.True, SaveGameService.LastError);
                Assert.That(SaveGameService.TryRead(out var saved, out var error), Is.True, error);
                string expectedCards = JsonUtility.ToJson(saved.cards);
                int expectedMap = run.CurrentMapIndex;
                GameFlowService.Instance.ShowMainMenu();
                yield return null;
                Assert.That(GameFlowService.Instance.ContinueRun(), Is.True);
                yield return null;
                yield return null;
                Assert.That(RunManager.Instance.CurrentMapIndex, Is.EqualTo(expectedMap));
                Assert.That(JsonUtility.ToJson(CardManager.Instance.CaptureSave()), Is.EqualTo(expectedCards));
                restoredDistrict = expectedMap;
                continue;
            }
            if (run.ActiveNode == null)
            {
                var next = run.currentPath.Where(n => n.accessible && !n.completed)
                    .OrderBy(n => n.kind == MapNodeType.Elite ? 0 : n.kind == MapNodeType.Combat ? 1 : 2).ThenBy(n => n.id).FirstOrDefault();
                Assert.That(next, Is.Not.Null, "No reachable node before terminal completion.");
                if (next.kind == MapNodeType.Elite) elites++;
                if (next.kind == MapNodeType.Combat || next.kind == MapNodeType.Elite || next.kind == MapNodeType.Headliner) encounters++;
                run.OnPathChosen(next.id);
                run.OnPathChosen(next.id); // repeated input must not start a second node
            }
            else if (run.ActiveNode.kind == MapNodeType.Shop)
            {
                shops++;
                Assert.That(SaveGameService.Checkpoint(run, true), Is.True, SaveGameService.LastError);
                run.OnShopDone();
                run.OnShopDone();
            }
            else
            {
                bool chosen = false;
                for (int i = 0; i < 8 && !chosen; i++) chosen = run.ChooseEventOption(i);
                Assert.That(chosen, Is.True, "Seed reached an Incident requiring a targeted interaction.");
            }
            yield return null;
        }
        Assert.That(RunManager.Instance.IsRunCompleted, Is.True, "Action budget exhausted.");
        Assert.That(RunManager.Instance.headlinerIndex, Is.EqualTo(12));
        Assert.That(CardManager.Instance.ownedCards.Count, Is.EqualTo(52));
        Assert.That(CardManager.Instance.ownedCards.Count(c => c.Definition.IsFaceCard), Is.EqualTo(12));
        Assert.That(SaveGameService.Store.HasActiveFiles, Is.False, "Victory must delete the active checkpoint.");
        Debug.Log($"FINAL_QA_RUN districts=12 encounters={encounters} elites={elites} shops={shops} restores={restoredDistrict} iterations={actions}");
        GameFlowService.Instance.ShowMainMenu();
        yield return null;
        GameFlowService.Instance.StartRun("FINAL-QA-SECOND-RUN");
        yield return null;
        yield return null;
        Assert.That(RunManager.Instance.RunSeed, Is.EqualTo("FINAL-QA-SECOND-RUN"));
        Assert.That(CardManager.Instance.ownedCards.Count, Is.EqualTo(40));
        Assert.That(RunManager.Instance.CompletedNodeCount, Is.Zero);
        Assert.That(GameplayInputGate.IsBlocked, Is.False);
        // The runner already fails on unexpected errors/exceptions. Do not promote unrelated
        // Editor package warnings (for example Unity AI cloud login) to gameplay failures.
    }
}
