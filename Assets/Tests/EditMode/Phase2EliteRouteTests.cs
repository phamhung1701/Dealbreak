using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase2EliteRouteTests
{
    readonly List<Object> _assets = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _assets)
            if (asset != null) Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void MapsOneThroughTwelve_HaveZeroToTwoVisibleElitesAndSafeAlternatives()
    {
        for (int mapIndex = 0; mapIndex < 12; mapIndex++)
        {
            for (int seedIndex = 0; seedIndex < 64; seedIndex++)
            {
                string seed = $"phase2-elite-{seedIndex}";
                var nodes = RunMapGenerator.Generate(new RunRandomContext(seed), mapIndex, null);
                var elites = nodes.Where(node => node.kind == MapNodeType.Elite).ToArray();

                Assert.That(elites.Length, Is.InRange(0, 2), $"map={mapIndex}, seed={seed}");
                Assert.That(elites.All(node => node.col > 0 && node.revealed && !node.hidden), Is.True);
                AssertAlternativeForEachElitePredecessor(nodes, mapIndex, seed);

                var repeated = RunMapGenerator.Generate(new RunRandomContext(seed), mapIndex, null);
                CollectionAssert.AreEqual(Signatures(nodes), Signatures(repeated));
            }
        }
    }

    [Test]
    public void PresencePolicyProducesZeroOneAndTwoEliteMaps()
    {
        var counts = new HashSet<int>();
        for (int seedIndex = 0; seedIndex < 256; seedIndex++)
        {
            var nodes = RunMapGenerator.Generate(new RunRandomContext($"phase2-count-{seedIndex}"), 4, null);
            counts.Add(nodes.Count(node => node.kind == MapNodeType.Elite));
        }

        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, counts);
    }

    [Test]
    public void AuthoredElitePoolIsSelectedAndNonEliteContentRemainsLegacyStable()
    {
        var catalog = ScriptableObject.CreateInstance<RunContentCatalog>();
        var captain = CreateEnemy("Captain");
        var royalKnight = CreateEnemy("Royal Knight");
        catalog.eliteEnemies = new[] { captain, royalKnight };

        const string seed = "phase2-authored-elites";
        const int mapIndex = 8;
        var withoutCatalog = RunMapGenerator.Generate(new RunRandomContext(seed), mapIndex, null);
        var withCatalog = RunMapGenerator.Generate(new RunRandomContext(seed), mapIndex, catalog);

        Assert.That(withCatalog.Where(node => node.kind == MapNodeType.Elite)
            .All(node => node.contentId == captain.name || node.contentId == royalKnight.name), Is.True);
        var untouchedIds = new HashSet<int>(withoutCatalog.Where(node => node.kind != MapNodeType.Elite)
            .Select(node => node.id));
        CollectionAssert.AreEqual(
            withoutCatalog.Where(node => untouchedIds.Contains(node.id)).Select(ContentAndPosition),
            withCatalog.Where(node => untouchedIds.Contains(node.id)).Select(ContentAndPosition));

        AssertAlternativeForEachElitePredecessor(withCatalog, mapIndex, seed);
        _assets.Add(catalog);
    }

    [Test]
    public void BothAuthoredElitesAreEligibleAfterMapTwo_AndFallbackCombatHasContent()
    {
        var catalog = ScriptableObject.CreateInstance<RunContentCatalog>();
        _assets.Add(catalog);
        var captain = CreateEnemy("CrewLeader");
        var knight = CreateEnemy("RiotCaptain");
        var goblin = CreateEnemy("Scrapper");
        catalog.normalEnemies = new[] { goblin };
        catalog.eliteEnemies = new[] { captain, knight };
        for (int mapIndex = 0; mapIndex < 12; mapIndex++)
        {
            var selected = new HashSet<string>();
            for (int seed = 0; seed < 64; seed++)
            {
                var map = RunMapGenerator.Generate(new RunRandomContext($"elite-roster-{seed}"), mapIndex, catalog);
                foreach (var node in map.Where(node => node.kind == MapNodeType.Elite)) selected.Add(node.contentId);
                var fallback = RunMapGenerator.Generate(new RunRandomContext($"elite-roster-{seed}"), mapIndex, null, new[] { goblin });
                Assert.That(fallback.Where(node => node.kind == MapNodeType.Combat).All(node => node.contentId == goblin.name), Is.True);
            }
            // Enforcers can appear from District 1 on.
            CollectionAssert.AreEquivalent(new[] { captain.name, knight.name }, selected, $"Map {mapIndex + 1}");
        }
    }

    static void AssertAlternativeForEachElitePredecessor(IReadOnlyList<PathNode> nodes, int mapIndex, string seed)
    {
        var byId = nodes.ToDictionary(node => node.id);
        var safe = new HashSet<int>(nodes.Where(node => node.kind == MapNodeType.Headliner).Select(node => node.id));
        foreach (var node in nodes.OrderByDescending(node => node.col))
            if (node.kind != MapNodeType.Elite && node.next.Any(safe.Contains)) safe.Add(node.id);
        Assert.That(nodes.Where(node => node.col == 0).All(node => safe.Contains(node.id)), Is.True,
            $"Every start must permit a complete non-Elite route: map={mapIndex}, seed={seed}.");
        foreach (var predecessor in nodes.Where(node => node.col < RunMapGenerator.RouteColumnCount - 1))
        {
            var successors = predecessor.next.Select(id => byId[id]).ToArray();
            if (!successors.Any(node => node.kind == MapNodeType.Elite)) continue;
            Assert.That(successors.Any(node => node.kind != MapNodeType.Elite),
                Is.True, $"Elite was the sole next-column choice at map={mapIndex}, seed={seed}, node={predecessor.id}.");
        }
    }

    EnemyTypeData CreateEnemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 4, 5);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }

    static string ContentAndPosition(PathNode node) =>
        $"{node.id}:{node.kind}:{node.type}:{node.contentId}:{node.col}:{node.row}";

    static IEnumerable<string> Signatures(IEnumerable<PathNode> nodes) => nodes.Select(node =>
        $"{ContentAndPosition(node)}:{node.hidden}:{node.revealed}:{string.Join(",", node.next)}");
}
