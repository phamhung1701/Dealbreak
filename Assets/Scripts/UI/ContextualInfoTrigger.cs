using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public readonly struct ContextualInfo
{
    public readonly string Category,Title,Body;
    public ContextualInfo(string category,string title,string body){Category=category;Title=title;Body=body;}
}

/// <summary>
/// Data adapter for offers, routes, HUD, settings, and Tricks. Describing must be read-only.
/// Details open on click (or keyboard focus), never on hover; a second click closes them.
/// </summary>
public sealed class ContextualInfoTrigger : MonoBehaviour,ISelectHandler,IDeselectHandler,IPointerClickHandler
{
    public string category,title;
    [TextArea] public string description;
    // Set on a button whose click is meant to show its detail (Black Market tiles, deck cards). Other
    // buttons act on click and show their detail on click only while unavailable.
    public bool pinOnClick;
    public bool allowWhileBlocked;
    public Func<ContextualInfo?> describe;
    bool _focus;
    public bool IsInspected => _focus;
    public ContextualInfo? Info => describe!=null?describe():string.IsNullOrEmpty(title)?null:new ContextualInfo(category,title,description);
    bool ClickShowsDetail => pinOnClick||!TryGetComponent<Selectable>(out var selectable)||!selectable.IsInteractable();
    // A click selects a button too; only keyboard or gamepad focus inspects, the click pins instead.
    public void OnSelect(BaseEventData e){if(e is PointerEventData)return;_focus=true;ContextualTooltipUI.Instance?.Inspect(this);}
    public void OnDeselect(BaseEventData _){if(!_focus)return;_focus=false;ContextualTooltipUI.Instance?.Leave(this);}
    public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&ClickShowsDetail)ContextualTooltipUI.Instance?.Pin(this);}
    void OnDisable(){_focus=false;ContextualTooltipUI.Instance?.Leave(this);}
}
