using System;
using UnityEngine;

[Serializable]
public sealed class CardInstance
{
    [SerializeField] CardData definition;
    [SerializeField] CardEnhancementData enhancement;
    [SerializeField] bool hasSuitOverride;
    [SerializeField] CardData.Suit suitOverride;
    [SerializeField] int id;

    public CardInstance(CardData definition, int id)
    {
        this.definition = definition != null
            ? definition
            : throw new ArgumentNullException(nameof(definition));
        this.id = id;
    }

    public CardData Definition => definition;
    public CardEnhancementData Enhancement => enhancement;
    public int Id => id;

    public CardData.Suit Suit => hasSuitOverride ? suitOverride : definition.suit;
    public bool HasSuitOverride => hasSuitOverride;
    public CardData.Rank Rank => definition.rank;

    // Compatibility forwards for existing gameplay code while it moves to the model API.
    public CardData.Suit suit => Suit;
    public CardData.Rank rank => Rank;

    public GameplayEffectState EffectState { get; } = new();
    public int PermanentAttackBonus { get; private set; }

    public bool MatchesSuit(CardData.Suit suit) =>
        Suit == suit || GameplayEffectResolver.HasEnhancementEffect(this, GameplayEffectKind.WildSuit);

    /// <summary>Joker: counts as any rank where cards group (Ace pairs, Hands sets, the Royal Flush).</summary>
    public bool IsWildRank => GameplayEffectResolver.HasEnhancementEffect(this, GameplayEffectKind.WildRank);

    public bool MatchesRank(CardData.Rank rank) => Rank == rank || IsWildRank;

    public void GainPermanentAttackBonus(int amount = 1)
    {
        if (amount > 0) PermanentAttackBonus = (int)Math.Min(int.MaxValue, (long)PermanentAttackBonus + amount);
    }

    public int BaseAttackValue => definition.AttackValue;
    public int AttackValue => Mathf.Max(0, BaseAttackValue + GameplayEffectResolver.CardLocalFlat(this, GameplayEffectKind.FlatAttack));
    public int DefenseValue => Mathf.Max(0, BaseAttackValue + GameplayEffectResolver.CardLocalFlat(this, GameplayEffectKind.FlatBlock));
    public string DisplayName => enhancement != null
        ? $"{definition.RankLabel}{SuitSymbol} [{ModSignature.Name(enhancement)}]"
        : $"{definition.RankLabel}{SuitSymbol}";
    public string SuitSymbol => Suit switch
    {
        CardData.Suit.Hearts => "♥",
        CardData.Suit.Diamonds => "♦",
        CardData.Suit.Clubs => "♣",
        CardData.Suit.Spades => "♠",
        _ => ""
    };

    public bool TryChangeSuit(CardData.Suit suit)
    {
        if (!Enum.IsDefined(typeof(CardData.Suit), suit) || Suit == suit) return false;
        hasSuitOverride = suit != definition.suit;
        suitOverride = suit;
        return true;
    }

    public CardSaveData CaptureSave() => new()
    {
        id = id, rank = (int)Rank, definitionSuit = (int)definition.suit,
        effectiveSuit = (int)Suit, enhancementId = enhancement != null ? enhancement.id : null,
        permanentAttack = PermanentAttackBonus, effects = EffectState.CaptureSave()
    };

    public static CardInstance Restore(CardData definition, CardSaveData data, CardEnhancementData enhancement)
    {
        var card = new CardInstance(definition, data.id)
        {
            enhancement = enhancement, hasSuitOverride = data.effectiveSuit != data.definitionSuit,
            suitOverride = (CardData.Suit)data.effectiveSuit, PermanentAttackBonus = data.permanentAttack
        };
        card.EffectState.RestoreSave(data.effects);
        return card;
    }

    public CardInstance ClonePermanentState(int newId)
    {
        var copy = new CardInstance(definition, newId)
        {
            hasSuitOverride = hasSuitOverride,
            suitOverride = suitOverride,
            enhancement = enhancement,
            PermanentAttackBonus = PermanentAttackBonus
        };
        return copy;
    }

    public bool TryApplyEnhancement(CardEnhancementData value, bool replaceExisting = false)
    {
        if (value == null || ReferenceEquals(enhancement, value) || enhancement != null && !replaceExisting)
            return false;
        if (enhancement != null) EffectState.Clear();
        enhancement = value;
        return true;
    }
}
