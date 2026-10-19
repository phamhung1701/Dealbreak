using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Scouting (Black Market Intel, the Late-Night Diner tip, Lantern) reveals one random reachable
// unknown node, and a revealed node shows what is in it.
public sealed class MapScoutingTests
{
    readonly List<Object> _created = new();
    GameObject _runObject;
    RunManager _run;
    EnemyTypeData _brute, _goblin;
    RunEventDefinition _diner;

    [SetUp]
    public void SetUp()
    {
        _brute = Track(EnemyTypeData.Create("Brute", 18, 3, 2));
        _goblin = Track(EnemyTypeData.Create("Goblin", 6, 2, 1));
        _brute.allies = new[] { new EncounterAllySpec { type = _goblin, count = 2 } };
        _diner = Track(ScriptableObject.CreateInstance<RunEventDefinition>());
        _diner.id = "evt_006";
        _diner.title = "Late-Night Diner";
        _diner.description = "A late-night diner offers a quiet booth and a few useful rumors.";
        var catalog = Track(ScriptableObject.CreateInstance<RunContentCatalog>());
        catalog.normalEnemies = new[] { _brute, _goblin };
        catalog.events = new[] { _diner };
        _runObject = new GameObject("Map scouting RunManager");
        _run = _runObject.AddComponent<RunManager>();
        _run.contentCatalog = catalog;
    }

    [TearDown]
    public void TearDown()
    {
        if (_runObject != null) Object.DestroyImmediate(_runObject);
        foreach (var created in _created) if (created != null) Object.DestroyImmediate(created);
        _created.Clear();
    }

    [Test]
    public void Scouting_PicksRandomlyAmongReachableUnknownNodesOfAnyKind_AndReplaysPerSeed()
    {
        var picks = new HashSet<int>();
        for (int seed = 0; seed < 40; seed++)
        {
            BuildRoute(out var unreachable);
            _run._randomContext = new RunRandomContext($"SCOUT-{seed}");
            Assert.That(_run.TryRevealEligibleHiddenNode(), Is.True);
            var scouted = _run.LastScoutedNode;
            Assert.That(scouted, Is.Not.Null);
            Assert.That(scouted, Is.Not.SameAs(unreachable), "Only nodes the player can still reach are scouted.");
            Assert.That(_run.currentPath.Count(node => _run.IsScoutedNode(node)), Is.EqualTo(1));
            picks.Add(scouted.id);

            BuildRoute(out _);
            _run._randomContext = new RunRandomContext($"SCOUT-{seed}");
            _run.TryRevealEligibleHiddenNode();
            Assert.That(_run.LastScoutedNode.id, Is.EqualTo(scouted.id), "The same state scouts the same node.");
        }
        Assert.That(picks, Is.EquivalentTo(new[] { 1, 2, 3, 4 }),
            "Fights (even reachable ones), hidden Incidents and unknown Risks are all candidates.");
    }

    [Test]
    public void Scouting_IsUnavailableOnceEveryReachableNodeIsKnown()
    {
        BuildRoute(out _);
        _run._randomContext = new RunRandomContext("SCOUT-ALL");
        for (int i = 0; i < 4; i++) Assert.That(_run.TryRevealEligibleHiddenNode(), Is.True);
        Assert.That(_run.CanRevealEligibleHiddenNode(), Is.False);
        Assert.That(_run.TryRevealEligibleHiddenNode(), Is.False);
    }

    [Test]
    public void ScoutedFight_ShowsItsEnemiesAndTotals()
    {
        BuildRoute(out _);
        var fight = _run.currentPath.Single(node => node.id == 2);
        Assert.That(_run.GetNodeDetail(fight), Does.StartWith("Unknown node"));
        Assert.That(_run.GetNodeDetail(fight), Does.Not.Contain("Brute"));

        fight.revealed = true;
        Assert.That(_run.GetNodeDetail(fight), Does.Not.Contain("Brute"), "A known fight still hides its enemies.");
        fight.scouted = true;

        string detail = _run.GetNodeDetail(fight);
        Assert.That(detail, Does.Contain("Street Fight • 3 enemies"));
        Assert.That(detail, Does.Contain("HP /").And.Contain("ATK total"));
        Assert.That(detail, Does.Contain("Brute, Goblin ×2"));
        Assert.That(_run.GetNodeDesc(fight), Does.Contain("ATK total"), "The map preview also shows a scouted fight.");
    }

    [Test]
    public void ScoutedIncident_NamesTheIncident()
    {
        BuildRoute(out _);
        var incident = _run.currentPath.Single(node => node.id == 3);
        Assert.That(_run.GetNodeDetail(incident), Does.Not.Contain(_diner.title));

        incident.revealed = incident.scouted = true;

        Assert.That(_run.IsScoutedNode(incident), Is.True);
        Assert.That(_run.GetNodeDetail(incident), Does.Contain("Incident • Late-Night Diner"));
        Assert.That(_run.GetNodeDetail(incident), Does.Contain(_diner.description));
    }

    [Test]
    public void Enforcer_KeepsItsStatsUntilScouted()
    {
        var elite = new PathNode { id = 9, mapIndex = 0, kind = MapNodeType.Elite, contentId = "Brute",
            revealed = true, col = 3 };
        _run.currentPath.Add(elite);
        Assert.That(_run.GetNodeDetail(elite), Does.Not.Contain("HP"));
        Assert.That(_run.IsScoutedNode(elite), Is.False);
        elite.accessible = true;
        Assert.That(_run.GetNodeDetail(elite), Does.Not.Contain("HP"), "Reaching an Enforcer does not reveal it.");
        elite.scouted = true;
        Assert.That(_run.GetNodeDetail(elite), Does.Contain("HP"));
    }

    // Start (accessible, known) -> unknown fight, hidden Incident, unknown Risk; one orphan.
    void BuildRoute(out PathNode unreachable)
    {
        _run.currentPath.Clear();
        _run.currentPath.Add(new PathNode { id = 1, mapIndex = 0, kind = MapNodeType.Combat, contentId = "Goblin",
            accessible = true, revealed = true, col = 0, row = 1, next = new List<int> { 2, 3 } });
        _run.currentPath.Add(new PathNode { id = 2, mapIndex = 0, kind = MapNodeType.Combat, contentId = "Brute",
            col = 1, row = 0, next = new List<int> { 4 } });
        _run.currentPath.Add(new PathNode { id = 3, mapIndex = 0, kind = MapNodeType.Event, contentId = "evt_006",
            hidden = true, col = 1, row = 1 });
        _run.currentPath.Add(new PathNode { id = 4, mapIndex = 0, kind = MapNodeType.Risk, col = 2, row = 0 });
        unreachable = new PathNode { id = 5, mapIndex = 0, kind = MapNodeType.Combat, contentId = "Brute",
            col = 2, row = 2 };
        _run.currentPath.Add(unreachable);
    }

    T Track<T>(T value) where T : Object
    {
        _created.Add(value);
        return value;
    }
}
