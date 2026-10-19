using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class Phase7GearMechanicsTests
{
    GameObject _cardsObject;
    GameObject _combatObject;
    GameObject _runObject;
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    readonly List<UnityEngine.Object> _created = new();

    [SetUp]
    public void SetUp()
    {
        _cardsObject = new GameObject("Phase7 mechanics CardManager");
        _cards = _cardsObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, Array.Empty<GearData>(), Array.Empty<CardEnhancementData>());
        _combatObject = new GameObject("Phase7 mechanics CombatManager");
        _combat = _combatObject.AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        _runObject = new GameObject("Phase7 mechanics RunManager");
        _run = _runObject.AddComponent<RunManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_runObject != null) UnityEngine.Object.DestroyImmediate(_runObject);
        if (_combatObject != null) UnityEngine.Object.DestroyImmediate(_combatObject);
        if (_cardsObject != null) UnityEngine.Object.DestroyImmediate(_cardsObject);
        foreach (var value in _created)
            if (value != null) UnityEngine.Object.DestroyImmediate(value);
        GameplayInputGate.Clear();
    }

    [Test]
    public void UpgradeAssets_RetainOnlyWorkbookRequiredPredecessorData()
    {
        var aceRare = Asset("AceRare");
        var aceEpic = Asset("AceEpic");
        var mimic = Asset("MimicEpic");
        var dwarfRare = Asset("DwarfRare");
        var dwarfEpic = Asset("DwarfEpic");
        var hands = Asset("HandsEpic");
        var spadeRare = Asset("SpadeRare");
        var spadeEpic = Asset("SpadeEpic");
        var hammerCommon = Asset("Hammer");
        var hammerRare = Asset("HammerRare");
        Assert.That(aceRare.effects.Length, Is.EqualTo(1));
        Assert.That(aceEpic.effects.Length, Is.EqualTo(1));
        Assert.That(mimic.effects.Any(effect => effect.kind == GameplayEffectKind.MimicInHandEffects), Is.True);
        Assert.That(dwarfRare.ruleModifiers.Length, Is.EqualTo(1));
        Assert.That(dwarfEpic.ruleModifiers.Length, Is.EqualTo(1));
        Assert.That(hands.ruleModifiers.Single().sameRankMultiCard.maximumCards, Is.EqualTo(4));
        Assert.That(spadeRare.effects.Any(effect => effect.kind == GameplayEffectKind.BlockMultiplier), Is.True);
        Assert.That(spadeEpic.effects.Any(effect => effect.kind == GameplayEffectKind.BlockMultiplier), Is.True);
        Assert.That(hammerCommon.canonicalId, Is.EqualTo("rel_009"));
        Assert.That(hammerCommon.effects.Single().kind, Is.EqualTo(GameplayEffectKind.GrantRandomCommonEnhancement));
        Assert.That(hammerRare.canonicalId, Is.EqualTo("rel_140"));
        Assert.That(hammerRare.upgradeFromId, Is.EqualTo("rel_009"));
        Assert.That(hammerRare.specialRule, Is.EqualTo(GearSpecialRule.Hammer));
        Assert.That(AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/SurveyorMap.asset"), Is.Null,
            "Surveyor’s Map is deliberately deferred as redundant.");
        var thief = AssetDatabase.LoadAssetAtPath<EnemyTypeData>("Assets/Data/Enemies/MasterThief.asset");
        Assert.That(thief, Is.Not.Null);
        Assert.That((thief.maxHp, thief.baseAttack, thief.creditsReward, thief.fleeAfterPlayerTurns),
            Is.EqualTo((24, 4, 20, 4)));
    }

    [Test]
    public void AceReturn_IsOncePerEncounterAndNeverDuplicatesThePhysicalCard()
    {
        var gear = Gear("rel_114", GearSpecialRule.AceRare);
        var ace = Card(CardData.Suit.Clubs, CardData.Rank.Ace, 21);
        var face = Card(CardData.Suit.Hearts, CardData.Rank.Jack, 22);
        SetOwnedCards(new[] { ace, face });
        var collection = CardCollectionOf(_cards);
        collection.DrawToHand(2, 2);
        Assert.That(collection.TryDiscard(ace), Is.True);
        Invoke(_combat, "TryReturnCriticalAce", new object[] { new[] { ace, face }, _cards });
        Assert.That(_cards.GetGearInstance(gear).State.GetEncounterCounter(-114), Is.EqualTo(1));
        Assert.That(_cards.hand.Count(card => ReferenceEquals(card, ace)), Is.EqualTo(1));
        Assert.That(collection.OwnedCards.Count, Is.EqualTo(2));
        Assert.That(collection.TryDiscard(ace), Is.True);
        Invoke(_combat, "TryReturnCriticalAce", new object[] { new[] { ace, face }, _cards });
        Assert.That(collection.DiscardPile.Count(card => ReferenceEquals(card, ace)), Is.EqualTo(1));
        _cards.ResetGearEncounterEffectState();
        Invoke(_combat, "TryReturnCriticalAce", new object[] { new[] { ace, face }, _cards });
        Assert.That(_cards.hand.Count(card => ReferenceEquals(card, ace)), Is.EqualTo(1));
    }

    [Test]
    public void MimicEpic_RetainsBaseAllHeldTriggersAndAddsOneOnlyForMatchingRank()
    {
        var mimic = Gear("rel_116", GearSpecialRule.MimicEpic);
        mimic.effects = new[] { new GameplayEffectDefinition
        {
            kind = GameplayEffectKind.MimicInHandEffects,
            trigger = GameplayEffectTrigger.CardCommitted,
            amount = 1
        } };
        var matching = Card(CardData.Suit.Hearts, CardData.Rank.Five, 31);
        var other = Card(CardData.Suit.Spades, CardData.Rank.King, 32);
        var played = Card(CardData.Suit.Clubs, CardData.Rank.Five, 33);
        Assert.That(matching.TryApplyEnhancement(Enhancement(GameplayEffectKind.Heal,
            GameplayEffectTrigger.PlayerTurnStart, 2)), Is.True);
        Assert.That(other.TryApplyEnhancement(Enhancement(GameplayEffectKind.Heal,
            GameplayEffectTrigger.PlayerTurnStart, 2)), Is.True);
        SetOwnedCards(new[] { matching, other });
        CardCollectionOf(_cards).DrawToHand(2, 2);
        _combat.player.TakeDamage(10);
        var action = new CombatActionContext(31, CombatActionOrigin.PlayerCard,
            sourcePlayer: _combat.player, card: played);
        var queue = new CombatReactionQueue();
        GameplayEffectResolver.EnqueueCardCommitted(
            new CardCommittedEffectContext(action, played, null, _combat, _cards, 0), queue);
        Assert.That(queue.ProcessPhase(CombatReactionPhase.CardCommitted), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(26),
            "Base Mimic heals both held cards once; Epic adds exactly one matching-rank retrigger.");
    }

    [Test]
    public void MasterThief_FourthActionFleeGivesNoKillCreditsEvenOnBonusAction()
    {
        var type = EnemyTypeData.Create("Master Thief", 24, 4, 20);
        type.fleeAfterPlayerTurns = 4;
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        ability.effect = EnemyAbilityEffect.MasterThief;
        type.abilities = new[] { ability };
        _created.Add(type);
        _created.Add(ability);
        var thief = new EnemyRuntime(type);
        _combat.StartEncounter(new[] { thief }, MapNodeType.Elite);
        Invoke(_combat, "CompleteEnemyResponse", null);
        for (int i = 0; i < 3; i++) Invoke(_combat, "CompletePlayerTurnForEnemies", null);
        Assert.That(_combat.Enemies.Contains(thief), Is.True);
        SetPrivate(_combat, "_isBonusPlayerAction", true);
        Invoke(_combat, "CompletePlayerTurnForEnemies", null);
        Assert.That(_combat.Enemies.Contains(thief), Is.False);
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(GetPrivate<int>(_combat, "_earnedCreditsReward"), Is.Zero);
    }

    [Test]
    public void HammerRare_CachesThreeCommonChoicesAndAppliesOneChosenTarget()
    {
        var common = Asset("Hammer");
        var rare = Asset("HammerRare");
        _cards.gearCatalog.AddRange(new[] { common, rare });
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        Assert.That(_cards.BuyGear(rare, 0), Is.True);
        foreach (var name in new[] { "Sharpened", "Reinforced", "Mending", "Quickdraw" })
            _cards.enhancementCatalog.Add(AssetDatabase.LoadAssetAtPath<CardEnhancementData>(
                $"Assets/Data/Enhancements/{name}.asset"));
        var card = Card(CardData.Suit.Clubs, CardData.Rank.Seven, 910);
        SetOwnedCards(new[] { card });
        CardCollectionOf(_cards).DrawToHand(1, 1);
        SetPrivate(_run, "_completedNodeCount", 3);
        RunEventDefinition shown = null;
        _run.OnShowEvent += definition => shown = definition;
        Invoke(_run, "TryOpenHammerReward", null);
        Assert.That(shown, Is.Not.Null);
        Assert.That(shown.choices.Length, Is.EqualTo(1));
        var choices = _run.GetEventEnhancementsForChoice(0).ToArray();
        Assert.That(choices.Length, Is.EqualTo(3));
        Assert.That(choices.Select(value => value.canonicalId).Distinct().Count(), Is.EqualTo(3));
        Assert.That(choices.All(value => value.rarity == "Common"), Is.True);
        Assert.That(_run.ChooseEventEnhancementTarget(0, card.Id, choices[0]), Is.True);
        Assert.That(card.Enhancement, Is.SameAs(choices[0]));
        Assert.That(_run.IsPendingHammerReward, Is.False);
        Assert.That(GameplayInputGate.IsBlocked, Is.False);
    }

    [Test]
    public void HammerRare_NoEligibleTargetOffersRetryOrDeclineWithoutSilentLoss()
    {
        var common = Asset("Hammer");
        var rare = Asset("HammerRare");
        _cards.gearCatalog.AddRange(new[] { common, rare });
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        Assert.That(_cards.BuyGear(rare, 0), Is.True);
        foreach (var name in new[] { "Sharpened", "Reinforced", "Mending", "Quickdraw" })
            _cards.enhancementCatalog.Add(AssetDatabase.LoadAssetAtPath<CardEnhancementData>(
                $"Assets/Data/Enhancements/{name}.asset"));
        var full = Card(CardData.Suit.Clubs, CardData.Rank.Seven, 911);
        SetOwnedCards(new[] { full });
        Assert.That(_cards.ApplyEnhancement(full.Id, _cards.enhancementCatalog[0]), Is.True);
        SetPrivate(_run, "_completedNodeCount", 3);
        RunEventDefinition shown = null;
        _run.OnShowEvent += definition => shown = definition;
        Invoke(_run, "TryOpenHammerReward", null);
        Assert.That(shown.choices.Select(choice => choice.interaction), Is.EqualTo(new[]
        { RunEventChoiceInteraction.HammerRetry, RunEventChoiceInteraction.HammerDecline }));
        Assert.That(_run.RetryHammerReward(), Is.True);
        Assert.That(_run.IsPendingHammerReward, Is.True);
        Assert.That(GameplayInputGate.IsBlocked, Is.False);

        var eligible = Card(CardData.Suit.Hearts, CardData.Rank.Ten, 912);
        SetOwnedCards(new[] { full, eligible });
        // Card-first Hammer (#38) targets a card in hand, so an eligible target must be drawn.
        _cards.DrawToHand(2);
        SetPrivate(_run, "_completedNodeCount", 4);
        Invoke(_run, "TryOpenHammerReward", null);
        Assert.That(_run.ActiveEvent.choices[0].interaction, Is.EqualTo(RunEventChoiceInteraction.ChooseEnhancementTarget));
        Assert.That(_run.DeclineHammerReward(), Is.True);
        Assert.That(_run.IsPendingHammerReward, Is.False);
    }

    [Test]
    public void ArsenalEpic_UsesFlooredHarmonicFaceCardCapacityWithoutShrinkingHand()
    {
        Gear("rel_030", GearSpecialRule.ArsenalEpic);
        var faces = new[]
        {
            Card(CardData.Suit.Hearts, CardData.Rank.Jack, 1),
            Card(CardData.Suit.Spades, CardData.Rank.Queen, 2),
            Card(CardData.Suit.Clubs, CardData.Rank.King, 3),
            Card(CardData.Suit.Diamonds, CardData.Rank.Jack, 4)
        };
        SetOwnedCards(faces);
        var collection = CardCollectionOf(_cards);
        collection.DrawToHand(4, 4);
        Assert.That(GameplayEffectResolver.CalculateHandCapacity(7, _cards), Is.EqualTo(9));
        Assert.That(_cards.hand.Count, Is.EqualTo(4), "Existing held cards are not discarded when computed capacity changes.");
    }

    [Test]
    public void AceEpic_GuaranteesAceFaceCritAndReturnsTheSamePhysicalAceWithinCapacity()
    {
        Gear("rel_115", GearSpecialRule.AceEpic);
        var ace = Card(CardData.Suit.Clubs, CardData.Rank.Ace, 11);
        var face = Card(CardData.Suit.Hearts, CardData.Rank.Jack, 12);
        Assert.That(GameplayEffectResolver.TryGetCriticalChanceOverride(new[] { ace, face }, _cards, out float chance), Is.True);
        Assert.That(chance, Is.EqualTo(100f));

        SetOwnedCards(new[] { ace, face });
        var collection = CardCollectionOf(_cards);
        collection.DrawToHand(2, 2);
        Assert.That(collection.TryDiscard(ace), Is.True);
        Assert.That(_cards.TryReturnDiscardCardToHand(ace), Is.True);
        Assert.That(_cards.hand.Count(card => ReferenceEquals(card, ace)), Is.EqualTo(1));
        Assert.That(collection.OwnedCards.Count, Is.EqualTo(2));

        var full = new CardCollection();
        var fullCards = Enumerable.Range(0, 8).Select(i => Card(CardData.Suit.Hearts, CardData.Rank.Two, 100 + i)).ToArray();
        full.Initialize(fullCards);
        full.DrawToHand(8, 8);
        Assert.That(full.TryDiscard(fullCards[0]), Is.True);
        Assert.That(full.TryReturnDiscardToHand(fullCards[0], 7), Is.False,
            "A physical Ace is not returned into an over-capacity hand.");
        Assert.That(full.DiscardPile.Count(card => ReferenceEquals(card, fullCards[0])), Is.EqualTo(1));
    }

    [Test]
    public void HandsEpic_AllowsFourMatchingCardsAndDoublesOnlyCardAuthoredOnPlayEffects()
    {
        var hands = Gear("rel_109", GearSpecialRule.HandsEpic);
        hands.ruleModifiers = new[] { new GameplayRuleModifierData
        {
            ruleId = "hands-four",
            kind = GameplayRuleModifierKind.SameRankMultiCard,
            sameRankMultiCard = new SameRankMultiCardRuleDefinition { requireMatchingRank = true, maximumCards = 4 }
        } };
        var group = Enumerable.Range(0, 4).Select(i => Card(CardData.Suit.Spades, CardData.Rank.Seven, 200 + i)).ToArray();
        Assert.That(GameplayEffectResolver.IsHandsMultiCardAction(group, _cards), Is.True);

        var enhancement = Enhancement(GameplayEffectKind.BonusCredits, GameplayEffectTrigger.CardCommitted, 1);
        Assert.That(group[0].TryApplyEnhancement(enhancement), Is.True);
        var enemy = Enemy("Hands test", 30);
        var action = new CombatActionContext(1, CombatActionOrigin.PlayerCard, card: group[0], targetEnemy: enemy);
        var queue = new CombatReactionQueue();
        GameplayEffectResolver.EnqueueCardCommitted(
            new CardCommittedEffectContext(action, group[0], enemy, _combat, _cards, 0, cardEffectActivations: 2), queue);
        Assert.That(queue.ProcessPhase(CombatReactionPhase.CardCommitted), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(2));
    }

    [Test]
    public void HeartEpic_ConvertsOverhealToVitalityButWitheringPreventsConversion()
    {
        var heart = Gear("rel_105", GearSpecialRule.HeartEpic);
        var card = Card(CardData.Suit.Hearts, CardData.Rank.Five, 300);
        for (int i = 0; i < 5; i++) EnqueueCardCommit(card, i + 1);
        var state = _cards.GetGearInstance(heart).State;
        Assert.That(state.GetCounter(-1050), Is.EqualTo(10));
        SetPrivate(_combat, "_openingPlayerAttackInProgress", true);
        Invoke(_combat, "PreparePhase7ActionBonuses", new object[] { new[] { card } });
        Assert.That(_combat.CalculateCardAttackDamage(card), Is.EqualTo(card.BaseAttackValue + 10));
        Invoke(_combat, "ConsumeAndAdvancePhase7ActionState", new object[] { new[] { card }, false });
        Assert.That(state.GetCounter(-1050), Is.Zero);

        var withering = Enemy(EnemyAbilityEffect.Withering, "Withering", 30);
        _combat.StartEnemy(withering);
        Assert.That(_combat.CanPlayerHeal(), Is.False);
        EnqueueCardCommit(card, 20);
        Assert.That(state.GetCounter(-1050), Is.Zero);
    }

    [Test]
    public void DwarfEpic_UsesPreparedSourceIdentityAndGrowsOnlyOnHigherRankKill()
    {
        Gear("rel_113", GearSpecialRule.DwarfEpic);
        var source = Card(CardData.Suit.Clubs, CardData.Rank.Two, 401);
        var higher = Card(CardData.Suit.Hearts, CardData.Rank.King, 402);
        SetOwnedCards(new[] { source, higher });
        Invoke(_combat, "ConsumeAndAdvancePhase7ActionState", new object[] { new[] { source }, false });
        Invoke(_combat, "PreparePhase7ActionBonuses", new object[] { new[] { higher } });
        Assert.That(GetPrivate<int>(_combat, "_activeDwarfBonus"), Is.EqualTo(1));
        Invoke(_combat, "ConsumeAndAdvancePhase7ActionState", new object[] { new[] { higher }, false });
        Invoke(_combat, "AwardDevouringKillBonus", new object[] { new[] { higher } });
        Assert.That(source.PermanentAttackBonus, Is.EqualTo(1));
        Assert.That(_cards.FindOwnedCard(source.Id), Is.SameAs(source));
    }

    [Test]
    public void Kingslayer_RequiresThreeDistinctManualRanksAndConsumesOneChargeOnAttack()
    {
        Gear("rel_138", GearSpecialRule.KingslayersMark);
        SetPrivate(_combat, "_isHeadlinerEncounter", true);
        var ace = Card(CardData.Suit.Clubs, CardData.Rank.Ace, 501);
        var king = Card(CardData.Suit.Hearts, CardData.Rank.King, 502);
        var queen = Card(CardData.Suit.Spades, CardData.Rank.Queen, 503);
        var state = _cards.GetGearInstance(_cards.ownedGear[0]).State;
        Invoke(_combat, "ConsumeAndAdvancePhase7ActionState", new object[] { new[] { ace }, false });
        Invoke(_combat, "ConsumeAndAdvancePhase7ActionState", new object[] { new[] { ace }, false });
        Assert.That(state.GetEncounterCounter(-1382), Is.Zero, "Repeating a rank does not qualify.");
        Invoke(_combat, "ConsumeAndAdvancePhase7ActionState", new object[] { new[] { king }, false });
        Invoke(_combat, "ConsumeAndAdvancePhase7ActionState", new object[] { new[] { queen }, false });
        Assert.That(state.GetEncounterCounter(-1382), Is.EqualTo(1));
        var attack = Card(CardData.Suit.Diamonds, CardData.Rank.Ten, 504);
        Invoke(_combat, "PreparePhase7ActionBonuses", new object[] { new[] { attack } });
        Assert.That(GetPrivate<int>(_combat, "_activeKingslayerBonus"), Is.EqualTo(1));
        Invoke(_combat, "ConsumeAndAdvancePhase7ActionState", new object[] { new[] { attack }, false });
        Assert.That(state.GetEncounterCounter(-1382), Is.Zero);
        _cards.ResetGearEncounterEffectState();
        Assert.That(state.GetEncounterCounter(-1381), Is.Zero);
    }

    [Test]
    public void Balancer_AlternationAddsOneAndRepeatingGroupResetsBeforeItCanBuff()
    {
        Gear("rel_118", GearSpecialRule.BalancersScale);
        var low = Card(CardData.Suit.Clubs, CardData.Rank.Four, 601);
        var face = Card(CardData.Suit.Hearts, CardData.Rank.Jack, 602);
        var state = _cards.GetGearInstance(_cards.ownedGear[0]).State;
        Invoke(_combat, "ConsumeAndAdvancePhase7ActionState", new object[] { new[] { low }, false });
        Invoke(_combat, "ConsumeAndAdvancePhase7ActionState", new object[] { new[] { face }, false });
        Assert.That(state.GetCounter(-1182), Is.EqualTo(1));
        Invoke(_combat, "PreparePhase7ActionBonuses", new object[] { new[] { face } });
        Assert.That(GetPrivate<int>(_combat, "_activeBalancerBonus"), Is.Zero);
        Assert.That(state.GetCounter(-1182), Is.Zero);
    }

    [Test]
    public void DiamondEpic_QueuesThirdManualPlayOnlyAndCardChoiceKeepsPhysicalIdentity()
    {
        Gear("rel_108", GearSpecialRule.DiamondEpic);
        var diamonds = Enumerable.Range(0, 3).Select(i => Card(CardData.Suit.Diamonds,
            (CardData.Rank)((int)CardData.Rank.Two + i), 701 + i)).ToArray();
        foreach (var diamond in diamonds)
            Invoke(_combat, "ConsumeAndAdvancePhase7ActionState", new object[] { new[] { diamond }, false });
        Assert.That(GetPrivate<int>(_combat, "_pendingDiamondDrawChoices"), Is.EqualTo(1));

        var chosen = Card(CardData.Suit.Hearts, CardData.Rank.King, 710);
        SetOwnedCards(diamonds.Concat(new[] { chosen }).ToArray());
        var collection = CardCollectionOf(_cards);
        collection.DrawToHand(4, 4);
        Assert.That(collection.TryDiscard(chosen), Is.True);
        Assert.That(_cards.TryReturnDiscardCardToHand(chosen), Is.True);
        Assert.That(_cards.hand.Count(card => ReferenceEquals(card, chosen)), Is.EqualTo(1));
    }

    [Test]
    public void SpadeEpic_FullBlockDrawsAndUnusedBlockBanksOneNextAttackBonus()
    {
        var spade = Gear("rel_107", GearSpecialRule.SpadeEpic);
        spade.effects = new[] { new GameplayEffectDefinition
        {
            kind = GameplayEffectKind.BlockMultiplier,
            trigger = GameplayEffectTrigger.DefenseCalculated,
            amount = 2,
            conditions = new[] { new GameplayEffectCondition { kind = GameplayConditionKind.CardSuit, suit = CardData.Suit.Spades } }
        } };
        var blocker = Card(CardData.Suit.Spades, CardData.Rank.Four, 801);
        var draw = Card(CardData.Suit.Hearts, CardData.Rank.King, 802);
        SetOwnedCards(new[] { draw, blocker });
        var collection = CardCollectionOf(_cards);
        collection.DrawToHand(1, 1);
        var enemy = Enemy("Spade test", 20);
        _combat.StartEnemy(enemy);
        var action = new CombatActionContext(71, CombatActionOrigin.PlayerDefense,
            sourcePlayer: _combat.player, card: blocker, targetEnemy: enemy);
        var context = new AttackBlockedEffectContext(action, blocker, enemy, 7, 0, _combat, _cards);
        var queue = (CombatReactionQueue)typeof(CombatManager)
            .GetField("_reactions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_combat);
        queue.Clear();
        SetPrivate(_combat, "_activeAction", action);
        SetPrivate(_combat, "_isResolvingAction", true);
        GameplayEffectResolver.EnqueueAttackBlocked(context, queue);
        Invoke(_combat, "ProcessReactions", new object[] { CombatReactionPhase.AttackBlocked });
        SetPrivate(_combat, "_activeAction", null);
        SetPrivate(_combat, "_isResolvingAction", false);
        queue.Clear();
        Assert.That(_cards.hand.Count, Is.EqualTo(2), "Rare draw behavior is retained on Epic.");
        Assert.That(_cards.GetGearInstance(spade).State.GetEncounterCounter(-1071), Is.EqualTo(1));
        var attack = Card(CardData.Suit.Clubs, CardData.Rank.Five, 803);
        Invoke(_combat, "PreparePhase7ActionBonuses", new object[] { new[] { attack } });
        Assert.That(GetPrivate<int>(_combat, "_activeSpadeBonus"), Is.EqualTo(1));
    }

    [Test]
    public void HiddenTrail_RerollsOneReachableHiddenNodeDeterministicallyWithoutChangingLinks()
    {
        var catalog = ScriptableObject.CreateInstance<RunContentCatalog>();
        _created.Add(catalog);
        var eventDef = Event("hidden-event", MapNodeType.Event);
        var riskDef = Event("hidden-risk", MapNodeType.Risk);
        catalog.events = new[] { eventDef };
        catalog.risks = new[] { riskDef };
        _run.contentCatalog = catalog;
        SetPrivate(_run, "_randomContext", new RunRandomContext("phase7-hidden-trail"));
        Gear("rel_135", GearSpecialRule.HiddenTrail);
        var root = new PathNode { id = 1, mapIndex = 0, kind = MapNodeType.Combat, accessible = true, next = new List<int> { 2 } };
        var hidden = new PathNode { id = 2, mapIndex = 0, kind = MapNodeType.Risk, contentId = "old-risk", hidden = true, revealed = false, next = new List<int>() };
        _run.currentPath.AddRange(new[] { root, hidden });
        var links = root.next.ToArray();
        Assert.That(GameplayEffectResolver.ApplyMapReady(new MapReadyEffectContext(_run, _cards, 0)), Is.True);
        Assert.That(hidden.kind, Is.EqualTo(MapNodeType.Event));
        Assert.That(hidden.contentId, Is.EqualTo("hidden-event"));
        Assert.That(hidden.hidden, Is.True);
        Assert.That(hidden.revealed, Is.False);
        CollectionAssert.AreEqual(links, root.next);
        Assert.That(GameplayEffectResolver.ApplyMapReady(new MapReadyEffectContext(_run, _cards, 0)), Is.False,
            "Map UI re-entry cannot reroll the same map again.");
    }

    [Test]
    public void WandererAndWarpath_TriggerOncePerMapAndGrantStartingBlockBeforeShield()
    {
        var boots = Gear("rel_133", GearSpecialRule.WanderersBoots);
        var banner = Gear("rel_134", GearSpecialRule.WarpathBanner);
        _combat.player.TakeDamage(5);
        ApplyNode(MapNodeType.Combat, 0);
        ApplyNode(MapNodeType.Shop, 0);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(25));
        ApplyNode(MapNodeType.Event, 0);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(28));
        Assert.That(_cards.credits, Is.EqualTo(3));
        ApplyNode(MapNodeType.Risk, 0);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(28));
        Assert.That(_cards.credits, Is.EqualTo(3));

        ApplyNode(MapNodeType.Combat, 1);
        ApplyNode(MapNodeType.Elite, 1);
        Assert.That(_cards.GetGearInstance(banner).State.GetCounter(-1340), Is.EqualTo(2));
        _combat.Reset();
        _combat.StartEncounter(new[] { Enemy("Warpath test", 10) }, MapNodeType.Combat);
        Assert.That(_combat.player.EncounterBlock, Is.EqualTo(4));
        _combat.player.GainShield(1);
        var request = new DamageRequest(new CombatActionContext(1, CombatActionOrigin.EnemyRetaliation),
            0, CombatDamageOrigin.EnemyAggregate, null, _combat.player, 5);
        Assert.That(_combat.player.ModifyIncomingCombatDamage(request, _combat, 5), Is.EqualTo(1));
        Assert.That(_combat.player.EncounterBlock, Is.Zero);
        Assert.That(_combat.player.ShieldCharges, Is.EqualTo(1));
    }

    void EnqueueCardCommit(CardInstance card, long actionId)
    {
        var action = new CombatActionContext(actionId, CombatActionOrigin.PlayerCard,
            sourcePlayer: _combat.player, card: card);
        var queue = new CombatReactionQueue();
        GameplayEffectResolver.EnqueueCardCommitted(
            new CardCommittedEffectContext(action, card, null, _combat, _cards, 0), queue);
        Assert.That(queue.ProcessPhase(CombatReactionPhase.CardCommitted), Is.True);
    }

    void ApplyNode(MapNodeType kind, int mapIndex) => typeof(RunManager)
        .GetMethod("ApplyPhase7MapCompletionEffects", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(_run, new object[] { new PathNode { kind = kind, mapIndex = mapIndex } });

    RunEventDefinition Event(string id, MapNodeType kind)
    {
        var value = ScriptableObject.CreateInstance<RunEventDefinition>();
        value.id = id;
        value.category = kind;
        _created.Add(value);
        return value;
    }

    GearData Asset(string file) => AssetDatabase.LoadAssetAtPath<GearData>($"Assets/Data/Relics/{file}.asset");

    GearData Gear(string id, GearSpecialRule rule)
    {
        var value = ScriptableObject.CreateInstance<GearData>();
        value.id = id;
        value.canonicalId = id;
        value.displayName = id;
        value.specialRule = rule;
        value.price = 0;
        _created.Add(value);
        Assert.That(_cards.BuyGear(value, 0), Is.True);
        return value;
    }

    CardEnhancementData Enhancement(GameplayEffectKind kind, GameplayEffectTrigger trigger, int amount)
    {
        var value = ScriptableObject.CreateInstance<CardEnhancementData>();
        value.id = $"enh-test-{_created.Count}";
        value.displayName = value.id;
        value.effects = new[] { new GameplayEffectDefinition { kind = kind, trigger = trigger, amount = amount } };
        _created.Add(value);
        return value;
    }

    CardInstance Card(CardData.Suit suit, CardData.Rank rank, int id)
    {
        var definition = CardData.Create(suit, rank);
        _created.Add(definition);
        return new CardInstance(definition, id);
    }

    EnemyRuntime Enemy(string name, int hp) => Enemy(Array.Empty<EnemyAbilityEffect>(), name, hp);

    EnemyRuntime Enemy(EnemyAbilityEffect abilityEffect, string name, int hp) => Enemy(new[] { abilityEffect }, name, hp);

    EnemyRuntime Enemy(EnemyAbilityEffect[] abilityEffects, string name, int hp)
    {
        var type = EnemyTypeData.Create(name, hp, 2, 0);
        _created.Add(type);
        type.abilities = abilityEffects.Select(effect =>
        {
            var ability = ScriptableObject.CreateInstance<EnemyAbility>();
            ability.effect = effect;
            _created.Add(ability);
            return ability;
        }).ToArray();
        return new EnemyRuntime(type);
    }

    void SetOwnedCards(IEnumerable<CardInstance> cards) => CardCollectionOf(_cards).Initialize(cards);

    static CardCollection CardCollectionOf(CardManager manager) => (CardCollection)typeof(CardManager)
        .GetField("_cards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

    static T GetPrivate<T>(object instance, string name) => (T)instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);

    static void SetPrivate(object instance, string name, object value) => instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);

    static object Invoke(object instance, string method, object[] args) => instance.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
}
