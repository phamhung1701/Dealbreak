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
    EditorialSurface _surface;

    // Cut Sheet sprite recipe (S10b, visual-design 3a), opt-in so legacy screens keep their look. The
    // face is 9-sliced on the panel's 4px border (tiling its 64px centre showed seams every tile). The
    // face is a panel sprite over a hard ink shadow; the shadow stays put and the face moves. Primary
    // prints brick, every other role paper. `selected` latches the button down (the active sort).
    [Tooltip("Draw the Cut Sheet sprite recipe instead of the flat frame. Set by DealbreakUIStyle.CutSheetButton.")]
    public bool cutSheet;
    [HideInInspector] public Role cutSheetRole;
    [Tooltip("Rest shadow depth in px (CutSheetMetrics.ShadowButton or ShadowSlot).")]
    public float restShadow=CutSheetMetrics.ShadowButton;
    /// <summary>ATTACK's disabled fill: brick 45% toward the board.</summary>
    public static readonly Color DisabledRed=new Color32(0x72,0x2A,0x22,0xFF);
    public const string HatchName="CutSheetHatch",FocusRingName="CutSheetFocusRing";
    const float FocusRingOutset=4;
    Image _hatch,_ring;
    Vector2 _faceOffset,_tweenFrom,_tweenTarget;
    float _tweenStart,_tweenSeconds;
    bool _tweenPress;
    Color _shownLabel;
    bool _labelsShown;
    public Vector2 FaceOffset=>_faceOffset;
    public bool HatchShown=>_hatch&&_hatch.enabled;
    public bool FocusRingShown=>_ring&&_ring.enabled;
    /// <summary>The shadow the eye reads: its rest depth less the face's travel toward it.</summary>
    public float VisibleShadowDepth
    {
        get
        {
            var shadow=frame?frame.SpriteShadow:null;
            if(!shadow||!shadow.Image||!shadow.Image.enabled)return 0;
            return Mathf.Max(0,shadow.Depth-_faceOffset.x);
        }
    }

    void LateUpdate()
    {
        if(!Refresh())return;
        if(cutSheet)return;
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
            _labelsShown=false;
        }
        if(cutSheet)return RefreshCutSheet(theme,available);
        // Cached, and looked up with TryGetComponent: a missing surface's Editor fake null would read as Open.
        if(!_surface)TryGetComponent(out _surface);
        bool open=_surface&&_surface.treatment==EditorialSurface.Treatment.Open;
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
        if(!paper&&_surface&&_surface.treatment==EditorialSurface.Treatment.Paper)
        {fill=theme.charcoal;rim=theme.slate;border=1;}
        Color labelColor=paper?theme.inkPrint:role==Role.Destructive?theme.brick:theme.paper;
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
    bool RefreshCutSheet(DealbreakUITheme theme,bool available)
    {
        bool primary=role==Role.Primary;
        bool latched=available&&selected;
        bool pressed=available&&_pressed;
        Color face=!available?(primary?DisabledRed:theme.muted):latched?theme.inkPrint:primary?theme.brick:theme.paper;
        Color label=latched||primary?theme.paper:theme.inkPrint;
        float depth=available&&!latched?restShadow:0;
        if(!frame.spriteMode||frame.sprite!=theme.panelSprite||frame.spriteType!=Image.Type.Sliced||frame.spriteTint!=face||
           !Mathf.Approximately(frame.shadowDepth,depth)||frame.tilt!=0||!frame.SpriteFace||!frame.SpriteFace.gameObject.activeSelf)
        {
            frame.spriteMode=true;frame.sprite=theme.panelSprite;frame.spriteType=Image.Type.Sliced;frame.spriteFillCenter=true;
            frame.spritePixelsPerUnitMultiplier=1;frame.spriteTint=face;frame.shadowDepth=depth;frame.tilt=0;
            if(frame.rectTransform.anchoredPosition!=Vector2.zero)frame.rectTransform.anchoredPosition=Vector2.zero;
            frame.Refresh();
        }
        if(!_hatch||!_ring)BuildParts(theme);
        if(_hatch&&_hatch.enabled==available)_hatch.enabled=!available;
        bool ring=available&&_focus;
        if(_ring&&_ring.enabled!=ring)_ring.enabled=ring;
        if(!_labelsShown||label!=_shownLabel)
        {
            foreach(var text in _labels)if(text)text.color=label;
            _shownLabel=label;_labelsShown=true;
        }
        if(button.targetGraphic&&button.targetGraphic.color!=Color.clear)button.targetGraphic.color=Color.clear;
        var target=latched||pressed?CutSheetMetrics.PressedFaceOffset:available&&_hover?CutSheetMetrics.HoverFaceOffset:Vector2.zero;
        StepFace(target,pressed||latched);
        // A face pressed onto its shadow hides it (a 3px sort shadow under a 4px press would leave a sliver).
        var shadow=frame.SpriteShadow;
        if(shadow&&shadow.Image)
        {
            bool show=shadow.Depth>0&&_faceOffset.x<shadow.Depth;
            if(shadow.Image.enabled!=show)shadow.Image.enabled=show;
        }
        return true;
    }

    // Press 60ms ease-out; release and hover 90ms ease-out-back. Reduce Motion snaps.
    void StepFace(Vector2 target,bool press)
    {
        if(target!=_tweenTarget)
        {
            _tweenFrom=_faceOffset;_tweenTarget=target;_tweenPress=press;_tweenStart=Time.unscaledTime;
            _tweenSeconds=CutSheetMotion.Seconds(press?CutSheetMetrics.PressSeconds:CutSheetMetrics.ReleaseSeconds);
        }
        if(_faceOffset==_tweenTarget&&frame.SpriteFace&&frame.SpriteFace.rectTransform.offsetMin==_faceOffset)return;
        float t=_tweenSeconds<=0?1:Mathf.Clamp01((Time.unscaledTime-_tweenStart)/_tweenSeconds);
        float eased=_tweenPress?1-(1-t)*(1-t):EaseOutBack(t);
        _faceOffset=t>=1?_tweenTarget:Vector2.LerpUnclamped(_tweenFrom,_tweenTarget,eased);
        frame.SetFaceOffset(_faceOffset);
    }

    static float EaseOutBack(float t)
    {
        const float c1=1.70158f,c3=c1+1;
        float u=t-1;
        return 1+c3*u*u*u+c1*u*u;
    }

    // The hatch (disabled) and the focus ring ride on the face, beneath the button's labels.
    void BuildParts(DealbreakUITheme theme)
    {
        var face=frame.SpriteFace;if(!face)return;
        if(!_hatch)
        {
            var existing=face.transform.Find(HatchName);
            var rect=existing?(RectTransform)existing:DealbreakUIStyle.Child(face.transform,HatchName,typeof(CanvasRenderer),typeof(Image));
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            rect.TryGetComponent(out _hatch);
            _hatch.sprite=theme.hatchSprite;_hatch.type=Image.Type.Tiled;_hatch.color=Color.white;_hatch.raycastTarget=false;
            if(!rect.TryGetComponent<ScreenPixelTile>(out _))rect.gameObject.AddComponent<ScreenPixelTile>();
            _hatch.enabled=false;
        }
        if(!_ring)
        {
            var existing=face.transform.Find(FocusRingName);
            _ring=existing&&existing.TryGetComponent<Image>(out var found)?found:CutSheetHoverRing.CreateRing(face.transform,FocusRingName,FocusRingOutset);
        }
    }

    public void OnPointerEnter(PointerEventData _) => _hover=true;
    public void OnPointerExit(PointerEventData _) { _hover=false;_pressed=false; }
    public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left)_pressed=true; }
    public void OnPointerUp(PointerEventData _) => _pressed=false;
    public void OnSelect(BaseEventData _) => _focus=true;
    public void OnDeselect(BaseEventData _) { _focus=false;_pressed=false; }
    void OnDisable() { _hover=_pressed=_focus=false; }
}
