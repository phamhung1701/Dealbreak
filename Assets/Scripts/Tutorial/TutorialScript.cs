using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The tutorial's lessons, in order: route, attack, enemy response, Perfect Block, kill draw,
/// Credits and drops, targeting, PASS, the three kinds of Trick, Ace pairs, Recover, the Black
/// Market, Headliners, glitching cards and the face-card reward. Numbers and names are read from
/// the live run, so the copy always matches the board.
/// </summary>
public static class TutorialScript
{
    public static List<TutorialStep> Build(TutorialDirector d)
    {
        var run = d.Run;
        var def = d.Definition;
        var steps = new List<TutorialStep>();
        int marketNode = def.fights.Length, headlinerNode = def.fights.Length + 1;

        GameState State() => d.Combat ? d.Combat.currentState : GameState.Idle;
        bool PlayerTurn() => State() == GameState.PlayerTurn;
        bool Defending() => State() == GameState.EnemyAttacking;
        bool InFight() => PlayerTurn() || Defending();
        bool FightStarted(int node) => run.ActiveNode != null && run.ActiveNode.id == node && InFight();
        bool OnMap(int next) => run.ActiveNode == null && run.PendingEnemyDrop == null && !run.IsRunCompleted &&
            TutorialTargets.MapNode(next).Any();
        bool InMarket() => run.ActiveNode != null && run.ActiveNode.kind == MapNodeType.Shop;
        int Incoming() => d.Combat.IncomingDamageAfterModifiers;
        int EnemyHp() => d.Combat.Enemies.Where(enemy => enemy != null && !enemy.IsDefeated).Sum(enemy => enemy.currentHp);
        ConsumableData Trick(ConsumableEffectType type) => d.Cards.Backpack.FirstOrDefault(trick => trick && trick.effectType == type);
        IEnumerable<RectTransform> Join(params IEnumerable<RectTransform>[] parts) => parts.SelectMany(part => part);

        // After the scripted beats of a fight, the player finishes it freely; the coach keeps up.
        string FightTitle(string goal) => Defending() ? "Block or pass" : d.Cards.HandCount == 0 ? "Recover" : goal;
        string FightBody(string goal) => Defending()
            ? $"Block with cards, or press PASS to take the {Incoming()} damage."
            : d.Cards.HandCount == 0
                ? $"Your hand is empty, so you Recover on your own: you take the enemies' {d.Combat.TotalEnemyAttack} attack, then draw a card."
                : goal;
        IEnumerable<RectTransform> FightTargets() => Defending()
            ? Join(TutorialTargets.Hand(), TutorialTargets.BlockButton(), TutorialTargets.PassButton())
            : d.Cards.HandCount == 0 ? TutorialTargets.PlayerHealth() : TutorialTargets.Enemies();

        // A fight's route step ends once that fight is under way; the Black Market's once it is open.
        TutorialStep Route(string id, string title, Func<string> body, int node, Action enter = null) => new()
        {
            Id = id, Lesson = "Route", Title = () => title, Body = body,
            Targets = () => TutorialTargets.MapNode(node), Ready = () => OnMap(node), Enter = enter, Side = Vector2.up,
            Done = () => node == marketNode ? InMarket() : FightStarted(node)
        };

        int turns = 0;
        TutorialStep Attack(string id, string lesson, string title, Func<string> body,
            Func<IEnumerable<RectTransform>> targets, TutorialGate.CommandFilter filter = null, Func<string> refusal = null) => new()
        {
            Id = id, Lesson = lesson, Title = () => title, Body = body, Targets = targets, Ready = PlayerTurn,
            Skip = () => !InFight(),
            Enter = () => turns = d.Combat.CompletedPlayerTurnCount,
            Done = () => d.Combat.CompletedPlayerTurnCount > turns || !InFight(),
            Filter = filter ?? ((command, _, __) => command == TutorialCommand.Attack),
            Refusal = refusal ?? (() => "Attack first: select a card, then press ATTACK.")
        };

        int used = 0;
        TutorialStep TrickLesson(string id, string title, ConsumableEffectType type, Func<ConsumableData, string> body,
            Func<IEnumerable<RectTransform>> target) => new()
        {
            Id = id, Lesson = "Tricks", Title = () => title,
            Body = () => Trick(type) is { } trick ? body(trick) : string.Empty,
            Targets = () => Join(TutorialTargets.BackpackSlots(trick => trick.effectType == type), target()),
            Ready = PlayerTurn, Side = Vector2.right,
            Skip = () => !InFight() || Trick(type) == null ||
                type == ConsumableEffectType.Heal && d.Combat.player.currentHealth >= d.Combat.player.maxHealth,
            Enter = () => used = d.TricksUsed(type),
            Done = () => d.TricksUsed(type) > used,
            Filter = (_, __, ___) => false,
            Refusal = () => "Use the Trick first: drag it out of your backpack."
        };

        int wins = 0;
        void CountWins() => wins = d.Victories;

        // District map.
        steps.Add(new TutorialStep
        {
            Id = "welcome", Lesson = "Dealbreak", Title = () => "Welcome to the deal",
            Body = () => "A run crosses 12 Districts. Each one ends with a Headliner: four Jacks, then four Queens, then four Kings.\n\n" +
                "You fight with a 40-card deck, Ace to 10 in four suits. This short District teaches the moves.",
            Ready = () => run.ActiveNode == null
        });
        steps.Add(Route("route", "Pick a route",
            () => "This is the District map. Its nodes are fights, Black Markets, Incidents and, at the end, the Headliner. You only move forward. " +
                "The line under the District title names its Headliner and what it can do.\n\nClick the first fight.",
            0, CountWins));

        // Fight 1: attack, the enemy response, Perfect Block and the kill draw.
        string firstEnemy = def.fights[0].enemies[0].enemyName;
        steps.Add(new TutorialStep
        {
            Id = "enemy", Lesson = "Combat", Title = () => "Read the enemy",
            Body = () => $"HP is the damage it takes to defeat the {firstEnemy}.\n\nATTACK is how hard it hits back each time it survives your turn. " +
                "Shield icons on an enemy's HP bar each stop one hit; while it has any, the bar is grey.\n\n" +
                "Click an enemy, card or item to read it. Click anywhere else to close it.",
            Targets = () => TutorialTargets.Enemies(), Ready = PlayerTurn, Skip = () => !InFight()
        });
        steps.Add(new TutorialStep
        {
            Id = "hand", Lesson = "Combat", Title = () => "Your hand",
            Body = () => $"Each card deals its rank in damage: Ace 1, then 2 to 10. You hold up to {d.Cards.HandCapacity} cards.\n\n" +
                "There is no draw each turn. Cards come from defeating enemies, from Perfect Blocks, and from Recover, which happens on its own when your hand is empty.",
            Targets = TutorialTargets.Hand, Ready = PlayerTurn, Skip = () => !InFight()
        });
        steps.Add(Attack("attack", "Attack", "Attack",
            () => "Click a card to select it, then press ATTACK. You can also drag a card onto the enemy.",
            () => Join(TutorialTargets.Hand(), TutorialTargets.AttackButton(), TutorialTargets.Enemies())));
        steps.Add(new TutorialStep
        {
            Id = "incoming", Lesson = "Defense", Title = () => "It hits back",
            Body = () => $"The {firstEnemy} survived, so it attacks. INCOMING is the damage on its way: {Incoming()}.\n\n" +
                "Block it by spending cards from your hand. Each card blocks its rank.",
            Targets = () => Join(TutorialTargets.Incoming(), TutorialTargets.Enemies()), Ready = Defending, Skip = () => !InFight()
        });
        int perfectBlocks = 0;
        steps.Add(new TutorialStep
        {
            Id = "perfect-block", Lesson = "Perfect Block", Title = () => "Perfect Block",
            Body = () => $"Block exactly {Incoming()}, no more and no less, for a Perfect Block: you take nothing and draw a card.\n\n" +
                $"Select cards that add up to {Incoming()}, then press BLOCK.",
            Targets = () => Join(TutorialTargets.Hand(), TutorialTargets.BlockButton(), TutorialTargets.Incoming()),
            Ready = Defending, Skip = () => !InFight(),
            Enter = () => perfectBlocks = d.PerfectBlocks,
            Done = () => d.PerfectBlocks > perfectBlocks || PlayerTurn(),
            Filter = (command, cards, _) => command == TutorialCommand.Block &&
                cards.Sum(card => d.Combat.CalculateCardDefense(card)) == Incoming(),
            Refusal = () => $"That isn't exactly {Incoming()}. Pick cards that add up to {Incoming()}."
        });
        steps.Add(new TutorialStep
        {
            Id = "perfect-draw", Lesson = "Perfect Block", Title = () => "Cards come back",
            Body = () => "The Perfect Block drew you a card. Every card you attack or block with goes back into your draw pile, which reshuffles.",
            Targets = TutorialTargets.DrawPile, Ready = PlayerTurn, Skip = () => !InFight()
        });
        steps.Add(new TutorialStep
        {
            Id = "finish-1", Lesson = "Combat",
            Title = () => FightTitle("Finish it"),
            Body = () => FightBody($"Defeat the {firstEnemy}: {EnemyHp()} HP left. A card that deals at least that much finishes it, and every enemy you defeat draws you a card."),
            Targets = FightTargets, Done = () => d.Victories > wins
        });
        steps.Add(new TutorialStep
        {
            Id = "loot", Lesson = "Loot", Title = () => "Credits and drops",
            Body = () => "Defeated enemies pay Credits: the count rises as their coins land, and they are yours once the fight is won. Spend them at the Black Market." +
                (def.fights[0].drop ? $"\n\nThis fight also dropped a Trick, the {def.fights[0].drop.displayName}. Drops go straight into your backpack, which holds {d.Cards.BackpackCapacity} Tricks." : string.Empty),
            Targets = () => Join(TutorialTargets.Credits(), TutorialTargets.BackpackSlots()),
            Ready = () => OnMap(1)
        });

        // Fight 2: targets, PASS, Tricks on yourself, a card and an enemy, the Ace pair, Recover.
        steps.Add(Route("route-2", "Next fight", () => "Take the next fight.", 1, CountWins));
        steps.Add(Attack("targets", "Attack", "Choose a target",
            () => "Two enemies this time. Select a card and press ATTACK, then click the enemy to hit. Dragging a card onto an enemy works too.",
            () => Join(TutorialTargets.Enemies(), TutorialTargets.AttackButton())));
        steps.Add(new TutorialStep
        {
            Id = "pass", Lesson = "Defense", Title = () => "Pass",
            Body = () => $"Blocking is optional. PASS takes the {Incoming()} INCOMING damage and keeps your cards for attacking.\n\nPress PASS.",
            Targets = () => Join(TutorialTargets.PassButton(), TutorialTargets.Incoming()),
            Ready = Defending, Skip = () => !InFight(), Done = PlayerTurn,
            Filter = (command, _, __) => command == TutorialCommand.Pass,
            Refusal = () => "Not this time: press PASS."
        });
        steps.Add(TrickLesson("trick-heal", "Heal yourself", ConsumableEffectType.Heal,
            trick => $"Tricks are one-use items. Drag the {trick.displayName} from your backpack onto your HP to heal {trick.healAmount}.",
            TutorialTargets.PlayerHealth));
        steps.Add(TrickLesson("trick-card", "Mod a card", ConsumableEffectType.ApplyEnhancement,
            trick => $"Card Tricks only work on cards in your hand. Drag the {trick.displayName} onto any card to give it the " +
                $"{(trick.enhancementToApply ? trick.enhancementToApply.displayName : "new")} Mod. Click the card to read it. " +
                "Whenever a Mod does something, its card hops and shows what it did.",
            TutorialTargets.Hand));
        steps.Add(TrickLesson("trick-enemy", "Hit an enemy", ConsumableEffectType.DirectEnemyDamage,
            trick => $"Drag the {trick.displayName} onto an enemy: {trick.damageAmount} damage, and no card spent.",
            () => TutorialTargets.Enemies()));
        var acePair = Attack("ace-pair", "Attack", "Ace pair",
            () => "An Ace teams up with any one other card: both hit together for their combined damage. Select an Ace and one other card, then press ATTACK.",
            () => Join(TutorialTargets.HandCards(card => card.Rank == CardData.Rank.Ace), TutorialTargets.AttackButton()),
            (command, cards, _) => command == TutorialCommand.Attack && GameplayEffectResolver.CanPlayAsAcePair(cards),
            () => "Select an Ace plus exactly one other card.");
        acePair.Skip = () => !InFight() || d.Cards.HandCount < 2 || d.Cards.hand.All(card => card.Rank != CardData.Rank.Ace);
        steps.Add(acePair);
        steps.Add(new TutorialStep
        {
            Id = "finish-2", Lesson = "Combat", Title = () => FightTitle("Win the fight"),
            Body = () => FightBody($"Defeat the rest: {EnemyHp()} HP to go."),
            Targets = FightTargets, Done = () => d.Victories > wins
        });

        // Any further fights in the definition are played freely.
        for (int fight = 2; fight < def.fights.Length; fight++)
        {
            steps.Add(Route($"route-{fight + 1}", "Next fight", () => "Take the next fight.", fight, CountWins));
            steps.Add(new TutorialStep
            {
                Id = $"finish-{fight + 1}", Lesson = "Combat", Title = () => FightTitle("Win the fight"),
                Body = () => FightBody($"Defeat the enemies: {EnemyHp()} HP to go."),
                Targets = FightTargets, Done = () => d.Victories > wins
            });
        }

        // Black Market.
        steps.Add(Route("route-market", "Black Market",
            () => $"You have {d.Cards.credits} Credits. Visit the Black Market.", marketNode));
        steps.Add(new TutorialStep
        {
            Id = "market", Lesson = "Black Market", Title = () => "Gear and Tricks",
            Body = () => $"Gear works in every fight for the rest of the run, and you can hold {d.Cards.GearCapacity}. Tricks are one-use and go in your backpack.\n\n" +
                "Each price button buys at once and the item jumps into its slot. Click a tile to read its rules. REROLL GOODS swaps the goods for a new set.",
            Targets = () => TutorialTargets.ShopOffers(offer => !offer.IsService),
            Ready = () => TutorialTargets.MarketOpen, Skip = () => !InMarket()
        });
        int GearPrice() => run.GetCurrentShopOffers().FirstOrDefault(offer => offer.kind == ShopOfferKind.Gear &&
            offer.gear == def.marketGear)?.price ?? def.marketGear.price;
        steps.Add(new TutorialStep
        {
            Id = "buy-gear", Lesson = "Black Market", Title = () => "Buy Gear",
            Body = () => $"Buy the {def.marketGear.displayName} for {GearPrice()} Credits. Owned Gear sits in the Gear row at the top. " +
                "When Gear does something, it hops and shows what it did, in the order of the row.",
            Targets = () => TutorialTargets.ShopOffers(offer => offer.kind == ShopOfferKind.Gear && offer.gear == def.marketGear),
            Ready = () => TutorialTargets.MarketOpen,
            Skip = () => !InMarket() || !d.Cards.HasGear(def.marketGear) && !d.Cards.CanAfford(GearPrice()),
            Done = () => d.Cards.HasGear(def.marketGear)
        });
        steps.Add(new TutorialStep
        {
            Id = "services", Lesson = "Black Market", Title = () => "Services",
            Body = () => "Stake puts Credits down: at your next Black Market it pays back 3× or nothing.\n\n" +
                "Supply buys cards from your draw pile straight into your hand.\n\nSet the amount with MIN, −, + and MAX on the tile, then press its price.",
            Targets = () => TutorialTargets.ShopOffers(offer => offer.IsService),
            Ready = () => TutorialTargets.MarketOpen, Skip = () => !InMarket()
        });
        steps.Add(new TutorialStep
        {
            Id = "sell", Lesson = "Black Market", Title = () => "Sell, merge and move",
            Body = () => "To sell Gear or a Trick, drag it onto the Black Market. Drop a Gear on a matching copy to merge the two into the next tier.\n\n" +
                "Drop a Gear or a Trick on another slot to move it there. While you hold an item, a red DISCARD strip on the right edge lets you throw it away.",
            Targets = () => Join(TutorialTargets.GearRail(), TutorialTargets.BackpackSlots()),
            Ready = () => TutorialTargets.MarketOpen, Skip = () => !InMarket()
        });
        steps.Add(new TutorialStep
        {
            Id = "leave-market", Lesson = "Black Market", Title = () => "Move on",
            Body = () => "Press CONTINUE TO MAP when you are done shopping.",
            Targets = TutorialTargets.MarketContinue, Ready = () => TutorialTargets.MarketOpen,
            Done = () => !InMarket()
        });

        // Headliner: glitching cards and the face-card reward.
        steps.Add(Route("route-headliner", "The Headliner",
            () => "Every District ends with a Headliner. Take it on.", headlinerNode, CountWins));
        string HeadlinerCard() => run.CurrentHeadliner?.sourceCard ? run.CurrentHeadliner.sourceCard.DisplayName : "face card";
        steps.Add(new TutorialStep
        {
            Id = "headliner", Lesson = "Headliner", Title = () => run.CurrentHeadliner ? run.CurrentHeadliner.enemyName : "Headliner",
            Body = () => $"Headliners are face cards. Defeat this one and its {HeadlinerCard()} joins your deck for the rest of the run.",
            Targets = () => TutorialTargets.Enemies(), Ready = PlayerTurn, Skip = () => !InFight()
        });
        bool Glitching(CardInstance card) => d.Combat.Enemies.Any(enemy => enemy != null && enemy.ResistsSuit(card.Suit));
        steps.Add(new TutorialStep
        {
            Id = "glitch", Lesson = "Headliner", Title = () => "Glitching cards",
            Body = () => $"Glitching cards share the Headliner's suit ({Symbol(def.headlinerSuit)}). They deal it no damage, but they still block.\n\n" +
                "Some enemies call a suit each turn. That suit glitches too, and its next hit is halved.",
            Targets = () => TutorialTargets.HandCards(Glitching), Ready = PlayerTurn,
            Skip = () => !InFight() || !d.Cards.hand.Any(Glitching)
        });
        steps.Add(new TutorialStep
        {
            Id = "win-headliner", Lesson = "Headliner", Title = () => FightTitle($"Take the {HeadlinerCard()}"),
            Body = () => FightBody($"Defeat {(run.CurrentHeadliner ? run.CurrentHeadliner.enemyName : "the Headliner")}: {EnemyHp()} HP to go."),
            Targets = FightTargets, Done = () => d.HeadlinerRewardGranted
        });
        steps.Add(new TutorialStep
        {
            Id = "reward", Lesson = "Headliner", Title = () => $"{d.HeadlinerReward?.DisplayName ?? "The card"} is yours",
            Body = () => $"The {d.HeadlinerReward?.DisplayName ?? "face card"} went into your draw pile. Face cards hit hard: Jacks 10, Queens 15, Kings 20.\n\n" +
                "Every Headliner you defeat adds its card.",
            Targets = TutorialTargets.HeadlinerReward, Ready = () => d.HeadlinerRewardGranted
        });
        steps.Add(new TutorialStep
        {
            Id = "done", Lesson = "Dealbreak", Title = () => "That's the deal",
            Body = () => "A full run is 12 Districts, with Incidents, Enforcers and tougher Headliners along the way. Your HP carries over between fights.\n\n" +
                "NEW RUN starts a real run. TUTORIAL in the main menu replays this one.",
            Ready = () => d.HeadlinerRewardGranted, Button = "FINISH"
        });
        return steps;
    }

    static string Symbol(CardData.Suit suit) => suit switch
    {
        CardData.Suit.Hearts => "♥",
        CardData.Suit.Diamonds => "♦",
        CardData.Suit.Clubs => "♣",
        _ => "♠"
    };
}
