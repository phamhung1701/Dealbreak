using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Dealbreak/Save Content Catalog")]
public sealed class SaveContentCatalog : ScriptableObject
{
    [UnityEngine.Serialization.FormerlySerializedAs("artifacts")]
    public GearData[] gearItems = Array.Empty<GearData>();
    public CardEnhancementData[] enhancements = Array.Empty<CardEnhancementData>();
    public ConsumableData[] consumables = Array.Empty<ConsumableData>();
    public RunContentCatalog runContent;
    public EnemyTypeData[] fallbackEnemies = Array.Empty<EnemyTypeData>();
    [Tooltip("Playable characters in their character-select order; the first is the default look.")]
    public CharacterData[] characters = Array.Empty<CharacterData>();
    public static SaveContentCatalog Load() => Resources.Load<SaveContentCatalog>("RunSaveContent");
    public CharacterData Character(string id) => string.IsNullOrEmpty(id) ? null :
        characters.FirstOrDefault(x => x != null && x.id == id);
    public GearData Gear(string id) => gearItems.FirstOrDefault(x => x != null && x.id == id);
    public CardEnhancementData Enhancement(string id) => enhancements.FirstOrDefault(x => x != null && x.id == id);
    public ConsumableData Consumable(string id) => consumables.FirstOrDefault(x => x != null && x.id == id);
    public RunEventDefinition Event(string id, MapNodeType kind) => runContent != null
        ? runContent.GetEvents(kind)?.FirstOrDefault(x => x != null && x.id == id) : null;
    public bool HasEnemy(string id) => fallbackEnemies.Any(x => x != null && x.name == id) ||
        runContent != null && ((runContent.normalEnemies?.Any(x => x != null && x.name == id) ?? false) ||
        (runContent.eliteEnemies?.Any(x => x != null && x.name == id) ?? false));
}
