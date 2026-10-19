using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PersistentRunSaveTests
{
    readonly List<GameObject> objects = new();
    readonly List<Object> assets = new();
    CardManager cards;
    CombatManager combat;
    RunManager run;
    SaveContentCatalog catalog;
    RunSaveStore store;
    string directory;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

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
        run.StartRunWithSeed("PERSISTENT-RUN-TEST");
        directory = System.IO.Path.Combine(Application.temporaryCachePath, "DealbreakSaveTests", Guid.NewGuid().ToString("N"));
        store = new RunSaveStore(System.IO.Path.Combine(directory, "active-run.json"));
    }
    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i]) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        foreach (var asset in assets) if (asset) Object.DestroyImmediate(asset);
        assets.Clear();
        typeof(SaveGameService).GetField("StoreOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GameplayInputGate.Clear(); FrontendLaunchContext.Clear();
    }
    T Make<T>() where T : Component
    {
        var go = new GameObject(typeof(T).Name); objects.Add(go); return go.AddComponent<T>();
    }
    static RunSaveData JsonClone(RunSaveData data) => JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(data));
    void Write(RunSaveData data) => Assert.That(store.Write(data, catalog, out var error), Is.True, error);
    RunSaveData Read()
    {
        Assert.That(store.TryRead(catalog, out var data, out var error), Is.True, error); return data;
    }

    [Test]
    public void JsonAndDiskRoundTrip_PreservesAllInitialAuthoritativeState()
    {
        var saved = run.CaptureSave(); Write(saved);
        run.RestoreSave(Read(), catalog);
        Assert.That(JsonUtility.ToJson(run.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(saved)));
        Assert.That(run.ActiveContract, Is.Null, "JsonUtility must not fabricate an empty Contract.");
    }

    [Test]
    public void Cards_PreserveStableIdsSuitOverridesEnhancementsCountersAndEveryZoneOrder()
    {
        var card = cards.ownedCards[0];
        cards.ApplyEnhancement(card.Id, catalog.enhancements[0]);
        card.TryChangeSuit((CardData.Suit)(((int)card.Suit + 1) % 4)); card.GainPermanentAttackBonus(7);
        card.EffectState.SetCounter(-80, 9);
        var collection = (CardCollection)typeof(CardManager).GetField("_cards", Private).GetValue(cards);
        collection.TryDiscard(cards.hand.Take(2).ToArray()); cards.ShuffleDeck();
        var saved = run.CaptureSave(); var expected = JsonUtility.ToJson(saved.cards);
        cards.DrawToHand(3); cards.ShuffleDeck();
        run.RestoreSave(JsonClone(saved), catalog);
        Assert.That(JsonUtility.ToJson(cards.CaptureSave()), Is.EqualTo(expected));
        Assert.That(cards.FindOwnedCard(card.Id), Is.Not.SameAs(card));
        Assert.That(cards.FindOwnedCard(card.Id).PermanentAttackBonus, Is.EqualTo(7));
        Assert.That(cards.FindOwnedCard(card.Id).EffectState.GetCounter(-80), Is.EqualTo(9));
    }

    [Test]
    public void AutoClaimedEnemyDrop_PersistsInventoryAndResolvedNodeContextWithoutDuplicateGrant()
    {
        const int nodeId = 43;
        const int mapIndex = 2;
        var candidates = cards.consumableCatalog
            .Where(item => item != null && item.dropAvailable && item.dropChance > 0f &&
                item.effectType != ConsumableEffectType.ApplyEnhancement)
            .OrderBy(item => item.id, StringComparer.Ordinal).ToArray();
        var chosen = (ConsumableData)null;
        string seed = null;
        int context = unchecked(mapIndex * 7919 ^ nodeId);
        for (int attempt = 0; attempt < 10000 && chosen == null; attempt++)
        {
            string candidateSeed = "AUTO-DROP-SAVE-" + attempt;
            var random = new RunRandomContext(candidateSeed).CreateStream("consumable-drops", context);
            foreach (var candidate in candidates)
                if (random.NextFloat() < Mathf.Clamp01(candidate.dropChance))
                {
                    chosen = candidate;
                    seed = candidateSeed;
                    break;
                }
        }
        Assert.That(chosen, Is.Not.Null, "A seeded eligible drop should be found.");
        run.StartRunWithSeed(seed);
        var node = new PathNode { id = nodeId, mapIndex = mapIndex, kind = MapNodeType.Combat };
        var grant = typeof(RunManager).GetMethod("TryGrantConsumableDropForNode", Private);
        Assert.That((bool)grant.Invoke(run, new object[] { node }), Is.True);
        Assert.That(run.PendingConsumableReward, Is.Null);
        Assert.That(cards.GetConsumableCount(chosen), Is.EqualTo(1));

        var checkpoint = run.CaptureSave();
        Write(checkpoint);
        run.RestoreSave(JsonClone(Read()), catalog);

        Assert.That(cards.GetConsumableCount(chosen), Is.EqualTo(1));
        Assert.That((bool)grant.Invoke(run, new object[] { node }), Is.False,
            "The saved resolved-node context prevents a restart or duplicate event from granting a second drop.");
        Assert.That(cards.GetConsumableCount(chosen), Is.EqualTo(1));
        Assert.That(run.PendingConsumableReward, Is.Null);
    }

    [Test]
    public void EnhancementTrickUse_RestoresExactCardModAndRemainingChargeCount()
    {
        var patch = ScriptableObject.CreateInstance<ConsumableData>();
        assets.Add(patch);
        patch.id = "save-enhancement-patch";
        patch.canonicalId = "con_save_enhancement_patch";
        patch.displayName = "Save Enhancement Patch";
        patch.effectType = ConsumableEffectType.ApplyEnhancement;
        patch.enhancementToApply = catalog.enhancements[0];
        patch.uses = 3;
        patch.shopAvailable = true;
        catalog.consumables = catalog.consumables.Concat(new[] { patch }).ToArray();
        cards.consumableCatalog.Add(patch);
        var target = cards.hand[0];
        int targetId = target.Id;
        Assert.That(cards.AddConsumable(patch), Is.True);

        Assert.That(cards.UseEnhancementConsumableAtSlot(0, targetId), Is.True);
        Assert.That(target.Enhancement, Is.SameAs(catalog.enhancements[0]));
        Assert.That(cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(2));
        var saved = run.CaptureSave();
        Write(saved);

        run.RestoreSave(JsonClone(Read()), catalog);

        var restoredTarget = cards.FindOwnedCard(targetId);
        Assert.That(restoredTarget, Is.Not.Null);
        Assert.That(restoredTarget.Enhancement, Is.SameAs(catalog.enhancements[0]));
        Assert.That(cards.GetConsumableInstanceAtSlot(0).Definition, Is.SameAs(patch));
        Assert.That(cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(2));
    }

    [Test]
    public void StatefulRandomStreams_ContinueAtExactSavedPositions()
    {
        var critical = (IRandomSource)typeof(CombatManager).GetField("_criticalRandom", Private).GetValue(combat);
        critical.NextFloat(); critical.NextFloat(); cards.ShuffleDeck();
        var saved = run.CaptureSave();
        int expectedCritical = critical.NextInt(0, 100000);
        cards.ShuffleDeck(); var expectedDeck = cards.deck.Select(x => x.Id).ToArray();
        run.RestoreSave(JsonClone(saved), catalog);
        var restoredCritical = (IRandomSource)typeof(CombatManager).GetField("_criticalRandom", Private).GetValue(combat);
        Assert.That(restoredCritical.NextInt(0, 100000), Is.EqualTo(expectedCritical));
        cards.ShuffleDeck(); Assert.That(cards.deck.Select(x => x.Id), Is.EqualTo(expectedDeck));
        var duelist = DeterministicRandom.Restore(saved.duelistRandom);
        var restoredDuelist = (IRandomSource)typeof(CombatManager).GetField("_duelistSuitRandom", Private).GetValue(combat);
        Assert.That(restoredDuelist.NextInt(0, 4), Is.EqualTo(duelist.NextInt(0, 4)));
    }

    [Test]
    public void MapHeadlinersAndTiming_RestoreWithoutGenerationOrProgressionEvents()
    {
        var saved = run.CaptureSave(); saved.bossIndex = 3; saved.timing.mapIndex = 3;
        saved.timing.totalElapsed = 222; saved.timing.mapElapsed = 21;
        foreach (var node in saved.nodes) node.mapIndex = 3;
        saved.nodes[0].completed = true; saved.nodes[0].accessible = false; saved.completedNodeCount = 7;
        int rewards = 0, results = 0;
        run.OnHeadlinerRewardGranted += _ => rewards++;
        combat.OnEncounterResult += _ => results++;
        run.RestoreSave(JsonClone(saved), catalog); run.PresentRestoredSave();
        Assert.That(run.CurrentMapIndex, Is.EqualTo(3)); Assert.That(run.CompletedNodeCount, Is.EqualTo(7));
        Assert.That(run.currentPath[0].completed, Is.True);
        Assert.That(run.CurrentHeadliner.sourceCard.suit, Is.EqualTo((CardData.Suit)saved.bosses[3].suit));
        Assert.That(run.Timing.TotalElapsedSeconds, Is.EqualTo(222));
        Assert.That(rewards, Is.Zero); Assert.That(results, Is.Zero);
    }

    [Test]
    public void GearAndConsumables_RestoreAcquisitionOrderIdsCountersAndCharges()
    {
        cards.BuyGear(catalog.gearItems[0], 0); cards.BuyGear(catalog.gearItems[1], 0);
        var gear = cards.GetGearInstanceAt(0); gear.State.SetCounter(-991, 42);
        gear.State.SetEncounterCounter(4, 3);
        gear.PreparedDwarfSourceCard = cards.ownedCards[0];
        var rune = ScriptableObject.CreateInstance<ConsumableData>(); assets.Add(rune);
        rune.id = "save-charge-fixture"; rune.uses = 3;
        catalog.consumables = catalog.consumables.Concat(new[] { rune }).ToArray();
        cards.consumableCatalog.Add(rune);
        Assert.That(cards.AddConsumable(rune), Is.True); cards.GetConsumableInstanceAtSlot(0).SpendCharge();
        var saved = run.CaptureSave();
        run.RestoreSave(JsonClone(saved), catalog);
        Assert.That(cards.GetGearInstanceAt(0).Id, Is.EqualTo(gear.Id));
        Assert.That(cards.GetGearInstanceAt(0).State.GetCounter(-991), Is.EqualTo(42));
        Assert.That(cards.GetGearInstanceAt(0).State.GetEncounterCounter(4), Is.EqualTo(3));
        Assert.That(cards.GetGearInstanceAt(0).PreparedDwarfSourceCard.Id, Is.EqualTo(saved.cards.owned[0].id));
        Assert.That(cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(rune.uses - 1));
        Assert.That(cards.BuyGear(catalog.gearItems[2], 0), Is.True);
        Assert.That(cards.GetGearInstanceAt(2).Id, Is.EqualTo(saved.cards.artifactIdWatermark + 1));
    }

    [Test]
    public void ShopInventoryRefreshPurchaseAndServiceState_RestoreWithoutDuplicatePaymentOrSettlement()
    {
        cards.AddCredits(200);
        var shop = run.currentPath.First(n => n.kind == MapNodeType.Shop); shop.accessible = true; run.OnPathChosen(shop.id);
        var gear = run.GetCurrentShopOffers().First(o => o.kind == ShopOfferKind.Gear);
        Assert.That(run.PurchaseShopOffer(gear.StableId), Is.True);
        Assert.That(run.RefreshGoods(0), Is.True);
        var saved = run.CaptureSave();
        saved.investmentStakes.Add(5);
        saved.shops.Single().usedServices.Add((int)ShopOfferKind.Supply);
        int credits = cards.credits, settlements = 0; run.OnInvestmentResult += _ => settlements++;
        run.RestoreSave(JsonClone(saved), catalog); run.PresentRestoredSave();
        Assert.That(cards.credits, Is.EqualTo(credits)); Assert.That(settlements, Is.Zero);
        Assert.That(run.PendingInvestmentCount, Is.EqualTo(1)); Assert.That(run.GoodsRefreshCount, Is.EqualTo(1));
        Assert.That(run.GoodsRefreshPrice, Is.EqualTo(5));
        Assert.That(run.PurchaseShopOffer(gear.StableId), Is.False);
        Assert.That(run.RefreshGoods(0), Is.False);
        Assert.That(run.PurchaseSupply("supply", 1), Is.False);
        Assert.That(JsonUtility.ToJson(run.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(saved)));
    }

    [Test]
    public void QuestConfirmation_CompletesShopStateAndProducesAValidCheckpoint()
    {
        cards.AddCredits(100);
        var node = run.currentPath.First(n => n.kind == MapNodeType.Shop); node.accessible = true; run.OnPathChosen(node.id);
        var offers = (List<ShopOffer>)run.GetCurrentShopOffers(); offers.RemoveAll(o => o.IsService);
        offers.Add(new ShopOffer { kind = ShopOfferKind.Investment, price = 5, basePrice = 5, investmentStake = 5 });
        offers.Add(new ShopOffer { kind = ShopOfferKind.Contract, price = 20, basePrice = 20 });
        run.OnShowEvent += _ => { };
        Assert.That(run.PurchaseShopOffer("investment"), Is.True);
        Assert.That(run.PurchaseShopOffer("contract:quest"), Is.True);
        Assert.That(run.CanCaptureSave, Is.False, "Objective selection is an unfinished transaction.");
        Assert.That(run.ChooseEventOption(0), Is.True);
        var saved = run.CaptureSave();
        Assert.That(saved.shops.Single().completed, Is.True);
        Assert.That(node.completed, Is.True);
        Assert.That(saved.hasContract, Is.True);
        Write(saved);
        run.RestoreSave(Read(), catalog);
        Assert.That(run.ActiveContract, Is.Not.Null);
        Assert.That(run.currentPath.Single(n => n.id == node.id).completed, Is.True);
    }

    [Test]
    public void ContractCountersAndRewardGuards_RoundTripWithoutEffects()
    {
        var saved = run.CaptureSave();
        saved.hasContract = true;
        saved.contract = new ContractSaveData { objective = 0, startedMap = 0, startedNode = 16, deadlineMap = 12, encounterWins = 1, stableId = "quest-saved" };
        saved.consumableDropContexts.Add(17); saved.resolvedRewardContexts.Add("saved-reward");
        saved.paidItemPurchases = 2; saved.combatWins = 3; saved.merchantGiftRewards = 1;
        run.RestoreSave(JsonClone(saved), catalog);
        Assert.That(run.ActiveContract.encounterWins, Is.EqualTo(1));
        Assert.That(JsonUtility.ToJson(run.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(saved)));
    }

    [Test]
    public void CombatAndTemporaryPrompts_CannotReplaceSafeCheckpoint()
    {
        var saved = run.CaptureSave(); Write(saved);
        GameplayInputGate.Set(GameplayInputBlockReason.GearChoice, true);
        Assert.That(run.CanCaptureSave, Is.False);
        Assert.Throws<InvalidOperationException>(() => run.CaptureSave());
        GameplayInputGate.Clear();
        var node = run.currentPath.First(n => n.accessible && n.kind == MapNodeType.Combat);
        run.OnPathChosen(node.id);
        Assert.That(run.CanCaptureSave, Is.False);
        Assert.That(JsonUtility.ToJson(Read()), Is.EqualTo(JsonUtility.ToJson(saved)));
    }

    [Test]
    public void MissingSave_IsGracefulAndCreatesNoFiles()
    {
        Assert.That(store.TryRead(catalog, out _, out var error), Is.False);
        Assert.That(error, Does.Contain("No active")); Assert.That(Directory.Exists(directory), Is.False);
    }

    [Test]
    public void CorruptedPrimary_RecoversLastValidBackup()
    {
        var first = run.CaptureSave(); Write(first);
        cards.AddCredits(9); Write(run.CaptureSave());
        File.WriteAllText(store.Path, "{broken");
        Assert.That(Read().cards.gold, Is.EqualTo(first.cards.gold));
        Assert.That(store.RecoveredFromBackup, Is.True);
    }

    [Test]
    public void WritingWhenPrimaryIsMissing_PreservesExistingRecoveryCopy()
    {
        var first = run.CaptureSave(); Write(first);
        File.Delete(store.Path);
        var next = JsonClone(first); next.cards.gold = 11; Write(next);
        File.WriteAllText(store.Path, "broken");
        Assert.That(Read().cards.gold, Is.EqualTo(first.cards.gold));
        Assert.That(store.RecoveredFromBackup, Is.True);
    }

    [Test]
    public void CorruptedBothFiles_DisablesLoad_AndDoesNotMutateRuntime()
    {
        Write(run.CaptureSave()); File.WriteAllText(store.Path, "broken"); File.WriteAllText(store.BackupPath, "broken");
        string before = JsonUtility.ToJson(run.CaptureSave());
        Assert.That(store.TryRead(catalog, out _, out _), Is.False);
        Assert.That(JsonUtility.ToJson(run.CaptureSave()), Is.EqualTo(before));
    }

    [Test]
    public void IncompatibleAndInvalidSnapshots_AreRejectedBeforeMutation()
    {
        var saved = run.CaptureSave(); var before = JsonUtility.ToJson(saved);
        saved.version++;
        Assert.That(store.Write(saved, catalog, out _), Is.False);
        Assert.Throws<ArgumentException>(() => run.RestoreSave(saved, catalog));
        saved.version--; saved.cards.deck.Add(saved.cards.hand[0]);
        Assert.Throws<ArgumentException>(() => run.RestoreSave(saved, catalog));
        Assert.That(JsonUtility.ToJson(run.CaptureSave()), Is.EqualTo(before));
    }

    [Test]
    public void IncompatibleVersionOnDisk_DisablesLoadEvenWithValidChecksum()
    {
        var saved = run.CaptureSave(); saved.version++;
        string payload = JsonUtility.ToJson(saved);
        string checksum;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            checksum = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).Replace("-", string.Empty);
        Directory.CreateDirectory(directory);
        File.WriteAllText(store.Path, JsonUtility.ToJson(new RunSaveEnvelope { checksum = checksum, payload = payload }));
        Assert.That(store.TryRead(catalog, out _, out var error), Is.False);
        Assert.That(error, Does.Contain("newer than this build supports"));
    }

    [Test]
    public void UnknownContentId_IsRejectedWithoutFallbackOrReroll()
    {
        var saved = run.CaptureSave(); saved.cards.owned[0].enhancementId = "missing-content";
        Assert.That(RunSaveValidation.Validate(saved, catalog, out var error), Is.False);
        Assert.That(error, Does.Contain("Unknown Enhancement"));
    }

    [Test]
    public void Delete_RemovesPrimaryBackupAndTemp_AndTombstonePreventsResurrection()
    {
        Write(run.CaptureSave()); File.WriteAllText(store.TemporaryPath, "interrupted");
        Assert.That(store.Delete(out _), Is.True);
        Assert.That(File.Exists(store.Path) || File.Exists(store.BackupPath) || File.Exists(store.TemporaryPath), Is.False);
        Assert.That(store.TryRead(catalog, out _, out _), Is.False);
        Assert.That(File.Exists(store.DeletedPath), Is.True);
        Write(run.CaptureSave()); Assert.That(store.TryRead(catalog, out _, out _), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TerminalVictoryOrDefeat_DeletesSaveAndPreventsAutosaveResurrection(bool victory)
    {
        typeof(SaveGameService).GetField("StoreOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, store);
        Write(run.CaptureSave());
        var writer = run.gameObject.AddComponent<RunSaveAutosave>();
        if (typeof(RunSaveAutosave).GetField("_run", Private).GetValue(writer) == null)
            typeof(RunSaveAutosave).GetMethod("OnEnable", Private).Invoke(writer, null);
        if (victory)
        {
            var callback = (Action)typeof(RunManager).GetField("OnRunCompleted", Private).GetValue(run); callback.Invoke();
        }
        else
        {
            var callback = (Action<EncounterResult>)typeof(CombatManager).GetField("OnEncounterResult", Private).GetValue(combat);
            callback.Invoke(EncounterResult.Defeat);
        }
        Assert.That(store.TryRead(catalog, out _, out _), Is.False);
        Assert.That(writer.Checkpoint(true), Is.False);
    }

    [Test]
    public void Migrations_UpgradeOlderVersionsStepwise_AndRejectNewerOrUnknownVersions()
    {
        var steps = new Dictionary<int, Action<RunSaveData>> { [0] = data => data.seed = "MIGRATED" };
        var older = JsonClone(run.CaptureSave()); older.version = 0;
        Assert.That(RunSaveMigrations.TryUpgrade(older, steps, out var error), Is.True, error);
        Assert.That((older.version, older.seed), Is.EqualTo((RunSaveValidation.Version, "MIGRATED")));

        var current = JsonClone(run.CaptureSave());
        Assert.That(RunSaveMigrations.TryUpgrade(current, out error), Is.True, error);
        Assert.That(current.version, Is.EqualTo(RunSaveValidation.Version));

        var newer = JsonClone(run.CaptureSave()); newer.version = RunSaveValidation.Version + 1;
        Assert.That(RunSaveMigrations.TryUpgrade(newer, out error), Is.False);
        Assert.That(error, Does.Contain("newer"));
        var unknown = JsonClone(run.CaptureSave()); unknown.version = 0;
        Assert.That(RunSaveMigrations.TryUpgrade(unknown, out error), Is.False, "No silent inference without a step.");
    }

    [Test]
    public void Restore_RegeneratesHeadlinerTextFromIdentity_AndKeepsStatsAndAbilities()
    {
        var saved = JsonClone(run.CaptureSave());
        saved.bosses[0].name = "Stale Legacy Name";
        foreach (var ability in saved.bosses[0].abilities) ability.displayName = "Stale Ability";
        run.RestoreSave(saved, catalog);
        var headliner = run.headlinerDeck[0];
        Assert.That(headliner.enemyName, Is.Not.EqualTo("Stale Legacy Name"));
        Assert.That(headliner.enemyName, Does.Contain("—"), "Canonical names come from the face-card identity.");
        Assert.That((headliner.maxHp, headliner.baseAttack, headliner.creditsReward),
            Is.EqualTo((saved.bosses[0].hp, saved.bosses[0].attack, saved.bosses[0].gold)));
        Assert.That(headliner.abilities.Select(ability => (int)ability.effect),
            Is.EqualTo(saved.bosses[0].abilities.Select(ability => ability.effect)));
        Assert.That(headliner.abilities.Any(ability => ability.displayName == "Stale Ability"), Is.False);
    }

    [Test]
    public void AbortedEncounter_KeepsSaveAndStopsAutosave()
    {
        typeof(SaveGameService).GetField("StoreOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, store);
        Write(run.CaptureSave());
        var writer = run.gameObject.AddComponent<RunSaveAutosave>();
        if (typeof(RunSaveAutosave).GetField("_run", Private).GetValue(writer) == null)
            typeof(RunSaveAutosave).GetMethod("OnEnable", Private).Invoke(writer, null);
        var callback = (Action<EncounterResult>)typeof(CombatManager).GetField("OnEncounterResult", Private).GetValue(combat);
        callback.Invoke(EncounterResult.Aborted);
        Assert.That(store.TryRead(catalog, out _, out _), Is.True, "A rule fault must not delete the player's run.");
        Assert.That(writer.Checkpoint(true), Is.False, "No state is written after an aborted encounter.");
    }

    [Test]
    public void ContinueLaunchPayload_IsOneShotAndMutuallyExclusiveWithNewRun()
    {
        var saved = run.CaptureSave(); FrontendLaunchContext.RequestContinue(saved);
        Assert.That(FrontendLaunchContext.TryConsumeRunSeed(out _), Is.False);
        Assert.That(FrontendLaunchContext.TryConsumeContinue(out var consumed), Is.True); Assert.That(consumed, Is.SameAs(saved));
        Assert.That(FrontendLaunchContext.TryConsumeContinue(out _), Is.False);
        FrontendLaunchContext.RequestContinue(saved); FrontendLaunchContext.RequestRun("new");
        Assert.That(FrontendLaunchContext.TryConsumeContinue(out _), Is.False);
    }
}
