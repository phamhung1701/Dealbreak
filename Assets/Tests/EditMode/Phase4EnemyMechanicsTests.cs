using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase4EnemyMechanicsTests
{
    readonly List<UnityEngine.Object> _created = new();
    GameObject _cardsObject;
    GameObject _combatObject;
    GameObject _runObject;
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;

    [SetUp]
    public void SetUp()
    {
        _cardsObject = new GameObject("Phase4 mechanics CardManager");
        _cards = _cardsObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, Array.Empty<GearData>(), Array.Empty<CardEnhancementData>());
        _combatObject = new GameObject("Phase4 mechanics CombatManager");
        _combat = _combatObject.AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
    }

    [TearDown]
    public void TearDown()
    {
        if (_runObject != null) UnityEngine.Object.DestroyImmediate(_runObject);
        if (_combatObject != null) UnityEngine.Object.DestroyImmediate(_combatObject);
        if (_cardsObject != null) UnityEngine.Object.DestroyImmediate(_cardsObject);
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void Oppression_ThresholdAndDoubleStrikeUseOneBudgetBeforeTwoHalfUpHits()
    {
        var runtime = Enemy(EnemyAbilityEffect.Oppression, EnemyAbilityEffect.DoubleStrike);
        var threeHeld = runtime.PrepareResponseHits(3);
        CollectionAssert.AreEqual(new[] { 4, 4 }, threeHeld, "6 × 60% rounds 3.6 half-up to 4 per hit.");
        runtime.ResetResponseIntent();
        var fourHeld = runtime.PrepareResponseHits(4);
        CollectionAssert.AreEqual(new[] { 5, 5 }, fourHeld,
            "The fourth held card adds +2 once to the 6-point budget before each 60% hit is rounded.");
        Assert.That(runtime.PreparedResponseBudget, Is.EqualTo(8));
    }

    [Test]
    public void DoubleStrike_IsTwoMitigationEventsWhileOrdinaryResponsesStayAggregate()
    {
        var striker = Enemy(EnemyAbilityEffect.DoubleStrike);
        _combat.StartEnemy(striker);
        _combat.player.GainShield(1);
        InstallPending(new[] { 5, 5 }, new[] { striker, striker });
        int doubleStrikeEvents = 0;
        _combat.OnDamageResolved += _ => doubleStrikeEvents++;
        _combat.TakeRemainingDamage();
        Assert.That(doubleStrikeEvents, Is.EqualTo(2));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(25));
        Assert.That(_combat.player.shieldCharges, Is.Zero);

        _combat.Reset();
        var first = Enemy();
        var second = Enemy();
        _combat.StartEncounter(new[] { first, second });
        _combat.player.GainShield(1);
        InstallPending(new[] { 6, 6 }, new[] { first, second });
        int ordinaryEvents = 0;
        _combat.OnDamageResolved += _ => ordinaryEvents++;
        _combat.TakeRemainingDamage();
        Assert.That(ordinaryEvents, Is.EqualTo(1), "Ordinary enemy attacks retain their single aggregate mitigation event.");
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30), "One Shield blocks the ordinary aggregate hit.");
    }

    [Test]
    public void Withering_BlocksDirectAndCombatHealingOnlyAboveHalfAndNeverOverheals()
    {
        var withering = Enemy(EnemyAbilityEffect.Withering);
        _combat.StartEnemy(withering);
        _combat.player.TakeDamage(5);
        Assert.That(_combat.player.Heal(3), Is.Zero, "Direct PlayerRuntime.Heal must honor the active Withering gate.");
        Assert.That(_combat.HealPlayer(3), Is.Zero);
        _combat.IncreasePlayerMaxHealth(2);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(25), "A max-health gain cannot smuggle healing through Withering.");
        withering.currentHp = 15;
        Assert.That(_combat.player.Heal(3), Is.EqualTo(3), "Exactly 50% is no longer above the threshold.");
        Assert.That(_combat.player.Heal(10), Is.EqualTo(4), "Healing is capped at the increased maximum health.");
        Assert.That(_combat.player.Heal(3), Is.Zero, "No additional healing may overheal.");
    }

    [Test]
    public void Silence_SuppressesAuxiliaryMendingAndGoldenHeldCardEffectsUntilThreshold()
    {
        var auxiliary = Enhancement(GameplayEffectKind.FlatAttack, GameplayEffectTrigger.InHandAttackCalculated, 3);
        var mending = Enhancement(GameplayEffectKind.Heal, GameplayEffectTrigger.PlayerTurnStart, 2);
        var golden = Enhancement(GameplayEffectKind.BonusCredits, GameplayEffectTrigger.AttackCommitted, 2);
        var heldAuxiliary = Card(auxiliary, 1);
        var heldMending = Card(mending, 2);
        var heldGolden = Card(golden, 3);
        var attacker = Card(null, 4);
        Collection(heldAuxiliary, heldMending, heldGolden, attacker);
        Assert.That(_combat.CalculateCardAttackDamage(attacker), Is.EqualTo(attacker.BaseAttackValue + 3));

        var silence = Enemy(EnemyAbilityEffect.Silence);
        silence.currentHp = 16;
        _combat.player.TakeDamage(5);
        _combat.StartEnemy(silence);
        Assert.That(_combat.CalculateCardAttackDamage(attacker), Is.EqualTo(attacker.BaseAttackValue));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(25), "Mending is suppressed at player-turn start.");
        Assert.That(_cards.credits, Is.Zero);

        var action = new CombatActionContext(100, CombatActionOrigin.PlayerCard,
            sourcePlayer: _combat.player, card: attacker, targetEnemy: silence);
        var queue = new CombatReactionQueue();
        GameplayEffectResolver.EnqueueAttackCommitted(
            new AttackCommittedEffectContext(action, new[] { attacker }, _cards.hand.ToArray(), silence, _combat, _cards), queue);
        queue.ProcessPhase(CombatReactionPhase.AttackCommitted);
        Assert.That(_cards.credits, Is.Zero, "Golden's held trigger is suppressed too.");

        silence.currentHp = 15;
        Assert.That(GameplayEffectResolver.HeldCardEffectsSuppressed(_combat), Is.False,
            "Effects resume at exactly 50% HP.");
        var turnQueue = new CombatReactionQueue();
        GameplayEffectResolver.EnqueuePlayerTurnStart(
            new PlayerTurnStartEffectContext(action, _combat.player, _combat, _cards, 2), turnQueue);
        turnQueue.ProcessPhase(CombatReactionPhase.PlayerTurnStarted);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(27), "Mending resumes when Silence turns off.");
    }

    [Test]
    public void Duelist_UsesAnnouncedSuitHalfUpAndConsumesOnlyFirstMatchingHit()
    {
        var duelist = Enemy(EnemyAbilityEffect.SuitCall);
        var first = Card(null, 10, CardData.Suit.Spades);
        var second = Card(null, 11, CardData.Suit.Spades);
        duelist.SetAnnouncedSuit(CardData.Suit.Spades);
        Assert.That(ResolveCardHit(duelist, first, 5), Is.EqualTo(3));
        Assert.That(duelist.DuelistCallConsumed, Is.True);
        Assert.That(ResolveCardHit(duelist, second, 5), Is.EqualTo(5));
        duelist.SetAnnouncedSuit(CardData.Suit.Hearts);
        Assert.That(duelist.DuelistCallConsumed, Is.False);
    }

    [Test]
    public void Duelist_AnnouncedSuitUsesDeterministicTurnStream()
    {
        var duelist = Enemy(EnemyAbilityEffect.SuitCall);
        var seed = new RunRandomContext("duelist-repeatable");
        var expected = seed.CreateStream("duelist-suit");
        var expectedFirst = (CardData.Suit)expected.NextInt(0, Enum.GetValues(typeof(CardData.Suit)).Length);
        var expectedSecond = (CardData.Suit)expected.NextInt(0, Enum.GetValues(typeof(CardData.Suit)).Length);
        _combat.ConfigureDuelistSuitRandom(new RunRandomContext("duelist-repeatable").CreateStream("duelist-suit"));
        _combat.StartEnemy(duelist);
        Assert.That(duelist.AnnouncedSuit, Is.EqualTo(expectedFirst));
        typeof(CombatManager).GetMethod("BeginPlayerTurn", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_combat, new object[] { true });
        Assert.That(duelist.AnnouncedSuit, Is.EqualTo(expectedSecond), "Bonus turns announce a fresh suit from the seeded stream.");
    }

    [Test]
    public void AuthoredNormalCompositionsKeepDuelistSoloAndSpawnWarDrummerAllies()
    {
        var brute = EnemyType("Brute", 18, 3);
        var goblin = EnemyType("Goblin", 3, 2);
        var duelist = EnemyType("Duelist", 14, 4, EnemyAbilityEffect.SuitCall);
        var drummer = EnemyType("War Drummer", 8, 1, EnemyAbilityEffect.WarDrum);
        drummer.allies = new[] { new EncounterAllySpec { type = goblin, count = 2 } };
        var captain = EnemyType("Goblin Captain", 16, 3, EnemyAbilityEffect.Captaincy);
        captain.allies = new[] { new EncounterAllySpec { type = goblin, count = 2 } };
        var royalKnight = EnemyType("Royal Knight", 32, 5, EnemyAbilityEffect.RoyalGuard);
        var catalog = ScriptableObject.CreateInstance<RunContentCatalog>();
        _created.Add(catalog);
        catalog.normalEnemies = new[] { brute, goblin, duelist, drummer };
        catalog.eliteEnemies = new[] { captain, royalKnight };
        _runObject = new GameObject("Phase4 composition RunManager");
        _run = _runObject.AddComponent<RunManager>();
        _run.contentCatalog = catalog;
        typeof(RunManager).GetMethod("TryStartEncounter", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_run, new object[] { new PathNode { kind = MapNodeType.Combat, contentId = "Duelist", mapIndex = 0 } });
        Assert.That(_combat.Enemies.Select(enemy => enemy.type.enemyName), Is.EqualTo(new[] { "Duelist" }));
        var duelistNode = new PathNode { kind = MapNodeType.Combat, contentId = "Duelist", mapIndex = 0, accessible = true, revealed = true };
        Assert.That(_run.GetNodeDesc(duelistNode), Does.Not.Contain("Brute"));

        _combat.Reset();
        typeof(RunManager).GetMethod("TryStartEncounter", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_run, new object[] { new PathNode { kind = MapNodeType.Combat, contentId = "WarDrummer", mapIndex = 0 } });
        Assert.That(_combat.Enemies.Select(enemy => enemy.type.enemyName), Is.EqualTo(new[] { "War Drummer", "Goblin", "Goblin" }));
        Assert.That(catalog.normalEnemies.Any(enemy => enemy.enemyName is "Goblin Captain" or "Royal Knight"), Is.False,
            "Elite variants remain outside the normal encounter pool.");
    }

    [Test]
    public void WarDrum_BuffsEachAllyToThreeAndDeathStopsFutureGrowthWithoutRemovingBuffs()
    {
        var drummer = Enemy(EnemyAbilityEffect.WarDrum);
        var allyA = Enemy();
        var allyB = Enemy();
        var group = new[] { drummer, allyA, allyB };
        for (int i = 0; i < 3; i++) drummer.CompleteEnemyResponse(group);
        Assert.That(allyA.currentAttack, Is.EqualTo(allyA.type.baseAttack + 3));
        Assert.That(allyB.currentAttack, Is.EqualTo(allyB.type.baseAttack + 3));
        drummer.DefeatInstantly();
        drummer.CompleteEnemyResponse(group);
        Assert.That(allyA.currentAttack, Is.EqualTo(allyA.type.baseAttack + 3));
        Assert.That(allyB.currentAttack, Is.EqualTo(allyB.type.baseAttack + 3));
        Assert.That(drummer.currentAttack, Is.EqualTo(drummer.type.baseAttack), "The drummer cannot self-buff.");
    }

    int ResolveCardHit(EnemyRuntime target, CardInstance card, int damage)
    {
        var action = new CombatActionContext(1, CombatActionOrigin.PlayerCard, card: card, targetEnemy: target);
        var request = new DamageRequest(action, 0, CombatDamageOrigin.Card, _combat.player, target, damage, hitCard: card);
        return target.ModifyIncomingCombatDamage(request, _combat, damage);
    }

    EnemyTypeData EnemyType(string name, int hp, int attack, params EnemyAbilityEffect[] effects)
    {
        var type = EnemyTypeData.Create(name, hp, attack, 0);
        type.name = name.Replace(" ", string.Empty);
        _created.Add(type);
        type.abilities = effects.Select(effect =>
        {
            var ability = ScriptableObject.CreateInstance<EnemyAbility>();
            ability.effect = effect;
            _created.Add(ability);
            return ability;
        }).ToArray();
        return type;
    }

    EnemyRuntime Enemy(params EnemyAbilityEffect[] effects)
    {
        var type = EnemyTypeData.Create("Phase4 Enemy", 30, 6, 0);
        _created.Add(type);
        type.abilities = effects.Select(effect =>
        {
            var ability = ScriptableObject.CreateInstance<EnemyAbility>();
            ability.effect = effect;
            _created.Add(ability);
            return ability;
        }).ToArray();
        return new EnemyRuntime(type);
    }

    CardEnhancementData Enhancement(GameplayEffectKind kind, GameplayEffectTrigger trigger, int amount)
    {
        var data = ScriptableObject.CreateInstance<CardEnhancementData>();
        data.displayName = kind.ToString();
        data.effects = new[] { new GameplayEffectDefinition { kind = kind, trigger = trigger, amount = amount } };
        _created.Add(data);
        return data;
    }

    CardInstance Card(CardEnhancementData enhancement, int id, CardData.Suit suit = CardData.Suit.Clubs)
    {
        var definition = CardData.Create(suit, CardData.Rank.Five);
        _created.Add(definition);
        var card = new CardInstance(definition, id);
        if (enhancement != null) card.TryApplyEnhancement(enhancement);
        return card;
    }

    void Collection(params CardInstance[] cards)
    {
        var field = typeof(CardManager).GetField("_cards", BindingFlags.Instance | BindingFlags.NonPublic);
        var collection = (CardCollection)field.GetValue(_cards);
        collection.Initialize(cards);
        collection.DrawToHand(cards.Length, 10);
    }

    void InstallPending(int[] damage, EnemyRuntime[] sources)
    {
        var pending = (List<int>)typeof(CombatManager).GetField("_pendingAttacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_combat);
        var attackers = (List<EnemyRuntime>)typeof(CombatManager).GetField("_pendingAttackSources", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_combat);
        pending.Clear(); pending.AddRange(damage);
        attackers.Clear(); attackers.AddRange(sources);
        _combat.pendingDamage = damage.Sum();
        _combat.currentState = GameState.EnemyAttacking;
    }
}
