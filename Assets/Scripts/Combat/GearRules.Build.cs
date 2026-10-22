// Build-capacity and acquisition Gear rules queried by CardManager.
public static partial class GearRules
{
    /// <summary>Cargo Case (any tier) widens the backpack to five slots.</summary>
    public static int BackpackCapacity(CardManager cards) =>
        cards != null && (cards.HasGearSpecialRule(GearSpecialRule.TravelerPackCommon) ||
            cards.HasGearSpecialRule(GearSpecialRule.TravelerPackRare))
            ? Tuning.cargoCaseBackpackSlots : CardManager.BASE_BACKPACK_CAPACITY;

    /// <summary>Cargo Case Rare stacks up to three identical Tricks per slot.</summary>
    public static int ConsumablesPerSlot(CardManager cards) =>
        cards != null && cards.HasGearSpecialRule(GearSpecialRule.TravelerPackRare)
            ? Tuning.cargoCaseRareTricksPerSlot : 1;

    /// <summary>Overflow Gear lets draws exceed the hand limit; the extra cards auto-play.</summary>
    public static int OverflowCapacity(CardManager cards)
    {
        int capacity = 0;
        if (cards == null) return capacity;
        for (int i = 0; i < cards.ownedGear.Count; i++)
        {
            var rule = cards.ownedGear[i]?.specialRule;
            if (rule is GearSpecialRule.OverflowRare or GearSpecialRule.OverflowEpic) return Tuning.overflowCanisterRareCapacity;
            if (rule == GearSpecialRule.OverflowCommon) capacity = Tuning.overflowCanisterCapacity;
        }
        return capacity;
    }

    /// <summary>The first Trick used in an encounter: Grappling Hook draws, First-Aid Kit heals.</summary>
    public static void OnFirstConsumableUsedInEncounter(CardManager cards, CombatManager combat)
    {
        foreach (var (gear, instance) in Owned(cards))
        {
            if (gear.specialRule == GearSpecialRule.ScavengersPouch)
            {
                int drawn = cards.DrawToHand(Tuning.grapplingHookDraw);
                if (drawn > 0) EffectTriggerFeed.Report(instance, EffectCue.Draw, drawn);
            }
            else if (gear.specialRule == GearSpecialRule.FieldMedicsKit)
            {
                int healed = combat != null ? combat.HealPlayer(Tuning.firstAidKitHeal) : 0;
                if (healed > 0) EffectTriggerFeed.Report(instance, EffectCue.Heal, healed);
            }
        }
    }

    /// <summary>Initializes rule state for newly acquired Gear.</summary>
    /// <returns>True when the Gear changes backpack capacity or stacking.</returns>
    public static bool OnAcquired(GearRuntimeInstance instance, int completedNodeCount)
    {
        var gear = instance.Definition;
        if (gear.specialRule == GearSpecialRule.Hammer)
            instance.State.SetCounter(GearCounterKeys.HammerLastRewardNode, completedNodeCount);
        return gear.specialRule is GearSpecialRule.TravelerPackCommon or GearSpecialRule.TravelerPackRare;
    }
}
