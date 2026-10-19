using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7DMapVisibilityTests
{
    [Test]
    public void DistantElitesRemainRevealedWithoutExposingHiddenEventsOrRisk()
    {
        int totalElites = 0;
        for (int mapIndex = 0; mapIndex < 12; mapIndex++)
        {
            var nodes = RunMapGenerator.Generate(new RunRandomContext("elite-visibility"), mapIndex, null);
            var elites = nodes.Where(node => node.kind == MapNodeType.Elite).ToArray();

            Assert.That(elites.Length, Is.InRange(0, 2));
            totalElites += elites.Length;
            Assert.That(elites.All(node => node.col > 0 && node.revealed && !node.hidden), Is.True);
            Assert.That(nodes.Where(node => node.hidden).All(node => !node.revealed), Is.True);
            Assert.That(nodes.Where(node => node.kind == MapNodeType.Shop && node.col > 0)
                .All(node => node.isShopCheckpoint && node.revealed), Is.True, "Mandatory checkpoints are always visible.");
        }
        Assert.That(totalElites, Is.GreaterThan(0), "The visibility assertions must include actual Elite nodes.");
    }

    [Test]
    public void CombatAndEliteMapLabels_DoNotRevealEncounterEnemyIdentity()
    {
        var gameObject = new GameObject("Phase7D Label RunManager");
        try
        {
            var run = gameObject.AddComponent<RunManager>();
            var combat = new PathNode { kind = MapNodeType.Combat, contentId = "Goblin" };
            var elite = new PathNode { kind = MapNodeType.Elite, contentId = "Knight" };

            Assert.That(run.GetNodeLabel(combat), Is.EqualTo("Street Fight"));
            Assert.That(run.GetNodeLabel(elite), Is.EqualTo("Enforcer Fight"));
            var headingMethod = typeof(PathScreenUI).GetMethod("GetNodeHeading", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(headingMethod, Is.Not.Null);
            Assert.That((string)headingMethod.Invoke(null, new object[] { combat, run }), Is.EqualTo("Street Fight"));
            Assert.That((string)headingMethod.Invoke(null, new object[] { elite, run }), Is.EqualTo("Enforcer Fight"));
            Assert.That(run.GetNodeDesc(combat), Is.EqualTo("Street Fight"));
            Assert.That(run.GetNodeDesc(elite), Is.EqualTo("Hard fight • better Credits"));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void PathNodeButtonUI_UsesCompactLabelsAndKeepsUnknownNodesUnknown()
    {
        var root = new GameObject("Path node label test");
        var labelObject = new GameObject("Label", typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform);
        try
        {
            var runObject = new GameObject("Path node run");
            try
            {
                var run = runObject.AddComponent<RunManager>();
                var ui = root.AddComponent<PathNodeButtonUI>();
                var label = labelObject.GetComponent<TextMeshProUGUI>();

                ui.SetNode(new PathNode { kind = MapNodeType.Combat, revealed = true }, run);
                Assert.That(label.text, Is.EqualTo("COMBAT"));
                ui.SetNode(new PathNode { kind = MapNodeType.Elite, revealed = true }, run);
                Assert.That(label.text, Is.EqualTo("ENFORCER"));
                ui.SetNode(new PathNode { kind = MapNodeType.Shop, revealed = true }, run);
                Assert.That(label.text, Is.EqualTo("BLACK
MARKET"));
                ui.SetNode(new PathNode { kind = MapNodeType.Event, revealed = true }, run);
                Assert.That(label.text, Is.EqualTo("INCIDENT"));
                ui.SetNode(new PathNode { kind = MapNodeType.Risk, revealed = true, accessible = true }, run);
                Assert.That(label.text, Is.EqualTo("?"));
                // A Risk revealed ahead of travel has been scouted: it names what it is.
                ui.SetNode(new PathNode { kind = MapNodeType.Risk, revealed = true, hidden = true, accessible = true }, run);
                Assert.That(label.text, Is.EqualTo("RISK"));
                ui.SetNode(new PathNode { kind = MapNodeType.Combat, revealed = false }, run);
                Assert.That(label.text, Is.EqualTo("?"));
            }
            finally
            {
                Object.DestroyImmediate(runObject);
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PathNodeButtonUI_PreservesHeadlinerIdentityWithoutDescription()
    {
        var runObject = new GameObject("Path node boss label test");
        var root = new GameObject("Path node boss button");
        var labelObject = new GameObject("Label", typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform);
        try
        {
            var run = runObject.AddComponent<RunManager>();
            var ui = root.AddComponent<PathNodeButtonUI>();
            var node = new PathNode { kind = MapNodeType.Headliner, revealed = true };
            ui.SetNode(node, run);
            Assert.That(labelObject.GetComponent<TextMeshProUGUI>().text, Is.EqualTo("HEADLINER"));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(runObject);
        }
    }

    [Test]
    public void MapVisibilityToggle_RequiresAwaitingRouteAndUnblockedInput()
    {
        GameplayInputGate.Clear();
        var runObject = new GameObject("Map toggle run");
        var root = new GameObject("Map toggle canvas", typeof(RectTransform), typeof(Canvas));
        root.SetActive(false);
        var panelObject = new GameObject("PathPanel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        panelObject.transform.SetParent(root.transform, false);
        var ui = root.AddComponent<PathScreenUI>();
        SetField(ui, "panel", panelObject);
        try
        {
            var run = runObject.AddComponent<RunManager>();
            root.SetActive(true);
            InvokePrivate(ui, "EnsureMapVisibilityControl");
            var control = root.transform.Find("MapVisibilityButton");
            Assert.That(control, Is.Not.Null);

            GameplayInputGate.Set(GameplayInputBlockReason.ConsumableReward, true);
            InvokePrivate(ui, "ShowImmediately");
            Assert.That(control.gameObject.activeSelf, Is.True,
                "A temporary reward gate must not permanently disable the route-map control.");
            GameplayInputGate.Clear();
            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.False,
                "The map must remain closeable once the temporary reward gate clears.");
            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.True);

            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.False);

            GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, true);
            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.False, "A blocked UI state must not reopen the map.");
            GameplayInputGate.Clear();

            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.True);

            typeof(RunManager).GetProperty(nameof(RunManager.IsRunCompleted))
                .GetSetMethod(true).Invoke(run, new object[] { true });
            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.True, "A completed run must not toggle the route map.");

            InvokePrivate(ui, "HandleRunCompleted");
            Assert.That(control.gameObject.activeSelf, Is.False, "The map toggle must be hidden for final victory.");
        }
        finally
        {
            GameplayInputGate.Clear();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(runObject);
        }
    }

    [Test]
    public void MapVisibilityToggle_IsHiddenWhenMapFlowIsCancelledOrReset()
    {
        GameplayInputGate.Clear();
        var runObject = new GameObject("Map lifecycle run");
        var root = new GameObject("Map lifecycle canvas", typeof(RectTransform), typeof(Canvas));
        root.SetActive(false);
        var panelObject = new GameObject("PathPanel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        panelObject.transform.SetParent(root.transform, false);
        var ui = root.AddComponent<PathScreenUI>();
        SetField(ui, "panel", panelObject);
        try
        {
            runObject.AddComponent<RunManager>();
            root.SetActive(true);
            InvokePrivate(ui, "EnsureMapVisibilityControl");
            var control = root.transform.Find("MapVisibilityButton");

            InvokePrivate(ui, "ShowImmediately");
            Assert.That(control.gameObject.activeSelf, Is.True);
            InvokePrivate(ui, "HandleRunStarted", "new-seed");
            Assert.That(control.gameObject.activeSelf, Is.False);

            InvokePrivate(ui, "ShowImmediately");
            Assert.That(control.gameObject.activeSelf, Is.True);
            InvokePrivate(ui, "HandlePresentationCancelled");
            Assert.That(control.gameObject.activeSelf, Is.False);

            InvokePrivate(ui, "ShowImmediately");
            Assert.That(control.gameObject.activeSelf, Is.True);
            InvokePrivate(ui, "OnDisable");
            Assert.That(control.gameObject.activeSelf, Is.False);
        }
        finally
        {
            GameplayInputGate.Clear();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(runObject);
        }
    }

    [Test]
    public void GeneratedRoute_PreservesSeededMiddleToOuterBranchesAndNodeReachability()
    {
        bool middleCanReachTop = false;
        bool middleCanReachBottom = false;

        for (int seed = 0; seed < 32; seed++)
        {
            string seedText = $"legacy-lane-{seed}";
            var nodes = RunMapGenerator.Generate(new RunRandomContext(seedText), 0, null);
            var replay = RunMapGenerator.Generate(new RunRandomContext(seedText), 0, null);
            var byId = nodes.ToDictionary(node => node.id);
            CollectionAssert.AreEqual(
                nodes.Select(node => $"{node.id}:{string.Join(",", node.next)}"),
                replay.Select(node => $"{node.id}:{string.Join(",", node.next)}"),
                "Restoring the prior link rule must remain deterministic for a fixed seed.");

            foreach (var node in nodes.Where(node => node.kind != MapNodeType.Headliner))
                foreach (int nextId in node.next)
                    Assert.That(byId[nextId].col - node.col, Is.InRange(0.5f, 1f),
                        "Route links advance one depth or cross a zero-depth checkpoint.");

            for (int depth = 0; depth < RunMapGenerator.RouteColumnCount - 1; depth++)
            {
                if (depth == 1) continue; // The mandatory Shop merges routes after Depth 2.
                var middle = nodes.Single(node => Mathf.Approximately(node.col, depth) &&
                    Mathf.Approximately(node.row, 1f));
                var nextLanes = middle.next.Select(id => byId[id])
                    .Where(next => Mathf.Approximately(next.col, depth + 1f))
                    .Select(next => Mathf.RoundToInt(next.row)).ToArray();
                Assert.That(nextLanes, Does.Contain(1), "The straight-ahead middle-lane link must remain.");
                Assert.That(nextLanes.Count(lane => lane != 1), Is.EqualTo(1),
                    "The seeded middle-lane diagonal must again reach exactly one outer lane.");
                middleCanReachTop |= nextLanes.Contains(0);
                middleCanReachBottom |= nextLanes.Contains(2);
            }

            var reachable = new HashSet<int>(nodes.Where(node => node.accessible).Select(node => node.id));
            var pending = new Queue<int>(reachable);
            while (pending.Count > 0)
                foreach (int nextId in byId[pending.Dequeue()].next)
                    if (reachable.Add(nextId)) pending.Enqueue(nextId);
            Assert.That(reachable.Count, Is.EqualTo(nodes.Count),
                "Straight-ahead links must keep every generated node reachable from the start depth.");

            for (int depth = 0; depth < RunMapGenerator.RouteColumnCount; depth++)
                Assert.That(nodes.Where(node => Mathf.Approximately(node.col, depth))
                    .Select(node => node.row).Distinct().Count(), Is.EqualTo(RunMapGenerator.RowCount));
            Assert.That(nodes.Single(node => node.kind == MapNodeType.Headliner).col,
                Is.EqualTo(RunMapGenerator.RouteColumnCount));
        }

        Assert.That(middleCanReachTop, Is.True);
        Assert.That(middleCanReachBottom, Is.True);
    }

    static void SetField(object target, string name, object value)
    {
        typeof(PathScreenUI).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    static void InvokePrivate(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing callback {name}.");
        method.Invoke(target, args);
    }

    [Test]
    public void InaccessibleEliteKeepsIdentifyingColorRatherThanUnknownRouteColor()
    {
        var method = typeof(PathScreenUI).GetMethod("GetNodeColor", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var elite = new PathNode { kind = MapNodeType.Elite, revealed = true, accessible = false };
        var unknown = new PathNode { kind = MapNodeType.Combat, revealed = false, accessible = false };

        Assert.That(elite.accessible, Is.False);
        var eliteColor = (Color)method.Invoke(null, new object[] { elite });
        var unknownColor = (Color)method.Invoke(null, new object[] { unknown });
        Assert.That(eliteColor, Is.Not.EqualTo(unknownColor), "Known elites retain their identity.");
        Assert.That(eliteColor.r, Is.GreaterThan(eliteColor.b));
        elite.accessible = true;
        var availableColor = (Color)method.Invoke(null, new object[] { elite });
        Assert.That(eliteColor.a, Is.LessThan(availableColor.a), "Unavailable tokens remain secondary.");
        Assert.That(eliteColor.maxColorComponent, Is.LessThan(availableColor.maxColorComponent));
    }
}
