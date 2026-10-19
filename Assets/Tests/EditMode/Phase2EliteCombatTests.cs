using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Regression coverage for authored elite encounters and elite combat abilities.</summary>
public sealed class Phase2EliteCombatTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _field = CreateHandField();
        var canvas = CreateComponent<Canvas>("Phase2 Drag Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefab = CreateCardView("Phase2 Card Prefab");
        _cards = CreateComponent<CardManager>("Phase2 CardManager");
        _cards.Configure(_field, canvas, prefab, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = CreateComponent<CombatManager>("Phase2 CombatManager");
        _combat.ConfigurePlayer(30);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [TestCase(1, 16, 3)]
    [TestCase(2, 19, 3)]
    [TestCase(12, 51, 5)]
    public void GoblinCaptainElite_UsesAuthoredStatsAndMapScaling(int mapNumber, int expectedHp, int expectedAttack)
    {
        var captain = LoadEnemy("GoblinCaptain");
        Assert.That(captain, Is.Not.Null);
        Assert.That((captain.maxHp, captain.baseAttack, captain.creditsReward), Is.EqualTo((16, 3, 0)));
        var run = CreateComponent<RunManager>("Phase2 Elite RunManager");
        run.contentCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/Data/Run/PrototypeRunContent.asset");
        run.currentPath.Add(new PathNode { id = 1, kind = MapNodeType.Elite, contentId = captain.name,
            mapIndex = mapNumber - 1, accessible = true, revealed = true });
        Assert.That(run.GetEncounterStats(run.currentPath[0]), Is.EqualTo((expectedHp, expectedAttack)));
        var previewAlly = EnemyMapScaling.Scale(3, 2, mapNumber - 1);
        Assert.That(run.GetNodeDesc(run.currentPath[0]), Does.Contain(
            $"{expectedHp + 2 * previewAlly.hp} HP / {expectedAttack + 2 * (previewAlly.attack + 1)} ATK total"));
        run.OnPathChosen(1);
        Assert.That(_combat.Enemies.Count, Is.EqualTo(3));
        Assert.That(_combat.Enemies[0].type, Is.SameAs(captain));
        Assert.That((_combat.Enemies[0].maxHp, _combat.Enemies[0].EffectiveAttack), Is.EqualTo((expectedHp, expectedAttack)));
        var allyStats = EnemyMapScaling.Scale(3, 2, mapNumber - 1);
        foreach (var goblin in _combat.Enemies.Skip(1))
        {
            Assert.That(goblin.type, Is.SameAs(LoadEnemy("Goblin")));
            Assert.That((goblin.maxHp, goblin.currentAttack, goblin.EffectiveAttack),
                Is.EqualTo((allyStats.hp, allyStats.attack, allyStats.attack + 1)));
        }
        Assert.That((captain.maxHp, captain.baseAttack, captain.creditsReward), Is.EqualTo((16, 3, 0)),
            "Previewing and spawning must not mutate the authored source.");
    }

    [Test]
    public void Captaincy_IsFixedBonusOnGoblinAndRemovedWhenCaptainDies()
    {
        var captain = CreateEnemy("Captain", 16, 3, EnemyAbilityEffect.Captaincy);
        var goblinType = CreateEnemyType("Goblin", 3, 2);
        var captainRuntime = new EnemyRuntime(captain);
        var goblin = new EnemyRuntime(goblinType);
        _combat.StartEncounter(new[] { captainRuntime, goblin });

        Assert.That(captainRuntime.HasCaptaincy, Is.True);
        Assert.That(goblin.CaptainAttackBonus, Is.EqualTo(1));
        Assert.That(goblin.currentAttack, Is.EqualTo(2), "Current attack remains the unmodified base value.");
        Assert.That(goblin.EffectiveAttack, Is.EqualTo(3));
        Assert.That(_combat.TotalEnemyAttack, Is.EqualTo(6));
        goblin.ReduceAttack(1);
        Assert.That((goblin.currentAttack, goblin.EffectiveAttack), Is.EqualTo((1, 2)),
            "The captain aura remains a fixed +1 after base attack changes.");

        captainRuntime.TakeDamage(16);
        Assert.That(captainRuntime.IsDefeated, Is.True);
        Assert.That(goblin.CaptainAttackBonus, Is.Zero);
        Assert.That(goblin.EffectiveAttack, Is.EqualTo(1));
    }

    [Test]
    public void RoyalGuard_GainsShieldOnNormalResponsesAndAlternatesHeavyResponses()
    {
        var knight = LoadEnemy("RoyalKnight");
        Assert.That(knight, Is.Not.Null);
        Assert.That((knight.maxHp, knight.baseAttack, knight.creditsReward), Is.EqualTo((32, 5, 0)));
        var runtime = new EnemyRuntime(knight);
        Assert.That(runtime.HasRoyalGuard, Is.True);
        _combat.StartEnemy(runtime);
        Assert.That(runtime.ShieldCharges, Is.Zero, "Royal Guard does not grant shield at encounter start.");

        Assert.That(runtime.PrepareResponseAttack(), Is.EqualTo(5));
        Assert.That(runtime.ShieldCharges, Is.EqualTo(1));
        Assert.That(runtime.NextAttackIsCharged, Is.True);
        Assert.That(runtime.PrepareResponseAttack(), Is.EqualTo(10));
        Assert.That(runtime.ShieldCharges, Is.EqualTo(1), "The heavy response is not a normal shield-gaining response.");
        Assert.That(runtime.NextAttackIsCharged, Is.False);
        Assert.That(runtime.PrepareResponseAttack(), Is.EqualTo(5));
        Assert.That(runtime.ShieldCharges, Is.EqualTo(2));

        runtime.ResetResponseIntent();
        Assert.That(runtime.NextAttackIsCharged, Is.False);
        Assert.That(runtime.PrepareResponseAttack(), Is.EqualTo(5));
        Assert.That(runtime.ShieldCharges, Is.EqualTo(3));
    }

    [Test]
    public void EliteAssetsAndCatalog_KeepAuthoredIdentityAndIndependentRuntimeInstances()
    {
        var captain = LoadEnemy("GoblinCaptain");
        var knight = LoadEnemy("RoyalKnight");
        var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/Data/Run/PrototypeRunContent.asset");
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.eliteEnemies, Does.Contain(captain));
        Assert.That(catalog.eliteEnemies, Does.Contain(knight));
        Assert.That(captain.encounterCount, Is.EqualTo(1));
        var goblin = LoadEnemy("Goblin");
        Assert.That(goblin.encounterCount, Is.EqualTo(3), "Captain encounters must not change the regular Goblin group size.");

        var first = new EnemyRuntime(captain);
        var second = new EnemyRuntime(captain);
        first.TakeDamage(4);
        first.IncreaseAttack(2);
        Assert.That((first.currentHp, first.currentAttack), Is.EqualTo((12, 5)));
        Assert.That((second.currentHp, second.currentAttack), Is.EqualTo((16, 3)));
        Assert.That((captain.maxHp, captain.baseAttack, captain.creditsReward), Is.EqualTo((16, 3, 0)));
        Assert.That((knight.maxHp, knight.baseAttack, knight.creditsReward), Is.EqualTo((32, 5, 0)));
    }

    [Test]
    public void CaptainKilledByRetaliation_RemovesBonusFromUnblockedPendingAttacks()
    {
        var captainType = CreateEnemy("Captain", 100, 3, EnemyAbilityEffect.Captaincy);
        var goblinType = CreateEnemyType("Goblin", 100, 2);
        var captain = new EnemyRuntime(captainType);
        var goblins = new[] { new EnemyRuntime(goblinType), new EnemyRuntime(goblinType) };
        var retaliation = UnityEditor.AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/RetaliationEmblem.asset");
        Assert.That(_cards.BuyGear(retaliation, 0), Is.True);
        _combat.StartEncounter(new[] { captain, goblins[0], goblins[1] });
        var attack = _field.cardsHolder.GetComponentsInChildren<CardView>()
            .Where(view => view.data != null && _cards.hand.Contains(view.data))
            .OrderBy(view => _combat.CalculateCardAttackDamage(view.data)).First();
        Assert.That(_combat.TryPlayCards(new[] { attack }, captain), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(9));
        captain.currentHp = 5;
        var defense = _field.cardsHolder.GetComponentsInChildren<CardView>()
            .First(view => view.data != null && _cards.hand.Contains(view.data) && _combat.CalculateCardDefense(view.data) >= 3);
        Assert.That(_combat.TryDefendWithCards(new[] { defense }, captain), Is.True);
        Assert.That(captain.IsDefeated, Is.True);
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(2));
        Assert.That(_combat.pendingDamage, Is.EqualTo(4));
        Assert.That(goblins.All(goblin => goblin.CaptainAttackBonus == 0 && goblin.EffectiveAttack == 2), Is.True);
    }

    [TestCase(0, 32, 5)]
    [TestCase(1, 38, 5)]
    [TestCase(11, 102, 9)]
    public void RoyalKnightSpawn_MatchesPreview_WithoutGenericEliteModifiers(int mapIndex, int hp, int attack)
    {
        var run = CreateComponent<RunManager>("Royal Knight Run");
        run.contentCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/Data/Run/PrototypeRunContent.asset");
        var node = new PathNode { id = 2, kind = MapNodeType.Elite, contentId = "RoyalKnight",
            mapIndex = mapIndex, accessible = true, revealed = true };
        run.currentPath.Add(node);
        Assert.That(run.GetEncounterStats(node), Is.EqualTo((hp, attack)));
        run.OnPathChosen(node.id);
        Assert.That(_combat.Enemies.Count, Is.EqualTo(1));
        Assert.That((_combat.Enemies[0].maxHp, _combat.Enemies[0].EffectiveAttack), Is.EqualTo((hp, attack)));
        Assert.That(_combat.Enemies[0].type, Is.SameAs(LoadEnemy("RoyalKnight")));
    }

    [Test]
    public void RoyalKnightCombat_ShieldAndHeavyTelegraphMatchPendingResponse()
    {
        var knight = new EnemyRuntime(LoadEnemy("RoyalKnight"));
        _combat.StartEnemy(knight);
        var display = CreateComponent<EnemyDisplayUI>("Royal Intent View");
        var status = CreateGameObject("Royal Intent Text", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
        display.statusText = status;
        display.Bind(knight);
        Assert.That(status.text, Does.Contain("Next: +1 Shield and 5 damage"));
        var attack = HandViews().OrderBy(view => _combat.CalculateCardAttackDamage(view.data)).First();
        Assert.That(_combat.TryPlayCards(new[] { attack }, knight), Is.True);
        Assert.That((_combat.pendingDamage, knight.ShieldCharges), Is.EqualTo((5, 1)));
        display.RefreshStateGuidance();
        Assert.That(status.text, Does.Contain("Shield: 1"));
        _combat.TakeRemainingDamage();
        display.RefreshStateGuidance();
        Assert.That(status.text, Does.Contain("charged attack next (10 damage)"));
        int hp = knight.currentHp;
        attack = HandViews().OrderBy(view => _combat.CalculateCardAttackDamage(view.data)).First();
        Assert.That(_combat.TryPlayCards(new[] { attack }, knight), Is.True);
        Assert.That((knight.currentHp, knight.ShieldCharges, _combat.pendingDamage), Is.EqualTo((hp, 0, 10)));
        display.RefreshStateGuidance();
        Assert.That(status.text, Does.Contain("Charged attack incoming (10 damage)"));
        _combat.Reset();
        _combat.StartEnemy(knight);
        Assert.That(knight.ShieldCharges, Is.Zero);
        Assert.That(knight.NextAttackIsCharged, Is.False);
    }

    IEnumerable<CardView> HandViews() => _field.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(view => view.data != null && _cards.hand.Contains(view.data));

    EnemyTypeData LoadEnemy(string assetName) =>
        UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyTypeData>($"Assets/Data/Enemies/{assetName}.asset");

    EnemyTypeData CreateEnemy(string name, int hp, int attack, EnemyAbilityEffect effect)
    {
        var type = CreateEnemyType(name, hp, attack);
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        _created.Add(ability);
        ability.effect = effect;
        type.abilities = new[] { ability };
        return type;
    }

    EnemyTypeData CreateEnemyType(string name, int hp, int attack)
    {
        var type = EnemyTypeData.Create(name, hp, attack, 0);
        _created.Add(type);
        return type;
    }

    Field CreateHandField()
    {
        var canvas = CreateComponent<Canvas>("Phase2 Hand Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Hand Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        var field = holder.AddComponent<Field>();
        field.cardsHolder = (RectTransform)holder.transform;
        return field;
    }

    CardView CreateCardView(string name)
    {
        var go = CreateGameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(CanvasGroup), typeof(LayoutElement));
        var view = go.AddComponent<CardView>();
        view.face = go.GetComponent<UnityEngine.UI.Image>();
        view.canvasGroup = go.GetComponent<CanvasGroup>();
        return view;
    }

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var go = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(go);
        return go;
    }
}
