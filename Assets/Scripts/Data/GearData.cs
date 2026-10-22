using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "ArtifactCapacityCategory")]
public enum GearCapacityCategory
{
    Persistent = 0,
    NonSlot = 1
}

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "ArtifactSpecialRule")]
public enum GearSpecialRule
{
    None = 0,
    RareClub = 1,
    RareHeart = 2,
    RareDiamond = 3,
    RareArsenal = 4,
    GlassCommon = 5,
    GlassRare = 6,
    GlassEpic = 7,
    RareRetaliation = 8,
    RareHands = 9,
    RoyalFamilyHeirloom = 10,
    OverflowCommon = 11,
    OverflowRare = 12,
    ClubEpic = 13,
    OverflowEpic = 14,
    ChallengerCrest = 15,
    BountyLedger = 16,
    GildedBlade = 17,
    TravelerPackCommon = 18,
    TravelerPackRare = 19,
    PreparationManual = 20,
    ScavengersPouch = 21,
    ScavengersSatchel = 22,
    CashbackToken = 23,
    GoldenVault = 24,
    MerchantsBadge = 25,
    MerchantsGift = 26,
    AlchemistsKit = 27,
    FieldMedicsKit = 28,
    RetaliationEpic = 29,
    CrownOfEndurance = 30,
    HuntersLedger = 31,
    TrophyRack = 32,
    DwarfRare = 33,
    DwarfEpic = 34,
    BalancersScale = 35,
    KingslayersMark = 36,
    AceRare = 37,
    AceEpic = 38,
    DiamondEpic = 39,
    Hammer = 40,
    ArsenalEpic = 41,
    HeartEpic = 42,
    MimicEpic = 43,
    HandsEpic = 44,
    SpadeRare = 45,
    SpadeEpic = 46,
    HiddenTrail = 47,
    WanderersBoots = 48,
    WarpathBanner = 49,
    // 50 was MasterThief, an enemy ability listed here by mistake; keep the value retired.
    Dealbreaker = 51
}

[CreateAssetMenu(fileName = "Gear", menuName = "Game/Gear Data")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "RelicData")]
public class GearData : ScriptableObject
{
    public string id;
    public string displayName;
    public string icon;
    [Tooltip("Optional authored Gear icon. The existing icon text is used when this is unassigned.")]
    public Sprite iconSprite;
    [TextArea] public string description;
    [Min(0)] public int price;

    [Header("Capacity")]
    // Existing authored Gear defaults to Persistent; future temporary/system/quest
    // Gear may be marked NonSlot without relying on names or creating a new inventory.
    public GearCapacityCategory capacityCategory = GearCapacityCategory.Persistent;
    public bool OccupiesCapacitySlot => capacityCategory == GearCapacityCategory.Persistent;

    [Header("Canonical Content Metadata")]
    public string canonicalId;
    public string rarity = "Common";
    [Min(1)] public int tier = 1;
    public string upgradeFromId;

    [Header("Typed Gameplay Effects")]
    public GameplayEffectDefinition[] effects = System.Array.Empty<GameplayEffectDefinition>();
    public GameplayRuleModifierData[] ruleModifiers = System.Array.Empty<GameplayRuleModifierData>();
    public GearSpecialRule specialRule;

    public int ModifyAttack(CardInstance card, int currentDamage) =>
        GameplayEffectResolver.ModifyWithSingleGear(this, card, currentDamage, false);

    public int ModifyDefense(CardInstance card, int currentDefense) =>
        GameplayEffectResolver.ModifyWithSingleGear(this, card, currentDefense, true);
}
