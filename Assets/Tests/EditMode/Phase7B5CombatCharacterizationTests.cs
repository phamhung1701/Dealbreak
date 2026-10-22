using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7B5CombatCharacterizationTests
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
        var dragCanvas = CreateComponent<Canvas>("Phase7B5 Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefab = CreateCardView("Phase7B5 Card Prefab");

        _cards = CreateComponent<CardManager>("Phase7B5 CardManager");
        _cards.Configure(_field, dragCanvas, prefab, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        _cards.BuildDeck();
        _cards.DealHand();

        _combat = CreateComponent<CombatManager>("Phase7B5 CombatManager");
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
    public void SingleCardAction_UsesExactTarget_ConsumesOnce_AndRunsTargetPostCardAbilityOnce()
    {
        var growth = CreateAbility(EnemyAbilityEffect.IncreaseAttackAfterPlayerCard, 1);
        var type = CreateEnemyType("Target", 50, 3, 0, growth);
        var first = new EnemyRuntime(type, 1, 2);
        var second = new EnemyRuntime(type, 2, 2);
        _combat.StartEncounter(new[] { first, second });
        var card = HighestAttackView();
        int expectedDamage = _combat.CalculateCardAttackDamage(card.data);
        int handBefore = _cards.HandCount;

        Assert.That(_combat.TryPlayCards(new[] { card }, second), Is.True);

        Assert.That(first.currentHp, Is.EqualTo(first.maxHp));
        Assert.That(second.currentHp, Is.EqualTo(second.maxHp - expectedDamage));
        Assert.That(first.currentAttack, Is.EqualTo(3));
        Assert.That(second.currentAttack, Is.EqualTo(4));
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore - 1));
        Assert.That(_cards.discardPile, Is.Empty, "The played card returns to the draw pile.");
    }

    [Test]
    public void Defense_InadequateCardPartiallyBlocksWithoutEndingDefense()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Defense", 50, 20, 0));
        _combat.StartEnemy(enemy);
        Assert.That(_combat.TryPlayCards(new[] { LowestAttackView() }, enemy), Is.True);
        var inadequate = HandViews()[0];
        int defense = _combat.CalculateCardDefense(inadequate.data);
        int handBefore = _cards.HandCount;
        int discardBefore = _cards.discardPile.Count;
        int deckBefore = _cards.deck.Count;

        Assert.That(_combat.TryDefendWithCards(new[] { inadequate }), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(20 - defense));
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore - 1));
        Assert.That(_cards.discardPile.Count, Is.EqualTo(discardBefore));
        Assert.That(_cards.deck.Count, Is.EqualTo(deckBefore + 1));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
    }

    [Test]
    public void Recover_AppliesOneAggregateAttack_ThenDrawsOnceWithoutRetaliation()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Recover", 50, 4, 0));
        _combat.StartEnemy(enemy);
        DiscardEntireHandDirectly();
        int healthBefore = _combat.player.currentHealth;

        _combat.Recover();

        Assert.That(_combat.player.currentHealth, Is.EqualTo(healthBefore - 4));
        Assert.That(_cards.HandCount, Is.EqualTo(1));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_combat.pendingDamage, Is.Zero);
    }

    [Test]
    public void Recover_WhenLethal_EmitsDefeatOnceAndDoesNotDraw()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Lethal Recover", 50, 30, 0));
        _combat.StartEnemy(enemy);
        DiscardEntireHandDirectly();
        int resultCount = 0;
        EncounterResult observed = EncounterResult.Victory;
        _combat.OnEncounterResult += result =>
        {
            resultCount++;
            observed = result;
        };

        _combat.Recover();
        _combat.Recover();

        Assert.That(_combat.player.IsDefeated, Is.True);
        Assert.That(_cards.HandCount, Is.Zero);
        Assert.That(resultCount, Is.EqualTo(1));
        Assert.That(observed, Is.EqualTo(EncounterResult.Defeat));
    }

    [Test]
    public void ExistingGearEnhancementAndEnemyTiming_RemainsOncePerCard()
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = "phase7b5_spade";
        gear.displayName = "Phase7B5 Spade";
        gear.effects = new[] { TestEffects.ReduceAttackByCardValue(TestEffects.Suit(CardData.Suit.Spades)) };
        _created.Add(gear);
        _cards.ownedGear.Add(gear);

        var mending = ScriptableObject.CreateInstance<CardEnhancementData>();
        mending.id = "phase7b5_mending";
        mending.displayName = "Phase7B5 Mending";
        mending.effects = new[] { TestEffects.HealOnPlay(2) };
        _created.Add(mending);

        var growth = CreateAbility(EnemyAbilityEffect.IncreaseAttackAfterPlayerCard, 1);
        var enemy = new EnemyRuntime(CreateEnemyType("Timing", 50, 8, 0, growth));
        _combat.StartEnemy(enemy);
        _combat.TakeRunDamage(5);
        var card = HandViews().First(view => view.data.Suit == CardData.Suit.Spades);
        Assert.That(_cards.ApplyEnhancement(card.data.Id, mending), Is.True);
        int baseValue = card.data.BaseAttackValue;

        Assert.That(_combat.TryPlayCards(new[] { card }, enemy), Is.True);

        Assert.That(_combat.player.currentHealth, Is.EqualTo(27));
        Assert.That(enemy.currentAttack, Is.EqualTo(Mathf.Max(0, 8 - baseValue) + 1));
        Assert.That(_cards.discardPile, Is.Empty, "The played card returns to the draw pile.");
    }

    [Test]
    public void KillDrawSecuredRewardAndEncounterResult_AreEachAppliedOnce()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Reward", 1, 0, 7));
        int resultCount = 0;
        _combat.OnEncounterResult += _ => resultCount++;
        _combat.StartEnemy(enemy);
        int handBefore = _cards.HandCount;

        Assert.That(_combat.TryPlayCards(new[] { HighestAttackView() }, enemy), Is.True);

        Assert.That(_cards.HandCount, Is.EqualTo(handBefore));
        Assert.That(_cards.credits, Is.EqualTo(7));
        Assert.That(resultCount, Is.EqualTo(1));
        _combat.TakeRemainingDamage();
        Assert.That(_cards.credits, Is.EqualTo(7));
        Assert.That(resultCount, Is.EqualTo(1));
    }

    void DiscardEntireHandDirectly()
    {
        foreach (var view in HandViews().ToArray())
            Assert.That(_cards.TryDiscard(view), Is.True);
        Assert.That(_cards.HandCount, Is.Zero);
    }

    CardView HighestAttackView() => HandViews().OrderByDescending(view => _combat.CalculateCardAttackDamage(view.data)).First();
    CardView LowestAttackView() => HandViews().OrderBy(view => _combat.CalculateCardAttackDamage(view.data)).First();

    List<CardView> HandViews()
    {
        var views = new List<CardView>();
        for (int i = 0; i < _field.cardsHolder.childCount; i++)
            if (_field.cardsHolder.GetChild(i).TryGetComponent<CardView>(out var view))
                views.Add(view);
        return views;
    }

    EnemyAbility CreateAbility(EnemyAbilityEffect effect, int amount)
    {
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        ability.effect = effect;
        ability.amount = amount;
        _created.Add(ability);
        return ability;
    }

    EnemyTypeData CreateEnemyType(string name, int health, int attack, int credits, params EnemyAbility[] abilities)
    {
        var type = EnemyTypeData.Create(name, health, attack, credits);
        type.abilities = abilities ?? System.Array.Empty<EnemyAbility>();
        _created.Add(type);
        return type;
    }

    Field CreateHandField()
    {
        var canvas = CreateComponent<Canvas>("Phase7B5 Hand Canvas");
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

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var gameObject = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }
}
