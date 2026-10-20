using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class PerAttackerDefenseTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        var canvas = CreateComponent<Canvas>("Defense Hand Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Hand Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var dragCanvas = CreateComponent<Canvas>("Defense Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = CreateGameObject("Defense Card Prefab", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = CreateComponent<CardManager>("Defense CardManager");
        _cards.Configure(_field, dragCanvas, prefab, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>());
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = CreateComponent<CombatManager>("Defense CombatManager");
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
    public void PendingIncomingDamage_IsAttributedToEachAttackerAndUpdatesAfterBlock()
    {
        var enemies = StartDefenseWindow(3, 5);
        AddDefenseBonus(5);

        Assert.That(_combat.GetPendingAttackDamage(enemies[0]), Is.EqualTo(3));
        Assert.That(_combat.GetPendingAttackDamage(enemies[1]), Is.EqualTo(5));

        var blockFive = HandViews().First(view => _combat.CalculateCardDefense(view.data) >= 5);
        Assert.That(_combat.TryDefendWithCards(new[] { blockFive }), Is.True);
        Assert.That(_combat.GetPendingAttackDamage(enemies[0]), Is.EqualTo(3));
        Assert.That(_combat.GetPendingAttackDamage(enemies[1]), Is.Zero);
        Assert.That(_combat.pendingDamage, Is.EqualTo(3));

        _combat.TakeRemainingDamage();
        Assert.That(_combat.GetPendingAttackDamage(enemies[0]), Is.Zero);
        Assert.That(_combat.pendingDamage, Is.Zero);
    }

    [Test]
    public void HeadlinerEncounter_AceCanBlockOnePointAttackWithoutChangingAggregateRetaliation()
    {
        var bossType = EnemyTypeData.Create("Test Boss", 100, 1, 0);
        _created.Add(bossType);
        var ace = FindViewInHand(card => card.Rank == CardData.Rank.Ace);
        var headliner = new EnemyRuntime(bossType);
        _combat.StartEncounter(new[] { headliner }, MapNodeType.Headliner);
        Assert.That(_combat.TryPlayCards(new[] { FindViewInHand(card => card.Rank != CardData.Rank.Ace) }, headliner), Is.True);
        Assert.That(_combat.CalculateCardDefense(ace.data), Is.EqualTo(1));
        Assert.That(_combat.CanDefendWithCards(new[] { ace }, headliner), Is.True);
        Assert.That(_combat.TryDefendWithCards(new[] { ace }, headliner), Is.True);
        Assert.That(_combat.pendingDamage, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void HeadlinerWithoutAbility_AcePartiallyBlocksAndRemainsInDefense()
    {
        var bossType = EnemyTypeData.Create("Test Boss", 100, 8, 0);
        _created.Add(bossType);
        var ace = FindViewInHand(card => card.Rank == CardData.Rank.Ace);
        var headliner = new EnemyRuntime(bossType);
        _combat.StartEncounter(new[] { headliner }, MapNodeType.Headliner);
        Assert.That(_combat.TryPlayCards(new[] { FindViewInHand(card => card.Rank != CardData.Rank.Ace) }, headliner), Is.True);

        Assert.That(_combat.CanDefendWithCards(new[] { ace }, headliner), Is.True);
        Assert.That(_combat.TryDefendWithCards(new[] { ace }, headliner), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(7));
        Assert.That(_combat.GetPendingAttackDamage(headliner), Is.EqualTo(7));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
    }

    [Test]
    public void UnyieldingDefense_RejectsPartialCardButAcceptsOneSufficientCard()
    {
        var bossType = EnemyTypeData.Create("Unyielding Boss", 100, 8, 0);
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        ability.effect = EnemyAbilityEffect.FullCoverageBlock;
        bossType.abilities = new[] { ability };
        _created.Add(bossType);
        _created.Add(ability);
        var ace = FindViewInHand(card => card.Rank == CardData.Rank.Ace);
        var headliner = new EnemyRuntime(bossType);
        _combat.StartEncounter(new[] { headliner }, MapNodeType.Headliner);
        Assert.That(_combat.TryPlayCards(new[] { FindViewInHand(card => card.Rank != CardData.Rank.Ace) }, headliner), Is.True);

        int handCount = _cards.HandCount;
        Assert.That(_combat.CanDefendWithCards(new[] { ace }, headliner), Is.False);
        Assert.That(_combat.TryDefendWithCards(new[] { ace }, headliner), Is.False);
        Assert.That(_cards.HandCount, Is.EqualTo(handCount));
        Assert.That(_combat.pendingDamage, Is.EqualTo(8));

        var sufficient = FindViewInHand(card => _combat.CalculateCardDefense(card) >= 8);
        Assert.That(_combat.TryDefendWithCards(new[] { sufficient }, headliner), Is.True);
        Assert.That(_combat.pendingDamage, Is.Zero);
    }

    [Test]
    public void OneDefenseSix_BlocksExactlyOneOfTwoThreeAttacks_AndLosesExcess()
    {
        var enemies = StartDefenseWindow(3, 3);
        AddDefenseBonus(6);
        var card = HandViews()[0];
        Assert.That(_combat.CalculateCardDefense(card.data), Is.GreaterThanOrEqualTo(6));
        int handBefore = _cards.HandCount;

        Assert.That(_combat.TryDefendWithCards(new[] { card }, enemies[1]), Is.True);
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(1));
        Assert.That(_combat.BlockedAttackCount, Is.EqualTo(1));
        Assert.That(_combat.pendingDamage, Is.EqualTo(3));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore - 1));
    }

    [Test]
    public void TwoAdequateCards_BlockTwoDistinctAttacks_EvenWhenCommittedTogether()
    {
        StartDefenseWindow(3, 3);
        AddDefenseBonus(3);
        var pair = HandViews().Take(2).ToArray();
        _cards.ToggleCardSelection(pair[0]);
        _cards.ToggleCardSelection(pair[1]);
        Assert.That(_cards.SelectedCards, Is.EqualTo(pair));
        Assert.That(_combat.TryPreviewDefense(pair, out int count, out int remaining), Is.True);
        Assert.That((count, remaining), Is.EqualTo((2, 0)));
        int discarded = _cards.discardPile.Count;
        int deckBefore = _cards.deck.Count;
        int handBefore = _cards.HandCount;
        var instances = pair.Select(view => view.data).ToArray();

        // Exercise the live selected-list input, which commit cleanup mutates.
        Assert.That(_combat.TryDefendWithCards(_cards.SelectedCards), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore - 2));
        Assert.That(_cards.discardPile.Count, Is.EqualTo(discarded));
        Assert.That(_cards.deck.Count, Is.EqualTo(deckBefore + 2));
        Assert.That(instances.All(card => _cards.deck.Contains(card)), Is.True);
        Assert.That(_cards.SelectedCards, Is.Empty);
        Assert.That(_combat.pendingDamage, Is.Zero);
        Assert.That(_combat.PendingAttackCount, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void OrdinaryAttack_AllowsMultipleCardsToCombinePartialBlock()
    {
        StartDefenseWindow(20);
        var cards = HandViews().OrderBy(card => _combat.CalculateCardDefense(card.data)).Take(2).ToArray();
        int blocked = cards.Sum(card => _combat.CalculateCardDefense(card.data));
        Assert.That(blocked, Is.LessThan(20));
        int deckBefore = _cards.deck.Count;

        Assert.That(_combat.TryDefendWithCards(cards), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(20 - blocked));
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(1));
        Assert.That(_cards.deck.Count, Is.EqualTo(deckBefore + 2));
    }

    [Test]
    public void DifferentAttacks_StrongestCardsMatchStrongestBlockableAttacks_RegardlessOfSubmissionOrder()
    {
        StartDefenseWindow(8, 5, 3);
        AddDefenseBonus(8);
        var pair = HandViews().Take(2).Reverse().ToArray();
        Assert.That(_combat.TryDefendWithCards(pair), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(3), "Highest two attacks are matched, not arbitrary attacks.");
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(1));
    }

    [Test]
    public void CardThatCannotBlockHighestAttack_CanMatchLowerAttack()
    {
        StartDefenseWindow(20, 3);
        AddDefenseBonus(3);
        var card = HandViews()[0];
        Assert.That(_combat.TryDefendWithCards(new[] { card }), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(20));
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(1));
    }

    [Test]
    public void DefenseWindow_SnapshotsOnlyPositiveLivingAttacks_AndIgnoresLaterAttackChanges()
    {
        var enemies = StartDefenseWindow(3, 0);
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(1));
        enemies[0].ReduceAttack(3);
        Assert.That(_combat.TotalEnemyAttack, Is.Zero);
        Assert.That(_combat.pendingDamage, Is.EqualTo(3));
        AddDefenseBonus(3);
        Assert.That(_combat.TryDefendWithCards(new[] { HandViews()[0] }), Is.True);
        Assert.That(_combat.pendingDamage, Is.Zero);
    }

    [Test]
    public void RemainingDamage_ResolvesAsOneEnemyAggregateHit_AndConsumesOnlyOneShield()
    {
        StartDefenseWindow(3, 3);
        AddDefenseBonus(3);
        Assert.That(_combat.TryDefendWithCards(new[] { HandViews()[0] }), Is.True);
        _combat.GrantPlayerShield(1);
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;
        int health = _combat.player.currentHealth;

        _combat.TakeRemainingDamage();

        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].Request.Origin, Is.EqualTo(CombatDamageOrigin.EnemyAggregate));
        Assert.That(results[0].Request.RequestedDamage, Is.EqualTo(3));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(health));
        Assert.That(_combat.player.ShieldCharges, Is.Zero);
        Assert.That(_combat.pendingDamage, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(-1, false)]
    public void DefenseAttackResult_OnlyExactPositiveCardBlockIsPerfect(int excessDamage, bool expectedPerfect)
    {
        var defense = HandViews().Skip(1).First(view => _combat.CalculateCardDefense(view.data) > 1);
        int effectiveBlock = _combat.CalculateCardDefense(defense.data);
        int incoming = effectiveBlock - excessDamage;
        StartDefenseWindow(incoming);
        var results = new List<DefenseAttackResult>();
        _combat.OnDefenseAttackResolved += results.Add;

        Assert.That(_combat.TryDefendWithCards(new[] { defense }), Is.True);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].IncomingDamage, Is.EqualTo(incoming));
        Assert.That(results[0].EffectiveBlock, Is.EqualTo(effectiveBlock));
        Assert.That(results[0].RemainingDamage, Is.EqualTo(Mathf.Max(0, incoming - effectiveBlock)));
        Assert.That(results[0].IsPerfectBlock, Is.EqualTo(expectedPerfect));
        if (expectedPerfect)
        {
            Assert.That(results[0].Attacker, Is.SameAs(_combat.Enemies[0]));
            Assert.That(results[0].BlockedDamage, Is.EqualTo(incoming));
        }
    }

    [Test]
    public void DefenseAttackResult_MultiCardMultiHitReportsOneResultPerFullyCoveredHit()
    {
        var pair = HandViews().Skip(1).Take(2).ToArray();
        int[] values = pair.Select(view => _combat.CalculateCardDefense(view.data)).ToArray();
        StartDefenseWindow(values);
        var results = new List<DefenseAttackResult>();
        _combat.OnDefenseAttackResolved += results.Add;

        Assert.That(_combat.TryDefendWithCards(pair), Is.True);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(result => result.IsPerfectBlock), Is.True);
        Assert.That(results.Select(result => result.AttackIndex).Distinct().Count(), Is.EqualTo(2));
        Assert.That(results.Select(result => result.Attacker).Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public void PreviewBlockedAttackers_NamesEachCardsAttackerWithoutChangingState()
    {
        var views = HandViews().Skip(1).ToArray();
        var first = views[0];
        var second = views.First(view => _combat.CalculateCardDefense(view.data) != _combat.CalculateCardDefense(first.data));
        var pair = new[] { first, second };
        int[] values = pair.Select(view => _combat.CalculateCardDefense(view.data)).ToArray();
        StartDefenseWindow(values);
        int pending = _combat.pendingDamage;
        int handCount = _cards.HandCount;

        var preview = _combat.PreviewBlockedAttackers(pair.Select(view => view.data).ToArray());

        Assert.That(preview, Has.Length.EqualTo(2));
        Assert.That(_combat.pendingDamage, Is.EqualTo(pending));
        Assert.That(_cards.HandCount, Is.EqualTo(handCount));
        var results = new List<DefenseAttackResult>();
        _combat.OnDefenseAttackResolved += results.Add;
        Assert.That(_combat.TryDefendWithCards(pair), Is.True);
        for (int i = 0; i < pair.Length; i++)
            Assert.That(preview[i], Is.SameAs(results.Single(result => result.IncomingDamage == values[i]).Attacker),
                $"Card {i} blocks the attack it fully covers.");
    }

    [Test]
    public void DefenseAttackResult_ZeroDamageDoesNotReportCardBlock()
    {
        var defense = HandViews()[0];
        StartDefenseWindow(0);
        var results = new List<DefenseAttackResult>();
        _combat.OnDefenseAttackResolved += results.Add;
        Assert.That(_combat.TryDefendWithCards(new[] { defense }), Is.False);
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void DefenseAttackResult_RejectedDefenseDoesNotReportCardBlock()
    {
        var defense = HandViews()[0];
        StartDefenseWindow(3);
        var results = new List<DefenseAttackResult>();
        _combat.OnDefenseAttackResolved += results.Add;
        GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, true);
        try { Assert.That(_combat.TryDefendWithCards(new[] { defense }), Is.False); }
        finally { GameplayInputGate.Clear(); }
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void DefenseAttackResult_ShieldOnlyPreventionDoesNotReportCardBlock()
    {
        StartDefenseWindow(3);
        var results = new List<DefenseAttackResult>();
        _combat.OnDefenseAttackResolved += results.Add;
        _combat.GrantPlayerShield(1);
        _combat.TakeRemainingDamage();
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void EnemyDisplay_PerfectBlockCueUsesResultAndDoesNotBlockRaycasts()
    {
        var defense = HandViews().Skip(1).First(view => _combat.CalculateCardDefense(view.data) > 1);
        int value = _combat.CalculateCardDefense(defense.data);
        var enemies = StartDefenseWindow(value);
        var displayObject = CreateGameObject("Perfect Block Enemy Display", typeof(RectTransform), typeof(Image), typeof(EnemyDisplayUI));
        var display = displayObject.GetComponent<EnemyDisplayUI>();
        display.portrait = displayObject.GetComponent<Image>();
        display.Bind(enemies[0]);
        typeof(EnemyDisplayUI).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(display, null);
        var results = new List<DefenseAttackResult>();
        _combat.OnDefenseAttackResolved += results.Add;

        Assert.That(display.isActiveAndEnabled, Is.True);
        Assert.That(CombatManager.Instance, Is.SameAs(_combat));
        Assert.That(_combat.TryDefendWithCards(new[] { defense }), Is.True);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(display.DisplayedEnemy, Is.SameAs(results[0].Attacker));
        Assert.That(display.IsPerfectBlockCueVisible, Is.True);
        var cue = (TMPro.TMP_Text)typeof(EnemyDisplayUI).GetField("_perfectBlockCueText",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(display);
        Assert.That(cue.text, Is.EqualTo("PERFECT BLOCK"));
        Assert.That(cue.raycastTarget, Is.False);
    }

    [TestCase(0, 1)]
    [TestCase(1, 0)]
    public void PerfectParry_ExactWindowDrawsOnce_OverblockDoesNot(int excess, int expectedDraw)
    {
        var defense = HandViews().Skip(1).First(view => _combat.CalculateCardDefense(view.data) > 1);
        int value = _combat.CalculateCardDefense(defense.data);
        StartDefenseWindow(value - excess);
        var eligible = _cards.deck.Select(card => card.Id).ToHashSet();
        int before = _cards.HandCount;
        Assert.That(_combat.TryDefendWithCards(new[] { defense }), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(before - 1 + expectedDraw));
        if (expectedDraw > 0) Assert.That(eligible.Contains(_cards.hand.Last().Id), Is.True);
        _combat.TakeRemainingDamage();
        Assert.That(_cards.HandCount, Is.EqualTo(before - 1 + expectedDraw));
    }

    [Test]
    public void PerfectParry_SeparatePartialCommitsAwardOnlyAtWindowCompletion()
    {
        var pair = HandViews().Skip(1).Take(2).ToArray();
        int total = pair.Sum(view => _combat.CalculateCardDefense(view.data));
        StartDefenseWindow(total);
        int before = _cards.HandCount;
        Assert.That(_combat.TryDefendWithCards(new[] { pair[0] }), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(before - 1));
        Assert.That(_combat.TryDefendWithCards(new[] { pair[1] }), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(before - 1));
    }

    [Test]
    public void PerfectParry_UsesPostModifierDamage()
    {
        var defense = HandViews()[1];
        var armor = ScriptableObject.CreateInstance<GearData>();
        _created.Add(armor);
        armor.effects = new[] { new GameplayEffectDefinition {
            kind = GameplayEffectKind.IncomingCombatDamageReduction,
            trigger = GameplayEffectTrigger.IncomingDamageCalculated, amount = 3 } };
        _cards.ownedGear.Add(armor);
        StartDefenseWindow(_combat.CalculateCardDefense(defense.data) + 3);
        int before = _cards.HandCount;
        Assert.That(_combat.TryDefendWithCards(new[] { defense }), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(before - 1));
        _combat.TakeRemainingDamage();
        Assert.That(_cards.HandCount, Is.EqualTo(before));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
    }

    [Test]
    public void PerfectParry_EmptyInitialDeckCannotDrawReturnedDefenseCard()
    {
        var collection = (CardCollection)typeof(CardManager).GetField("_cards",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(_cards);
        foreach (var card in collection.Deck.ToArray()) collection.RemoveOwnedCard(card);
        var defense = HandViews()[1];
        StartDefenseWindow(_combat.CalculateCardDefense(defense.data));
        // The attack that opened the window went back to the draw pile; empty it again.
        foreach (var card in collection.Deck.ToArray()) collection.RemoveOwnedCard(card);
        int before = _cards.HandCount;
        Assert.That(_cards.deck, Is.Empty);
        Assert.That(_combat.TryDefendWithCards(new[] { defense }), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(before - 1));
        Assert.That(_cards.deck.Count, Is.EqualTo(1));
    }

    [Test]
    public void PerfectParry_ModifiersReducingIncomingToZeroNeverAward()
    {
        var armor = ScriptableObject.CreateInstance<GearData>();
        _created.Add(armor);
        armor.effects = new[] { new GameplayEffectDefinition {
            kind = GameplayEffectKind.IncomingCombatDamageReduction,
            trigger = GameplayEffectTrigger.IncomingDamageCalculated, amount = 100 } };
        _cards.ownedGear.Add(armor);
        var defense = HandViews()[1];
        StartDefenseWindow(_combat.CalculateCardDefense(defense.data));
        int before = _cards.HandCount;
        Assert.That(_combat.TryDefendWithCards(new[] { defense }), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(before - 1));
    }

    [Test]
    public void PerfectParry_MultipleAttacksAndCardsAwardOneDrawTotal()
    {
        var pair = HandViews().Skip(1).Take(2).ToArray();
        StartDefenseWindow(pair.Select(view => _combat.CalculateCardDefense(view.data)).ToArray());
        int before = _cards.HandCount;
        Assert.That(_combat.TryDefendWithCards(pair), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(before - 1));
        Assert.That(_combat.PendingAttackCount, Is.Zero);
    }

    [Test]
    public void PerfectParry_DoubleStrikeIncludesGearAndEnhancementBlock_AndDrawsOnlyOnce()
    {
        var pair = HandViews().Skip(1).Take(2).ToArray();
        foreach (var view in pair)
        {
            var enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
            _created.Add(enhancement);
            enhancement.defenseBonus = 10 - _combat.CalculateCardDefense(view.data);
            Assert.That(_cards.ApplyEnhancement(view.data.Id, enhancement), Is.True);
        }
        AddDefenseBonus(2);
        var results = new List<DefenseAttackResult>();
        _combat.OnDefenseAttackResolved += results.Add;
        Assert.That(pair.All(view => _combat.CalculateCardDefense(view.data) == 12), Is.True);
        var type = EnemyTypeData.Create("Double Strike Parry", 1000, 20, 0);
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        ability.effect = EnemyAbilityEffect.DoubleStrike;
        type.abilities = new[] { ability };
        _created.Add(type); _created.Add(ability);
        _combat.StartEnemy(new EnemyRuntime(type));
        Assert.That(_combat.TryPlayCards(new[] { HandViews().First(view => !pair.Contains(view)) }, _combat.currentEnemy), Is.True);
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(2));
        Assert.That(_combat.pendingDamage, Is.EqualTo(24));
        var displayObject = CreateGameObject("Double Strike Perfect Block Display", typeof(RectTransform), typeof(Image), typeof(EnemyDisplayUI));
        var display = displayObject.GetComponent<EnemyDisplayUI>();
        display.portrait = displayObject.GetComponent<Image>();
        display.Bind(_combat.currentEnemy);
        typeof(EnemyDisplayUI).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(display, null);
        int before = _cards.HandCount;
        Assert.That(_combat.TryDefendWithCards(pair), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(before - 1));
        Assert.That(_combat.PendingAttackCount, Is.Zero);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(result => result.IsPerfectBlock), Is.True);
        var cue = (TMPro.TMP_Text)typeof(EnemyDisplayUI).GetField("_perfectBlockCueText",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(display);
        Assert.That(cue.text, Is.EqualTo("PERFECT BLOCK ×2"), "Two covered hits from one enemy coalesce without duplicating the presentation cue.");
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
    }

    [Test]
    public void DoubleStrike_HitsShareOneBlockPool_SoAnExactTotalIsAPerfectBlock()
    {
        var pair = DefensePair(12, 8);
        var striker = StartDoubleStrikeWindow(17, pair);
        Assert.That(_combat.pendingDamage, Is.EqualTo(20), "17 × 60% rounds to 10 per hit.");
        var results = new List<DefenseAttackResult>();
        _combat.OnDefenseAttackResolved += results.Add;
        Assert.That(_combat.TryPreviewDefense(pair, out int blocked, out int remaining), Is.True);
        Assert.That(blocked, Is.EqualTo(2));
        Assert.That(remaining, Is.Zero, "The 12's spare 2 carries onto the second hit.");

        int before = _cards.HandCount;
        Assert.That(_combat.TryDefendWithCards(pair, striker), Is.True);
        Assert.That(_combat.PendingAttackCount, Is.Zero);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
        Assert.That(_cards.HandCount, Is.EqualTo(before - 1), "Perfect Block draws one card.");
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(result => result.IsPerfectBlock), Is.True);
    }

    [Test]
    public void DoubleStrike_OverblockAcrossThePoolIsNotAPerfectBlock()
    {
        var pair = DefensePair(12, 9);
        var striker = StartDoubleStrikeWindow(17, pair);
        int before = _cards.HandCount;
        Assert.That(_combat.TryDefendWithCards(pair, striker), Is.True);
        Assert.That(_combat.PendingAttackCount, Is.Zero);
        Assert.That(_cards.HandCount, Is.EqualTo(before - 2));
    }

    [Test]
    public void DoubleStrike_WithFullCoverageBlock_DoesNotCarryBlockBetweenHits()
    {
        var pair = DefensePair(12, 8);
        var striker = StartDoubleStrikeWindow(17, pair, EnemyAbilityEffect.FullCoverageBlock);
        Assert.That(_combat.CanDefendWithCards(pair, striker), Is.False,
            "Each hit still needs one card that covers it alone.");
    }

    CardView[] DefensePair(int first, int second)
    {
        var pair = HandViews().Skip(1).Take(2).ToArray();
        int[] targets = { first, second };
        for (int i = 0; i < pair.Length; i++)
        {
            var enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
            _created.Add(enhancement);
            enhancement.defenseBonus = targets[i] - _combat.CalculateCardDefense(pair[i].data);
            Assert.That(_cards.ApplyEnhancement(pair[i].data.Id, enhancement), Is.True);
            Assert.That(_combat.CalculateCardDefense(pair[i].data), Is.EqualTo(targets[i]));
        }
        return pair;
    }

    EnemyRuntime StartDoubleStrikeWindow(int attack, CardView[] reserved, params EnemyAbilityEffect[] extraAbilities)
    {
        var type = EnemyTypeData.Create("Double Strike Pool", 1000, attack, 0);
        type.abilities = extraAbilities.Prepend(EnemyAbilityEffect.DoubleStrike).Select(effect =>
        {
            var ability = ScriptableObject.CreateInstance<EnemyAbility>();
            ability.effect = effect;
            _created.Add(ability);
            return ability;
        }).ToArray();
        _created.Add(type);
        var striker = new EnemyRuntime(type);
        _combat.StartEnemy(striker);
        Assert.That(_combat.TryPlayCards(new[] { HandViews().First(view => !reserved.Contains(view)) }, striker), Is.True);
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(2));
        return striker;
    }

    [Test]
    public void StrictDraw_EmptyDeckNeverRecyclesDiscard_AndHonorsCapacity()
    {
        var collection = new CardCollection();
        collection.Initialize(_cards.ownedCards.Take(2));
        Assert.That(collection.DrawExistingToHand(5, 1), Is.EqualTo(1));
        Assert.That(collection.DrawExistingToHand(5, 1), Is.Zero);
        Assert.That(collection.TryDiscard(collection.Hand.ToArray()), Is.True);
        Assert.That(collection.DrawExistingToHand(5, 8), Is.EqualTo(1));
        Assert.That(collection.DrawExistingToHand(5, 8), Is.Zero);
        Assert.That(collection.DiscardPile.Count, Is.EqualTo(1));
    }

    EnemyRuntime[] StartDefenseWindow(params int[] attacks)
    {
        var enemies = new EnemyRuntime[attacks.Length];
        for (int i = 0; i < attacks.Length; i++)
        {
            var type = EnemyTypeData.Create($"Attacker {i}", 100, attacks[i], 0);
            _created.Add(type);
            enemies[i] = new EnemyRuntime(type);
        }
        _combat.StartEncounter(enemies);
        Assert.That(_combat.TryPlayCards(new[] { HandViews()[0] }, enemies[0]), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(attacks.Sum()));
        return enemies;
    }

    CardView FindViewInHand(System.Func<CardInstance, bool> predicate)
    {
        for (int pass = 0; pass < 120; pass++)
        {
            var found = HandViews().FirstOrDefault(view => predicate(view.data));
            if (found != null) return found;
            var discard = HandViews().FirstOrDefault();
            if (discard == null) break;
            Assert.That(_cards.TryDiscard(discard), Is.True);
            _cards.DrawToHand(1);
        }
        Assert.Fail("Required card could not be found in the collection.");
        return null;
    }

    void AddDefenseBonus(int bonus)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.defenseBonus = bonus;
        _created.Add(gear);
        _cards.ownedGear.Add(gear);
    }

    List<CardView> HandViews()
    {
        var views = new List<CardView>();
        for (int i = 0; i < _field.cardsHolder.childCount; i++)
            if (_field.cardsHolder.GetChild(i).TryGetComponent<CardView>(out var view)) views.Add(view);
        return views;
    }

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var gameObject = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }
}
