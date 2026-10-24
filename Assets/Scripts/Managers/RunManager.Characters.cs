using System;
using System.Linq;

// The character's start-of-run perks. Each applies once, when the run starts; the capacity perks
// (hand size, Gear and Trick slots) are read live by CardManager and saved with the character.
public partial class RunManager
{
    void ApplyCharacterMaxHealth(CharacterData character)
    {
        if (character == null || character.maxHealthBonus <= 0) return;
        CombatManager.Instance.IncreasePlayerMaxHealth(character.maxHealthBonus);
    }

    void ApplyCharacterStartingItems(CharacterData character)
    {
        if (character == null) return;
        var cards = CardManager.Instance;
        if (character.startingCredits > 0) cards.AddCredits(character.startingCredits);
        var gear = PickCharacterStartingGear(character, cards, _randomContext);
        if (gear != null) cards.GrantGearReward(gear);
    }

    /// <summary>
    /// One standalone Gear of the character's starting rarity, picked from the Gear catalog in id
    /// order by its own seeded stream, so it never shifts any other stream.
    /// </summary>
    internal static GearData PickCharacterStartingGear(CharacterData character, CardManager cards, RunRandomContext random)
    {
        if (character == null || string.IsNullOrWhiteSpace(character.startingGearRarity) || cards == null || random == null)
            return null;
        var pool = cards.gearCatalog.Where(gear => gear != null && !string.IsNullOrWhiteSpace(gear.id) &&
                gear.tier <= 1 && string.IsNullOrEmpty(gear.upgradeFromId) &&
                string.Equals(gear.rarity, character.startingGearRarity, StringComparison.OrdinalIgnoreCase))
            .OrderBy(gear => gear.id, StringComparer.Ordinal).ToList();
        return pool.Count == 0 ? null : pool[random.CreateStream("character-gear").NextInt(0, pool.Count)];
    }
}
