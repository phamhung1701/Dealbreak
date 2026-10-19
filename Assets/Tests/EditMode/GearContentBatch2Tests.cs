using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class GearContentBatch2Tests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    GearData _lantern, _hammer, _light;
    CardEnhancementData _mending;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _lantern = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/Lantern.asset");
        _hammer = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/Hammer.asset");
        _light = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/HealingLight.asset");
        _mending = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Enhancements/Mending.asset");
        Assert.That(new UnityEngine.Object[] { _lantern, _hammer, _light, _mending }, Has.All.Not.Null);
        var canvas = Make<Canvas>("Batch2 Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = new GameObject("Batch2 Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _created.Add(holder);
        holder.transform.SetParent(canvas.transform, false);
        var field = holder.AddComponent<Field>();
        field.cardsHolder = (RectTransform)holder.transform;
        var drag = Make<Canvas>("Batch2 Drag");
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefab = new GameObject("Batch2 Card", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        _created.Add(prefab);
        var view = prefab.AddComponent<CardView>();
        view.face = prefab.GetComponent<Image>();
        view.canvasGroup = prefab.GetComponent<CanvasGroup>();
        _cards = Make<CardManager>("Batch2 Cards");
        _cards.Configure(field, drag, view, new[] { _lantern, _hammer, _light,
            AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/ClubPower.asset"),
            AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/SpadePower.asset"),
            AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/Tome.asset") },
            new[] { _mending,
                AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Enhancements/Sharpened.asset"),
                AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Enhancements/Quickdraw.asset"),
                AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Enhancements/Reinforced.asset") });
        _combat = Make<CombatManager>("Batch2 Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Batch2 Run");
        _run.thiefType = Enemy("Thief", 20, 1);
        _run.goblinType = Enemy("Goblin", 20, 2);
        _run.knightType = Enemy("Knight", 20, 3);
        _run.contentCatalog = AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/Data/Run/PrototypeRunContent.asset");
        _run.StartRunWithSeed("LANTERN-HAMMER-LIGHT");
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void Definitions_AreCanonicalTierOneAndRemainImmutableDuringEffects()
    {
        var assets = new[] { _lantern, _hammer, _light };
        var ids = new[] { "rel_008", "rel_009", "rel_010" };
        var prices = new[] { 18, 20, 18 };
        var kinds = new[] { GameplayEffectKind.RevealMapNode,
            GameplayEffectKind.GrantRandomCommonEnhancement, GameplayEffectKind.Heal };
        var triggers = new[] { GameplayEffectTrigger.MapReady,
            GameplayEffectTrigger.NodeCompleted, GameplayEffectTrigger.PlayerTurnStart };
        var serialized = assets.Select(JsonUtility.ToJson).ToArray();
        for (int i = 0; i < 3; i++)
        {
            Assert.That(assets[i].id, Is.EqualTo(ids[i]));
            Assert.That(assets[i].canonicalId, Is.EqualTo(ids[i]));
            Assert.That(assets[i].rarity, Is.EqualTo("Common"));
            Assert.That(assets[i].tier, Is.EqualTo(1));
            Assert.That(assets[i].price, Is.EqualTo(prices[i]));
            Assert.That(assets[i].capacityCategory, Is.EqualTo(GearCapacityCategory.Persistent));
            Assert.That(assets[i].effects, Has.Length.EqualTo(1));
            Assert.That((assets[i].effects[0].kind, assets[i].effects[0].trigger),
                Is.EqualTo((kinds[i], triggers[i])));
            Assert.That(_cards.BuyGear(assets[i], 0), Is.True);
        }
        CompleteNodes(3);
        _run.RestartRun();
        for (int i = 0; i < 3; i++)
            Assert.That(JsonUtility.ToJson(assets[i]), Is.EqualTo(serialized[i]));
    }

    [Test]
    public void Lantern_RevealsExactlyOneReachableHiddenNodeWithoutChangingMapOrEliteVisibility()
    {
        var before = _run.currentPath.ToDictionary(n => n.id,
            n => (n.revealed, n.scouted, n.accessible, n.completed, n.hidden, n.kind,
                n.col, n.row, n.contentId, next: string.Join(",", n.next)));
        Assert.That(_cards.BuyGear(_lantern, 0), Is.True);
        var extra = _run.currentPath.Where(n => !before[n.id].scouted && n.scouted).ToArray();
        Assert.That(extra, Has.Length.EqualTo(1));
        Assert.That(_run.IsScoutedNode(extra[0]), Is.True, "Lantern scouts one reachable unknown node of any kind.");
        Assert.That(_run.LastScoutedNode, Is.SameAs(extra[0]));
        foreach (var node in _run.currentPath)
        {
            var baseline = before[node.id];
            Assert.That((node.accessible, node.completed, node.hidden, node.kind,
                node.col, node.row, node.contentId, string.Join(",", node.next)),
                Is.EqualTo((baseline.accessible, baseline.completed, baseline.hidden,
                    baseline.kind, baseline.col, baseline.row, baseline.contentId, baseline.next)));
            if (node.kind == MapNodeType.Elite) Assert.That(node.revealed, Is.True);
        }
        _run.RefreshCurrentMapEffects();
        Assert.That(_run.currentPath.Count(n => !before[n.id].scouted && n.scouted), Is.EqualTo(1),
            "The Artifact reveals at most one node per map.");
    }

    [Test]
    public void Lantern_SameSeedReplaysReveal_AndRestartWithoutOwnershipUsesBaseline()
    {
        _cards.BuyGear(_lantern, 0);
        int first = _run.currentPath.Single(n => _run.IsScoutedNode(n)).id;
        _run.RestartRun();
        Assert.That(_cards.ownedGear, Is.Empty);
        Assert.That(_run.currentPath.Single(n => n.id == first).scouted, Is.False);
        _cards.BuyGear(_lantern, 0);
        Assert.That(_run.currentPath.Single(n => _run.IsScoutedNode(n)).id, Is.EqualTo(first));
        _run.StartRunWithSeed("LANTERN-DIFFERENT");
        Assert.That(_cards.ownedGear, Is.Empty);
        Assert.That(_run.currentPath.Count(n => _run.IsScoutedNode(n)), Is.Zero);
    }

    [Test]
    public void Lantern_AppliesOnceOnEachNewMapWithoutConsumingAnotherRevealOnRefresh()
    {
        _cards.BuyGear(_lantern, 0);
        var runtime = _cards.GetGearInstance(_lantern);
        Assert.That(runtime.State.GetCounter(0), Is.EqualTo(1));
        _run.headlinerIndex = 1;
        typeof(RunManager).GetMethod("GenerateAndAnnounceCycle",
            BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(_run, null);
        Assert.That(_run.CurrentMapIndex, Is.EqualTo(1));
        Assert.That(_run.currentPath.Count(n => _run.IsScoutedNode(n)), Is.EqualTo(1));
        Assert.That(runtime.State.GetCounter(0), Is.EqualTo(2));
        _run.RefreshCurrentMapEffects();
        Assert.That(_run.currentPath.Count(n => _run.IsScoutedNode(n)), Is.EqualTo(1));
    }

    [Test]
    public void Lantern_DoesNotRevealCompletedOrDisconnectedCandidates()
    {
        var hidden = _run.currentPath.Where(n => n.hidden && !n.revealed)
            .OrderBy(n => n.col).ThenBy(n => n.row).ToArray();
        Assert.That(hidden, Is.Not.Empty);
        var disconnected = hidden[0];
        foreach (var node in _run.currentPath) node.next.Remove(disconnected.id);
        // Other hidden nodes remain eligible when reachable; the orphan never becomes visible.
        _cards.BuyGear(_lantern, 0);
        Assert.That(disconnected.revealed, Is.False);
        _run.RestartRun();
        var completed = _run.currentPath.Where(n => n.hidden && !n.revealed)
            .OrderBy(n => n.col).ThenBy(n => n.row).First();
        completed.completed = true;
        _cards.BuyGear(_lantern, 0);
        Assert.That(completed.revealed, Is.False);
    }

    [Test]
    public void Hammer_CounterTriggersOnlyAtThreeAndSix_AfterCompletion_AndOnlyOnce()
    {
        _cards.BuyGear(_hammer, 0);
        var runtime = _cards.GetGearInstance(_hammer);
        Assert.That(runtime.State.GetCounter(0), Is.Zero);
        for (int count = 1; count <= 6; count++)
        {
            CompleteNodes(1);
            Assert.That(_run.CompletedNodeCount, Is.EqualTo(count));
            Assert.That(runtime.State.GetCounter(0), Is.EqualTo(count));
            Assert.That(_cards.ownedCards.Count(c => c.Enhancement != null), Is.EqualTo(count / 3));
        }
        Assert.That(_cards.ownedCards.Where(c => c.Enhancement != null)
            .All(c => c.Enhancement.rarity == "Common" && !string.IsNullOrEmpty(c.Enhancement.canonicalId)),
            Is.True);
    }

    [Test]
    public void Hammer_LateAcquisitionResumesGlobalNodeMilestonesAndRestartResetsCounter()
    {
        CompleteNodes(2);
        Assert.That(_run.CompletedNodeCount, Is.EqualTo(2));
        _cards.BuyGear(_hammer, 0);
        var runtime = _cards.GetGearInstance(_hammer);
        Assert.That(runtime.State.GetCounter(0), Is.EqualTo(2));
        CompleteNodes(1);
        Assert.That(_cards.ownedCards.Count(c => c.Enhancement != null), Is.EqualTo(1));
        _run.RestartRun();
        Assert.That(_run.CompletedNodeCount, Is.Zero);
        Assert.That(_cards.ownedCards.All(c => c.Enhancement == null), Is.True);
        _cards.BuyGear(_hammer, 0);
        Assert.That(_cards.GetGearInstance(_hammer).State.GetCounter(0), Is.Zero);
        CompleteNodes(2);
        Assert.That(_cards.ownedCards.All(c => c.Enhancement == null), Is.True);
    }

    [Test]
    public void Hammer_ExcludesIneligibleCards_OnlyUsesCanonicalCommon_AndNoEligibleIsSafe()
    {
        _cards.BuyGear(_hammer, 0);
        var rare = ScriptableObject.CreateInstance<CardEnhancementData>();
        rare.id = "test-rare";
        rare.canonicalId = "enh_future_rare";
        rare.rarity = "Rare";
        _created.Add(rare);
        _cards.enhancementCatalog.Add(rare);
        var eligible = _cards.ownedCards.Last();
        var common = _cards.enhancementCatalog.First(e => e != null && e.rarity == "Common");
        foreach (var card in _cards.ownedCards.Where(c => c != eligible))
            Assert.That(_cards.ApplyEnhancement(card.Id, common), Is.True);
        CompleteNodes(3);
        Assert.That(eligible.Enhancement, Is.Not.Null);
        Assert.That(eligible.Enhancement.rarity, Is.EqualTo("Common"));
        int enhanced = _cards.ownedCards.Count(c => c.Enhancement != null);
        CompleteNodes(3);
        Assert.That(_cards.ownedCards.Count(c => c.Enhancement != null), Is.EqualTo(enhanced));
        Assert.That(_cards.GetGearInstance(_hammer).State.GetCounter(0), Is.EqualTo(6));
    }

    [Test]
    public void Hammer_SameSeedAndActionsChooseSameCardAndCommonEnhancement_WithoutCritRng()
    {
        var random = new CountingRandom();
        _combat.ConfigureCriticalRandom(random);
        _cards.BuyGear(_hammer, 0);
        CompleteNodes(3);
        var first = _cards.ownedCards.Single(c => c.Enhancement != null);
        var signature = (first.Id, first.Enhancement.canonicalId);
        Assert.That(random.Calls, Is.Zero);
        _run.RestartRun();
        random = new CountingRandom();
        _combat.ConfigureCriticalRandom(random);
        _cards.BuyGear(_hammer, 0);
        CompleteNodes(3);
        var replay = _cards.ownedCards.Single(c => c.Enhancement != null);
        Assert.That((replay.Id, replay.Enhancement.canonicalId), Is.EqualTo(signature));
        Assert.That(random.Calls, Is.Zero);
    }

    [Test]
    public void HealingLight_UsesPlayerTurnStart_IndependentlyFromMending_InStableOrder()
    {
        _cards.BuyGear(_light, 0);
        var mendingCard = Views()[0];
        Assert.That(_cards.ApplyEnhancement(mendingCard.data.Id, _mending), Is.True);
        var logs = new List<string>();
        _combat.OnCombatLog += logs.Add;
        _combat.TakeRunDamage(10);
        var enemy = new EnemyRuntime(Enemy("Long Target", 100, 2));
        _combat.StartEnemy(enemy);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(24));
        CollectionAssert.AreEqual(new[] { "Auto-Injector: Healed 2 HP.", "Regenerative: Healed 2 HP." },
            logs.Where(s => s.Contains(": Healed ")).ToArray());
        Assert.That(_combat.TryPlayCards(new[] { Views().First(v => v != mendingCard) }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(24), "Enemy-turn start does not heal.");
        _combat.TakeRemainingDamage();
        Assert.That(_combat.player.currentHealth, Is.EqualTo(26));
        _combat.StartEnemy(new EnemyRuntime(Enemy("Duplicate Start", 100, 2)));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(26), "An active encounter cannot start twice.");
        _combat.HealPlayer(100);
        Assert.That(_combat.TryPlayCards(new[] { Views().First(v => v != mendingCard) }, enemy), Is.True);
        _combat.TakeRemainingDamage();
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30), "Both heals clamp at max HP.");
    }

    [Test]
    public void ShopOffersCanSurfaceAndPurchaseThree_WithCapacityAndDuplicateGuards()
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < 48 && seen.Count < 3; i++)
        {
            _run.StartRunWithSeed($"THREE-ARTIFACTS-{i}");
            var shop = _run.currentPath.First(n => n.kind == MapNodeType.Shop);
            shop.accessible = true;
            _cards.AddCredits(100);
            _run.OnPathChosen(shop.id);
            foreach (var offer in _run.GetCurrentShopOffers().Where(o => o.kind == ShopOfferKind.Gear))
                if (offer.gear == _lantern || offer.gear == _hammer || offer.gear == _light)
                    seen.Add(offer.gear.canonicalId);
        }
        Assert.That(seen, Is.EquivalentTo(new[] { "rel_008", "rel_009", "rel_010" }));
        var chosen = _run.GetCurrentShopOffers().First(o => o.kind == ShopOfferKind.Gear);
        int credits = _cards.credits, price = chosen.price;
        Assert.That(_run.PurchaseShopOffer(chosen.StableId), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(credits - price));
        Assert.That(_run.PurchaseShopOffer(chosen.StableId), Is.False);
        Assert.That(_cards.GearSlotsUsed, Is.EqualTo(1));
        foreach (var gear in _cards.gearCatalog.Where(a => a != chosen.gear).Take(4))
            Assert.That(_cards.BuyGear(gear, 0), Is.True);
        Assert.That(_cards.GearSlotsUsed, Is.EqualTo(5));
        Assert.That(_cards.BuyGear(_cards.gearCatalog.First(a => !_cards.HasGear(a)), 0), Is.False);
    }

    void CompleteNodes(int count)
    {
        var type = typeof(RunManager);
        var active = type.GetField("_activeNode", BindingFlags.Instance | BindingFlags.NonPublic);
        var complete = type.GetMethod("CompleteActiveNode", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(active, Is.Not.Null);
        Assert.That(complete, Is.Not.Null);
        for (int i = 0; i < count; i++)
        {
            var node = _run.currentPath.First(n => !n.completed);
            active.SetValue(_run, node);
            complete.Invoke(_run, null);
            Assert.That(node.completed, Is.True);
        }
    }

    CardView[] Views() => _cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(v => v.data != null && _cards.hand.Contains(v.data)).ToArray();

    EnemyTypeData Enemy(string name, int health, int attack)
    {
        var type = EnemyTypeData.Create(name, health, attack, 0);
        type.name = name;
        _created.Add(type);
        return type;
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go.AddComponent<T>();
    }

    sealed class CountingRandom : IRandomSource
    {
        public int Calls { get; private set; }
        public int NextInt(int minInclusive, int maxExclusive) => throw new NotImplementedException();
        public float NextFloat() { Calls++; return 0f; }
    }
}
