using System.Collections.Generic;

public static class AttackSelectionRules
{
    static readonly CardData.Rank[] RoyalRanks =
    {
        CardData.Rank.Ten,
        CardData.Rank.Jack,
        CardData.Rank.Queen,
        CardData.Rank.King,
        CardData.Rank.Ace
    };

    public static int RoyalFlushSize => RoyalRanks.Length;

    public static bool IsRoyalFamilyPartial(IReadOnlyList<CardInstance> cards)
    {
        if (cards == null || cards.Count == 0 || cards.Count > RoyalRanks.Length) return false;
        var ranks = new HashSet<CardData.Rank>();
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null || !IsRoyalRank(card.Rank) || !ranks.Add(card.Rank)) return false;
        }

        foreach (CardData.Suit suit in System.Enum.GetValues(typeof(CardData.Suit)))
        {
            bool allMatch = true;
            for (int i = 0; i < cards.Count; i++)
                if (!cards[i].MatchesSuit(suit)) { allMatch = false; break; }
            if (allMatch) return true;
        }
        return false;
    }

    public static bool IsRoyalFamily(IReadOnlyList<CardInstance> cards)
    {
        if (cards == null || cards.Count != RoyalRanks.Length || !IsRoyalFamilyPartial(cards)) return false;
        var ranks = new HashSet<CardData.Rank>();
        for (int i = 0; i < cards.Count; i++) ranks.Add(cards[i].Rank);
        return ranks.Count == RoyalRanks.Length;
    }

    static bool IsRoyalRank(CardData.Rank rank)
    {
        for (int i = 0; i < RoyalRanks.Length; i++)
            if (RoyalRanks[i] == rank) return true;
        return false;
    }
}
