using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Player-facing text that quotes a tuned number is authored copy (owned by the terminology workbook),
// while the number itself lives in Resources/GameBalance. A balance change must come with a text
// change: these tests fail when the two drift apart.
public sealed class BalanceTextDriftTests
{
    static GameBalance Balance => GameBalance.Current;

    static void AssertQuotes(string text, string what, params string[] expected)
    {
        Assert.That(text, Is.Not.Null.And.Not.Empty, what);
        string flat = Regex.Replace(text, @"\s+", " ");
        foreach (var quote in expected)
            Assert.That(flat, Does.Contain(quote),
                $"{what} no longer quotes \"{quote}\" from GameBalance; update the text or the balance.");
    }

    static string Ratio(int numerator, int denominator) =>
        ((decimal)numerator / denominator).ToString("0.##", CultureInfo.InvariantCulture);

    static string Ordinal(int value) => value switch
    {
        2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth",
        _ => value + "th"
    };

    static string Gear(string asset) =>
        AssetDatabase.LoadAssetAtPath<GearData>($"Assets/Data/Gear/{asset}.asset").description;

    static string HeadlinerAbility(EnemyAbilityEffect effect) =>
        HeadlinerCatalog.Load().abilityText.First(entry => entry.effect == effect).description;

    static string EnemyAbility(string asset) =>
        AssetDatabase.LoadAssetAtPath<EnemyAbility>($"Assets/Data/Enemies/Abilities/{asset}.asset").description;

    [Test]
    public void GearDescriptions_QuoteTheirGearBalanceNumbers()
    {
        var g = Balance.gear;
        AssertQuotes(Gear("BountyHologram"), "Bounty Hologram",
            $"+{g.bountyHologramEnforcerPercent - 100}% base Credits", $"+{g.bountyHologramHeadlinerPercent - 100}% base Credits");
        AssertQuotes(Gear("CargoCase"), "Cargo Case",
            $"({CardManager.BASE_BACKPACK_CAPACITY} to {g.cargoCaseBackpackSlots})", $"up to {g.cargoCaseRareTricksPerSlot} identical");
        AssertQuotes(Gear("CargoCaseRare"), "Cargo Case Rare",
            $"Retain {g.cargoCaseBackpackSlots} slots", $"up to {g.cargoCaseRareTricksPerSlot} identical tricks per slot");
        AssertQuotes(Gear("CashbackCard"), "Cashback Card", $"Refund {g.cashbackCardPercent}% actual Credits spent");
        AssertQuotes(Gear("CityBoots"), "City Boots",
            $"{g.cityBootsNodeTypes} different node types", $"Heal {g.cityBootsHeal} HP and gain {g.cityBootsCredits} Credits");
        AssertQuotes(Gear("ClubAmplifierRare"), "Club Amplifier Rare", $"×{g.clubAmplifierRareMultiplier}");
        AssertQuotes(Gear("ClubAmplifierEpic"), "Club Amplifier Epic",
            $"×{Ratio(g.clubAmplifierEpicNumerator, g.clubAmplifierEpicDenominator)} each");
        AssertQuotes(Gear("CounterpunchGlovesEpic"), "Counterpunch Gloves Epic",
            $"+{g.counterpunchGlovesEpicGrowth} counterattack damage");
        AssertQuotes(Gear("DiamondDriverRare"), "Diamond Driver Rare", $"draws {g.diamondDriverDraw} cards");
        AssertQuotes(Gear("DiamondDriverEpic"), "Diamond Driver Epic", $"Keep Rare draw {g.diamondDriverDraw}",
            $"every {Ordinal(g.diamondDriverEpicDiamondsPerChoice)} manually played Diamond");
        AssertQuotes(Gear("FirstAidKit"), "First-Aid Kit", $"{g.firstAidKitHeal} HP");
        AssertQuotes(Gear("GlassShard"), "Glass Shard",
            $"Card attacks deal +{g.glassShardAttackBonus} damage", $"incoming attacks deal +{g.glassShardIncomingBonus} damage");
        AssertQuotes(Gear("GlassShardRare"), "Glass Shard Rare",
            $"Card attacks and incoming attacks deal {g.glassShardRareAttackMultiplier}× damage");
        Assert.That(g.glassShardRareIncomingMultiplier, Is.EqualTo(g.glassShardRareAttackMultiplier),
            "Glass Shard Rare's text gives attacks and incoming attacks one multiplier.");
        AssertQuotes(Gear("GlassShardEpic"), "Glass Shard Epic",
            $"At exactly {g.glassShardEpicAtHealth} HP, card attacks deal {g.glassShardEpicAttackMultiplier}× damage");
        AssertQuotes(Gear("GoldPistol"), "Gold Pistol", $"+1 damage per {g.goldPistolCreditsPerDamage} Credits held");
        AssertQuotes(Gear("NuclearOption"), "Nuclear Option", $"2 to {g.nuclearOptionMaxCards} cards",
            $"up to {g.nuclearOptionMaxCards} cards", $"{g.nuclearOptionMaxCards} cards maximum");
        AssertQuotes(Gear("GrapplingHook"), "Grappling Hook", $"draws {g.grapplingHookDraw}");
        AssertQuotes(Gear("GuardDrone"), "Guard Drone", $"+{g.guardDroneBlockPerTrickType} Block/type");
        AssertQuotes(Gear("HeartMedsRare"), "Heart Meds Rare",
            $"heals {g.heartMedsHeal} HP", $"gain {g.heartMedsLowHealthCredits} Credits");
        AssertQuotes(Gear("HeartMedsEpic"), "Heart Meds Epic",
            $"Rare Heal {g.heartMedsHeal} and conditional Credits {g.heartMedsLowHealthCredits}");
        AssertQuotes(Gear("LowballRare"), "Lowball Rare",
            $"+{g.lowballAttackBonus} damage to next attack", $"below {g.lowballBelowRank}");
        AssertQuotes(Gear("LowballEpic"), "Lowball Epic", $"+{g.lowballEpicPermanentAttack} permanent attack");
        AssertQuotes(Gear("LoyaltyCard"), "Loyalty Card", $"{100 - g.loyaltyCardPricePercent}% purchase discount");
        AssertQuotes(Gear("LoyaltyGift"), "Loyalty Gift", $"Every {g.loyaltyGiftPurchasesPerReward} item purchases");
        AssertQuotes(Gear("MemoryCardRare"), "Memory Card Rare",
            $"Gain {g.memoryCardRareHandBonus} hand slots", $"take {Ratio(g.memoryCardRareIncomingPercent, 100)}× incoming");
        AssertQuotes(Gear("ModBenchRare"), "Mod Bench Rare",
            $"Every {g.modBenchNodesPerReward} nodes, choose 1 of {g.modBenchChoices} Common Mods");
        AssertQuotes(Gear("ModCase"), "Mod Case", $"choose 1 of {g.modCaseChoices} random mod tricks");
        AssertQuotes(Gear("OverflowCanister"), "Overflow Canister", $"Up to {g.overflowCanisterCapacity} per root");
        AssertQuotes(Gear("OverflowCanisterRare"), "Overflow Canister Rare", $"Up to {g.overflowCanisterRareCapacity} per root");
        AssertQuotes(Gear("OverflowCanisterEpic"), "Overflow Canister Epic", $"Up to {g.overflowCanisterRareCapacity} per root");
        AssertQuotes(Gear("RiotHelmet"), "Riot Helmet", $"with {g.riotHelmetShield} Shield");
        AssertQuotes(Gear("SavingsSafe"), "Savings Safe", $"grants {g.savingsSafeInterestPercent}% of unspent Credits");
        AssertQuotes(Gear("SpadeShieldRare"), "Spade Shield Rare", $"Draw {g.spadeShieldDraw} card");
        AssertQuotes(Gear("SpotterEye"), "Spotter Eye",
            $"play {g.spotterEyeDistinctRanks} different ranks", $"+{g.spotterEyeAttackBonus} damage to next attack");
        AssertQuotes(Gear("SupplyCrate"), "Supply Crate", $"Every {g.supplyCrateWinsPerReward} combat wins");
        AssertQuotes(Gear("SurvivorsBadge"), "Survivor's Badge",
            $"+{g.survivorsBadgeMaxHealth} Max HP; +{g.survivorsBadgeFlawlessBonus} additional");
        AssertQuotes(Gear("VarsityJacket"), "Varsity Jacket",
            $"+{g.varsityJacketBlockPerWin} encounter Block per consecutive",
            $"Cap {g.varsityJacketBlockPerWin * g.varsityJacketMaxStreak}");
    }

    [Test]
    public void EnemyAbilityDescriptions_QuoteTheirEnemyAbilityBalanceNumbers()
    {
        var e = Balance.enemyAbilities;
        AssertQuotes(HeadlinerAbility(EnemyAbilityEffect.HeavySwing), "Heavy Swing", $"{e.heavySwingPercent}% ATK");
        AssertQuotes(HeadlinerAbility(EnemyAbilityEffect.Desperation), "Desperation",
            $"Gains {e.desperationAttackPercent - 100}% ATK while below {e.desperationBelowHealthPercent}% maximum HP");
        AssertQuotes(HeadlinerAbility(EnemyAbilityEffect.Regeneration), "Regeneration",
            $"Heals {e.regenerationHealPercent}% maximum HP", $"up to {e.regenerationMaxActivations} times");
        AssertQuotes(HeadlinerAbility(EnemyAbilityEffect.Guarded), "Guarded", $"reduced by {100 - e.guardedDamagePercent}%");
        AssertQuotes(HeadlinerAbility(EnemyAbilityEffect.DoubleStrike), "Double Strike",
            $"each hit deals {e.doubleStrikeHitPercent}% of the response budget");
        AssertQuotes(HeadlinerAbility(EnemyAbilityEffect.Silence), "Silence", $"While above {e.silenceAboveHealthPercent}% HP");
        AssertQuotes(HeadlinerAbility(EnemyAbilityEffect.Withering), "Withering", $"While above {e.witheringAboveHealthPercent}% HP");
        AssertQuotes(HeadlinerAbility(EnemyAbilityEffect.Oppression), "Oppression",
            $"+{e.oppressionAttackPerCard} response damage per held card beyond {e.oppressionFreeCards}");

        Assert.That(e.chargedAttackPercent, Is.EqualTo(200), "Wind-Up and Defense Cycle text say double damage.");
        AssertQuotes(EnemyAbility("WindUp"), "Wind-Up", "double-damage");
        AssertQuotes(EnemyAbility("DefenseCycle"), "Defense Cycle", $"+{e.royalGuardShield} Shield", "double-damage");
        AssertQuotes(EnemyAbility("CrewAura"), "Crew Aura", $"+{e.captaincyAttackBonus} ATK");
        AssertQuotes(EnemyAbility("HypeLoop"), "Hype Loop", $"+{e.warDrumAttackPerTurn} ATK", $"up to +{e.warDrumMaxBonus}");
        AssertQuotes(EnemyAbility("SuitCallout"), "Suit Callout", $"deals {e.suitCallDamagePercent}% damage");
    }

    [Test]
    public void RedlineDealChoices_QuoteTheirCostsAndRarities()
    {
        var r = Balance.redlineDeal;
        var deal = AssetDatabase.LoadAssetAtPath<RunEventDefinition>("Assets/Data/Run/Incidents/RedlineDeal.asset");
        Assert.That(deal.flow, Is.EqualTo(RunEventFlow.BloodRitual));
        AssertQuotes(deal.choices[0].description, "Go All In", $"Set max HP to {r.allInMaxHealth}", $"{r.allInGearRarity} Gear");
        AssertQuotes(deal.choices[1].description, "Take the Big Cut", $"Pay {r.bigCutHpCost} current HP", $"{r.bigCutGearRarity} Gear");
        AssertQuotes(deal.choices[2].description, "Take the Small Cut", $"Pay {r.smallCutHpCost} current HP");
    }

    [Test]
    public void QuestContractText_QuotesItsPriceRewardsAndPenalty()
    {
        var c = Balance.contracts;
        var contract = Resources.Load<RunEventDefinition>("QuestContract");
        Assert.That(contract, Is.Not.Null, "Resources/QuestContract holds the Contract choice text.");
        Assert.That(contract.choices, Has.Length.EqualTo(4), "Combat, Enforcer and Flawless Contracts, then Cancel.");
        AssertQuotes(contract.description, "Quest Contract", $"its {Balance.blackMarket.contractPrice} Credits fee");
        AssertQuotes(contract.choices[0].description, "Combat Contract",
            $"Win {c.combatWinsRequired} encounters within {c.combatDeadlineDistricts} Districts",
            $"Reward: {c.combatReward} Credits", $"lose {c.failureHpLoss} HP");
        AssertQuotes(contract.choices[1].description, "Enforcer Contract",
            $"a {c.enforcerRewardRarity} Gear item", $"heal {c.enforcerFallbackHeal} HP", $"lose {c.failureHpLoss} HP");
        AssertQuotes(contract.choices[2].description, "Flawless Contract",
            $"Reward: {c.flawlessReward} Credits", $"lose {c.failureHpLoss} HP");
    }
}
