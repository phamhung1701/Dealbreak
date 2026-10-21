using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>The first NEW RUN opens the tutorial in the real scenes, and it never touches the run save.</summary>
public sealed class TutorialPlayModeTests
{
    static readonly FieldInfo StoreField = typeof(SaveGameService).GetField("StoreOverride", BindingFlags.Static | BindingFlags.NonPublic);
    object _previousStore;
    string _directory;
    bool _wasCompleted;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _wasCompleted = TutorialProgress.Completed;
        TutorialProgress.Reset();
        _previousStore = StoreField.GetValue(null);
        _directory = Path.Combine(Application.temporaryCachePath, "DealbreakTutorialQA", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        StoreField.SetValue(null, new RunSaveStore(Path.Combine(_directory, "active-run.json")));
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1;
        yield return SceneManager.LoadSceneAsync(GameFlowService.MainMenuSceneName);
        var persistent = new[]
        {
            RunManager.Instance != null ? RunManager.Instance.gameObject : null,
            CombatManager.Instance != null ? CombatManager.Instance.gameObject : null,
            CardManager.Instance != null ? CardManager.Instance.gameObject : null
        };
        foreach (var root in persistent.Where(root => root != null).Distinct())
            UnityEngine.Object.Destroy(root);
        yield return null;
        StoreField.SetValue(null, _previousStore);
        FrontendLaunchContext.Clear();
        GameplayInputGate.Clear();
        TutorialGate.Clear();
        if (_wasCompleted) TutorialProgress.MarkCompleted();
        else TutorialProgress.Reset();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator FirstNewRun_OpensTheTutorial_CoachRefusesOffScriptCommands_AndSkipReturnsToTheMenu()
    {
        string savePath = Path.Combine(_directory, "active-run.json");
        File.WriteAllText(savePath, "existing save");
        var savedAt = File.GetLastWriteTimeUtc(savePath);

        yield return SceneManager.LoadSceneAsync(GameFlowService.MainMenuSceneName);
        yield return null;
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuUI>();
        Assert.That(menu, Is.Not.Null);
        Assert.That(menu.TutorialButton, Is.Not.Null, "The main menu offers TUTORIAL.");
        menu.newRunButton.onClick.Invoke();
        yield return WaitFor(() => TutorialDirector.Instance != null && TutorialDirector.Instance.Coach != null &&
            TutorialDirector.Instance.Coach.IsVisible);

        var director = TutorialDirector.Instance;
        Assert.That(RunManager.Instance.IsTutorialRun, Is.True, "A first NEW RUN starts the tutorial.");
        Assert.That(director.CurrentStep.Id, Is.EqualTo("welcome"));
        Assert.That(RunManager.Instance.GetComponent<RunSaveAutosave>(), Is.Null, "The tutorial never autosaves.");

        director.Next();
        yield return WaitFor(() => director.CurrentStep.Id == "route" && director.Coach.IsVisible);
        Assert.That(director.Coach.TargetBounds.Count, Is.EqualTo(1), "The route lesson points at the first fight.");
        var node = TutorialTargets.MapNode(0).Single().GetComponent<UnityEngine.UI.Button>();
        node.onClick.Invoke();
        yield return WaitFor(() => director.CurrentStep.Id == "enemy" && director.Coach.IsVisible);
        director.Next();
        director.Next();
        yield return WaitFor(() => director.CurrentStep.Id == "attack" && director.Coach.IsVisible);

        var actions = UnityEngine.Object.FindFirstObjectByType<ActionButtonsUI>();
        actions.passButton.onClick.Invoke();
        yield return null;
        Assert.That(CombatManager.Instance.currentState, Is.EqualTo(GameState.PlayerTurn), "PASS is refused during the Attack lesson.");
        Assert.That(director.Coach.RefusalText, Is.Not.Empty);
        AssertPanelInsideScreen(director.Coach);

        director.Skip();
        yield return WaitFor(() => SceneManager.GetActiveScene().name == GameFlowService.MainMenuSceneName);
        Assert.That(TutorialProgress.Completed, Is.True, "Skipping counts as done.");
        Assert.That(File.ReadAllText(savePath), Is.EqualTo("existing save"));
        Assert.That(File.GetLastWriteTimeUtc(savePath), Is.EqualTo(savedAt));
        Assert.That(TutorialGate.Allows(TutorialCommand.Pass), Is.True, "Leaving the tutorial lifts its filter.");
    }

    static void AssertPanelInsideScreen(TutorialCoachUI coach)
    {
        var corners = new Vector3[4];
        coach.Panel.GetWorldCorners(corners);
        Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(0));
        Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(0));
        Assert.That(corners[2].x, Is.LessThanOrEqualTo(Screen.width));
        Assert.That(corners[2].y, Is.LessThanOrEqualTo(Screen.height));
    }

    static IEnumerator WaitFor(Func<bool> condition, float seconds = 15f)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(condition(), Is.True, "Timed out waiting for the tutorial.");
    }
}
