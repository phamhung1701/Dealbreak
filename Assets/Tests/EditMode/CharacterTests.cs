using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// Playable characters (owner, 2026-10-24): one simple perk each, picked before a run. Start-of-run
// perks apply once; capacity perks are read live and come back with the save.
public sealed class CharacterTests
{
    readonly List<GameObject> objects = new();
    readonly List<Object> assets = new();
    CardManager cards;
    CombatManager combat;
    RunManager run;
    SaveContentCatalog catalog;
    const string Seed = "CHARACTER-TEST";

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear(); DevModeRuntime.Disable(); FrontendLaunchContext.Clear();
        var authoredCatalog = SaveContentCatalog.Load(); Assert.That(authoredCatalog, Is.Not.Null);
        catalog = Object.Instantiate(authoredCatalog); assets.Add(catalog);
        cards = Make<CardManager>();
        cards.Configure(null, null, null, catalog.gearItems, catalog.enhancements, catalog.consumables);
        combat = Make<CombatManager>(); combat.ConfigurePlayer(30);
        run = Make<RunManager>(); run.contentCatalog = catalog.runContent;
        run.thiefType = catalog.fallbackEnemies[0]; run.goblinType = catalog.fallbackEnemies[1]; run.knightType = catalog.fallbackEnemies[2];
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i]) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        foreach (var asset in assets) if (asset) Object.DestroyImmediate(asset);
        assets.Clear();
        GameplayInputGate.Clear(); FrontendLaunchContext.Clear();
    }

    T Make<T>() where T : Component
    {
        var go = new GameObject(typeof(T).Name); objects.Add(go); return go.AddComponent<T>();
    }

    CharacterData Character(string id)
    {
        var character = catalog.Character(id);
        Assert.That(character, Is.Not.Null, id);
        return character;
    }

    static RunSaveData JsonClone(RunSaveData data) => JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(data));

    // The cards stream's deal; Gear may change the map on purpose (scouting, routes), so it is left out.
    static string Snapshot(CardManager cards) =>
        string.Join(",", cards.hand.Concat(cards.deck).Select(card => $"{card.Rank}{card.Suit}"));

    [Test]
    public void EachCharacter_HasExactlyOnePerk_AndItsTextQuotesThatPerk()
    {
        foreach (var character in catalog.characters)
        {
            var perks = new List<string>();
            if (character.maxHealthBonus > 0) perks.Add($"+{character.maxHealthBonus} max HP");
            if (character.handSizeBonus > 0) perks.Add($"+{character.handSizeBonus} hand size");
            if (character.gearSlotBonus > 0) perks.Add($"+{character.gearSlotBonus} Gear slot");
            if (character.trickSlotBonus > 0) perks.Add($"+{character.trickSlotBonus} Trick slot");
            if (character.startingCredits > 0) perks.Add($"{character.startingCredits} Credits");
            if (!string.IsNullOrWhiteSpace(character.startingGearRarity)) perks.Add($"{character.startingGearRarity} Gear");
            Assert.That(perks, Has.Count.EqualTo(1), character.displayName);
            Assert.That(character.description, Does.Contain(perks[0]), character.displayName);
        }
    }

    [Test]
    public void Rook_StartsAtFullBonusHealth_AndARunWithoutRookDoesNotKeepIt()
    {
        run.StartRunWithSeed(Seed, Character("chr_rook"));
        Assert.That((combat.player.currentHealth, combat.player.maxHealth), Is.EqualTo((40, 40)));

        run.StartRunWithSeed(Seed, null);
        Assert.That((combat.player.currentHealth, combat.player.maxHealth), Is.EqualTo((30, 30)));
    }

    [Test]
    public void Scarlet_DealsAndHoldsOneMoreCard()
    {
        run.StartRunWithSeed(Seed, Character("chr_scarlet"));
        Assert.That(cards.HandCapacity, Is.EqualTo(CardManager.HAND_SIZE + 1));
        Assert.That(cards.hand, Has.Count.EqualTo(CardManager.HAND_SIZE + 1));
    }

    [Test]
    public void Dex_HoldsOneMoreGear()
    {
        run.StartRunWithSeed(Seed, Character("chr_dex"));
        Assert.That(cards.GearCapacity, Is.EqualTo(CardManager.BASE_GEAR_CAPACITY + 1));
        var commons = cards.gearCatalog.Where(gear => gear.rarity == "Common" && gear.tier <= 1 &&
                string.IsNullOrEmpty(gear.upgradeFromId) && gear.OccupiesCapacitySlot)
            .OrderBy(gear => gear.id, StringComparer.Ordinal).Take(CardManager.BASE_GEAR_CAPACITY + 2).ToList();
        for (int i = 0; i <= CardManager.BASE_GEAR_CAPACITY; i++)
            Assert.That(cards.GrantGearReward(commons[i]), Is.True, commons[i].id);
        Assert.That(cards.GrantGearReward(commons[^1]), Is.False, "The sixth slot is the last one.");
    }

    [Test]
    public void Nova_TrickSlotStacksOnCargoCase()
    {
        run.StartRunWithSeed(Seed, Character("chr_nova"));
        Assert.That(cards.BackpackCapacity, Is.EqualTo(CardManager.BASE_BACKPACK_CAPACITY + 1));
        var cargoCase = cards.gearCatalog.First(gear => gear.specialRule == GearSpecialRule.TravelerPackCommon);
        Assert.That(cards.GrantGearReward(cargoCase), Is.True);
        Assert.That(cards.BackpackCapacity, Is.EqualTo(GameBalance.Current.gear.cargoCaseBackpackSlots + 1));
    }

    [Test]
    public void Velvet_StartsWithHerCredits()
    {
        run.StartRunWithSeed(Seed, Character("chr_velvet"));
        Assert.That(cards.credits, Is.EqualTo(15));
    }

    [Test]
    public void Glitch_StartsWithOneSeededCommonGear_AndShiftsNoOtherRandomness()
    {
        run.StartRunWithSeed(Seed, null);
        string plain = Snapshot(cards);

        run.StartRunWithSeed(Seed, Character("chr_glitch"));
        Assert.That(cards.ownedGear, Has.Count.EqualTo(1));
        var gear = cards.ownedGear[0];
        Assert.That((gear.rarity, gear.tier, gear.upgradeFromId ?? string.Empty), Is.EqualTo(("Common", 1, string.Empty)));
        Assert.That(Snapshot(cards), Is.EqualTo(plain), "The deal ignores the character.");

        run.StartRunWithSeed(Seed, Character("chr_glitch"));
        Assert.That(cards.ownedGear.Single(), Is.SameAs(gear), "The same seed gives the same Gear.");
    }

    [Test]
    public void SaveRoundTrip_KeepsTheCharacterAndItsSlots()
    {
        run.StartRunWithSeed(Seed, Character("chr_nova"));
        var saved = run.CaptureSave();
        Assert.That(saved.cards.character, Is.EqualTo("chr_nova"));

        run.StartRunWithSeed(Seed, null);
        Assert.That(RunSaveValidation.Validate(JsonClone(saved), catalog, out var error), Is.True, error);
        run.RestoreSave(JsonClone(saved), catalog);
        Assert.That(cards.Character, Is.SameAs(Character("chr_nova")));
        Assert.That(cards.BackpackCapacity, Is.EqualTo(CardManager.BASE_BACKPACK_CAPACITY + 1));
        Assert.That(JsonUtility.ToJson(run.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(saved)));
    }

    [Test]
    public void Validation_RejectsAnUnknownCharacter()
    {
        run.StartRunWithSeed(Seed, Character("chr_dex"));
        var saved = JsonClone(run.CaptureSave());
        saved.cards.character = "chr_nobody";
        Assert.That(RunSaveValidation.Validate(saved, catalog, out var error), Is.False);
        Assert.That(error, Does.Contain("character"));
    }

    [Test]
    public void MigrationFromVersion5_ContinuesWithoutACharacter()
    {
        run.StartRunWithSeed(Seed, null);
        var v5 = JsonClone(run.CaptureSave());
        v5.version = 5;
        v5.cards.character = null;

        Assert.That(RunSaveMigrations.TryUpgrade(v5, out var error), Is.True, error);
        Assert.That(v5.version, Is.EqualTo(RunSaveValidation.Version));
        Assert.That(v5.cards.character, Is.Empty);
        Assert.That(RunSaveValidation.Validate(v5, catalog, out error), Is.True, error);
        run.RestoreSave(v5, catalog);
        Assert.That(cards.Character, Is.Null);
        Assert.That(cards.HandCapacity, Is.EqualTo(CardManager.HAND_SIZE));
    }

    [Test]
    public void NewRunHandoff_CarriesThePickedCharacter()
    {
        FrontendLaunchContext.RequestRun("seed", "chr_scarlet");
        Assert.That(FrontendLaunchContext.TryConsumeRunSeed(out var seed, out var characterId), Is.True);
        Assert.That((seed, characterId), Is.EqualTo((RunRandomContext.NormalizeSeed("seed"), "chr_scarlet")));
        Assert.That(FrontendLaunchContext.TryConsumeRunSeed(out _, out characterId), Is.False);
        Assert.That(characterId, Is.Null);
    }

    [Test]
    public void RestartingARun_KeepsItsCharacter()
    {
        run.StartRunWithSeed(Seed, Character("chr_scarlet"));
        run.RestartRun();
        Assert.That(cards.Character, Is.SameAs(Character("chr_scarlet")));
        Assert.That(cards.hand, Has.Count.EqualTo(CardManager.HAND_SIZE + 1));
    }

    [Test]
    public void Avatar_ShowsTheRunsCharacter_OrTheFirstLookWithNoDetail()
    {
        var rook = Character("chr_rook");
        var nova = Character("chr_nova");
        Assert.That(PlayerAvatarUI.PortraitFor(nova), Is.SameAs(nova.portrait));
        Assert.That(PlayerAvatarUI.PortraitFor(null), Is.SameAs(catalog.characters[0].portrait));
        Assert.That(catalog.characters[0], Is.SameAs(rook));
        Assert.That(PlayerAvatarUI.Describe(null), Is.Null);
        var detail = PlayerAvatarUI.Describe(nova);
        Assert.That(detail.HasValue, Is.True);
        Assert.That((detail.Value.Title, detail.Value.Body), Is.EqualTo((nova.displayName, nova.description)));
    }
}
