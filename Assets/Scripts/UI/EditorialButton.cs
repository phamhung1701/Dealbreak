using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Brief visual feedback only; never adds or replaces command listeners.</summary>
public sealed class EditorialButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    // Paper: cream stock with ink type (tape, signs). Stamp: an ink box with a paper double rule.
    public enum Role { Primary, Secondary, Destructive, Quiet, Paper, Stamp }
    public Role role;
    public EditorialFrame frame;
    public Button button;
    public bool selected;
    public bool emphasized;
    bool _hover,_pressed,_focus;
    TMPro.TMP_Text[] _labels;
    bool? _lastAvailable;
    void LateUpdate()
    {
        if(!Refresh())return;
        var rect=frame.rectTransform;
        bool available=_lastAvailable==true;
        var target=new Vector2(0,_pressed&&available?-2:available&&(_hover||_focus)?2:0);
        rect.anchoredPosition=Vector2.Lerp(rect.anchoredPosition,target,1-Mathf.Exp(-Time.unscaledDeltaTime*24));
    }
    // Fill, rim and label colors for the current state. Styling calls it at once, so a button built
    // mid-frame is never drawn for a frame with its default dark fill.
    public bool Refresh()
    {
        var theme=DealbreakUIStyle.Theme;
        if (!button || !frame || !theme) return false;
        bool available=button.IsInteractable();
        if(_lastAvailable!=available)
        {
            _labels=GetComponentsInChildren<TMPro.TMP_Text>(true);
            _lastAvailable=available;
        }
        bool open=GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Open;
        bool paper=GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Paper||role==Role.Paper;
        Color labelColor=paper?theme.ink:open&&available&&(_hover||_focus||selected)?theme.ochre:!open&&available&&role==Role.Primary?theme.ink:theme.paper;
        if(open&&!available)labelColor=Color.Lerp(theme.paper,theme.slate,.7f);
        foreach(var label in _labels)if(label)label.color=labelColor;
        if(button.targetGraphic)button.targetGraphic.color=Color.clear;
        Color fill=role==Role.Primary?theme.ochre:role==Role.Destructive?theme.brick:role is Role.Quiet or Role.Stamp?theme.charcoal:theme.slate;
        if(paper)fill=theme.paper;
        if(role==Role.Stamp)fill=theme.ink;
        if (!available) fill=Color.Lerp(fill,paper?theme.slate:theme.charcoal,paper?.2f:.68f);
        else if (_hover||_focus||selected) fill=Color.Lerp(fill,emphasized?theme.ochre:theme.paper,emphasized?.24f:.13f);
        if (_pressed&&available) fill=Color.Lerp(fill,emphasized?theme.brick:theme.ink,emphasized?.36f:.2f);
        Color rim=_pressed&&available&&emphasized?theme.brick:selected||available&&(_hover||_focus)?theme.paper:emphasized?theme.ochre:theme.slate;
        // A stamp's paper rule is always printed; it brightens to ochre under the pointer.
        if(role==Role.Stamp)rim=available&&(_hover||_focus||selected)?theme.ochre:theme.paper;
        float border=emphasized?Mathf.Max(6f,frame.border):role==Role.Stamp?Mathf.Max(8f,frame.border):frame.border;
        if(fill!=frame.fill||rim!=frame.rim||border!=frame.border){frame.border=border;frame.fill=fill;frame.rim=rim;frame.Refresh();}
        return true;
    }
    public void OnPointerEnter(PointerEventData _) => _hover=true;
    public void OnPointerExit(PointerEventData _) { _hover=false;_pressed=false; }
    public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left)_pressed=true; }
    public void OnPointerUp(PointerEventData _) => _pressed=false;
    public void OnSelect(BaseEventData _) => _focus=true;
    public void OnDeselect(BaseEventData _) { _focus=false;_pressed=false; }
    void OnDisable() { _hover=_pressed=_focus=false; }
}
