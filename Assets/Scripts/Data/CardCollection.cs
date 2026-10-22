using System;
using System.Collections.Generic;

/// <summary>
/// Plain runtime model for the cards owned by one run and their current zones.
/// All zone mutations are centralized here so a card cannot exist in multiple zones.
/// </summary>
[Serializable]
public sealed class CardCollection
{
    readonly List<CardInstance> _ownedCards = new();
    readonly List<CardInstance> _deck = new();
    readonly List<CardInstance> _hand = new();
    readonly List<CardInstance> _discardPile = new();
    IRandomSource _random = new DeterministicRandom(RunRandomContext.StableHash("CARDPG_DEFAULT_CARDS"));

    public IReadOnlyList<CardInstance> OwnedCards => _ownedCards;
    public IReadOnlyList<CardInstance> Deck => _deck;
    public IReadOnlyList<CardInstance> Hand => _hand;
    public IReadOnlyList<CardInstance> DiscardPile => _discardPile;
    public int HandCount => _hand.Count;

    public void ConfigureRandom(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public void Initialize(IEnumerable<CardInstance> cards)
    {
        if (cards == null) throw new ArgumentNullException(nameof(cards));

        var validatedCards = new List<CardInstance>();
        var ids = new HashSet<int>();
        foreach (var card in cards)
        {
            if (card == null)
                throw new ArgumentException("Owned cards cannot contain null.", nameof(cards));
            if (!ids.Add(card.Id))
                throw new ArgumentException($"Duplicate card id {card.Id}.", nameof(cards));

            validatedCards.Add(card);
        }

        Clear();
        _ownedCards.AddRange(validatedCards);
        _deck.AddRange(validatedCards);
    }

    public uint CaptureRandomState() => _random is DeterministicRandom random ? random.State
        : throw new InvalidOperationException("Cannot persist an unsupported card random source.");

    public void RestoreZones(IReadOnlyList<CardInstance> owned, IReadOnlyList<int> deck,
        IReadOnlyList<int> hand, IReadOnlyList<int> discard, uint randomState)
    {
        var byId = new Dictionary<int, CardInstance>();
        foreach (var card in owned) byId.Add(card.Id, card);
        var seen = new HashSet<int>();
        foreach (var zone in new[] { deck, hand, discard })
            foreach (int id in zone)
                if (!byId.ContainsKey(id) || !seen.Add(id)) throw new ArgumentException("Invalid card zones.");
        if (seen.Count != owned.Count || randomState == 0) throw new ArgumentException("Incomplete card zones or RNG.");
        Clear();
        _ownedCards.AddRange(owned);
        foreach (int id in deck) _deck.Add(byId[id]);
        foreach (int id in hand) _hand.Add(byId[id]);
        foreach (int id in discard) _discardPile.Add(byId[id]);
        _random = DeterministicRandom.Restore(randomState);
    }

    public bool AddOwnedToDeck(CardInstance card) => AddOwned(card, _deck);

    public bool AddOwnedToHand(CardInstance card) => AddOwned(card, _hand);

    public bool AddOwnedToDiscard(CardInstance card) => AddOwned(card, _discardPile);

    bool AddOwned(CardInstance card, List<CardInstance> destination)
    {
        if (card == null) return false;

        foreach (var ownedCard in _ownedCards)
        {
            if (ReferenceEquals(ownedCard, card) || ownedCard.Id == card.Id)
                return false;
        }

        if (_deck.Contains(card) || _hand.Contains(card) || _discardPile.Contains(card))
            return false;

        _ownedCards.Add(card);
        destination.Add(card);
        return true;
    }

    public bool TryRemoveOwnedCards(IReadOnlyList<CardInstance> cards)
    {
        if (cards == null || cards.Count == 0 || cards.Count > _ownedCards.Count) return false;
        var seen = new HashSet<CardInstance>();
        foreach (var card in cards)
            if (card == null || !seen.Add(card) || !_ownedCards.Contains(card)) return false;
        foreach (var card in cards)
        {
            _ownedCards.Remove(card);
            _deck.Remove(card);
            _hand.Remove(card);
            _discardPile.Remove(card);
        }
        return true;
    }

    public bool RemoveOwnedCard(CardInstance card)
    {
        if (card == null || !_ownedCards.Remove(card)) return false;
        _deck.Remove(card);
        _hand.Remove(card);
        _discardPile.Remove(card);
        return true;
    }

    public void Clear()
    {
        _ownedCards.Clear();
        _deck.Clear();
        _hand.Clear();
        _discardPile.Clear();
    }

    public void ShuffleDeck() => _deck.Shuffle(_random);

    public int DrawToHand(int requestedCount, int handCapacity)
    {
        if (requestedCount <= 0 || handCapacity <= _hand.Count) return 0;

        int targetCount = Math.Min(requestedCount, handCapacity - _hand.Count);
        int drawn = 0;
        while (drawn < targetCount)
        {
            if (_deck.Count == 0)
                RecycleDiscard();
            if (_deck.Count == 0)
                break;

            var card = _deck[^1];
            _deck.RemoveAt(_deck.Count - 1);
            _hand.Add(card);
            drawn++;
        }
        return drawn;
    }

    // Strict draws never recycle discards or use overflow capacity. An eligibility snapshot
    // lets Perfect Parry exclude cards returned to the deck during the defense window.
    public int DrawExistingToHand(int requestedCount, int handCapacity, ISet<int> eligibleIds = null)
    {
        int drawn = 0;
        for (int i = _deck.Count - 1; i >= 0 && drawn < requestedCount && _hand.Count < handCapacity; i--)
        {
            var card = _deck[i];
            if (eligibleIds != null && !eligibleIds.Contains(card.Id)) continue;
            _deck.RemoveAt(i);
            _hand.Add(card);
            drawn++;
        }
        return drawn;
    }

    public bool ContainsInHand(CardInstance card) => card != null && _hand.Contains(card);

    public bool TryDiscard(CardInstance card)
    {
        if (card == null) return false;
        return TryDiscard(new[] { card });
    }

    public bool TryDiscard(IReadOnlyList<CardInstance> cards) => TryMoveHandCards(cards, _discardPile);

    /// <summary>Returns played hand cards straight to the draw deck, then shuffles once using the run's card stream.</summary>
    public bool TryRecycleHandToDeck(IReadOnlyList<CardInstance> cards)
    {
        if (!TryMoveHandCards(cards, _deck)) return false;
        ShuffleDeck();
        return true;
    }

    bool TryMoveHandCards(IReadOnlyList<CardInstance> cards, List<CardInstance> destination)
    {
        if (cards == null || cards.Count == 0) return false;
        var snapshot = new List<CardInstance>(cards.Count);
        var uniqueCards = new HashSet<CardInstance>();
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null || !uniqueCards.Add(card) || !_hand.Contains(card))
                return false;
            snapshot.Add(card);
        }
        // The caller may supply Hand itself. Snapshot before removing anything.
        foreach (var card in snapshot)
        {
            _hand.Remove(card);
            destination.Add(card);
        }
        return true;
    }

    /// <summary>Moves one exact card from the draw deck or the discard pile into the hand.</summary>
    public bool TryTakeToHand(CardInstance card, int handCapacity)
    {
        if (card == null || _hand.Count >= handCapacity || !_deck.Remove(card) && !_discardPile.Remove(card)) return false;
        _hand.Add(card);
        return true;
    }

    public int ReturnRandomDiscardToDeck(int requestedCount)
    {
        if (requestedCount <= 0 || _discardPile.Count == 0) return 0;

        _discardPile.Shuffle(_random);
        int moved = Math.Min(requestedCount, _discardPile.Count);
        for (int i = 0; i < moved; i++)
        {
            int lastIndex = _discardPile.Count - 1;
            _deck.Add(_discardPile[lastIndex]);
            _discardPile.RemoveAt(lastIndex);
        }
        return moved;
    }

    void RecycleDiscard()
    {
        if (_discardPile.Count == 0) return;
        _deck.AddRange(_discardPile);
        _discardPile.Clear();
        ShuffleDeck();
    }
}
