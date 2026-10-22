using System;
using UnityEngine;

/// <summary>
/// Balance numbers for run systems, authored in Resources/GameBalance. Rules read their numbers
/// here instead of carrying literals. Player-facing text that quotes a number is authored
/// separately and pinned to these values by <c>BalanceTextDriftTests</c>.
/// </summary>
[CreateAssetMenu(fileName = "GameBalance", menuName = "Dealbreak/Game Balance")]
public sealed class GameBalance : ScriptableObject
{
    public DistrictScalingBalance districtScaling = new();
    public EnforcerBalance enforcers = new();
    public BlackMarketBalance blackMarket = new();
    public ContractBalance contracts = new();
    public RedlineDealBalance redlineDeal = new();

    static GameBalance _current;

    public static GameBalance Current
    {
        get
        {
            if (_current == null) _current = Resources.Load<GameBalance>("GameBalance");
            if (_current == null) throw new InvalidOperationException("Resources/GameBalance is missing.");
            return _current;
        }
    }
}

[Serializable]
public sealed class DistrictScalingBalance
{
    [Tooltip("Enemy HP gained per District after the first, in percent of authored HP.")]
    [Min(0)] public int hpGrowthPercent = 20;
    [Tooltip("Enemy ATK gained per District after the first, in percent of authored ATK.")]
    [Min(0)] public int attackGrowthPercent = 7;
    [Tooltip("Infinite mode: HP multiplier per District past the authored run, in percent.")]
    [Min(100)] public int infiniteHpPercent = 110;
    [Tooltip("Infinite mode: ATK multiplier per District past the authored run, in percent.")]
    [Min(100)] public int infiniteAttackPercent = 104;
}

[Serializable]
public sealed class EnforcerBalance
{
    [Header("Generic Enforcers (authored Enforcers use their own stats)")]
    [Min(100)] public int hpPercent = 150;
    [Min(0)] public int attackBonus = 2;
    [Min(1)] public int creditsMultiplier = 2;
}

[Serializable]
public struct GearRarityWeight
{
    public string rarity;
    [Min(0)] public int weight;
}

[Serializable]
public sealed class BlackMarketBalance
{
    [Header("Goods")]
    [Tooltip("Goods per Black Market (x + y + z), before and after the route midpoint.")]
    public Vector3Int midGoods = new(2, 2, 1);
    public Vector3Int preHeadlinerGoods = new(1, 1, 3);
    [Tooltip("Chance in percent that a Goods slot is Gear; otherwise a Trick.")]
    [Range(0, 100)] public int gearSlotPercent = 75;
    [Tooltip("Rarity roll for a Gear slot, in order. A rarity with nothing left falls through to the next.")]
    public GearRarityWeight[] gearRarityWeights =
    {
        new() { rarity = "Common", weight = 6 },
        new() { rarity = "Rare", weight = 3 },
        new() { rarity = "Epic", weight = 1 }
    };
    [Tooltip("First Goods refresh price; refresh n costs this plus n(n+3)/2.")]
    [Min(0)] public int refreshBasePrice = 3;
    [Tooltip("Selling pays this percent of the authored price, rounded down.")]
    [Range(0, 100)] public int sellPercent = 50;

    [Header("Services")]
    [Min(1)] public int servicesOffered = 2;
    [Min(0)] public int contractPrice = 20;
    [Min(0)] public int intelPrice = 5;
    [Min(0)] public int stakePrice = 5;
    [Min(1)] public int supplyPricePerCard = 3;
    [Tooltip("A successful Stake pays the stake times this.")]
    [Min(0)] public int stakePayoutMultiplier = 3;
}

[Serializable]
public sealed class ContractBalance
{
    [Header("Combat Contract")]
    [Min(1)] public int combatWinsRequired = 2;
    [Tooltip("Districts the Combat Contract stays open, counting the one it was taken in.")]
    [Min(1)] public int combatDeadlineDistricts = 12;
    [Min(0)] public int combatReward = 6;

    [Header("Flawless Contract")]
    [Min(0)] public int flawlessReward = 10;

    [Header("Enforcer Contract")]
    public string enforcerRewardRarity = "Rare";
    [Tooltip("Healed instead when no Gear of the reward rarity can be collected.")]
    [Min(0)] public int enforcerFallbackHeal = 5;

    [Header("Failure")]
    [Tooltip("HP lost when a Contract fails. Never takes the player below 1 HP.")]
    [Min(0)] public int failureHpLoss = 5;
}

[Serializable]
public sealed class RedlineDealBalance
{
    [Tooltip("A Risk node is the Redline Deal one time in this many.")]
    [Min(1)] public int appearanceOneIn = 20;

    [Header("Go All In")]
    public string allInGearRarity = "Legendary";
    [Min(1)] public int allInMaxHealth = 5;

    [Header("Take the Big Cut")]
    public string bigCutGearRarity = "Epic";
    [Tooltip("Paid from current HP; the player must have more than this.")]
    [Min(0)] public int bigCutHpCost = 15;

    [Header("Take the Small Cut (upgrade an owned Gear)")]
    [Min(0)] public int smallCutHpCost = 5;
}
