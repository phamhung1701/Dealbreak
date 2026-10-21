using System;
using UnityEngine;

/// <summary>
/// The scripted tutorial run: one District whose route is these fights in order, then a Black
/// Market, then a Jack Headliner. Each fight sets the exact hand, the enemies, the Tricks handed
/// out as it starts and the Trick its enemies drop. The lesson flow lives in TutorialScript.
/// </summary>
[CreateAssetMenu(fileName = "TutorialRun", menuName = "Dealbreak/Tutorial Run")]
public sealed class TutorialRunDefinition : ScriptableObject
{
    [Serializable]
    public struct CardSpec
    {
        public CardData.Suit suit;
        public CardData.Rank rank;

        public CardSpec(CardData.Suit suit, CardData.Rank rank) { this.suit = suit; this.rank = rank; }
        public bool Matches(CardInstance card) => card != null && card.Suit == suit && card.Rank == rank;
    }

    [Serializable]
    public struct EnemySpec
    {
        public string enemyName;
        [Min(1)] public int hp;
        [Min(0)] public int attack;
        [Min(0)] public int credits;
    }

    [Serializable]
    public sealed class Fight
    {
        public EnemySpec[] enemies = Array.Empty<EnemySpec>();
        [Tooltip("The exact hand when the fight starts; the rest of the deck is the draw pile.")]
        public CardSpec[] hand = Array.Empty<CardSpec>();
        [Tooltip("Tricks put into the backpack as the fight starts.")]
        public ConsumableData[] grants = Array.Empty<ConsumableData>();
        [Tooltip("The Trick this fight's enemies drop when it is won.")]
        public ConsumableData drop;
    }

    public string seed = "TUTORIAL";
    public Fight[] fights = Array.Empty<Fight>();

    [Header("Black Market")]
    public GearData marketGear;
    public ConsumableData marketTrick;

    [Header("Headliner")]
    public CardData.Suit headlinerSuit = CardData.Suit.Spades;
    [Min(0)] public int headlinerAttack = 4;
    public CardSpec[] headlinerHand = Array.Empty<CardSpec>();

    public static TutorialRunDefinition Load() => Resources.Load<TutorialRunDefinition>("TutorialRun");

    public bool IsValid(out string error)
    {
        error = null;
        // TutorialScript teaches attack and blocking in the first fight and Tricks in the second.
        if (fights == null || fights.Length < 2) error = "the tutorial needs at least two fights";
        else if (Array.Exists(fights, fight => fight == null || fight.enemies == null || fight.enemies.Length == 0))
            error = "every tutorial fight needs an enemy";
        else if (marketGear == null || marketTrick == null) error = "the tutorial Black Market needs a Gear and a Trick";
        return error == null;
    }
}
