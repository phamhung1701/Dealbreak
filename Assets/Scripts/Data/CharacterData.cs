using UnityEngine;

/// <summary>
/// A playable character, picked before a run (Balatro-deck style). Its perks are typed data:
/// the start-of-run ones apply once in <c>RunManager.StartRunWithSeed</c>, the capacity ones are
/// read live by <c>CardManager</c>. The saved identity is <see cref="id"/>.
/// </summary>
[CreateAssetMenu(fileName = "Character", menuName = "Game/Character")]
public sealed class CharacterData : ScriptableObject
{
    public string id;
    public string displayName;
    [Tooltip("One short sentence naming the perk; it quotes the numbers below.")]
    [TextArea] public string description;
    public Sprite portrait;

    [Header("Start of run")]
    [Min(0)] public int maxHealthBonus;
    [Min(0)] public int startingCredits;
    [Tooltip("Rarity of one seeded-random standalone Gear the run starts with; empty for none.")]
    public string startingGearRarity;

    [Header("Capacity")]
    [Min(0)] public int handSizeBonus;
    [Min(0)] public int gearSlotBonus;
    [Min(0)] public int trickSlotBonus;
}
