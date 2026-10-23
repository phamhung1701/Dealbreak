using UnityEngine;

[CreateAssetMenu(fileName = "CardEnhancement", menuName = "Game/Mod Data")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "CardEnhancementData")]
public sealed class CardEnhancementData : ScriptableObject
{
    public string id;
    public string displayName;
    public string icon;
    [TextArea] public string description;
    [Min(0)] public int price;

    [Header("Canonical Content Metadata")]
    public string canonicalId;
    public string rarity = "Common";
    [Min(1)] public int tier = 1;
    public string upgradeFromId;

    [Header("Typed Gameplay Effects")]
    public GameplayEffectDefinition[] effects = System.Array.Empty<GameplayEffectDefinition>();
    public GameplayRuleModifierData[] ruleModifiers = System.Array.Empty<GameplayRuleModifierData>();

    [Header("Signature (played instead while Overclock is owned; see ModSignature)")]
    public string signatureName;
    [TextArea] public string signatureDescription;
    public GameplayEffectDefinition[] signatureEffects = System.Array.Empty<GameplayEffectDefinition>();
    public bool HasSignature => !string.IsNullOrEmpty(signatureName) && signatureEffects != null && signatureEffects.Length > 0;

    [Header("Presentation")]
    [Tooltip("This Mod's own look on a card that carries it (CardModFxUI).")]
    public CardModVisual visual;
}

/// <summary>A Mod's ambient look on a card that carries it. Serialized by value: append, never renumber.</summary>
public enum CardModVisual
{
    None = 0,
    EchoFrame = 1,
    BoostChevrons = 2,
    Reticle = 3,
    TwinSlash = 4,
    Overheat = 5,
    ArmorPlates = 6,
    ClawMarks = 7,
    CreditChips = 8,
    LoadBar = 9,
    HealPulse = 10,
    Shockwave = 11,
    SuitCycle = 12,
}
