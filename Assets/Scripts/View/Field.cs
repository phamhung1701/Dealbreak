using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation order and layout targets only; CardCollection owns every card zone.</summary>
public class Field : MonoBehaviour
{
    [Header("References")]
    public RectTransform cardsHolder;

    [Header("Hand Motion")]
    [Min(0f)] public float reorderThreshold = 18f;
    [Tooltip("Seconds between cards dealt in the same frame.")]
    [Min(0f)] public float dealStagger = 0.06f;

    [Header("Fan Layout")]
    public float maximumSpacing = -18f;
    [Min(0f)] public float minimumVisibleStep = 28f;
    [Min(0f)] public float fanRise = 14f;
    [Range(0f, 20f)] public float fanAngle = 8f;

    // Explicit view order, never reconstructed from rendering/sibling order.
    readonly List<CardView> _orderedCards = new();
    HorizontalLayoutGroup _layoutGroup;
    GameObject _dragPlaceholder;
    CardView _draggedCard;
    int _dragOriginalIndex;
    bool _dragInsideHand;
    Vector2 _lastHolderSize;
    int _lastChildCount = -1;
    int _dealFrame = -1;
    int _dealCount;

    public int CardCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _orderedCards.Count; i++)
                if (_orderedCards[i] != null && _orderedCards[i].homeField == this) count++;
            return count;
        }
    }

    public GameObject BeginVisualDrag(CardView card)
    {
        if (!CanReorder(card) || _draggedCard != null) return null;
        _draggedCard = card;
        _dragOriginalIndex = _orderedCards.IndexOf(card);
        _dragInsideHand = true;
        // A marker retains the drag slot identity, but never drives layout or ownership.
        _dragPlaceholder = new GameObject($"{card.name} Drag Slot", typeof(RectTransform));
        _dragPlaceholder.transform.SetParent(cardsHolder, false);
        Reflow();
        return _dragPlaceholder;
    }

    public void RefreshLayout() => Reflow();

    public void UpdateVisualDrag(CardView card, Vector2 screenPosition, Camera eventCamera)
    {
        if (card != _draggedCard || cardsHolder == null) return;
        bool inside = ContainsScreenPoint(screenPosition, eventCamera);
        if (_dragInsideHand != inside)
        {
            _dragInsideHand = inside;
            Reflow();
        }
        if (!inside || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cardsHolder, screenPosition, eventCamera, out var pointer)) return;

        int slot = _orderedCards.IndexOf(card);
        int destination = slot;
        // Use stable targets rather than moving visuals, and canvas-local hysteresis.
        if (slot > 0 && pointer.x < _orderedCards[slot - 1].LayoutTargetPosition.x - reorderThreshold)
            destination--;
        else if (slot < _orderedCards.Count - 1 &&
                 pointer.x > _orderedCards[slot + 1].LayoutTargetPosition.x + reorderThreshold)
            destination++;
        if (destination == slot) return;
        _orderedCards.RemoveAt(slot);
        _orderedCards.Insert(destination, card);
        Reflow();
    }

    public bool ContainsScreenPoint(Vector2 screenPosition, Camera eventCamera) =>
        cardsHolder != null && RectTransformUtility.RectangleContainsScreenPoint(cardsHolder, screenPosition, eventCamera);

    public void EndVisualDrag(CardView card, bool commitReorder)
    {
        if (card != _draggedCard) return;
        if (card != null && card.homeField == this && cardsHolder != null)
        {
            if (!commitReorder)
            {
                _orderedCards.Remove(card);
                _orderedCards.Insert(Mathf.Clamp(_dragOriginalIndex, 0, _orderedCards.Count), card);
            }
            // Keep the release pose. CardView follows the freshly calculated return target.
            card.transform.SetParent(cardsHolder, true);
        }
        DestroyPlaceholder();
        _draggedCard = null;
        Reflow();
    }

    public void RemoveVisualDragSlot(CardView card)
    {
        if (card != _draggedCard) return;
        _orderedCards.Remove(card);
        DestroyPlaceholder();
        _draggedCard = null;
        Reflow();
    }

    public void ReturnCardToStart(CardView card)
    {
        if (!CanReorder(card)) return;
        card.transform.SetParent(cardsHolder, true);
        Reflow();
    }

    public void SetNewCard(CardView card, CardView left, CardView right) => RepositionCard(card, left, right);

    public void RepositionCard(CardView card, CardView left, CardView right)
    {
        if (!CanReorder(card)) return;
        _orderedCards.Remove(card);
        int leftIndex = left != null ? _orderedCards.IndexOf(left) : -1;
        int rightIndex = right != null ? _orderedCards.IndexOf(right) : -1;
        int target = leftIndex >= 0 && rightIndex >= 0 ? (leftIndex + rightIndex) / 2 + 1
            : leftIndex >= 0 ? leftIndex + 1 : rightIndex >= 0 ? rightIndex : _orderedCards.Count;
        _orderedCards.Insert(Mathf.Clamp(target, 0, _orderedCards.Count), card);
        card.transform.SetParent(cardsHolder, true);
        Reflow();
    }

    public void AddCard(CardView card, CardInstance data)
    {
        if (card == null || cardsHolder == null) return;
        card.transform.SetParent(cardsHolder, false);
        card.homeField = this;
        if (!_orderedCards.Contains(card)) _orderedCards.Add(card);
        card.Setup(data);
        Reflow();
        // Short, local entry from the draw-pile side; no dependency on gameplay timing. Cards drawn
        // together are dealt one after another (presentation only, capped so big draws stay quick).
        float delay = 0f;
        if (Application.isPlaying)
        {
            if (_dealFrame != Time.frameCount) { _dealFrame = Time.frameCount; _dealCount = 0; }
            delay = Mathf.Min(_dealCount++, 7) * dealStagger;
        }
        card.BeginHandEntry(new Vector3(cardsHolder.rect.xMax + 35f,
            cardsHolder.rect.center.y - cardsHolder.rect.height * 0.12f, 0f), delay);
    }

    public void ClearCards()
    {
        if (cardsHolder == null) return;
        _draggedCard?.CancelActiveDrag();
        for (int i = _orderedCards.Count - 1; i >= 0; i--)
        {
            var card = _orderedCards[i];
            if (card == null) continue;
            card.homeField = null;
            card.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(card.gameObject);
            else DestroyImmediate(card.gameObject);
        }
        _orderedCards.Clear();
        DestroyPlaceholder();
        _draggedCard = null;
        Reflow();
    }

    public void SortByRank() => SortHand(true);
    public void SortBySuit() => SortHand(false);

    void SortHand(bool rankFirst)
    {
        if (cardsHolder == null || _draggedCard != null) return;
        PruneDepartedCards();
        // Stable insertion sort: tiny hand, no allocation, identical rank/suit keep their order.
        for (int i = 1; i < _orderedCards.Count; i++)
        {
            var card = _orderedCards[i];
            int j = i - 1;
            while (j >= 0 && CompareCards(_orderedCards[j], card, rankFirst) > 0)
            {
                _orderedCards[j + 1] = _orderedCards[j];
                j--;
            }
            _orderedCards[j + 1] = card;
        }
        Reflow();
    }

    static int CompareCards(CardView a, CardView b, bool rankFirst)
    {
        if (a.data == null || b.data == null) return 0;
        int primary = rankFirst ? a.data.Rank.CompareTo(b.data.Rank) : a.data.Suit.CompareTo(b.data.Suit);
        return primary != 0 ? primary : rankFirst ? a.data.Suit.CompareTo(b.data.Suit) : a.data.Rank.CompareTo(b.data.Rank);
    }

    void LateUpdate()
    {
        if (cardsHolder == null) return;
        if (_lastChildCount != cardsHolder.childCount ||
            (_lastHolderSize - cardsHolder.rect.size).sqrMagnitude > 0.01f)
            Reflow();
    }

    void PruneDepartedCards()
    {
        for (int i = _orderedCards.Count - 1; i >= 0; i--)
            if (_orderedCards[i] == null || _orderedCards[i].homeField != this)
                _orderedCards.RemoveAt(i);
    }

    void Reflow()
    {
        if (cardsHolder == null) return;
        PruneDepartedCards();
        if (_layoutGroup == null) _layoutGroup = cardsHolder.GetComponent<HorizontalLayoutGroup>();
        if (_layoutGroup != null) _layoutGroup.enabled = false;
        int count = _orderedCards.Count - (_draggedCard != null && !_dragInsideHand ? 1 : 0);
        float cardWidth = 1f, cardHeight = 1f;
        for (int i = 0; i < _orderedCards.Count; i++)
        {
            var rect = (RectTransform)_orderedCards[i].transform;
            cardWidth = Mathf.Max(cardWidth, rect.rect.width);
            cardHeight = Mathf.Max(cardHeight, rect.rect.height);
        }
        float leftPadding = _layoutGroup != null ? _layoutGroup.padding.left : 0f;
        float rightPadding = _layoutGroup != null ? _layoutGroup.padding.right : 0f;
        float usableWidth = Mathf.Max(1f, cardsHolder.rect.width - leftPadding - rightPadding);
        // Reserve a little room for fan rotation/selection. Very large hands scale gently to fit.
        float envelopeWidth = cardWidth + cardHeight * Mathf.Sin(fanAngle * Mathf.Deg2Rad) + 12f;
        float minimumSpan = envelopeWidth + Mathf.Max(0, count - 1) * minimumVisibleStep;
        float scale = Mathf.Min(1f, usableWidth / Mathf.Max(1f, minimumSpan),
            Mathf.Max(1f, cardsHolder.rect.height - fanRise) / cardHeight);
        float maxStep = Mathf.Max(minimumVisibleStep, cardWidth + maximumSpacing);
        float step = count > 1 ? Mathf.Min(maxStep * scale,
            Mathf.Max(0f, usableWidth - envelopeWidth * scale) / (count - 1)) : 0f;
        float centerX = cardsHolder.rect.center.x + (leftPadding - rightPadding) * 0.5f;
        int occupiedIndex = 0;
        int siblingIndex = 0;
        for (int i = 0; i < _orderedCards.Count; i++)
        {
            var card = _orderedCards[i];
            card.index = i;
            bool dragged = card == _draggedCard;
            if (dragged && !_dragInsideHand) continue;
            float t = count > 1 ? 2f * occupiedIndex / (count - 1) - 1f : 0f;
            var position = new Vector3(centerX + (occupiedIndex - (count - 1) * 0.5f) * step,
                cardsHolder.rect.center.y, 0f);
            card.SetLayoutTarget(position, new Vector2(0f, fanRise * (1f - t * t)), -fanAngle * t, scale);
            if (dragged)
            {
                if (_dragPlaceholder != null)
                {
                    _dragPlaceholder.transform.SetSiblingIndex(siblingIndex++);
                    _dragPlaceholder.transform.localPosition = position;
                }
            }
            else if (card.transform.parent == cardsHolder)
                card.transform.SetSiblingIndex(siblingIndex++);
            occupiedIndex++;
        }
        _lastHolderSize = cardsHolder.rect.size;
        _lastChildCount = cardsHolder.childCount;
    }

    void DestroyPlaceholder()
    {
        if (_dragPlaceholder == null) return;
        var placeholder = _dragPlaceholder;
        _dragPlaceholder = null;
        placeholder.transform.SetParent(null, false);
        placeholder.SetActive(false);
        if (Application.isPlaying) Destroy(placeholder);
        else DestroyImmediate(placeholder);
    }

    bool CanReorder(CardView card) => card != null && cardsHolder != null &&
        card.homeField == this && _orderedCards.Contains(card);
}
