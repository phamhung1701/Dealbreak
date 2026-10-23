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
    static GearBalance Tuning => GameBalance.Current.gear;

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
        {
            int block = cards.DistinctConsumableTypeCount * Tuning.guardDroneBlockPerTrickType;
            player.GainEncounterBlock(block);
            if (block > 0) ReportFirst(cards, GearSpecialRule.PreparationManual, EffectCue.Block, block);
        }
        foreach (var (gear, instance) in Owned(cards))
            if (gear.specialRule == GearSpecialRule.WarpathBanner)
            {
                int block = Math.Min(Tuning.varsityJacketMaxStreak,
                    instance.State.GetCounter(GearCounterKeys.WarpathBannerWinStreak)) * Tuning.varsityJacketBlockPerWin;
                player.GainEncounterBlock(block);
                if (block > 0) EffectTriggerFeed.Report(instance, EffectCue.Block, block);
            }
        if (eliteOrHeadliner && cards.HasGearSpecialRule(GearSpecialRule.ChallengerCrest))
        {
            int gained = player.GainShield(Tuning.riotHelmetShield);
            if (gained > 0) ReportFirst(cards, GearSpecialRule.ChallengerCrest, EffectCue.Shield, gained);
        }
    }

    /// <summary>Reports the first owned Gear with <paramref name="rule"/>: for rules that apply once
    /// however many copies are owned.</summary>
    static void ReportFirst(CardManager cards, GearSpecialRule rule, EffectCue cue, int amount)
    {
        foreach (var (gear, instance) in Owned(cards))
            if (gear.specialRule == rule)
            {
                EffectTriggerFeed.Report(instance, cue, amount);
                return;
            }
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
                bonuses.Kingslayer = Tuning.spotterEyeAttackBonus;
            if (gear.specialRule is GearSpecialRule.DwarfRare or GearSpecialRule.DwarfEpic)
            {
                var source = instance.PreparedDwarfSourceCard;
                bool qualifying = source != null && cards.FindOwnedCard(source.Id) == source &&
                    playedCards.Any(card => card != null && card.RanksAbove(source.Rank));
                if (qualifying)
                {
                    bonuses.Dwarf = Tuning.lowballAttackBonus;
                    bonuses.DwarfSource = source;
                }
            }
            if (gear.specialRule == GearSpecialRule.BalancersScale)
            {
                int previousGroup = state.GetCounter(GearCounterKeys.BalancersScalePreviousGroup);
                int playedGroup = playedCards.Count > 0 ? GetBalancerGroup(playedCards[0], previousGroup) : 0;
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
                    card.RanksBelow(Tuning.lowballBelowRank))
                    instance.PreparedDwarfSourceCard = card;
                if (headlinerEncounter && gear.specialRule == GearSpecialRule.KingslayersMark)
                {
                    int mask = state.GetEncounterCounter(GearCounterKeys.KingslayerRankMask);
                    mask |= 1 << (int)(card.IsWildRank ? FirstUncountedRank(mask) : card.Rank);
                    int uniqueRanks = 0;
                    for (int bits = mask; bits != 0; bits >>= 1) uniqueRanks += bits & 1;
                    if (uniqueRanks >= Tuning.spotterEyeDistinctRanks)
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
                    if (count >= Tuning.diamondDriverEpicDiamondsPerChoice)
                    {
                        diamondChoices++;
                        count -= Tuning.diamondDriverEpicDiamondsPerChoice;
                    }
                    state.SetCounter(GearCounterKeys.DiamondEpicSuitCount, count);
                }
                if (gear.specialRule == GearSpecialRule.BalancersScale)
                {
                    int previous = state.GetCounter(GearCounterKeys.BalancersScalePreviousGroup);
                    int group = GetBalancerGroup(card, previous);
                    if (group == 0) continue;
                    int bonus = state.GetCounter(GearCounterKeys.BalancersScaleBonus);
                    if (group == previous) bonus = 0;
                    else if (previous != 0) bonus = ClampToInt((long)bonus + Tuning.flipSwitchAttackBonus);
                    state.SetCounter(GearCounterKeys.BalancersScalePreviousGroup, group);
                    state.SetCounter(GearCounterKeys.BalancersScaleBonus, bonus);
                }
            }
        }
        return diamondChoices;
    }

    // Flip Switch groups: 1 below Five, 2 face cards. A Joker takes the group the last card did not.
    static int GetBalancerGroup(CardInstance card, int previousGroup) => card == null ? 0 :
        card.IsWildRank ? (previousGroup == 1 ? 2 : 1) :
        card.Rank < CardData.Rank.Five ? 1 :
        card.Rank is CardData.Rank.Jack or CardData.Rank.Queen or CardData.Rank.King ? 2 : 0;

    // Kingslayer's Mark: a Joker counts as the lowest rank not yet counted.
    static CardData.Rank FirstUncountedRank(int mask)
    {
        foreach (CardData.Rank rank in Enum.GetValues(typeof(CardData.Rank)))
            if ((mask & (1 << (int)rank)) == 0) return rank;
        return CardData.Rank.Ace;
    }

    /// <summary>Hands Epic: a same-rank Hands action triggers each card's committed effects twice.</summary>
    public static bool HandsActionRepeatsCardEffects(CardManager cards) =>
        cards != null && cards.HasGearSpecialRule(GearSpecialRule.HandsEpic);

    public static void ClearPendingDwarfSources(CardManager cards)
    {
        foreach (var (_, instance) in Owned(cards))
            instance.PendingDwarfSourceCard = null;
    }

    /// <summary>Reports the Gear behind an action's once-per-action attack bonuses (prepared bonuses,
    /// Gold Pistol, Headhunter Rifle and, on the opening attack, Notched Pistol).</summary>
    public static void TraceActionBonuses(CardManager cards, ActionBonuses bonuses, bool eliteOrHeadlinerEncounter,
        bool openingAttack, EffectTriggerTrace trace)
    {
        if (trace == null) return;
        foreach (var (gear, instance) in Owned(cards))
        {
            int amount = gear.specialRule switch
            {
                GearSpecialRule.KingslayersMark => bonuses.Kingslayer,
                GearSpecialRule.DwarfRare or GearSpecialRule.DwarfEpic when bonuses.DwarfSource != null &&
                    instance.PreparedDwarfSourceCard == bonuses.DwarfSource => bonuses.Dwarf,
                GearSpecialRule.BalancersScale => bonuses.Balancer,
                GearSpecialRule.SpadeEpic => bonuses.Spade,
                GearSpecialRule.HeartEpic => bonuses.Heart,
                GearSpecialRule.GildedBlade => GoldPistolBonus(cards),
                GearSpecialRule.TrophyRack when eliteOrHeadlinerEncounter => ClampToInt(
                    (long)instance.State.GetCounter(GearCounterKeys.TrophyRackEliteKills) * Tuning.headhunterRifleAttackPerKill),
                GearSpecialRule.HuntersLedger when openingAttack =>
                    instance.State.GetCounter(GearCounterKeys.HuntersLedgerEliteKills),
                _ => 0
            };
            if (amount > 0) trace.Add(instance, EffectCue.AttackBonus, amount);
        }
    }

    /// <summary>Notched Pistol (Hunter's Ledger): the opening attack of an encounter gains its kills.</summary>
    public static int OpeningAttackBonus(CardManager cards)
    {
        int bonus = 0;
        foreach (var (gear, instance) in Owned(cards))
            if (gear.specialRule == GearSpecialRule.HuntersLedger)
                bonus += instance.State.GetCounter(GearCounterKeys.HuntersLedgerEliteKills);
        return bonus;
    }

    /// <summary>Headhunter Rifle (Trophy Rack): every attack in an Enforcer or Headliner encounter
    /// gains its per-kill damage for each Enforcer or Headliner defeated.</summary>
    public static int HeadhunterAttackBonus(CardManager cards, bool eliteOrHeadlinerEncounter)
    {
        if (!eliteOrHeadlinerEncounter) return 0;
        long bonus = 0;
        foreach (var (gear, instance) in Owned(cards))
            if (gear.specialRule == GearSpecialRule.TrophyRack)
                bonus += (long)instance.State.GetCounter(GearCounterKeys.TrophyRackEliteKills) * Tuning.headhunterRifleAttackPerKill;
        return ClampToInt(bonus);
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
                !committedCards.Any(card => card != null && card.RanksAbove(source.Rank))) continue;
            int growth = Tuning.lowballEpicPermanentAttack;
            source.GainPermanentAttackBonus(growth);
            cards.NotifyCardInstanceChanged(source);
            instance.State.SetCounter(GearCounterKeys.DwarfEpicPermanentGained,
                ClampToInt((long)instance.State.GetCounter(GearCounterKeys.DwarfEpicPermanentGained) + growth));
            instance.PendingDwarfSourceCard = null;
            log?.Invoke($"{gear.displayName}: prepared {source.DisplayName} permanently gained +{growth} attack.");
            EffectTriggerFeed.Report(instance, EffectCue.Growth, growth);
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
            // The physical Ace, or the Joker that stood in for it.
            var ace = committedCards.FirstOrDefault(card => card != null && card.Rank == CardData.Rank.Ace) ??
                committedCards.FirstOrDefault(card => card != null && card.IsWildRank);
            if (ace == null || !cards.TryTakeCardToHand(ace)) continue;
            state.SetEncounterCounter(GearCounterKeys.AceReturnUsed, 1);
            log?.Invoke($"{gear.displayName}: returned the physical Ace to hand.");
            EffectTriggerFeed.Report(instance, EffectCue.ReturnToHand, 1);
            return;
        }
    }

    /// <summary>Bounty Hologram scales won Enforcer and Headliner Credits (GameBalance percents).</summary>
    public static int ScaleVictoryCredits(CardManager cards, int credits, bool headlinerEncounter,
        bool eliteOrHeadlinerEncounter)
    {
        if (cards == null || !cards.HasGearSpecialRule(GearSpecialRule.BountyLedger)) return credits;
        int numerator = headlinerEncounter ? Tuning.bountyHologramHeadlinerPercent
            : eliteOrHeadlinerEncounter ? Tuning.bountyHologramEnforcerPercent : 100;
        return (int)Math.Min(int.MaxValue, ((long)credits * numerator + 50) / 100);
    }

    /// <summary>Reports Bounty Hologram's share of a won encounter's Credits.</summary>
    public static void ReportVictoryCreditsScaling(CardManager cards, int baseCredits, int scaledCredits)
    {
        if (scaledCredits > baseCredits) ReportFirst(cards, GearSpecialRule.BountyLedger, EffectCue.Credits,
            scaledCredits - baseCredits);
    }
}
