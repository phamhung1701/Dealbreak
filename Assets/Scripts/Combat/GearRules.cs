using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Gear behavior that typed <see cref="GameplayEffectDefinition"/> data cannot express yet.
/// Managers decide <i>when</i> a hook runs; this class decides <i>what</i> each
/// <see cref="GearSpecialRule"/> does. Hooks visit Gear in acquisition order, which is also the
/// reaction source order. Prefer authoring typed effects over adding rules here.
/// </summary>
public static partial class GearRules
{
    /// <summary>Per-action stateful attack bonuses, prepared before the action commits.</summary>
    public struct ActionBonuses
    {
        public int Kingslayer, Dwarf, Balancer, Spade, Heart;
        public CardInstance DwarfSource;
        public int Total => Kingslayer + Dwarf + Balancer + Spade + Heart;
    }

    static IEnumerable<(GearData gear, GearRuntimeInstance instance)> Owned(CardManager cards)
    {
        if (cards == null) yield break;
        for (int gearIndex = 0; gearIndex < cards.ownedGear.Count; gearIndex++)
        {
            var gear = cards.ownedGear[gearIndex];
            if (gear != null) yield return (gear, cards.GetGearInstanceAt(gearIndex));
        }
    }

    // ---- Encounter start ------------------------------------------------------------------

    public static void OnEncounterStarting(CardManager cards, PlayerRuntime player, bool eliteOrHeadliner)
    {
        if (cards == null) return;
        if (cards.HasGearSpecialRule(GearSpecialRule.PreparationManual))
            player.GainEncounterBlock(cards.DistinctConsumableTypeCount * 2);
        foreach (var (gear, instance) in Owned(cards))
            if (gear.specialRule == GearSpecialRule.WarpathBanner)
                player.GainEncounterBlock(Math.Min(6,
                    instance.State.GetCounter(GearCounterKeys.WarpathBannerWinStreak) * 2));
        if (eliteOrHeadliner && cards.HasGearSpecialRule(GearSpecialRule.ChallengerCrest))
            player.GainShield(1);
    }

    // ---- Player attack actions ------------------------------------------------------------

    /// <param name="commit">False for previews: computes the same bonuses without resetting state.</param>
    public static ActionBonuses PrepareActionBonuses(CardManager cards, IReadOnlyList<CardInstance> playedCards,
        bool headlinerEncounter, bool commit = true)
    {
        var bonuses = new ActionBonuses();
        if (cards == null || playedCards == null) return bonuses;
        foreach (var (gear, instance) in Owned(cards))
        {
            if (instance == null) continue;
            var state = instance.State;
            if (headlinerEncounter && gear.specialRule == GearSpecialRule.KingslayersMark &&
                state.GetEncounterCounter(GearCounterKeys.KingslayerCharges) > 0)
                bonuses.Kingslayer = 1;
            if (gear.specialRule is GearSpecialRule.DwarfRare or GearSpecialRule.DwarfEpic)
            {
                var source = instance.PreparedDwarfSourceCard;
                bool qualifying = source != null && cards.FindOwnedCard(source.Id) == source &&
                    playedCards.Any(card => card != null && card.Rank > source.Rank);
                if (qualifying)
                {
                    bonuses.Dwarf = 1;
                    bonuses.DwarfSource = source;
                }
            }
            if (gear.specialRule == GearSpecialRule.BalancersScale)
            {
                int previousGroup = state.GetCounter(GearCounterKeys.BalancersScalePreviousGroup);
                int playedGroup = playedCards.Count > 0 ? GetBalancerGroup(playedCards[0]) : 0;
                if (playedGroup != 0 && playedGroup == previousGroup)
                {
                    if (commit) state.SetCounter(GearCounterKeys.BalancersScaleBonus, 0);
                    bonuses.Balancer = 0;
                }
                else bonuses.Balancer = Math.Max(0, state.GetCounter(GearCounterKeys.BalancersScaleBonus));
            }
            if (gear.specialRule == GearSpecialRule.SpadeEpic)
                bonuses.Spade = Math.Max(0, state.GetEncounterCounter(GearCounterKeys.SpadeEpicOverblocks));
            if (gear.specialRule == GearSpecialRule.HeartEpic &&
                playedCards.Any(card => card != null && card.MatchesSuit(CardData.Suit.Hearts)))
                bonuses.Heart = Math.Max(0, state.GetCounter(GearCounterKeys.HeartEpicStoredOverheal));
        }
        return bonuses;
    }

    /// <summary>Consumes spent bonuses and advances per-card Gear state once per committed action.</summary>
    /// <returns>The number of Diamond Emblem discard choices earned by this action.</returns>
    public static int AdvanceActionState(CardManager cards, IReadOnlyList<CardInstance> playedCards,
        ActionBonuses spent, bool autoPlay, bool headlinerEncounter)
    {
        int diamondChoices = 0;
        if (cards == null || playedCards == null) return diamondChoices;
        foreach (var (gear, instance) in Owned(cards))
        {
            var state = instance.State;
            if (spent.DwarfSource != null && instance.PreparedDwarfSourceCard == spent.DwarfSource)
            {
                instance.PendingDwarfSourceCard = spent.DwarfSource;
                instance.PreparedDwarfSourceCard = null;
            }
            if (spent.Kingslayer > 0 && gear.specialRule == GearSpecialRule.KingslayersMark)
                state.SetEncounterCounter(GearCounterKeys.KingslayerCharges,
                    Math.Max(0, state.GetEncounterCounter(GearCounterKeys.KingslayerCharges) - 1));
            if (spent.Balancer > 0 && gear.specialRule == GearSpecialRule.BalancersScale)
                state.SetCounter(GearCounterKeys.BalancersScaleBonus, 0);
            if (spent.Spade > 0 && gear.specialRule == GearSpecialRule.SpadeEpic)
                state.SetEncounterCounter(GearCounterKeys.SpadeEpicOverblocks, 0);
            if (spent.Heart > 0 && gear.specialRule == GearSpecialRule.HeartEpic)
                state.SetCounter(GearCounterKeys.HeartEpicStoredOverheal, 0);
        }
        if (autoPlay) return diamondChoices;

        foreach (var (gear, instance) in Owned(cards))
        {
            var state = instance.State;
            foreach (var card in playedCards)
            {
                if (card == null) continue;
                if (gear.specialRule is GearSpecialRule.DwarfRare or GearSpecialRule.DwarfEpic &&
                    card.Rank < CardData.Rank.Five)
                    instance.PreparedDwarfSourceCard = card;
                if (headlinerEncounter && gear.specialRule == GearSpecialRule.KingslayersMark)
                {
                    int mask = state.GetEncounterCounter(GearCounterKeys.KingslayerRankMask) | (1 << (int)card.Rank);
                    int uniqueRanks = 0;
                    for (int bits = mask; bits != 0; bits >>= 1) uniqueRanks += bits & 1;
                    if (uniqueRanks >= 3)
                    {
                        state.SetEncounterCounter(GearCounterKeys.KingslayerRankMask, 0);
                        state.SetEncounterCounter(GearCounterKeys.KingslayerCharges,
                            state.GetEncounterCounter(GearCounterKeys.KingslayerCharges) + 1);
                    }
                    else state.SetEncounterCounter(GearCounterKeys.KingslayerRankMask, mask);
                }
                if (gear.specialRule == GearSpecialRule.DiamondEpic &&
                    card.MatchesSuit(CardData.Suit.Diamonds))
                {
                    int count = state.GetCounter(GearCounterKeys.DiamondEpicSuitCount) + 1;
                    if (count >= 3)
                    {
                        diamondChoices++;
                        count -= 3;
                    }
                    state.SetCounter(GearCounterKeys.DiamondEpicSuitCount, count);
                }
                if (gear.specialRule == GearSpecialRule.BalancersScale)
                {
                    int group = GetBalancerGroup(card);
                    if (group == 0) continue;
                    int previous = state.GetCounter(GearCounterKeys.BalancersScalePreviousGroup);
                    int bonus = state.GetCounter(GearCounterKeys.BalancersScaleBonus);
                    if (group == previous) bonus = 0;
                    else if (previous != 0) bonus++;
                    state.SetCounter(GearCounterKeys.BalancersScalePreviousGroup, group);
                    state.SetCounter(GearCounterKeys.BalancersScaleBonus, bonus);
                }
            }
        }
        return diamondChoices;
    }

    static int GetBalancerGroup(CardInstance card) => card == null ? 0 :
        card.Rank < CardData.Rank.Five ? 1 :
        card.Rank is CardData.Rank.Jack or CardData.Rank.Queen or CardData.Rank.King ? 2 : 0;

    /// <summary>Hands Epic: a same-rank Hands action triggers each card's committed effects twice.</summary>
    public static bool HandsActionRepeatsCardEffects(CardManager cards) =>
        cards != null && cards.HasGearSpecialRule(GearSpecialRule.HandsEpic);

    public static void ClearPendingDwarfSources(CardManager cards)
    {
        foreach (var (_, instance) in Owned(cards))
            instance.PendingDwarfSourceCard = null;
    }

    /// <summary>Hunter's Ledger and Trophy Rack bonuses for the opening attack of an encounter.</summary>
    public static int OpeningAttackBonus(CardManager cards, bool headlinerEncounter)
    {
        int bonus = 0;
        foreach (var (gear, instance) in Owned(cards))
        {
            if (gear.specialRule == GearSpecialRule.HuntersLedger)
                bonus += instance.State.GetCounter(GearCounterKeys.HuntersLedgerEliteKills);
            else if (gear.specialRule == GearSpecialRule.TrophyRack && headlinerEncounter)
                bonus += instance.State.GetCounter(GearCounterKeys.TrophyRackEliteKills);
        }
        return bonus;
    }

    /// <summary>Dwarf Epic: a prepared low card permanently gains attack when a higher card kills.</summary>
    public static void OnKill(CardManager cards, IReadOnlyList<CardInstance> committedCards, Action<string> log)
    {
        if (committedCards == null) return;
        foreach (var (gear, instance) in Owned(cards))
        {
            if (gear.specialRule != GearSpecialRule.DwarfEpic) continue;
            var source = instance.PendingDwarfSourceCard;
            if (source == null || cards.FindOwnedCard(source.Id) != source ||
                !committedCards.Any(card => card != null && card.Rank > source.Rank)) continue;
            source.GainPermanentAttackBonus(1);
            cards.NotifyCardInstanceChanged(source);
            instance.PendingDwarfSourceCard = null;
            log?.Invoke($"{gear.displayName}: prepared {source.DisplayName} permanently gained +1 attack.");
        }
    }

    /// <summary>Ace Emblem: once per encounter, a critical Ace pair returns its physical Ace to hand.</summary>
    public static void TryReturnCriticalAce(CardManager cards, IReadOnlyList<CardInstance> committedCards,
        Action<string> log)
    {
        if (committedCards == null || cards == null) return;
        foreach (var (gear, instance) in Owned(cards))
        {
            if (gear.specialRule is not (GearSpecialRule.AceRare or GearSpecialRule.AceEpic)) continue;
            var state = instance.State;
            if (state.GetEncounterCounter(GearCounterKeys.AceReturnUsed) != 0) continue;
            var ace = committedCards.FirstOrDefault(card => card != null && card.Rank == CardData.Rank.Ace);
            if (ace == null || !cards.TryTakeCardToHand(ace)) continue;
            state.SetEncounterCounter(GearCounterKeys.AceReturnUsed, 1);
            log?.Invoke($"{gear.displayName}: returned the physical Ace to hand.");
            return;
        }
    }

    /// <summary>Bounty Ledger scales defeated-enemy Credits (×1.5 Enforcer, ×1.25 Headliner).</summary>
    public static int ScaleVictoryCredits(CardManager cards, int credits, bool headlinerEncounter,
        bool eliteOrHeadlinerEncounter)
    {
        if (cards == null || !cards.HasGearSpecialRule(GearSpecialRule.BountyLedger)) return credits;
        int numerator = headlinerEncounter ? 125 : eliteOrHeadlinerEncounter ? 150 : 100;
        return (int)Math.Min(int.MaxValue, ((long)credits * numerator + 50) / 100);
    }
}
