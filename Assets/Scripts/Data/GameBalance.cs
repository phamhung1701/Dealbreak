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
    public GearBalance gear = new();
    public EnemyAbilityBalance enemyAbilities = new();

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

/// <summary>Numbers for Gear special rules (<see cref="GearRules"/>), grouped by Gear family.</summary>
[Serializable]
public sealed class GearBalance
{
    [Header("Heart Meds (Rare, Epic)")]
    [Min(0)] public int heartMedsHeal = 3;
    [Tooltip("Credits paid when a Heart is played below half HP.")]
    [Min(0)] public int heartMedsLowHealthCredits = 3;
    [Tooltip("Epic: most overheal banked as attack.")]
    [Min(0)] public int heartMedsEpicOverhealCap = 10;

    [Header("Diamond Driver (Rare, Epic)")]
    [Min(0)] public int diamondDriverDraw = 2;
    [Tooltip("Epic: Diamonds played per discard choice.")]
    [Min(1)] public int diamondDriverEpicDiamondsPerChoice = 3;

    [Header("Spade Shield (Rare, Epic)")]
    [Min(0)] public int spadeShieldDraw = 1;

    [Header("Club Amplifier (Rare, Epic)")]
    [Min(1)] public int clubAmplifierRareMultiplier = 2;
    [Tooltip("Epic: a Club's damage is multiplied by numerator/denominator once per unplayed Club in hand.")]
    [Min(1)] public int clubAmplifierEpicNumerator = 5;
    [Min(1)] public int clubAmplifierEpicDenominator = 4;

    [Header("Glass Shard (Common, Rare, Epic)")]
    [Min(0)] public int glassShardAttackBonus = 5;
    [Min(0)] public int glassShardIncomingBonus = 5;
    [Min(1)] public int glassShardRareAttackMultiplier = 2;
    [Min(1)] public int glassShardRareIncomingMultiplier = 2;
    [Min(1)] public int glassShardEpicAttackMultiplier = 3;
    [Tooltip("Epic: the multiplier applies at exactly this HP.")]
    [Min(1)] public int glassShardEpicAtHealth = 1;

    [Header("Memory Card (Rare)")]
    [Min(0)] public int memoryCardRareHandBonus = 3;
    [Tooltip("Incoming retaliation in percent, rounded half up.")]
    [Min(100)] public int memoryCardRareIncomingPercent = 150;

    [Header("Overflow Canister (Common, Rare and Epic)")]
    [Min(0)] public int overflowCanisterCapacity = 2;
    [Min(0)] public int overflowCanisterRareCapacity = 3;

    [Header("Cargo Case (Common, Rare)")]
    [Min(1)] public int cargoCaseBackpackSlots = 5;
    [Tooltip("Rare: identical Tricks per backpack slot.")]
    [Min(1)] public int cargoCaseRareTricksPerSlot = 3;

    [Header("Guard Drone")]
    [Tooltip("Encounter Block per distinct Trick held.")]
    [Min(0)] public int guardDroneBlockPerTrickType = 2;

    [Header("Varsity Jacket")]
    [Tooltip("Encounter Block per combat win in the current streak.")]
    [Min(0)] public int varsityJacketBlockPerWin = 2;
    [Min(0)] public int varsityJacketMaxStreak = 3;

    [Header("Riot Helmet")]
    [Min(0)] public int riotHelmetShield = 1;

    [Header("Spotter Eye")]
    [Tooltip("Distinct ranks played against a Headliner to earn a charge.")]
    [Min(1)] public int spotterEyeDistinctRanks = 3;
    [Min(0)] public int spotterEyeAttackBonus = 1;

    [Header("Lowball (Rare, Epic)")]
    [Tooltip("A card below this rank primes the bonus.")]
    public CardData.Rank lowballBelowRank = CardData.Rank.Five;
    [Min(0)] public int lowballAttackBonus = 1;
    [Tooltip("Epic: permanent attack the primed card gains when a higher card kills.")]
    [Min(0)] public int lowballEpicPermanentAttack = 1;

    [Header("Counterpunch Gloves (Epic)")]
    [Tooltip("Permanent counter damage gained per counterattack kill.")]
    [Min(0)] public int counterpunchGlovesEpicGrowth = 1;

    [Header("Bounty Hologram")]
    [Tooltip("Credits from a won Enforcer encounter, in percent, rounded half up.")]
    [Min(0)] public int bountyHologramEnforcerPercent = 150;
    [Tooltip("Credits from a won Headliner encounter, in percent, rounded half up.")]
    [Min(0)] public int bountyHologramHeadlinerPercent = 125;

    [Header("Gold Pistol")]
    [Tooltip("Opening attack gains 1 damage per this many Credits held.")]
    [Min(1)] public int goldPistolCreditsPerDamage = 10;

    [Header("Survivor's Badge")]
    [Tooltip("Max HP gained per Headliner win.")]
    [Min(0)] public int survivorsBadgeMaxHealth = 3;
    [Tooltip("Extra max HP when the Headliner win cost no HP.")]
    [Min(0)] public int survivorsBadgeFlawlessBonus = 3;

    [Header("Savings Safe")]
    [Tooltip("Interest on held Credits after a Headliner win, in percent, rounded half up.")]
    [Min(0)] public int savingsSafeInterestPercent = 10;

    [Header("City Boots")]
    [Tooltip("Distinct node types visited in a District to earn the reward.")]
    [Min(1)] public int cityBootsNodeTypes = 3;
    [Min(0)] public int cityBootsHeal = 3;
    [Min(0)] public int cityBootsCredits = 3;

    [Header("Black Market Gear")]
    [Tooltip("Cashback Card: refund in percent of Credits spent, rounded half up.")]
    [Min(0)] public int cashbackCardPercent = 15;
    [Tooltip("Loyalty Card: Gear and Trick prices in percent, rounded half up.")]
    [Min(0)] public int loyaltyCardPricePercent = 80;
    [Tooltip("Loyalty Gift: paid Gear or Trick purchases per Trick reward.")]
    [Min(1)] public int loyaltyGiftPurchasesPerReward = 3;

    [Header("Rewards")]
    [Tooltip("Supply Crate: combat wins per Trick reward.")]
    [Min(1)] public int supplyCrateWinsPerReward = 3;
    [Tooltip("Mod Case: Mod Tricks offered at each District start.")]
    [Min(1)] public int modCaseChoices = 3;
    [Tooltip("Mod Bench: completed nodes between Mod rewards.")]
    [Min(1)] public int modBenchNodesPerReward = 3;
    [Tooltip("Mod Bench: Common Mods offered per reward.")]
    [Min(1)] public int modBenchChoices = 3;

    [Header("First Trick of an encounter")]
    [Tooltip("Grappling Hook: cards drawn.")]
    [Min(0)] public int grapplingHookDraw = 1;
    [Tooltip("First-Aid Kit: HP healed.")]
    [Min(0)] public int firstAidKitHeal = 3;
}

/// <summary>Numbers for enemy abilities, keyed by effect. Authored abilities still carry their own
/// <see cref="EnemyAbility.amount"/> where the effect uses it.</summary>
[Serializable]
public sealed class EnemyAbilityBalance
{
    [Header("Charged attacks (Wind-Up, Defense Cycle, Heavy Swing)")]
    [Tooltip("A charged hit, in percent of attack.")]
    [Min(100)] public int chargedAttackPercent = 200;
    [Tooltip("Heavy Swing's charged hit, in percent of attack.")]
    [Min(100)] public int heavySwingPercent = 150;
    [Tooltip("Royal Guard: Shield gained before each uncharged attack.")]
    [Min(0)] public int royalGuardShield = 1;

    [Header("Desperation")]
    [Tooltip("Active below this percent of max HP.")]
    [Range(0, 100)] public int desperationBelowHealthPercent = 50;
    [Min(100)] public int desperationAttackPercent = 125;

    [Header("Double Strike")]
    [Tooltip("Each of the two hits, in percent of the attack.")]
    [Range(0, 100)] public int doubleStrikeHitPercent = 60;

    [Header("Oppression")]
    [Tooltip("Held cards above this count add attack.")]
    [Min(0)] public int oppressionFreeCards = 3;
    [Min(0)] public int oppressionAttackPerCard = 2;

    [Header("War Drum (Hype Loop)")]
    [Tooltip("Attack each ally gains per enemy turn.")]
    [Min(0)] public int warDrumAttackPerTurn = 1;
    [Tooltip("Most attack War Drum adds to one ally per encounter.")]
    [Min(0)] public int warDrumMaxBonus = 3;

    [Header("Regeneration")]
    [Tooltip("Heal per enemy turn, in percent of max HP, rounded half up.")]
    [Range(0, 100)] public int regenerationHealPercent = 5;
    [Min(0)] public int regenerationMaxActivations = 3;

    [Header("Damage reduction")]
    [Tooltip("Guarded: the first hit each turn, in percent.")]
    [Range(0, 100)] public int guardedDamagePercent = 50;
    [Tooltip("Suit Call: the first hit of the called suit, in percent.")]
    [Range(0, 100)] public int suitCallDamagePercent = 50;

    [Header("Silence and Withering")]
    [Tooltip("Active while above this percent of max HP.")]
    [Range(0, 100)] public int silenceAboveHealthPercent = 50;
    [Range(0, 100)] public int witheringAboveHealthPercent = 50;

    [Header("Captaincy (Crew Aura)")]
    [Tooltip("Attack each ally gains while its captain stands.")]
    [Min(0)] public int captaincyAttackBonus = 1;
}
