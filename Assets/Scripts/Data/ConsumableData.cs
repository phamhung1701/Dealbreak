using UnityEngine;

public enum ConsumableEffectType
{
    Heal = 0,
    DirectEnemyDamage = 1,
    ApplyEnhancement = 2,
    DuplicateCard = 3,
    DestroyCards = 4,
    ChangeSuit = 5
}

[CreateAssetMenu(fileName = "Consumable", menuName = "Game/Trick")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "ConsumableData")]
public sealed class ConsumableData : ScriptableObject
{
    public string id;
    public string canonicalId;
    public string displayName;
    public string icon;
    [Tooltip("Optional authored Trick icon. The existing icon text is used when this is unassigned.")]
    public Sprite iconSprite;
    [TextArea] public string description;
    [Min(0)] public int price = 5;
    [Min(1)] public int uses = 1;
    public ConsumableEffectType effectType = ConsumableEffectType.Heal;
    [Min(0)] public int healAmount;
    [Min(0)] public int damageAmount;
    public CardEnhancementData enhancementToApply;
    [Tooltip("DuplicateCard: how many copies of the target card go into the hand.")]
    [Min(1)] public int copies = 1;
    public CardData.Suit targetSuit;
    public bool shopAvailable = true;
    public bool dropAvailable = true;
    public bool eventAvailable;
    [Range(0f, 1f)] public float dropChance = 0.1f;
}
