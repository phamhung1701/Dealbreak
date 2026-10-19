// Build-capacity and acquisition Gear rules queried by CardManager.
public static partial class GearRules
{
    /// <summary>Traveler's Pack (any tier) widens the backpack to five slots.</summary>
    public static int BackpackCapacity(CardManager cards) =>
        cards != null && (cards.HasGearSpecialRule(GearSpecialRule.TravelerPackCommon) ||
            cards.HasGearSpecialRule(GearSpecialRule.TravelerPackRare))
            ? CardManager.TRAVELER_BACKPACK_CAPACITY : CardManager.BASE_BACKPACK_CAPACITY;

    /// <summary>Traveler's Pack Rare stacks up to three identical Tricks per slot.</summary>
    public static int ConsumablesPerSlot(CardManager cards) =>
        cards != null && cards.HasGearSpecialRule(GearSpecialRule.TravelerPackRare)
            ? CardManager.RARE_CONSUMABLES_PER_SLOT : 1;

    /// <summary>Overflow Gear lets draws exceed the hand limit; the extra cards auto-play.</summary>
    public static int OverflowCapacity(CardManager cards)
    {
        int capacity = 0;
        if (cards == null) return capacity;
        for (int i = 0; i < cards.ownedGear.Count; i++)
        {
            var rule = cards.ownedGear[i]?.specialRule;
            if (rule is GearSpecialRule.OverflowRare or GearSpecialRule.OverflowEpic) return 3;
            if (rule == GearSpecialRule.OverflowCommon) capacity = 2;
        }
        return capacity;
    }

    /// <summary>The first Trick used in an encounter: Scavenger's Pouch draws, Field Medic's Kit heals.</summary>
    public static void OnFirstConsumableUsedInEncounter(CardManager cards, CombatManager combat)
    {
        foreach (var (gear, _) in Owned(cards))
        {
            if (gear.specialRule == GearSpecialRule.ScavengersPouch)
                cards.DrawToHand(1);
            else if (gear.specialRule == GearSpecialRule.FieldMedicsKit)
                combat?.HealPlayer(3);
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
