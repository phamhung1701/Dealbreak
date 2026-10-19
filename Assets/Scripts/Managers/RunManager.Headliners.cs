using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// RunManager: Headliner sequence, generated abilities and canonical names.
public partial class RunManager
{
    void BuildHeadlinerDeck()
    {
        headlinerDeck.Clear();
        headlinerIndex = 0;
        foreach (var rank in new[] { CardData.Rank.Jack, CardData.Rank.Queen, CardData.Rank.King })
        {
            var rankHeadliners = new List<EnemyTypeData>();
            foreach (CardData.Suit suit in Enum.GetValues(typeof(CardData.Suit)))
            {
                var card = CardData.Create(suit, rank);
                rankHeadliners.Add(CreateHeadlinerType(card));
            }
            rankHeadliners.Shuffle(_randomContext.CreateStream("boss-order", (int)rank));
            headlinerDeck.AddRange(rankHeadliners);
        }
    }

    EnemyTypeData CreateHeadlinerType(CardData card)
    {
        int credits = card.rank switch
        {
            CardData.Rank.Jack => 10,
            CardData.Rank.Queen => 15,
            _ => 20,
        };
        var headliner = EnemyTypeData.Create(GetCanonicalHeadlinerName(card), 30, 6, credits);
        headliner.sourceCard = card;
        var random = _randomContext.CreateStream("boss-ability", (int)card.rank * 10 + (int)card.suit);
        EnemyAbility[] abilities;
        if (card.rank != CardData.Rank.King)
        {
            abilities = new[] { CreateAbility(Choose(random,
                EnemyAbilityEffect.DoubleStrike, EnemyAbilityEffect.HeavySwing,
                EnemyAbilityEffect.Desperation, EnemyAbilityEffect.Regeneration,
                EnemyAbilityEffect.Guarded, EnemyAbilityEffect.Silence,
                EnemyAbilityEffect.Withering, EnemyAbilityEffect.Oppression,
                EnemyAbilityEffect.FullCoverageBlock)) };
        }
        else
        {
            var hardCounters = new[] { EnemyAbilityEffect.Silence, EnemyAbilityEffect.Withering, EnemyAbilityEffect.Oppression };
            bool hasHardCounter = random.NextInt(0, 2) == 0;
            if (hasHardCounter)
            {
                var chosenHardCounter = Choose(random, hardCounters);
                var secondary = Choose(random, new[] { EnemyAbilityEffect.DoubleStrike,
                    EnemyAbilityEffect.HeavySwing, EnemyAbilityEffect.Desperation,
                    EnemyAbilityEffect.Regeneration, EnemyAbilityEffect.Guarded,
                    EnemyAbilityEffect.FullCoverageBlock });
                abilities = new[] { CreateAbility(chosenHardCounter), CreateAbility(secondary) };
            }
            else
            {
                abilities = new[] {
                    CreateAbility(Choose(random, EnemyAbilityEffect.Regeneration, EnemyAbilityEffect.Guarded,
                        EnemyAbilityEffect.FullCoverageBlock)),
                    CreateAbility(Choose(random, EnemyAbilityEffect.DoubleStrike,
                        EnemyAbilityEffect.HeavySwing, EnemyAbilityEffect.Desperation)) };
            }
        }
        headliner.abilities = abilities;
        _generatedEnemyTypes.Add(headliner);
        return headliner;
    }

    internal static string GetCanonicalHeadlinerName(CardData card)
    {
        if (card == null) return "Headliner";
        return (card.rank, card.suit) switch
        {
            (CardData.Rank.Jack, CardData.Suit.Hearts) => "J♥ — The Charmer",
            (CardData.Rank.Jack, CardData.Suit.Diamonds) => "J♦ — The Courier",
            (CardData.Rank.Jack, CardData.Suit.Clubs) => "J♣ — The Hitter",
            (CardData.Rank.Jack, CardData.Suit.Spades) => "J♠ — The Fixer",
            (CardData.Rank.Queen, CardData.Suit.Hearts) => "Q♥ — The Influencer",
            (CardData.Rank.Queen, CardData.Suit.Diamonds) => "Q♦ — The Broker",
            (CardData.Rank.Queen, CardData.Suit.Clubs) => "Q♣ — The Foreman",
            (CardData.Rank.Queen, CardData.Suit.Spades) => "Q♠ — The Handler",
            (CardData.Rank.King, CardData.Suit.Hearts) => "K♥ — The Icon",
            (CardData.Rank.King, CardData.Suit.Diamonds) => "K♦ — The Mogul",
            (CardData.Rank.King, CardData.Suit.Clubs) => "K♣ — The Chairman",
            (CardData.Rank.King, CardData.Suit.Spades) => "K♠ — The Director",
            _ => "Headliner"
        };
    }

    static T Choose<T>(IRandomSource random, params T[] options) => options[random.NextInt(0, options.Length)];

    EnemyAbility CreateAbility(EnemyAbilityEffect effect)
    {
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        ability.effect = effect;
        ability.id = effect.ToString();
        ability.displayName = HeadlinerAbilityName(effect);
        ability.description = HeadlinerAbilityDescription(effect);
        _generatedEnemyAbilities.Add(ability);
        return ability;
    }

    // Headliner identity text is derived from rank/suit/effect, so restored saves show current terminology.
    internal static string HeadlinerAbilityName(EnemyAbilityEffect effect) => effect switch
        {
            EnemyAbilityEffect.HeavySwing => "Heavy Swing",
            EnemyAbilityEffect.Desperation => "Desperation",
            EnemyAbilityEffect.Regeneration => "Regeneration",
            EnemyAbilityEffect.Guarded => "Guarded",
            EnemyAbilityEffect.DoubleStrike => "Double Strike",
            EnemyAbilityEffect.Silence => "Silence",
            EnemyAbilityEffect.Withering => "Withering",
            EnemyAbilityEffect.Oppression => "Oppression",
            EnemyAbilityEffect.FullCoverageBlock => "Unyielding Defense",
            _ => effect.ToString()
        };

    internal static string HeadlinerAbilityDescription(EnemyAbilityEffect effect) => effect switch
        {
            EnemyAbilityEffect.HeavySwing => "Alternates normal attacks with a 150% ATK response.",
            EnemyAbilityEffect.Desperation => "Gains 25% ATK while below 50% maximum HP.",
            EnemyAbilityEffect.Regeneration => "Heals 5% maximum HP after an enemy response, up to 3 times.",
            EnemyAbilityEffect.Guarded => "The first non-immune hit is reduced by 50%; resets after each enemy response.",
            EnemyAbilityEffect.DoubleStrike => "Attacks twice per response; each hit deals 60% of the response budget.",
            EnemyAbilityEffect.Silence => "While above 50% HP, disables held-card effects.",
            EnemyAbilityEffect.Withering => "While above 50% HP, prevents player healing.",
            EnemyAbilityEffect.Oppression => "+2 response damage per held card beyond 3.",
            EnemyAbilityEffect.FullCoverageBlock => "Each attack must be fully covered by one Block card; partial or combined blocks are rejected.",
            _ => effect.ToString()
        };

    public string CurrentHeadlinerDescription
    {
        get
        {
            var headliner = CurrentHeadliner;
            if (headliner == null) return string.Empty;
            string abilities = headliner.abilities == null ? string.Empty :
                string.Join(" • ", headliner.abilities.Where(ability => ability != null).Select(ability =>
                    string.IsNullOrEmpty(ability.description) ? ability.displayName : $"{ability.displayName}: {ability.description}"));

            int headlinerHp = CurrentMapIndex == 0 ? FirstMapHeadlinerHp : headliner.maxHp;
            var stats = ScaleEncounterStats(headlinerHp, headliner.baseAttack, CurrentMapIndex);
            return string.IsNullOrEmpty(abilities)
                ? $"{headliner.enemyName} • {stats.hp} HP / {stats.attack} ATK"
                : $"{headliner.enemyName} • {stats.hp} HP / {stats.attack} ATK • {abilities}";
        }
    }
}
