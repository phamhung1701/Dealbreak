using System;
using System.Collections.Generic;
using UnityEngine;

// CardManager: hand views, selection, drag and targeting modes. Presentation state only;
// card zones and ownership stay in CardManager.cs and CardCollection.
public partial class CardManager
{
    readonly HashSet<CardView> _trackedViews = new();
    readonly List<CardView> _selectedCards = new();
    CardView _activeDragCard;
    Action<CardView> _handEnhancementTargetHandler;
    Func<int, bool> _handTargetEligibility;
    Action<EnemyRuntime> _enemyTargetHandler;
    Func<EnemyRuntime, bool> _enemyTargetEligibility;
    public bool IsHandEnhancementTargeting => _handEnhancementTargetHandler != null;
    public bool IsEnemyTargeting => _enemyTargetHandler != null;
    public event Action<bool> OnEnemyTargetingChanged;
    public IReadOnlyList<CardView> SelectedCards => _selectedCards;
    public CardView ActiveDragCard => _activeDragCard;
    public event Action<CardView> OnCardSelected;

    void RemoveTrackedViews(CardInstance card)
    {
        var views = new List<CardView>();
        foreach (var view in _trackedViews)
            if (view != null && ReferenceEquals(view.data, card)) views.Add(view);
        foreach (var view in views)
        {
            _selectedCards.Remove(view);
            view.SetSelectionOrder(0);
            DetachAndDestroyView(view);
        }
        RefreshSelectionPresentation();
    }

    public void BeginHandEnhancementTargeting(Action<CardView> onTarget) =>
        BeginHandTargeting(onTarget, null);

    public void BeginHandTargeting(Action<CardView> onTarget, Func<int, bool> isEligible)
    {
        EndEnemyTargeting();
        EndHandEnhancementTargeting();
        _handEnhancementTargetHandler = onTarget;
        _handTargetEligibility = isEligible;
        ClearSelection();
        CancelCardInteractions();
        RefreshHandTargetPresentation();
    }

    public void BeginEnemyTargeting(Action<EnemyRuntime> onTarget, Func<EnemyRuntime, bool> isEligible)
    {
        EndEnemyTargeting();
        if (IsHandEnhancementTargeting) EndHandEnhancementTargeting();
        if (onTarget == null) return;
        _enemyTargetHandler = onTarget;
        _enemyTargetEligibility = isEligible;
        if (_activeDragCard != null) _activeDragCard.CancelActiveDrag();
        _activeDragCard = null;
        OnEnemyTargetingChanged?.Invoke(true);
    }

    public bool HandleEnemyTargetClick(EnemyRuntime enemy)
    {
        if (!IsEnemyTargeting) return false;
        if (!GameplayInputGate.IsBlocked && enemy != null &&
            (_enemyTargetEligibility == null || _enemyTargetEligibility(enemy)))
            _enemyTargetHandler?.Invoke(enemy);
        return true;
    }

    public void EndEnemyTargeting()
    {
        if (!IsEnemyTargeting) return;
        _enemyTargetHandler = null;
        _enemyTargetEligibility = null;
        OnEnemyTargetingChanged?.Invoke(false);
    }

    public void EndHandEnhancementTargeting()
    {
        _handEnhancementTargetHandler = null;
        _handTargetEligibility = null;
        foreach (var view in _trackedViews)
        {
            if (view == null) continue;
            view.SetHandTargetState(false, false);
            view.SetSelected(false);
        }
        ClearSelection();
    }

    public void RefreshHandTargetPresentation()
    {
        foreach (var view in _trackedViews)
        {
            if (view == null) continue;
            bool isHand = view.data != null && _cards.ContainsInHand(view.data);
            bool targeting = IsHandEnhancementTargeting && isHand;
            bool eligible = targeting && (_handTargetEligibility == null || _handTargetEligibility(view.data.Id));
            view.SetHandTargetState(targeting, eligible);
        }
    }

    public bool CanRouteHandEnhancementTargetClick => IsHandEnhancementTargeting &&
        (!GameplayInputGate.IsBlocked || RunManager.Instance != null && RunManager.Instance.IsPendingHammerReward &&
            GameplayInputGate.Reasons == GameplayInputBlockReason.GearChoice);

    public void HandleCardClick(CardView card)
    {
        if (IsEnemyTargeting) return;
        if (IsHandEnhancementTargeting)
        {
            if (CanRouteHandEnhancementTargetClick && IsTrackedHandView(card) &&
                (_handTargetEligibility == null || _handTargetEligibility(card.data.Id)))
                _handEnhancementTargetHandler?.Invoke(card);
            return;
        }
        ToggleCardSelection(card);
    }

    bool IsTrackedHandView(CardView card) => card != null && card.data != null &&
        _trackedViews.Contains(card) && _cards.ContainsInHand(card.data);

    public void CancelCardInteractions()
    {
        EndEnemyTargeting();
        ClearSelection();
        if (_activeDragCard != null)
            _activeDragCard.CancelActiveDrag();
        _activeDragCard = null;

        foreach (var view in _trackedViews)
            if (view != null)
                view.ForceToIdle();
    }

    public void SelectCard(CardView card) => ToggleCardSelection(card);

    public void ToggleCardSelection(CardView card)
    {
        if (IsHandEnhancementTargeting)
        {
            if (CanRouteHandEnhancementTargetClick && IsTrackedHandView(card) &&
                (_handTargetEligibility == null || _handTargetEligibility(card.data.Id)))
                _handEnhancementTargetHandler?.Invoke(card);
            return;
        }
        if (!CanSelect(card)) return;

        if (_selectedCards.Remove(card))
        {
            card.SetSelectionOrder(0);
            RefreshSelectionPresentation();
            return;
        }

        var combat = CombatManager.Instance;
        if (combat == null || !combat.CanAddCardToSelection(_selectedCards, card))
            return;

        if (combat.ShouldReplaceSelectionOnAdd &&
            !combat.CanPairSelection(_selectedCards, card) &&
            !GameplayEffectResolver.CanContinueAttackSelection(_selectedCards, card, this))
            ClearSelection(false);

        _selectedCards.Add(card);
        RefreshSelectionPresentation();
    }

    public void SelectOnlyCard(CardView card)
    {
        if (!CanSelect(card)) return;
        if (_selectedCards.Count == 1 && _selectedCards[0] == card) return;

        var combat = CombatManager.Instance;
        if (combat == null || !combat.CanAddCardToSelection(System.Array.Empty<CardView>(), card))
            return;

        ClearSelection(false);
        _selectedCards.Add(card);
        RefreshSelectionPresentation();
    }

    public bool PrepareDragSelection(CardView card)
    {
        if (!CanSelect(card)) return false;
        if (_selectedCards.Contains(card)) return true;

        ToggleCardSelection(card);
        return _selectedCards.Contains(card);
    }

    public IReadOnlyList<CardView> GetSelectedCardsSnapshot()
    {
        return new List<CardView>(_selectedCards);
    }

    public void BeginCardDrag(CardView card)
    {
        if (card != null && _trackedViews.Contains(card))
            _activeDragCard = card;
    }

    public void EndCardDrag(CardView card)
    {
        if (_activeDragCard == card)
            _activeDragCard = null;
    }

    public void DeselectCard() => ClearSelection();
    public void ClearSelection() => ClearSelection(true);
    bool CanCreateViews() => handField != null && handField.cardsHolder != null && cardPrefab != null && dragCanvas != null;

    CardView CreateView(CardInstance cardInstance)
    {
        var view = Instantiate(cardPrefab);
        _trackedViews.Add(view);
        handField.AddCard(view, cardInstance);
        if (IsHandEnhancementTargeting)
            view.SetHandTargetState(true, _handTargetEligibility == null || _handTargetEligibility(cardInstance.Id));
        return view;
    }

    bool CanSelect(CardView card)
    {
        return !IsHandEnhancementTargeting && !IsEnemyTargeting && !GameplayInputGate.IsBlocked && card != null && card.data != null &&
            _trackedViews.Contains(card) && _cards.ContainsInHand(card.data);
    }

    void ClearSelection(bool notify)
    {
        for (int i = 0; i < _selectedCards.Count; i++)
            if (_selectedCards[i] != null)
                _selectedCards[i].SetSelectionOrder(0);

        _selectedCards.Clear();
        selectedCard = null;
        if (notify)
            OnCardSelected?.Invoke(null);
    }

    void RefreshSelectionPresentation()
    {
        for (int i = 0; i < _selectedCards.Count; i++)
            if (_selectedCards[i] != null)
                _selectedCards[i].SetSelectionOrder(i + 1);

        selectedCard = _selectedCards.Count > 0 ? _selectedCards[^1] : null;
        OnCardSelected?.Invoke(selectedCard);
    }

    void RefreshTrackedView(CardInstance card)
    {
        foreach (var view in _trackedViews)
        {
            if (view != null && ReferenceEquals(view.data, card))
            {
                view.Setup(card);
                return;
            }
        }
    }

    void DetachAndDestroyView(CardView view)
    {
        _trackedViews.Remove(view);
        if (view == null) return;

        view.homeField = null;
        view.transform.SetParent(null, false);
        view.gameObject.SetActive(false);
        DestroyRuntimeObject(view.gameObject);
    }
}
