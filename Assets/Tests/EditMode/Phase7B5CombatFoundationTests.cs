using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class Phase7B5CombatFoundationTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _field = CreateHandField();
        var dragCanvas = CreateComponent<Canvas>("Phase7B5 Foundation Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefab = CreateCardView("Phase7B5 Foundation Card Prefab");

        _cards = CreateComponent<CardManager>("Phase7B5 Foundation CardManager");
        _cards.Configure(_field, dragCanvas, prefab, Array.Empty<GearData>(), Array.Empty<CardEnhancementData>());
        _cards.BuildDeck();
        _cards.DealHand();

        _combat = CreateComponent<CombatManager>("Phase7B5 Foundation CombatManager");
        _combat.ConfigurePlayer(30);
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
    public void TryStartEncounter_ValidatesAllEnemiesBeforeMutatingCombatState()
    {
        var candidate = new EnemyRuntime(CreateEnemyType("Encounter Validation", 10, 1, 0));
        Assert.Throws<System.ArgumentException>(() =>
            _combat.TryStartEncounter(new EnemyRuntime[] { candidate, null }));
        Assert.That(_combat.Enemies, Is.Empty);
        Assert.That(_combat.currentEnemy, Is.Null);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.Idle));

        Assert.Throws<System.ArgumentException>(() =>
            _combat.TryStartEncounter(new[] { candidate, candidate }));
        Assert.That(_combat.Enemies, Is.Empty);
        Assert.That(_combat.currentEnemy, Is.Null);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.Idle));

        Assert.That(_combat.TryStartEncounter(new[] { candidate }), Is.True);
        Assert.That(_combat.Enemies, Is.EqualTo(new[] { candidate }));
    }

    [Test]
    public void CombatReset_ClearsQueuedOverflowAndBonusActionState()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Overflow Reset", 100, 0, 0));
        _combat.StartEnemy(enemy);
        SetPrivateField("_overflowRootActive", true);
        SetPrivateField("_overflowBudgetRemaining", 1);
        SetPrivateField("_overflowRootActionId", 42L);
        SetPrivateField("_executingOverflowAutoPlay", true);
        SetPrivateField("_isBonusPlayerAction", true);
        var card = HighestAttackView();
        _combat.QueueOverflowAutoPlay(card);
        Assert.That(_combat.QueuedOverflowAutoPlayCount, Is.EqualTo(1));

        _combat.Reset();

        Assert.That(_combat.QueuedOverflowAutoPlayCount, Is.Zero);
        Assert.That(_combat.OverflowDrawAllowance, Is.Zero);
        Assert.That(_combat.IsResolvingOverflowAttack, Is.False);
        Assert.That(_combat.IsBonusPlayerAction, Is.False);
        Assert.That(_combat.CanAutoPlayOverflow, Is.False);
    }

    void SetPrivateField(string name, object value) => typeof(CombatManager).GetField(name,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(_combat, value);

    [Test]
    public void DamageResult_DistinguishesModifierShieldHpLossOverkillAndLethality()
    {
        var guard = CreateAbility(EnemyAbilityEffect.ReduceIncomingCardDamage, 2);
        var enemy = new EnemyRuntime(CreateEnemyType("Resolver Target", 10, 0, 0, guard));
        enemy.GainShield(1);
        var action = new CombatActionContext(1, CombatActionOrigin.PlayerCard, _combat.player, card: _cards.ownedCards[0], targetEnemy: enemy);
        var resolver = new CombatResolver();

        var blocked = resolver.Resolve(new DamageRequest(action, 0, CombatDamageOrigin.Card, _combat.player, enemy, 7), _combat);
        var lethal = resolver.Resolve(new DamageRequest(action, 1, CombatDamageOrigin.Card, _combat.player, enemy, 100), _combat);

        Assert.That(blocked.RequestedDamage, Is.EqualTo(7));
        Assert.That(blocked.ModifiedDamage, Is.EqualTo(5));
        Assert.That(blocked.ModifierPreventedDamage, Is.EqualTo(2));
        Assert.That(blocked.ShieldBlockedDamage, Is.EqualTo(5));
        Assert.That(blocked.ActualHpLost, Is.Zero);
        Assert.That(blocked.ShieldConsumed, Is.True);
        Assert.That(lethal.ModifiedDamage, Is.EqualTo(98));
        Assert.That(lethal.ActualHpLost, Is.EqualTo(10));
        Assert.That(lethal.OverkillDamage, Is.EqualTo(88));
        Assert.That(lethal.WasLethal, Is.True);
    }

    [Test]
    public void DamageResult_AlreadyDefeatedTargetIsNotReportedAsModifierPrevention()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Already Defeated", 5, 0, 0));
        enemy.ApplyResolvedCombatDamage(5);
        var action = new CombatActionContext(6, CombatActionOrigin.PlayerCard, _combat.player, targetEnemy: enemy);
        var request = new DamageRequest(action, 0, CombatDamageOrigin.Card, _combat.player, enemy, 9);

        var result = new CombatResolver().Resolve(request, _combat);

        Assert.That(result.RequestedDamage, Is.EqualTo(9));
        Assert.That(result.ModifiedDamage, Is.Zero);
        Assert.That(result.ModifierPreventedDamage, Is.Zero);
        Assert.That(result.ShieldBlockedDamage, Is.Zero);
        Assert.That(result.ActualHpLost, Is.Zero);
        Assert.That(result.OverkillDamage, Is.Zero);
        Assert.That(result.TargetAlreadyDefeated, Is.True);
        Assert.That(result.WasLethal, Is.False);
    }

    [Test]
    public void Shield_ZeroOrFullyPreventedDamageDoesNotConsumeCharge()
    {
        var fullGuard = CreateAbility(EnemyAbilityEffect.ReduceIncomingCardDamage, 10);
        var enemy = new EnemyRuntime(CreateEnemyType("Prevented Target", 10, 0, 0, fullGuard));
        enemy.GainShield(2);
        var action = new CombatActionContext(2, CombatActionOrigin.PlayerCard, _combat.player, card: _cards.ownedCards[0], targetEnemy: enemy);
        var resolver = new CombatResolver();

        var zero = resolver.Resolve(new DamageRequest(action, 0, CombatDamageOrigin.Card, _combat.player, enemy, 0), _combat);
        var prevented = resolver.Resolve(new DamageRequest(action, 1, CombatDamageOrigin.Card, _combat.player, enemy, 5), _combat);

        Assert.That(zero.ShieldConsumed, Is.False);
        Assert.That(prevented.ModifiedDamage, Is.Zero);
        Assert.That(prevented.ModifierPreventedDamage, Is.EqualTo(5));
        Assert.That(prevented.ShieldConsumed, Is.False);
        Assert.That(enemy.ShieldCharges, Is.EqualTo(2));
        Assert.That(enemy.currentHp, Is.EqualTo(10));
    }

    [Test]
    public void Shield_IsSymmetricAndNonCombatHealthCostBypassesIt()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Shield Symmetry", 10, 0, 0));
        enemy.GainShield(1);
        _combat.player.GainShield(2);
        var resolver = new CombatResolver();
        var enemyAction = new CombatActionContext(3, CombatActionOrigin.PlayerCard, _combat.player, targetEnemy: enemy);
        var playerAction = new CombatActionContext(4, CombatActionOrigin.EnemyRetaliation, sourceEnemy: enemy);

        var enemyResult = resolver.Resolve(new DamageRequest(enemyAction, 0, CombatDamageOrigin.Card, _combat.player, enemy, 50), _combat);
        var playerResult = resolver.Resolve(new DamageRequest(playerAction, 0, CombatDamageOrigin.EnemyAggregate, enemy, _combat.player, 50), _combat);
        int runDamage = _combat.TakeRunDamage(3);

        Assert.That(enemyResult.ActualHpLost, Is.Zero);
        Assert.That(enemy.ShieldCharges, Is.Zero);
        Assert.That(playerResult.ActualHpLost, Is.Zero);
        Assert.That(_combat.player.ShieldCharges, Is.EqualTo(1));
        Assert.That(runDamage, Is.EqualTo(3));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(27));
        Assert.That(_combat.player.ShieldCharges, Is.EqualTo(1));
    }

    [Test]
    public void EncounterAndCombatReset_ClearPlayerAndEnemyShield()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Shield Reset", 10, 0, 0));
        _combat.player.GainShield(3);
        enemy.GainShield(2);

        _combat.StartEnemy(enemy);
        Assert.That(_combat.player.ShieldCharges, Is.Zero);
        Assert.That(enemy.ShieldCharges, Is.Zero);

        _combat.player.GainShield(1);
        enemy.GainShield(1);
        _combat.Reset();
        Assert.That(_combat.player.ShieldCharges, Is.Zero);
        Assert.That(enemy.ShieldCharges, Is.EqualTo(1), "Removed enemy state is not retained by CombatManager; a reused enemy resets on its next encounter.");

        _combat.StartEnemy(enemy);
        Assert.That(enemy.ShieldCharges, Is.Zero);
    }

    [Test]
    public void MultipleShieldCharges_BlockIndependentHitsOneAtATime()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Multiple Shield", 10, 0, 0));
        enemy.GainShield(2);
        var action = new CombatActionContext(5, CombatActionOrigin.PlayerCard, _combat.player, targetEnemy: enemy, hitCount: 3);
        var resolver = new CombatResolver();

        var first = resolver.Resolve(new DamageRequest(action, 0, CombatDamageOrigin.Card, _combat.player, enemy, 3), _combat);
        var second = resolver.Resolve(new DamageRequest(action, 1, CombatDamageOrigin.Card, _combat.player, enemy, 100), _combat);
        var third = resolver.Resolve(new DamageRequest(action, 2, CombatDamageOrigin.Card, _combat.player, enemy, 3), _combat);

        Assert.That(first.ShieldConsumed, Is.True);
        Assert.That(second.ShieldConsumed, Is.True);
        Assert.That(third.ShieldConsumed, Is.False);
        Assert.That(third.ActualHpLost, Is.EqualTo(3));
        Assert.That(enemy.ShieldCharges, Is.Zero);
        Assert.That(enemy.currentHp, Is.EqualTo(7));
    }

    [Test]
    public void AggregateEnemyRetaliation_RemainsOneDamageInstanceForShield()
    {
        var type = CreateEnemyType("Aggregate", 100, 4, 0);
        var first = new EnemyRuntime(type, 1, 2);
        var second = new EnemyRuntime(type, 2, 2);
        _combat.StartEncounter(new[] { first, second });
        Assert.That(_combat.TryPlayCards(new[] { LowestAttackView() }, first), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(8));
        _combat.GrantPlayerShield(1);

        _combat.TakeRemainingDamage();

        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
        Assert.That(_combat.player.ShieldCharges, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void EnemyRetaliationWithoutDefense_AppliesResolvedDamageAndReportsHpLoss()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Retaliation", 100, 7, 0));
        _combat.StartEnemy(enemy);
        Assert.That(_combat.TryPlayCards(new[] { LowestAttackView() }, enemy), Is.True);
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;
        int healthBefore = _combat.player.currentHealth;

        _combat.TakeRemainingDamage();

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Request.Origin, Is.EqualTo(CombatDamageOrigin.EnemyAggregate));
        Assert.That(results[0].RequestedDamage, Is.EqualTo(7));
        Assert.That(results[0].ActualHpLost, Is.EqualTo(7));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(healthBefore - 7));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void EnemyRetaliationAfterPartialBlock_AppliesOnlyPositiveResidualDamage()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Partial Block", 100, 20, 0));
        _combat.StartEnemy(enemy);
        Assert.That(_combat.TryPlayCards(new[] { LowestAttackView() }, enemy), Is.True);
        var defense = HandViews().First(view => _combat.CalculateCardDefense(view.data) > 0);
        int block = _combat.CalculateCardDefense(defense.data);
        int healthBefore = _combat.player.currentHealth;
        Assert.That(_combat.TryDefendWithCards(new[] { defense }), Is.True);
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;

        _combat.TakeRemainingDamage();

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].RequestedDamage, Is.EqualTo(20 - block));
        Assert.That(results[0].ActualHpLost, Is.EqualTo(20 - block));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(healthBefore - (20 - block)));
    }

    [Test]
    public void EnemyRetaliationAfterFullBlock_AppliesNoHpDamage()
    {
        var attackCard = LowestAttackView();
        var defenseCard = HandViews().First(view => view != attackCard && _combat.CalculateCardDefense(view.data) > 0);
        int block = _combat.CalculateCardDefense(defenseCard.data);
        var enemy = new EnemyRuntime(CreateEnemyType("Full Block", 100, block, 0));
        _combat.StartEnemy(enemy);
        Assert.That(_combat.TryPlayCards(new[] { attackCard }, enemy), Is.True);
        int healthBefore = _combat.player.currentHealth;
        Assert.That(_combat.TryDefendWithCards(new[] { defenseCard }), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(healthBefore));
    }

    [Test]
    public void MultiEnemyRetaliation_ResolvesOneAggregateDamageRequest()
    {
        var type = CreateEnemyType("Aggregate HP", 100, 4, 0);
        var first = new EnemyRuntime(type, 1, 2);
        var second = new EnemyRuntime(type, 2, 2);
        _combat.StartEncounter(new[] { first, second });
        Assert.That(_combat.TryPlayCards(new[] { LowestAttackView() }, first), Is.True);
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;

        _combat.TakeRemainingDamage();

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].RequestedDamage, Is.EqualTo(8));
        Assert.That(results[0].ActualHpLost, Is.EqualTo(8));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(22));
    }

    [Test]
    public void MultiEnemyAggregateAboveIntRange_SaturatesInsteadOfDroppingDamage()
    {
        var type = CreateEnemyType("Overflow Aggregate", 100, 1_500_000_000, 0);
        var first = new EnemyRuntime(type, 1, 1);
        var second = new EnemyRuntime(type, 2, 1);
        _combat.StartEncounter(new[] { first, second });
        Assert.That(_combat.TryPlayCards(new[] { LowestAttackView() }, first), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(int.MaxValue));
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;

        _combat.TakeRemainingDamage();

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].RequestedDamage, Is.EqualTo(int.MaxValue));
        Assert.That(results[0].ActualHpLost, Is.EqualTo(30));
        Assert.That(_combat.player.IsDefeated, Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameOver));
    }

    [Test]
    public void PassPlayerTurn_LeavesCardsUntouchedThenResolvesOneMultiEnemyResponse()
    {
        var ability = ScriptableObject.CreateInstance<CountingEnemyAbility>();
        _created.Add(ability);
        var type = CreateEnemyType("Pass Response", 100, 4, 0, ability);
        var first = new EnemyRuntime(type, 1, 2);
        var second = new EnemyRuntime(type, 2, 2);
        _combat.StartEncounter(new[] { first, second });
        var handBefore = _cards.hand.ToArray();
        var deckBefore = _cards.deck.ToArray();
        var discardBefore = _cards.discardPile.ToArray();
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;

        Assert.That(_combat.CanPassPlayerTurn, Is.True);
        Assert.That(_combat.PassPlayerTurn(), Is.True);

        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(_combat.pendingDamage, Is.EqualTo(8));
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(2));
        Assert.That(_combat.CompletedPlayerTurnCount, Is.EqualTo(1));
        Assert.That(_cards.hand, Is.EqualTo(handBefore));
        Assert.That(_cards.deck, Is.EqualTo(deckBefore));
        Assert.That(_cards.discardPile, Is.EqualTo(discardBefore));
        Assert.That(ability.CardResolvedCount, Is.Zero, "Pass does not trigger card-play effects.");
        Assert.That(results, Is.Empty, "Pass advances to, but does not bypass or pre-resolve, enemy retaliation.");

        _combat.TakeRemainingDamage();
        Assert.That(results, Has.Count.EqualTo(1), "Ordinary multi-enemy retaliation remains one aggregate damage action.");
        Assert.That(results[0].RequestedDamage, Is.EqualTo(8));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(22));
    }

    [Test]
    public void PassPlayerTurn_ConsumesBonusActionAndStartsEnemyResponseOnce()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Bonus Pass", 100, 3, 0));
        _combat.StartEnemy(enemy);
        typeof(CombatManager).GetMethod("BeginPlayerTurn", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(_combat, new object[] { true });
        Assert.That(_combat.IsBonusPlayerAction, Is.True);

        Assert.That(_combat.PassPlayerTurn(), Is.True);

        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(_combat.pendingDamage, Is.EqualTo(3));
        Assert.That(_combat.CompletedPlayerTurnCount, Is.EqualTo(1));
        Assert.That(_combat.QueuedExtraPlayerTurns, Is.Zero);
    }

    [Test]
    public void PassPlayerTurn_RejectsBlockedOrInvalidPhaseAndHandlesZeroAttackNormally()
    {
        var enemy = new EnemyRuntime(CreateEnemyType("Blocked Pass", 100, 4, 0));
        _combat.StartEnemy(enemy);
        var resolvingField = typeof(CombatManager).GetField("_isResolvingAction",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        resolvingField.SetValue(_combat, true);
        Assert.That(_combat.CanPassPlayerTurn, Is.False);
        Assert.That(_combat.PassPlayerTurn(), Is.False);
        resolvingField.SetValue(_combat, false);
        GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, true);
        try
        {
            Assert.That(_combat.CanPassPlayerTurn, Is.False);
            Assert.That(_combat.PassPlayerTurn(), Is.False);
        }
        finally { GameplayInputGate.Clear(); }
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_combat.PassPlayerTurn(), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(_combat.CanPassPlayerTurn, Is.False);
        Assert.That(_combat.PassPlayerTurn(), Is.False, "Pass is unavailable during the enemy response.");

        _combat.Reset();
        var harmless = new EnemyRuntime(CreateEnemyType("Zero Attack Pass", 100, 0, 0));
        _combat.StartEnemy(harmless);
        Assert.That(_combat.PassPlayerTurn(), Is.True);
        Assert.That(_combat.pendingDamage, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_combat.CompletedPlayerTurnCount, Is.EqualTo(1));
    }

    [Test]
    public void Recover_MultiEnemyAggregateAppliesReductionThenConsumesOneShieldAndDrawsOnce()
    {
        var reduction = ScriptableObject.CreateInstance<GearData>();
        reduction.id = "recover-reduction";
        reduction.effects = new[]
        {
            new GameplayEffectDefinition
            {
                kind = GameplayEffectKind.IncomingCombatDamageReduction,
                trigger = GameplayEffectTrigger.IncomingDamageCalculated,
                amount = 2,
                conditions = Array.Empty<GameplayEffectCondition>()
            }
        };
        _created.Add(reduction);
        var type = CreateEnemyType("Shielded Recover", 100, 6, 0);
        var firstEnemy = new EnemyRuntime(type, 1, 3);
        var secondEnemy = new EnemyRuntime(type, 2, 3);
        _cards.ownedGear.Add(reduction);
        _combat.StartEncounter(new[] { firstEnemy, secondEnemy });
        Assert.That(_combat.TotalEnemyAttack, Is.EqualTo(12));
        foreach (var view in HandViews().ToArray())
            Assert.That(_cards.TryDiscard(view), Is.True);
        _combat.GrantPlayerShield(1);
        var damageResults = new List<DamageResult>();
        _combat.OnDamageResolved += damageResults.Add;

        _combat.Recover();

        Assert.That(_combat.player.currentHealth, Is.EqualTo(30), "Typed incoming reduction applies before Shield.");
        Assert.That(_combat.player.ShieldCharges, Is.Zero, "One positive post-reduction aggregate hit consumes one Shield charge.");
        Assert.That(damageResults.Count, Is.EqualTo(1), "One aggregate Recover hit; no second enemy attack/Retaliation.");
        Assert.That(damageResults[0].Request.Origin, Is.EqualTo(CombatDamageOrigin.Recovery));
        Assert.That(damageResults[0].ModifiedDamage, Is.EqualTo(10));
        Assert.That(damageResults[0].ShieldConsumed, Is.True);
        Assert.That(damageResults[0].ShieldBlockedDamage, Is.EqualTo(10));
        Assert.That(_cards.HandCount, Is.EqualTo(1), "Survival makes one draw request.");
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void Recover_ZeroPostReductionDamagePreservesShieldAndStillDrawsOnce()
    {
        var reduction = ScriptableObject.CreateInstance<GearData>();
        reduction.id = "recover-full-reduction";
        reduction.effects = new[]
        {
            new GameplayEffectDefinition
            {
                kind = GameplayEffectKind.IncomingCombatDamageReduction,
                trigger = GameplayEffectTrigger.IncomingDamageCalculated,
                amount = 10,
                conditions = Array.Empty<GameplayEffectCondition>()
            }
        };
        _created.Add(reduction);
        _cards.ownedGear.Add(reduction);
        var type = CreateEnemyType("Fully Reduced Recover", 100, 4, 0);
        var firstEnemy = new EnemyRuntime(type, 1, 4);
        var secondEnemy = new EnemyRuntime(type, 2, 4);
        _combat.StartEncounter(new[] { firstEnemy, secondEnemy });
        foreach (var view in HandViews().ToArray())
            Assert.That(_cards.TryDiscard(view), Is.True);
        _combat.GrantPlayerShield(1);
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;

        _combat.Recover();

        Assert.That(results.Count, Is.EqualTo(1), "Both enemies contribute to one Recovery damage request.");
        Assert.That(results[0].RequestedDamage, Is.EqualTo(8));
        Assert.That(results[0].ModifiedDamage, Is.Zero);
        Assert.That(results[0].ShieldConsumed, Is.False);
        Assert.That(_combat.player.ShieldCharges, Is.EqualTo(1));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
        Assert.That(_cards.HandCount, Is.EqualTo(1));

        Assert.That(_combat.TakeRunDamage(2), Is.EqualTo(2), "Direct Run/Event HP costs bypass Shield.");
        Assert.That(_combat.player.ShieldCharges, Is.EqualTo(1));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(28));
    }

    [Test]
    public void DefeatedEnemy_DoesNotRunOrdinaryPerHitOrPostCardReactions()
    {
        var counter = CreateCountingAbility();
        var enemy = new EnemyRuntime(CreateEnemyType("Dead Reaction", 1, 0, 0, counter));
        _combat.StartEnemy(enemy);

        Assert.That(_combat.TryPlayCards(new[] { HighestAttackView() }, enemy, 3), Is.True);

        Assert.That(enemy.IsDefeated, Is.True);
        Assert.That(counter.HitResolvedCount, Is.Zero);
        Assert.That(counter.CardResolvedCount, Is.Zero);
    }

    [Test]
    public void MultiHit_ConsumesCardOnce_ResolvesOrderedHits_AndRunsOncePerCardEffectsOnce()
    {
        var counter = CreateCountingAbility();
        var reduction = CreateAbility(EnemyAbilityEffect.ReduceIncomingCardDamage, 1);
        var enemy = new EnemyRuntime(CreateEnemyType("Multi Hit", 100, 20, 0, reduction, counter));
        _combat.StartEnemy(enemy);
        var card = HighestAttackView();
        int damagePerHit = _combat.CalculateCardAttackDamage(card.data);
        int handBefore = _cards.HandCount;
        enemy.GainShield(1);

        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = "phase7b5_once";
        gear.displayName = "Once Per Card";
        gear.damageMultiplier = 1;
        gear.reduceEnemyAttackByCardValue = true;
        _created.Add(gear);
        _cards.ownedGear.Add(gear);

        var enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        enhancement.id = "phase7b5_heal";
        enhancement.displayName = "Heal Once";
        enhancement.healOnPlay = 2;
        _created.Add(enhancement);
        Assert.That(_cards.ApplyEnhancement(card.data.Id, enhancement), Is.True);
        _combat.TakeRunDamage(5);

        var results = new List<DamageResult>();
        _combat.OnDamageResolved += result =>
        {
            if (result.Request.Origin == CombatDamageOrigin.Card)
                results.Add(result);
        };

        Assert.That(_combat.TryPlayCards(new[] { card }, enemy, 3), Is.True);

        Assert.That(results.Select(result => result.Request.HitIndex), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(results.Count(result => result.ShieldConsumed), Is.EqualTo(1));
        Assert.That(results.Sum(result => result.ActualHpLost), Is.EqualTo((damagePerHit - 1) * 2));
        Assert.That(counter.HitResolvedCount, Is.EqualTo(3));
        Assert.That(counter.CardResolvedCount, Is.EqualTo(1));
        Assert.That(enemy.currentAttack, Is.EqualTo(Mathf.Max(0, 20 - card.data.BaseAttackValue)));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(27));
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore - 1));
        Assert.That(_cards.discardPile.Count, Is.EqualTo(1));
    }

    [Test]
    public void MultiHit_EarlyDeathStopsRemainingHitsAndDoesNotRetarget()
    {
        var type = CreateEnemyType("Multi Target", 1, 0, 4);
        var first = new EnemyRuntime(type, 1, 2);
        var second = new EnemyRuntime(type, 2, 2);
        _combat.StartEncounter(new[] { first, second });
        var card = HighestAttackView();
        int cardHitCount = 0;
        _combat.OnDamageResolved += result =>
        {
            if (result.Request.Origin == CombatDamageOrigin.Card)
                cardHitCount++;
        };

        Assert.That(_combat.TryPlayCards(new[] { card }, first, 3), Is.True);

        Assert.That(cardHitCount, Is.EqualTo(1));
        Assert.That(first.IsDefeated, Is.True);
        Assert.That(second.currentHp, Is.EqualTo(second.maxHp));
        Assert.That(_combat.Enemies, Is.EqualTo(new[] { second }));
        Assert.That(_cards.credits, Is.Zero);
    }

    [Test]
    public void ReentrantCardCommand_IsRejectedDuringCommittedAction()
    {
        var reentrant = ScriptableObject.CreateInstance<ReentrantCardPlayAbility>();
        _created.Add(reentrant);
        var enemy = new EnemyRuntime(CreateEnemyType("Reentrant", 100, 1, 0, reentrant));
        _combat.StartEnemy(enemy);
        var first = LowestAttackView();
        var second = HandViews().First(view => view != first);
        reentrant.Combat = _combat;
        reentrant.Card = second;
        reentrant.Target = enemy;

        Assert.That(_combat.TryPlayCards(new[] { first }, enemy), Is.True);

        Assert.That(reentrant.Attempted, Is.True);
        Assert.That(reentrant.Succeeded, Is.False);
        Assert.That(_cards.discardPile.Count, Is.EqualTo(1));
        Assert.That(_cards.hand, Does.Contain(second.data));
    }

    [Test]
    public void LethalReaction_StopsLaterQueuedDamageAndCompletesDefeatOnce()
    {
        var lethalReaction = ScriptableObject.CreateInstance<LethalThenFollowupReactiveAbility>();
        _created.Add(lethalReaction);
        var enemy = new EnemyRuntime(CreateEnemyType("Lethal Reaction", 100, 0, 0, lethalReaction));
        _combat.StartEnemy(enemy);
        lethalReaction.Combat = _combat;

        int reactiveDamageResults = 0;
        int encounterResults = 0;
        EncounterResult observedResult = EncounterResult.Victory;
        _combat.OnDamageResolved += result =>
        {
            if (result.Request.Origin == CombatDamageOrigin.Reactive)
                reactiveDamageResults++;
        };
        _combat.OnEncounterResult += result =>
        {
            encounterResults++;
            observedResult = result;
        };

        Assert.That(_combat.TryPlayCards(new[] { LowestAttackView() }, enemy), Is.True);

        Assert.That(lethalReaction.FollowupQueued, Is.True);
        Assert.That(_combat.player.IsDefeated, Is.True);
        Assert.That(reactiveDamageResults, Is.EqualTo(1));
        Assert.That(encounterResults, Is.EqualTo(1));
        Assert.That(observedResult, Is.EqualTo(EncounterResult.Defeat));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameOver));
        Assert.That(_combat.IsResolvingAction, Is.False);
        Assert.That(_combat.ActiveAction, Is.Null);
    }

    [Test]
    public void ReactionQueue_UsesExplicitOrder_DeduplicatesSameHook_AndAllowsRepeatedHits()
    {
        var queue = new CombatReactionQueue();
        var order = new List<string>();

        Assert.That(queue.Enqueue(10, -1, CombatReactionPhase.CardCommitted, CombatReactionSourceCategory.Enhancement, 0, 0, () => order.Add("enhancement"), priority: -10), Is.True);
        Assert.That(queue.Enqueue(10, -1, CombatReactionPhase.CardCommitted, CombatReactionSourceCategory.Gear, 1, 0, () => order.Add("artifact-1"), priority: -20), Is.True);
        Assert.That(queue.Enqueue(10, -1, CombatReactionPhase.CardCommitted, CombatReactionSourceCategory.Gear, 0, 1, () => order.Add("artifact-0-handler-1"), priority: -30), Is.True);
        Assert.That(queue.Enqueue(10, -1, CombatReactionPhase.CardCommitted, CombatReactionSourceCategory.Gear, 0, 0, () => order.Add("artifact-0"), priority: 20), Is.True);
        Assert.That(queue.Enqueue(10, -1, CombatReactionPhase.CardCommitted, CombatReactionSourceCategory.Gear, 0, 0, () => order.Add("duplicate"), priority: -20), Is.False);
        Assert.That(queue.Enqueue(10, 0, CombatReactionPhase.HitResolved, CombatReactionSourceCategory.EnemyAbility, 0, 0, () => order.Add("hit-0")), Is.True);
        Assert.That(queue.Enqueue(10, 1, CombatReactionPhase.HitResolved, CombatReactionSourceCategory.EnemyAbility, 0, 0, () => order.Add("hit-1")), Is.True);

        queue.ProcessPhase(CombatReactionPhase.CardCommitted);
        queue.ProcessPhase(CombatReactionPhase.HitResolved);

        Assert.That(order, Is.EqualTo(new[] { "artifact-0", "artifact-0-handler-1", "artifact-1", "enhancement", "hit-0", "hit-1" }));
    }

    [Test]
    public void ReactionQueue_RunawayWorkIsStoppedAndDiagnosed()
    {
        var queue = new CombatReactionQueue(operationLimit: 4);
        int handler = 0;
        Action schedule = null;
        schedule = () =>
        {
            int current = handler++;
            queue.Enqueue(1, 0, CombatReactionPhase.HitResolved, CombatReactionSourceCategory.Core, 0, current, schedule);
        };
        schedule();
        LogAssert.Expect(LogType.Error, "Combat reaction limit (4) exceeded. Remaining reactions were discarded.");

        Assert.That(queue.ProcessPhase(CombatReactionPhase.HitResolved), Is.False);
        Assert.That(queue.RunawayDetected, Is.True);
        Assert.That(queue.ProcessedCount, Is.EqualTo(4));
        Assert.That(queue.PendingCount, Is.Zero);
    }

    [Test]
    public void RunawayDuringLethalPlayerAction_DoesNotResolveVictoryOrGrantRewards()
    {
        var runaway = ScriptableObject.CreateInstance<RunawayOnHitAbility>();
        runaway.Combat = _combat;
        _created.Add(runaway);
        var enemy = new EnemyRuntime(CreateEnemyType("Runaway Player Action", 1000, 0, 75, runaway));
        _combat.StartEnemy(enemy);
        int results = 0;
        EncounterResult? lastResult = null;
        _combat.OnEncounterResult += value => { results++; lastResult = value; };

        LogAssert.Expect(LogType.Error,
            $"Combat reaction limit ({CombatReactionQueue.DefaultOperationLimit}) exceeded. Remaining reactions were discarded.");
        Assert.That(_combat.TryPlayCards(new[] { HighestAttackView() }, enemy), Is.True);

        Assert.That(enemy.IsDefeated, Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameOver));
        Assert.That(results, Is.EqualTo(1));
        Assert.That(lastResult, Is.EqualTo(EncounterResult.Aborted), "A rule fault aborts; it is not a player defeat.");
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_combat.TryPlayCards(new[] { LowestAttackView() }, enemy), Is.False);
        Assert.That(results, Is.EqualTo(1), "A terminal reaction fault must not publish a second result.");
    }

    [Test]
    public void RunawayDuringDefense_DoesNotResolveVictoryOrGrantRewards()
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = "test-runaway-counter";
        gear.effects = new[]
        {
            new GameplayEffectDefinition
            {
                kind = GameplayEffectKind.CounterDamage,
                trigger = GameplayEffectTrigger.AttackBlocked,
                amount = 1,
                conditions = Array.Empty<GameplayEffectCondition>()
            }
        };
        _created.Add(gear);
        Assert.That(_cards.BuyGear(gear, 0), Is.True);

        var enemy = new EnemyRuntime(CreateEnemyType("Runaway Defense", 1000, 1, 90));
        _combat.StartEnemy(enemy);
        Assert.That(_combat.TryPlayCards(new[] { HighestAttackView() }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));

        var runaway = ScriptableObject.CreateInstance<RunawayOnHitAbility>();
        runaway.Combat = _combat;
        _created.Add(runaway);
        enemy.type.abilities = new[] { runaway };
        int results = 0;
        EncounterResult? lastResult = null;
        _combat.OnEncounterResult += value => { results++; lastResult = value; };

        LogAssert.Expect(LogType.Error,
            $"Combat reaction limit ({CombatReactionQueue.DefaultOperationLimit}) exceeded. Remaining reactions were discarded.");
        Assert.That(_combat.TryDefendWithCards(new[] { LowestAttackView() }, enemy), Is.True);

        Assert.That(enemy.IsDefeated, Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameOver));
        Assert.That(results, Is.EqualTo(1));
        Assert.That(lastResult, Is.EqualTo(EncounterResult.Aborted), "A rule fault aborts; it is not a player defeat.");
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_combat.TryDefendWithCards(new[] { LowestAttackView() }, enemy), Is.False);
        Assert.That(results, Is.EqualTo(1), "A terminal reaction fault must not publish a second result.");
    }

    [Test]
    public void RunawayDuringEncounterStartup_StopsBeforeDrawAndPlayerTurn()
    {
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        _created.Add(ability);
        var type = CreateEnemyType("Runaway Encounter Start", 100, 0, 0);
        type.abilities = Enumerable.Repeat(ability, CombatReactionQueue.DefaultOperationLimit + 1).ToArray();
        var enemy = new EnemyRuntime(type);
        int handBefore = _cards.HandCount;
        int results = 0;
        EncounterResult? lastResult = null;
        _combat.OnEncounterResult += value => { results++; lastResult = value; };

        LogAssert.Expect(LogType.Error,
            $"Combat reaction limit ({CombatReactionQueue.DefaultOperationLimit}) exceeded. Remaining reactions were discarded.");
        _combat.StartEncounter(new[] { enemy });

        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameOver));
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore));
        Assert.That(results, Is.EqualTo(1));
        Assert.That(lastResult, Is.EqualTo(EncounterResult.Aborted), "A rule fault aborts; it is not a player defeat.");
        Assert.That(_combat.TryPlayCards(new[] { LowestAttackView() }, enemy), Is.False);
        Assert.That(results, Is.EqualTo(1), "A startup reaction fault must publish exactly one result.");
    }

    [Test]
    public void CombatReset_RestartsActionIdentityAndClearsTransientResolutionState()
    {
        var firstEnemy = new EnemyRuntime(CreateEnemyType("First Action", 100, 0, 0));
        _combat.StartEnemy(firstEnemy);
        long firstActionId = 0;
        _combat.OnDamageResolved += result => firstActionId = result.Request.Action.ActionId;
        Assert.That(_combat.TryPlayCards(new[] { LowestAttackView() }, firstEnemy), Is.True);
        Assert.That(firstActionId, Is.GreaterThan(0));

        _combat.Reset();
        var secondEnemy = new EnemyRuntime(CreateEnemyType("Second Action", 100, 0, 0));
        _combat.StartEnemy(secondEnemy);
        long restartedActionId = 0;
        _combat.OnDamageResolved += result => restartedActionId = result.Request.Action.ActionId;
        Assert.That(_combat.TryPlayCards(new[] { LowestAttackView() }, secondEnemy), Is.True);

        Assert.That(restartedActionId, Is.EqualTo(firstActionId));
        Assert.That(_combat.IsResolvingAction, Is.False);
        Assert.That(_combat.ActiveAction, Is.Null);
    }

    CountingEnemyAbility CreateCountingAbility()
    {
        var ability = ScriptableObject.CreateInstance<CountingEnemyAbility>();
        _created.Add(ability);
        return ability;
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
        type.abilities = abilities ?? Array.Empty<EnemyAbility>();
        _created.Add(type);
        return type;
    }

    CardView HighestAttackView() => HandViews().OrderByDescending(view => _combat.CalculateCardAttackDamage(view.data)).First();
    CardView LowestAttackView() => HandViews().OrderBy(view => _combat.CalculateCardAttackDamage(view.data)).First();

    List<CardView> HandViews()
    {
        var result = new List<CardView>();
        for (int i = 0; i < _field.cardsHolder.childCount; i++)
            if (_field.cardsHolder.GetChild(i).TryGetComponent<CardView>(out var view))
                result.Add(view);
        return result;
    }

    Field CreateHandField()
    {
        var canvas = CreateComponent<Canvas>("Phase7B5 Foundation Hand Canvas");
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

    GameObject CreateGameObject(string name, params Type[] components)
    {
        var gameObject = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }
}

public sealed class RunawayOnHitAbility : EnemyAbility
{
    public CombatManager Combat { get; set; }
    bool _queuedRunaway;

    public override void OnIncomingHitResolved(EnemyRuntime enemy, CombatManager context, DamageResult result)
    {
        if (_queuedRunaway) return;
        _queuedRunaway = true;
        int lethalDamage = enemy.currentHp + 1;
        for (int i = 0; i < CombatReactionQueue.DefaultOperationLimit + 4; i++)
            Combat.QueueReactiveDamage(enemy, enemy, i == 0 ? lethalDamage : 0, i, 0);
    }
}

public sealed class CountingEnemyAbility : EnemyAbility
{
    public int HitResolvedCount { get; private set; }
    public int CardResolvedCount { get; private set; }

    public override void OnEncounterStarted(EnemyRuntime enemy, CombatManager context)
    {
    }

    public override void OnIncomingHitResolved(EnemyRuntime enemy, CombatManager context, DamageResult result)
    {
        HitResolvedCount++;
    }

    public override void OnPlayerCardResolved(EnemyRuntime enemy, CombatManager context)
    {
        CardResolvedCount++;
    }
}

public sealed class ReentrantCardPlayAbility : EnemyAbility
{
    public CombatManager Combat { get; set; }
    public CardView Card { get; set; }
    public EnemyRuntime Target { get; set; }
    public bool Attempted { get; private set; }
    public bool Succeeded { get; private set; }

    public override void OnPlayerCardResolved(EnemyRuntime enemy, CombatManager context)
    {
        Attempted = true;
        Succeeded = Combat.TryPlayCards(new[] { Card }, Target);
    }
}

public sealed class LethalThenFollowupReactiveAbility : EnemyAbility
{
    public CombatManager Combat { get; set; }
    public bool FollowupQueued { get; private set; }

    public override void OnIncomingHitResolved(EnemyRuntime enemy, CombatManager context, DamageResult result)
    {
        Combat.QueueReactiveDamage(enemy, Combat.player, Combat.player.currentHealth, 0, 0);
        FollowupQueued = Combat.QueueReactiveDamage(enemy, Combat.player, 1, 1, 0);
    }
}
