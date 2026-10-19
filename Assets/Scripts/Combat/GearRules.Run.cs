using System;

// Route, victory and economy Gear rules. RunManager owns the flows (reward queue, Hammer
// event, purchases) and asks these rules what applies.
public static partial class GearRules
{
    // ---- Encounter victory ----------------------------------------------------------------

    /// <summary>Victory Gear growth: Hunter's Ledger/Trophy Rack count Enforcer kills; Crown of
    /// Endurance grows max HP and Golden Vault pays interest after a Headliner.</summary>
    public static void OnEncounterWon(CardManager cards, CombatManager combat, bool wasElite, bool wasHeadliner)
    {
        if (cards == null) return;
        if (wasElite)
        {
            foreach (var (gear, instance) in Owned(cards))
            {
                var state = instance.State;
                if (gear.specialRule == GearSpecialRule.HuntersLedger)
                    state.SetCounter(GearCounterKeys.HuntersLedgerEliteKills,
                        state.GetCounter(GearCounterKeys.HuntersLedgerEliteKills) + 1);
                if (gear.specialRule == GearSpecialRule.TrophyRack)
                    state.SetCounter(GearCounterKeys.TrophyRackEliteKills,
                        state.GetCounter(GearCounterKeys.TrophyRackEliteKills) + 1);
            }
        }
        if (wasHeadliner)
        {
            foreach (var (gear, _) in Owned(cards))
            {
                if (gear.specialRule != GearSpecialRule.CrownOfEndurance) continue;
                int growth = 3 + ((combat?.EncounterActualHpLost ?? 0) == 0 ? 3 : 0);
                combat?.IncreasePlayerMaxHealth(growth, healIncrease: false);
            }
        }
        if (wasHeadliner && cards.HasGearSpecialRule(GearSpecialRule.GoldenVault))
        {
            int payout = RoundPercentage(cards.credits, 10);
            if (payout > 0) cards.AddCredits(payout);
        }
    }

    /// <summary>Scavenger's Satchel: every third combat win queues a Trick reward.</summary>
    public static bool CountsWinsForSatchelRewards(CardManager cards) =>
        cards != null && cards.HasGearSpecialRule(GearSpecialRule.ScavengersSatchel);

    // ---- Route ----------------------------------------------------------------------------

    /// <summary>Node-completion Gear: Wanderer's Boots reward three distinct node types per
    /// District once; Warpath Banner tracks the combat-win streak (max 3).</summary>
    public static void OnNodeCompleted(CardManager cards, CombatManager combat, PathNode node,
        int distinctVisitedNodeTypes)
    {
        if (cards == null || node == null) return;
        bool isCombatWin = node.kind is MapNodeType.Combat or MapNodeType.Elite or MapNodeType.Headliner;
        foreach (var (gear, instance) in Owned(cards))
        {
            var state = instance.State;
            if (gear.specialRule == GearSpecialRule.WanderersBoots && distinctVisitedNodeTypes >= 3 &&
                state.GetCounter(GearCounterKeys.WanderersBootsRewardedMap) != node.mapIndex + 1)
            {
                state.SetCounter(GearCounterKeys.WanderersBootsRewardedMap, node.mapIndex + 1);
                combat?.HealPlayer(3);
                cards.AddCredits(3);
            }
            if (gear.specialRule == GearSpecialRule.WarpathBanner)
                state.SetCounter(GearCounterKeys.WarpathBannerWinStreak, isCombatWin
                    ? Math.Min(3, state.GetCounter(GearCounterKeys.WarpathBannerWinStreak) + 1) : 0);
        }
    }

    /// <summary>Alchemist's Kit: each District start offers a cached Enhancement-Trick choice.</summary>
    public static bool OffersAlchemistRewards(CardManager cards) =>
        cards != null && cards.HasGearSpecialRule(GearSpecialRule.AlchemistsKit);

    /// <summary>The owned Hammer instance whose node counter drives Hammer rewards, if any.</summary>
    public static GearRuntimeInstance FindHammer(CardManager cards)
    {
        foreach (var (gear, instance) in Owned(cards))
            if (gear.specialRule == GearSpecialRule.Hammer) return instance;
        return null;
    }

    // ---- Black Market economy -------------------------------------------------------------

    /// <summary>Purchase Gear captured before a transaction, since the purchase may change it.</summary>
    public readonly struct PurchaseModifiers
    {
        public bool Cashback { get; }
        public bool MerchantsGift { get; }
        public PurchaseModifiers(bool cashback, bool merchantsGift)
        {
            Cashback = cashback;
            MerchantsGift = merchantsGift;
        }
    }

    public static PurchaseModifiers CapturePurchaseModifiers(CardManager cards) => new(
        cards != null && cards.HasGearSpecialRule(GearSpecialRule.CashbackToken),
        cards != null && cards.HasGearSpecialRule(GearSpecialRule.MerchantsGift));

    /// <summary>Cashback Token refunds 15% of the Credits actually spent on a paid item.</summary>
    public static int CashbackRefund(PurchaseModifiers modifiers, int actualCreditsSpent) =>
        modifiers.Cashback ? Math.Min(actualCreditsSpent, RoundPercentage(actualCreditsSpent, 15)) : 0;

    /// <summary>Merchant's Badge discounts eligible paid items (Gear, Mods, Tricks) to 80%.</summary>
    public static int DiscountedItemPrice(CardManager cards, int basePrice) =>
        cards != null && cards.HasGearSpecialRule(GearSpecialRule.MerchantsBadge)
            ? RoundPercentage(basePrice, 80) : basePrice;

    static int RoundPercentage(int value, int percent)
    {
        if (value <= 0 || percent <= 0) return 0;
        return (int)Math.Min(int.MaxValue, ((long)value * percent + 50L) / 100L);
    }
}
