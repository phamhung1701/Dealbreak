using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7BMultiEnemyTests
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
        var dragCanvas = CreateComponent<Canvas>("Phase7B Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefab = CreateCardView("Phase7B Card Prefab");

        _cards = CreateComponent<CardManager>("Phase7B CardManager");
        _cards.Configure(_field, dragCanvas, prefab, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        _cards.BuildDeck();
        _cards.DealHand();

        _combat = CreateComponent<CombatManager>("Phase7B CombatManager");
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

    [Test]
    public void GoblinGroup_TargetedKillsDrawCards_SurvivorsAttack_AndVictoryAwardsNineCredits()
    {
        var type = CreateEnemyType("Goblin", 3, 2, 3, encounterCount: 3);
        var goblins = new[]
        {
            new EnemyRuntime(type, 1, 3),
            new EnemyRuntime(type, 2, 3),
            new EnemyRuntime(type, 3, 3)
        };
        _combat.StartEncounter(goblins);

        Assert.That(_combat.Enemies.Count, Is.EqualTo(3));
        Assert.That(_combat.TotalEnemyAttack, Is.EqualTo(6));
        Assert.That(goblins.Select(enemy => enemy.DisplayName), Is.EqualTo(new[] { "Goblin 1", "Goblin 2", "Goblin 3" }));

        KillTarget(goblins[2]);
        Assert.That(_combat.Enemies.Contains(goblins[2]), Is.False);
        Assert.That(_combat.Enemies.Count, Is.EqualTo(2));
        Assert.That(_cards.HandCount, Is.EqualTo(8), "The consumed attack card should be replaced by the kill draw.");
        Assert.That(_combat.pendingDamage, Is.EqualTo(4));
        Assert.That(_cards.credits, Is.Zero, "Kill rewards are secured until encounter victory.");

        _combat.TakeRemainingDamage();
        KillTarget(goblins[0]);
        Assert.That(_combat.Enemies.Count, Is.EqualTo(1));
        Assert.That(_combat.pendingDamage, Is.EqualTo(2));

        _combat.TakeRemainingDamage();
        KillTarget(goblins[1]);
        Assert.That(_combat.Enemies, Is.Empty);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameWon));
        Assert.That(_cards.credits, Is.EqualTo(9));
    }

    [Test]
    public void Thief_FleesAfterSecondCompletedPlayerTurn_WithoutCreditsOrKillDraw()
    {
        var type = CreateEnemyType("Thief", 12, 1, 15, fleeAfterPlayerTurns: 2);
        var thief = new EnemyRuntime(type);
        _combat.StartEnemy(thief);
        int initialTotalCardsOutsideDeck = _cards.hand.Count + _cards.discardPile.Count;

        PlayLowestAttackCard(thief);
        Assert.That(_combat.Enemies.Contains(thief), Is.True);
        Assert.That(thief.PlayerTurnsCompleted, Is.EqualTo(1));
        Assert.That(_combat.pendingDamage, Is.EqualTo(1));
        _combat.TakeRemainingDamage();

        PlayLowestAttackCard(thief);
        Assert.That(thief.PlayerTurnsCompleted, Is.EqualTo(2));
        Assert.That(_combat.Enemies, Is.Empty);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameWon));
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_cards.hand.Count + _cards.discardPile.Count, Is.EqualTo(initialTotalCardsOutsideDeck),
            "Fleeing must not trigger the kill draw.");
    }

    [Test]
    public void AuthoredPhase7BEnemyAssets_HaveApprovedStatsAndIdentityRules()
    {
#if UNITY_EDITOR
        var thief = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyTypeData>("Assets/Data/Enemies/Thief.asset");
        var goblin = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyTypeData>("Assets/Data/Enemies/Goblin.asset");

        Assert.That((thief.maxHp, thief.baseAttack, thief.creditsReward, thief.encounterCount, thief.fleeAfterPlayerTurns),
            Is.EqualTo((12, 1, 15, 1, 2)));
        Assert.That((goblin.maxHp, goblin.baseAttack, goblin.creditsReward, goblin.encounterCount, goblin.fleeAfterPlayerTurns),
            Is.EqualTo((3, 2, 3, 3, 0)));
#endif
    }

    [Test]
    public void KnightAsset_UsesApprovedBaseStats_WithoutChangingExistingReward()
    {
#if UNITY_EDITOR
        var knight = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyTypeData>("Assets/Data/Enemies/Knight.asset");
        Assert.That(knight, Is.Not.Null);
        Assert.That((knight.maxHp, knight.baseAttack, knight.creditsReward, knight.encounterCount),
            Is.EqualTo((16, 4, 10, 1)));
#endif
    }

    [TestCase(0, 16, 4)]
    [TestCase(1, 19, 4)]
    [TestCase(11, 51, 7)]
    public void KnightCombat_PreviewAndSpawnShareScaledStats_WithoutMutatingSource(
        int mapIndex, int expectedHp, int expectedAttack)
    {
        var knight = CreateEnemyType("Knight", 16, 4, 10);
        var run = CreateComponent<RunManager>("Phase1 RunManager");
        run.knightType = knight;
        var node = new PathNode
        {
            id = 7, kind = MapNodeType.Combat, contentId = "Knight",
            mapIndex = mapIndex, accessible = true, revealed = true
        };
        run.currentPath.Add(node);

        Assert.That(run.GetEncounterStats(node), Is.EqualTo((expectedHp, expectedAttack)));
        Assert.That(run.GetNodeDesc(node), Does.Contain($"{expectedHp} HP / {expectedAttack} ATK"));
        _combat.Reset();
        run.OnPathChosen(node.id);

        Assert.That(_combat.Enemies.Count, Is.EqualTo(1));
        Assert.That((_combat.Enemies[0].maxHp, _combat.Enemies[0].currentAttack),
            Is.EqualTo((expectedHp, expectedAttack)));
        Assert.That((knight.maxHp, knight.baseAttack), Is.EqualTo((16, 4)));
    }

    [Test]
    public void EliteEncounter_ScalesBaseBeforeFixedModifier_AndKeepsAssetUnchanged()
    {
        var knight = CreateEnemyType("Knight", 16, 4, 10);
        var run = CreateComponent<RunManager>("Phase1 Elite RunManager");
        run.knightType = knight;
        var node = new PathNode
        {
            id = 8, kind = MapNodeType.Elite, contentId = "Knight",
            mapIndex = 11, accessible = true, revealed = true
        };
        run.currentPath.Add(node);

        Assert.That(run.GetEncounterStats(node), Is.EqualTo((77, 9)));
        _combat.Reset();
        run.OnPathChosen(node.id);

        Assert.That(_combat.Enemies.Count, Is.EqualTo(1));
        Assert.That((_combat.Enemies[0].maxHp, _combat.Enemies[0].currentAttack), Is.EqualTo((77, 9)));
        Assert.That((knight.maxHp, knight.baseAttack, knight.creditsReward), Is.EqualTo((16, 4, 10)));
    }

    [Test]
    public void HeadlinerEncounter_PreviewAndSpawnScaleCachedHeadlinerWithoutChangingIt()
    {
        var headliner = CreateEnemyType("Jack of Hearts", 20, 10, 10);
        var run = CreateComponent<RunManager>("Phase1 Boss RunManager");
        run.headlinerDeck.Add(headliner);
        var node = new PathNode
        {
            id = 9, kind = MapNodeType.Headliner, contentId = "boss",
            mapIndex = 11, accessible = true, revealed = true
        };
        run.currentPath.Add(node);

        Assert.That(run.GetEncounterStats(node), Is.EqualTo((64, 18)));
        _combat.Reset();
        run.OnPathChosen(node.id);

        Assert.That(_combat.Enemies.Count, Is.EqualTo(1));
        Assert.That((_combat.Enemies[0].maxHp, _combat.Enemies[0].currentAttack), Is.EqualTo((64, 18)));
        Assert.That((headliner.maxHp, headliner.baseAttack), Is.EqualTo((20, 10)));
    }

    [Test]
    public void ShieldbearerCombat_PairsWithOneGoblin_AndStartsWithOneShield()
    {
        var shield = CreateEnemyType("Shieldbearer", 10, 3, 0);
        var shieldAbility = ScriptableObject.CreateInstance<EnemyAbility>();
        _created.Add(shieldAbility);
        shieldAbility.effect = EnemyAbilityEffect.StartWithShield;
        shieldAbility.amount = 1;
        shield.abilities = new[] { shieldAbility };
        var goblin = CreateEnemyType("Goblin", 3, 2, 3, encounterCount: 3);
        shield.allies = new[] { new EncounterAllySpec { type = goblin, count = 1 } };
        var catalog = ScriptableObject.CreateInstance<RunContentCatalog>();
        _created.Add(catalog);
        catalog.normalEnemies = new[] { shield, goblin };
        var run = CreateComponent<RunManager>("Phase1 Mixed RunManager");
        run.contentCatalog = catalog;
        var node = new PathNode
        {
            id = 19, kind = MapNodeType.Combat, contentId = "Shieldbearer",
            mapIndex = 1, accessible = true, revealed = true
        };
        run.currentPath.Add(node);

        _combat.Reset();
        run.OnPathChosen(node.id);
        Assert.That(_combat.Enemies.Select(enemy => enemy.type.enemyName),
            Is.EqualTo(new[] { "Shieldbearer", "Goblin" }));
        Assert.That((_combat.Enemies[0].maxHp, _combat.Enemies[0].currentAttack), Is.EqualTo((12, 3)));
        Assert.That((_combat.Enemies[1].maxHp, _combat.Enemies[1].currentAttack), Is.EqualTo((4, 2)));
        var shieldRuntime = _combat.Enemies[0];
        Assert.That(shieldRuntime.ShieldCharges, Is.EqualTo(1));
        Assert.That(shieldRuntime.TakeCardDamage(2, _combat), Is.Zero);
        Assert.That((shieldRuntime.currentHp, shieldRuntime.ShieldCharges), Is.EqualTo((12, 0)));
        _combat.Reset();
        _combat.StartEncounter(new[] { shieldRuntime, new EnemyRuntime(goblin, startingHp: 4, startingAttack: 2) });
        Assert.That(shieldRuntime.ShieldCharges, Is.EqualTo(1));
    }

    [Test]
    public void KnightMapThreeEncounter_IsSolo()
    {
        var knight = CreateEnemyType("Knight", 16, 4, 10);
        var goblin = CreateEnemyType("Goblin", 3, 2, 3);
        var run = CreateComponent<RunManager>("Phase1 Knight Solo RunManager");
        run.knightType = knight;
        run.goblinType = goblin;
        var node = new PathNode
        {
            id = 20, kind = MapNodeType.Combat, contentId = "Knight",
            mapIndex = 2, accessible = true, revealed = true
        };
        run.currentPath.Add(node);
        _combat.Reset();
        run.OnPathChosen(node.id);
        Assert.That(_combat.Enemies.Count, Is.EqualTo(1));
        Assert.That((_combat.Enemies[0].maxHp, _combat.Enemies[0].currentAttack), Is.EqualTo((22, 5)));
    }

    [Test]
    public void BruteCombat_AlternatesResponseDamage_AndTelegraphsTheChargedTurn()
    {
        var brute = CreateEnemyType("Brute", 18, 3, 0);
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        _created.Add(ability);
        ability.effect = EnemyAbilityEffect.AlternateChargedAttack;
        ability.displayName = "Charged Attack";
        brute.abilities = new[] { ability };
        var runtime = new EnemyRuntime(brute);
        _combat.StartEnemy(runtime);
        var display = CreateDisplay("Brute Intent View");
        var status = CreateGameObject("Brute Intent Status", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        display.statusText = status;
        display.Bind(runtime);

        Assert.That(runtime.NextAttackIsCharged, Is.False);
        PlayLowestAttackCard(runtime);
        Assert.That((_combat.pendingDamage, runtime.CurrentResponseIsCharged), Is.EqualTo((3, false)));
        _combat.TakeRemainingDamage();
        Assert.That(runtime.NextAttackIsCharged, Is.True);
        display.RefreshStateGuidance();
        Assert.That(status.text, Does.Contain("Warning: charged attack next (6 damage)"));

        PlayLowestAttackCard(runtime);
        Assert.That((_combat.pendingDamage, runtime.CurrentResponseIsCharged), Is.EqualTo((6, true)));
        display.RefreshStateGuidance();
        Assert.That(status.text, Does.Contain("Charged attack incoming (6 damage)"));
        _combat.TakeRemainingDamage();
        Assert.That(runtime.NextAttackIsCharged, Is.False);
    }

    [Test]
    public void RunManager_NormalGoblinNodeStartsThreeRuntimeEnemies()
    {
        var thief = CreateEnemyType("Thief", 12, 1, 15, fleeAfterPlayerTurns: 2);
        var goblin = CreateEnemyType("Goblin", 3, 2, 3, encounterCount: 3);
        var knight = CreateEnemyType("Knight", 25, 8, 10);
        var run = CreateComponent<RunManager>("Phase7B RunManager");
        run.thiefType = thief;
        run.goblinType = goblin;
        run.knightType = knight;
        run.currentPath.Add(new PathNode
        {
            id = 7,
            kind = MapNodeType.Combat,
            contentId = "Goblin",
            accessible = true,
            revealed = true
        });

        _combat.Reset();
        run.OnPathChosen(7);

        Assert.That(_combat.Enemies.Count, Is.EqualTo(3));
        Assert.That(_combat.Enemies.All(enemy => object.ReferenceEquals(enemy.type, goblin)), Is.True);
        Assert.That(_combat.Enemies.Select(enemy => enemy.DisplayName),
            Is.EqualTo(new[] { "Goblin 1", "Goblin 2", "Goblin 3" }));
    }

    [Test]
    public void EnemyGroupUI_BindsOnePresentationPerActiveEnemy()
    {
        var type = CreateEnemyType("Goblin", 3, 2, 3, encounterCount: 3);
        var enemies = new[]
        {
            new EnemyRuntime(type, 1, 3),
            new EnemyRuntime(type, 2, 3),
            new EnemyRuntime(type, 3, 3)
        };
        _combat.StartEncounter(enemies);

        var groupObject = CreateGameObject("Enemy Group", typeof(RectTransform));
        groupObject.SetActive(false);
        var group = groupObject.AddComponent<EnemyGroupUI>();
        var container = CreateGameObject("Enemy Area", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        container.transform.SetParent(groupObject.transform, false);
        var prefab = CreateDisplay("Enemy View Prefab");
        prefab.gameObject.SetActive(false);
        group.enemyContainer = (RectTransform)container.transform;
        group.enemyViewPrefab = prefab;
        groupObject.SetActive(true);
        group.Refresh();

        Assert.That(group.ActiveViews.Select(view => view.DisplayedEnemy), Is.EqualTo(enemies));
        Assert.That(group.ActiveViews.All(view => view.gameObject.activeSelf), Is.True);

        var attackCard = HighestAttackView();
        Assert.That(_combat.TryPlayCards(new[] { attackCard }, enemies[1]), Is.True);
        group.Refresh();
        Assert.That(group.ActiveViews.Count, Is.EqualTo(2));
        Assert.That(group.ActiveViews.Select(view => view.DisplayedEnemy), Is.EqualTo(_combat.Enemies));
    }

    void KillTarget(EnemyRuntime target)
    {
        var view = HighestAttackView();
        Assert.That(_combat.CalculateCardAttackDamage(view.data), Is.GreaterThanOrEqualTo(target.currentHp));
        Assert.That(_combat.TryPlayCards(new[] { view }, target), Is.True);
    }

    void PlayLowestAttackCard(EnemyRuntime target)
    {
        var view = HandViews().OrderBy(card => _combat.CalculateCardAttackDamage(card.data)).First();
        Assert.That(_combat.TryPlayCards(new[] { view }, target), Is.True);
    }

    CardView HighestAttackView() => HandViews().OrderByDescending(card => _combat.CalculateCardAttackDamage(card.data)).First();

    List<CardView> HandViews()
    {
        var result = new List<CardView>();
        for (int i = 0; i < _field.cardsHolder.childCount; i++)
            if (_field.cardsHolder.GetChild(i).TryGetComponent<CardView>(out var view))
                result.Add(view);
        return result;
    }

    EnemyDisplayUI CreateDisplay(string name)
    {
        var gameObject = CreateGameObject(name, typeof(RectTransform), typeof(Image));
        return gameObject.AddComponent<EnemyDisplayUI>();
    }

    Field CreateHandField()
    {
        var canvas = CreateComponent<Canvas>("Phase7B Hand Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Hand Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        var field = holder.AddComponent<Field>();
        field.cardsHolder = (RectTransform)holder.transform;
        return field;
    }

    CardView CreateCardView(string name)
    {
        var gameObject = CreateGameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var view = gameObject.AddComponent<CardView>();
        view.face = gameObject.GetComponent<Image>();
        view.canvasGroup = gameObject.GetComponent<CanvasGroup>();
        return view;
    }

    EnemyTypeData CreateEnemyType(
        string name,
        int health,
        int attack,
        int credits,
        int encounterCount = 1,
        int fleeAfterPlayerTurns = 0)
    {
        var type = EnemyTypeData.Create(name, health, attack, credits);
        type.encounterCount = encounterCount;
        type.fleeAfterPlayerTurns = fleeAfterPlayerTurns;
        _created.Add(type);
        return type;
    }

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var gameObject = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }
}
