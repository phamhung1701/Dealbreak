using System;
using UnityEngine;

public enum ShopOfferKind
{
    Gear = 0,
    Enhancement = 1,
    Investment = 2,
    Guidance = 3,
    Consumable = 4,
    Contract = 5,
    Supply = 6
}

[Serializable]
public sealed class ShopOffer
{
    // Version-1 saves persist these prefixes. Never derive them from display/code names.
    public static string PersistentGoodsPrefix(ShopOfferKind kind) => kind switch
    {
        ShopOfferKind.Gear => "artifact",
        ShopOfferKind.Enhancement => "enhancement",
        ShopOfferKind.Consumable => "consumable",
        _ => kind.ToString().ToLowerInvariant()
    };

    public ShopOfferKind kind;
    [UnityEngine.Serialization.FormerlySerializedAs("artifact")]
    public GearData gear;
    public CardEnhancementData enhancement;
    public ConsumableData consumable;
    public int slot;
    public int basePrice;
    public int price;
    public int investmentStake;
    public int supplyQuantity = 1;
    public bool purchased;
    public int generation;
    public bool IsService => kind is ShopOfferKind.Investment or ShopOfferKind.Guidance or ShopOfferKind.Contract or ShopOfferKind.Supply;

    public string StableId => generation == 0 ? BaseStableId : $"refresh:{generation}:{BaseStableId}";
    string BaseStableId => kind switch
    {
        ShopOfferKind.Gear => $"artifact:{gear?.id}",
        ShopOfferKind.Enhancement => $"enhancement:{slot}:{enhancement?.id}",
        ShopOfferKind.Investment => "investment",
        ShopOfferKind.Guidance => "guidance",
        ShopOfferKind.Consumable => $"consumable:{consumable?.id}",
        ShopOfferKind.Contract => "contract:quest",
        ShopOfferKind.Supply => "supply",
        _ => "unknown"
    };

    public string DisplayName => kind switch
    {
        ShopOfferKind.Gear => gear != null ? gear.displayName : "Unknown Gear",
        ShopOfferKind.Enhancement => enhancement != null ? enhancement.displayName : "Unknown Mod",
        ShopOfferKind.Investment => "Stake",
        ShopOfferKind.Guidance => "Intel",
        ShopOfferKind.Consumable => consumable != null ? consumable.displayName : "Unknown Trick",
        ShopOfferKind.Contract => "Contract",
        ShopOfferKind.Supply => "Supply",
        _ => "Unknown Offer"
    };

    public string Icon => kind switch
    {
        ShopOfferKind.Gear => gear != null ? gear.icon : string.Empty,
        ShopOfferKind.Enhancement => enhancement != null ? enhancement.icon : string.Empty,
        ShopOfferKind.Investment => "GOLD",
        ShopOfferKind.Guidance => "MAP",
        ShopOfferKind.Consumable => consumable != null ? consumable.icon : string.Empty,
        _ => string.Empty
    };

    /// <summary>The authored item sprite of a Gear or Trick offer; services have none.</summary>
    public Sprite IconSprite => kind switch
    {
        ShopOfferKind.Gear => gear != null ? gear.iconSprite : null,
        ShopOfferKind.Consumable => consumable != null ? consumable.iconSprite : null,
        _ => null
    };

    public string Description(CardManager cards)
    {
        if (kind == ShopOfferKind.Gear)
            return gear != null ? gear.description : string.Empty;
        if (kind == ShopOfferKind.Enhancement)
        {
            string effect = enhancement != null ? enhancement.description : string.Empty;
            return $"Choose a card in your hand to enhance: {effect}";
        }
        if (kind == ShopOfferKind.Consumable)
            return consumable != null ? consumable.description : string.Empty;
        if (kind == ShopOfferKind.Investment)
            return $"Stake Credits now; at the next Black Market, receive {GameBalance.Current.blackMarket.stakePayoutMultiplier}× the stake or nothing.";
        if (kind == ShopOfferKind.Guidance)
            return "Reveal 1 random hidden node on the current district.";
        if (kind == ShopOfferKind.Supply)
            return $"Draw cards from the existing Draw Pile for {GameBalance.Current.blackMarket.supplyPricePerCard} Credits each, up to your hand limit. No recycling.";
        if (kind == ShopOfferKind.Contract)
        {
            var c = GameBalance.Current.contracts;
            return $"Choose: win {c.combatWinsRequired} encounters within {c.combatDeadlineDistricts} Districts for {c.combatReward} Credits; defeat a reachable Enforcer before leaving this district for an eligible {c.enforcerRewardRarity} Gear or heal {c.enforcerFallbackHeal} HP if none is collectible; or win an encounter with zero actual HP loss for {c.flawlessReward} Credits. Contract is run-local and uses no Gear slot. One active Contract maximum; failure settles once for up to {c.failureHpLoss} HP, never below 1.";
        }
        return string.Empty;
    }
}
