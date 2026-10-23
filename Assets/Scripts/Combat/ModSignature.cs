/// <summary>
/// Overclock (<see cref="GearSpecialRule.SignatureMods"/>): while it is owned, every modded card plays
/// its Mod's Signature version: the Signature effects, name and text replace the normal ones. The card
/// keeps the same Mod, so nothing is saved; owning the Gear is the whole switch. Every reader of a
/// card's Mod effects, name or text goes through here, so previews and execution always agree.
/// </summary>
public static class ModSignature
{
    public static bool Active(CardManager cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.ownedGear.Count; i++)
            if (cards.ownedGear[i] != null && cards.ownedGear[i].specialRule == GearSpecialRule.SignatureMods) return true;
        return false;
    }

    static bool Plays(CardEnhancementData mod) => mod != null && mod.HasSignature && Active(CardManager.Instance);

    /// <summary>The effects a card with <paramref name="mod"/> plays right now.</summary>
    public static GameplayEffectDefinition[] Effects(CardEnhancementData mod) =>
        mod == null ? null : Plays(mod) ? mod.signatureEffects : mod.effects;

    public static string Name(CardEnhancementData mod) =>
        mod == null ? null : Plays(mod) ? mod.signatureName : mod.displayName;

    public static string Description(CardEnhancementData mod) =>
        mod == null ? null : Plays(mod) ? mod.signatureDescription : mod.description;
}

/// <summary>
/// Encounter counters a Signature Mod keeps on its card's <see cref="CardInstance.EffectState"/>. They are
/// negative so they never meet an effect or rule index, and they are save data: never renumber them.
/// </summary>
public static class ModCounterKeys
{
    // Transfusion: 1 once its card has turned healing past full HP into Shield this fight.
    public const int TransfusionShieldGranted = -1001;
    // Bulwark: the enemy turn (completed player turns + 1) in which its card last returned to hand.
    public const int BulwarkReturnedTurn = -1002;
}
