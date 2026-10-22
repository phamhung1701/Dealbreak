using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RunResultPresentationTests
{
    readonly List<GameObject> _objects = new();
    readonly List<Object> _assets = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    RunResultUI _results;
    AttackCardPresentationUI _presentation;
    AttackCardPresentationUI _previousPresentation;
    GameObject _panel;
    TMP_Text _title;
    TMP_Text _body;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cards = Make<CardManager>("Results Cards");
        _cards.Configure(null, null, null, System.Array.Empty<GearData>());
        _cards.BuildDeck();
        _combat = Make<CombatManager>("Results Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Results Run");
        _run.thiefType = Enemy("Pickpocket");
        _run.goblinType = Enemy("Scrapper");
        _run.knightType = Enemy("SecurityGuard");
        _run.StartRunWithSeed("RESULT-PRESENTATION-TEST");

        var host = MakeObject("Results Host", typeof(RectTransform));
        host.SetActive(false);
        _panel = MakeObject("Results Panel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(host.transform, false);
        _panel.SetActive(false);
        _panel.GetComponent<RectTransform>().sizeDelta = new Vector2(920f, 760f);
        var viewportObject = MakeObject("Results Summary Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(_panel.transform, false);
        var viewport = (RectTransform)viewportObject.transform;
        viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
        viewport.anchoredPosition = new Vector2(0f, 65f);
        viewport.sizeDelta = new Vector2(700f, 330f);
        var scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.vertical = true;
        scroll.horizontal = false;
        var titleObject = MakeObject("Results Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(_panel.transform, false);
        _title = titleObject.GetComponent<TMP_Text>();
        var bodyObject = MakeObject("Results Body", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        bodyObject.transform.SetParent(viewportObject.transform, false);
        _body = bodyObject.GetComponent<TMP_Text>();
        var bodyRect = (RectTransform)bodyObject.transform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.sizeDelta = Vector2.zero;
        bodyObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = bodyRect;
        var resultsHost = host.AddComponent<RunResultUI>();
        resultsHost.resultPanel = _panel;
        resultsHost.resultTitleLabel = _title;
        resultsHost.resultBodyLabel = _body;
        _results = resultsHost;
        host.SetActive(true);
        // EditMode does not dispatch the runtime lifecycle callback that resets static presentation state.
        Invoke(_results, "HideAll");

        var presentationObject = MakeObject("Results Presentation", typeof(RectTransform));
        presentationObject.SetActive(false);
        _presentation = presentationObject.AddComponent<AttackCardPresentationUI>();
        presentationObject.SetActive(true);
        var instanceProperty = typeof(AttackCardPresentationUI).GetProperty("Instance",
            BindingFlags.Static | BindingFlags.Public);
        _previousPresentation = AttackCardPresentationUI.Instance;
        instanceProperty.GetSetMethod(true).Invoke(null, new object[] { _presentation });
    }

    [TearDown]
    public void TearDown()
    {
        if (_results) Invoke(_results, "HideAll");
        if (_presentation) _presentation.CancelAll();
        var instanceProperty = typeof(AttackCardPresentationUI).GetProperty("Instance",
            BindingFlags.Static | BindingFlags.Public);
        instanceProperty.GetSetMethod(true).Invoke(null, new object[] { _previousPresentation });
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i]) Object.DestroyImmediate(_objects[i]);
        foreach (var asset in _assets)
            if (asset) Object.DestroyImmediate(asset);
        _objects.Clear();
        _assets.Clear();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TerminalResultWaitsForPresentationCompletionOrCancellation(bool victory)
    {
        typeof(AttackCardPresentationUI).GetField("_pendingCommits", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_presentation, 1);
        Invoke(_presentation, "NotifyIfIdle", false);

        if (victory) Invoke(_results, "HandleRunCompleted");
        else Invoke(_results, "HandleEncounterResult", EncounterResult.Defeat);

        Assert.That(_panel.activeSelf, Is.False, "The terminal screen must not cover the final combat presentation.");
        Assert.That(RunResultUI.IsShowingResult, Is.False);
        Assert.That(_body.text ?? string.Empty, Is.Empty);

        if (victory)
        {
            typeof(AttackCardPresentationUI).GetField("_pendingCommits", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_presentation, 0);
            Invoke(_presentation, "NotifyIfIdle", false);
        }
        else _presentation.CancelAll();

        Assert.That(_panel.activeSelf, Is.True);
        Assert.That(_title.text, Is.EqualTo(victory ? "RUN VICTORY" : "RUN DEFEAT"));
        Assert.That(RunResultUI.IsShowingResult, Is.True);
    }

    [Test]
    public void AbandonedRunShowsDefeatWithSeedAndNoOrdinaryRetryAction()
    {
        var retry = MakeObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button));
        _results.retrySameSeedButton = retry.GetComponent<Button>();
        _results.ShowAbandonedRun();
        Assert.That(_panel.activeSelf, Is.True);
        Assert.That(_title.text, Is.EqualTo("RUN DEFEAT"));
        Assert.That(_body.text, Does.Contain(_run.RunSeed));
        Assert.That(retry.activeSelf, Is.False);
    }

    [Test]
    public void LongSeedAndBuildSummaryRemainScrollableWithoutVisibleTimers()
    {
        string longSeed = new string('S', 180) + "-END";
        _run.StartRunWithSeed(longSeed);
        for (int i = 0; i < 12; i++)
        {
            _run.Timing.Advance(65.5d + i);
            _run.Timing.CompleteCurrentMap(i < 11);
        }
        for (int i = 0; i < 8; i++)
        {
            var gear = ScriptableObject.CreateInstance<GearData>();
            gear.id = "long-summary-gear-" + i;
            gear.displayName = "Long Gear Name " + i;
            _assets.Add(gear);
            _cards.ownedGear.Add(gear);
        }
        typeof(RunResultUI).GetField("_lastHeadlinerRewardName", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_results, "Final Headliner Reward");

        Invoke(_results, "ShowResult", true);
        Canvas.ForceUpdateCanvases();

        Assert.That(_body.text, Does.Contain(longSeed));
        Assert.That(_body.text, Does.Contain("PROGRESS"));
        Assert.That(_body.text, Does.Contain("RUN TOTALS"));
        Assert.That(_body.text, Does.Contain("Long Gear Name 7"));
        Assert.That(_body.text, Does.Contain("Final Headliner Reward"));
        Assert.That(_body.text, Does.Not.Contain("Total Run Time"));
        Assert.That(_run.Timing.CompletedSplits.Count, Is.EqualTo(12));
        Assert.That(_body.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
        var scroll = _body.GetComponentInParent<ScrollRect>();
        Assert.That(scroll, Is.Not.Null);
        Assert.That(scroll.content, Is.SameAs(_body.rectTransform));
    }

    EnemyTypeData Enemy(string name)
    {
        var type = EnemyTypeData.Create(name, 10, 1, 0);
        _assets.Add(type);
        return type;
    }

    void Invoke(object target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, arguments);

    GameObject MakeObject(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        _objects.Add(go);
        return go;
    }

    T Make<T>(string name) where T : Component
    {
        var go = MakeObject(name);
        return go.AddComponent<T>();
    }
}
