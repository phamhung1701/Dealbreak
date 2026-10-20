using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Brief visual feedback only; never adds or replaces command listeners.</summary>
public sealed class EditorialButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    // Weight, not shape, tells buttons apart. Primary: solid signal red. Secondary: charcoal with a
    // hairline. Destructive: a red outline. Quiet: bare text. Paper: solid off-white with ink type.
    // Outline: a 1px off-white rule. Pointer, focus and selection add a 2px acid-lime rule.
    public enum Role { Primary, Secondary, Destructive, Quiet, Paper, Outline }
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
    // Fill, rule and label colors for the current state. Styling calls it at once, so a button built
    // mid-frame is never drawn for a frame with its default fill.
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
        bool paper=role==Role.Paper;
        bool active=available&&(_hover||_focus||selected);
        var clear=new Color(0,0,0,0);
        Color fill=role switch
        {
            Role.Primary=>theme.brick,
            Role.Paper=>theme.paper,
            Role.Quiet or Role.Outline or Role.Destructive=>clear,
            _=>theme.charcoal
        };
        Color rim=role switch
        {
            Role.Outline=>theme.paper,
            Role.Destructive=>theme.brick,
            Role.Secondary=>theme.slate,
            _=>fill
        };
        float border=role is Role.Primary or Role.Paper or Role.Quiet?0:1;
        // Paper-treatment buttons (Incident and reward choices) are raised content blocks.
        if(!paper&&GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Paper)
        {fill=theme.charcoal;rim=theme.slate;border=1;}
        Color labelColor=paper?theme.ink:role==Role.Destructive?theme.brick:theme.paper;
        if(open||role==Role.Quiet)labelColor=active?theme.ochre:theme.paper;
        if(!available)
        {
            fill=fill.a>0?theme.charcoal:clear;
            rim=border>0?theme.slate:fill;
            labelColor=Color.Lerp(theme.paper,theme.slate,.65f);
        }
        else if(active&&!open&&role!=Role.Quiet){rim=theme.ochre;border=2;}
        if(_pressed&&available&&fill.a>0)fill=Color.Lerp(fill,theme.ink,.18f);
        // Emphasis is weight, not colour: lime stays reserved for selection.
        if(emphasized&&available&&!active&&border>0)border=2;
        foreach(var label in _labels)if(label)label.color=labelColor;
        if(button.targetGraphic)button.targetGraphic.color=Color.clear;
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
