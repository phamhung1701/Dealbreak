using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GameDataGenerator
{
    const string GearDir = "Assets/Data/Gear";
    const string EnhancementDir = "Assets/Data/Mods";
    const string EnemyDir = "Assets/Data/Enemies";

    [MenuItem("Dealbreak/Content/Generate Data Assets")]
    public static void GenerateAll()
    {
        GenerateGear();
        GenerateEnhancements();
        GenerateEnemies();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Game data assets generated.");
    }

    [MenuItem("Dealbreak/Content/Generate Gear Assets")]
    public static void GenerateGear()
    {
        EnsureFolder(GearDir);

        var club = CreateGear("ClubAmplifier", "club_power", "Club Amplifier", "♣", "Trigger: On Card Played\nCondition: Suit = Clubs\nEffect: Multiply this card's damage.\nValue: ×2", 15);
        ConfigureCoreGear(club, "rel_001", GameplayEffectKind.AttackMultiplier,
            GameplayEffectTrigger.AttackCalculated, 2, CardData.Suit.Clubs);

        var spade = CreateGear("SpadeShield", "spade_power", "Spade Shield", "♠", "Trigger: On Card Played\nCondition: Suit = Spades\nEffect: Multiply this card's block value.\nValue: ×2", 15);
        ConfigureCoreGear(spade, "rel_004", GameplayEffectKind.BlockMultiplier,
            GameplayEffectTrigger.DefenseCalculated, 2, CardData.Suit.Spades);

        var heart = CreateGear("HeartMeds", "heart_power", "Heart Meds", "♥", "Trigger: On Card Played\nCondition: Suit = Hearts\nEffect: Heal HP.\nValue: +1 HP", 20);
        ConfigureCoreGear(heart, "rel_002", GameplayEffectKind.Heal,
            GameplayEffectTrigger.CardCommitted, 1, CardData.Suit.Hearts);

        var diamond = CreateGear("DiamondDriver", "diamond_power", "Diamond Driver", "♦", "Trigger: On Card Played\nCondition: Suit = Diamonds\nEffect: Draw cards.\nValue: 2\nLimit: 1 card", 20);
        ConfigureCoreGear(diamond, "rel_003", GameplayEffectKind.Draw,
            GameplayEffectTrigger.CardCommitted, 2, CardData.Suit.Diamonds);

        // Temporary prototype prices; the spreadsheet leaves prices blank.
        ConfigureTypedGear(CreateGear("StreetJacket", "rel_005", "Street Jacket", "ARMOR",
            "Trigger: On Taking Damage\nEffect: Reduce incoming damage.\nValue: -3 damage", 15), "rel_005", "Common",
            new GameplayEffectDefinition { kind = GameplayEffectKind.IncomingCombatDamageReduction,
                trigger = GameplayEffectTrigger.IncomingDamageCalculated, amount = 3 });
        ConfigureTypedGear(CreateGear("Scalpel", "rel_006", "Scalpel", "DAGGER",
            "Trigger: On Card Played\nEffect: Add flat damage.\nValue: +3 damage\nLimit: Total damage cannot exceed 10.", 15),
            "rel_006", "Common",
            new GameplayEffectDefinition { kind = GameplayEffectKind.FlatActionDamage,
                trigger = GameplayEffectTrigger.ActionDamageCalculated, amount = 3 },
            new GameplayEffectDefinition { kind = GameplayEffectKind.ActionDamageCap,
                trigger = GameplayEffectTrigger.ActionDamageCalculated, amount = 10 });
        ConfigureTypedGear(CreateGear("FieldTablet", "rel_007", "Field Tablet", "TOME",
            "Trigger: On Combat Start\nCondition: Enough free hand space\nEffect: Draw cards.\nValue: 1", 18), "rel_007", "Common",
            new GameplayEffectDefinition { kind = GameplayEffectKind.Draw,
                trigger = GameplayEffectTrigger.EncounterStart, amount = 1 });
        ConfigureTypedGear(CreateGear("PowerBaton", "rel_023", "Power Baton", "SWORD",
            "Trigger: On Card Played\nCondition: Attack > 8\nEffect: Gain bonus damage.\nValue: +10 damage", 25),
            "rel_023", "Rare",
            new GameplayEffectDefinition { kind = GameplayEffectKind.FlatAttack,
                trigger = GameplayEffectTrigger.AttackCalculated, amount = 10,
                conditions = new[] { new GameplayEffectCondition
                { kind = GameplayConditionKind.AttackValueGreaterThan, value = 8 } } });

        var artisan = CreateGear("ModdersKit", "artisan_tools", "Modder's Kit", "ENH", "Enhanced cards deal +3 damage", 25);
        artisan.requiresEnhancedCard = true;
        artisan.flatDamageBonus = 3;

        var sleeves = CreateGear("CarbonSleeves", "reinforced_sleeves", "Carbon Sleeves", "BLOCK", "Enhanced cards block +3 damage", 22);
        sleeves.requiresEnhancedCard = true;
        sleeves.defenseBonus = 3;

        CreateGear("RecoveryPills", "victory_draught", "Recovery Pills", "HEAL", "Heal 3 HP after each victory", 24).healAfterVictory = 3;
        CreateGear("PayoutTracker", "golden_compass", "Payout Tracker", "GOLD", "Gain +4 Credits after each victory", 20).bonusCredits = 4;

        ConfigureSpecialGear(CreateGear("CargoCase", "rel_120", "Cargo Case", "PACK",
            "Trigger: Passive\nCondition: Backpack capacity\nEffect: Add 2 backpack slots (3 to 5).\nValue: +2 slots\nLimit: Tricks remain non-stacking at Common tier; Rare enables stacks of up to 3 identical items.", 20),
            "rel_120", "Common", 1, "", GearSpecialRule.TravelerPackCommon);
        ConfigureSpecialGear(CreateGear("CargoCaseRare", "rel_121", "Cargo Case", "PACK+",
            "Trigger: Passive\nCondition: Tricks in backpack\nEffect: Retain 5 slots; up to 3 identical tricks per slot; track charges per item, use partially-used first, separate rune suits; capacity loss withdraw-only, never delete.\nValue: 3 per slot\nLimit: Withdraw-only on capacity loss", 30),
            "rel_121", "Rare", 2, "rel_120", GearSpecialRule.TravelerPackRare);
        ConfigureSpecialGear(CreateGear("GuardDrone", "rel_123", "Guard Drone", "BLOCK",
            "Trigger: Combat Start\nCondition: Distinct trick types in backpack\nEffect: Starting block per distinct trick type in backpack.\nValue: +2 Block/type\nLimit: Encounter-long numeric Block; consumed point-for-point before Shield; expires after encounter", 20),
            "rel_123", "Common", 1, "", GearSpecialRule.PreparationManual);
        ConfigureSpecialGear(CreateGear("GrapplingHook", "rel_117", "Grappling Hook", "DRAW",
            "Trigger: On Trick Use\nCondition: First trick used this encounter\nEffect: First trick used each encounter draws 1.\nValue: 1 card\nLimit: First per encounter", 20),
            "rel_117", "Common", 1, "", GearSpecialRule.ScavengersPouch);
        ConfigureSpecialGear(CreateGear("SupplyCrate", "rel_129", "Supply Crate", "LOOT",
            "Trigger: Combat Wins\nCondition: Every 3 combat wins\nEffect: Every 3 combat wins: random non-mod trick.\nValue: 1\nLimit: Full claim tray: replace or decline", 30),
            "rel_129", "Rare", 1, "", GearSpecialRule.ScavengersSatchel);
        ConfigureSpecialGear(CreateGear("CashbackCard", "rel_125", "Cashback Card", "15%",
            "Trigger: On Purchase\nCondition: Actual Credits spent\nEffect: Refund 15% actual Credits spent; free purchases no cashback, refund never exceeds cost, no retroactive benefit.\nValue: 15%\nLimit: Actual spend only; provisional", 20),
            "rel_125", "Common", 1, "", GearSpecialRule.CashbackToken);
        ConfigureSpecialGear(CreateGear("SavingsSafe", "rel_127", "Savings Safe", "VAULT",
            "Trigger: Headliner Defeated\nCondition: Unspent Credits\nEffect: Headliner defeat grants 10% of unspent Credits.\nValue: 10%\nLimit: Savings accumulate; provisional", 30),
            "rel_127", "Rare", 1, "", GearSpecialRule.GoldenVault);
        ConfigureSpecialGear(CreateGear("LoyaltyCard", "rel_124", "Loyalty Card", "SALE",
            "Trigger: On Purchase\nCondition: Eligible item purchase\nEffect: 20% purchase discount.\nValue: 20%\nLimit: Discount before cashback; provisional", 20),
            "rel_124", "Common", 1, "", GearSpecialRule.MerchantsBadge);
        ConfigureSpecialGear(CreateGear("LoyaltyGift", "rel_131", "Loyalty Gift", "GIFT",
            "Trigger: Item Purchases\nCondition: Every 3 item purchases\nEffect: After 3 item purchases: random non-mod trick.\nValue: 1\nLimit: Free rewards do not count; full tray replace/decline", 30),
            "rel_131", "Rare", 1, "", GearSpecialRule.MerchantsGift);
        ConfigureSpecialGear(CreateGear("ModCase", "rel_130", "Mod Case", "ALCH",
            "Trigger: New District\nCondition: District start\nEffect: New district: choose 1 of 3 random mod tricks.\nValue: 1 of 3\nLimit: Full tray replace/decline; not enemy drops", 30),
            "rel_130", "Rare", 1, "", GearSpecialRule.AlchemistsKit);
        ConfigureSpecialGear(CreateGear("FirstAidKit", "rel_122", "First-Aid Kit", "MEDIC",
            "Trigger: On Trick Use\nCondition: First trick used this encounter\nEffect: First trick per encounter also heals.\nValue: 3 HP\nLimit: First successful trick use per encounter", 20),
            "rel_122", "Common", 1, "", GearSpecialRule.FieldMedicsKit);

        ConfigureSpecialGear(CreateGear("CounterpunchGlovesEpic", "rel_111", "Counterpunch Gloves", "RETAL+",
            "Trigger: On Counterattack Kill\nCondition: Counterattack defeats enemy\nEffect: Keep Rare; counterattack kill permanently increases counterattack damage.\nValue: +1 counterattack damage\nLimit: Each lethal counterattack kill; run-local; new-run reset", 30),
            "rel_111", "Epic", 3, "rel_022", GearSpecialRule.RetaliationEpic);
        ConfigureSpecialGear(CreateGear("SurvivorsBadge", "rel_139", "Survivor's Badge", "CROWN",
            "Trigger: Headliner Defeated\nCondition: Headliner defeated\nEffect: Headliner kill permanently increases max HP; additional if encounter has zero-HP loss.\nValue: +3 Max HP; +3 additional on zero actual HP lost\nLimit: Once per Headliner kill; shield/block do not count as HP loss; max-HP gain does not heal", 20),
            "rel_139", "Common", 1, "", GearSpecialRule.CrownOfEndurance);
        ConfigureSpecialGear(CreateGear("SpotterEye", "rel_138", "Spotter Eye", "KING",
            "Trigger: On Attack\nCondition: Versus Headliner; 3 different ranks played\nEffect: Vs Headliner, play 3 different ranks to empower next attack; consume charge and repeat.\nValue: +1 damage to next attack\nLimit: Headliner only; 3 distinct manually played physical ranks; one charge per attack; encounter reset", 20),
            "rel_138", "Common", 1, "", GearSpecialRule.KingslayersMark);
        ConfigureSpecialGear(CreateGear("AceProtocolRare", "rel_114", "Ace Protocol", "ACE+",
            "Trigger: On Ace Pair Critical\nCondition: Ace pair critically strikes\nEffect: Keep Common crit chance; critical Ace-pair returns Ace to hand once per encounter.\nValue: Return 1 physical Ace to hand\nLimit: Once per encounter after a critical Ace pair; skip if hand is at capacity", 30),
            "rel_114", "Rare", 2, "rel_014", GearSpecialRule.AceRare);
        ConfigureSpecialGear(CreateGear("AceProtocolEpic", "rel_115", "Ace Protocol", "ACE++",
            "Trigger: On Ace Pair\nCondition: Ace paired with a Face card\nEffect: Keep Rare; pairing Ace with Face guarantees crit.\nValue: Guaranteed critical hit\nLimit: Ace paired with J (Jack)", 30),
            "rel_115", "Epic", 3, "rel_114", GearSpecialRule.AceEpic);
        ConfigureSpecialGear(CreateGear("DiamondDriverEpic", "rel_108", "Diamond Driver", "♦+",
            "Trigger: On Card Played\nCondition: Suit = Diamonds; every third manually played Diamond\nEffect: Keep Rare draw 2; every third manually played Diamond choose a card from the draw pile to draw.\nValue: Choose 1 physical draw pile card to put into hand\nLimit: Every third manually played Diamond; strict hand capacity; empty draw pile/full hand skips with log; cancel skips draw", 30),
            "rel_108", "Epic", 3, "rel_027", GearSpecialRule.DiamondEpic);
        ConfigureSpecialGear(CreateGear("HeadhunterRifle", "rel_137", "Headhunter Rifle", "TROPHY",
            "Trigger: On Enforcer Kill\nCondition: Enforcer defeated\nEffect: Enforcer kill permanently strengthens opening attack vs Headliner.\nValue: +1 opening-attack damage versus Headliner\nLimit: Each actual Enforcer kill; run-local; applies to first attack action of each Headliner encounter", 20),
            "rel_137", "Common", 1, "", GearSpecialRule.TrophyRack);
        ConfigureSpecialGear(CreateGear("NotchedPistol", "rel_119", "Notched Pistol", "HUNT",
            "Trigger: On Enforcer Kill\nCondition: Enforcer defeated\nEffect: Enforcer kill permanently strengthens first attack each encounter.\nValue: +1 opening-attack damage\nLimit: Each actual Enforcer kill; applies to first attack action of each encounter; run-local", 20),
            "rel_119", "Common", 1, "", GearSpecialRule.HuntersLedger);
        ConfigureSpecialGear(CreateGear("ModBenchRare", "rel_140", "Mod Bench", "HAMMER",
            "Trigger: After Every 3 Nodes\nCondition: Choose 1 of 3 Common Mods; eligible target card available\nEffect: Every 3 nodes, choose 1 of 3 Common Mods and apply it to a chosen eligible card instead of enhancing a random card.\nValue: Choose 1 of 3 cached Common Mods and 1 eligible card\nLimit: Every 3 completed nodes after acquisition; Retry/Decline if no eligible card; one Mod per card", 30),
            "rel_140", "Rare", 2, "rel_009", GearSpecialRule.Hammer);
        ConfigureSpecialGear(CreateGear("MemoryCardEpic", "rel_030", "Memory Card", "ARSENAL+",
            "Trigger: Passive\nCondition: Face Cards in hand\nEffect: Increase hand size based on the number of Face Cards in hand, with diminishing returns.\nValue: Hand capacity += floor(1 + 1/2 + ... + 1/n)\nLimit: n is Face cards in hand; no inherited lower-tier effects; no forced discard", 30),
            "rel_030", "Epic", 3, "rel_029", GearSpecialRule.ArsenalEpic);
        ConfigureSpecialGear(CreateGear("HeartMedsEpic", "rel_105", "Heart Meds", "♥+",
            "Trigger: On Card Played\nCondition: Suit = Hearts; overhealing\nEffect: Keep Rare healing/Credits; overhealing builds Vitality spent for bonus damage on next Heart attack.\nValue: Overheal becomes Vitality 1:1\nLimit: Retains Rare Heal 3 and conditional Credits 3; Heal Lock-prevented healing gives no Vitality", 30),
            "rel_105", "Epic", 3, "rel_026", GearSpecialRule.HeartEpic);
        ConfigureSpecialGear(CreateGear("EchoDriverEpic", "rel_116", "Echo Driver", "MIMIC+",
            "Trigger: While in Hand\nCondition: Held cards share matching rank\nEffect: Keep base; held matching-rank cards trigger in-hand effects one additional time.\nValue: Matching-rank held effects trigger +1 additional time\nLimit: Retains base all-held retrigger once; extra applies only to matching rank and eligible in-hand effects", 30),
            "rel_116", "Epic", 2, "rel_017", GearSpecialRule.MimicEpic);
        ConfigureSpecialGear(CreateGear("LowballRare", "rel_112", "Lowball", "LOW+",
            "Trigger: After a Played Turn\nCondition: Played rank < 5\nEffect: Keep Common bonus action; rank <5 played strengthens next higher-rank attack.\nValue: +1 damage to next attack with rank higher than prepared source\nLimit: After manually played rank below Five; retains Common bonus action", 30),
            "rel_112", "Rare", 2, "rel_013", GearSpecialRule.DwarfRare);
        ConfigureSpecialGear(CreateGear("LowballEpic", "rel_113", "Lowball", "LOW++",
            "Trigger: On Kill\nCondition: Prepared higher-rank attack kills enemy\nEffect: Keep Rare; prepared higher-rank attack kill permanently strengthens low-rank source.\nValue: +1 permanent attack to low-rank source card\nLimit: Prepared higher-rank attack must kill; source physical card must remain owned; run-local", 30),
            "rel_113", "Epic", 3, "rel_112", GearSpecialRule.DwarfEpic);
        ConfigureSpecialGear(CreateGear("DetourPin", "rel_135", "Detour Pin", "TRAIL",
            "Trigger: Before Node Entry\nCondition: Once per district; reachable hidden node\nEffect: Once/district reroll reachable hidden node to different hidden type before entry.\nValue: 1 hidden-node reroll\nLimit: Once per district; first reachable hidden Incident/Risk changes to the opposite type; deterministic; no Headliner/connectivity change", 20),
            "rel_135", "Common", 1, "", GearSpecialRule.HiddenTrail);
        ConfigureSpecialGear(CreateGear("CityBoots", "rel_133", "City Boots", "BOOTS",
            "Trigger: Node Visit\nCondition: 3 different node types visited this district\nEffect: Visit 3 different node types in district: heal and gain Credits once/district.\nValue: Heal 3 HP and gain 3 Credits\nLimit: Once per district after 3 distinct node types visited", 20),
            "rel_133", "Common", 1, "", GearSpecialRule.WanderersBoots);
        ConfigureSpecialGear(CreateGear("VarsityJacket", "rel_134", "Varsity Jacket", "WARPATH",
            "Trigger: Combat Start\nCondition: Consecutive combat wins\nEffect: Consecutive combat wins increase starting block next combat.\nValue: +2 encounter Block per consecutive Combat/Enforcer/Headliner victory\nLimit: Cap 6; reset by a completed noncombat node; consumed before Shield; expires at encounter end", 20),
            "rel_134", "Common", 1, "", GearSpecialRule.WarpathBanner);
        ConfigureSpecialGear(CreateGear("MatchedCellsEpic", "rel_109", "Matched Cells", "HANDS+",
            "Trigger: On Attack Selection\nCondition: Four cards of matching rank\nEffect: Keep Rare; allow four matching ranks; each card on-play effect triggers twice.\nValue: Allow 4 same-rank cards; each card’s Mod on-play effect triggers twice\nLimit: Retains Rare same-rank rule; no extra attack", 30),
            "rel_109", "Epic", 3, "rel_020", GearSpecialRule.HandsEpic);
        ConfigureSpecialGear(CreateGear("FlipSwitch", "rel_118", "Flip Switch", "SCALE",
            "Trigger: On Card Played\nCondition: Manually played card rank is low or Face\nEffect: Alternate manually playing low-rank and Face cards to empower attacks; repeating group resets bonus.\nValue: +1 damage to next attack on manual Low↔Face alternation\nLimit: Low is rank below Five; Face is Jack/Queen/King; consume once; repeating category clears before attack", 20),
            "rel_118", "Common", 1, "", GearSpecialRule.BalancersScale);
        ConfigureSpecialGear(CreateGear("SpadeShieldRare", "rel_106", "Spade Shield", "♠+",
            "Trigger: On Card Played\nCondition: Suit = Spades; one Spade fully blocks the attack\nEffect: Keep doubled block, over-block still count as Perfect Parry\nValue: Draw 1 card\nLimit: One Spade must fully block one attack; retains doubled Block", 30),
            "rel_106", "Rare", 2, "rel_004", GearSpecialRule.SpadeRare);
        ConfigureSpecialGear(CreateGear("SpadeShieldEpic", "rel_107", "Spade Shield", "♠++",
            "Trigger: On Card Played\nCondition: Suit = Spades; Spade block unused\nEffect: Keep Rare; unused Spade block adds damage to next attack.\nValue: +1 damage per unused Spade Block to next attack\nLimit: Retains Rare draw/doubled Block; encounter-local charge consumed by next attack", 30),
            "rel_107", "Epic", 3, "rel_106", GearSpecialRule.SpadeEpic);
        CopyUpgradeBaseData("rel_114", "rel_014");
        CopyUpgradeBaseData("rel_115", "rel_114");
        CopyUpgradeBaseData("rel_108", "rel_027");
        CopyUpgradeBaseData("rel_105", "rel_026");
        CopyUpgradeBaseData("rel_116", "rel_017");
        CopyUpgradeBaseData("rel_112", "rel_013");
        CopyUpgradeBaseData("rel_113", "rel_112");
        CopyUpgradeBaseData("rel_109", "rel_020");
        CopyUpgradeBaseData("rel_106", "rel_004");
        CopyUpgradeBaseData("rel_107", "rel_106");
        CreateMasterThief();
        Debug.Log("Gear assets created or updated.");
    }

    [MenuItem("Dealbreak/Content/Generate Mod Assets")]
    public static void GenerateEnhancements()
    {
        EnsureFolder(EnhancementDir);
        ConfigureCoreEnhancement(CreateEnhancement("Boosted", "sharpened", "Boosted", "ATK", "Trigger: On Card Played\nEffect: This card deals additional flat damage.\nValue: 3", 12),
            "enh_001", GameplayEffectKind.FlatAttack, GameplayEffectTrigger.AttackCalculated, 3);
        ConfigureCoreEnhancement(CreateEnhancement("Plated", "reinforced", "Plated", "DEF", "Trigger: On Discard\nEffect: This card provides additional block.\nValue: 3", 12),
            "enh_002", GameplayEffectKind.FlatBlock, GameplayEffectTrigger.DefenseCalculated, 3);
        ConfigureCoreEnhancement(CreateEnhancement("Regenerative", "mending", "Regenerative", "HP", "Trigger: While in Hand\nEffect: Heal HP.\nValue: 2", 15),
            "enh_003", GameplayEffectKind.Heal, GameplayEffectTrigger.PlayerTurnStart, 2);
        ConfigureCoreEnhancement(CreateEnhancement("Quickload", "quickdraw", "Quickload", "DRAW", "Trigger: On Card Played\nEffect: Draw a card.\nValue: 1", 18),
            "enh_004", GameplayEffectKind.Draw, GameplayEffectTrigger.CardCommitted, 1);
        ConfigureCoreEnhancement(CreateEnhancement("DoubleStrike", "double_strike", "Double Strike", "2×", "This card hits the chosen enemy twice, dealing its full resolved damage on each hit.", 20),
            "enh_005", GameplayEffectKind.DoubleStrike, GameplayEffectTrigger.AttackCalculated, 1);
        Debug.Log("Enhancement assets created or updated.");
    }

    [MenuItem("Dealbreak/Content/Generate Enemy Assets")]
    public static void GenerateEnemies()
    {
        EnsureFolder(EnemyDir);
        CreateEnemy("Pickpocket", "Pickpocket", 12, 1, 15, fleeAfterPlayerTurns: 2);
        CreateEnemy("Scrapper", "Scrapper", 3, 2, 3, encounterCount: 3);
        CreateEnemy("SecurityGuard", "Security Guard", 16, 4, 10);
        // Temporary 0 Credits rewards until the economy values in the workbook are approved.
        CreateEnemy("RiotGuard", "Riot Guard", 10, 3, 0);
        CreateEnemy("Heavy", "Heavy", 18, 3, 0);
        CreateEnemy("Challenger", "Challenger", 14, 4, 0);
        CreateEnemy("HypeMan", "Hype Man", 8, 1, 0);
        CreateEnemy("CrewLeader", "Crew Leader", 16, 3, 0);
        CreateEnemy("RiotCaptain", "Riot Captain", 32, 5, 0);
        CreateMasterThief();
        ConfigureEnemyAbility("Ghost", "PickpocketGetaway", "Pickpocket / Getaway",
            "After the first enemy attack, steal up to 8 Credits; flee after the player’s fourth action unless defeated.",
            EnemyAbilityEffect.MasterThief, 8);
        ConfigureEnemyAbility("RiotGuard", "StartingShield", "Starting Shield",
            "Begins each encounter with one Shield.", EnemyAbilityEffect.StartWithShield, 1);
        ConfigureEnemyAbility("Heavy", "WindUp", "Wind-Up",
            "Alternates normal attacks with announced double-damage attacks.",
            EnemyAbilityEffect.AlternateChargedAttack, 1);
        ConfigureEnemyAbility("Challenger", "SuitCallout", "Suit Callout",
            "At each player-turn start, announce a seeded suit. The first attack with that suit deals 50% damage, not immunity.",
            EnemyAbilityEffect.SuitCall, 1);
        ConfigureEnemyAbility("HypeMan", "HypeLoop", "Hype Loop",
            "After each enemy response, grant each ally +1 ATK for this encounter, up to +3. Does not buff self; death stops growth; fixed and not District-scaled.",
            EnemyAbilityEffect.WarDrum, 1);
        ConfigureEnemyAbility("CrewLeader", "CrewAura", "Crew Aura",
            "Two Scrapper allies gain +1 ATK while this Crew Leader lives. This bonus is not District-scaled.",
            EnemyAbilityEffect.Captaincy, 1);
        ConfigureEnemyAbility("RiotCaptain", "DefenseCycle", "Defense Cycle",
            "Alternates +1 Shield and a normal attack, then a telegraphed double-damage attack.",
            EnemyAbilityEffect.RoyalGuard, 1);
        Debug.Log("Enemy assets created.");
    }

    static GearData CreateGear(string name, string id, string displayName, string icon, string description, int price)
    {
        string path = $"{GearDir}/{name}.asset";
        var gear = AssetDatabase.LoadAssetAtPath<GearData>(path);
        if (gear == null)
        {
            gear = ScriptableObject.CreateInstance<GearData>();
            AssetDatabase.CreateAsset(gear, path);
        }

        gear.id = id;
        gear.displayName = displayName;
        gear.icon = icon;
        gear.description = description;
        gear.price = price;
        gear.restrictToSuit = false;
        gear.requiresEnhancedCard = false;
        gear.damageMultiplier = 1;
        gear.flatDamageBonus = 0;
        gear.defenseBonus = 0;
        gear.reduceEnemyAttackByCardValue = false;
        gear.recycleDiscardByCardValue = false;
        gear.drawByCardValue = false;
        gear.healAfterVictory = 0;
        gear.bonusCredits = 0;
        EditorUtility.SetDirty(gear);
        return gear;
    }

    static CardEnhancementData CreateEnhancement(string name, string id, string displayName, string icon, string description, int price)
    {
        string path = $"{EnhancementDir}/{name}.asset";
        var enhancement = AssetDatabase.LoadAssetAtPath<CardEnhancementData>(path);
        if (enhancement == null)
        {
            enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
            AssetDatabase.CreateAsset(enhancement, path);
        }

        enhancement.id = id;
        enhancement.displayName = displayName;
        enhancement.icon = icon;
        enhancement.description = description;
        enhancement.price = price;
        EditorUtility.SetDirty(enhancement);
        return enhancement;
    }

    static void CopyUpgradeBaseData(string upgradeId, string predecessorId)
    {
        var assets = AssetDatabase.FindAssets("t:GearData", new[] { GearDir })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GearData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(gear => gear != null).ToArray();
        var upgrade = assets.FirstOrDefault(gear => gear.canonicalId == upgradeId);
        var predecessor = assets.FirstOrDefault(gear => gear.canonicalId == predecessorId);
        if (upgrade == null || predecessor == null) return;
        upgrade.effects = predecessor.effects != null ? (GameplayEffectDefinition[])predecessor.effects.Clone() : System.Array.Empty<GameplayEffectDefinition>();
        upgrade.ruleModifiers = predecessor.ruleModifiers?.Where(rule => rule != null).Select(rule => new GameplayRuleModifierData
        {
            ruleId = rule.ruleId,
            kind = rule.kind,
            extraTurn = rule.extraTurn,
            sameRankMultiCard = rule.sameRankMultiCard
        }).ToArray() ?? System.Array.Empty<GameplayRuleModifierData>();
        if (upgradeId == "rel_109")
            foreach (var rule in upgrade.ruleModifiers)
                if (rule.kind == GameplayRuleModifierKind.SameRankMultiCard)
                    rule.sameRankMultiCard.maximumCards = 4;
        upgrade.restrictToSuit = predecessor.restrictToSuit;
        upgrade.affectedSuit = predecessor.affectedSuit;
        upgrade.requiresEnhancedCard = predecessor.requiresEnhancedCard;
        upgrade.damageMultiplier = predecessor.damageMultiplier;
        upgrade.flatDamageBonus = predecessor.flatDamageBonus;
        upgrade.defenseBonus = predecessor.defenseBonus;
        upgrade.reduceEnemyAttackByCardValue = predecessor.reduceEnemyAttackByCardValue;
        upgrade.recycleDiscardByCardValue = predecessor.recycleDiscardByCardValue;
        upgrade.drawByCardValue = predecessor.drawByCardValue;
        upgrade.healAfterVictory = predecessor.healAfterVictory;
        upgrade.bonusCredits = predecessor.bonusCredits;
        EditorUtility.SetDirty(upgrade);
    }

    static void ConfigureSpecialGear(GearData gear, string canonicalId, string rarity,
        int tier, string upgradeFromId, GearSpecialRule specialRule)
    {
        gear.canonicalId = canonicalId;
        gear.rarity = rarity;
        gear.tier = tier;
        gear.upgradeFromId = upgradeFromId;
        gear.specialRule = specialRule;
        gear.effects = System.Array.Empty<GameplayEffectDefinition>();
        gear.ruleModifiers = System.Array.Empty<GameplayRuleModifierData>();
        EditorUtility.SetDirty(gear);
    }

    static void ConfigureTypedGear(GearData gear, string canonicalId, string rarity,
        params GameplayEffectDefinition[] effects)
    {
        gear.canonicalId = canonicalId;
        gear.rarity = rarity;
        gear.tier = 1;
        gear.upgradeFromId = "";
        gear.effects = effects;
        gear.ruleModifiers = System.Array.Empty<GameplayRuleModifierData>();
        EditorUtility.SetDirty(gear);
    }

    static void ConfigureCoreGear(GearData gear, string canonicalId,
        GameplayEffectKind kind, GameplayEffectTrigger trigger, int amount, CardData.Suit suit)
    {
        gear.canonicalId = canonicalId;
        gear.rarity = "Common";
        gear.tier = 1;
        gear.upgradeFromId = "";
        gear.restrictToSuit = true;
        gear.affectedSuit = suit;
        gear.effects = new[] { new GameplayEffectDefinition
        {
            kind = kind, trigger = trigger, amount = amount,
            conditions = new[] { new GameplayEffectCondition
            {
                kind = GameplayConditionKind.CardSuit, suit = suit
            } }
        } };
        EditorUtility.SetDirty(gear);
    }

    static void ConfigureCoreEnhancement(CardEnhancementData enhancement, string canonicalId,
        GameplayEffectKind kind, GameplayEffectTrigger trigger, int amount)
    {
        enhancement.canonicalId = canonicalId;
        enhancement.rarity = "Common";
        enhancement.tier = 1;
        enhancement.upgradeFromId = "";
        enhancement.attackBonus = 0;
        enhancement.defenseBonus = 0;
        enhancement.healOnPlay = 0;
        enhancement.drawOnPlay = 0;
        enhancement.effects = new[] { new GameplayEffectDefinition
        {
            kind = kind, trigger = trigger, amount = amount
        } };
        EditorUtility.SetDirty(enhancement);
    }

    static void CreateMasterThief()
    {
        const string path = "Assets/Data/Enemies/Ghost.asset";
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyTypeData>(path);
        if (enemy == null)
        {
            enemy = ScriptableObject.CreateInstance<EnemyTypeData>();
            AssetDatabase.CreateAsset(enemy, path);
        }
        enemy.enemyName = "Ghost";
        enemy.maxHp = 24;
        enemy.baseAttack = 4;
        enemy.creditsReward = 20; // Owner-approved normal Elite reward; stolen Credits are refunded separately on defeat.
        enemy.encounterCount = 1;
        enemy.fleeAfterPlayerTurns = 4;
        EditorUtility.SetDirty(enemy);
    }

    static void CreateEnemy(
        string name,
        string displayName,
        int hp,
        int atk,
        int credits,
        int encounterCount = 1,
        int fleeAfterPlayerTurns = 0)
    {
        var path = $"{EnemyDir}/{name}.asset";
        if (File.Exists(path)) return;
        var enemy = ScriptableObject.CreateInstance<EnemyTypeData>();
        enemy.enemyName = displayName;
        enemy.maxHp = hp;
        enemy.baseAttack = atk;
        enemy.creditsReward = credits;
        enemy.encounterCount = encounterCount;
        enemy.fleeAfterPlayerTurns = fleeAfterPlayerTurns;
        AssetDatabase.CreateAsset(enemy, path);
    }

    static void ConfigureEnemyAbility(string enemyAssetName, string abilityAssetName,
        string displayName, string description, EnemyAbilityEffect effect, int amount)
    {
        string folder = $"{EnemyDir}/Abilities";
        EnsureFolder(folder);
        string path = $"{folder}/{abilityAssetName}.asset";
        var ability = AssetDatabase.LoadAssetAtPath<EnemyAbility>(path);
        if (ability == null)
        {
            ability = ScriptableObject.CreateInstance<EnemyAbility>();
            AssetDatabase.CreateAsset(ability, path);
        }
        ability.id = abilityAssetName;
        ability.displayName = displayName;
        ability.description = description;
        ability.effect = effect;
        ability.amount = amount;
        EditorUtility.SetDirty(ability);

        var enemy = AssetDatabase.LoadAssetAtPath<EnemyTypeData>($"{EnemyDir}/{enemyAssetName}.asset");
        if (enemy == null) return;
        enemy.abilities = new[] { ability };
        EditorUtility.SetDirty(enemy);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(parent));
        AssetDatabase.CreateFolder(parent, folder);
    }
}
