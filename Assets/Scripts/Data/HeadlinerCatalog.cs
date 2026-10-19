using System;
using UnityEngine;

[Serializable]
public struct HeadlinerName
{
    public CardData.Rank rank;
    public CardData.Suit suit;
    public string displayName;
}

[Serializable]
public struct EnemyAbilityText
{
    public EnemyAbilityEffect effect;
    public string displayName;
    [TextArea] public string description;
}

/// <summary>
/// Headliner balance, player-facing identity text and generated-ability pools. Pool order is part of
/// seeded generation: reordering or inserting entries changes which ability a seed rolls.
/// </summary>
[CreateAssetMenu(fileName = "HeadlinerCatalog", menuName = "Dealbreak/Headliner Catalog")]
public sealed class HeadlinerCatalog : ScriptableObject
{
    [Header("Balance")]
    [Min(1)] public int maxHp = 30;
    [Min(0)] public int baseAttack = 6;
    [Tooltip("Maximum HP of the District 1 Headliner, before District scaling.")]
    [Min(1)] public int firstDistrictMaxHp = 20;
    [Min(0)] public int jackCredits = 10;
    [Min(0)] public int queenCredits = 15;
    [Min(0)] public int kingCredits = 20;

    [Header("Identity")]
    public HeadlinerName[] names = Array.Empty<HeadlinerName>();
    public EnemyAbilityText[] abilityText = Array.Empty<EnemyAbilityText>();

    [Header("Ability pools (order is seeded)")]
    public EnemyAbilityEffect[] jackAndQueenAbilities = Array.Empty<EnemyAbilityEffect>();
    public EnemyAbilityEffect[] kingHardCounters = Array.Empty<EnemyAbilityEffect>();
    public EnemyAbilityEffect[] kingHardCounterPartners = Array.Empty<EnemyAbilityEffect>();
    public EnemyAbilityEffect[] kingDefensiveAbilities = Array.Empty<EnemyAbilityEffect>();
    public EnemyAbilityEffect[] kingOffensiveAbilities = Array.Empty<EnemyAbilityEffect>();

    public static HeadlinerCatalog Load() => Resources.Load<HeadlinerCatalog>("HeadlinerCatalog");

    public int CreditsFor(CardData.Rank rank) => rank switch
    {
        CardData.Rank.Jack => jackCredits,
        CardData.Rank.Queen => queenCredits,
        _ => kingCredits
    };

    public string NameFor(CardData card)
    {
        if (card == null) return "Headliner";
        foreach (var entry in names)
            if (entry.rank == card.rank && entry.suit == card.suit) return entry.displayName;
        return "Headliner";
    }

    public string AbilityName(EnemyAbilityEffect effect)
    {
        foreach (var entry in abilityText)
            if (entry.effect == effect) return entry.displayName;
        return effect.ToString();
    }

    public string AbilityDescription(EnemyAbilityEffect effect)
    {
        foreach (var entry in abilityText)
            if (entry.effect == effect) return entry.description;
        return effect.ToString();
    }
}
