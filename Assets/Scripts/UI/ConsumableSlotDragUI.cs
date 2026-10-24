using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Pointer drag for a Trick slot: the Trick is dragged like a card onto a hand card, an enemy,
/// the HP bar, the Black Market (to sell) or another Trick slot (see <see cref="ItemDragUI"/>).
/// </summary>
public sealed class ConsumableSlotDragUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    int _slotIndex;
    bool _dragging;
    bool _suppressNextClick;

    public void Configure(ActionButtonsUI owner, int slotIndex) => _slotIndex = slotIndex;

    public bool ConsumeClickSuppression()
    {
        bool suppress = _suppressNextClick;
        _suppressNextClick = false;
        return suppress;
    }

    void OnDisable()
    {
        if (_dragging) ItemDragUI.Get()?.Cancel();
        _dragging = false;
        _suppressNextClick = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        var drag = ItemDragUI.Get();
        _dragging = drag != null && drag.BeginTrick(_slotIndex, (RectTransform)transform, eventData);
        if (_dragging) _suppressNextClick = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragging) ItemDragUI.Get()?.Drag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;
        ItemDragUI.Get()?.End(eventData);
        StartCoroutine(ClearClickSuppressionNextFrame());
    }

    System.Collections.IEnumerator ClearClickSuppressionNextFrame()
    {
        yield return null;
        _suppressNextClick = false;
    }
}
