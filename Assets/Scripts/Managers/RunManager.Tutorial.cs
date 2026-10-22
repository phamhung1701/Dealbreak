using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// RunManager: the scripted tutorial run. One District with a fixed route (the definition's fights,
// a Black Market, then a Jack Headliner), fixed rosters, hands, goods and drops. Rosters go through
// FindEnemy and EncounterComposer like any fight. The tutorial never writes or deletes the run save:
// GameController leaves the autosave off for it.
public partial class RunManager
{
    const string TutorialFightPrefix = "tutorial-fight-";

    TutorialRunDefinition _tutorial;
    readonly Dictionary<string, EnemyTypeData> _tutorialEnemies = new();
    readonly Dictionary<string, TutorialRunDefinition.Fight> _tutorialFights = new();

    public bool IsTutorialRun => _tutorial != null;
    public TutorialRunDefinition Tutorial => _tutorial;

    public bool StartTutorialRun(TutorialRunDefinition tutorial)
    {
        string error = null;
        if (tutorial == null || !tutorial.IsValid(out error))
        {
            Debug.LogError($"RunManager: the tutorial cannot start: {(tutorial == null ? "Resources/TutorialRun is missing" : error)}.", this);
            return false;
        }
        if (CardManager.Instance == null || CombatManager.Instance == null)
        {
            Debug.LogError("RunManager: run configuration is incomplete.", this);
            return false;
        }

        runSeed = RunRandomContext.NormalizeSeed(tutorial.seed);
        _randomContext = new RunRandomContext(runSeed);

        SubscribeToCombatResults();
        CombatManager.Instance.Reset();
        CombatManager.Instance.ConfigureCriticalRandom(_randomContext.CreateStream("combat-critical"));
        CombatManager.Instance.ConfigureDuelistSuitRandom(_randomContext.CreateStream("duelist-suit"));
        CardManager.Instance.Reset();
        ResetRuntimeState();
        _tutorial = tutorial;
        CardManager.Instance.ConfigureRandom(_randomContext.CreateStream("cards"));
        CardManager.Instance.BuildDeck();
        StageTutorialHand(tutorial.fights[0].hand);
        BuildTutorialContent();
        Timing.StartNewRun(CurrentMapIndex);
        OnRunStarted?.Invoke(runSeed);
        NextCycle();
        return true;
    }

    void BuildTutorialContent()
    {
        for (int i = 0; i < _tutorial.fights.Length; i++)
        {
            var fight = _tutorial.fights[i];
            var primary = CreateTutorialEnemy(fight.enemies[0]);
            primary.allies = fight.enemies.Skip(1)
                .Select(spec => new EncounterAllySpec { type = CreateTutorialEnemy(spec), count = 1 }).ToArray();
            _tutorialEnemies[TutorialFightPrefix + i] = primary;
            _tutorialFights[TutorialFightPrefix + i] = fight;
        }

        headlinerDeck.Clear();
        headlinerIndex = 0;
        var card = CardData.Create(_tutorial.headlinerSuit, CardData.Rank.Jack);
        var headliner = EnemyTypeData.Create(Headliners.NameFor(card), Headliners.maxHp, _tutorial.headlinerAttack,
            Headliners.CreditsFor(card.rank));
        headliner.sourceCard = card;
        headliner.abilities = Array.Empty<EnemyAbility>();
        _generatedEnemyTypes.Add(headliner);
        headlinerDeck.Add(headliner);
    }

    EnemyTypeData CreateTutorialEnemy(TutorialRunDefinition.EnemySpec spec)
    {
        var enemy = EnemyTypeData.Create(spec.enemyName, spec.hp, spec.attack, spec.credits);
        enemy.abilities = Array.Empty<EnemyAbility>();
        enemy.eligibleAsEliteBase = false;
        _generatedEnemyTypes.Add(enemy);
        return enemy;
    }

    // The fights in a row, then the Black Market, then the Headliner.
    List<PathNode> BuildTutorialPath()
    {
        var nodes = new List<PathNode>();
        for (int i = 0; i < _tutorial.fights.Length; i++)
            nodes.Add(new PathNode { id = i, kind = MapNodeType.Combat, type = "combat",
                contentId = TutorialFightPrefix + i, col = i, row = 1 });
        int shop = nodes.Count;
        nodes.Add(new PathNode { id = shop, kind = MapNodeType.Shop, type = "shop", contentId = RunMapGenerator.MidBlackMarketContentId,
            col = shop, row = 1, isShopCheckpoint = true });
        nodes.Add(new PathNode { id = shop + 1, kind = MapNodeType.Headliner, type = "boss", contentId = "boss",
            col = shop + 1, row = 1 });
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].mapIndex = CurrentMapIndex;
            nodes[i].revealed = true;
            if (i + 1 < nodes.Count) nodes[i].next.Add(nodes[i + 1].id);
        }
        nodes[0].accessible = true;
        return nodes;
    }

    TutorialRunDefinition.Fight TutorialFightFor(PathNode node) =>
        node != null && _tutorialFights.TryGetValue(node.contentId ?? string.Empty, out var fight) ? fight : null;

    // Each fight starts from its scripted hand, with the Tricks it hands out already in the backpack.
    void PrepareTutorialEncounter(PathNode node)
    {
        var cards = CardManager.Instance;
        var fight = TutorialFightFor(node);
        if (fight != null)
        {
            StageTutorialHand(fight.hand);
            foreach (var trick in fight.grants)
                if (trick != null && cards.CanAddConsumable(trick)) cards.AddConsumable(trick);
        }
        else if (node.kind == MapNodeType.Headliner) StageTutorialHand(_tutorial.headlinerHand);
    }

    void StageTutorialHand(IReadOnlyList<TutorialRunDefinition.CardSpec> hand)
    {
        var cards = CardManager.Instance;
        if (hand == null || hand.Count == 0 || cards == null) return;
        if (cards.hand.Count == hand.Count && hand.All(spec => cards.hand.Any(spec.Matches))) return;
        if (cards.HandCount > 0) cards.TryReturnToDrawPile(cards.hand.ToList());
        foreach (var spec in hand)
        {
            var card = cards.deck.FirstOrDefault(spec.Matches);
            if (card != null) cards.TryTakeCardToHand(card);
        }
    }

    ConsumableData TutorialDropFor(PathNode node) => TutorialFightFor(node)?.drop;

    void PrepareTutorialShopOffers()
    {
        var gear = _tutorial.marketGear;
        var trick = _tutorial.marketTrick;
        _activeShopOffers.Add(new ShopOffer { kind = ShopOfferKind.Gear, gear = gear, basePrice = gear.price,
            price = CalculateEffectiveShopPrice(ShopOfferKind.Gear, gear.price) });
        _activeShopOffers.Add(new ShopOffer { kind = ShopOfferKind.Consumable, consumable = trick, basePrice = trick.price,
            price = CalculateEffectiveShopPrice(ShopOfferKind.Consumable, trick.price) });
        _activeShopOffers.Add(ServiceOffer(ShopOfferKind.Investment));
        _activeShopOffers.Add(ServiceOffer(ShopOfferKind.Supply));
    }

    void ResetTutorialState()
    {
        _tutorial = null;
        _tutorialEnemies.Clear();
        _tutorialFights.Clear();
    }
}
