using System;
using UnityEngine;
using UnityEngine.EventSystems;

public readonly struct ContextualInfo
{
    public readonly string Category,Title,Body;
    public ContextualInfo(string category,string title,string body){Category=category;Title=title;Body=body;}
}

/// <summary>Data adapter for offers, routes, HUD, settings, and Tricks. Describing must be read-only.</summary>
public sealed class ContextualInfoTrigger : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler,IPointerClickHandler
{
    public string category,title;
    [TextArea] public string description;
    public bool pinOnClick,allowWhileBlocked;
    // Opens only when clicked (pinned), never on hover: Black Market offers show details on click.
    public bool clickOnly;
    public Func<ContextualInfo?> describe;
    bool _pointer,_focus;
    public bool IsInspected => _pointer||_focus;
    public ContextualInfo? Info => describe!=null?describe():string.IsNullOrEmpty(title)?null:new ContextualInfo(category,title,description);
    public void OnPointerEnter(PointerEventData _){_pointer=true;if(!clickOnly)ContextualTooltipUI.Instance?.Inspect(this);}
    public void OnPointerExit(PointerEventData _){_pointer=false;if(!_focus)ContextualTooltipUI.Instance?.Leave(this);}
    public void OnSelect(BaseEventData _){_focus=true;if(!clickOnly)ContextualTooltipUI.Instance?.Inspect(this);}
    public void OnDeselect(BaseEventData _){_focus=false;if(!_pointer)ContextualTooltipUI.Instance?.Leave(this);}
    public void OnPointerClick(PointerEventData e){if(pinOnClick&&e.button==PointerEventData.InputButton.Left)ContextualTooltipUI.Instance?.Pin(this);}
    void OnDisable(){_pointer=_focus=false;ContextualTooltipUI.Instance?.Leave(this);}
}
