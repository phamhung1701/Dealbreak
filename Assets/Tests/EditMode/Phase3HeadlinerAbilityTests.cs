using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase3HeadlinerAbilityTests
{
    readonly List<Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void GeneratedRoster_KeepsTwelveFaceCardsWithDeterministicValidAbilityPairs()
    {
        var first = CreateGeneratedRun("phase3-seed");
        var signature = Signature(first.headlinerDeck);
        Assert.That(first.headlinerDeck.Count, Is.EqualTo(12));
        for (int group = 0; group < 3; group++)
        {
            var bosses = first.headlinerDeck.Skip(group * 4).Take(4).ToArray();
            Assert.That(bosses.Select(headliner => headliner.sourceCard.rank).Distinct().Count(), Is.EqualTo(1));
            Assert.That(bosses.Select(headliner => headliner.sourceCard.suit).Distinct().Count(), Is.EqualTo(4));
            foreach (var headliner in bosses)
            {
                Assert.That((headliner.maxHp, headliner.baseAttack), Is.EqualTo((30, 6)));
                Assert.That(headliner.creditsReward, Is.EqualTo(headliner.sourceCard.rank switch
                {
                    CardData.Rank.Jack => 10,
                    CardData.Rank.Queen => 15,
                    _ => 20
                }));
                Assert.That(headliner.abilities.Length, Is.EqualTo(headliner.sourceCard.rank == CardData.Rank.King ? 2 : 1));
                Assert.That(headliner.abilities.Select(a => a.effect).Distinct().Count(), Is.EqualTo(headliner.abilities.Length));
                if (headliner.sourceCard.rank == CardData.Rank.King)
                {
                    var hardCounters = headliner.abilities.Count(a => a.effect is EnemyAbilityEffect.Silence or
                        EnemyAbilityEffect.Withering or EnemyAbilityEffect.Oppression);
                    Assert.That(hardCounters, Is.LessThanOrEqualTo(1));
                    if (hardCounters == 0)
                    {
                        Assert.That(headliner.abilities.Count(a => a.effect is EnemyAbilityEffect.Guarded or EnemyAbilityEffect.Regeneration or
                            EnemyAbilityEffect.FullCoverageBlock), Is.EqualTo(1));
                        Assert.That(headliner.abilities.Count(a => a.effect is EnemyAbilityEffect.DoubleStrike or EnemyAbilityEffect.HeavySwing or EnemyAbilityEffect.Desperation), Is.EqualTo(1));
                    }
                    else
                        Assert.That(headliner.abilities.Count(a => a.effect is EnemyAbilityEffect.DoubleStrike or EnemyAbilityEffect.HeavySwing or
                            EnemyAbilityEffect.Desperation or EnemyAbilityEffect.Regeneration or EnemyAbilityEffect.Guarded or
                            EnemyAbilityEffect.FullCoverageBlock), Is.EqualTo(1));
                }
                else Assert.That(headliner.abilities.All(a => a.effect is EnemyAbilityEffect.DoubleStrike or EnemyAbilityEffect.HeavySwing or
                    EnemyAbilityEffect.Desperation or EnemyAbilityEffect.Regeneration or EnemyAbilityEffect.Guarded or
                    EnemyAbilityEffect.Silence or EnemyAbilityEffect.Withering or EnemyAbilityEffect.Oppression or
                    EnemyAbilityEffect.FullCoverageBlock), Is.True);
            }
        }
        DestroyGeneratedRun(first);

        var sameSeed = CreateGeneratedRun("phase3-seed");
        Assert.That(Signature(sameSeed.headlinerDeck), Is.EqualTo(signature));
        sameSeed.headlinerIndex = 4;
        var currentHeadliner = sameSeed.CurrentHeadliner;
        var description = sameSeed.CurrentHeadlinerDescription;
        var scaled = EnemyMapScaling.Scale(currentHeadliner.maxHp, currentHeadliner.baseAttack, sameSeed.CurrentMapIndex);
        Assert.That(description, Does.Contain($"{scaled.hp} HP / {scaled.attack} ATK"));
        Assert.That(description, Does.Contain(currentHeadliner.abilities[0].displayName));
        Assert.That(sameSeed.CurrentHeadlinerDescription, Is.EqualTo(description));
        Assert.That(sameSeed.CurrentHeadliner, Is.SameAs(currentHeadliner), "Reading/reopening the map description must not reroll the cached boss.");
        DestroyGeneratedRun(sameSeed);

        var changedSeed = CreateGeneratedRun("phase3-other-seed");
        Assert.That(Signature(changedSeed.headlinerDeck), Is.Not.EqualTo(signature));
        DestroyGeneratedRun(changedSeed);
    }

    RunManager CreateGeneratedRun(string seed)
    {
        var go = new GameObject("Phase3 generated run");
        _created.Add(go);
        var run = go.AddComponent<RunManager>();
        typeof(RunManager).GetField("_randomContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(run, new RunRandomContext(seed));
        typeof(RunManager).GetMethod("BuildHeadlinerDeck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(run, null);
        return run;
    }

    static string Signature(IReadOnlyList<EnemyTypeData> deck) => string.Join("|", deck.Select(headliner =>
        $"{headliner.sourceCard.rank}:{headliner.sourceCard.suit}:{string.Join(",", headliner.abilities.Select(a => a.effect))}"));

    void DestroyGeneratedRun(RunManager run)
    {
        _created.Remove(run.gameObject);
        Object.DestroyImmediate(run.gameObject);
    }

    [Test]
    public void GeneratedHeadlinerPool_CanRollUnyieldingDefenseWithoutMakingItUniversal()
    {
        int abilityCount = 0;
        int bossCount = 0;
        for (int i = 0; i < 40; i++)
        {
            var run = CreateGeneratedRun($"full-coverage-{i}");
            bossCount += run.headlinerDeck.Count;
            abilityCount += run.headlinerDeck.Count(headliner => headliner.abilities.Any(ability =>
                ability.effect == EnemyAbilityEffect.FullCoverageBlock));
            DestroyGeneratedRun(run);
        }
        Assert.That(abilityCount, Is.GreaterThan(0));
        Assert.That(abilityCount, Is.LessThan(bossCount));
    }

    [Test]
    public void FirstMapHeadlinerUsesTwentyHpWithoutChangingLaterHeadlinerDefinitions()
    {
        var run = CreateGeneratedRun("first-map-boss-nerf");
        var firstHeadliner = run.CurrentHeadliner;
        var firstNode = new PathNode { kind = MapNodeType.Headliner, mapIndex = 0 };
        Assert.That(firstHeadliner.maxHp, Is.EqualTo(30), "The generated definition remains reusable for later scaling.");
        Assert.That(run.GetEncounterStats(firstNode).hp, Is.EqualTo(run.FirstMapHeadlinerHp));
        Assert.That(run.CurrentHeadlinerDescription, Does.Contain($"{run.FirstMapHeadlinerHp} HP"));

        run.headlinerIndex = 1;
        var secondHeadliner = run.CurrentHeadliner;
        var secondNode = new PathNode { kind = MapNodeType.Headliner, mapIndex = 1 };
        Assert.That(run.GetEncounterStats(secondNode).hp,
            Is.EqualTo(EnemyMapScaling.Scale(secondHeadliner.maxHp, secondHeadliner.baseAttack, 1).hp));
        DestroyGeneratedRun(run);
    }

    [Test]
    public void HeadlinerSuitImmunity_UsesActionSuitSnapshotAndPreservesGuardAndShield()
    {
        var type = CreateEnemy(EnemyAbilityEffect.Guarded);
        var headlinerCard = CardData.Create(CardData.Suit.Hearts, CardData.Rank.King);
        _created.Add(headlinerCard);
        type.sourceCard = headlinerCard;
        var runtime = new EnemyRuntime(type);
        runtime.GainShield(1);

        var matchingCard = new CardInstance(CardData.Create(CardData.Suit.Clubs, CardData.Rank.Five), 1);
        _created.Add(matchingCard.Definition);
        matchingCard.TryChangeSuit(CardData.Suit.Hearts);
        var immune = ResolveDamage(runtime, matchingCard, CombatDamageOrigin.Card, 5);
        Assert.That(immune.ModifiedDamage, Is.Zero);
        Assert.That(immune.ActualHpLost, Is.Zero);
        Assert.That(runtime.ShieldCharges, Is.EqualTo(1), "An immune hit must not consume Shield.");

        var nonmatchingCard = new CardInstance(CardData.Create(CardData.Suit.Spades, CardData.Rank.Five), 2);
        _created.Add(nonmatchingCard.Definition);
        var next = ResolveDamage(runtime, nonmatchingCard, CombatDamageOrigin.Card, 5);
        Assert.That(next.ModifiedDamage, Is.EqualTo(3), "Guarded remains ready and halves the next non-immune hit half-up.");
        Assert.That(runtime.ShieldCharges, Is.EqualTo(0));
        Assert.That(next.ShieldConsumed, Is.True);
    }

    [Test]
    public void HeadlinerSuitImmunity_RunsAfterIncomingModifiersAndDoesNotApplyToNonCardDamage()
    {
        var type = CreateEnemy(EnemyAbilityEffect.ReduceIncomingCardDamage);
        type.abilities[0].amount = 2;
        var headlinerCard = CardData.Create(CardData.Suit.Diamonds, CardData.Rank.Queen);
        _created.Add(headlinerCard);
        type.sourceCard = headlinerCard;
        var runtime = new EnemyRuntime(type);
        var matching = new CardInstance(CardData.Create(CardData.Suit.Spades, CardData.Rank.Four), 3);
        _created.Add(matching.Definition);
        matching.TryChangeSuit(CardData.Suit.Diamonds);

        var immune = ResolveDamage(runtime, matching, CombatDamageOrigin.Card, 7);
        Assert.That(immune.ModifiedDamage, Is.Zero, "Card modifiers run before the intrinsic suit immunity.");

        var consumable = ResolveDamage(runtime, matching, CombatDamageOrigin.Consumable, 7);
        var reactive = ResolveDamage(runtime, matching, CombatDamageOrigin.Reactive, 7);
        Assert.That(consumable.ModifiedDamage, Is.EqualTo(7));
        Assert.That(reactive.ModifiedDamage, Is.EqualTo(7));
    }

    DamageResult ResolveDamage(EnemyRuntime enemy, CardInstance card, CombatDamageOrigin origin, int amount)
    {
        var action = new CombatActionContext(1, CombatActionOrigin.PlayerCard, card: card, targetEnemy: enemy);
        var request = new DamageRequest(action, 0, origin, null, enemy, amount);
        return new CombatResolver().Resolve(request, null);
    }

    [Test]
    public void HeavySwingAndDesperation_MultiplyBeforeSingleHalfUpRounding()
    {
        var heavy = CreateEnemy(EnemyAbilityEffect.HeavySwing);
        var heavyRuntime = new EnemyRuntime(heavy);
        Assert.That(heavyRuntime.PrepareResponseAttack(), Is.EqualTo(6));
        Assert.That(heavyRuntime.PrepareResponseAttack(), Is.EqualTo(9));

        var combined = CreateEnemy(EnemyAbilityEffect.HeavySwing, EnemyAbilityEffect.Desperation);
        var runtime = new EnemyRuntime(combined);
        runtime.currentHp = 14;
        Assert.That(runtime.PrepareResponseAttack(), Is.EqualTo(8));
        Assert.That(runtime.PrepareResponseAttack(), Is.EqualTo(11));
        runtime.currentHp = 15;
        runtime.ResetResponseIntent();
        Assert.That(runtime.PrepareResponseAttack(), Is.EqualTo(6), "Exactly 50% HP does not activate Desperation.");
    }

    [Test]
    public void Regeneration_HealsHalfUpAfterResponseAndStopsAfterThreeActivations()
    {
        var runtime = new EnemyRuntime(CreateEnemy(EnemyAbilityEffect.Regeneration));
        runtime.currentHp = 20;
        for (int i = 0; i < 4; i++) runtime.CompleteEnemyResponse();
        Assert.That(runtime.currentHp, Is.EqualTo(26), "5% of 30 HP is 1.5, rounded half-up to 2, for at most 3 activations.");
    }

    [Test]
    public void Guarded_ReducesFirstHitHalfUpAndResetsAfterResponse()
    {
        var runtime = new EnemyRuntime(CreateEnemy(EnemyAbilityEffect.Guarded));
        Assert.That(ApplyDamage(runtime, 3), Is.EqualTo(2));
        Assert.That(ApplyDamage(runtime, 3), Is.EqualTo(3));
        runtime.CompleteEnemyResponse();
        Assert.That(ApplyDamage(runtime, 3), Is.EqualTo(2));
    }

    int ApplyDamage(EnemyRuntime enemy, int damage)
    {
        var action = new CombatActionContext(1, CombatActionOrigin.Legacy, targetEnemy: enemy);
        var request = new DamageRequest(action, 0, CombatDamageOrigin.Card, null, enemy, damage);
        return enemy.ModifyIncomingCombatDamage(request, null, damage);
    }

    EnemyTypeData CreateEnemy(params EnemyAbilityEffect[] effects)
    {
        var type = EnemyTypeData.Create("Phase 3 Boss", 30, 6, 10);
        _created.Add(type);
        type.abilities = new EnemyAbility[effects.Length];
        for (int i = 0; i < effects.Length; i++)
        {
            var ability = ScriptableObject.CreateInstance<EnemyAbility>();
            _created.Add(ability);
            ability.effect = effects[i];
            type.abilities[i] = ability;
        }
        return type;
    }
}
