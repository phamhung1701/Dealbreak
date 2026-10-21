using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GameplayEffectKind
{
    FlatAttack = 0,
    AttackMultiplier = 1,
    FlatBlock = 2,
    BlockMultiplier = 3,
    Heal = 4,
    Draw = 5,
    BonusCredits = 6,
    ReduceAttackByCardValue = 7,
    RecycleDiscardByCardValue = 8,
    DrawByCardValue = 9,
    FlatActionDamage = 10,
    ActionDamageCap = 11,
    IncomingCombatDamageReduction = 12,
    RevealMapNode = 13,
    GrantRandomCommonEnhancement = 14,
    CriticalChanceOverride = 15,
    CounterDamage = 16,
    HandSizeBonus = 17,
    VampiricActionDamage = 18,
    ExplosiveAreaDamage = 19,
    PermanentKillAttack = 20,
    WildSuit = 21,
    DestroyBlockingCard = 22,
    MimicInHandEffects = 23,
    DoubleStrike = 24,
    RevealAllMapNodes = 25
}

public enum GameplayEffectTrigger
{
    AttackCalculated = 0,
    DefenseCalculated = 1,
    CardCommitted = 2,
    PlayerTurnStart = 3,
    EncounterWon = 4,
    ActionDamageCalculated = 5,
    IncomingDamageCalculated = 6,
    EncounterStart = 7,
    MapReady = 8,
    NodeCompleted = 9,
    CriticalChanceCalculated = 10,
    AttackBlocked = 11,
    AttackCommitted = 12,
    InHandAttackCalculated = 13,
    HandCapacityCalculated = 14
}

public enum GameplayEffectCategory
{
    Reactive,
    NumericModifier,
    RuleModifier
}

public enum GameplayConditionKind
{
    CardSuit = 0,
    EnhancedCard = 1,
    AttackValueGreaterThan = 2,
    MultiCardAction = 3,
    HandsMultiCardAction = 4,
    CardRank = 5
}

public enum GameplayRuleModifierKind
{
    ExtraTurn = 0,
    SameRankMultiCard = 1
}

public enum ExtraTurnRuleTrigger
{
    FirstPlayerTurnCompleted = 0,
    QualifyingPlayedActionCompleted = 1
}

[Serializable]
public struct ExtraTurnRuleDefinition
{
    public ExtraTurnRuleTrigger trigger;
    public bool oncePerEncounter;
    public bool cannotTriggerFromBonusAction;
    public bool requireAllPlayedCardsBelowRank;
    public CardData.Rank exclusiveRank;
}

[Serializable]
public struct SameRankMultiCardRuleDefinition
{
    public bool requireMatchingRank;
    [Min(2)] public int maximumCards;
}

[Serializable]
public struct GameplayEffectCondition
{
    public GameplayConditionKind kind;
    public CardData.Suit suit;
    public CardData.Rank rank;
    public bool expected;
    public int value;

    public bool Matches(CardInstance card, int attackValue = 0, int selectedCardCount = 1,
        bool handsMultiCardAction = false)
    {
        return kind switch
        {
            GameplayConditionKind.CardSuit => card != null && card.MatchesSuit(suit),
            GameplayConditionKind.CardRank => card != null && card.Rank == rank,
            GameplayConditionKind.EnhancedCard => (card?.Enhancement != null) == expected,
            GameplayConditionKind.AttackValueGreaterThan => attackValue > value,
            GameplayConditionKind.MultiCardAction => (selectedCardCount > 1) == expected,
            GameplayConditionKind.HandsMultiCardAction => handsMultiCardAction == expected,
            _ => false
        };
    }
}

// Rule modifiers deliberately do not pass through numeric/reactive Apply paths.
// Future typed subclasses own selection/turn/map permission behavior.
[Serializable]
public class GameplayRuleModifierData
{
    public string ruleId;
    public GameplayRuleModifierKind kind;
    public ExtraTurnRuleDefinition extraTurn;
    public SameRankMultiCardRuleDefinition sameRankMultiCard;
}

[Serializable]
public struct GameplayEffectDefinition
{
    public GameplayEffectKind kind;
    public GameplayEffectTrigger trigger;
    [Min(0)] public int amount;
    public GameplayEffectCondition[] conditions;

    public GameplayEffectCategory Category => kind switch
    {
        GameplayEffectKind.FlatAttack or GameplayEffectKind.AttackMultiplier or
            GameplayEffectKind.FlatBlock or GameplayEffectKind.BlockMultiplier or
            GameplayEffectKind.FlatActionDamage or GameplayEffectKind.ActionDamageCap or
            GameplayEffectKind.IncomingCombatDamageReduction or
            GameplayEffectKind.CriticalChanceOverride or
            GameplayEffectKind.HandSizeBonus or GameplayEffectKind.VampiricActionDamage => GameplayEffectCategory.NumericModifier,
        _ => GameplayEffectCategory.Reactive
    };

    public bool Matches(CardInstance card, int attackValue = 0, int selectedCardCount = 1,
        bool handsMultiCardAction = false)
    {
        if (conditions == null) return true;
        for (int i = 0; i < conditions.Length; i++)
            if (!conditions[i].Matches(card, attackValue, selectedCardCount, handsMultiCardAction)) return false;
        return true;
    }
}

// This state belongs to one run-owned source, never to an authoring asset.
public sealed class GameplayEffectState
{
    readonly Dictionary<int, int> _counters = new();
    readonly Dictionary<int, int> _encounterCounters = new();

    public int GetCounter(int effectIndex) => _counters.TryGetValue(effectIndex, out int value) ? value : 0;
    public void SetCounter(int effectIndex, int value) => _counters[effectIndex] = value;
    public int GetEncounterCounter(int ruleIndex) => _encounterCounters.TryGetValue(ruleIndex, out int value) ? value : 0;
    public void SetEncounterCounter(int ruleIndex, int value) => _encounterCounters[ruleIndex] = value;
    public EffectStateSaveData CaptureSave() => new()
    {
        persistent = _counters.OrderBy(x => x.Key).Select(x => new CounterSaveData { key = x.Key, value = x.Value }).ToList(),
        encounter = _encounterCounters.OrderBy(x => x.Key).Select(x => new CounterSaveData { key = x.Key, value = x.Value }).ToList()
    };
    public void RestoreSave(EffectStateSaveData data)
    {
        Clear();
        foreach (var counter in data.persistent) _counters.Add(counter.key, counter.value);
        foreach (var counter in data.encounter) _encounterCounters.Add(counter.key, counter.value);
    }

    public void ClearEncounter() => _encounterCounters.Clear();
    public void Clear() { _counters.Clear(); _encounterCounters.Clear(); }
}

public sealed class GearRuntimeInstance
{
    static long s_nextId;
    public static long IdentityWatermark => s_nextId;
    public static void RestoreIdentityWatermark(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        System.Threading.Interlocked.Exchange(ref s_nextId, value);
    }
    public long Id { get; private set; } = System.Threading.Interlocked.Increment(ref s_nextId);
    public GearData Definition { get; }
    public GameplayEffectState State { get; } = new();
    public CardInstance PreparedDwarfSourceCard { get; set; }
    public CardInstance PendingDwarfSourceCard { get; set; }

    public GearRuntimeInstance(GearData definition) =>
        Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));

    public static GearRuntimeInstance Restore(GearData definition, long id, EffectStateSaveData effects)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var instance = new GearRuntimeInstance(definition) { Id = id };
        s_nextId = Math.Max(s_nextId, id);
        instance.State.RestoreSave(effects);
        return instance;
    }

    public void ResetEncounterState()
    {
        State.ClearEncounter();
        PreparedDwarfSourceCard = null;
        PendingDwarfSourceCard = null;
    }
}

public readonly struct GameplayEffectSource
{
    public GearRuntimeInstance Gear { get; }
    public CardInstance Card { get; }
    public GameplayEffectState State => Gear != null ? Gear.State : Card?.EffectState;
    public string DisplayName => Gear != null ? Gear.Definition.displayName : Card?.Enhancement?.displayName;
    public CombatReactionSourceCategory Category => Gear != null
        ? CombatReactionSourceCategory.Gear : CombatReactionSourceCategory.Enhancement;

    public GameplayEffectSource(GearRuntimeInstance gear) { Gear = gear; Card = null; }
    public GameplayEffectSource(CardInstance card) { Gear = null; Card = card; }
}

public readonly struct CompletedPlayerActionContext
{
    public CombatActionContext Action { get; }
    public PlayerRuntime Player { get; }
    public CombatManager Combat { get; }
    public CardManager Cards { get; }
    public IReadOnlyList<CardInstance> CommittedCards { get; }
    public int CompletedPlayerTurn { get; }

    public CompletedPlayerActionContext(CombatActionContext action, PlayerRuntime player,
        CombatManager combat, CardManager cards, IReadOnlyList<CardInstance> committedCards,
        int completedPlayerTurn)
    {
        Action = action;
        Player = player;
        Combat = combat;
        Cards = cards;
        CommittedCards = committedCards;
        CompletedPlayerTurn = completedPlayerTurn;
    }
}

public readonly struct PlayerTurnGrant
{
    public string SourceName { get; }
    public long ActionId { get; }
    public int SourceOrder { get; }
    public int RuleOrder { get; }

    public PlayerTurnGrant(string sourceName, long actionId, int sourceOrder, int ruleOrder)
    {
        SourceName = sourceName;
        ActionId = actionId;
        SourceOrder = sourceOrder;
        RuleOrder = ruleOrder;
    }
}

public readonly struct CardCommittedEffectContext
{
    public CombatActionContext Action { get; }
    public CardInstance Card { get; }
    public EnemyRuntime Target { get; }
    public CombatManager Combat { get; }
    public CardManager Cards { get; }
    public int CardOrder { get; }
    public int HitIndex => -1 - CardOrder; // Once per committed card, never once per hit.
    public int HitCount => Action.HitCount;
    public int CardEffectActivations { get; }

    public CardCommittedEffectContext(CombatActionContext action, CardInstance card,
        EnemyRuntime target, CombatManager combat, CardManager cards, int cardOrder,
        int cardEffectActivations = 1)
    {
        Action = action;
        Card = card;
        Target = target;
        Combat = combat;
        Cards = cards;
        CardOrder = cardOrder;
        CardEffectActivations = Math.Max(1, cardEffectActivations);
    }
}

public readonly struct AttackCommittedEffectContext
{
    public CombatActionContext Action { get; }
    public IReadOnlyList<CardInstance> CommittedCards { get; }
    public IReadOnlyList<CardInstance> HandSnapshot { get; }
    public EnemyRuntime Target { get; }
    public CombatManager Combat { get; }
    public CardManager Cards { get; }

    public AttackCommittedEffectContext(CombatActionContext action,
        IReadOnlyList<CardInstance> committedCards, IReadOnlyList<CardInstance> handSnapshot,
        EnemyRuntime target, CombatManager combat, CardManager cards)
    {
        Action = action;
        CommittedCards = committedCards;
        HandSnapshot = handSnapshot;
        Target = target;
        Combat = combat;
        Cards = cards;
    }
}

public readonly struct PlayerTurnStartEffectContext
{
    public CombatActionContext Action { get; }
    public PlayerRuntime Player { get; }
    public CombatManager Combat { get; }
    public CardManager Cards { get; }
    public int TurnNumber { get; }

    public PlayerTurnStartEffectContext(CombatActionContext action, PlayerRuntime player,
        CombatManager combat, CardManager cards, int turnNumber)
    {
        Action = action;
        Player = player;
        Combat = combat;
        Cards = cards;
        TurnNumber = turnNumber;
    }
}

public readonly struct AttackBlockedEffectContext
{
    public CombatActionContext Action { get; }
    public CardInstance BlockingCard { get; }
    public EnemyRuntime AttackedEnemy { get; }
    public int BlockedAttackDamage { get; }
    public int BlockOrder { get; }
    public CombatManager Combat { get; }
    public CardManager Cards { get; }

    public AttackBlockedEffectContext(CombatActionContext action, CardInstance blockingCard,
        EnemyRuntime attackedEnemy, int blockedAttackDamage, int blockOrder,
        CombatManager combat, CardManager cards)
    {
        Action = action;
        BlockingCard = blockingCard;
        AttackedEnemy = attackedEnemy;
        BlockedAttackDamage = blockedAttackDamage;
        BlockOrder = blockOrder;
        Combat = combat;
        Cards = cards;
    }
}

public readonly struct EncounterStartEffectContext
{
    public CombatActionContext Action { get; }
    public PlayerRuntime Player { get; }
    public CombatManager Combat { get; }
    public CardManager Cards { get; }
    public IReadOnlyList<EnemyRuntime> Enemies { get; }
    public int BaselineCardsDrawn { get; }

    public EncounterStartEffectContext(CombatActionContext action, PlayerRuntime player,
        CombatManager combat, CardManager cards, IReadOnlyList<EnemyRuntime> enemies,
        int baselineCardsDrawn)
    {
        Action = action;
        Player = player;
        Combat = combat;
        Cards = cards;
        Enemies = enemies;
        BaselineCardsDrawn = baselineCardsDrawn;
    }
}

public readonly struct ActionDamageEffectContext
{
    public CombatActionContext Action { get; }
    public IReadOnlyList<CardInstance> Cards { get; }
    public EnemyRuntime Target { get; }
    public CardManager CardManager { get; }
    public int AggregatedDamage { get; }
    public bool IsHandsMultiCardAction { get; }

    public ActionDamageEffectContext(CombatActionContext action, IReadOnlyList<CardInstance> cards,
        EnemyRuntime target, CardManager cardManager, int aggregatedDamage, bool isHandsMultiCardAction = false)
    {
        Action = action;
        Cards = cards;
        Target = target;
        CardManager = cardManager;
        AggregatedDamage = aggregatedDamage;
        IsHandsMultiCardAction = isHandsMultiCardAction;
    }
}

public readonly struct IncomingDamageEffectContext
{
    public DamageRequest Request { get; }
    public CombatManager Combat { get; }
    public CardManager Cards { get; }
    public int RawDamage { get; }

    public IncomingDamageEffectContext(DamageRequest request, CombatManager combat,
        CardManager cards, int rawDamage)
    {
        Request = request;
        Combat = combat;
        Cards = cards;
        RawDamage = rawDamage;
    }
}

public readonly struct MapReadyEffectContext
{
    public RunManager Run { get; }
    public CardManager Cards { get; }
    public int MapIndex { get; }

    public MapReadyEffectContext(RunManager run, CardManager cards, int mapIndex)
    {
        Run = run;
        Cards = cards;
        MapIndex = mapIndex;
    }
}

public readonly struct NodeCompletedEffectContext
{
    public RunManager Run { get; }
    public CardManager Cards { get; }
    public PathNode Node { get; }
    public int CompletedNodeCount { get; }

    public NodeCompletedEffectContext(RunManager run, CardManager cards,
        PathNode node, int completedNodeCount)
    {
        Run = run;
        Cards = cards;
        Node = node;
        CompletedNodeCount = completedNodeCount;
    }
}

// Legacy fields feed this same foundation only when a definition has no authored effects.
public static class GameplayEffectResolver
{
    public static int EffectCount(GameplayEffectSource source)
    {
        if (source.Gear != null)
        {
            var definition = source.Gear.Definition;
            if (definition.effects != null && definition.effects.Length > 0) return definition.effects.Length;
            int count = 0;
            if (definition.damageMultiplier > 1) count++;
            if (definition.flatDamageBonus != 0) count++;
            if (definition.defenseBonus != 0) count++;
            if (definition.reduceEnemyAttackByCardValue) count++;
            if (definition.recycleDiscardByCardValue) count++;
            if (definition.drawByCardValue) count++;
            if (definition.healAfterVictory > 0) count++;
            if (definition.bonusCredits > 0) count++;
            return count;
        }
        var enhancement = source.Card?.Enhancement;
        if (enhancement == null) return 0;
        if (enhancement.effects != null && enhancement.effects.Length > 0) return enhancement.effects.Length;
        int legacyCount = 0;
        if (enhancement.attackBonus != 0) legacyCount++;
        if (enhancement.defenseBonus != 0) legacyCount++;
        if (enhancement.healOnPlay > 0) legacyCount++;
        if (enhancement.drawOnPlay > 0) legacyCount++;
        return legacyCount;
    }

    public static GameplayEffectDefinition EffectAt(GameplayEffectSource source, int index)
    {
        if (source.Gear != null)
        {
            var a = source.Gear.Definition;
            if (a.effects != null && a.effects.Length > 0) return a.effects[index];
            var effect = new GameplayEffectDefinition { conditions = Array.Empty<GameplayEffectCondition>() };
            if (a.damageMultiplier > 1 && index-- == 0) return With(effect, GameplayEffectKind.AttackMultiplier, GameplayEffectTrigger.AttackCalculated, a.damageMultiplier);
            if (a.flatDamageBonus != 0 && index-- == 0) return With(effect, GameplayEffectKind.FlatAttack, GameplayEffectTrigger.AttackCalculated, a.flatDamageBonus);
            if (a.defenseBonus != 0 && index-- == 0) return With(effect, GameplayEffectKind.FlatBlock, GameplayEffectTrigger.DefenseCalculated, a.defenseBonus);
            if (a.reduceEnemyAttackByCardValue && index-- == 0) return With(effect, GameplayEffectKind.ReduceAttackByCardValue, GameplayEffectTrigger.CardCommitted, 1);
            if (a.recycleDiscardByCardValue && index-- == 0) return With(effect, GameplayEffectKind.RecycleDiscardByCardValue, GameplayEffectTrigger.CardCommitted, 1);
            if (a.drawByCardValue && index-- == 0) return With(effect, GameplayEffectKind.DrawByCardValue, GameplayEffectTrigger.CardCommitted, 1);
            effect.conditions = Array.Empty<GameplayEffectCondition>();
            if (a.healAfterVictory > 0 && index-- == 0) return With(effect, GameplayEffectKind.Heal, GameplayEffectTrigger.EncounterWon, a.healAfterVictory);
            if (a.bonusCredits > 0 && index == 0) return With(effect, GameplayEffectKind.BonusCredits, GameplayEffectTrigger.EncounterWon, a.bonusCredits);
        }
        else if (source.Card?.Enhancement != null)
        {
            var e = source.Card.Enhancement;
            if (e.effects != null && e.effects.Length > 0) return e.effects[index];
            if (e.attackBonus != 0 && index-- == 0) return With(default, GameplayEffectKind.FlatAttack, GameplayEffectTrigger.AttackCalculated, e.attackBonus);
            if (e.defenseBonus != 0 && index-- == 0) return With(default, GameplayEffectKind.FlatBlock, GameplayEffectTrigger.DefenseCalculated, e.defenseBonus);
            if (e.healOnPlay > 0 && index-- == 0) return With(default, GameplayEffectKind.Heal, GameplayEffectTrigger.CardCommitted, e.healOnPlay);
            if (e.drawOnPlay > 0 && index == 0) return With(default, GameplayEffectKind.Draw, GameplayEffectTrigger.CardCommitted, e.drawOnPlay);
        }
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    static GameplayEffectDefinition With(GameplayEffectDefinition effect, GameplayEffectKind kind,
        GameplayEffectTrigger trigger, int amount)
    {
        effect.kind = kind;
        effect.trigger = trigger;
        effect.amount = amount;
        return effect;
    }

    static bool Applies(GameplayEffectSource source, GameplayEffectDefinition effect,
        GameplayEffectTrigger trigger, CardInstance card, int attackValue = 0,
        int selectedCardCount = 1, bool handsMultiCardAction = false)
    {
        if (source.Gear != null &&
            (source.Gear.Definition.effects == null || source.Gear.Definition.effects.Length == 0) &&
            trigger != GameplayEffectTrigger.EncounterWon &&
            !source.Gear.Definition.Matches(card))
            return false;
        return effect.trigger == trigger && effect.Matches(card, attackValue, selectedCardCount,
            handsMultiCardAction) &&
            (source.Card == null || ReferenceEquals(source.Card, card));
    }

    static bool HasAttackThreshold(GameplayEffectDefinition effect)
    {
        if (effect.conditions == null) return false;
        for (int i = 0; i < effect.conditions.Length; i++)
            if (effect.conditions[i].kind == GameplayConditionKind.AttackValueGreaterThan) return true;
        return false;
    }

    public static bool HeldCardEffectsSuppressed(CombatManager combat)
    {
        if (combat == null) return false;
        var enemies = combat.Enemies;
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i] != null && !enemies[i].IsDefeated && enemies[i].HasSilence) return true;
        return false;
    }

    public static int CardLocalFlat(CardInstance card, GameplayEffectKind kind)
    {
        if (card?.Enhancement == null) return 0;
        var source = new GameplayEffectSource(card);
        var trigger = kind == GameplayEffectKind.FlatAttack
            ? GameplayEffectTrigger.AttackCalculated : GameplayEffectTrigger.DefenseCalculated;
        int flat = 0;
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (effect.kind == kind && Applies(source, effect, trigger, card)) flat += effect.amount;
        }
        return flat;
    }

    public static int CalculateAttack(CardInstance card, CardManager cards, IReadOnlyList<CardInstance> playedCards = null,
        int overflowHandCount = -1, int openingAttackBonus = 0)
    {
        if (card == null) return 0;
        int localFlat = 0, localMultiplier = 1, gearFlat = 0, gearMultiplier = 1;
        if (card.Enhancement != null)
            Accumulate(new GameplayEffectSource(card), card, GameplayEffectTrigger.AttackCalculated,
                GameplayEffectKind.FlatAttack, GameplayEffectKind.AttackMultiplier,
                ref localFlat, ref localMultiplier, includeThresholdEffects: false);
        localFlat += SumInHandAttackBonuses(card, cards);
        localFlat += SumMimickedInHandAttackBonuses(card, cards);
        int unplayedClubCount = 0;
        if (cards != null)
            for (int i = 0; i < cards.hand.Count; i++)
            {
                var held = cards.hand[i];
                if (held == null || !held.MatchesSuit(CardData.Suit.Clubs)) continue;
                bool played = false;
                for (int j = 0; playedCards != null && j < playedCards.Count; j++)
                    if (ReferenceEquals(held, playedCards[j])) { played = true; break; }
                if (!played) unplayedClubCount++;
            }
        if (cards != null)
            for (int i = 0; i < cards.ownedGear.Count; i++)
            {
                var gear = cards.ownedGear[i];
                if (gear == null) continue;
                Accumulate(new GameplayEffectSource(cards.GetGearInstanceAt(i)), card,
                    GameplayEffectTrigger.AttackCalculated, GameplayEffectKind.FlatAttack,
                    GameplayEffectKind.AttackMultiplier, ref gearFlat, ref gearMultiplier,
                    includeThresholdEffects: false);
            }

        int qualifyingValue = ClampToInt(((long)card.BaseAttackValue + card.PermanentAttackBonus + localFlat) *
            localMultiplier * gearMultiplier + gearFlat);
        int conditionalFlat = 0, conditionalMultiplier = 1;
        if (card.Enhancement != null)
            Accumulate(new GameplayEffectSource(card), card, GameplayEffectTrigger.AttackCalculated,
                GameplayEffectKind.FlatAttack, GameplayEffectKind.AttackMultiplier,
                ref conditionalFlat, ref conditionalMultiplier, includeThresholdEffects: true,
                attackValue: qualifyingValue);
        if (cards != null)
            for (int i = 0; i < cards.ownedGear.Count; i++)
            {
                var gear = cards.ownedGear[i];
                if (gear == null) continue;
                Accumulate(new GameplayEffectSource(cards.GetGearInstanceAt(i)), card,
                    GameplayEffectTrigger.AttackCalculated, GameplayEffectKind.FlatAttack,
                    GameplayEffectKind.AttackMultiplier, ref conditionalFlat, ref conditionalMultiplier,
                    includeThresholdEffects: true, attackValue: qualifyingValue);
            }
        int resolved = ClampToInt((long)qualifyingValue * conditionalMultiplier + conditionalFlat);
        resolved = GearRules.ApplyFinalAttackRules(cards, card, resolved, unplayedClubCount, overflowHandCount);
        if (openingAttackBonus > 0 && GearRules.GrantsOpeningAttackBonus(cards))
            resolved = ClampToInt((long)resolved + openingAttackBonus);
        return resolved;
    }

    static int SumMimickedInHandAttackBonuses(CardInstance target, CardManager cards)
    {
        if (target == null || cards == null || HeldCardEffectsSuppressed(CombatManager.Instance)) return 0;
        int repetitions = 0;
        int epicExtraRepetitions = 0;
        for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
        {
            var gear = cards.ownedGear[gearIndex];
            if (gear == null) continue;
            var source = new GameplayEffectSource(cards.GetGearInstanceAt(gearIndex));
            for (int effectIndex = 0; effectIndex < EffectCount(source); effectIndex++)
            {
                var effect = EffectAt(source, effectIndex);
                if (effect.kind != GameplayEffectKind.MimicInHandEffects ||
                    effect.trigger != GameplayEffectTrigger.CardCommitted || !effect.Matches(target)) continue;
                repetitions += Mathf.Max(1, effect.amount);
                if (GearRules.IsMimicEpic(gear))
                    epicExtraRepetitions += Mathf.Max(1, effect.amount);
            }
        }
        return repetitions * SumInHandAttackBonuses(target, cards) +
            epicExtraRepetitions * SumInHandAttackBonuses(target, cards, requireMatchingRank: true);
    }

    static int SumInHandAttackBonuses(CardInstance target, CardManager cards, bool requireMatchingRank = false)
    {
        if (target == null || cards == null || HeldCardEffectsSuppressed(CombatManager.Instance)) return 0;
        int total = 0;
        for (int handIndex = 0; handIndex < cards.hand.Count; handIndex++)
        {
            var sourceCard = cards.hand[handIndex];
            if (sourceCard?.Enhancement == null || requireMatchingRank && sourceCard.Rank != target.Rank) continue;
            var source = new GameplayEffectSource(sourceCard);
            for (int effectIndex = 0; effectIndex < EffectCount(source); effectIndex++)
            {
                var effect = EffectAt(source, effectIndex);
                if (effect.kind == GameplayEffectKind.FlatAttack &&
                    effect.trigger == GameplayEffectTrigger.InHandAttackCalculated &&
                    effect.Matches(target)) total += effect.amount;
            }
        }
        return total;
    }

    public static int CalculateBlock(CardInstance card, CardManager cards) =>
        CalculateBlockValue(card, cards);

    static int CalculateBlockValue(CardInstance card, CardManager cards)
    {
        if (card == null) return 0;
        int localFlat = 0, localMultiplier = 1, gearFlat = 0, gearMultiplier = 1;
        if (card.Enhancement != null)
            Accumulate(new GameplayEffectSource(card), card, GameplayEffectTrigger.DefenseCalculated,
                GameplayEffectKind.FlatBlock, GameplayEffectKind.BlockMultiplier,
                ref localFlat, ref localMultiplier, includeThresholdEffects: false);
        if (cards != null)
            for (int i = 0; i < cards.ownedGear.Count; i++)
            {
                var gear = cards.ownedGear[i];
                if (gear == null) continue;
                Accumulate(new GameplayEffectSource(cards.GetGearInstanceAt(i)), card,
                    GameplayEffectTrigger.DefenseCalculated, GameplayEffectKind.FlatBlock,
                    GameplayEffectKind.BlockMultiplier, ref gearFlat, ref gearMultiplier,
                    includeThresholdEffects: false);
            }
        return ClampToInt(((long)card.BaseAttackValue + localFlat) *
            localMultiplier * gearMultiplier + gearFlat);
    }

    static int ClampToInt(long value) => (int)Math.Min(Math.Max(value, 0L), int.MaxValue);

    static void Accumulate(GameplayEffectSource source, CardInstance card,
        GameplayEffectTrigger trigger, GameplayEffectKind flatKind, GameplayEffectKind multiplierKind,
        ref int flat, ref int multiplier, bool includeThresholdEffects, int attackValue = 0)
    {
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (HasAttackThreshold(effect) != includeThresholdEffects ||
                !Applies(source, effect, trigger, card, attackValue)) continue;
            if (effect.kind == flatKind) flat += effect.amount;
            else if (effect.kind == multiplierKind)
                multiplier = Mathf.Max(1, multiplier * Mathf.Max(1, effect.amount));
        }
    }

    public static int ModifyWithSingleGear(GearData gear, CardInstance card, int value, bool defense)
    {
        if (gear == null) return value;
        var source = new GameplayEffectSource(new GearRuntimeInstance(gear));
        int flat = 0, multiplier = 1;
        var trigger = defense ? GameplayEffectTrigger.DefenseCalculated : GameplayEffectTrigger.AttackCalculated;
        Accumulate(source, card, trigger,
            defense ? GameplayEffectKind.FlatBlock : GameplayEffectKind.FlatAttack,
            defense ? GameplayEffectKind.BlockMultiplier : GameplayEffectKind.AttackMultiplier,
            ref flat, ref multiplier, includeThresholdEffects: false);
        int qualifyingValue = ClampToInt((long)value * multiplier + flat);
        if (defense) return qualifyingValue;
        int conditionalFlat = 0, conditionalMultiplier = 1;
        Accumulate(source, card, trigger, GameplayEffectKind.FlatAttack,
            GameplayEffectKind.AttackMultiplier, ref conditionalFlat, ref conditionalMultiplier,
            includeThresholdEffects: true, attackValue: qualifyingValue);
        return ClampToInt((long)qualifyingValue * conditionalMultiplier + conditionalFlat);
    }

    public static int CalculateActionDamage(ActionDamageEffectContext context, out int actionDamageCap)
    {
        long damage = Math.Max(0, context.AggregatedDamage);
        damage += SumActionEffect(context, GameplayEffectKind.FlatActionDamage);
        damage += SumActionEffect(context, GameplayEffectKind.VampiricActionDamage);
        actionDamageCap = SmallestActionCap(context);
        // The cap is a budget over the complete action, not one cap per card or hit.
        // CombatManager applies the budget before critical multiplication and apportions it to hits.
        return ClampToInt(damage);
    }

    // Matching critical overrides are resolved once per action in deterministic source order:
    // Gear (acquisition/effect order), then committed-card Enhancements (commit/effect order).
    // Later matching overrides win; evaluation itself never consumes RNG.
    public static bool TryGetCriticalChanceOverride(IReadOnlyList<CardInstance> committedCards,
        CardManager manager, out float chancePercent)
    {
        chancePercent = 0f;
        if (committedCards == null || committedCards.Count == 0) return false;
        bool found = false;
        if (manager != null)
            for (int i = 0; i < manager.ownedGear.Count; i++)
            {
                var gear = manager.ownedGear[i];
                if (gear != null)
                    ApplyCriticalChanceOverrides(new GameplayEffectSource(manager.GetGearInstanceAt(i)),
                        committedCards, ref chancePercent, ref found);
            }
        for (int i = 0; i < committedCards.Count; i++)
        {
            var card = committedCards[i];
            if (card?.Enhancement != null)
                ApplyCriticalChanceOverrides(new GameplayEffectSource(card), committedCards,
                    ref chancePercent, ref found);
        }
        if (GearRules.GuaranteesCritical(manager, committedCards))
        {
            chancePercent = 100f;
            return true;
        }
        return found;
    }

    static void ApplyCriticalChanceOverrides(GameplayEffectSource source,
        IReadOnlyList<CardInstance> committedCards, ref float chancePercent, ref bool found)
    {
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (effect.kind != GameplayEffectKind.CriticalChanceOverride ||
                effect.trigger != GameplayEffectTrigger.CriticalChanceCalculated) continue;
            bool applies = false;
            if (source.Card != null)
                applies = Applies(source, effect, GameplayEffectTrigger.CriticalChanceCalculated, source.Card);
            else
                for (int cardIndex = 0; cardIndex < committedCards.Count && !applies; cardIndex++)
                    applies = Applies(source, effect, GameplayEffectTrigger.CriticalChanceCalculated,
                        committedCards[cardIndex]);
            if (!applies) continue;
            chancePercent = Mathf.Clamp(effect.amount, 0, 100);
            found = true;
        }
    }

    public static int GetVampiricHealthCost(IReadOnlyList<CardInstance> cards)
    {
        if (cards == null) return 0;
        int cost = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card?.Enhancement == null) continue;
            var source = new GameplayEffectSource(card);
            for (int j = 0; j < EffectCount(source); j++)
            {
                var effect = EffectAt(source, j);
                if (effect.kind == GameplayEffectKind.VampiricActionDamage &&
                    effect.trigger == GameplayEffectTrigger.ActionDamageCalculated && effect.Matches(card))
                    cost += Math.Max(0, effect.amount);
            }
        }
        return cost;
    }

    public static bool HasEnhancementEffect(CardInstance card, GameplayEffectKind kind)
    {
        if (card?.Enhancement == null) return false;
        var source = new GameplayEffectSource(card);
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (effect.kind == kind) return true;
        }
        return false;
    }

    public static bool HasDoubleStrikeEffect(IReadOnlyList<CardInstance> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
            if (HasEnhancementEffect(cards[i], GameplayEffectKind.DoubleStrike)) return true;
        return false;
    }

    public static bool HasExplosiveEffect(IReadOnlyList<CardInstance> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
            if (HasEnhancementEffect(cards[i], GameplayEffectKind.ExplosiveAreaDamage)) return true;
        return false;
    }

    public static bool IsLethalEnchantment(CardInstance card) =>
        HasEnhancementEffect(card, GameplayEffectKind.CriticalChanceOverride);

    static int SumActionEffect(ActionDamageEffectContext context, GameplayEffectKind kind)
    {
        int total = 0;
        var manager = context.CardManager;
        if (manager != null)
            for (int i = 0; i < manager.ownedGear.Count; i++)
            {
                var gear = manager.ownedGear[i];
                if (gear == null) continue;
                total += SumSourceEffects(new GameplayEffectSource(manager.GetGearInstanceAt(i)),
                    GameplayEffectTrigger.ActionDamageCalculated, kind, null,
                    context.Cards?.Count ?? 0, handsMultiCardAction: context.IsHandsMultiCardAction);
            }
        if (context.Cards != null)
            for (int i = 0; i < context.Cards.Count; i++)
            {
                var card = context.Cards[i];
                if (card?.Enhancement == null) continue;
                total += SumSourceEffects(new GameplayEffectSource(card),
                    GameplayEffectTrigger.ActionDamageCalculated, kind, card, context.Cards.Count,
                    context.IsHandsMultiCardAction);
            }
        return total;
    }

    static int SmallestActionCap(ActionDamageEffectContext context)
    {
        int cap = -1;
        var manager = context.CardManager;
        if (manager != null)
            for (int i = 0; i < manager.ownedGear.Count; i++)
            {
                var gear = manager.ownedGear[i];
                if (gear == null) continue;
                cap = MinSourceCap(new GameplayEffectSource(manager.GetGearInstanceAt(i)), cap, null,
                    context.Cards?.Count ?? 0, context.IsHandsMultiCardAction);
            }
        if (context.Cards != null)
            for (int i = 0; i < context.Cards.Count; i++)
            {
                var card = context.Cards[i];
                if (card?.Enhancement == null) continue;
                cap = MinSourceCap(new GameplayEffectSource(card), cap, card, context.Cards.Count,
                    context.IsHandsMultiCardAction);
            }
        return cap;
    }

    static int SumSourceEffects(GameplayEffectSource source, GameplayEffectTrigger trigger,
        GameplayEffectKind kind, CardInstance card, int selectedCardCount = 1,
        bool handsMultiCardAction = false)
    {
        int total = 0;
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (effect.kind == kind && Applies(source, effect, trigger, card,
                selectedCardCount: selectedCardCount, handsMultiCardAction: handsMultiCardAction)) total += effect.amount;
        }
        return total;
    }

    static int MinSourceCap(GameplayEffectSource source, int currentCap, CardInstance card,
        int selectedCardCount, bool handsMultiCardAction)
    {
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (effect.kind != GameplayEffectKind.ActionDamageCap ||
                !Applies(source, effect, GameplayEffectTrigger.ActionDamageCalculated, card,
                    selectedCardCount: selectedCardCount, handsMultiCardAction: handsMultiCardAction)) continue;
            currentCap = currentCap < 0 ? effect.amount : Math.Min(currentCap, effect.amount);
        }
        return currentCap;
    }

    public static int CalculateHandCapacity(int baseCapacity, CardManager cards)
    {
        int capacity = Math.Max(0, baseCapacity);
        if (cards == null) return capacity;
        for (int i = 0; i < cards.ownedGear.Count; i++)
        {
            var gear = cards.ownedGear[i];
            if (gear == null) continue;
            capacity += SumSourceEffects(new GameplayEffectSource(cards.GetGearInstanceAt(i)),
                GameplayEffectTrigger.HandCapacityCalculated, GameplayEffectKind.HandSizeBonus, null);
            capacity += GearRules.HandCapacityBonus(cards, gear);
        }
        for (int i = 0; i < cards.hand.Count; i++)
        {
            var card = cards.hand[i];
            if (card?.Enhancement != null)
                capacity += SumSourceEffects(new GameplayEffectSource(card),
                    GameplayEffectTrigger.HandCapacityCalculated, GameplayEffectKind.HandSizeBonus, card);
        }
        return Math.Max(0, capacity);
    }

    public static int ModifyIncomingCombatDamage(IncomingDamageEffectContext context)
    {
        int damage = Math.Max(0, context.RawDamage);
        var cards = context.Cards;
        if (cards == null) return damage;
        long reductions = 0;
        for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
        {
            var gear = cards.ownedGear[gearIndex];
            if (gear == null) continue;
            var source = new GameplayEffectSource(cards.GetGearInstanceAt(gearIndex));
            for (int j = 0; j < EffectCount(source); j++)
            {
                var effect = EffectAt(source, j);
                if (effect.kind == GameplayEffectKind.IncomingCombatDamageReduction &&
                    Applies(source, effect, GameplayEffectTrigger.IncomingDamageCalculated, null))
                    reductions += Math.Max(0, effect.amount);
            }
        }
        damage = ClampToInt(Math.Max(0L, damage - reductions));
        if (context.Request.Origin == CombatDamageOrigin.EnemyAggregate)
            damage = GearRules.ApplyAggregateIncomingRules(cards, damage);
        return damage;
    }

    public static void InitializeNodeCounters(GameplayEffectSource source, int completedNodeCount)
    {
        for (int i = 0; i < EffectCount(source); i++)
            if (EffectAt(source, i).trigger == GameplayEffectTrigger.NodeCompleted)
                source.State.SetCounter(i, completedNodeCount);
    }

    public static bool ApplyMapReady(MapReadyEffectContext context)
    {
        if (context.Run == null || context.Cards == null) return false;
        bool changed = false;
        for (int sourceOrder = 0; sourceOrder < context.Cards.ownedGear.Count; sourceOrder++)
        {
            var gear = context.Cards.ownedGear[sourceOrder];
            if (gear == null) continue;
            var source = new GameplayEffectSource(context.Cards.GetGearInstanceAt(sourceOrder));
            for (int effectIndex = 0; effectIndex < EffectCount(source); effectIndex++)
            {
                var effect = EffectAt(source, effectIndex);
                // Revealing the whole map is idempotent: it simply runs on every map-ready pass.
                if (effect.kind == GameplayEffectKind.RevealAllMapNodes &&
                    Applies(source, effect, GameplayEffectTrigger.MapReady, null))
                {
                    changed |= context.Run.RevealAllNodes();
                    continue;
                }
                if (effect.kind != GameplayEffectKind.RevealMapNode ||
                    !Applies(source, effect, GameplayEffectTrigger.MapReady, null) ||
                    source.State.GetCounter(effectIndex) == context.MapIndex + 1)
                    continue;
                if (!context.Run.TryRevealEligibleHiddenNode()) continue;
                source.State.SetCounter(effectIndex, context.MapIndex + 1);
                changed = true;
            }
            changed |= GearRules.ApplyMapReadyRules(gear, source.State, context);
        }
        return changed;
    }

    public static void ApplyNodeCompleted(NodeCompletedEffectContext context)
    {
        if (context.Run == null || context.Cards == null || context.Node == null ||
            !context.Node.completed) return;
        for (int sourceOrder = 0; sourceOrder < context.Cards.ownedGear.Count; sourceOrder++)
        {
            var gear = context.Cards.ownedGear[sourceOrder];
            if (gear == null) continue;
            var source = new GameplayEffectSource(context.Cards.GetGearInstanceAt(sourceOrder));
            for (int effectIndex = 0; effectIndex < EffectCount(source); effectIndex++)
            {
                var effect = EffectAt(source, effectIndex);
                if (effect.kind != GameplayEffectKind.GrantRandomCommonEnhancement ||
                    !Applies(source, effect, GameplayEffectTrigger.NodeCompleted, null)) continue;
                source.State.SetCounter(effectIndex, context.CompletedNodeCount);
                if (effect.amount > 0 && context.CompletedNodeCount % effect.amount == 0)
                    context.Run.TryGrantRandomCommonEnhancement(context.CompletedNodeCount);
            }
        }
    }

    public static void EnqueueEncounterStart(EncounterStartEffectContext context,
        CombatReactionQueue queue)
    {
        for (int i = 0; i < context.Cards.ownedGear.Count; i++)
        {
            var gear = context.Cards.ownedGear[i];
            if (gear != null)
                EnqueueEncounterSource(new GameplayEffectSource(context.Cards.GetGearInstanceAt(i)),
                    null, context, queue, i);
        }
        for (int i = 0; i < context.Cards.hand.Count; i++)
        {
            var card = context.Cards.hand[i];
            if (card?.Enhancement != null)
                EnqueueEncounterSource(new GameplayEffectSource(card), card, context, queue, i);
        }
    }

    static void EnqueueEncounterSource(GameplayEffectSource source, CardInstance card,
        EncounterStartEffectContext context, CombatReactionQueue queue, int sourceOrder)
    {
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (!Applies(source, effect, GameplayEffectTrigger.EncounterStart, card)) continue;
            int effectIndex = i;
            queue.Enqueue(context.Action.ActionId, -1, CombatReactionPhase.EncounterReady,
                source.Category, sourceOrder, effectIndex,
                () => ApplyReactive(source, effect, context.Combat, context.Cards, card, null));
        }
    }

    public static bool CanContinueSameRankSelection(IReadOnlyList<CardView> selected,
        CardView candidate, CardManager cards)
    {
        if (selected == null || candidate?.data == null || cards == null) return false;
        int count = selected.Count + 1;
        if (count < 2) return false;
        for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
        {
            var gear = cards.ownedGear[gearIndex];
            if (gear?.ruleModifiers == null) continue;
            for (int ruleIndex = 0; ruleIndex < gear.ruleModifiers.Length; ruleIndex++)
            {
                var rule = gear.ruleModifiers[ruleIndex];
                if (rule == null || rule.kind != GameplayRuleModifierKind.SameRankMultiCard ||
                    count > Math.Max(2, rule.sameRankMultiCard.maximumCards)) continue;
                if (!rule.sameRankMultiCard.requireMatchingRank) return true;
                var rank = selected.Count > 0 ? selected[0]?.data?.Rank : candidate.data.Rank;
                if (rank != candidate.data.Rank) continue;
                bool allMatch = true;
                for (int i = 1; i < selected.Count; i++)
                    if (selected[i]?.data == null || selected[i].data.Rank != rank) { allMatch = false; break; }
                if (allMatch) return true;
            }
        }
        return false;
    }

    public static bool CanContinueAttackSelection(IReadOnlyList<CardView> selected,
        CardView candidate, CardManager cards)
    {
        if (selected == null || candidate?.data == null || cards == null) return false;
        if (selected.Count == 0) return true;
        if (CanContinueSameRankSelection(selected, candidate, cards)) return true;
        if (!HasRoyalFamilyHeirloom(cards)) return false;
        var proposed = new List<CardInstance>(selected.Count + 1);
        for (int i = 0; i < selected.Count; i++) proposed.Add(selected[i]?.data);
        proposed.Add(candidate.data);
        return AttackSelectionRules.IsRoyalFamilyPartial(proposed);
    }

    public static bool HasRoyalFamilyHeirloom(CardManager manager) => GearRules.EnablesRoyalFamily(manager);

    public static bool IsRoyalFamilyPartialSelection(IReadOnlyList<CardView> cards, CardManager manager)
    {
        if (!HasRoyalFamilyHeirloom(manager) || cards == null || cards.Count == 0) return false;
        var instances = new CardInstance[cards.Count];
        for (int i = 0; i < cards.Count; i++) instances[i] = cards[i]?.data;
        return AttackSelectionRules.IsRoyalFamilyPartial(instances);
    }

    public static bool IsRoyalFamilySelection(IReadOnlyList<CardInstance> cards, CardManager manager) =>
        HasRoyalFamilyHeirloom(manager) && AttackSelectionRules.IsRoyalFamily(cards);

    public static bool IsHandsMultiCardAction(IReadOnlyList<CardInstance> cards, CardManager manager)
    {
        if (cards == null || cards.Count < 2 || cards.Count > 4 || manager == null || cards[0] == null) return false;
        // A legal two-card Ace action retains its existing Ace-pair semantics, even A+A.
        if (cards.Count == 2 && cards.Any(card => card?.Rank == CardData.Rank.Ace)) return false;
        for (int i = 1; i < cards.Count; i++)
            if (cards[i] == null || cards[i].Rank != cards[0].Rank) return false;
        return HasHandsRule(manager, cards.Count);
    }

    static bool HasHandsRule(CardManager manager, int cardCount)
    {
        for (int i = 0; i < manager.ownedGear.Count; i++)
        {
            var rules = manager.ownedGear[i]?.ruleModifiers;
            if (rules == null) continue;
            for (int j = 0; j < rules.Length; j++)
                if (rules[j] != null && rules[j].kind == GameplayRuleModifierKind.SameRankMultiCard &&
                    cardCount <= Math.Max(2, rules[j].sameRankMultiCard.maximumCards)) return true;
        }
        return false;
    }

    // Selection rules operate on committed card data; CardView overloads only adapt UI selections.
    static CardInstance[] Instances(IReadOnlyList<CardView> views)
    {
        if (views == null) return null;
        var instances = new CardInstance[views.Count];
        for (int i = 0; i < views.Count; i++) instances[i] = views[i]?.data;
        return instances;
    }

    public static bool IsSameRankMultiCardAction(IReadOnlyList<CardView> cards, CardManager manager) =>
        IsSameRankMultiCardAction(Instances(cards), manager);

    public static bool IsSameRankMultiCardAction(IReadOnlyList<CardInstance> cards, CardManager manager)
    {
        if (cards == null || cards.Count < 2 || manager == null || cards[0] == null) return false;
        for (int i = 1; i < cards.Count; i++)
            if (cards[i] == null || cards[i].Rank != cards[0].Rank) return false;
        for (int i = 0; i < manager.ownedGear.Count; i++)
        {
            var rules = manager.ownedGear[i]?.ruleModifiers;
            if (rules == null) continue;
            for (int j = 0; j < rules.Length; j++)
            {
                var rule = rules[j];
                if (rule == null || rule.kind != GameplayRuleModifierKind.SameRankMultiCard ||
                    cards.Count > Math.Max(2, rule.sameRankMultiCard.maximumCards)) continue;
                if (!rule.sameRankMultiCard.requireMatchingRank) return true;
                bool allMatch = true;
                for (int cardIndex = 1; cardIndex < cards.Count; cardIndex++)
                    if (cards[cardIndex] == null || cards[cardIndex].Rank != cards[0].Rank)
                    { allMatch = false; break; }
                if (allMatch) return true;
            }
        }
        return false;
    }

    public static void EnqueueCompletedActionTurnGrants(CompletedPlayerActionContext context,
        Queue<PlayerTurnGrant> grants)
    {
        var cards = context.Cards;
        if (cards == null || context.CommittedCards == null || grants == null) return;
        for (int sourceOrder = 0; sourceOrder < cards.ownedGear.Count; sourceOrder++)
        {
            var gear = cards.ownedGear[sourceOrder];
            if (gear == null) continue;
            var source = new GameplayEffectSource(cards.GetGearInstanceAt(sourceOrder));
            EnqueueExtraTurnRules(source, gear.ruleModifiers, context, grants, sourceOrder);
        }
        // If a future Enhancement carries a rule modifier, it follows Gear sources
        // and the committed-card order, without a separate rule engine.
        for (int cardOrder = 0; cardOrder < context.CommittedCards.Count; cardOrder++)
        {
            var card = context.CommittedCards[cardOrder];
            if (card?.Enhancement == null) continue;
            EnqueueExtraTurnRules(new GameplayEffectSource(card), card.Enhancement.ruleModifiers,
                context, grants, cards.ownedGear.Count + cardOrder);
        }
    }

    static void EnqueueExtraTurnRules(GameplayEffectSource source,
        GameplayRuleModifierData[] rules, CompletedPlayerActionContext context,
        Queue<PlayerTurnGrant> grants, int sourceOrder)
    {
        if (rules == null) return;
        for (int ruleOrder = 0; ruleOrder < rules.Length; ruleOrder++)
        {
            var rule = rules[ruleOrder];
            if (rule == null || rule.kind != GameplayRuleModifierKind.ExtraTurn) continue;
            var ruleData = rule.extraTurn;
            if (context.Action?.IsBonusAction == true && ruleData.cannotTriggerFromBonusAction) continue;
            bool qualifies = ruleData.trigger switch
            {
                ExtraTurnRuleTrigger.FirstPlayerTurnCompleted => context.CompletedPlayerTurn == 1,
                ExtraTurnRuleTrigger.QualifyingPlayedActionCompleted =>
                    context.CommittedCards.Count > 0 && (!ruleData.requireAllPlayedCardsBelowRank ||
                        context.CommittedCards.All(card => card != null && card.Rank < ruleData.exclusiveRank)),
                _ => false
            };
            if (!qualifies) continue;
            if (ruleData.oncePerEncounter)
            {
                if (source.State.GetEncounterCounter(ruleOrder) != 0) continue;
                source.State.SetEncounterCounter(ruleOrder, 1);
            }
            grants.Enqueue(new PlayerTurnGrant(source.DisplayName, context.Action.ActionId,
                sourceOrder, ruleOrder));
        }
    }

    public static bool CanPlayAsAcePair(IReadOnlyList<CardView> cards) => CanPlayAsAcePair(Instances(cards));

    public static bool CanPlayAsAcePair(IReadOnlyList<CardInstance> cards)
    {
        return cards != null && cards.Count == 2 && cards[0] != null && cards[1] != null &&
            (cards[0].Rank == CardData.Rank.Ace || cards[1].Rank == CardData.Rank.Ace);
    }

    public static bool CanPlaySelection(IReadOnlyList<CardView> cards, EnemyRuntime target,
        CardManager manager) => CanPlaySelection(Instances(cards), target, manager);

    public static bool CanPlaySelection(IReadOnlyList<CardInstance> cards, EnemyRuntime target,
        CardManager manager)
    {
        if (target == null || target.IsDefeated || cards == null || cards.Count == 0 || cards.Count > 5) return false;
        if (!cards.All(card => card != null)) return false;
        if (cards.Count <= 4 && (cards.Count == 1 || CanPlayAsAcePair(cards) || IsSameRankMultiCardAction(cards, manager))) return true;
        if (cards.Count == 5)
            return IsRoyalFamilySelection(cards, manager);
        return false;
    }

    public static void EnqueueCardCommitted(CardCommittedEffectContext context, CombatReactionQueue queue)
    {
        for (int i = 0; i < context.Cards.ownedGear.Count; i++)
        {
            var gear = context.Cards.ownedGear[i];
            var gearInstance = context.Cards.GetGearInstanceAt(i);
            if (gear != null)
            {
                EnqueueCommittedSource(new GameplayEffectSource(gearInstance),
                    context, queue, i);
                GearRules.EnqueueCardCommittedRules(gear, gearInstance, i, context, queue);
            }
        }
        if (context.Card.Enhancement != null && !HeldCardEffectsSuppressed(context.Combat))
            for (int activation = 0; activation < context.CardEffectActivations; activation++)
                EnqueueCommittedSource(new GameplayEffectSource(context.Card), context, queue,
                    activation * 10000);
    }

    static void EnqueueCommittedSource(GameplayEffectSource source, CardCommittedEffectContext context,
        CombatReactionQueue queue, int sourceOrder)
    {
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (source.Gear != null && effect.kind == GameplayEffectKind.MimicInHandEffects)
            {
                if (!Applies(source, effect, GameplayEffectTrigger.CardCommitted, context.Card)) continue;
                int mimicEffectIndex = i;
                queue.Enqueue(context.Action.ActionId, context.HitIndex,
                    CombatReactionPhase.CardCommitted, source.Category, sourceOrder, mimicEffectIndex,
                    () => EnqueueMimickedInHandEffects(context, queue, sourceOrder));
                continue;
            }
            if (!Applies(source, effect, GameplayEffectTrigger.CardCommitted, context.Card)) continue;
            int effectIndex = i;
            queue.Enqueue(context.Action.ActionId, context.HitIndex, CombatReactionPhase.CardCommitted,
                source.Category, sourceOrder, effectIndex,
                () => ApplyReactive(source, effect, context.Combat, context.Cards, context.Card, context.Target));
        }
    }

    static void EnqueueMimickedInHandEffects(CardCommittedEffectContext context,
        CombatReactionQueue queue, int mimicSourceOrder)
    {
        if (HeldCardEffectsSuppressed(context.Combat)) return;
        var cards = context.Cards;
        for (int handIndex = 0; handIndex < cards.hand.Count; handIndex++)
        {
            var card = cards.hand[handIndex];
            if (card?.Enhancement == null) continue;
            var source = new GameplayEffectSource(card);
            for (int effectIndex = 0; effectIndex < EffectCount(source); effectIndex++)
            {
                var effect = EffectAt(source, effectIndex);
                if (!IsMimicRetriggerableHandEffect(effect) || !effect.Matches(card)) continue;
                bool epicExtra = context.Card != null && card.Rank == context.Card.Rank &&
                    GearRules.OwnsMimicEpic(cards);
                int repeats = epicExtra ? 2 : 1;
                int capturedIndex = effectIndex;
                int sourceOrder = cards.ownedGear.Count + handIndex + mimicSourceOrder;
                for (int repeat = 0; repeat < repeats; repeat++)
                    queue.Enqueue(context.Action.ActionId, context.HitIndex,
                        CombatReactionPhase.CardCommitted, CombatReactionSourceCategory.Enhancement,
                        sourceOrder, capturedIndex + repeat * 10000,
                        () => ApplyReactive(source, effect, context.Combat, cards, card, context.Target,
                            sourceOrder, capturedIndex));
            }
        }
    }

    static bool IsMimicRetriggerableHandEffect(GameplayEffectDefinition effect)
    {
        if (effect.Category != GameplayEffectCategory.Reactive ||
            effect.trigger is not (GameplayEffectTrigger.AttackCommitted or GameplayEffectTrigger.PlayerTurnStart or
                GameplayEffectTrigger.EncounterStart)) return false;
        return effect.kind is GameplayEffectKind.Heal or GameplayEffectKind.Draw or
            GameplayEffectKind.BonusCredits or GameplayEffectKind.ReduceAttackByCardValue or
            GameplayEffectKind.RecycleDiscardByCardValue or GameplayEffectKind.DrawByCardValue;
    }

    public static void EnqueueAttackBlocked(AttackBlockedEffectContext context,
        CombatReactionQueue queue)
    {
        if (context.Action == null || context.BlockingCard == null || context.AttackedEnemy == null ||
            context.Combat == null || context.Cards == null) return;
        for (int sourceOrder = 0; sourceOrder < context.Cards.ownedGear.Count; sourceOrder++)
        {
            var gear = context.Cards.ownedGear[sourceOrder];
            if (gear == null) continue;
            EnqueueAttackBlockedSource(
                new GameplayEffectSource(context.Cards.GetGearInstanceAt(sourceOrder)),
                context, queue, sourceOrder);
        }
        if (context.BlockingCard.Enhancement != null)
            EnqueueAttackBlockedSource(new GameplayEffectSource(context.BlockingCard),
                context, queue, context.BlockOrder);
    }

    static void EnqueueAttackBlockedSource(GameplayEffectSource source,
        AttackBlockedEffectContext context, CombatReactionQueue queue, int sourceOrder)
    {
        for (int effectIndex = 0; effectIndex < EffectCount(source); effectIndex++)
        {
            var effect = EffectAt(source, effectIndex);
            if (!Applies(source, effect, GameplayEffectTrigger.AttackBlocked, context.BlockingCard)) continue;
            if (effect.kind == GameplayEffectKind.DestroyBlockingCard)
            {
                if (source.Gear == null || source.State.GetEncounterCounter(effectIndex) != 0) continue;
                source.State.SetEncounterCounter(effectIndex, 1);
            }
            else if (effect.kind != GameplayEffectKind.CounterDamage) continue;
            int capturedIndex = effectIndex;
            queue.Enqueue(context.Action.ActionId, int.MinValue + context.BlockOrder, CombatReactionPhase.AttackBlocked,
                source.Category, sourceOrder, capturedIndex,
                () => ApplyReactive(source, effect, context.Combat, context.Cards,
                    context.BlockingCard, context.AttackedEnemy, sourceOrder, capturedIndex));
        }
        GearRules.EnqueueAttackBlockedRules(source, context, queue, sourceOrder);
    }

    public static int CalculateRetaliationDamage(int committedBlock, int incomingAttack)
    {
        int block = Math.Max(0, committedBlock);
        int attack = Math.Max(0, incomingAttack);
        int actuallyBlocked = Math.Min(block, attack);
        if (actuallyBlocked <= 0) return 0;
        return (long)block * 2 > (long)attack * 3
            ? ClampToInt((long)actuallyBlocked * 2) : actuallyBlocked;
    }

    public static void EnqueueAttackCommitted(AttackCommittedEffectContext context,
        CombatReactionQueue queue)
    {
        if (context.Action == null || context.Target == null || context.Combat == null || context.Cards == null)
            return;
        for (int sourceOrder = 0; sourceOrder < context.Cards.ownedGear.Count; sourceOrder++)
        {
            var gear = context.Cards.ownedGear[sourceOrder];
            if (gear != null)
                EnqueueAttackCommittedSource(new GameplayEffectSource(context.Cards.GetGearInstanceAt(sourceOrder)),
                    context, queue, sourceOrder);
        }
        if (context.HandSnapshot == null || HeldCardEffectsSuppressed(context.Combat)) return;
        for (int handOrder = 0; handOrder < context.HandSnapshot.Count; handOrder++)
        {
            var card = context.HandSnapshot[handOrder];
            if (card?.Enhancement != null)
                EnqueueAttackCommittedSource(new GameplayEffectSource(card), context, queue, handOrder);
        }
    }

    static void EnqueueAttackCommittedSource(GameplayEffectSource source,
        AttackCommittedEffectContext context, CombatReactionQueue queue, int sourceOrder)
    {
        for (int effectIndex = 0; effectIndex < EffectCount(source); effectIndex++)
        {
            var effect = EffectAt(source, effectIndex);
            if (effect.trigger != GameplayEffectTrigger.AttackCommitted) continue;
            bool applies = false;
            if (source.Card != null)
                applies = Applies(source, effect, GameplayEffectTrigger.AttackCommitted, source.Card);
            else if (context.CommittedCards != null)
                for (int i = 0; i < context.CommittedCards.Count && !applies; i++)
                    applies = Applies(source, effect, GameplayEffectTrigger.AttackCommitted,
                        context.CommittedCards[i]);
            else
                applies = Applies(source, effect, GameplayEffectTrigger.AttackCommitted, null);
            if (!applies) continue;
            int capturedIndex = effectIndex;
            queue.Enqueue(context.Action.ActionId, -1, CombatReactionPhase.AttackCommitted,
                source.Category, sourceOrder, capturedIndex,
                () => ApplyReactive(source, effect, context.Combat, context.Cards,
                    source.Card, context.Target, sourceOrder, capturedIndex));
        }
    }

    public static void EnqueuePlayerTurnStart(PlayerTurnStartEffectContext context, CombatReactionQueue queue)
    {
        for (int i = 0; i < context.Cards.ownedGear.Count; i++)
        {
            var gear = context.Cards.ownedGear[i];
            if (gear != null)
                EnqueueTurnSource(new GameplayEffectSource(context.Cards.GetGearInstanceAt(i)),
                    null, context, queue, i);
        }
        // Snapshot hand order and identity at the authoritative turn-start boundary.
        if (HeldCardEffectsSuppressed(context.Combat)) return;
        for (int i = 0; i < context.Cards.hand.Count; i++)
        {
            var card = context.Cards.hand[i];
            if (card?.Enhancement != null)
                EnqueueTurnSource(new GameplayEffectSource(card), card, context, queue, i);
        }
    }

    static void EnqueueTurnSource(GameplayEffectSource source, CardInstance card,
        PlayerTurnStartEffectContext context, CombatReactionQueue queue, int sourceOrder)
    {
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (!Applies(source, effect, GameplayEffectTrigger.PlayerTurnStart, card)) continue;
            int effectIndex = i;
            queue.Enqueue(context.Action.ActionId, -1, CombatReactionPhase.PlayerTurnStarted,
                source.Category, sourceOrder, effectIndex,
                () => ApplyReactive(source, effect, context.Combat, context.Cards, card, null));
        }
    }

    public static (int bonusCredits, int heal) EncounterWon(CardManager cards)
    {
        int credits = 0, heal = 0;
        for (int i = 0; i < cards.ownedGear.Count; i++)
        {
            var gear = cards.ownedGear[i];
            if (gear == null) continue;
            var source = new GameplayEffectSource(cards.GetGearInstanceAt(i));
            for (int j = 0; j < EffectCount(source); j++)
            {
                var effect = EffectAt(source, j);
                if (effect.trigger != GameplayEffectTrigger.EncounterWon || !effect.Matches(null)) continue;
                if (effect.kind == GameplayEffectKind.BonusCredits) credits += effect.amount;
                else if (effect.kind == GameplayEffectKind.Heal) heal += effect.amount;
            }
        }
        return (credits, heal);
    }

    static void ApplyReactive(GameplayEffectSource source, GameplayEffectDefinition effect,
        CombatManager combat, CardManager cards, CardInstance card, EnemyRuntime target,
        int sourceOrder = 0, int handlerOrder = 0)
    {
        if (source.Card != null && HeldCardEffectsSuppressed(combat)) return;
        int amount = Mathf.Max(0, effect.amount);
        switch (effect.kind)
        {
            case GameplayEffectKind.Heal:
                int healed = combat.HealPlayer(amount);
                if (healed > 0) combat.LogEffect($"{source.DisplayName}: Healed {healed} HP.");
                break;
            case GameplayEffectKind.Draw:
                int drawn = cards.DrawToHand(amount);
                if (drawn > 0) combat.LogEffect($"{source.DisplayName}: Drew {drawn} card(s).");
                break;
            case GameplayEffectKind.BonusCredits:
                cards.AddCredits(amount);
                break;
            case GameplayEffectKind.ReduceAttackByCardValue:
                if (target == null || card == null) break;
                target.ReduceAttack(card.BaseAttackValue);
                combat.LogEffect($"{source.DisplayName}: Enemy ATK reduced by {card.BaseAttackValue} to {target.currentAttack}!");
                break;
            case GameplayEffectKind.RecycleDiscardByCardValue:
                if (card == null) break;
                int returned = cards.ReturnRandomDiscardToDeck(card.BaseAttackValue);
                if (returned > 0) combat.LogEffect($"{source.DisplayName}: {returned} cards returned to deck!");
                break;
            case GameplayEffectKind.DrawByCardValue:
                if (card == null) break;
                int drawnByValue = cards.DrawToHand(card.BaseAttackValue);
                if (drawnByValue > 0) combat.LogEffect($"{source.DisplayName}: Drew {drawnByValue} cards!");
                break;
            case GameplayEffectKind.CounterDamage:
                if (target == null) break;
                if (combat.QueueReactiveDamage(combat.player, target, amount, sourceOrder,
                    handlerOrder, sourceCategory: source.Category))
                    combat.LogEffect($"{source.DisplayName}: Counterattacked {target.DisplayName} for {amount} damage.");
                break;
            case GameplayEffectKind.DestroyBlockingCard:
                if (card == null || cards == null) break;
                string destroyedName = card.DisplayName;
                if (cards.DestroyOwnedCard(card))
                    combat.LogEffect($"{source.DisplayName}: destroyed {destroyedName} after blocking.");
                break;
        }
    }
}
