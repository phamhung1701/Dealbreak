using UnityEngine;

[CreateAssetMenu(fileName = "Card", menuName = "Game/Card Data")]
public class CardData : ScriptableObject
{
    public enum Suit { Hearts, Diamonds, Clubs, Spades }
    public enum Rank { Ace, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King }

    public Suit suit;
    public Rank rank;

    public string SuitSymbol => suit switch
    {
        Suit.Hearts   => "\u2665",
        Suit.Diamonds => "\u2666",
        Suit.Clubs    => "\u2663",
        Suit.Spades   => "\u2660",
        _ => ""
    };

    public string SuitName => suit switch
    {
        Suit.Hearts   => "Hearts",
        Suit.Diamonds => "Diamonds",
        Suit.Clubs    => "Clubs",
        Suit.Spades   => "Spades",
        _ => ""
    };

    public string RankLabel => rank switch
    {
        Rank.Ace   => "A",
        Rank.Two   => "2",
        Rank.Three => "3",
        Rank.Four  => "4",
        Rank.Five  => "5",
        Rank.Six   => "6",
        Rank.Seven => "7",
        Rank.Eight => "8",
        Rank.Nine  => "9",
        Rank.Ten   => "10",
        Rank.Jack  => "J",
        Rank.Queen => "Q",
        Rank.King  => "K",
        _ => ""
    };

    public string RankName => rank switch
    {
        Rank.Jack  => "Jack",
        Rank.Queen => "Queen",
        Rank.King  => "King",
        _          => ""
    };

    public string DisplayName => RankLabel + SuitSymbol;

    public int AttackValue => rank switch
    {
        Rank.Ace   => 1,
        Rank.Two   => 2,
        Rank.Three => 3,
        Rank.Four  => 4,
        Rank.Five  => 5,
        Rank.Six   => 6,
        Rank.Seven => 7,
        Rank.Eight => 8,
        Rank.Nine  => 9,
        Rank.Ten   => 10,
        Rank.Jack  => 10,
        Rank.Queen => 15,
        Rank.King  => 20,
        _          => 0
    };

    public bool IsFaceCard => rank == Rank.Jack || rank == Rank.Queen || rank == Rank.King;

    public Color SuitColor => suit switch
    {
        Suit.Hearts   => new Color(0.8f, 0.1f, 0.1f),
        Suit.Diamonds => new Color(0.1f, 0.4f, 0.9f),
        Suit.Clubs    => new Color(0.1f, 0.6f, 0.2f),
        Suit.Spades   => new Color(0.2f, 0.2f, 0.2f),
        _             => Color.white
    };

    public static CardData Create(Suit suit, Rank rank)
    {
        var card = CreateInstance<CardData>();
        card.suit = suit;
        card.rank = rank;
        return card;
    }
}
