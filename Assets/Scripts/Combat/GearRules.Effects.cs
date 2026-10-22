using System;
using System.Collections.Generic;
using System.Linq;

// Gear rules evaluated inside GameplayEffectResolver's calculations and reaction enqueueing.
public static partial class GearRules
{
    static int ClampToInt(long value) => (int)Math.Min(Math.Max(value, 0L), int.MaxValue);

    // ---- Attack and defense values --------------------------------------------------------

    /// <summary>
    /// Final attack flats, then final multipliers, applied after typed effects (Club, Overflow, Glass).
    /// Club Rare doubles "after flat bonuses", so every flat lands before any multiplier whatever the
    /// order the Gear was acquired in.
    /// </summary>
    public static int ApplyFinalAttackRules(CardManager cards, CardInstance card, int resolved,
        int unplayedClubCount, int overflowHandCount)
    {
        var combat = CombatManager.Instance;
        foreach (var (gear, _) in Owned(cards))
        {
            switch (gear.specialRule)
            {
                case GearSpecialRule.OverflowEpic when combat?.IsResolvingOverflowAttack == true:
                    resolved = ClampToInt((long)resolved + Math.Max(0, overflowHandCount));
                    break;
                case GearSpecialRule.GlassCommon:
                    resolved = ClampToInt((long)resolved + Tuning.glassShardAttackBonus);
                    break;
            }
        }
        foreach (var (gear, _) in Owned(cards))
        {
            switch (gear.specialRule)
            {
                case GearSpecialRule.ClubEpic when card.MatchesSuit(CardData.Suit.Clubs):
                {
                    long numerator = 1, denominator = 1;
                    for (int clubIndex = 0; clubIndex < unplayedClubCount; clubIndex++)
                    {
                        numerator *= Tuning.clubAmplifierEpicNumerator;
                        denominator *= Tuning.clubAmplifierEpicDenominator;
                    }
                    resolved = ClampToInt((long)resolved * numerator / denominator);
                    break;
                }
                case GearSpecialRule.RareClub when card.MatchesSuit(CardData.Suit.Clubs):
                    resolved = ClampToInt((long)resolved * Tuning.clubAmplifierRareMultiplier);
                    break;
                case GearSpecialRule.GlassRare:
                    resolved = ClampToInt((long)resolved * Tuning.glassShardRareAttackMultiplier);
                    break;
                case GearSpecialRule.GlassEpic when combat?.player != null &&
                    combat.player.currentHealth <= Tuning.glassShardEpicAtOrBelowHealth:
                    resolved = ClampToInt((long)resolved * Tuning.glassShardEpicAttackMultiplier);
                    break;
            }
        }
        return resolved;
    }

    /// <summary>Opening-attack bonuses (Credits, counters, primed action bonuses) apply only while
    /// a Gear that grants them is owned.</summary>
    public static bool GrantsOpeningAttackBonus(CardManager cards) =>
        cards != null && cards.ownedGear.Any(gear => gear != null &&
            gear.specialRule is GearSpecialRule.GildedBlade or GearSpecialRule.HuntersLedger or
                GearSpecialRule.TrophyRack or GearSpecialRule.KingslayersMark or
                GearSpecialRule.DwarfRare or GearSpecialRule.DwarfEpic or
                GearSpecialRule.BalancersScale or GearSpecialRule.SpadeEpic or GearSpecialRule.HeartEpic);

    /// <summary>Gold Pistol: every attack gains 1 damage per 10 Credits held.</summary>
    public static int GoldPistolBonus(CardManager cards) =>
        cards != null && cards.HasGearSpecialRule(GearSpecialRule.GildedBlade)
            ? Math.Max(0, cards.credits / Tuning.goldPistolCreditsPerDamage) : 0;

    /// <summary>Mimic Epic also repeats in-hand effects of cards matching the committed rank.</summary>
    public static bool IsMimicEpic(GearData gear) => gear != null && gear.specialRule == GearSpecialRule.MimicEpic;

    public static bool OwnsMimicEpic(CardManager cards) =>
        cards != null && cards.ownedGear.Any(IsMimicEpic);

    /// <summary>Ace Epic: an Ace committed with a face card is always critical.</summary>
    public static bool GuaranteesCritical(CardManager cards, IReadOnlyList<CardInstance> committedCards) =>
        cards != null && cards.HasGearSpecialRule(GearSpecialRule.AceEpic) &&
        committedCards.Any(card => card?.Rank == CardData.Rank.Ace) &&
        committedCards.Any(card => card?.Rank is CardData.Rank.Jack or CardData.Rank.Queen or CardData.Rank.King);

    /// <summary>The live state an owned Gear shows under its description ("Currently +3."), or empty.</summary>
    public static string LiveStatus(CardManager cards, GearData gear, GearRuntimeInstance instance)
    {
        if (cards == null || gear == null || instance == null) return string.Empty;
        var state = instance.State;
        switch (gear.specialRule)
        {
            case GearSpecialRule.RetaliationEpic:
                return $"Currently +{state.GetCounter(GearCounterKeys.RetaliationEpicGrowth)}.";
            case GearSpecialRule.DwarfEpic:
                return $"Currently +{state.GetCounter(GearCounterKeys.DwarfEpicPermanentGained)}.";
            case GearSpecialRule.ArsenalEpic:
            {
                int faces = cards.hand.Count(card => card?.Rank is CardData.Rank.Jack or CardData.Rank.Queen or CardData.Rank.King);
                int bonus = HandCapacityBonus(cards, gear);
                int next = faces + 1;
                double harmonic = 0d;
                for (int faceIndex = 1; faceIndex <= next; faceIndex++) harmonic += 1d / faceIndex;
                while (Math.Floor(harmonic) <= bonus) harmonic += 1d / ++next;
                return $"Currently +{bonus} hand size; next at {next} face cards.";
            }
            default:
                return string.Empty;
        }
    }

    /// <summary>An owned Gear's description with its live state.</summary>
    public static string OwnedDescription(CardManager cards, GearData gear, long instanceId)
    {
        if (gear == null) return string.Empty;
        string live = LiveStatus(cards, gear, cards?.GetGearInstanceById(instanceId));
        return string.IsNullOrEmpty(live) ? gear.description : $"{gear.description}\n{live}";
    }

    /// <summary>Royal Family Heirloom enables the five-card Royal Family action.</summary>
    public static bool EnablesRoyalFamily(CardManager cards) => cards != null &&
        cards.ownedGear.Any(gear => gear != null && gear.specialRule == GearSpecialRule.RoyalFamilyHeirloom);

    /// <summary>Nuclear Option: the most cards one attack may commit, or 0 without it.</summary>
    public static int NuclearOptionMaxCards(CardManager cards) =>
        EnablesRoyalFamily(cards) ? Tuning.nuclearOptionMaxCards : 0;

    /// <summary>Memory Card hand size: Rare +3; Epic adds floor(H(face cards held)).</summary>
    public static int HandCapacityBonus(CardManager cards, GearData gear)
    {
        if (gear.specialRule == GearSpecialRule.RareArsenal) return Tuning.memoryCardRareHandBonus;
        if (gear.specialRule != GearSpecialRule.ArsenalEpic) return 0;
        int faces = cards.hand.Count(card => card?.Rank is CardData.Rank.Jack or CardData.Rank.Queen or CardData.Rank.King);
        double harmonic = 0d;
        for (int faceIndex = 1; faceIndex <= faces; faceIndex++) harmonic += 1d / faceIndex;
        return (int)Math.Floor(harmonic);
    }

    /// <summary>
    /// Dealbreaker: once per combat, an enemy hit that would be lethal destroys one random card from
    /// the hand and is negated. The card is picked by card id, not hand order, from
    /// <paramref name="random"/>. With an empty hand nothing happens and the charge is kept.
    /// </summary>
    public static bool TryDealbreakerSave(CardManager cards, Func<IRandomSource> random, Action<string> log)
    {
        if (cards == null || cards.hand.Count == 0) return false;
        foreach (var (gear, instance) in Owned(cards))
        {
            if (gear.specialRule != GearSpecialRule.Dealbreaker || instance == null ||
                instance.State.GetEncounterCounter(GearCounterKeys.DealbreakerUsed) != 0) continue;
            var candidates = cards.hand.Where(card => card != null).OrderBy(card => card.Id).ToArray();
            if (candidates.Length == 0) return false;
            var source = random?.Invoke();
            var destroyed = candidates[source != null ? source.NextInt(0, candidates.Length) : 0];
            if (!cards.DestroyOwnedCard(destroyed)) return false;
            instance.State.SetEncounterCounter(GearCounterKeys.DealbreakerUsed, 1);
            log?.Invoke($"{gear.displayName}: destroyed {destroyed.DisplayName} to negate a lethal hit.");
            return true;
        }
        return false;
    }

    /// <summary>Penalties on aggregate enemy retaliation: Glass Shard (+5 or ×2), then Memory Card (×1.5).</summary>
    public static int ApplyAggregateIncomingRules(CardManager cards, int damage)
    {
        bool arsenal = false, glassCommon = false, glassRare = false;
        foreach (var (gear, _) in Owned(cards))
        {
            arsenal |= gear.specialRule == GearSpecialRule.RareArsenal;
            glassCommon |= gear.specialRule == GearSpecialRule.GlassCommon;
            glassRare |= gear.specialRule == GearSpecialRule.GlassRare;
        }
        if (glassCommon) damage = ClampToInt((long)damage + Tuning.glassShardIncomingBonus);
        else if (glassRare) damage = ClampToInt((long)damage * Tuning.glassShardRareIncomingMultiplier);
        if (arsenal) damage = ClampToInt(((long)damage * Tuning.memoryCardRareIncomingPercent + 50) / 100);
        return damage;
    }

    // ---- Route ----------------------------------------------------------------------------

    /// <summary>Hidden Trail rerolls one reachable hidden node once per District.</summary>
    public static bool ApplyMapReadyRules(GearData gear, GameplayEffectState state, MapReadyEffectContext context)
    {
        if (gear.specialRule != GearSpecialRule.HiddenTrail ||
            state.GetCounter(GearCounterKeys.HiddenTrailRerolledMap) == context.MapIndex + 1 ||
            !context.Run.TryRerollEligibleHiddenNode()) return false;
        state.SetCounter(GearCounterKeys.HiddenTrailRerolledMap, context.MapIndex + 1);
        return true;
    }

    // ---- Reactions ------------------------------------------------------------------------

    /// <summary>Heart Gear heals (and Epic banks overheal); Diamond Gear draws two. Both run after
    /// the Gear's typed committed effects in the same source position.</summary>
    public static void EnqueueCardCommittedRules(GearData gear, GearRuntimeInstance gearInstance, int sourceOrder,
        CardCommittedEffectContext context, CombatReactionQueue queue)
    {
        if (gear.specialRule is GearSpecialRule.RareHeart or GearSpecialRule.HeartEpic &&
            context.Card.MatchesSuit(CardData.Suit.Hearts))
            queue.Enqueue(context.Action.ActionId, context.HitIndex, CombatReactionPhase.CardCommitted,
                CombatReactionSourceCategory.Gear, sourceOrder, int.MaxValue - 1,
                () =>
                {
                    int before = context.Combat.player.currentHealth;
                    int heal = Tuning.heartMedsHeal;
                    int healed = context.Combat.HealPlayer(heal);
                    if (before * 2 < context.Combat.player.maxHealth) context.Cards.AddCredits(Tuning.heartMedsLowHealthCredits);
                    if (gear.specialRule == GearSpecialRule.HeartEpic && context.Combat.CanPlayerHeal())
                    {
                        var state = gearInstance.State;
                        int overheal = Math.Max(0, heal - healed);
                        state.SetCounter(GearCounterKeys.HeartEpicStoredOverheal, Math.Min(Tuning.heartMedsEpicOverhealCap,
                            state.GetCounter(GearCounterKeys.HeartEpicStoredOverheal) + overheal));
                    }
                });
        if (gear.specialRule is GearSpecialRule.RareDiamond or GearSpecialRule.DiamondEpic &&
            context.Card.MatchesSuit(CardData.Suit.Diamonds))
            queue.Enqueue(context.Action.ActionId, context.HitIndex, CombatReactionPhase.CardCommitted,
                CombatReactionSourceCategory.Gear, sourceOrder, int.MaxValue,
                () => context.Cards.DrawToHand(Tuning.diamondDriverDraw));
    }

    /// <summary>Spade Shield Rare and Epic: a Spade that blocks more than its attack still counts as
    /// a Perfect Block.</summary>
    public static bool SpadeOverblockCountsAsPerfect(CardManager cards, CardInstance blockingCard) =>
        blockingCard != null && blockingCard.MatchesSuit(CardData.Suit.Spades) && cards != null &&
        (cards.HasGearSpecialRule(GearSpecialRule.SpadeRare) || cards.HasGearSpecialRule(GearSpecialRule.SpadeEpic));

    /// <summary>A blocking card that exactly covers its attack is a Perfect Block, and so is a Spade
    /// over-block with Spade Shield Rare or Epic.</summary>
    public static bool IsPerfectCardBlock(CardManager cards, CardInstance blockingCard, int block, int attack) =>
        attack > 0 && (block == attack || block > attack && SpadeOverblockCountsAsPerfect(cards, blockingCard));

    /// <summary>Spade Shield Epic banks the over-blocked damage for the next attack; Counterpunch
    /// Rare and Epic counterattack the blocked enemy for the blocked damage, more on a Perfect Block
    /// (Epic adds its growth).</summary>
    public static void EnqueueAttackBlockedRules(GameplayEffectSource source, AttackBlockedEffectContext context,
        CombatReactionQueue queue, int sourceOrder)
    {
        var rule = source.Gear?.Definition.specialRule;
        if (rule == GearSpecialRule.SpadeEpic && context.BlockingCard.MatchesSuit(CardData.Suit.Spades))
        {
            int block = context.Combat.CalculateCardDefense(context.BlockingCard);
            int attack = Math.Max(0, context.BlockedAttackDamage);
            if (attack > 0 && block > attack)
                source.State.SetEncounterCounter(GearCounterKeys.SpadeEpicOverblocks, ClampToInt(
                    (long)source.State.GetEncounterCounter(GearCounterKeys.SpadeEpicOverblocks) + block - attack));
        }
        if (rule is GearSpecialRule.RareRetaliation or GearSpecialRule.RetaliationEpic)
        {
            int block = context.Combat.CalculateCardDefense(context.BlockingCard);
            int attack = Math.Max(0, context.BlockedAttackDamage);
            bool perfect = IsPerfectCardBlock(context.Cards, context.BlockingCard, block, attack);
            int perfectPercent = rule == GearSpecialRule.RetaliationEpic
                ? Tuning.counterpunchGlovesEpicPerfectPercent : Tuning.counterpunchGlovesRarePerfectPercent;
            int retaliation = GameplayEffectResolver.CalculateRetaliationDamage(block, attack, perfect ? perfectPercent : 100);
            if (retaliation > 0 && rule == GearSpecialRule.RetaliationEpic)
                retaliation = ClampToInt((long)retaliation + source.State.GetCounter(GearCounterKeys.RetaliationEpicGrowth));
            if (retaliation > 0)
                queue.Enqueue(context.Action.ActionId, int.MinValue + context.BlockOrder,
                    CombatReactionPhase.AttackBlocked, source.Category, sourceOrder, int.MaxValue,
                    () => context.Combat.QueueReactiveDamage(context.Combat.player, context.AttackedEnemy,
                        retaliation, sourceOrder, int.MaxValue, sourceCategory: source.Category));
        }
    }
}
