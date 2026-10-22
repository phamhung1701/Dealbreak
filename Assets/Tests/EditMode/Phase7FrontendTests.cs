using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7FrontendTests
{
    readonly List<Object> _created = new();
    float _originalTimeScale;
    int _savedWidth;
    int _savedHeight;
    int _savedMode;
    int _savedVSync;
    bool _hadWidth;
    bool _hadHeight;
    bool _hadMode;
    bool _hadVSync;

    const string WidthKey = "display.width";
    const string HeightKey = "display.height";
    const string ModeKey = "display.mode";
    const string VSyncKey = "display.vsync";

    [SetUp]
    public void SetUp()
    {
        _originalTimeScale = Time.timeScale;
        GameplayInputGate.Clear();
        FrontendLaunchContext.Clear();
        SavePreference(WidthKey, out _hadWidth, out _savedWidth);
        SavePreference(HeightKey, out _hadHeight, out _savedHeight);
        SavePreference(ModeKey, out _hadMode, out _savedMode);
        SavePreference(VSyncKey, out _hadVSync, out _savedVSync);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = _originalTimeScale <= 0f ? 1f : _originalTimeScale;
        GameplayInputGate.Clear();
        FrontendLaunchContext.Clear();

        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();

        RestorePreference(WidthKey, _hadWidth, _savedWidth);
        RestorePreference(HeightKey, _hadHeight, _savedHeight);
        RestorePreference(ModeKey, _hadMode, _savedMode);
        RestorePreference(VSyncKey, _hadVSync, _savedVSync);
        PlayerPrefs.Save();
    }

    [TestCase(false, null, false)]
    [TestCase(false, "disk write failed", true)]
    [TestCase(true, "stale error", false)]
    public void ExitCheckpointPolicy_BlocksOnlyWhenAnAttemptedSaveReportsFailure(bool checkpointed, string error, bool expectedAbort)
    {
        var policy = typeof(GameFlowService).GetMethod("ShouldAbortExit", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(policy, Is.Not.Null);
        Assert.That((bool)policy.Invoke(null, new object[] { checkpointed, error }), Is.EqualTo(expectedAbort));
    }

    [Test]
    public void LaunchContext_NormalizesAndConsumesRunAndMenuRequestsOnce()
    {
        FrontendLaunchContext.RequestRun("  PHASE-7  ");
        FrontendLaunchContext.RequestMainMenu(MainMenuDestination.RunSetup);

        Assert.That(FrontendLaunchContext.TryConsumeRunSeed(out string seed), Is.True);
        Assert.That(seed, Is.EqualTo("PHASE-7"));
        Assert.That(FrontendLaunchContext.TryConsumeRunSeed(out string secondSeed), Is.False);
        Assert.That(secondSeed, Is.Null);
        Assert.That(FrontendLaunchContext.ConsumeMainMenuDestination(), Is.EqualTo(MainMenuDestination.RunSetup));
        Assert.That(FrontendLaunchContext.ConsumeMainMenuDestination(), Is.EqualTo(MainMenuDestination.Home));
    }

    [Test]
    public void RandomSeed_IsEightUppercaseHexCharacters()
    {
        string seed = RunSeedUtility.GenerateSeed();

        Assert.That(seed, Has.Length.EqualTo(8));
        Assert.That(seed.All(character => char.IsDigit(character) || character is >= 'A' and <= 'F'), Is.True);
        Assert.That(RunRandomContext.NormalizeSeed(seed), Is.EqualTo(seed));
    }

    [Test]
    public void RunSetup_BlankIsOptionalAndEachNewLaunchGeneratesAFreshSeed()
    {
        var setup = CreateComponent<RunSetupUI>("Run Setup");
        setup.seedInput = CreateComponent<TMP_InputField>("Seed Input");
        setup.seedPreviewLabel = CreateComponent<TextMeshProUGUI>("Seed Preview");
        setup.seedInput.text = "OLD-RUN";

        setup.Prepare();
        Assert.That(setup.seedInput.text, Is.Empty);
        Assert.That(setup.seedPreviewLabel.text, Does.Contain("fresh random seed"));

        string first = RunSetupUI.ResolveStartSeed(setup.seedInput.text);
        string second = RunSetupUI.ResolveStartSeed("   ");
        Assert.That(first, Is.Not.Empty.And.Not.EqualTo("CARDPG"));
        Assert.That(second, Is.Not.Empty.And.Not.EqualTo(first));
        Assert.That(setup.seedInput.text, Is.Empty, "Generated seeds must not turn into sticky custom input.");
    }

    [Test]
    public void RunSetup_CustomSeedAndOneShotHandoffPreserveActualNormalizedSeed()
    {
        string custom = RunSetupUI.ResolveStartSeed("  My Shareable Seed  ");
        Assert.That(custom, Is.EqualTo("My Shareable Seed"));
        Assert.That(RunSetupUI.ResolveStartSeed("  My Shareable Seed  "), Is.EqualTo(custom));
        FrontendLaunchContext.RequestRun(custom);
        Assert.That(FrontendLaunchContext.TryConsumeRunSeed(out string handoff), Is.True);
        Assert.That(handoff, Is.EqualTo(custom));
        Assert.That(FrontendLaunchContext.TryConsumeRunSeed(out _), Is.False);

        var cards = CreateComponent<CardManager>("Seed CardManager");
        cards.Configure(null, null, null, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        var combat = CreateComponent<CombatManager>("Seed CombatManager");
        combat.ConfigurePlayer(30);
        var run = CreateComponent<RunManager>("Seed RunManager");
        run.thiefType = CreateEnemy("Pickpocket", 10, 4, 5);
        run.goblinType = CreateEnemy("Scrapper", 15, 6, 7);
        run.knightType = CreateEnemy("SecurityGuard", 25, 8, 10);
        run.StartRunWithSeed(handoff);
        Assert.That(run.RunSeed, Is.EqualTo(custom));
        string firstMap = string.Join(",", run.currentPath.Select(node => $"{node.kind}:{node.row}:{node.col}"));
        run.RestartRun();
        Assert.That(run.RunSeed, Is.EqualTo(custom));
        Assert.That(string.Join(",", run.currentPath.Select(node => $"{node.kind}:{node.row}:{node.col}")), Is.EqualTo(firstMap));

        string fresh = RunSetupUI.ResolveStartSeed("");
        FrontendLaunchContext.RequestRun(fresh);
        Assert.That(FrontendLaunchContext.TryConsumeRunSeed(out string nextHandoff), Is.True);
        run.StartRunWithSeed(nextHandoff);
        Assert.That(run.RunSeed, Is.EqualTo(fresh).And.Not.EqualTo(custom));
        run.RestartRun();
        Assert.That(run.RunSeed, Is.EqualTo(fresh));
        string anotherFresh = RunSetupUI.ResolveStartSeed("");
        FrontendLaunchContext.RequestRun(anotherFresh);
        Assert.That(FrontendLaunchContext.TryConsumeRunSeed(out string laterHandoff), Is.True);
        run.StartRunWithSeed(laterHandoff);
        Assert.That(run.RunSeed, Is.EqualTo(anotherFresh).And.Not.EqualTo(fresh));
    }

    [Test]
    public void ModalCoordinator_NestsLayersAndRestoresGameplayStateOnlyAfterFinalPop()
    {
        var host = CreateGameObject("Coordinator Host");
        var gameplayGroup = host.AddComponent<CanvasGroup>();
        var coordinator = host.AddComponent<GameplayMenuCoordinator>();
        coordinator.gameplayCanvasGroup = gameplayGroup;

        var dragCanvasObject = CreateGameObject("Drag Canvas");
        dragCanvasObject.AddComponent<Canvas>();
        var dragRaycaster = dragCanvasObject.AddComponent<GraphicRaycaster>();
        coordinator.dragCanvasRaycaster = dragRaycaster;

        var pausePanel = CreateGameObject("Pause Panel");
        var settingsPanel = CreateGameObject("Settings Panel");
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        var pauseOwner = new object();
        var settingsOwner = new object();
        Time.timeScale = 0.75f;

        Assert.That(coordinator.Push(pauseOwner, pausePanel, null), Is.True);
        Assert.That(coordinator.Push(settingsOwner, settingsPanel, null), Is.True);
        Assert.That(coordinator.LayerCount, Is.EqualTo(2));
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(GameplayInputGate.IsBlocked, Is.True);
        Assert.That(gameplayGroup.interactable, Is.False);
        Assert.That(gameplayGroup.blocksRaycasts, Is.False);
        Assert.That(dragRaycaster.enabled, Is.False);
        Assert.That(coordinator.Pop(pauseOwner), Is.False, "Only the top modal may pop.");

        Assert.That(coordinator.Pop(settingsOwner), Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(GameplayInputGate.IsBlocked, Is.True);
        Assert.That(coordinator.Pop(pauseOwner), Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(0.75f));
        Assert.That(GameplayInputGate.IsBlocked, Is.False);
        Assert.That(gameplayGroup.interactable, Is.True);
        Assert.That(gameplayGroup.blocksRaycasts, Is.True);
        Assert.That(dragRaycaster.enabled, Is.True);
    }

    [Test]
    public void DisplaySettings_InvalidSavedValuesNormalizeToSupportedValuesAndRewritePreferences()
    {
        PlayerPrefs.SetInt(WidthKey, 1);
        PlayerPrefs.SetInt(HeightKey, 1);
        PlayerPrefs.SetInt(ModeKey, int.MaxValue);
        PlayerPrefs.SetInt(VSyncKey, 9);

        DisplaySettingsData settings = DisplaySettingsService.Load();
        var available = DisplaySettingsService.GetAvailableResolutions();

        Assert.That(available.Any(value => value.Width == settings.Width && value.Height == settings.Height), Is.True);
        Assert.That(DisplaySettingsService.SupportedModes, Does.Contain(settings.Mode));
        Assert.That(settings.VSyncCount, Is.InRange(0, 1));
        Assert.That(PlayerPrefs.GetInt(WidthKey), Is.EqualTo(settings.Width));
        Assert.That(PlayerPrefs.GetInt(HeightKey), Is.EqualTo(settings.Height));
        Assert.That(PlayerPrefs.GetInt(ModeKey), Is.EqualTo((int)settings.Mode));
        Assert.That(PlayerPrefs.GetInt(VSyncKey), Is.EqualTo(settings.VSyncCount));
    }

    [Test]
    public void BlockingMenu_PreventsPathSelectionUntilGameplayInputIsRestored()
    {
        var cards = CreateComponent<CardManager>("Phase7 CardManager");
        cards.Configure(null, null, null, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        var combat = CreateComponent<CombatManager>("Phase7 CombatManager");
        combat.ConfigurePlayer(30);
        var run = CreateComponent<RunManager>("Phase7 RunManager");
        run.thiefType = CreateEnemy("Pickpocket", 10, 4, 5);
        run.goblinType = CreateEnemy("Scrapper", 15, 6, 7);
        run.knightType = CreateEnemy("SecurityGuard", 25, 8, 10);
        run.StartRunWithSeed("PHASE7-GATE");
        var node = run.currentPath.First(value => value.accessible);

        GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, true);
        run.OnPathChosen(node.id);
        Assert.That(run.ActiveNode, Is.Null);
        Assert.That(node.completed, Is.False);

        GameplayInputGate.Clear();
        run.OnPathChosen(node.id);
        Assert.That(run.ActiveNode, Is.SameAs(node));
    }

    [Test]
    public void ResultsScreen_ShowsCurrentRunSummaryAndRetryUsesSameSeed()
    {
        var cards = CreateComponent<CardManager>("Phase7 Result CardManager");
        cards.Configure(null, null, null, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        var combat = CreateComponent<CombatManager>("Phase7 Result CombatManager");
        combat.ConfigurePlayer(30);
        var run = CreateComponent<RunManager>("Phase7 Result RunManager");
        run.thiefType = CreateEnemy("Pickpocket", 10, 4, 5);
        run.goblinType = CreateEnemy("Scrapper", 15, 6, 7);
        run.knightType = CreateEnemy("SecurityGuard", 25, 8, 10);
        string generatedSeed = RunSetupUI.ResolveStartSeed("");
        run.StartRunWithSeed(generatedSeed);
        Assert.That(run.RunSeed, Is.EqualTo(generatedSeed));
        cards.AddCredits(27);
        run.Timing.Advance(62.8d);
        run.Timing.CompleteCurrentMap();
        run.Timing.Advance(5.4d);
        run.Timing.StopRun();

        var resultHost = CreateGameObject("Result Host");
        resultHost.SetActive(false);
        var panel = new GameObject("Result Panel", typeof(RectTransform), typeof(Image));
        _created.Add(panel);
        panel.transform.SetParent(resultHost.transform, false);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(920f, 760f);
        var viewportObject = new GameObject("Result Summary Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        _created.Add(viewportObject);
        viewportObject.transform.SetParent(panel.transform, false);
        var viewport = (RectTransform)viewportObject.transform;
        viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
        viewport.anchoredPosition = new Vector2(0f, 65f);
        viewport.sizeDelta = new Vector2(700f, 330f);
        var scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.vertical = true;
        scroll.horizontal = false;
        var title = CreateComponent<TextMeshProUGUI>("Result Title");
        title.transform.SetParent(panel.transform);
        var body = CreateComponent<TextMeshProUGUI>("Result Body");
        body.transform.SetParent(viewportObject.transform);
        body.rectTransform.anchorMin = new Vector2(0f, 1f);
        body.rectTransform.anchorMax = new Vector2(1f, 1f);
        body.rectTransform.pivot = new Vector2(0.5f, 1f);
        body.rectTransform.sizeDelta = new Vector2(0f, 0f);
        var bodyFitter = body.gameObject.AddComponent<ContentSizeFitter>();
        bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = body.rectTransform;
        var result = resultHost.AddComponent<RunResultUI>();
        result.resultPanel = panel;
        result.resultTitleLabel = title;
        result.resultBodyLabel = body;
        resultHost.SetActive(true);

        typeof(RunResultUI).GetMethod("ShowResult", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(result, new object[] { false });

        Assert.That(panel.activeSelf, Is.True);
        Assert.That(title.text, Is.EqualTo("RUN DEFEAT"));
        Assert.That(body.text, Does.Contain("SEED"));
        Assert.That(body.text, Does.Contain(run.RunSeed));
        Assert.That(body.text, Does.Contain("RUN TOTALS"));
        Assert.That(body.text, Does.Contain("Credits  27"));
        Assert.That(body.text, Does.Contain("Headliners defeated"));
        Assert.That(body.text, Does.Not.Contain("Total Run Time"));
        Assert.That(body.text, Does.Not.Contain("TIMING"));
        Assert.That(run.Timing.TotalElapsedSeconds, Is.GreaterThan(68));

        result.RetrySameSeed();
        Assert.That(run.RunSeed, Is.EqualTo(generatedSeed));
        Assert.That(run.headlinerIndex, Is.Zero);
        Assert.That(run.Timing.IsActive, Is.True);
        Assert.That(run.Timing.TotalElapsedSeconds, Is.Zero);
        Assert.That(run.Timing.CompletedSplits, Is.Empty);
        Assert.That(cards.credits, Is.Zero);
        Assert.That(panel.activeSelf, Is.False);

        typeof(RunResultUI).GetMethod("ShowResult", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(result, new object[] { true });
        Assert.That(title.text, Is.EqualTo("RUN VICTORY"));
        Assert.That(body.text, Does.Contain(run.RunSeed));
        Assert.That(body.text, Does.Contain("RUN TOTALS"));
        result.RetrySameSeed();
        Assert.That(run.RunSeed, Is.EqualTo(generatedSeed));
    }

    T CreateComponent<T>(string name) where T : Component
    {
        var gameObject = CreateGameObject(name);
        return gameObject.AddComponent<T>();
    }

    GameObject CreateGameObject(string name)
    {
        var gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }

    EnemyTypeData CreateEnemy(string name, int health, int attack, int credits)
    {
        var enemy = EnemyTypeData.Create(name, health, attack, credits);
        enemy.name = name;
        _created.Add(enemy);
        return enemy;
    }

    static void SavePreference(string key, out bool existed, out int value)
    {
        existed = PlayerPrefs.HasKey(key);
        value = PlayerPrefs.GetInt(key);
    }

    static void RestorePreference(string key, bool existed, int value)
    {
        if (existed) PlayerPrefs.SetInt(key, value);
        else PlayerPrefs.DeleteKey(key);
    }
}
