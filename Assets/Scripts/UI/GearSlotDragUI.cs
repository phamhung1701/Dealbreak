using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Pointer drag for an owned Gear slot. Dropped on a matching copy it merges, on the Black Market it
/// sells, anywhere else it returns to its slot (see <see cref="ItemDragUI"/>).
/// </summary>
[RequireComponent(typeof(GearIconSlotUI))]
public sealed class GearSlotDragUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    GearIconSlotUI _slot;
    bool _dragging;

    void Awake() => _slot = GetComponent<GearIconSlotUI>();

    void OnDisable()
    {
        if (_dragging) ItemDragUI.Get()?.Cancel();
        _dragging = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !_slot || !_slot.Gear) return;
        var drag = ItemDragUI.Get();
        _dragging = drag != null && drag.BeginGear(_slot.InstanceId, (RectTransform)transform, eventData);
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
    }
}
