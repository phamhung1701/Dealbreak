using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class Phase7GrowthTests
{
    GameObject _cardsObject;
    GameObject _combatObject;
    GameObject _runObject;
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    readonly List<GearData> _gears = new();
    readonly List<Object> _created = new();

    [SetUp]
    public void SetUp()
    {
        _cardsObject = new GameObject("Phase7 growth CardManager");
        _cards = _cardsObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        _combatObject = new GameObject("Phase7 growth CombatManager");
        _combat = _combatObject.AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        _runObject = new GameObject("Phase7 growth RunManager");
        _run = _runObject.AddComponent<RunManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_runObject != null) Object.DestroyImmediate(_runObject);
        if (_combatObject != null) Object.DestroyImmediate(_combatObject);
        if (_cardsObject != null) Object.DestroyImmediate(_cardsObject);
        foreach (var gear in _gears)
            if (gear != null) Object.DestroyImmediate(gear);
        foreach (var created in _created)
            if (created != null) Object.DestroyImmediate(created);
    }

    [Test]
    public void CrownOfEndurance_HeadlinerGrowthUsesActualHpLossAndDoesNotHeal()
    {
        var crown = Gear("rel_139", GearSpecialRule.CrownOfEndurance);
        _combat.player.TakeDamage(5);
        SetPrivate(_combat, "_encounterActualHpLost", 0);
        InvokeVictoryRewards(wasHeadliner: true);
        Assert.That(_combat.player.maxHealth, Is.EqualTo(36));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(25), "Max-HP growth does not heal.");

        SetPrivate(_combat, "_encounterActualHpLost", 1);
        InvokeVictoryRewards(wasHeadliner: true);
        Assert.That(_combat.player.maxHealth, Is.EqualTo(39));
    }

    [Test]
    public void EliteKillGrowth_IsRunLocalAndFeedsOnlyOpeningAttackBonuses()
    {
        var hunter = Gear("rel_119", GearSpecialRule.HuntersLedger);
        var trophy = Gear("rel_137", GearSpecialRule.TrophyRack);
        SetPrivate(_run, "_activeNode", new PathNode { kind = MapNodeType.Elite });
        SetPrivate(_combat, "_eliteKillCount", 1);
        InvokeVictoryRewards(wasHeadliner: false);
        Assert.That(_cards.GetGearInstance(hunter).State.GetCounter(-119), Is.EqualTo(1));
        Assert.That(_cards.GetGearInstance(trophy).State.GetCounter(-137), Is.EqualTo(1));

        _combat.StartEncounter(new[] { Enemy("Growth boss") }, MapNodeType.Headliner);
        var cardData = CardData.Create(CardData.Suit.Clubs, CardData.Rank.Five);
        _created.Add(cardData);
        var card = new CardInstance(cardData, 7001);
        SetPrivate(_combat, "_openingPlayerAttackInProgress", true);
        Assert.That(_combat.CalculateCardAttackDamage(card), Is.EqualTo(card.BaseAttackValue + 2));
        SetPrivate(_combat, "_openingPlayerAttackInProgress", false);
        SetPrivate(_combat, "_playerAttackActionCount", 1);
        Assert.That(_combat.CalculateCardAttackDamage(card), Is.EqualTo(card.BaseAttackValue),
            "Growth applies only to the first attack action in the Boss encounter.");
    }

    [Test]
    public void EliteFleeWithoutKill_DoesNotTriggerKillGrowth()
    {
        var hunter = Gear("rel_119", GearSpecialRule.HuntersLedger);
        SetPrivate(_run, "_activeNode", new PathNode { kind = MapNodeType.Elite });
        SetPrivate(_combat, "_eliteKillCount", 0);
        InvokeVictoryRewards(wasHeadliner: false);
        Assert.That(_cards.GetGearInstance(hunter).State.GetCounter(-119), Is.Zero);
    }

    [Test]
    public void RetaliationEpic_GrowsOnlyOnCounterattackKillsAndCarriesGrowthForward()
    {
        var retaliation = Gear("rel_111", GearSpecialRule.RetaliationEpic);
        var first = Enemy("Retaliation nonlethal", 5);
        var second = Enemy("Retaliation base kill", 4);
        var third = Enemy("Retaliation grown kill", 5);
        _combat.StartEncounter(new[] { first, second, third });
        var cardData = CardData.Create(CardData.Suit.Clubs, CardData.Rank.Four);
        _created.Add(cardData);
        var blockCard = new CardInstance(cardData, 7010);
        ResolveRetaliation(blockCard, first, 4);
        Assert.That(_cards.GetGearInstance(retaliation).State.GetCounter(-111), Is.Zero);
        ResolveRetaliation(blockCard, second, 4);
        Assert.That(_cards.GetGearInstance(retaliation).State.GetCounter(-111), Is.EqualTo(1));
        ResolveRetaliation(blockCard, third, 4);
        Assert.That(_cards.GetGearInstance(retaliation).State.GetCounter(-111), Is.EqualTo(2));
    }

    void ResolveRetaliation(CardInstance blockingCard, EnemyRuntime enemy, int incomingAttack)
    {
        var action = new CombatActionContext(9000 + enemy.currentHp, CombatActionOrigin.PlayerDefense,
            sourcePlayer: _combat.player, card: blockingCard, targetEnemy: enemy);
        var context = new AttackBlockedEffectContext(action, blockingCard, enemy, incomingAttack,
            0, _combat, _cards);
        var queue = (CombatReactionQueue)typeof(CombatManager)
            .GetField("_reactions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_combat);
        queue.Clear();
        SetPrivate(_combat, "_activeAction", action);
        SetPrivate(_combat, "_isResolvingAction", true);
        GameplayEffectResolver.EnqueueAttackBlocked(context, queue);
        typeof(CombatManager).GetMethod("ProcessReactions", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_combat, new object[] { CombatReactionPhase.AttackBlocked });
        SetPrivate(_combat, "_activeAction", null);
        SetPrivate(_combat, "_isResolvingAction", false);
        queue.Clear();
    }

    [Test]
    public void MasterThief_StealsUpToEightAndReturnsItExactlyOnceOnKill()
    {
        var type = EnemyTypeData.Create("Master Thief", 24, 4, 20);
        _created.Add(type);
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        ability.effect = EnemyAbilityEffect.MasterThief;
        ability.amount = AssetDatabase.LoadAssetAtPath<EnemyAbility>("Assets/Data/Enemies/Abilities/MasterThief.asset").amount;
        Assert.That(ability.amount, Is.EqualTo(8), "The steal limit is authored on the Master Thief ability.");
        type.abilities = new[] { ability };
        _created.Add(ability);
        var thief = new EnemyRuntime(type);
        _combat.StartEnemy(thief);
        _cards.AddCredits(12);
        typeof(CombatManager).GetMethod("CompleteEnemyResponse", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_combat, null);
        Assert.That(_cards.credits, Is.EqualTo(4));
        Assert.That(thief.StolenCredits, Is.EqualTo(8));
        typeof(CombatManager).GetMethod("CompleteEnemyResponse", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_combat, null);
        Assert.That(_cards.credits, Is.EqualTo(4), "Only the first enemy attack steals.");

        thief.DefeatInstantly();
        typeof(CombatManager).GetMethod("HandleEnemyDefeated", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_combat, new object[] { thief });
        Assert.That(_cards.credits, Is.EqualTo(12));
        Assert.That((int)typeof(CombatManager).GetField("_earnedCreditsReward", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(_combat), Is.EqualTo(20), "Normal Elite reward remains separate from the stolen-Gold refund.");
        typeof(CombatManager).GetMethod("HandleEnemyDefeated", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_combat, new object[] { thief });
        Assert.That(_cards.credits, Is.EqualTo(12), "A repeated death callback cannot refund twice.");
    }

    [Test]
    public void MasterThief_ZeroCreditsDoesNotDisableFourActionEscapeTimer()
    {
        var type = EnemyTypeData.Create("Master Thief", 24, 4, 0);
        _created.Add(type);
        type.fleeAfterPlayerTurns = 4;
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        ability.effect = EnemyAbilityEffect.MasterThief;
        type.abilities = new[] { ability };
        _created.Add(ability);
        var thief = new EnemyRuntime(type);
        Assert.That(thief.TryBeginMasterThiefSteal(), Is.True);
        thief.SetStolenCredits(0);
        for (int i = 0; i < 3; i++) thief.NotifyPlayerTurnCompleted();
        Assert.That(thief.ShouldFlee, Is.False);
        thief.NotifyPlayerTurnCompleted();
        Assert.That(thief.ShouldFlee, Is.True);
    }

    GearData Gear(string id, GearSpecialRule rule)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = id;
        gear.canonicalId = id;
        gear.displayName = id;
        gear.price = 0;
        gear.specialRule = rule;
        _gears.Add(gear);
        Assert.That(_cards.BuyGear(gear, 0), Is.True);
        return gear;
    }

    EnemyRuntime Enemy(string name) => Enemy(name, 20);

    EnemyRuntime Enemy(string name, int hp)
    {
        var type = EnemyTypeData.Create(name, hp, 2, 0);
        _created.Add(type);
        return new EnemyRuntime(type);
    }

    void InvokeVictoryRewards(bool wasHeadliner) => typeof(RunManager)
        .GetMethod("ApplyVictoryGearRewards", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(_run, new object[] { wasHeadliner });

    static void SetPrivate(object instance, string name, object value) => instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
}
