using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public sealed class TutorialTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    TutorialRunDefinition _definition;
    TutorialDirector _director;
    bool _wasCompleted;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        TutorialGate.Clear();
        _wasCompleted = TutorialProgress.Completed;
        TutorialProgress.Reset();
        _definition = TutorialRunDefinition.Load();
        Assert.That(_definition, Is.Not.Null, "Resources/TutorialRun is missing.");
        var registry = SaveContentCatalog.Load();
        _cards = Make<CardManager>("Tutorial Test Cards");
        _cards.Configure(null, null, null, registry.gearItems, registry.enhancements, registry.consumables);
        _combat = Make<CombatManager>("Tutorial Test Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Tutorial Test Run");
        _run.contentCatalog = registry.runContent;
    }

    [TearDown]
    public void TearDown()
    {
        if (_director) Object.DestroyImmediate(_director.gameObject);
        TutorialGate.Clear();
        GameplayInputGate.Clear();
        FrontendLaunchContext.Clear();
        if (_wasCompleted) TutorialProgress.MarkCompleted();
        else TutorialProgress.Reset();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void TutorialRun_IsOneScriptedDistrictEndingInAJackOfTheDefinedSuit()
    {
        Assert.That(_run.StartTutorialRun(_definition), Is.True);

        Assert.That(_run.IsTutorialRun, Is.True);
        Assert.That(_run.currentPath.Select(node => node.kind), Is.EqualTo(new[]
            { MapNodeType.Combat, MapNodeType.Combat, MapNodeType.Shop, MapNodeType.Headliner }));
        for (int i = 0; i < _run.currentPath.Count - 1; i++)
            Assert.That(_run.currentPath[i].next, Is.EqualTo(new[] { _run.currentPath[i + 1].id }));
        Assert.That(_run.currentPath.Count(node => node.accessible), Is.EqualTo(1));
        Assert.That(_run.currentPath[0].accessible, Is.True);
        Assert.That(Hand(), Is.EqualTo(Specs(_definition.fights[0].hand)));
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(40));
        Assert.That(_run.HeadlinerTotal, Is.EqualTo(1));
        Assert.That(_run.CurrentHeadliner.sourceCard.rank, Is.EqualTo(CardData.Rank.Jack));
        Assert.That(_run.CurrentHeadliner.sourceCard.suit, Is.EqualTo(_definition.headlinerSuit));
        Assert.That(_run.CurrentHeadliner.abilities, Is.Empty, "The tutorial Headliner has no extra trait to explain.");
    }

    [Test]
    public void TutorialFights_StageTheirHandsRostersGrantsAndDrops()
    {
        _run.StartTutorialRun(_definition);
        _run.OnPathChosen(0);
        var first = _definition.fights[0];
        Assert.That(_combat.Enemies.Select(enemy => (enemy.type.enemyName, enemy.currentHp, enemy.currentAttack)),
            Is.EqualTo(first.enemies.Select(spec => (spec.enemyName, spec.hp, spec.attack))));
        PlayToVictory();
        Assert.That(_cards.Backpack, Does.Contain(first.drop), "The first fight drops its scripted Trick.");
        Assert.That(_cards.credits, Is.EqualTo(first.enemies.Sum(spec => spec.credits)));

        _run.OnPathChosen(1);
        var second = _definition.fights[1];
        Assert.That(Hand(), Is.EquivalentTo(Specs(second.hand)));
        Assert.That(_combat.Enemies.Select(enemy => enemy.type.enemyName), Is.EqualTo(second.enemies.Select(spec => spec.enemyName)));
        foreach (var trick in second.grants) Assert.That(_cards.Backpack, Does.Contain(trick));
    }

    [Test]
    public void TutorialBlackMarket_SellsTheScriptedGearAndTrickWithStakeAndSupply()
    {
        _run.StartTutorialRun(_definition);
        var market = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        market.accessible = true;
        _run.OnPathChosen(market.id);

        var offers = _run.GetCurrentShopOffers();
        Assert.That(offers.Select(offer => offer.kind), Is.EqualTo(new[]
            { ShopOfferKind.Gear, ShopOfferKind.Consumable, ShopOfferKind.Investment, ShopOfferKind.Supply }));
        Assert.That(offers[0].gear, Is.SameAs(_definition.marketGear));
        Assert.That(offers[1].consumable, Is.SameAs(_definition.marketTrick));
    }

    [Test]
    public void NormalRunAfterTheTutorial_DropsEveryTutorialRule()
    {
        _run.StartTutorialRun(_definition);
        _run.StartRunWithSeed("AFTER-TUTORIAL");

        Assert.That(_run.IsTutorialRun, Is.False);
        Assert.That(_run.HeadlinerTotal, Is.EqualTo(12));
        Assert.That(_run.currentPath.Count, Is.GreaterThan(4));
        Assert.That(_run.currentPath.Any(node => node.contentId != null && node.contentId.StartsWith("tutorial")), Is.False);
    }

    [Test]
    public void Gate_RefusesOnlyWhatTheFilterRefuses_AndReportsIt()
    {
        var refused = new List<TutorialCommand>();
        void Record(TutorialCommand command) => refused.Add(command);
        TutorialGate.Refused += Record;
        try
        {
            Assert.That(TutorialGate.Allows(TutorialCommand.Block), Is.True, "No tutorial, no refusals.");
            TutorialGate.SetFilter((command, _, __) => command == TutorialCommand.Pass);
            Assert.That(TutorialGate.Allows(TutorialCommand.Pass), Is.True);
            Assert.That(TutorialGate.Allows(TutorialCommand.Attack), Is.False);
            Assert.That(CombatCommands.Block(System.Array.Empty<CardView>(), null), Is.False);
            Assert.That(refused, Is.EqualTo(new[] { TutorialCommand.Attack, TutorialCommand.Block }));
            TutorialGate.Clear();
            Assert.That(TutorialGate.Allows(TutorialCommand.Attack), Is.True);
        }
        finally { TutorialGate.Refused -= Record; }
    }

    [Test]
    public void LaunchContext_TutorialRequestIsConsumedOnceAndReplacedByOtherLaunches()
    {
        FrontendLaunchContext.RequestTutorial();
        Assert.That(FrontendLaunchContext.HasPendingTutorial, Is.True);
        FrontendLaunchContext.RequestRun("SEED");
        Assert.That(FrontendLaunchContext.HasPendingTutorial, Is.False);

        FrontendLaunchContext.RequestTutorial();
        Assert.That(FrontendLaunchContext.TryConsumeRunSeed(out _), Is.False);
        Assert.That(FrontendLaunchContext.TryConsumeTutorial(), Is.True);
        Assert.That(FrontendLaunchContext.TryConsumeTutorial(), Is.False);
    }

    // The whole lesson plan, played the way the coach asks, from the first route to FINISH.
    [Test]
    public void Director_TeachesEveryLessonInOrderAndMarksTheTutorialDone()
    {
        _director = TutorialDirector.Begin(_run, _definition);
        Assert.That(_director, Is.Not.Null);
        var visited = new List<string>();
        for (int guard = 0; guard < 600 && !_director.HasEnded; guard++)
        {
            _director.Tick();
            var step = _director.CurrentStep;
            if (step == null) break;
            if (visited.LastOrDefault() != step.Id) visited.Add(step.Id);
            Act(step);
        }

        var lessons = new[] { "welcome", "route", "enemy", "hand", "attack", "incoming", "perfect-block", "perfect-draw",
            "finish-1", "loot", "route-2", "targets", "pass", "trick-heal", "trick-card", "trick-enemy", "ace-pair",
            "route-market", "market", "buy-gear", "services", "sell", "leave-market", "route-headliner", "headliner",
            "glitch", "win-headliner", "reward", "done" };
        Assert.That(visited.Intersect(lessons), Is.EqualTo(lessons), $"Visited: {string.Join(", ", visited)}");
        Assert.That(_director.HasEnded, Is.True);
        Assert.That(TutorialProgress.Completed, Is.True);
        Assert.That(_director.HeadlinerReward?.Rank, Is.EqualTo(CardData.Rank.Jack));
        Assert.That(_cards.ownedCards.Any(card => card.Rank == CardData.Rank.Jack && card.Suit == _definition.headlinerSuit), Is.True);
    }

    // Copy follows the GameData workbook: no retired terms, and no unformatted placeholders.
    [Test]
    public void Script_UsesWorkbookTermsOnly()
    {
        _director = TutorialDirector.Begin(_run, _definition);
        var retired = new Regex(@"\b(Boss|Gold|Shop|Relic|Artifact|Consumable|Enhancement|Elite|Event|Upgrade)\b");
        foreach (var step in _director.Steps)
        {
            string copy = $"{step.Lesson} {step.Title()} {step.Body()} {step.Refusal?.Invoke()} {step.Button}";
            Assert.That(retired.IsMatch(copy), Is.False, $"{step.Id}: {copy}");
            Assert.That(copy, Does.Not.Contain("{"), step.Id);
        }
        Assert.That(_director.Steps.Select(step => step.Id), Is.Unique);
    }

    void Act(TutorialStep step)
    {
        var state = _combat.currentState;
        if (step.IsModal) { _director.Next(); return; }
        switch (step.Id)
        {
            case "perfect-block":
            {
                var exact = ExactBlock(_combat.IncomingDamageAfterModifiers);
                var over = _cards.hand.First(card => card.AttackValue > _combat.IncomingDamageAfterModifiers);
                Assert.That(TutorialGate.Allows(TutorialCommand.Block, new[] { over }), Is.False);
                Assert.That(TutorialGate.Allows(TutorialCommand.Pass), Is.False);
                Assert.That(TutorialGate.Allows(TutorialCommand.Block, exact) && _combat.TryDefendWithCards(exact, _combat.currentEnemy), Is.True);
                return;
            }
            case "pass":
                Assert.That(TutorialGate.Allows(TutorialCommand.Block, new[] { _cards.hand[0] }), Is.False);
                Assert.That(TutorialGate.Allows(TutorialCommand.Pass), Is.True);
                _combat.TakeRemainingDamage();
                return;
            case "trick-heal":
            case "trick-card":
            case "trick-enemy":
                Assert.That(TutorialGate.Allows(TutorialCommand.Attack, new[] { _cards.hand[0] }, Living()), Is.False);
                UseTrick(step.Id);
                return;
            case "ace-pair":
            {
                var ace = _cards.hand.First(card => card.Rank == CardData.Rank.Ace);
                var partner = _cards.hand.Where(card => card != ace).OrderByDescending(card => card.AttackValue).First();
                Assert.That(TutorialGate.Allows(TutorialCommand.Attack, new[] { partner }, Living()), Is.False);
                Assert.That(TutorialGate.Allows(TutorialCommand.Attack, new[] { ace, partner }, Living()) &&
                    _combat.TryPlayCards(new[] { ace, partner }, Living()), Is.True);
                return;
            }
            case "buy-gear":
                Assert.That(_run.PurchaseShopOffer(_run.GetCurrentShopOffers().First(offer => offer.kind == ShopOfferKind.Gear).StableId), Is.True);
                return;
            case "leave-market":
                _run.OnShopDone();
                return;
        }
        if (step.Id.StartsWith("route"))
        {
            _run.OnPathChosen(_run.currentPath.First(node => node.accessible && !node.completed).id);
            return;
        }
        // Attack lessons and free fighting: hit hardest, take hits, Recover on an empty hand.
        if (state == GameState.EnemyAttacking) { _combat.TakeRemainingDamage(); return; }
        if (state != GameState.PlayerTurn) return;
        if (_cards.HandCount == 0) { _combat.Recover(); return; }
        var target = Living();
        var card = _cards.hand.Where(value => !target.ResistsSuit(value.Suit)).OrderByDescending(value => value.AttackValue).FirstOrDefault()
            ?? _cards.hand[0];
        Assert.That(TutorialGate.Allows(TutorialCommand.Attack, new[] { card }, target) && _combat.TryPlayCards(new[] { card }, target),
            Is.True, step.Id);
    }

    void UseTrick(string id)
    {
        var type = id switch
        {
            "trick-heal" => ConsumableEffectType.Heal,
            "trick-card" => ConsumableEffectType.ApplyEnhancement,
            _ => ConsumableEffectType.DirectEnemyDamage
        };
        int slot = Enumerable.Range(0, _cards.BackpackSlotsUsed).First(i => _cards.GetConsumableAtSlot(i).effectType == type);
        bool used = type switch
        {
            ConsumableEffectType.Heal => _cards.UseConsumableAtSlot(slot, _combat),
            ConsumableEffectType.ApplyEnhancement => _cards.UseEnhancementConsumableAtSlot(slot,
                _cards.hand.First(card => card.Enhancement == null).Id),
            _ => _cards.UseConsumableAtSlot(slot, _combat, Living())
        };
        Assert.That(used, Is.True, id);
    }

    void PlayToVictory()
    {
        for (int guard = 0; guard < 60 && _combat.currentState is GameState.PlayerTurn or GameState.EnemyAttacking; guard++)
        {
            if (_combat.currentState == GameState.EnemyAttacking) { _combat.TakeRemainingDamage(); continue; }
            var card = _cards.hand.OrderByDescending(value => value.AttackValue).First();
            Assert.That(_combat.TryPlayCards(new[] { card }, Living()), Is.True);
        }
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameWon));
    }

    CardInstance[] ExactBlock(int amount)
    {
        var hand = _cards.hand.ToArray();
        for (int mask = 1; mask < 1 << hand.Length; mask++)
        {
            var subset = hand.Where((_, i) => (mask & 1 << i) != 0).ToArray();
            if (subset.Sum(card => _combat.CalculateCardDefense(card)) == amount) return subset;
        }
        Assert.Fail($"No exact block of {amount} in hand.");
        return null;
    }

    EnemyRuntime Living() => _combat.Enemies.First(enemy => enemy != null && !enemy.IsDefeated);

    IEnumerable<(CardData.Suit, CardData.Rank)> Hand() => _cards.hand.Select(card => (card.Suit, card.Rank));
    static IEnumerable<(CardData.Suit, CardData.Rank)> Specs(IEnumerable<TutorialRunDefinition.CardSpec> specs) =>
        specs.Select(spec => (spec.suit, spec.rank));

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go.AddComponent<T>();
    }
}
