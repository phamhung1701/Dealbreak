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

    static int GearAmount(string asset, GameplayEffectKind kind) =>
        AssetDatabase.LoadAssetAtPath<GearData>($"Assets/Data/Gear/{asset}.asset").effects.First(e => e.kind == kind).amount;

    static int GearConditionValue(string asset, GameplayEffectKind kind, GameplayConditionKind condition) =>
        AssetDatabase.LoadAssetAtPath<GearData>($"Assets/Data/Gear/{asset}.asset").effects
            .First(e => e.kind == kind).conditions.First(c => c.kind == condition).value;

    [Test]
    public void GearDescriptions_QuoteTheirGearBalanceNumbers()
    {
        var g = Balance.gear;
        Assert.That(g.bountyHologramEnforcerPercent, Is.EqualTo(g.bountyHologramHeadlinerPercent),
            "Bounty Hologram's text gives Enforcers and Headliners one bonus.");
        AssertQuotes(Gear("BountyHologram"), "Bounty Hologram", $"pays {g.bountyHologramHeadlinerPercent - 100}% more Credits");
        AssertQuotes(Gear("CargoCase"), "Cargo Case", $"Gain {g.cargoCaseBackpackSlots - CardManager.BASE_BACKPACK_CAPACITY} Trick slots");
        AssertQuotes(Gear("CargoCaseRare"), "Cargo Case Rare",
            $"Gain {g.cargoCaseBackpackSlots - CardManager.BASE_BACKPACK_CAPACITY} Trick slots", $"up to {g.cargoCaseRareTricksPerSlot} per slot");
        AssertQuotes(Gear("CashbackCard"), "Cashback Card", $"Refund {g.cashbackCardPercent}% of the Credits");
        AssertQuotes(Gear("CityBoots"), "City Boots",
            $"{g.cityBootsNodeTypes} different node types", $"heal {g.cityBootsHeal} HP and gain {g.cityBootsCredits} Credits");
        Assert.That(g.clubAmplifierRareMultiplier, Is.EqualTo(2), "Club Amplifier Rare's text says double DMG.");
        AssertQuotes(Gear("ClubAmplifierEpic"), "Club Amplifier Epic",
            $"×{Ratio(g.clubAmplifierEpicNumerator, g.clubAmplifierEpicDenominator)} DMG for each other Club");
        AssertQuotes(Gear("CounterpunchGlovesRare"), "Counterpunch Gloves Rare",
            $"×{Ratio(g.counterpunchGlovesRarePerfectPercent, 100)} on a Perfect Block");
        AssertQuotes(Gear("CounterpunchGlovesEpic"), "Counterpunch Gloves Epic",
            $"×{Ratio(g.counterpunchGlovesEpicPerfectPercent, 100)} on a Perfect Block",
            $"permanently gain +{g.counterpunchGlovesEpicGrowth} DMG for each enemy defeated");
        AssertQuotes(Gear("DiamondDriverRare"), "Diamond Driver Rare", $"draws {g.diamondDriverDraw} cards");
        AssertQuotes(Gear("DiamondDriverEpic"), "Diamond Driver Epic", $"draws {g.diamondDriverDraw} cards",
            $"Every {Ordinal(g.diamondDriverEpicDiamondsPerChoice)} Diamond");
        AssertQuotes(Gear("FirstAidKit"), "First-Aid Kit", $"heals {g.firstAidKitHeal} HP");
        AssertQuotes(Gear("GlassShard"), "Glass Shard",
            $"Deal {g.glassShardAttackBonus} more DMG", $"take {g.glassShardIncomingBonus} more DMG");
        Assert.That((g.glassShardRareAttackMultiplier, g.glassShardRareIncomingMultiplier), Is.EqualTo((2, 2)),
            "Glass Shard Rare's text says double DMG both ways.");
        Assert.That(g.glassShardEpicAttackMultiplier, Is.EqualTo(3), "Glass Shard Epic's text says triple DMG.");
        AssertQuotes(Gear("GlassShardEpic"), "Glass Shard Epic", $"at {g.glassShardEpicAtOrBelowHealth} HP or less");
        AssertQuotes(Gear("GoldPistol"), "Gold Pistol", $"1 more DMG per {g.goldPistolCreditsPerDamage} Credits held");
        AssertQuotes(Gear("NuclearOption"), "Nuclear Option", $"up to {g.nuclearOptionMaxCards} cards");
        AssertQuotes(Gear("GrapplingHook"), "Grappling Hook", $"draws {g.grapplingHookDraw} card");
        AssertQuotes(Gear("GuardDrone"), "Guard Drone", $"{g.guardDroneBlockPerTrickType} Block for each different Trick");
        AssertQuotes(Gear("HeadhunterRifle"), "Headhunter Rifle", $"adds {g.headhunterRifleAttackPerKill} DMG");
        AssertQuotes(Gear("HeartMedsRare"), "Heart Meds Rare",
            $"heals {g.heartMedsHeal} HP", $"gain {g.heartMedsLowHealthCredits} Credits");
        AssertQuotes(Gear("HeartMedsEpic"), "Heart Meds Epic",
            $"heals {g.heartMedsHeal} HP", $"gain {g.heartMedsLowHealthCredits} Credits", $"up to {g.heartMedsEpicOverhealCap}");
        Assert.That(g.lowballBelowRank, Is.EqualTo(CardData.Rank.Five), "Lowball's text says below 5.");
        AssertQuotes(Gear("LowballRare"), "Lowball Rare", $"adds {g.lowballAttackBonus} DMG to your next attack");
        AssertQuotes(Gear("LowballEpic"), "Lowball Epic",
            $"adds {g.lowballAttackBonus} DMG to your next attack", $"permanently gains {g.lowballEpicPermanentAttack} DMG");
        AssertQuotes(Gear("FlipSwitch"), "Flip Switch", $"adds {g.flipSwitchAttackBonus} DMG to your next attack");
        AssertQuotes(Gear("LoyaltyCard"), "Loyalty Card", $"{100 - g.loyaltyCardPricePercent}% discount");
        AssertQuotes(Gear("LoyaltyGift"), "Loyalty Gift", $"every {g.loyaltyGiftPurchasesPerReward} purchases");
        AssertQuotes(Gear("MemoryCardRare"), "Memory Card Rare",
            $"by {g.memoryCardRareHandBonus} and take ×{Ratio(g.memoryCardRareIncomingPercent, 100)} DMG");
        AssertQuotes(Gear("ModBenchRare"), "Mod Bench Rare",
            $"Every {g.modBenchNodesPerReward} nodes, choose 1 of {g.modBenchChoices} Common Mods");
        AssertQuotes(Gear("ModCase"), "Mod Case", $"choose 1 of {g.modCaseChoices} random Mod Tricks");
        AssertQuotes(Gear("OverflowCanister"), "Overflow Canister", $"up to {g.overflowCanisterCapacity} per action");
        AssertQuotes(Gear("OverflowCanisterRare"), "Overflow Canister Rare", $"up to {g.overflowCanisterRareCapacity} per action");
        AssertQuotes(Gear("OverflowCanisterEpic"), "Overflow Canister Epic", $"up to {g.overflowCanisterRareCapacity} per action");
        AssertQuotes(Gear("RiotHelmet"), "Riot Helmet", $"Gain {g.riotHelmetShield} Shield");
        AssertQuotes(Gear("SavingsSafe"), "Savings Safe", $"Gain {g.savingsSafeInterestPercent}% of your current Credits");
        AssertQuotes(Gear("SpotterEye"), "Spotter Eye",
            $"play {g.spotterEyeDistinctRanks} different ranks", $"add {g.spotterEyeAttackBonus} DMG to your next attack");
        Assert.That(g.supplyCrateWinsPerReward, Is.EqualTo(1), "Supply Crate's text says after each combat won.");
        AssertQuotes(Gear("SurvivorsBadge"), "Survivor's Badge",
            $"max HP by {g.survivorsBadgeMaxHealth}, or by {g.survivorsBadgeMaxHealth + g.survivorsBadgeFlawlessBonus}");
        AssertQuotes(Gear("VarsityJacket"), "Varsity Jacket",
            $"{g.varsityJacketBlockPerWin} Block per combat won in a row", $"up to {g.varsityJacketBlockPerWin * g.varsityJacketMaxStreak}");
    }

    // Typed Gear effects keep their numbers on the Gear asset itself.
    [Test]
    public void GearDescriptions_QuoteTheirTypedEffectAmounts()
    {
        AssertQuotes(Gear("CounterpunchGloves"), "Counterpunch Gloves",
            $"counter-attack for {GearAmount("CounterpunchGloves", GameplayEffectKind.CounterDamage)} DMG");
        AssertQuotes(Gear("StreetJacket"), "Street Jacket",
            $"by {GearAmount("StreetJacket", GameplayEffectKind.IncomingCombatDamageReduction)}");
        AssertQuotes(Gear("TacticalPlating"), "Tactical Plating",
            $"by {GearAmount("TacticalPlating", GameplayEffectKind.IncomingCombatDamageReduction)}",
            $"{GearConditionValue("TacticalPlating", GameplayEffectKind.IncomingCombatDamageReduction, GameplayConditionKind.AttackerHealthAtLeast)} or more HP");
        AssertQuotes(Gear("PowerBaton"), "Power Baton",
            $"more than {GearConditionValue("PowerBaton", GameplayEffectKind.FlatAttack, GameplayConditionKind.AttackValueGreaterThan)} DMG",
            $"deal {GearAmount("PowerBaton", GameplayEffectKind.FlatAttack)} more DMG");
        AssertQuotes(Gear("Scalpel"), "Scalpel", $"{GearAmount("Scalpel", GameplayEffectKind.FlatActionDamage)} more DMG",
            $"up to {GearAmount("Scalpel", GameplayEffectKind.ActionDamageCap)} DMG");
        AssertQuotes(Gear("MatchedCells"), "Matched Cells", $"at most {GearAmount("MatchedCells", GameplayEffectKind.ActionDamageCap)} DMG");
        AssertQuotes(Gear("Lighter"), "Lighter", $"draw {GearAmount("Lighter", GameplayEffectKind.DestroyBlockingCard)} card");
        AssertQuotes(Gear("DiamondDriver"), "Diamond Driver", $"draws {GearAmount("DiamondDriver", GameplayEffectKind.Draw)} card");
        AssertQuotes(Gear("RecoveryPills"), "Recovery Pills", $"Heal {GearAmount("RecoveryPills", GameplayEffectKind.Heal)} HP");
        AssertQuotes(Gear("AutoInjector"), "Auto-Injector", $"Heal {GearAmount("AutoInjector", GameplayEffectKind.Heal)} HP");
        AssertQuotes(Gear("FieldTablet"), "Field Tablet", $"Draw {GearAmount("FieldTablet", GameplayEffectKind.Draw)} card");
        AssertQuotes(Gear("PayoutTracker"), "Payout Tracker", $"Gain {GearAmount("PayoutTracker", GameplayEffectKind.BonusCredits)} Credits");
        AssertQuotes(Gear("AceProtocol"), "Ace Protocol",
            $"{GearAmount("AceProtocol", GameplayEffectKind.CriticalChanceOverride)}%");
        AssertQuotes(Gear("CarbonSleeves"), "Carbon Sleeves", $"block {GearAmount("CarbonSleeves", GameplayEffectKind.FlatBlock)} more DMG");
        AssertQuotes(Gear("ModdersKit"), "Modder's Kit", $"deal {GearAmount("ModdersKit", GameplayEffectKind.FlatAttack)} more DMG");
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

    // Trick numbers live on the Trick asset itself, or on the Mod a Mod Trick applies.
    [Test]
    public void TrickDescriptions_QuoteTheirTrickAndModNumbers()
    {
        AssertQuotes(Trick("EnergyBar").description, "Energy Bar", $"Heal {Trick("EnergyBar").healAmount} HP");
        AssertQuotes(Trick("ThrowingStar").description, "Throwing Star", $"Deal {Trick("ThrowingStar").damageAmount} DMG");
        AssertQuotes(Trick("CloneVial").description, "Clone Vial", $"Add {Trick("CloneVial").copies} copies");
        AssertModTrick("BoosterShot", amount => $"+{amount} DMG");
        AssertModTrick("AlloyPlate", amount => $"blocks {amount} more DMG");
        AssertModTrick("Bandage", amount => $"heal {amount} HP at the start of each turn");
        AssertModTrick("QuickMarker", amount => $"draw {amount} card");
        AssertModTrick("LaserScope", amount => $"{amount}% chance");
        AssertModTrick("PokerChip", amount => $"gain {amount} Credits");
        AssertModTrick("BackupDrone", amount => $"played cards deal +{amount} DMG");
        AssertModTrick("BloodBag", amount => $"costs {amount} HP and deals +{amount} DMG");
        AssertModTrick("Firecracker", null);
        AssertModTrick("FeralCapsule", amount => $"permanently gains +{amount} DMG");
        AssertModTrick("PrismCube", null);
        AssertModTrick("DoubleTap", null);
    }

    static ConsumableData Trick(string asset) =>
        AssetDatabase.LoadAssetAtPath<ConsumableData>($"Assets/Data/Tricks/{asset}.asset");

    static void AssertModTrick(string asset, System.Func<int, string> quote)
    {
        var trick = Trick(asset);
        var mod = trick.enhancementToApply;
        Assert.That(mod, Is.Not.Null, asset);
        AssertQuotes(trick.description, asset, $"Give a hand card {mod.displayName}:");
        if (quote != null) AssertQuotes(trick.description, asset, quote(mod.effects[0].amount));
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
