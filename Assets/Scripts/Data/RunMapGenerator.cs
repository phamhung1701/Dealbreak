using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RunMapGenerator
{
    public const int RowCount = 3;
    public const int RouteColumnCount = 5;

    public static List<PathNode> Generate(
        RunRandomContext randomContext,
        int mapIndex,
        RunContentCatalog catalog,
        EnemyTypeData[] fallbackEnemies = null)
    {
        var layoutRandom = randomContext.CreateStream("map-layout", mapIndex);
        var contentRandom = randomContext.CreateStream("map-content", mapIndex);
        var nodes = new List<PathNode>();
        int nextId = 0;

        var columns = new List<PathNode>[RouteColumnCount];
        for (int col = 0; col < RouteColumnCount; col++)
        {
            columns[col] = new List<PathNode>(RowCount);
            var kinds = GetColumnKinds(col);
            kinds.Shuffle(layoutRandom);

            for (int row = 0; row < RowCount; row++)
            {
                MapNodeType kind = kinds[row];
                // Shops are mandatory zero-depth checkpoints, never random route nodes.
                if (kind == MapNodeType.Shop)
                    kind = MapNodeType.Combat;
                bool hidden = kind == MapNodeType.Risk ||
                    (kind == MapNodeType.Event && contentRandom.NextInt(0, 2) == 0);
                int nodeId = nextId++;
                var node = new PathNode
                {
                    id = nodeId,
                    type = ToLegacyType(kind),
                    kind = kind,
                    col = col,
                    row = row,
                    accessible = col == 0,
                    revealed = kind == MapNodeType.Elite || (col == 0 && !hidden),
                    hidden = hidden,
                    completed = false,
                    mapIndex = mapIndex,
                    contentId = PickContentId(kind, mapIndex, catalog, fallbackEnemies, contentRandom, randomContext, nodeId)
                };
                columns[col].Add(node);
                nodes.Add(node);
            }
        }

        for (int col = 0; col < RouteColumnCount - 1; col++)
        {
            for (int row = 0; row < RowCount; row++)
            {
                var node = columns[col][row];
                AddUnique(node.next, columns[col + 1][row].id);

                int diagonal = layoutRandom.NextInt(0, 2) == 0 ? row - 1 : row + 1;
                if (diagonal >= 0 && diagonal < RowCount)
                    AddUnique(node.next, columns[col + 1][diagonal].id);
            }
        }

        var headliner = new PathNode
        {
            id = nextId,
            type = "boss",
            kind = MapNodeType.Headliner,
            col = RouteColumnCount,
            row = 1,
            accessible = false,
            revealed = true,
            hidden = false,
            completed = false,
            mapIndex = mapIndex,
            contentId = "boss"
        };
        foreach (var node in columns[^1])
            AddUnique(node.next, headliner.id);
        nodes.Add(headliner);

        ApplyEliteRoutePolicy(randomContext, mapIndex, catalog, fallbackEnemies, columns);
        InsertShopCheckpoint(nodes, columns[1], columns[2], mapIndex, headliner.id + 1, 1.5f);
        InsertShopCheckpoint(nodes, columns[4], new[] { headliner }, mapIndex, headliner.id + 2, 4.5f);

        return nodes;
    }

    static void InsertShopCheckpoint(List<PathNode> nodes, IEnumerable<PathNode> predecessors,
        IEnumerable<PathNode> successors, int mapIndex, int id, float column)
    {
        var shop = new PathNode { id = id, kind = MapNodeType.Shop, type = "shop",
            contentId = column < 2 ? "shop-mid" : "shop-preboss", mapIndex = mapIndex,
            col = column, row = 1, revealed = true, isShopCheckpoint = true };
        foreach (var next in successors) shop.next.Add(next.id);
        foreach (var previous in predecessors)
        {
            previous.next.Clear();
            previous.next.Add(id);
        }
        nodes.Add(shop);
    }

    static void ApplyEliteRoutePolicy(
        RunRandomContext randomContext,
        int mapIndex,
        RunContentCatalog catalog,
        EnemyTypeData[] fallbackEnemies,
        List<PathNode>[] columns)
    {
        var eliteNodes = columns.SelectMany(column => column)
            .Where(node => node.kind == MapNodeType.Elite)
            .ToArray();
        var presenceRandom = randomContext.CreateStream("map-elite-presence", mapIndex);
        var eliteContentRandom = randomContext.CreateStream("map-elite-content", mapIndex);
        EnemyTypeData[] authoredElites = catalog != null ? catalog.eliteEnemies : null;
        var eligibleElites = authoredElites == null
            ? new List<EnemyTypeData>()
            : authoredElites.Where(enemy => enemy != null).ToList();

        foreach (var elite in eliteNodes)
        {
            if (presenceRandom.NextInt(0, 2) == 0)
            {
                elite.kind = MapNodeType.Combat;
                elite.type = ToLegacyType(MapNodeType.Combat);
                elite.contentId = PickContentId(MapNodeType.Combat, mapIndex, catalog, fallbackEnemies,
                    randomContext.CreateStream("map-elite-fallback", unchecked(mapIndex * 31 + elite.id)),
                    randomContext, elite.id);
                elite.revealed = elite.col == 0 && !elite.hidden;
            }
            else if (eligibleElites.Count > 0)
            {
                elite.contentId = eligibleElites[eliteContentRandom.NextInt(0, eligibleElites.Count)].name;
            }
        }

        // An Elite must never be the only selectable destination from its predecessor.
        for (int col = 0; col < RouteColumnCount - 1; col++)
        {
            foreach (var predecessor in columns[col])
            {
                bool hasEliteSuccessor = predecessor.next.Any(id =>
                    columns[col + 1].Any(node => node.id == id && node.kind == MapNodeType.Elite));
                bool hasNonEliteSuccessor = predecessor.next.Any(id =>
                    columns[col + 1].Any(node => node.id == id && node.kind != MapNodeType.Elite));
                if (!hasEliteSuccessor || hasNonEliteSuccessor) continue;

                var alternative = columns[col + 1].Where(node => node.kind != MapNodeType.Elite)
                    .OrderBy(node => node.row).ThenBy(node => node.id).FirstOrDefault();
                if (alternative != null) AddUnique(predecessor.next, alternative.id);
            }
        }
    }

    static List<MapNodeType> GetColumnKinds(int column) => column switch
    {
        0 => new() { MapNodeType.Combat, MapNodeType.Combat, MapNodeType.Combat },
        1 => new() { MapNodeType.Event, MapNodeType.Combat, MapNodeType.Shop },
        2 => new() { MapNodeType.Elite, MapNodeType.Combat, MapNodeType.Risk },
        3 => new() { MapNodeType.Event, MapNodeType.Event, MapNodeType.Combat },
        _ => new() { MapNodeType.Elite, MapNodeType.Combat, MapNodeType.Shop }
    };

    static string PickContentId(
        MapNodeType kind,
        int mapIndex,
        RunContentCatalog catalog,
        EnemyTypeData[] fallbackEnemies,
        IRandomSource random,
        RunRandomContext randomContext,
        int nodeId)
    {
        if (kind == MapNodeType.Combat || kind == MapNodeType.Elite)
        {
            var enemies = catalog != null && catalog.normalEnemies != null && catalog.normalEnemies.Length > 0
                ? catalog.normalEnemies
                : fallbackEnemies;
            if (enemies == null || enemies.Length == 0) return string.Empty;
            var eligible = new List<EnemyTypeData>(enemies.Length);
            foreach (var enemy in enemies)
                if (enemy != null && mapIndex >= enemy.minMapIndex &&
                    (kind != MapNodeType.Elite || enemy.eligibleAsEliteBase))
                    eligible.Add(enemy);
            if (eligible.Count == 0) return string.Empty;
            return eligible[random.NextInt(0, eligible.Count)].name;
        }

        if (kind == MapNodeType.Event || kind == MapNodeType.Risk)
        {
            var events = catalog != null ? catalog.GetEvents(kind) : null;
            if (events == null || events.Length == 0) return string.Empty;
            if (kind == MapNodeType.Risk)
            {
                var ritual = events.FirstOrDefault(value => value != null && value.flow == RunEventFlow.BloodRitual);
                if (ritual != null && randomContext != null)
                {
                    int context = unchecked(mapIndex * 7919 ^ nodeId);
                    if (randomContext.CreateStream("blood-ritual-appearance", context)
                            .NextInt(0, GameBalance.Current.redlineDeal.appearanceOneIn) == 0)
                        return ritual.id;
                    events = events.Where(value => value != null && value.flow != RunEventFlow.BloodRitual).ToArray();
                }
            }
            if (events.Length == 0) return string.Empty;
            var definition = events[random.NextInt(0, events.Length)];
            return definition != null ? definition.id : string.Empty;
        }

        return ToLegacyType(kind);
    }

    // PathNode.type/contentId are persisted independently of the numeric kind.
    static string ToLegacyType(MapNodeType kind) => kind == MapNodeType.Headliner
        ? "boss" : kind.ToString().ToLowerInvariant();

    static void AddUnique(List<int> values, int value)
    {
        if (!values.Contains(value)) values.Add(value);
    }
}
