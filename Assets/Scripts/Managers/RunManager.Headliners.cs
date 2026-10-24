using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// RunManager: Headliner sequence and generated abilities. Names, text, balance and ability pools
// are authored in Resources/HeadlinerCatalog; pool order is part of seeded generation.
public partial class RunManager
{
    HeadlinerCatalog _headliners;

    internal HeadlinerCatalog Headliners
    {
        get
        {
            if (_headliners == null) _headliners = HeadlinerCatalog.Load();
            if (_headliners == null)
                throw new InvalidOperationException("Resources/HeadlinerCatalog is missing.");
            return _headliners;
        }
    }

    /// <summary>Maximum HP of the District 1 Headliner, before District scaling.</summary>
    public int FirstMapHeadlinerHp => Headliners.firstDistrictMaxHp;

    internal void BuildHeadlinerDeck()
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
        var catalog = Headliners;
        var headliner = EnemyTypeData.Create(catalog.NameFor(card), catalog.maxHp, catalog.baseAttack,
            catalog.CreditsFor(card.rank));
        headliner.sourceCard = card;
        var random = _randomContext.CreateStream("boss-ability", (int)card.rank * 10 + (int)card.suit);
        EnemyAbility[] abilities;
        if (card.rank != CardData.Rank.King)
        {
            abilities = new[] { CreateAbility(Choose(random, catalog.jackAndQueenAbilities)) };
        }
        else
        {
            bool hasHardCounter = random.NextInt(0, 2) == 0;
            if (hasHardCounter)
            {
                var chosenHardCounter = Choose(random, catalog.kingHardCounters);
                var secondary = Choose(random, catalog.kingHardCounterPartners);
                abilities = new[] { CreateAbility(chosenHardCounter), CreateAbility(secondary) };
            }
            else
            {
                abilities = new[] {
                    CreateAbility(Choose(random, catalog.kingDefensiveAbilities)),
                    CreateAbility(Choose(random, catalog.kingOffensiveAbilities)) };
            }
        }
        headliner.abilities = abilities;
        _generatedEnemyTypes.Add(headliner);
        return headliner;
    }

    static T Choose<T>(IRandomSource random, params T[] options) => options[random.NextInt(0, options.Length)];

    /// <summary>
    /// A cleared District restores part of the missing HP before the next one
    /// (<c>GameBalance.districtClear</c>, rounded half up). Not combat healing: it consumes no RNG.
    /// </summary>
    internal int HealAtDistrictClear()
    {
        var combat = CombatManager.Instance;
        if (combat == null || combat.player == null) return 0;
        long missing = Math.Max(0, combat.player.maxHealth - combat.player.currentHealth);
        int percent = GameBalance.Current.districtClear.healPercentOfMissing;
        int amount = (int)Math.Min(int.MaxValue, (missing * percent + 50) / 100);
        if (amount <= 0) return 0;
        int healed = combat.HealPlayer(amount);
        if (healed > 0) GameplayBalanceTelemetry.Record("district_clear_heal", "", healed);
        return healed;
    }

    EnemyAbility CreateAbility(EnemyAbilityEffect effect)
    {
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        ability.effect = effect;
        ability.id = effect.ToString();
        ability.displayName = Headliners.AbilityName(effect);
        ability.description = Headliners.AbilityDescription(effect);
        _generatedEnemyAbilities.Add(ability);
        return ability;
    }

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
