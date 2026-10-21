using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Inspect/focus adapter; all commit input still routes through the existing handlers. Details open
/// on click or keyboard focus, never on hover: a selected card or Gear shows its own, an enemy pins
/// its detail from its target click, and a hand card outside a fight pins it here.
/// </summary>
public sealed class CombatInspectUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    bool _pointer, _focus;
    /// <summary>The pointer is over the object; presentation only (an enemy's outline), never its detail.</summary>
    public bool IsHovered => _pointer;
    public bool IsInspected => _focus;
    public void OnPointerEnter(PointerEventData _) => _pointer = true;
    public void OnPointerExit(PointerEventData _) => _pointer = false;
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !TryGetComponent<CardView>(out var card)) return;
        var cards = CardManager.Instance;
        // In a fight a click selects the card, and the selection shows its detail.
        if (card.homeField != null && !DealbreakUIRuntime.CombatActive && cards?.CanRouteHandEnhancementTargetClick != true) Pin();
    }
    public void Pin() => ContextualTooltipUI.Instance?.Pin(this);
    // A click selects a Selectable too; only keyboard or gamepad focus inspects.
    public void OnSelect(BaseEventData eventData)
    {
        if (eventData is PointerEventData) return;
        _focus = true;
        if (TryGetComponent<CardView>(out var card) && !GameplayInputGate.IsBlocked) card.SetInspectFocus(true);
        Inspect();
    }
    public void OnDeselect(BaseEventData _)
    {
        if (!_focus) return;
        _focus = false;
        if (TryGetComponent<CardView>(out var card)) card.SetInspectFocus(false);
        Leave();
    }
    void OnDisable()
    {
        _pointer = _focus = false;
        if (TryGetComponent<CardView>(out var card)) card.SetInspectFocus(false);
        Leave();
    }
    void Inspect(){if(ContextualTooltipUI.Instance)ContextualTooltipUI.Instance.Inspect(this);else CombatDetailPanelUI.Instance?.Inspect(this);}
    void Leave(){if(ContextualTooltipUI.Instance)ContextualTooltipUI.Instance.Leave(this);else CombatDetailPanelUI.Instance?.Leave(this);}
    public void OnSubmit(BaseEventData eventData)
    {
        if (EventSystem.current == null) return;
        var selectable=GetComponent<UnityEngine.UI.Selectable>();
        if(selectable&&!selectable.IsInteractable())return;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        if (TryGetComponent<CardView>(out var card))
        {
            if(card.isActiveAndEnabled)card.OnPointerClick(pointer);
            else
            {
                var choice=GetComponentInParent<UnityEngine.UI.Button>();
                if(choice&&!GameplayInputGate.IsBlocked&&choice.IsInteractable())choice.onClick.Invoke();
                else ContextualTooltipUI.Instance?.Pin(this);
            }
            return;
        }
        if(GameplayInputGate.IsBlocked)return;
        if (TryGetComponent<CardActionDropTarget>(out var target)) target.OnPointerClick(pointer);
        else if (TryGetComponent<GearIconSlotUI>(out var gear)) gear.OnPointerClick(pointer);
    }
}
