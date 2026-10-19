using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class PathNode
{
    public int id;
    public string type;
    public MapNodeType kind;
    public string contentId;
    public int mapIndex;
    public float col, row;
    public List<int> next = new();
    public bool completed, accessible, revealed, hidden;
    // Set when scouting reveals the node's contents; a fight's enemies stay unknown otherwise.
    public bool scouted;
    public bool isShopCheckpoint;
}

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "RunManager")]
public partial class RunManager : Singleton<RunManager>
{
    [Header("Enemy Type Assets")]
    public EnemyTypeData thiefType;
    public EnemyTypeData goblinType;
    public EnemyTypeData knightType;

    [Header("Phase 4 Content")]
    public RunContentCatalog contentCatalog;

    [Header("Runtime State")]
    [SerializeField] string runSeed;
    // RunManager is created at runtime, so these are not serialized; tests may set the index internally.
    public List<EnemyTypeData> headlinerDeck { get; } = new();
    public int headlinerIndex { get; internal set; }
    public List<PathNode> currentPath { get; } = new();

    public event Action OnShowPathScreen;
    public event Action OnHidePathScreen;
    public event Action OnShowShop;
    public event Action OnHideShop;
    public event Action<RunEventDefinition> OnShowEvent;
    public event Action OnHideEvent;
    public event Action OnRunCompleted;
    public event Action<string> OnRunStarted;
    public event Action<string> OnCycleStarted;
    public event Action OnMapRevealChanged;
    public event Action<CardInstance> OnHeadlinerRewardGranted;
    public event Action OnShopOffersChanged;
    public event Action<string> OnInvestmentResult;
    public event Action OnConsumableRewardChanged;
    public event Action<string, string, IReadOnlyList<CardInstance>, Action<int>> OnShowGearDiscardChoice;

    readonly List<EnemyTypeData> _generatedEnemyTypes = new();
    readonly List<EnemyAbility> _generatedEnemyAbilities = new();
    readonly HashSet<MapNodeType> _visitedNodeTypesCurrentMap = new();
    int _visitedNodeTypesMapIndex = -1;
    PathNode _activeNode;
    RunEventDefinition _activeEvent;
    CombatManager _subscribedCombatManager;
    internal RunRandomContext _randomContext;
    int _completedNodeCount;

    public RunTimingStatistics Timing { get; } = new();

    public int CurrentCycle => HeadlinerTotal > 0 ? Math.Max(1, headlinerIndex == int.MaxValue ? int.MaxValue : headlinerIndex + 1) : 0;
    public int HeadlinerTotal => headlinerDeck.Count;
    public bool IsInfiniteMode { get; private set; }
    public bool IsRunCompleted { get; private set; }
    public bool CanContinueInfiniteMode => IsRunCompleted && !IsInfiniteMode && headlinerDeck.Count == 12 && headlinerIndex == 12;
    public string RunSeed => runSeed;
    public int CurrentMapIndex => headlinerIndex;
    public int CompletedNodeCount => _completedNodeCount;

    public PathNode ActiveNode => _activeNode;
    public RunEventDefinition ActiveEvent => _activeEvent;
    public EnemyTypeData CurrentHeadliner => headlinerDeck.Count == 0 || headlinerIndex < 0
        ? null
        : headlinerDeck[headlinerIndex % headlinerDeck.Count];

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        SubscribeToCombatResults();
    }

    protected override void OnDestroy()
    {
        GameplayBalanceTelemetry.Finish("interrupted");
        UnsubscribeFromCombatResults();
        Reset();
        base.OnDestroy();
    }

    void Update()
    {
        Timing.Advance(Time.deltaTime);
    }

    public void StartRun()
    {
        if (string.IsNullOrWhiteSpace(runSeed))
            runSeed = RunSeedUtility.GenerateSeed();
        StartRunWithSeed(runSeed);
    }

    public void StartNewRun()
    {
        StartRunWithSeed(RunSeedUtility.GenerateSeed());
    }

    public void RestartRun()
    {
        StartRunWithSeed(runSeed);
    }

    public void StartRunWithSeed(string seed)
    {
        if (!HasRequiredConfiguration())
        {
            Debug.LogError("RunManager: run configuration is incomplete.", this);
            return;
        }

        if (!SaveGameService.BeginNewRun(this)) return;
        runSeed = RunRandomContext.NormalizeSeed(seed);
        _randomContext = new RunRandomContext(runSeed);

        SubscribeToCombatResults();
        CombatManager.Instance.Reset();
        CombatManager.Instance.ConfigureCriticalRandom(_randomContext.CreateStream("combat-critical"));
        CombatManager.Instance.ConfigureDuelistSuitRandom(_randomContext.CreateStream("duelist-suit"));
        CardManager.Instance.Reset();
        ResetRuntimeState();
        CardManager.Instance.ConfigureRandom(_randomContext.CreateStream("cards"));
        CardManager.Instance.BuildDeck();
        CardManager.Instance.DealHand();
        BuildHeadlinerDeck();
        Timing.StartNewRun(CurrentMapIndex);
        GameplayBalanceTelemetry.Begin(runSeed);
        OnRunStarted?.Invoke(runSeed);
        NextCycle();
        SaveGameService.Checkpoint(this, true);
    }

    bool HasRequiredConfiguration()
    {
        bool hasFallbackEnemies = thiefType != null && goblinType != null && knightType != null;
        bool hasCatalogEnemies = contentCatalog != null && contentCatalog.normalEnemies != null &&
            contentCatalog.normalEnemies.Any(enemy => enemy != null);
        return (hasFallbackEnemies || hasCatalogEnemies) &&
            CardManager.Instance != null && CombatManager.Instance != null;
    }

    void NextCycle()
    {
        GenerateAndAnnounceCycle();
        OnShowPathScreen?.Invoke();
    }

    void GenerateAndAnnounceCycle()
    {
        GeneratePath(CurrentMapIndex);
        QueueAlchemistRewardForCurrentMap();
        OnCycleStarted?.Invoke(IsInfiniteMode
            ? $"District {CurrentCycle} / Infinite"
            : $"District {CurrentCycle} / {HeadlinerTotal}");
    }

    public IReadOnlyList<PathNode> GenerateMapForIndex(int mapIndex)
    {
        if (_randomContext == null)
            _randomContext = new RunRandomContext(runSeed);
        return RunMapGenerator.Generate(_randomContext, mapIndex, contentCatalog, GetFallbackEnemies());
    }

    void GeneratePath(int mapIndex)
    {
        currentPath.Clear();
        LastScoutedNode = null;
        _visitedNodeTypesCurrentMap.Clear();
        _visitedNodeTypesMapIndex = mapIndex;
        currentPath.AddRange(GenerateMapForIndex(mapIndex));
        GameplayEffectResolver.ApplyMapReady(new MapReadyEffectContext(this, CardManager.Instance, mapIndex));
    }

    public void RefreshCurrentMapEffects()
    {
        if (_randomContext == null || currentPath.Count == 0) return;
        if (GameplayEffectResolver.ApplyMapReady(
            new MapReadyEffectContext(this, CardManager.Instance, CurrentMapIndex)))
            OnMapRevealChanged?.Invoke();
    }

    internal bool TryRerollEligibleHiddenNode()
    {
        var candidate = FindHiddenTrailCandidate();
        if (candidate == null || _randomContext == null) return false;
        MapNodeType replacement = candidate.kind == MapNodeType.Risk ? MapNodeType.Event : MapNodeType.Risk;
        var definitions = contentCatalog != null ? contentCatalog.GetEvents(replacement) : null;
        if (definitions == null || definitions.Length == 0) return false;
        var random = _randomContext.CreateStream("hidden-trail-content",
            unchecked(candidate.mapIndex * 7919 ^ candidate.id));
        var definition = definitions[random.NextInt(0, definitions.Length)];
        if (definition == null || string.IsNullOrEmpty(definition.id)) return false;
        candidate.kind = replacement;
        candidate.type = replacement.ToString().ToLowerInvariant();
        candidate.contentId = definition.id;
        candidate.hidden = true;
        candidate.revealed = false;
        return true;
    }

    // Scouting (Black Market Intel, the Late-Night Diner tip, Lantern) reveals one random reachable
    // node that still shows "?", of any kind, together with its details. The pick is seeded by the
    // current candidate set on its own stream, so it consumes no shared randomness and the same
    // state always scouts the same node.
    internal bool CanRevealEligibleHiddenNode() => FindScoutCandidates().Count > 0;

    internal bool TryRevealEligibleHiddenNode()
    {
        var candidates = FindScoutCandidates();
        if (candidates.Count == 0) return false;
        int key = CurrentMapIndex;
        foreach (var node in candidates) key = unchecked(key * 31 + node.id);
        var random = (_randomContext ?? new RunRandomContext(runSeed)).CreateStream("map-scout", key);
        var scouted = candidates[random.NextInt(0, candidates.Count)];
        scouted.revealed = true;
        scouted.scouted = true;
        LastScoutedNode = scouted;
        return true;
    }

    /// <summary>The node most recently revealed by scouting on this map; runtime presentation only.</summary>
    public PathNode LastScoutedNode { get; private set; }

    static bool IsHiddenRosterKind(MapNodeType kind) => kind is MapNodeType.Combat or MapNodeType.Elite;

    /// <summary>
    /// True for a node whose contents scouting revealed and the player has yet to visit. Scoutable
    /// kinds are otherwise revealed only on becoming accessible, and hidden Incidents/Risks only on
    /// being entered.
    /// </summary>
    public bool IsScoutedNode(PathNode node) => node != null && node != _activeNode && !node.completed &&
        node.scouted && node.revealed;

    /// <summary>
    /// Whether the map may show a node's contents: enemies, totals or the Incident. A fight's
    /// enemies stay unknown until it is scouted; the Headliner is always known.
    /// </summary>
    public bool NodeDetailsVisible(PathNode node) => node != null && node.revealed &&
        (!IsHiddenRosterKind(node.kind) || node.scouted || node.completed);

    List<PathNode> FindScoutCandidates()
    {
        var candidates = new List<PathNode>();
        foreach (var node in ReachableRouteNodes())
            if (node != _activeNode && !node.scouted && (!node.revealed || !NodeDetailsVisible(node)))
                candidates.Add(node);
        candidates.Sort((a, b) => a.id.CompareTo(b.id));
        return candidates;
    }

    // Hidden Trail rerolls the nearest hidden Incident/Risk: route depth, row, then stable node ID.
    PathNode FindHiddenTrailCandidate()
    {
        PathNode candidate = null;
        foreach (var node in ReachableRouteNodes())
        {
            if (node.hidden && !node.revealed &&
                (node.kind == MapNodeType.Event || node.kind == MapNodeType.Risk) &&
                (candidate == null || node.col < candidate.col ||
                    (Mathf.Approximately(node.col, candidate.col) &&
                        (node.row < candidate.row ||
                            (Mathf.Approximately(node.row, candidate.row) && node.id < candidate.id)))))
                candidate = node;
        }
        return candidate;
    }

    internal bool TryRevealHiddenNodeFromEvent()
    {
        if (!TryRevealEligibleHiddenNode()) return false;
        OnMapRevealChanged?.Invoke();
        return true;
    }

    // Uncompleted current-map nodes the player can still reach from where they stand.
    IEnumerable<PathNode> ReachableRouteNodes()
    {
        if (currentPath.Count == 0) yield break;
        var frontier = _activeNode != null && !_activeNode.completed
            ? new[] { _activeNode }
            : currentPath.Where(node => node.accessible && !node.completed)
                .OrderBy(node => node.id).ToArray();
        var pending = new Queue<PathNode>(frontier);
        var visited = new HashSet<int>();
        while (pending.Count > 0)
        {
            var node = pending.Dequeue();
            if (node == null || node.mapIndex != CurrentMapIndex || node.completed ||
                !visited.Add(node.id)) continue;
            yield return node;
            foreach (int nextId in node.next)
            {
                var next = FindNode(nextId);
                if (next != null && !next.completed) pending.Enqueue(next);
            }
        }
    }

    public void OnPathChosen(int id)
    {
        if (GameplayInputGate.IsBlocked || _activeNode != null || IsRunCompleted) return;

        var node = FindNode(id);
        if (node == null || !node.accessible || node.completed) return;

        var combat = CombatManager.Instance;
        if (combat == null || combat.currentState is GameState.PlayerTurn or GameState.EnemyAttacking or GameState.GameOver)
        {
            Debug.LogError("RunManager: route selection rejected while the previous encounter is not resolved.", this);
            return;
        }
        if ((node.kind is MapNodeType.Combat or MapNodeType.Elite or MapNodeType.Headliner) && !combat.CanStartEncounter)
        {
            Debug.LogError($"RunManager: cannot start {node.kind} node {node.id} from combat state {combat.currentState}.", this);
            return;
        }

        // Capture before committing the route or running encounter-start draws/effects.
        SaveGameService.Checkpoint(this, true);
        var columnNodes = currentPath.Where(other => Mathf.Approximately(other.col, node.col)).ToArray();
        var accessibleBefore = columnNodes.Select(other => other.accessible).ToArray();
        var revealedBefore = columnNodes.Select(other => other.revealed).ToArray();
        node.accessible = false;
        node.revealed = true;
        foreach (var other in columnNodes)
            if (other.id != node.id) other.accessible = false;

        _activeNode = node;
        OnHidePathScreen?.Invoke();

        switch (node.kind)
        {
            case MapNodeType.Combat:
            case MapNodeType.Elite:
            case MapNodeType.Headliner:
                if (!TryStartEncounter(node))
                {
                    RollbackNodeStart(node, columnNodes, accessibleBefore, revealedBefore,
                        $"RunManager: failed to start {node.kind} node {node.id}; route selection was rolled back.");
                }
                break;
            case MapNodeType.Shop:
                PrepareShopOffers();
                OnShowShop?.Invoke();
                break;
            case MapNodeType.Event:
            case MapNodeType.Risk:
                _activeEvent = FindEvent(node.kind, node.contentId);
                if (_activeEvent == null || _activeEvent.choices == null || _activeEvent.choices.Length == 0)
                {
                    RollbackNodeStart(node, columnNodes, accessibleBefore, revealedBefore,
                        $"RunManager: missing or empty {node.kind} content '{node.contentId}' for node {node.id}; route selection was rolled back.");
                    return;
                }
                OnShowEvent?.Invoke(_activeEvent);
                break;
            default:
                RollbackNodeStart(node, columnNodes, accessibleBefore, revealedBefore,
                    $"RunManager: unsupported node kind {node.kind} at node {node.id}; route selection was rolled back.");
                break;
        }
    }

    void RollbackNodeStart(PathNode node, PathNode[] columnNodes, bool[] accessibleBefore,
        bool[] revealedBefore, string error)
    {
        if (_activeNode == node) _activeNode = null;
        _activeEvent = null;
        for (int i = 0; i < columnNodes.Length; i++)
        {
            columnNodes[i].accessible = accessibleBefore[i];
            columnNodes[i].revealed = revealedBefore[i];
        }
        Debug.LogError(error, this);
        OnShowPathScreen?.Invoke();
    }

    bool TryStartEncounter(PathNode node)
    {
        var combat = CombatManager.Instance;
        if (node == null || combat == null || !combat.CanStartEncounter) return false;

        var authoredElite = node.kind == MapNodeType.Elite ? FindAuthoredElite(node.contentId) : null;
        var enemyType = node.kind switch
        {
            MapNodeType.Headliner => CurrentHeadliner,
            MapNodeType.Elite => authoredElite ?? CreateEliteType(FindEnemy(node.contentId)),
            _ => FindEnemy(node.contentId)
        };
        if (enemyType == null) return false;

        var members = ComposeEncounter(node, enemyType, authoredElite != null);
        var enemies = new List<EnemyRuntime>(members.Count);
        foreach (var member in members)
            enemies.Add(new EnemyRuntime(member.Type, member.Instance, member.GroupSize, member.Hp, member.Attack));
        return combat.TryStartEncounter(enemies, node.kind);
    }

    // Spawn and preview share this composition; previews pass the source type of a generic Enforcer.
    List<EncounterMember> ComposeEncounter(PathNode node, EnemyTypeData primary, bool authoredElite) =>
        EncounterComposer.Compose(primary, GetEncounterStats(node), node.kind, authoredElite,
            node.mapIndex, node.id, _randomContext ?? new RunRandomContext(runSeed));

    static (int hp, int attack) ScaleEncounterStats(int hp, int attack, int mapIndex) =>
        EncounterComposer.ScaleStats(hp, attack, mapIndex);

    /// <summary>Preview and spawn use the same scaled base stats; elite bonuses are applied afterwards.</summary>
    public (int hp, int attack) GetEncounterStats(PathNode node)
    {
        if (node == null) return (0, 0);
        var authoredElite = node.kind == MapNodeType.Elite ? FindAuthoredElite(node.contentId) : null;
        var source = authoredElite ?? (node.kind == MapNodeType.Headliner ? CurrentHeadliner : FindEnemy(node.contentId));
        if (source == null) return (0, 0);
        int baseHp = node.kind == MapNodeType.Headliner && node.mapIndex == 0
            ? FirstMapHeadlinerHp : source.maxHp;
        var stats = ScaleEncounterStats(baseHp, source.baseAttack, node.mapIndex);
        return node.kind == MapNodeType.Elite && authoredElite == null
            ? (Mathf.CeilToInt(stats.hp * 1.5f), stats.attack + 2)
            : stats;
    }

    EnemyTypeData CreateEliteType(EnemyTypeData source)
    {
        if (source == null) return null;
        var elite = EnemyTypeData.Create(
            $"Enforcer {source.enemyName}",
            Mathf.CeilToInt(source.maxHp * 1.5f),
            source.baseAttack + 2,
            source.creditsReward * 2);
        var ability = contentCatalog != null ? contentCatalog.eliteAbility : null;
        elite.abilities = ability != null ? new[] { ability } : Array.Empty<EnemyAbility>();
        _generatedEnemyTypes.Add(elite);
        return elite;
    }

    EnemyTypeData FindAuthoredElite(string contentId) => contentCatalog?.eliteEnemies?.FirstOrDefault(
        enemy => enemy != null && (enemy.name == contentId || enemy.enemyName == contentId));

    EnemyTypeData FindEnemy(string contentId)
    {
        foreach (var enemy in GetNormalEnemies())
        {
            if (enemy == null) continue;
            if (enemy.name == contentId || enemy.enemyName == contentId)
                return enemy;
        }
        return null;
    }

    EnemyTypeData[] GetNormalEnemies()
    {
        if (contentCatalog != null && contentCatalog.normalEnemies != null &&
            contentCatalog.normalEnemies.Any(enemy => enemy != null))
            return contentCatalog.normalEnemies;
        return GetFallbackEnemies();
    }

    EnemyTypeData[] GetFallbackEnemies() => new[] { thiefType, goblinType, knightType };

    RunEventDefinition FindEvent(MapNodeType category, string contentId)
    {
        var definitions = contentCatalog != null ? contentCatalog.GetEvents(category) : null;
        return definitions?.FirstOrDefault(definition => definition != null && definition.id == contentId);
    }

    PathNode FindNode(int id) => currentPath.FirstOrDefault(node => node.id == id);

    public string GetNodeLabel(PathNode node)
    {
        if (node == null) return "Unknown";
        return node.kind switch
        {
            MapNodeType.Combat => "Street Fight",
            MapNodeType.Elite => "Enforcer Fight",
            MapNodeType.Shop => "Black Market",
            MapNodeType.Event => "Incident",
            MapNodeType.Risk => "Risk / Reward",
            MapNodeType.Headliner => CurrentHeadliner != null ? $"Headliner Fight: {CurrentHeadliner.enemyName}" : "Headliner Fight",
            _ => "Unknown"
        };
    }

    public string GetNodeDesc(PathNode node)
    {
        if (node == null) return string.Empty;
        bool showStats = NodeDetailsVisible(node) &&
            (node.kind == MapNodeType.Combat || node.kind == MapNodeType.Elite || node.kind == MapNodeType.Headliner);
        var stats = showStats ? GetEncounterStats(node) : (hp: 0, attack: 0);
        showStats &= stats.hp > 0;
        return node.kind switch
        {
            MapNodeType.Combat => showStats
                ? DescribeEncounter("Street Fight", ComposeEncounter(node, FindEnemy(node.contentId), false))
                : "Street Fight",
            MapNodeType.Elite => showStats
                ? GetElitePreviewDescription(node, stats)
                : "Hard fight • better Credits",
            MapNodeType.Shop => "Spend Credits on Gear and card Mods",
            MapNodeType.Event => "An Incident choice with immediate effects",
            MapNodeType.Risk => "Trade safety for a stronger reward",
            MapNodeType.Headliner => showStats
                ? $"Major Headliner fight • {stats.hp} HP / {stats.attack} ATK"
                : "Major Headliner fight",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Everything the player may know about a node, for the route map: the enemy roster and
    /// totals of a fight, or the specific Incident. Unknown nodes stay unknown.
    /// </summary>
    public string GetNodeDetail(PathNode node)
    {
        if (node == null) return string.Empty;
        if (!node.revealed) return "Unknown node • scout it or travel closer to learn more";
        string label = GetNodeLabel(node);
        if (!NodeDetailsVisible(node)) return $"{label} • {GetNodeDesc(node)}";
        switch (node.kind)
        {
            case MapNodeType.Combat:
            {
                var enemy = FindEnemy(node.contentId);
                if (enemy == null) return label;
                var members = ComposeEncounter(node, enemy, false);
                return DescribeRoster(DescribeEncounter(label, members), members);
            }
            case MapNodeType.Elite:
            {
                var authored = FindAuthoredElite(node.contentId);
                if (authored != null)
                {
                    var members = ComposeEncounter(node, authored, true);
                    return DescribeRoster(DescribeEncounter(label, members), members);
                }
                var source = FindEnemy(node.contentId);
                var stats = GetEncounterStats(node);
                var trait = contentCatalog != null ? contentCatalog.eliteAbility : null;
                return source == null ? label
                    : $"{label} • {stats.hp} HP / {stats.attack} ATK\nEnforcer {source.enemyName}" +
                      (trait != null && !string.IsNullOrEmpty(trait.displayName) ? $" • Traits: {trait.displayName}" : string.Empty);
            }
            case MapNodeType.Event:
            case MapNodeType.Risk:
            {
                var definition = FindEvent(node.kind, node.contentId);
                return definition == null || string.IsNullOrEmpty(definition.title)
                    ? $"{label} • {GetNodeDesc(node)}"
                    : $"{label} • {definition.title}\n" +
                      (string.IsNullOrEmpty(definition.description) ? GetNodeDesc(node) : definition.description);
            }
            case MapNodeType.Headliner:
                return CurrentHeadlinerDescription;
            default:
                return $"{label} • {GetNodeDesc(node)}";
        }
    }

    // Lists who is in a fight, in spawn order, with duplicates counted, then their traits.
    static string DescribeRoster(string summary, IReadOnlyList<EncounterMember> members)
    {
        var names = members.Where(member => member.Type != null)
            .GroupBy(member => member.Type.enemyName)
            .Select(group => group.Count() > 1 ? $"{group.Key} ×{group.Count()}" : group.Key);
        var traits = members.Where(member => member.Type != null && member.Type.abilities != null)
            .SelectMany(member => member.Type.abilities)
            .Where(ability => ability != null && !string.IsNullOrEmpty(ability.displayName))
            .Select(ability => ability.displayName).Distinct().ToArray();
        return $"{summary}\n{string.Join(", ", names)}" +
            (traits.Length > 0 ? $" • Traits: {string.Join(", ", traits)}" : string.Empty);
    }

    string GetElitePreviewDescription(PathNode node, (int hp, int attack) stats)
    {
        var authored = FindAuthoredElite(node.contentId);
        if (authored == null) return $"Hard fight • better Credits • {stats.hp} HP / {stats.attack} ATK";
        var members = ComposeEncounter(node, authored, true);
        return members.Count == 1
            ? $"Enforcer • 1 enemy • {stats.hp} HP / {stats.attack} ATK"
            : DescribeEncounter("Enforcer", members);
    }

    // Totals include allies and the Captain aura, so the preview matches the spawned encounter.
    static string DescribeEncounter(string label, IReadOnlyList<EncounterMember> members)
    {
        var totals = EncounterComposer.Totals(members);
        return members.Count <= 1
            ? $"{label} • {totals.hp} HP / {totals.attack} ATK"
            : $"{label} • {members.Count} enemies • {totals.hp} HP / {totals.attack} ATK total";
    }

    static int IncrementToMaximum(int value) => value < int.MaxValue ? value + 1 : int.MaxValue;

    void HandleEncounterResult(EncounterResult result)
    {
        if (result is EncounterResult.Defeat or EncounterResult.Aborted)
            Timing.StopRun();
        if (_activeNode == null) return;

        if (result != EncounterResult.Victory)
        {
            _activeNode = null;
            return;
        }

        bool wasHeadliner = _activeNode.kind == MapNodeType.Headliner;
        ApplyVictoryGearRewards(wasHeadliner);
        ProgressContractForVictory(_activeNode, CombatManager.Instance?.EncounterActualHpLost ?? 0);
        if ((CombatManager.Instance?.EncounterDefeatedEnemyCount ?? 0) > 0)
            TryGrantConsumableDropForNode(_activeNode);
        CompleteActiveNode();
        if (wasHeadliner)
            HandleHeadlinerVictory();
        else
            ShowPathAfterNode();
    }

    void ApplyNodeCompletionTracking(PathNode node)
    {
        var cards = CardManager.Instance;
        if (cards == null || node == null) return;
        if (_visitedNodeTypesMapIndex != node.mapIndex)
        {
            _visitedNodeTypesCurrentMap.Clear();
            _visitedNodeTypesMapIndex = node.mapIndex;
        }
        _visitedNodeTypesCurrentMap.Add(node.kind);
        GearRules.OnNodeCompleted(cards, CombatManager.Instance, node, _visitedNodeTypesCurrentMap.Count);
    }

    void CompleteActiveNode()
    {
        var resolvedNode = _activeNode;
        _activeNode = null;
        if (resolvedNode == null || resolvedNode.completed) return;

        resolvedNode.completed = true;
        if (resolvedNode.kind == MapNodeType.Shop && _shopStates.TryGetValue((resolvedNode.mapIndex, resolvedNode.id), out var shop))
        {
            shop.Completed = true;
            if (shop == _activeShopState) shop.MadePricedPurchase = _madeSuccessfulPurchaseInCurrentShop;
        }
        // Completion hooks count visited nodes, not route depths. Checkpoints retain
        // existing Gear visit/counter behavior; their fractional column consumes no depth.
        _completedNodeCount = IncrementToMaximum(_completedNodeCount);
        ApplyNodeCompletionTracking(resolvedNode);
        foreach (var nextId in resolvedNode.next)
        {
            var next = FindNode(nextId);
            if (next == null || next.completed) continue;
            next.accessible = true;
            if (!next.hidden) next.revealed = true;
        }
        GameplayEffectResolver.ApplyNodeCompleted(new NodeCompletedEffectContext(
            this, CardManager.Instance, resolvedNode, _completedNodeCount));
        RefreshCurrentMapEffects();
        TryOpenHammerReward();
    }

    void ShowPathAfterNode()
    {
        if (!IsRunCompleted && _activeEvent == null)
            OnShowPathScreen?.Invoke();
        SaveGameService.Checkpoint(this);
    }

    void HandleHeadlinerVictory()
    {
        var defeatedHeadliner = CurrentHeadliner;
        if (defeatedHeadliner == null)
        {
            Debug.LogError("RunManager: boss victory has no matching boss definition.", this);
            return;
        }

        var reward = CardManager.Instance.AddHeadlinerReward(defeatedHeadliner.sourceCard);
        OnHeadlinerRewardGranted?.Invoke(reward);

        SettleContractAtMapExit();
        if (IsInfiniteMode && headlinerIndex == int.MaxValue)
        {
            Timing.CompleteCurrentMap(false);
            Timing.StopRun();
            IsRunCompleted = true;
            GameplayBalanceTelemetry.Finish("completed");
            OnRunCompleted?.Invoke();
            return;
        }
        bool finalMap = !IsInfiniteMode && headlinerIndex + 1 >= headlinerDeck.Count;
        Timing.CompleteCurrentMap(!finalMap);
        headlinerIndex++;
        if (!IsInfiniteMode && headlinerIndex >= headlinerDeck.Count)
        {
            Timing.StopRun();
            IsRunCompleted = true;
            GameplayBalanceTelemetry.Finish("completed");
            OnRunCompleted?.Invoke();
            return;
        }

        NextCycle();
    }

    void SubscribeToCombatResults()
    {
        var combatManager = CombatManager.Instance;
        if (_subscribedCombatManager == combatManager) return;

        UnsubscribeFromCombatResults();
        _subscribedCombatManager = combatManager;
        if (_subscribedCombatManager != null)
        {
            _subscribedCombatManager.OnEncounterResult += HandleEncounterResult;
            _subscribedCombatManager.DiscardChoiceRequester = RequestGearDiscardChoice;
        }
    }

    void UnsubscribeFromCombatResults()
    {
        if (_subscribedCombatManager != null)
        {
            _subscribedCombatManager.OnEncounterResult -= HandleEncounterResult;
            if (_subscribedCombatManager.DiscardChoiceRequester == (Func<string, string, IReadOnlyList<CardInstance>, Action<int>, bool>)RequestGearDiscardChoice)
                _subscribedCombatManager.DiscardChoiceRequester = null;
        }
        _subscribedCombatManager = null;
    }

    public bool ContinueInfiniteMode()
    {
        if (!CanContinueInfiniteMode || !Timing.ResumeAtNextMap()) return false;
        IsInfiniteMode = true;
        IsRunCompleted = false;
        SaveGameService.ResumeSaving(this);
        NextCycle();
        SaveGameService.Checkpoint(this, true);
        return true;
    }

    public void AbandonRun()
    {
        GameplayBalanceTelemetry.Finish("abandoned");
        SaveGameService.EndRun(this);
        Timing.StopRun();
    }

    public void Reset()
    {
        ResetRuntimeState();
        runSeed = string.Empty;
        _randomContext = null;
    }

    void ResetRuntimeState()
    {
        _activeNode = null;
        _activeEvent = null;
        _shopStates.Clear();
        _activeShopState = null;
        _shopTransaction = false;
        _activeShopOffers = new List<ShopOffer>();
        _usedSpecialShopOffers = new HashSet<ShopOfferKind>();
        _purchasedConsumablesInShop = new HashSet<string>();
        Timing.Reset();
        IsRunCompleted = false;
        IsInfiniteMode = false;
        GameplayInputGate.Set(GameplayInputBlockReason.GearChoice, false);
        OnHideShop?.Invoke();
        OnHideEvent?.Invoke();
        GameplayInputGate.Set(GameplayInputBlockReason.ConsumableReward, false);
        GameplayInputGate.Set(GameplayInputBlockReason.GearChoice, false);
        _pendingConsumableRewards.Clear();
        _pendingHammerReward = false;
        _hammerRetryPending = false;
        _pendingHammerEnhancements.Clear();
        if (_pendingHammerEvent != null) DestroyRuntimeObject(_pendingHammerEvent);
        _pendingHammerEvent = null;
        ClearPendingBloodRitual();
        OnConsumableRewardChanged?.Invoke();

        foreach (var ability in _generatedEnemyAbilities)
            if (ability != null) DestroyRuntimeObject(ability);
        _generatedEnemyAbilities.Clear();
        foreach (var enemy in _generatedEnemyTypes)
        {
            if (enemy == null) continue;
            var sourceCard = enemy.sourceCard;
            enemy.sourceCard = null;
            DestroyRuntimeObject(enemy);
            if (sourceCard != null)
                DestroyRuntimeObject(sourceCard);
        }

        _generatedEnemyTypes.Clear();
        headlinerDeck.Clear();
        headlinerIndex = 0;
        _completedNodeCount = 0;
        _pendingInvestmentStakes.Clear();
        _paidItemPurchaseCount = 0;
        _combatWinCount = 0;
        _merchantGiftRewardCount = 0;
        _satchelRewardCount = 0;
        _activeContract = null;
        _madeSuccessfulPurchaseInCurrentShop = false;
        if (_pendingContractSelectionEvent != null) DestroyRuntimeObject(_pendingContractSelectionEvent);
        _pendingContractSelectionEvent = null;
        _pendingContractPrice = 0;
        _usedSpecialShopOffers.Clear();
        _purchasedConsumablesInShop.Clear();
        _consumableDropResolvedContexts.Clear();
        _resolvedConsumableRewardContexts.Clear();
        _alchemistChoicesByMap.Clear();
        _visitedNodeTypesCurrentMap.Clear();
        _visitedNodeTypesMapIndex = -1;
        currentPath.Clear();
    }

    static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
