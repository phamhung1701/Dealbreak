using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GameState { Idle, PlayerTurn, EnemyAttacking, GameOver, GameWon }

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "CombatManager")]
public partial class CombatManager : Singleton<CombatManager>
{
    [Header("Critical Hits")]
    [SerializeField, Range(0f, 100f)] float criticalChancePercent = 25f;
    IRandomSource _criticalRandom;
    IRandomSource _duelistSuitRandom;

    [Header("Runtime State")]
    public GameState currentState = GameState.Idle;
    public EnemyRuntime currentEnemy;
    public int pendingDamage;

    readonly List<EnemyRuntime> _enemies = new();
    readonly List<int> _pendingAttacks = new();
    readonly List<EnemyRuntime> _pendingAttackSources = new();
    readonly Queue<PlayerTurnGrant> _queuedPlayerTurnGrants = new();
    int _attackCountAtStart;
    long _parryIncomingDamage;
    long _parryEffectiveBlock;
    bool _parryResolved;
    bool _parryDamageLeaked;
    readonly HashSet<int> _parryDrawCandidates = new();
    readonly Queue<CardView> _overflowAutoPlays = new();
    int _overflowBudgetRemaining;
    long _overflowRootActionId;
    bool _overflowRootActive;
    bool _executingOverflowAutoPlay;
    int _completedPlayerTurnCount;
    bool _isBonusPlayerAction;
    public int PendingAttackCount => _pendingAttacks.Count;
    public int BlockedAttackCount => _attackCountAtStart - _pendingAttacks.Count;

    public int GetPendingAttackDamage(EnemyRuntime enemy)
    {
        if (enemy == null) return 0;
        long total = 0;
        for (int i = 0; i < _pendingAttacks.Count; i++)
            if (ReferenceEquals(_pendingAttackSources[i], enemy))
                total = Math.Min(int.MaxValue, total + _pendingAttacks[i]);
        return (int)total;
    }
    readonly HashSet<EnemyRuntime> _processedEnemyDeaths = new();
    readonly CombatResolver _resolver = new();
    readonly CombatReactionQueue _reactions = new();
    int _earnedCreditsReward;
    long _nextActionId = 1;
    int _nextReactiveHitIndex;
    int _playerTurnNumber;
    int _playerAttackActionCount;
    bool _openingPlayerAttackInProgress;
    int _activeKingslayerBonus;
    int _activeDwarfBonus;
    int _activeBalancerBonus;
    int _activeSpadeBonus;
    int _activeHeartBonus;
    bool _handsEpicAction;
    CardInstance _activeDwarfSourceCard;
    int _pendingDiamondDrawChoices;
    int _encounterActualHpLost;
    bool _isEliteOrHeadlinerEncounter;
    bool _isHeadlinerEncounter;
    bool _isEliteEncounter;
    int _eliteKillCount;
    public int EliteKillCount => _eliteKillCount;
    public int EncounterDefeatedEnemyCount => _processedEnemyDeaths.Count;
    public int EncounterActualHpLost => _encounterActualHpLost;
    public bool IsHeadlinerEncounter => _isHeadlinerEncounter;
    public bool IsOpeningPlayerAttack => _openingPlayerAttackInProgress || _playerAttackActionCount == 0;
    public int QueuedExtraPlayerTurns => _queuedPlayerTurnGrants.Count;
    public int CompletedPlayerTurnCount => _completedPlayerTurnCount;
    public bool IsBonusPlayerAction => _isBonusPlayerAction;
    bool _isResolvingAction;
    bool _reactionFaulted;
    CombatActionContext _activeAction;
    CombatReactionPhase? _processingReactionPhase;
    public IReadOnlyList<EnemyRuntime> Enemies => _enemies;
    public int TotalEnemyAttack
    {
        get
        {
            long total = 0;
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i] != null && !_enemies[i].IsDefeated)
                    total = Math.Min(int.MaxValue, total + _enemies[i].EffectiveAttack);
            return (int)total;
        }
    }

    public PlayerRuntime player { get; } = new();

    int _configuredPlayerMaxHealth = PlayerRuntime.DefaultMaxHealth;

    public event Action<GameState> OnStateChanged;
    public event Action OnEnemyChanged;
    public event Action OnEnemiesChanged;
    public event Action OnEnemyHpChanged;
    public event Action<int> OnPendingDamageChanged;
    public event Action<int, int> OnPlayerHealthChanged;
    public event Action<EncounterResult> OnEncounterResult;
    public event Action<DamageResult> OnDamageResolved;
    public event Action<DefenseAttackResult> OnDefenseAttackResolved;
    public event Action<string> OnCombatLog;

    public bool IsResolvingAction => _isResolvingAction;
    public CombatActionContext ActiveAction => _activeAction;

    bool _encounterResolved;

    readonly struct DefenseAssignment
    {
        public CardInstance BlockingCard { get; }
        public int CardOrder { get; }
        public int AttackIndex { get; }
        public int AttackDamageBeforeBlock { get; }
        public int BlockedDamage { get; }

        public DefenseAssignment(CardInstance blockingCard, int cardOrder, int attackIndex,
            int attackDamageBeforeBlock, int blockedDamage)
        {
            BlockingCard = blockingCard;
            CardOrder = cardOrder;
            AttackIndex = attackIndex;
            AttackDamageBeforeBlock = attackDamageBeforeBlock;
            BlockedDamage = blockedDamage;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        player.HealingAllowed = CanPlayerHeal;
    }

    public bool CanPlayerHeal()
    {
        for (int i = 0; i < _enemies.Count; i++)
            if (_enemies[i] != null && !_enemies[i].IsDefeated && _enemies[i].HasWithering) return false;
        return true;
    }

    public void ConfigurePlayer(int maxHealth)
    {
        _configuredPlayerMaxHealth = maxHealth;
        player.Configure(maxHealth);
        player.Reset();
        NotifyPlayerHealthChanged();
    }

    public void StartEnemy(EnemyRuntime enemy)
    {
        if (enemy == null) throw new ArgumentNullException(nameof(enemy));
        StartEncounter(new[] { enemy });
    }

    public bool IsResolvingOverflowAttack => _executingOverflowAutoPlay;
    public bool CanStartEncounter => !_reactionFaulted &&
        currentState is GameState.Idle or GameState.GameWon;

    public void StartEncounter(IReadOnlyList<EnemyRuntime> enemies, MapNodeType encounterKind = MapNodeType.Combat)
    {
        TryStartEncounter(enemies, encounterKind);
    }

    public bool TryStartEncounter(IReadOnlyList<EnemyRuntime> enemies, MapNodeType encounterKind = MapNodeType.Combat)
    {
        if (enemies == null || enemies.Count == 0)
            throw new ArgumentException("An encounter requires at least one enemy.", nameof(enemies));
        if (!CanStartEncounter) return false;

        UnsubscribeFromEnemies();
        CardManager.Instance?.CancelCardInteractions();
        _enemies.Clear();
        _pendingAttacks.Clear();
        _pendingAttackSources.Clear();
        _attackCountAtStart = 0;
        _parryResolved = true;
        _parryDrawCandidates.Clear();
        _queuedPlayerTurnGrants.Clear();
        _completedPlayerTurnCount = 0;
        var uniqueEnemies = new HashSet<EnemyRuntime>();
        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null || !uniqueEnemies.Add(enemy))
                throw new ArgumentException("Encounter enemies must be non-null unique instances.", nameof(enemies));
            _enemies.Add(enemy);
        }

        currentEnemy = _enemies[0];
        player.HealingAllowed = CanPlayerHeal;
        var captain = _enemies.Find(enemy => enemy.HasCaptaincy && !enemy.IsDefeated);
        foreach (var enemy in _enemies)
            enemy.AssignCaptain(captain != null && EncounterComposer.IsCaptainSupported(captain.type, enemy.type)
                ? captain : null);
        _earnedCreditsReward = 0;
        _processedEnemyDeaths.Clear();
        _reactions.Clear();
        _playerTurnNumber = 0;
        _playerAttackActionCount = 0;
        _encounterActualHpLost = 0;
        _eliteKillCount = 0;
        _isEliteEncounter = encounterKind == MapNodeType.Elite;
        _isEliteOrHeadlinerEncounter = encounterKind is MapNodeType.Elite or MapNodeType.Headliner;
        _isHeadlinerEncounter = encounterKind == MapNodeType.Headliner;
        _encounterResolved = false;
        player.ResetShield();
        player.ResetEncounterBlock();
        var cards = CardManager.Instance;
        if (cards != null && cards.HasGearSpecialRule(GearSpecialRule.PreparationManual))
            player.GainEncounterBlock(cards.DistinctConsumableTypeCount * 2);
        if (cards != null)
            for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
            {
                var gear = cards.ownedGear[gearIndex];
                if (gear != null && gear.specialRule == GearSpecialRule.WarpathBanner)
                    player.GainEncounterBlock(Math.Min(6,
                        cards.GetGearInstanceAt(gearIndex).State.GetCounter(-1340) * 2));
            }
        if (_isEliteOrHeadlinerEncounter && cards?.ownedGear.Any(a => a != null &&
            a.specialRule == GearSpecialRule.ChallengerCrest) == true)
            player.GainShield(1);
        cards?.ResetGearEncounterEffectState();
        SetPendingDamage(0, true);
        var encounterAction = CreateAction(CombatActionOrigin.Legacy);
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            enemy.ResetShield();
            enemy.ResetResponseIntent();
            enemy.OnHpChanged += HandleEnemyHpChanged;
            enemy.OnAttackChanged += HandleEnemyHpChanged;
            EnqueueEncounterStartedAbilities(encounterAction, enemy, i);
        }
        if (!ProcessReactions(CombatReactionPhase.EncounterStarted)) return true;

        int drawn = CardManager.Instance.DrawToHand(1);
        GameplayEffectResolver.EnqueueEncounterStart(
            new EncounterStartEffectContext(encounterAction, player, this, CardManager.Instance,
                _enemies, drawn), _reactions);
        ProcessReactions(CombatReactionPhase.EncounterReady);
        if (_reactionFaulted) return true;

        OnEnemiesChanged?.Invoke();
        OnEnemyChanged?.Invoke();
        BeginPlayerTurn();
        if (_reactionFaulted) return true;
        Log(_enemies.Count == 1
            ? $"Enemy appears: {_enemies[0].DisplayName} (HP: {_enemies[0].maxHp}, ATK: {_enemies[0].EffectiveAttack})"
            : $"Enemy group appears: {string.Join(", ", _enemies.ConvertAll(enemy => $"{enemy.DisplayName} {enemy.currentHp} HP/{enemy.EffectiveAttack} ATK"))}");
        if (drawn > 0)
            Log($"Drew {drawn} card for the encounter.");
        return true;
    }

    public bool SelectEnemyTarget(EnemyRuntime enemy)
    {
        if (enemy == null || enemy.IsDefeated || _reactionFaulted || !_enemies.Contains(enemy)) return false;
        if (ReferenceEquals(currentEnemy, enemy)) return true;
        currentEnemy = enemy;
        OnEnemyChanged?.Invoke();
        return true;
    }

    public int QueuedOverflowAutoPlayCount => _overflowAutoPlays.Count;
    public int OverflowDrawAllowance => _overflowRootActive && currentState == GameState.PlayerTurn
        ? _overflowBudgetRemaining : 0;

    public void QueueOverflowAutoPlay(CardView card)
    {
        if (card == null || !_overflowRootActive || _overflowBudgetRemaining <= 0 ||
            currentState != GameState.PlayerTurn || _encounterResolved) return;
        _overflowBudgetRemaining--;
        _overflowAutoPlays.Enqueue(card);
    }

    public bool IsPlayerActionActive => _isResolvingAction && currentState == GameState.PlayerTurn;

    public bool CanAutoPlayOverflow => _overflowRootActive && currentState == GameState.PlayerTurn &&
        !player.IsDefeated && !_encounterResolved && !_reactionFaulted;

    public bool CanUseConsumableDamage(int amount) => CanUseConsumableDamage(amount, currentEnemy);

    public bool CanUseConsumableDamage(int amount, EnemyRuntime target) =>
        !GameplayInputGate.IsBlocked && amount > 0 && !_reactionFaulted && !_isResolvingAction &&
        currentState is (GameState.PlayerTurn or GameState.EnemyAttacking) && !_encounterResolved &&
        target != null && !target.IsDefeated && _enemies.Contains(target);

    public bool TryUseConsumableDamage(int amount) => TryUseConsumableDamage(amount, currentEnemy);

    public bool TryUseConsumableDamage(int amount, EnemyRuntime target)
    {
        if (!CanUseConsumableDamage(amount, target)) return false;
        _isResolvingAction = true;
        _reactions.Clear();
        _activeAction = CreateAction(CombatActionOrigin.Consumable, sourcePlayer: player,
            targetEnemy: target);
        _nextReactiveHitIndex = 1;
        try
        {
            var request = new DamageRequest(_activeAction, 0, CombatDamageOrigin.Consumable,
                player, target, amount);
            var result = ResolveDamage(request);
            if (target.IsDefeated)
                HandleEnemyDefeated(target);
            else
                EnqueueIncomingHitResolvedAbilities(_activeAction, target, result);

            ProcessReactions(CombatReactionPhase.HitResolved);
            if (_reactionFaulted) return true;
            Log($"Trick dealt {result.ActualHpLost} damage to {target.DisplayName}.");
            // A Trick may kill the last enemy while blocking; that also ends the encounter.
            TryResolveTerminalOutcome();
            return true;
        }
        finally
        {
            _activeAction = null;
            _isResolvingAction = false;
            _reactions.Clear();
        }
    }

    public bool ShouldReplaceSelectionOnAdd => currentState == GameState.PlayerTurn;

    public bool CanAddCardToSelection(IReadOnlyList<CardView> selectedCards, CardView candidate)
    {
        if (GameplayInputGate.IsBlocked || candidate?.data == null ||
            !AreValidHandViews(new[] { candidate }))
            return false;

        return currentState switch
        {
            GameState.PlayerTurn => CanExtendPlayerSelection(selectedCards, candidate),
            GameState.EnemyAttacking => CanAddDefenseCard(selectedCards, candidate),
            _ => false
        };
    }

    bool CanExtendPlayerSelection(IReadOnlyList<CardView> selectedCards, CardView candidate)
    {
        int count = selectedCards?.Count ?? 0;
        if (count == 0) return true;
        if (count == 1)
        {
            // Permit a second click so the UI can replace an ordinary selection; ToggleCardSelection
            // keeps it only when it forms an Ace pair, Hands set, or Royal Family prefix.
            return true;
        }
        if (GameplayEffectResolver.CanContinueSameRankSelection(selectedCards, candidate, CardManager.Instance)) return true;
        return GameplayEffectResolver.CanContinueAttackSelection(selectedCards, candidate, CardManager.Instance);
    }

    public bool CanExtendSameRankSelection(IReadOnlyList<CardView> selectedCards, CardView candidate) =>
        GameplayEffectResolver.CanContinueSameRankSelection(selectedCards, candidate, CardManager.Instance);

    public bool CanPairSelection(IReadOnlyList<CardView> selectedCards, CardView candidate)
    {
        return currentState == GameState.PlayerTurn && selectedCards != null && selectedCards.Count == 1 &&
            selectedCards[0]?.data != null && candidate?.data != null &&
            (selectedCards[0].data.Rank == CardData.Rank.Ace || candidate.data.Rank == CardData.Rank.Ace);
    }

    public int CalculateSelectedDefense(IReadOnlyList<CardView> cards)
    {
        if (cards == null) return 0;
        int total = 0;
        for (int i = 0; i < cards.Count; i++)
            if (cards[i]?.data != null)
                total += CalculateCardDefense(cards[i].data);
        return total;
    }

    public bool TryPreviewDefense(IReadOnlyList<CardView> cards, out int blockedCount, out int remainingDamage)
    {
        blockedCount = 0;
        remainingDamage = pendingDamage;
        if (cards == null || cards.Count == 0 || !AreValidHandViews(cards)) return false;
        var matched = MatchDefenseAssignments(cards);
        if (matched == null) return false;
        var reductions = new int[_pendingAttacks.Count];
        for (int i = 0; i < matched.Count; i++)
        {
            reductions[matched[i].AttackIndex] += matched[i].BlockedDamage;
            remainingDamage -= matched[i].BlockedDamage;
        }
        for (int i = 0; i < reductions.Length; i++)
            if (reductions[i] >= _pendingAttacks[i]) blockedCount++;
        return true;
    }

    bool CanAddDefenseCard(IReadOnlyList<CardView> selectedCards, CardView candidate)
    {
        if (pendingDamage <= 0 || currentState != GameState.EnemyAttacking) return false;
        var proposed = new List<CardView>((selectedCards?.Count ?? 0) + 1);
        if (selectedCards != null) proposed.AddRange(selectedCards);
        proposed.Add(candidate);
        return AreValidHandViews(proposed) && MatchDefenseAssignments(proposed) != null;
    }

    // Full-coverage attacks require one sufficient card. Ordinary attacks accept partial
    // Block and can receive multiple cards. Stronger cards/attacks win deterministic ties.
    List<DefenseAssignment> MatchDefenseAssignments(IReadOnlyList<CardView> cards)
    {
        var cardOrder = new List<int>(cards.Count);
        for (int i = 0; i < cards.Count; i++) cardOrder.Add(i);
        cardOrder.Sort((a, b) =>
        {
            int comparison = CalculateCardDefense(cards[b].data).CompareTo(CalculateCardDefense(cards[a].data));
            return comparison != 0 ? comparison : a.CompareTo(b);
        });
        var remaining = _pendingAttacks.ToArray();
        var matched = new List<DefenseAssignment>(cards.Count);
        foreach (int cardIndex in cardOrder)
        {
            int defense = CalculateCardDefense(cards[cardIndex].data);
            if (defense <= 0) return null;
            int attackIndex = FindDefenseTarget(remaining, defense, true, true);
            if (attackIndex < 0) attackIndex = FindDefenseTarget(remaining, defense, false, true);
            if (attackIndex < 0) attackIndex = FindDefenseTarget(remaining, defense, false, false);
            if (attackIndex < 0) return null;
            int before = remaining[attackIndex];
            int blocked = Mathf.Min(defense, before);
            remaining[attackIndex] -= blocked;
            matched.Add(new DefenseAssignment(cards[cardIndex].data, cardIndex, attackIndex, before, blocked));
        }
        return matched;
    }

    int FindDefenseTarget(IReadOnlyList<int> remaining, int defense,
        bool requiresFullCoverage, bool mustFullyCover)
    {
        int best = -1;
        for (int i = 0; i < remaining.Count; i++)
        {
            int damage = remaining[i];
            if (damage <= 0 || i >= _pendingAttackSources.Count) continue;
            bool restricted = _pendingAttackSources[i] != null &&
                _pendingAttackSources[i].RequiresFullCoverageBlock;
            if (restricted != requiresFullCoverage || mustFullyCover && defense < damage) continue;
            if (best < 0 || damage > remaining[best]) best = i;
        }
        return best;
    }

    public void PlayCard(CardView card)
    {
        if (card != null)
            TryPlayCards(new[] { card }, currentEnemy);
    }

    public float CriticalChancePercent
    {
        get => criticalChancePercent;
        set => criticalChancePercent = Mathf.Clamp(value, 0f, 100f);
    }

    public void ConfigureCriticalRandom(IRandomSource random) => _criticalRandom = random;

    public void ConfigureDuelistSuitRandom(IRandomSource random) => _duelistSuitRandom = random;

    public bool CanPlayCards(IReadOnlyList<CardView> cards, EnemyRuntime target)
    {
        if (GameplayInputGate.IsBlocked || _reactionFaulted || _isResolvingAction || currentState != GameState.PlayerTurn ||
            target == null || target.IsDefeated || !_enemies.Contains(target) || _encounterResolved ||
            !AreValidHandViews(cards) || !GameplayEffectResolver.CanPlaySelection(cards, target, CardManager.Instance))
            return false;
        var instances = new CardInstance[cards.Count];
        for (int i = 0; i < cards.Count; i++) instances[i] = cards[i].data;
        int cost = GameplayEffectResolver.GetVampiricHealthCost(instances);
        return cost == 0 || CanTakeRunDamage(cost);
    }

    public bool TryPlayCards(IReadOnlyList<CardView> cards, EnemyRuntime target)
    {
        return TryPlayCards(cards, target, 1);
    }

    public bool TryPlayCards(IReadOnlyList<CardView> cards, EnemyRuntime target, int hitCount)
    {
        if (hitCount <= 0 || !CanPlayCards(cards, target)) return false;
        var cardInstances = new CardInstance[cards.Count];
        for (int i = 0; i < cards.Count; i++) cardInstances[i] = cards[i].data;
        // Derive the enhancement's extra hit from the committed gameplay data, not from UI input.
        if (GameplayEffectResolver.HasDoubleStrikeEffect(cardInstances)) hitCount = 2;
        _openingPlayerAttackInProgress = _playerAttackActionCount == 0;
        _activeKingslayerBonus = 0;
        _activeDwarfBonus = 0;
        _activeBalancerBonus = 0;
        _activeSpadeBonus = 0;
        _activeHeartBonus = 0;
        _activeDwarfSourceCard = null;

        long rawCardDamageTotal = 0;
        var names = new List<string>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            rawCardDamageTotal += CalculateCardAttackDamage(cardInstances[i], cardInstances, i == 0);
            names.Add(cardInstances[i].DisplayName);
        }
        int aggregatedDamage = (int)Math.Min(rawCardDamageTotal, int.MaxValue);
        PreparePhase7ActionBonuses(cardInstances);
        if (_activeKingslayerBonus + _activeDwarfBonus + _activeBalancerBonus + _activeSpadeBonus + _activeHeartBonus > 0)
        {
            rawCardDamageTotal = 0;
            for (int i = 0; i < cards.Count; i++)
                rawCardDamageTotal += CalculateCardAttackDamage(cardInstances[i], cardInstances, i == 0);
            aggregatedDamage = (int)Math.Min(rawCardDamageTotal, int.MaxValue);
        }
        int vampiricCost = GameplayEffectResolver.GetVampiricHealthCost(cardInstances);
        bool isExplosive = GameplayEffectResolver.HasExplosiveEffect(cardInstances);
        bool hasLethal = cardInstances.Any(GameplayEffectResolver.IsLethalEnchantment);
        bool isRoyalFamily = GameplayEffectResolver.IsRoyalFamilySelection(cardInstances, CardManager.Instance);
        var cm = CardManager.Instance;
        bool handsGroup = GameplayEffectResolver.IsHandsMultiCardAction(cardInstances, cm);
        _handsEpicAction = handsGroup && cm != null && cm.HasGearSpecialRule(GearSpecialRule.HandsEpic);
        bool critical = false;
        bool isAcePair = cards.Count == 2 && GameplayEffectResolver.CanPlayAsAcePair(cards);
        bool hasCriticalChanceOverride = GameplayEffectResolver.TryGetCriticalChanceOverride(
            cardInstances, cm, out float criticalChanceOverride);
        float actionCriticalChance = hasCriticalChanceOverride
            ? criticalChanceOverride : criticalChancePercent;

        bool isOverflowAutoPlay = _executingOverflowAutoPlay;
        if (!isOverflowAutoPlay)
        {
            _overflowRootActive = true;
            _overflowRootActionId = _nextActionId;
            _overflowBudgetRemaining = cm != null ? cm.OverflowCapacity : 0;
            _overflowAutoPlays.Clear();
        }
        _isResolvingAction = true;
        _reactions.Clear();
        _activeAction = CreateAction(
            CombatActionOrigin.PlayerCard,
            sourcePlayer: player,
            card: cardInstances[0],
            targetEnemy: target,
            hitCount: hitCount);
        int damagePerHit = GameplayEffectResolver.CalculateActionDamage(
            new ActionDamageEffectContext(_activeAction, cardInstances, target, cm, aggregatedDamage,
                handsGroup),
            out int actionDamageCap);
        _nextReactiveHitIndex = Math.Max(hitCount, handsGroup ? cards.Count : hitCount);

        try
        {
            if (!cm.TryDiscardCards(cards)) return false;
            _playerAttackActionCount++;
            ConsumeAndAdvancePhase7ActionState(cardInstances, isOverflowAutoPlay);
            if (vampiricCost > 0) TakeRunDamage(vampiricCost);

            var handSnapshot = new List<CardInstance>(cm.hand);
            GameplayEffectResolver.EnqueueAttackCommitted(
                new AttackCommittedEffectContext(_activeAction, cardInstances, handSnapshot,
                    target, this, cm), _reactions);
            bool reactionFailed = !ProcessReactions(CombatReactionPhase.AttackCommitted);

            if (!reactionFailed && !isRoyalFamily &&
                (isAcePair || hasCriticalChanceOverride || hasLethal))
            {
                if (actionCriticalChance >= 100f) critical = true;
                else if (actionCriticalChance > 0f && _criticalRandom != null)
                    critical = _criticalRandom.NextFloat() * 100f < actionCriticalChance;
            }
            if (critical) damagePerHit = (int)Math.Min((long)damagePerHit * 2, int.MaxValue);
            int remainingActionDamage = actionDamageCap < 0 ? -1 :
                (int)Math.Min((long)actionDamageCap * (critical ? 2 : 1), int.MaxValue);

            int totalHpLost = 0;
            int resolvedHits = 0;
            if (isRoyalFamily || handsGroup)
            {
                long damageBudget = actionDamageCap < 0
                    ? Math.Max(0L, (long)damagePerHit)
                    : Math.Min(Math.Max(0L, (long)actionDamageCap * (critical ? 2 : 1)),
                        Math.Max(0L, (long)damagePerHit));
                long expectedBaseAfterCritical = rawCardDamageTotal * (critical ? 2 : 1);
                long actionBonus = Math.Max(0L, (long)damagePerHit - expectedBaseAfterCritical);
                for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    EnqueueCardCommittedEffects(_activeAction, cardInstances[cardIndex], target, cm, cardIndex);
                    reactionFailed = !ProcessReactions(CombatReactionPhase.CardCommitted);
                    if (reactionFailed) break;
                    if (player.IsDefeated)
                    {
                        ResolveEncounter(EncounterResult.Defeat);
                        return true;
                    }
                    if (isRoyalFamily || target.IsDefeated || !_enemies.Contains(target)) continue;

                    long perCardDamage = CalculateCardAttackDamage(cardInstances[cardIndex], cardInstances, cardIndex == 0);
                    if (cardIndex == 0) perCardDamage += actionBonus;
                    if (critical) perCardDamage *= 2;
                    int requestedDamage = (int)Math.Min(Math.Max(0L, perCardDamage), damageBudget);
                    damageBudget -= requestedDamage;
                    if (requestedDamage <= 0) continue;

                    var request = new DamageRequest(_activeAction, cardIndex, CombatDamageOrigin.Card,
                        player, target, requestedDamage, hitCard: cardInstances[cardIndex], isCritical: critical);
                    var result = ResolveDamage(request);
                    totalHpLost += result.ActualHpLost;
                    resolvedHits++;
                    if (!target.IsDefeated)
                        EnqueueIncomingHitResolvedAbilities(_activeAction, target, result);
                    reactionFailed = !ProcessReactions(CombatReactionPhase.HitResolved);
                    if (_reactionFaulted) return true;
                    if (player.IsDefeated)
                    {
                        ResolveEncounter(EncounterResult.Defeat);
                        return true;
                    }
                    if (target.IsDefeated)
                    {
                        AwardDevouringKillBonus(cardInstances);
                        HandleEnemyDefeated(target);
                    }
                }

                if (!reactionFailed && !player.IsDefeated && isRoyalFamily && !target.IsDefeated)
                {
                    // Explicit defeat outcome: never encode instant defeat as an oversized damage request.
                    target.DefeatInstantly();
                    HandleEnemyDefeated(target);
                }
                if (player.IsDefeated)
                {
                    ResolveEncounter(EncounterResult.Defeat);
                    return true;
                }
            }
            else
            {
                for (int cardIndex = 0; cardIndex < cards.Count && !reactionFailed; cardIndex++)
                {
                    EnqueueCardCommittedEffects(_activeAction, cardInstances[cardIndex], target, cm, cardIndex);
                    reactionFailed = !ProcessReactions(CombatReactionPhase.CardCommitted);
                }

                for (int hitIndex = 0; hitIndex < hitCount && !reactionFailed; hitIndex++)
                {
                    if (target.IsDefeated || !_enemies.Contains(target)) break;
                    int requestedDamage = remainingActionDamage < 0
                        ? damagePerHit : Math.Min(damagePerHit, remainingActionDamage);
                    if (requestedDamage <= 0 && remainingActionDamage == 0) break;
                    if (remainingActionDamage >= 0) remainingActionDamage -= requestedDamage;

                    var request = new DamageRequest(
                        _activeAction,
                        hitIndex,
                        CombatDamageOrigin.Card,
                        player,
                        target,
                        requestedDamage,
                        hitCard: cardInstances[Math.Min(hitIndex, cardInstances.Length - 1)],
                        isCritical: critical);
                    var result = ResolveDamage(request);
                    totalHpLost += result.ActualHpLost;
                    resolvedHits++;
                    if (!target.IsDefeated)
                        EnqueueIncomingHitResolvedAbilities(_activeAction, target, result);
                    reactionFailed = !ProcessReactions(CombatReactionPhase.HitResolved);
                    if (_reactionFaulted) return true;
                    if (player.IsDefeated)
                    {
                        ResolveEncounter(EncounterResult.Defeat);
                        return true;
                    }
                    if (target.IsDefeated)
                    {
                        AwardDevouringKillBonus(cardInstances);
                        HandleEnemyDefeated(target);
                        break;
                    }
                }
            }

            if (critical && isAcePair)
                TryReturnCriticalAce(cardInstances, cm);

            if (!reactionFailed && isExplosive && !isRoyalFamily)
                ResolveExplosiveDamage(cardInstances, target, damagePerHit, critical);

            if (hitCount == 1)
                Log($"Played {string.Join(" + ", names)} on {target.DisplayName}{(critical ? " (CRITICAL)" : string.Empty)} for {totalHpLost} damage! (HP: {target.currentHp}/{target.maxHp})");
            else
                Log($"Played {string.Join(" + ", names)} on {target.DisplayName}{(critical ? " (CRITICAL)" : string.Empty)}: {resolvedHits}/{hitCount} hits dealt {totalHpLost} damage. (HP: {target.currentHp}/{target.maxHp})");

            if (!reactionFailed && !target.IsDefeated && _enemies.Contains(target))
            {
                EnqueuePlayerCardResolvedAbilities(_activeAction, target);
                reactionFailed = !ProcessReactions(CombatReactionPhase.CardResolved);
            }

            if (_reactionFaulted) return true;
            if (isOverflowAutoPlay) return true;
            var completedAction = _activeAction;
            DrainOverflowAutoPlays();
            _activeAction = completedAction;
            if (player.IsDefeated)
            {
                ResolveEncounter(EncounterResult.Defeat);
                return true;
            }
            if (_encounterResolved || _reactionFaulted) return true;
            if (_pendingDiamondDrawChoices > 0)
            {
                int queuedChoices = _pendingDiamondDrawChoices;
                _pendingDiamondDrawChoices = 0;
                if (OpenNextDiamondDrawChoice(queuedChoices, completedAction, cm, cardInstances))
                    return true;
            }
            FinishCommittedPlayerAction(completedAction, cm, cardInstances);
            return true;
        }
        finally
        {
            _activeAction = null;
            _isResolvingAction = false;
            _openingPlayerAttackInProgress = false;
            _activeKingslayerBonus = 0;
            _activeDwarfBonus = 0;
            _activeBalancerBonus = 0;
            _activeSpadeBonus = 0;
            _activeHeartBonus = 0;
            _activeDwarfSourceCard = null;
            _handsEpicAction = false;
            ClearPendingDwarfSources();
            _reactions.Clear();
            if (!isOverflowAutoPlay)
            {
                _overflowRootActive = false;
                _overflowRootActionId = 0;
                _overflowBudgetRemaining = 0;
                if (_encounterResolved || _reactionFaulted) _overflowAutoPlays.Clear();
            }
        }
    }

    bool OpenNextDiamondDrawChoice(int remainingChoices, CombatActionContext completedAction,
        CardManager cards, CardInstance[] committedCards)
    {
        if (remainingChoices <= 0 || cards == null) return false;
        if (cards.discardPile.Count == 0)
        {
            Log("Diamond Emblem: no discarded cards are available to choose.");
            return false;
        }
        if (cards.HandCount >= cards.HandCapacity)
        {
            Log("Diamond Emblem: the hand is full; the discard draw was skipped.");
            return false;
        }
        var run = RunManager.Instance;
        if (run == null) return false;
        var candidates = cards.discardPile.ToArray();
        bool opened = run.RequestGearDiscardChoice("DIAMOND EMBLEM",
            "Choose one physical card from the discard pile to return to your hand. Cancel skips this draw.",
            candidates, cardId =>
            {
                if (cardId > 0)
                {
                    var chosen = cards.discardPile.FirstOrDefault(card => card != null && card.Id == cardId);
                    if (chosen != null && cards.TryReturnDiscardCardToHand(chosen))
                        Log($"Diamond Emblem: returned {chosen.DisplayName} from discard to hand.");
                    else if (chosen != null)
                        Log("Diamond Emblem: the hand is full; the discard draw was skipped.");
                }
                if (remainingChoices > 1 && OpenNextDiamondDrawChoice(
                    remainingChoices - 1, completedAction, cards, committedCards)) return;
                FinishCommittedPlayerAction(completedAction, cards, committedCards);
            });
        if (!opened) Log("Diamond Emblem: discard choice UI unavailable; draw was skipped.");
        return opened;
    }

    void FinishCommittedPlayerAction(CombatActionContext completedAction,
        CardManager cards, IReadOnlyList<CardInstance> committedCards)
    {
        if (TryResolveTerminalOutcome()) return;
        CompletePlayerTurnForEnemies();
        if (TryResolveTerminalOutcome()) return;
        _completedPlayerTurnCount++;
        GameplayEffectResolver.EnqueueCompletedActionTurnGrants(
            new CompletedPlayerActionContext(completedAction, player, this, cards,
                committedCards, _completedPlayerTurnCount), _queuedPlayerTurnGrants);
        if (BeginGrantedPlayerTurn()) return;
        BeginEnemyAttack();
    }

    void DrainOverflowAutoPlays()
    {
        if (!_overflowRootActive || _overflowAutoPlays.Count == 0) return;
        _activeAction = null;
        _isResolvingAction = false;
        _reactions.Clear();
        while (_overflowAutoPlays.Count > 0 && CanAutoPlayOverflow)
        {
            var card = _overflowAutoPlays.Dequeue();
            if (card == null || card.data == null || !CardManager.Instance.hand.Any(instance => ReferenceEquals(instance, card.data)))
                continue;
            var target = currentEnemy != null && !currentEnemy.IsDefeated && _enemies.Contains(currentEnemy)
                ? currentEnemy : _enemies.FirstOrDefault(enemy => enemy != null && !enemy.IsDefeated);
            if (target == null) break;
            bool previous = _executingOverflowAutoPlay;
            _executingOverflowAutoPlay = true;
            try
            {
                Log($"Overflow auto-plays {card.data.DisplayName} on {target.DisplayName}.");
                TryPlayCards(new[] { card }, target, 1);
            }
            finally { _executingOverflowAutoPlay = previous; }
        }
        if (_encounterResolved || _reactionFaulted || player.IsDefeated) _overflowAutoPlays.Clear();
    }

    public void DiscardCard(CardView card)
    {
        if (card != null)
            TryDefendWithCards(new[] { card });
    }

    public bool CanDefendWithCards(IReadOnlyList<CardView> cards)
    {
        return CanDefendWithCards(cards, currentEnemy);
    }

    public bool CanDefendWithCards(IReadOnlyList<CardView> cards, EnemyRuntime target)
    {
        return !GameplayInputGate.IsBlocked && !_reactionFaulted && !_isResolvingAction && currentState == GameState.EnemyAttacking &&
            target != null && !target.IsDefeated && _enemies.Contains(target) && !_encounterResolved &&
            cards != null && cards.Count > 0 && AreValidHandViews(cards) && MatchDefenseAssignments(cards) != null;
    }

    public bool TryDefendWithCards(IReadOnlyList<CardView> cards)
    {
        return TryDefendWithCards(cards, currentEnemy);
    }

    public bool TryDefendWithCards(IReadOnlyList<CardView> cards, EnemyRuntime target)
    {
        if (!CanDefendWithCards(cards, target)) return false;

        var assignments = MatchDefenseAssignments(cards);
        if (assignments == null) return false;
        var names = new List<string>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
            names.Add(cards[i].data.DisplayName);

        _isResolvingAction = true;
        _reactions.Clear();
        _activeAction = CreateAction(CombatActionOrigin.PlayerDefense, sourcePlayer: player);
        _nextReactiveHitIndex = 0;
        try
        {
            var cardManager = CardManager.Instance;
            long effectiveBlock = 0;
            foreach (var view in cards) effectiveBlock += CalculateCardDefense(view.data);
            if (cardManager == null || !cardManager.TryRecycleDefenseCards(cards)) return false;
            _parryEffectiveBlock += effectiveBlock;

            assignments.Sort((left, right) =>
            {
                int comparison = left.AttackIndex.CompareTo(right.AttackIndex);
                return comparison != 0 ? comparison : left.CardOrder.CompareTo(right.CardOrder);
            });
            int blockedDamage = 0;
            var incomingAttacks = _pendingAttacks.ToArray();
            var attackSources = _pendingAttackSources.ToArray();
            var reductions = new int[incomingAttacks.Length];
            var effectiveBlocks = new int[incomingAttacks.Length];
            for (int blockOrder = 0; blockOrder < assignments.Count; blockOrder++)
            {
                var assignment = assignments[blockOrder];
                int attackIndex = assignment.AttackIndex;
                var attacker = _pendingAttackSources[attackIndex];
                blockedDamage += assignment.BlockedDamage;
                reductions[attackIndex] += assignment.BlockedDamage;
                effectiveBlocks[attackIndex] += CalculateCardDefense(assignment.BlockingCard);
                GameplayEffectResolver.EnqueueAttackBlocked(
                    new AttackBlockedEffectContext(_activeAction, assignment.BlockingCard, attacker,
                        assignment.AttackDamageBeforeBlock, blockOrder, this, cardManager), _reactions);
            }

            int blockedAttacks = 0;
            for (int i = _pendingAttacks.Count - 1; i >= 0; i--)
            {
                if (reductions[i] <= 0) continue;
                int remaining = Mathf.Max(0, _pendingAttacks[i] - reductions[i]);
                if (remaining == 0)
                {
                    blockedAttacks++;
                    _pendingAttacks.RemoveAt(i);
                    _pendingAttackSources.RemoveAt(i);
                }
                else _pendingAttacks[i] = remaining;
            }
            SetPendingDamage(pendingDamage - blockedDamage);
            for (int i = 0; i < reductions.Length; i++)
            {
                if (reductions[i] <= 0) continue;
                int remainingDamage = Mathf.Max(0, incomingAttacks[i] - reductions[i]);
                OnDefenseAttackResolved?.Invoke(new DefenseAttackResult(
                    attackSources[i], i, incomingAttacks[i], effectiveBlocks[i], reductions[i], remainingDamage));
            }
            Log($"Defended with {string.Join(", ", names)}: blocked {blockedDamage} damage and fully covered {blockedAttacks} attack(s). Remaining: {pendingDamage}");

            bool reactionsCompleted = ProcessReactions(CombatReactionPhase.AttackBlocked);
            if (reactionsCompleted)
                ProcessReactions(CombatReactionPhase.HitResolved);
            if (TryResolveTerminalOutcome()) return true;

            if (pendingDamage <= 0)
            {
                ResolvePerfectParry();
                CompleteEnemyResponse();
                BeginPlayerTurn();
                Log("Defended! Your turn.");
            }
            return true;
        }
        finally
        {
            _activeAction = null;
            _isResolvingAction = false;
            _reactions.Clear();
        }
    }

    public void TakeRemainingDamage()
    {
        if (GameplayInputGate.IsBlocked || _isResolvingAction || currentState != GameState.EnemyAttacking ||
            _enemies.Count == 0 || _encounterResolved)
            return;

        _isResolvingAction = true;
        _activeAction = CreateAction(CombatActionOrigin.EnemyRetaliation);
        _nextReactiveHitIndex = 1;
        var remaining = new List<(int amount, bool split)>();
        // Match the saturating pending-damage total; unchecked int addition could erase a positive aggregate.
        long ordinaryTotal = 0;
        for (int i = 0; i < _pendingAttacks.Count; i++)
        {
            var source = _pendingAttackSources[i];
            if (source != null && source.HasDoubleStrike) remaining.Add((_pendingAttacks[i], true));
            else ordinaryTotal = Math.Min(int.MaxValue, ordinaryTotal + _pendingAttacks[i]);
        }
        if (ordinaryTotal > 0) remaining.Insert(0, ((int)ordinaryTotal, false));
        try
        {
            int lost = 0;
            for (int i = 0; i < remaining.Count; i++)
            {
                var request = new DamageRequest(_activeAction, i, CombatDamageOrigin.EnemyAggregate,
                    null, player, remaining[i].amount, allowShield: true);
                var result = ResolveDamage(request);
                lost += result.ActualHpLost;
                _parryDamageLeaked |= result.ModifiedDamage > 0;
                if (!ProcessReactions(CombatReactionPhase.HitResolved) || _reactionFaulted) break;
                if (player.IsDefeated) break;
            }
            if (!_reactionFaulted && !player.IsDefeated) ResolvePerfectParry();
            Log($"Took {lost} damage. (HP: {player.currentHealth}/{player.maxHealth})");
            TryResolveTerminalOutcome();
        }
        finally
        {
            _activeAction = null;
            _isResolvingAction = false;
            _reactions.Clear();
        }
        _pendingAttacks.Clear();
        _pendingAttackSources.Clear();
        SetPendingDamage(0);

        if (!_encounterResolved && !_reactionFaulted)
        {
            CompleteEnemyResponse();
            BeginPlayerTurn();
            Log("Your turn.");
        }
    }

    public bool CanPassPlayerTurn
    {
        get
        {
            var cards = CardManager.Instance;
            return !GameplayInputGate.IsBlocked && !_reactionFaulted && !_isResolvingAction &&
                currentState == GameState.PlayerTurn && !_encounterResolved && !player.IsDefeated &&
                _enemies.Count > 0 && cards != null && !cards.IsHandEnhancementTargeting && !cards.IsEnemyTargeting;
        }
    }

    public bool PassPlayerTurn()
    {
        if (!CanPassPlayerTurn) return false;
        _isResolvingAction = true;
        _reactions.Clear();
        try
        {
            CardManager.Instance.ClearSelection();
            CompletePlayerTurnForEnemies();
            if (TryResolveTerminalOutcome()) return true;

            _completedPlayerTurnCount++;
            // Passing consumes the current action (including a bonus action) and cannot mint new action grants.
            _queuedPlayerTurnGrants.Clear();
            BeginEnemyAttack();
            return true;
        }
        finally
        {
            _isResolvingAction = false;
            _reactions.Clear();
        }
    }

    public void Recover()
    {
        if (_reactionFaulted || GameplayInputGate.IsBlocked || _isResolvingAction || currentState != GameState.PlayerTurn ||
            _enemies.Count == 0 || _encounterResolved || HandCount() != 0)
            return;

        int damage = TotalEnemyAttack;
        // Recover is one aggregate combat-damage hit; apply reductions before its single Shield check.
        ResolvePlayerDamageAction(
            CombatActionOrigin.Recovery,
            CombatDamageOrigin.Recovery,
            damage,
            "Recovery attack dealt",
            allowShield: true);
        if (_encounterResolved || _reactionFaulted) return;

        int drawn = CardManager.Instance.DrawToHand(1);
        Log(drawn > 0 ? "Recovered and drew 1 card." : "Recovered, but no card could be drawn.");
    }

    void BeginEnemyAttack()
    {
        _pendingAttacks.Clear();
        _pendingAttackSources.Clear();
        int heldCardCount = CardManager.Instance != null ? CardManager.Instance.HandCount : 0;
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy == null || enemy.IsDefeated) continue;
            var hits = enemy.PrepareResponseHits(heldCardCount);
            for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
            {
                if (hits[hitIndex] <= 0) continue;
                _pendingAttacks.Add(hits[hitIndex]);
                _pendingAttackSources.Add(enemy);
            }
        }
        _attackCountAtStart = _pendingAttacks.Count;
        SnapshotPerfectParry();
        long total = 0;
        for (int i = 0; i < _pendingAttacks.Count; i++)
            total = Math.Min(int.MaxValue, total + _pendingAttacks[i]);
        SetPendingDamage((int)total);
        if (pendingDamage <= 0)
        {
            CompleteEnemyResponse();
            BeginPlayerTurn();
            Log("Enemy attack is 0! Your turn.");
            return;
        }

        SetState(GameState.EnemyAttacking);
        string breakdown = string.Join(" + ", _enemies.ConvertAll(enemy =>
            $"{enemy.DisplayName} {enemy.PreparedResponseBudget}{(enemy.HasDoubleStrike ? " (2 hits at 60%)" : enemy.CurrentResponseIsCharged ? " (charged)" : "")}"));
        Log($"Surviving enemies attack for {pendingDamage} ({breakdown})! Discard to defend or take the remaining damage.");
    }

    void SnapshotPerfectParry()
    {
        _parryIncomingDamage = 0;
        _parryEffectiveBlock = 0;
        _parryResolved = false;
        _parryDamageLeaked = false;
        _parryDrawCandidates.Clear();
        var cards = CardManager.Instance;
        if (cards != null)
            foreach (var card in cards.deck) _parryDrawCandidates.Add(card.Id);
        var action = new CombatActionContext(0, CombatActionOrigin.EnemyRetaliation);
        long ordinary = 0;
        for (int i = 0; i < _pendingAttacks.Count; i++)
        {
            if (_pendingAttackSources[i] != null && _pendingAttackSources[i].HasDoubleStrike)
                _parryIncomingDamage += PreviewParryDamage(action, _pendingAttacks[i]);
            else ordinary = Math.Min(int.MaxValue, ordinary + _pendingAttacks[i]);
        }
        if (ordinary > 0) _parryIncomingDamage += PreviewParryDamage(action, (int)ordinary);
    }

    int PreviewParryDamage(CombatActionContext action, int damage)
    {
        if (HasInfiniteHealthForDev) return 0;
        var request = new DamageRequest(action, 0, CombatDamageOrigin.EnemyAggregate, null, player, damage);
        return GameplayEffectResolver.ModifyIncomingCombatDamage(
            new IncomingDamageEffectContext(request, this, CardManager.Instance, damage));
    }

    void ResolvePerfectParry()
    {
        if (_parryResolved) return;
        _parryResolved = true;
        if (_parryDamageLeaked || _parryIncomingDamage <= 0 || _parryEffectiveBlock != _parryIncomingDamage) return;
        int drawn = CardManager.Instance != null
            ? CardManager.Instance.DrawExistingToHand(1, _parryDrawCandidates) : 0;
        Log(drawn > 0 ? "Perfect Block! Drew 1 card." : "Perfect Block! No eligible card could be drawn.");
    }

    void CompleteEnemyResponse()
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy == null) continue;
            if (enemy.TryBeginMasterThiefSteal()) ResolveMasterThiefSteal(enemy);
            enemy.CompleteEnemyResponse(_enemies);
        }
        OnEnemyHpChanged?.Invoke();
    }

    void ResolveMasterThiefSteal(EnemyRuntime thief)
    {
        var cards = CardManager.Instance;
        int stolen = cards != null ? Math.Min(8, Math.Max(0, cards.credits)) : 0;
        if (stolen > 0) cards.SpendCredits(stolen);
        thief.SetStolenCredits(stolen);
        Log($"{thief.DisplayName} stole {stolen} Credits and will flee after 4 player actions unless defeated!");
    }

    void CompletePlayerTurnForEnemies()
    {
        for (int i = 0; i < _enemies.Count; i++)
            _enemies[i].NotifyPlayerTurnCompleted();

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (!enemy.ShouldFlee) continue;
            Log($"{enemy.DisplayName} fled after {enemy.PlayerTurnsCompleted} player turns. No gold awarded.");
            RemoveEnemy(enemy);
        }
    }

    void HandleEnemyDefeated(EnemyRuntime enemy)
    {
        if (enemy == null || !_enemies.Contains(enemy) || !_processedEnemyDeaths.Add(enemy)) return;
        _earnedCreditsReward += Mathf.Max(0, enemy.CreditsReward);
        if (_isEliteEncounter) _eliteKillCount++;
        int returnedCredits = enemy.ClaimStolenCredits();
        if (returnedCredits > 0) CardManager.Instance.AddCredits(returnedCredits);
        RemoveEnemy(enemy);
        int drawn = CardManager.Instance.DrawToHand(1);
        Log($"{enemy.DisplayName} defeated! {enemy.CreditsReward} Credits secured.{(returnedCredits > 0 ? $" Returned {returnedCredits} stolen Credits." : string.Empty)}{(drawn > 0 ? " Drew 1 card." : string.Empty)}");
    }

    void AwardDevouringKillBonus(IReadOnlyList<CardInstance> committedCards)
    {
        if (committedCards == null || CardManager.Instance == null) return;
        var cards = CardManager.Instance;
        for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
        {
            var gear = cards.ownedGear[gearIndex];
            if (gear == null || gear.specialRule != GearSpecialRule.DwarfEpic) continue;
            var instance = cards.GetGearInstanceAt(gearIndex);
            var source = instance.PendingDwarfSourceCard;
            if (source == null || cards.FindOwnedCard(source.Id) != source ||
                !committedCards.Any(card => card != null && card.Rank > source.Rank)) continue;
            source.GainPermanentAttackBonus(1);
            cards.NotifyCardInstanceChanged(source);
            instance.PendingDwarfSourceCard = null;
            Log($"{gear.displayName}: prepared {source.DisplayName} permanently gained +1 attack.");
        }
        for (int i = 0; i < committedCards.Count; i++)
        {
            var card = committedCards[i];
            if (!GameplayEffectResolver.HasEnhancementEffect(card, GameplayEffectKind.PermanentKillAttack)) continue;
            card.GainPermanentAttackBonus(1);
            CardManager.Instance.NotifyCardInstanceChanged(card);
            Log($"{card.Enhancement.displayName}: permanently gained +1 attack.");
        }
    }

    void ResolveExplosiveDamage(IReadOnlyList<CardInstance> committedCards, EnemyRuntime primaryTarget, int damage, bool critical)
    {
        if (damage <= 0) return;
        var targets = new List<EnemyRuntime>(_enemies);
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (target == null || ReferenceEquals(target, primaryTarget) || target.IsDefeated || !_enemies.Contains(target))
                continue;
            var request = new DamageRequest(_activeAction, _nextReactiveHitIndex++,
                CombatDamageOrigin.Card, player, target, damage, isCritical: critical);
            var result = ResolveDamage(request);
            if (target.IsDefeated)
            {
                AwardDevouringKillBonus(committedCards);
                HandleEnemyDefeated(target);
            }
            else
                EnqueueIncomingHitResolvedAbilities(_activeAction, target, result);
            if (!ProcessReactions(CombatReactionPhase.HitResolved) || _reactionFaulted || player.IsDefeated)
                return;
        }
    }

    void RemoveEnemy(EnemyRuntime enemy)
    {
        if (enemy == null || !_enemies.Remove(enemy)) return;
        // A Captain can die during defense (for example to Retaliation). Remove only its
        // fixed aura contribution from still-pending attacks; do not rebuild other snapshots.
        if (enemy.HasCaptaincy)
        {
            int removedBonus = 0;
            for (int i = 0; i < _pendingAttackSources.Count; i++)
            {
                var source = _pendingAttackSources[i];
                if (!source.IsSupportedBy(enemy)) continue;
                int bonus = Mathf.Min(_pendingAttacks[i], source.PreparedCaptainAttackBonus);
                _pendingAttacks[i] -= bonus;
                removedBonus += bonus;
            }
            foreach (var ally in _enemies)
                if (ally.IsSupportedBy(enemy)) ally.AssignCaptain(null);
            if (removedBonus > 0) SetPendingDamage(pendingDamage - removedBonus);
        }
        enemy.OnHpChanged -= HandleEnemyHpChanged;
        enemy.OnAttackChanged -= HandleEnemyHpChanged;
        if (ReferenceEquals(currentEnemy, enemy))
            currentEnemy = _enemies.Count > 0 ? _enemies[0] : null;
        OnEnemiesChanged?.Invoke();
        OnEnemyChanged?.Invoke();
    }

    DamageResult ResolveDamage(DamageRequest request)
    {
        var result = _resolver.Resolve(request, this);
        if (ReferenceEquals(request.Target, player))
        {
            _encounterActualHpLost += result.ActualHpLost;
            NotifyPlayerHealthChanged();
        }
        OnDamageResolved?.Invoke(result);
        return result;
    }

    void ResolvePlayerDamageAction(
        CombatActionOrigin actionOrigin,
        CombatDamageOrigin damageOrigin,
        int amount,
        string logPrefix,
        bool allowShield)
    {
        _isResolvingAction = true;
        _reactions.Clear();
        _activeAction = CreateAction(actionOrigin);
        _nextReactiveHitIndex = 1;
        try
        {
            var request = new DamageRequest(
                _activeAction,
                0,
                damageOrigin,
                null,
                player,
                amount,
                allowShield);
            var result = ResolveDamage(request);
            ProcessReactions(CombatReactionPhase.HitResolved);
            Log($"{logPrefix} {result.ActualHpLost} damage. (HP: {player.currentHealth}/{player.maxHealth})");
            TryResolveTerminalOutcome();
        }
        finally
        {
            _activeAction = null;
            _isResolvingAction = false;
            _reactions.Clear();
        }
    }

    public int GrantPlayerShield(int charges)
    {
        return player.GainShield(charges);
    }

    public int GrantEnemyShield(EnemyRuntime enemy, int charges)
    {
        return enemy != null && _enemies.Contains(enemy) ? enemy.GainShield(charges) : 0;
    }

    public bool QueueReactiveDamage(
        ICombatDamageTarget source,
        ICombatDamageTarget target,
        int amount,
        int sourceOrder,
        int handlerOrder,
        int priority = 0,
        CombatReactionSourceCategory sourceCategory = CombatReactionSourceCategory.Core,
        Action<DamageResult> onResolved = null)
    {
        if (!_isResolvingAction || _activeAction == null || target == null ||
            (_processingReactionPhase != CombatReactionPhase.HitResolved &&
             _processingReactionPhase != CombatReactionPhase.AttackBlocked))
            return false;
        var phase = _processingReactionPhase.Value;
        int hitIndex = _nextReactiveHitIndex++;
        return _reactions.Enqueue(
            _activeAction.ActionId,
            hitIndex,
            phase,
            sourceCategory,
            sourceOrder,
            handlerOrder,
            () =>
            {
                var request = new DamageRequest(
                    _activeAction,
                    hitIndex,
                    CombatDamageOrigin.Reactive,
                    source,
                    target,
                    amount);
                var result = ResolveDamage(request);
                onResolved?.Invoke(result);
                if (target is EnemyRuntime enemy && !enemy.IsDefeated)
                    EnqueueIncomingHitResolvedAbilities(_activeAction, enemy, result);
                if (target is EnemyRuntime defeatedEnemy && defeatedEnemy.IsDefeated)
                    HandleEnemyDefeated(defeatedEnemy);
            },
            priority);
    }

    void EnqueueEncounterStartedAbilities(CombatActionContext action, EnemyRuntime enemy, int enemyOrder)
    {
        var abilities = enemy?.type?.abilities;
        if (abilities == null) return;
        for (int abilityIndex = 0; abilityIndex < abilities.Length; abilityIndex++)
        {
            var ability = abilities[abilityIndex];
            if (ability == null) continue;
            int capturedIndex = abilityIndex;
            _reactions.Enqueue(
                action.ActionId,
                -1,
                CombatReactionPhase.EncounterStarted,
                CombatReactionSourceCategory.EnemyAbility,
                enemyOrder,
                capturedIndex,
                () => ability.OnEncounterStarted(enemy, this));
        }
    }

    void EnqueueCardCommittedEffects(
        CombatActionContext action,
        CardInstance card,
        EnemyRuntime target,
        CardManager cards,
        int cardOrder)
    {
        GameplayEffectResolver.EnqueueCardCommitted(
            new CardCommittedEffectContext(action, card, target, this, cards, cardOrder,
                _handsEpicAction ? 2 : 1), _reactions);
    }

    bool BeginGrantedPlayerTurn()
    {
        if (_queuedPlayerTurnGrants.Count == 0) return false;
        var grant = _queuedPlayerTurnGrants.Dequeue();
        Log($"{grant.SourceName} grants an extra player turn.");
        BeginPlayerTurn(isBonusAction: true);
        return true;
    }

    void BeginPlayerTurn(bool isBonusAction = false)
    {
        if (_reactionFaulted) return;
        _isBonusPlayerAction = isBonusAction;
        SetState(GameState.PlayerTurn);
        var cards = CardManager.Instance;
        if (cards == null || _encounterResolved) return;
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy == null || enemy.IsDefeated || !enemy.HasSuitCall) continue;
            var random = _duelistSuitRandom ??= new DeterministicRandom(0x44554C49);
            var suit = (CardData.Suit)random.NextInt(0, Enum.GetValues(typeof(CardData.Suit)).Length);
            enemy.SetAnnouncedSuit(suit);
            Log($"{enemy.DisplayName} calls {suit}.");
        }
        OnEnemyHpChanged?.Invoke();
        // Separate bounded phase so a zero-attack turn cannot disturb the action's hit queue.
        var turnQueue = new CombatReactionQueue();
        var context = new PlayerTurnStartEffectContext(CreateAction(CombatActionOrigin.Legacy),
            player, this, cards, ++_playerTurnNumber);
        GameplayEffectResolver.EnqueuePlayerTurnStart(context, turnQueue);
        if (!turnQueue.ProcessPhase(CombatReactionPhase.PlayerTurnStarted,
            () => player.IsDefeated || _encounterResolved))
        {
            HandleReactionFault(CombatReactionPhase.PlayerTurnStarted);
            return;
        }
    }

    internal void LogEffect(string message) => Log(message);

    void EnqueueIncomingHitResolvedAbilities(
        CombatActionContext action,
        EnemyRuntime target,
        DamageResult result)
    {
        var abilities = target?.type?.abilities;
        if (abilities == null) return;
        int enemyOrder = _enemies.IndexOf(target);
        for (int abilityIndex = 0; abilityIndex < abilities.Length; abilityIndex++)
        {
            var ability = abilities[abilityIndex];
            if (ability == null) continue;
            int capturedIndex = abilityIndex;
            _reactions.Enqueue(
                action.ActionId,
                result.Request.HitIndex,
                CombatReactionPhase.HitResolved,
                CombatReactionSourceCategory.EnemyAbility,
                enemyOrder,
                capturedIndex,
                () =>
                {
                    if (target.IsDefeated || !_enemies.Contains(target)) return;
                    ability.OnIncomingHitResolved(target, this, result);
                });
        }
    }

    void EnqueuePlayerCardResolvedAbilities(CombatActionContext action, EnemyRuntime target)
    {
        var abilities = target?.type?.abilities;
        if (abilities == null) return;
        int enemyOrder = _enemies.IndexOf(target);
        for (int abilityIndex = 0; abilityIndex < abilities.Length; abilityIndex++)
        {
            var ability = abilities[abilityIndex];
            if (ability == null) continue;
            int capturedIndex = abilityIndex;
            _reactions.Enqueue(
                action.ActionId,
                -1,
                CombatReactionPhase.CardResolved,
                CombatReactionSourceCategory.EnemyAbility,
                enemyOrder,
                capturedIndex,
                () =>
                {
                    if (target.IsDefeated || !_enemies.Contains(target)) return;
                    ability.OnPlayerCardResolved(target, this);
                });
        }
    }

    bool ProcessReactions(CombatReactionPhase phase)
    {
        _processingReactionPhase = phase;
        try
        {
            bool completed = _reactions.ProcessPhase(
                phase,
                () => player.IsDefeated || _encounterResolved);
            if (!completed)
            {
                Log($"Reaction processing halted during {phase}; remaining action effects were skipped.");
                HandleReactionFault(phase);
            }
            return completed;
        }
        finally
        {
            _processingReactionPhase = null;
        }
    }

    void HandleReactionFault(CombatReactionPhase phase)
    {
        if (_reactionFaulted || _encounterResolved) return;
        _reactionFaulted = true;
        _queuedPlayerTurnGrants.Clear();
        Log($"Encounter halted after reaction processing failed during {phase}. No victory or rewards will be granted.");
        ResolveEncounter(EncounterResult.Defeat);
    }

    CombatActionContext CreateAction(
        CombatActionOrigin origin,
        PlayerRuntime sourcePlayer = null,
        EnemyRuntime sourceEnemy = null,
        CardInstance card = null,
        EnemyRuntime targetEnemy = null,
        int hitCount = 1)
    {
        return new CombatActionContext(
            _nextActionId++,
            origin,
            sourcePlayer,
            sourceEnemy,
            card,
            targetEnemy,
            hitCount,
            origin == CombatActionOrigin.PlayerCard && _isBonusPlayerAction,
            _overflowRootActive ? _overflowRootActionId : 0,
            _executingOverflowAutoPlay);
    }

    public bool ForceDefeatForDevelopment()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_encounterResolved || _reactionFaulted || _isResolvingAction || AttackCardPresentationUI.Instance != null && AttackCardPresentationUI.Instance.IsBusy) return false;
        ResolveEncounter(EncounterResult.Defeat);
        return true;
#else
        return false;
#endif
    }

    // The single terminal check shared by every combat command tail. Defeat wins over Victory;
    // no surviving enemy ends the encounter from any live state, discarding pending attacks.
    bool TryResolveTerminalOutcome()
    {
        if (_encounterResolved || _reactionFaulted) return true;
        if (player.IsDefeated)
        {
            ResolveEncounter(EncounterResult.Defeat);
            return true;
        }
        if (_enemies.Count > 0) return false;
        if (currentState == GameState.EnemyAttacking) ResolvePerfectParry();
        _queuedPlayerTurnGrants.Clear();
        ResolveEncounter(EncounterResult.Victory);
        return true;
    }

    void ResolveEncounter(EncounterResult result)
    {
        if (_encounterResolved || _reactionFaulted && result != EncounterResult.Defeat) return;

        _encounterResolved = true;
        _pendingDiamondDrawChoices = 0;
        CardManager.Instance?.CancelCardInteractions();
        UnsubscribeFromEnemies();
        _pendingAttacks.Clear();
        _pendingAttackSources.Clear();
        _attackCountAtStart = 0;
        _parryResolved = true;
        _parryDrawCandidates.Clear();
        _queuedPlayerTurnGrants.Clear();
        SetPendingDamage(0);
        player.ResetEncounterBlock();

        if (result == EncounterResult.Victory)
        {
            var cards = CardManager.Instance;
            int reward = _earnedCreditsReward;
            int bountyBase = reward;
            if (cards != null)
                foreach (var gear in cards.ownedGear)
                    if (gear != null && gear.specialRule == GearSpecialRule.BountyLedger)
                    {
                        int numerator = _isHeadlinerEncounter ? 125 : _isEliteOrHeadlinerEncounter ? 150 : 100;
                        bountyBase = (int)Math.Min(int.MaxValue, ((long)bountyBase * numerator + 50) / 100);
                        break;
                    }
            reward = bountyBase;
            var (bonusCredits, victoryHeal) = GameplayEffectResolver.EncounterWon(cards);
            reward = (int)Math.Min(int.MaxValue, (long)reward + bonusCredits);

            cards.AddCredits(reward);
            if (victoryHeal > 0)
            {
                int healed = HealPlayer(victoryHeal);
                if (healed > 0) Log($"Gear restored {healed} HP after victory.");
            }
            SetState(GameState.GameWon);
            Log($"Encounter cleared! +{reward}g");
        }
        else
        {
            SetState(GameState.GameOver);
            Log("Player defeated! Game Over!");
        }

        OnEncounterResult?.Invoke(result);
    }

    public int HealPlayer(int amount)
    {
        int healed = player.Heal(amount);
        if (healed > 0) NotifyPlayerHealthChanged();
        return healed;
    }

    public int IncreasePlayerMaxHealth(int amount, bool healIncrease = true)
    {
        int gained = player.IncreaseMaxHealth(amount, healIncrease);
        if (gained > 0) NotifyPlayerHealthChanged();
        return gained;
    }

    public int SetPlayerMaxHealthAndClamp(int value)
    {
        int changed = player.SetMaxHealthAndClamp(value);
        NotifyPlayerHealthChanged();
        return changed;
    }

    public bool HasInfiniteHealthForDev
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return DevModeRuntime.InfiniteHealth;
#else
            return false;
#endif
        }
    }

    public bool CanTakeRunDamage(int amount)
    {
        if (HasInfiniteHealthForDev) return true;
        return amount <= 0 || player.currentHealth > amount;
    }

    public int TakeRunDamage(int amount)
    {
        if (!CanTakeRunDamage(amount)) return 0;
        int dealt = player.TakeDamage(amount);
        if (dealt > 0) NotifyPlayerHealthChanged();
        return dealt;
    }

    void TryReturnCriticalAce(IReadOnlyList<CardInstance> committedCards, CardManager cards)
    {
        if (committedCards == null || cards == null) return;
        for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
        {
            var gear = cards.ownedGear[gearIndex];
            if (gear == null || gear.specialRule is not (GearSpecialRule.AceRare or GearSpecialRule.AceEpic))
                continue;
            var state = cards.GetGearInstanceAt(gearIndex).State;
            if (state.GetEncounterCounter(-114) != 0) continue;
            var ace = committedCards.FirstOrDefault(card => card != null && card.Rank == CardData.Rank.Ace);
            if (ace == null || !cards.TryReturnDiscardCardToHand(ace)) continue;
            state.SetEncounterCounter(-114, 1);
            Log($"{gear.displayName}: returned the physical Ace to hand.");
            return;
        }
    }

    void PreparePhase7ActionBonuses(IReadOnlyList<CardInstance> playedCards)
    {
        var cards = CardManager.Instance;
        if (cards == null || playedCards == null) return;
        for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
        {
            var gear = cards.ownedGear[gearIndex];
            if (gear == null) continue;
            var instance = cards.GetGearInstanceAt(gearIndex);
            if (instance == null) continue;
            var state = instance.State;
            if (_isHeadlinerEncounter && gear.specialRule == GearSpecialRule.KingslayersMark &&
                state.GetEncounterCounter(-1382) > 0)
                _activeKingslayerBonus = 1;
            if (gear.specialRule is GearSpecialRule.DwarfRare or GearSpecialRule.DwarfEpic)
            {
                var source = instance.PreparedDwarfSourceCard;
                bool qualifying = source != null && cards.FindOwnedCard(source.Id) == source &&
                    playedCards.Any(card => card != null && card.Rank > source.Rank);
                if (qualifying)
                {
                    _activeDwarfBonus = 1;
                    _activeDwarfSourceCard = source;
                }
            }
            if (gear.specialRule == GearSpecialRule.BalancersScale)
            {
                int previousGroup = state.GetCounter(-1181);
                int playedGroup = playedCards.Count > 0 ? GetBalancerGroup(playedCards[0]) : 0;
                if (playedGroup != 0 && playedGroup == previousGroup)
                {
                    state.SetCounter(-1182, 0);
                    _activeBalancerBonus = 0;
                }
                else _activeBalancerBonus = Math.Max(0, state.GetCounter(-1182));
            }
            if (gear.specialRule == GearSpecialRule.SpadeEpic)
                _activeSpadeBonus = Math.Max(0, state.GetEncounterCounter(-1071));
            if (gear.specialRule == GearSpecialRule.HeartEpic &&
                playedCards.Any(card => card != null && card.MatchesSuit(CardData.Suit.Hearts)))
                _activeHeartBonus = Math.Max(0, state.GetCounter(-1050));
        }
    }

    void ConsumeAndAdvancePhase7ActionState(IReadOnlyList<CardInstance> playedCards, bool autoPlay)
    {
        var cards = CardManager.Instance;
        if (cards == null || playedCards == null) return;
        for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
        {
            var gear = cards.ownedGear[gearIndex];
            if (gear == null) continue;
            var instance = cards.GetGearInstanceAt(gearIndex);
            var state = instance.State;
            if (_activeDwarfSourceCard != null && instance.PreparedDwarfSourceCard == _activeDwarfSourceCard)
            {
                instance.PendingDwarfSourceCard = _activeDwarfSourceCard;
                instance.PreparedDwarfSourceCard = null;
            }
            if (_activeKingslayerBonus > 0 && gear.specialRule == GearSpecialRule.KingslayersMark)
                state.SetEncounterCounter(-1382, Math.Max(0, state.GetEncounterCounter(-1382) - 1));
            if (_activeBalancerBonus > 0 && gear.specialRule == GearSpecialRule.BalancersScale)
                state.SetCounter(-1182, 0);
            if (_activeSpadeBonus > 0 && gear.specialRule == GearSpecialRule.SpadeEpic)
                state.SetEncounterCounter(-1071, 0);
            if (_activeHeartBonus > 0 && gear.specialRule == GearSpecialRule.HeartEpic)
                state.SetCounter(-1050, 0);
        }
        if (autoPlay) return;

        for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
        {
            var gear = cards.ownedGear[gearIndex];
            if (gear == null) continue;
            var instance = cards.GetGearInstanceAt(gearIndex);
            var state = instance.State;
            foreach (var card in playedCards)
            {
                if (card == null) continue;
                if (gear.specialRule is GearSpecialRule.DwarfRare or GearSpecialRule.DwarfEpic &&
                    card.Rank < CardData.Rank.Five)
                    instance.PreparedDwarfSourceCard = card;
                if (_isHeadlinerEncounter && gear.specialRule == GearSpecialRule.KingslayersMark)
                {
                    int mask = state.GetEncounterCounter(-1381) | (1 << (int)card.Rank);
                    int uniqueRanks = 0;
                    for (int bits = mask; bits != 0; bits >>= 1) uniqueRanks += bits & 1;
                    if (uniqueRanks >= 3)
                    {
                        state.SetEncounterCounter(-1381, 0);
                        state.SetEncounterCounter(-1382, state.GetEncounterCounter(-1382) + 1);
                    }
                    else state.SetEncounterCounter(-1381, mask);
                }
                if (gear.specialRule == GearSpecialRule.DiamondEpic &&
                    card.MatchesSuit(CardData.Suit.Diamonds))
                {
                    int count = state.GetCounter(-108) + 1;
                    if (count >= 3)
                    {
                        _pendingDiamondDrawChoices++;
                        count -= 3;
                    }
                    state.SetCounter(-108, count);
                }
                if (gear.specialRule == GearSpecialRule.BalancersScale)
                {
                    int group = GetBalancerGroup(card);
                    if (group == 0) continue;
                    int previous = state.GetCounter(-1181);
                    int bonus = state.GetCounter(-1182);
                    if (group == previous) bonus = 0;
                    else if (previous != 0) bonus++;
                    state.SetCounter(-1181, group);
                    state.SetCounter(-1182, bonus);
                }
            }
        }
    }

    static int GetBalancerGroup(CardInstance card) => card == null ? 0 :
        card.Rank < CardData.Rank.Five ? 1 :
        card.Rank is CardData.Rank.Jack or CardData.Rank.Queen or CardData.Rank.King ? 2 : 0;

    void ClearPendingDwarfSources()
    {
        var cards = CardManager.Instance;
        if (cards == null) return;
        for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
        {
            var gear = cards.ownedGear[gearIndex];
            if (gear != null) cards.GetGearInstanceAt(gearIndex).PendingDwarfSourceCard = null;
        }
    }

    public int CalculateCardAttackDamage(CardInstance card, IReadOnlyList<CardInstance> playedCards = null,
        bool includeOpeningAttackBonus = true)
    {
        var cards = CardManager.Instance;
        int overflowHeld = _executingOverflowAutoPlay && cards != null
            ? Math.Max(0, cards.HandCount - 1) : -1;
        int gilded = includeOpeningAttackBonus && IsOpeningPlayerAttack && _playerTurnNumber == 1 &&
            !_executingOverflowAutoPlay && cards != null ? Math.Max(0, cards.credits / 10) : 0;
        if (includeOpeningAttackBonus)
            gilded += _activeKingslayerBonus + _activeDwarfBonus + _activeBalancerBonus + _activeSpadeBonus + _activeHeartBonus;
        if (includeOpeningAttackBonus && IsOpeningPlayerAttack && cards != null)
        {
            for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
            {
                var gear = cards.ownedGear[gearIndex];
                if (gear == null) continue;
                var state = cards.GetGearInstanceAt(gearIndex).State;
                if (gear.specialRule == GearSpecialRule.HuntersLedger)
                    gilded += state.GetCounter(-119);
                else if (gear.specialRule == GearSpecialRule.TrophyRack && _isHeadlinerEncounter)
                    gilded += state.GetCounter(-137);
            }
        }
        return GameplayEffectResolver.CalculateAttack(card, cards, playedCards, overflowHeld, gilded);
    }

    public int CalculateCardDefense(CardInstance card) =>
        GameplayEffectResolver.CalculateBlock(card, CardManager.Instance);

    bool AreValidHandViews(IReadOnlyList<CardView> cards)
    {
        var manager = CardManager.Instance;
        if (manager == null) return false;

        var uniqueCards = new HashSet<CardInstance>();
        for (int i = 0; i < cards.Count; i++)
        {
            var view = cards[i];
            if (view == null || view.data == null || !uniqueCards.Add(view.data))
                return false;

            bool found = false;
            for (int handIndex = 0; handIndex < manager.hand.Count; handIndex++)
            {
                if (!ReferenceEquals(manager.hand[handIndex], view.data)) continue;
                found = true;
                break;
            }
            if (!found) return false;
        }
        return true;
    }

    int HandCount() => CardManager.Instance.HandCount;

    void HandleEnemyHpChanged()
    {
        OnEnemyHpChanged?.Invoke();
    }

    void NotifyPlayerHealthChanged()
    {
        OnPlayerHealthChanged?.Invoke(player.currentHealth, player.maxHealth);
    }

    void SetPendingDamage(int value, bool forceNotify = false)
    {
        int nextValue = Mathf.Max(0, value);
        if (!forceNotify && pendingDamage == nextValue) return;

        pendingDamage = nextValue;
        OnPendingDamageChanged?.Invoke(pendingDamage);
    }

    void UnsubscribeFromEnemies()
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy == null) continue;
            enemy.OnHpChanged -= HandleEnemyHpChanged;
            enemy.OnAttackChanged -= HandleEnemyHpChanged;
        }
    }

    void Log(string msg)
    {
        OnCombatLog?.Invoke(msg);
        Debug.Log($"[Combat] {msg}");
    }

    void SetState(GameState state)
    {
        currentState = state;
        OnStateChanged?.Invoke(state);
    }

    public void Reset()
    {
        UnsubscribeFromEnemies();
        _enemies.Clear();
        _pendingAttacks.Clear();
        _pendingAttackSources.Clear();
        _attackCountAtStart = 0;
        _parryResolved = true;
        _parryDrawCandidates.Clear();
        currentEnemy = null;
        _isEliteOrHeadlinerEncounter = false;
        _isHeadlinerEncounter = false;
        _isEliteEncounter = false;
        _eliteKillCount = 0;
        _earnedCreditsReward = 0;
        _processedEnemyDeaths.Clear();
        _reactions.Clear();
        _completedPlayerTurnCount = 0;
        _queuedPlayerTurnGrants.Clear();
        _nextActionId = 1;
        _nextReactiveHitIndex = 0;
        _playerTurnNumber = 0;
        _playerAttackActionCount = 0;
        _openingPlayerAttackInProgress = false;
        _activeKingslayerBonus = 0;
        _activeDwarfBonus = 0;
        _activeBalancerBonus = 0;
        _activeSpadeBonus = 0;
        _activeHeartBonus = 0;
        _activeDwarfSourceCard = null;
        _handsEpicAction = false;
        _pendingDiamondDrawChoices = 0;
        _encounterActualHpLost = 0;
        _activeAction = null;
        _isResolvingAction = false;
        _encounterResolved = false;
        _reactionFaulted = false;
        player.Configure(_configuredPlayerMaxHealth);
        player.Reset();
        SetPendingDamage(0, true);
        SetState(GameState.Idle);
        NotifyPlayerHealthChanged();
        OnEnemiesChanged?.Invoke();
        OnEnemyChanged?.Invoke();
        OnCombatLog?.Invoke(string.Empty);
    }
}
