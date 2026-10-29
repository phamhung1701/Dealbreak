using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// One shared detail frame for all screens, with live data and bounded scrolling. Details open on
/// click (pinned) or keyboard focus, never on hover; a selected card or Gear shows its own.
/// </summary>
public sealed class ContextualTooltipUI : MonoBehaviour
{
    public static ContextualTooltipUI Instance {get;private set;}
    public GameObject panel;
    public TMP_Text title,body;
    public ScrollRect scroll;
    public bool HasPinnedDetail => _pinned!=null;
    public bool IsPinned(MonoBehaviour source) => source&&_pinned==source;
    MonoBehaviour _source,_pinned,_dismissed;
    float _leaveUntil;
    string _lastHeading,_lastBody;
    float _lastWidth,_lastScale=-1;
    readonly Vector3[] _positionCorners=new Vector3[4];
    readonly List<CardView> _positionHandViews=new();
    static readonly List<CanvasGroup> ParentGroups=new();
    readonly List<GearIconSlotUI> _gearSlots=new();

    // Cut Sheet skin (HUD slice S12, visual-design 3b): a paper face over a 6px ink shadow, with a
    // notch drawn under the face that peeks out toward the clicked object. Built once at runtime, so
    // every scene that carries the detail panel (combat, map, Black Market, menus) gets the same look.
    public const float BodySize=20,HeadingSize=22,NotchSize=14,NotchInset=14,GrainAlpha=.04f;
    /// <summary>
    /// Tooltip text size (owner 2026-10-27: 100/115/130%), read from the player's setting (S20). It
    /// scales the body and heading type and the maximum width together; a body that then outgrows
    /// the screen scrolls.
    /// </summary>
    public static float TextScale=>Mathf.Clamp(CutSheetMotion.TooltipTextScale,1f,1.5f);
    public static float MaxWidth=>CutSheetMetrics.TooltipMaxWidth*TextScale;
    Image _face;CutSheetShadow _shadow;RectTransform _notch;
    public Image Face=>_face;
    public CutSheetShadow Shadow=>_shadow;
    public RectTransform Notch=>_notch;
    /// <summary>Unit vector from the panel toward the object it describes; the notch points this way.</summary>
    public Vector2 NotchDirection{get;private set;}
    /// <summary>The panel's resting anchored position; the open slide ends here.</summary>
    public Vector2 RestPosition{get;private set;}
    float _openedAt,_closeStart,_closeFrom,_progress;
    bool _closing;
    string _headingCategory,_headingTitle,_headingText;
    string _measuredHeading,_measuredBody;float _measuredWidth=-1,_measuredScale=-1,_headingHeight,_bodyHeight;

    // Above every gameplay layer (hand cards 1-21, route nodes, attack visuals 60-66), below modal menus (100).
    const int SortingOrder=90;
    const float Gap=12f;
    static readonly List<RectTransform> Obstacles=new();
    enum Side{Right,Left,Above,Below}
    static readonly Side[] BesideOrder={Side.Right,Side.Left,Side.Above,Side.Below};
    static readonly Side[] HandOrder={Side.Above,Side.Right,Side.Left};
    static readonly Side[] RowOrder={Side.Below,Side.Right,Side.Left};
    static bool IsRow(Rect bounds)=>bounds.width>bounds.height*1.5f;

    /// <summary>
    /// Registers a panel a tooltip must step around, such as the Trick and Gear action panels that
    /// open beside their slots. Inactive or destroyed panels are ignored.
    /// </summary>
    public static void RegisterObstacle(RectTransform obstacle){if(obstacle&&!Obstacles.Contains(obstacle))Obstacles.Add(obstacle);}
    static readonly List<RectTransform[]> SlotGroups=new();
    /// <summary>
    /// Registers a row of slots (the Trick slots) whose details open beside the whole row, so a
    /// detail never covers the neighbouring slots. Gear slots use their shared container.
    /// </summary>
    public static void RegisterSlotGroup(RectTransform[] slots)
    {
        SlotGroups.RemoveAll(group=>group.All(member=>!member));
        if(slots!=null&&slots.Length>0)SlotGroups.Add(slots);
    }
    void OnEnable(){Instance=this;EnsureSkin();LinkScrollbar();Hide(true);CutSheetMotion.Changed-=OnSettingsChanged;CutSheetMotion.Changed+=OnSettingsChanged;}
    // A text-size change while a detail is open re-lays it out at once (type, width, height, scroll).
    void OnSettingsChanged()
    {
        if(!this||!panel||!panel.activeSelf||_closing||!body||!title)return;
        _lastScale=-1;
        LateUpdate();
    }
    void EnsureSkin()
    {
        if(!panel||_face)return;
        var theme=DealbreakUIStyle.Theme;var root=panel.transform;
        // The neo-punk chrome (charcoal EditorialFrame, title band, backing fill) retires here.
        for(int i=0;i<root.childCount;i++)
        {
            var child=root.GetChild(i);
            if(child.name=="DetailTitleBand"||child.TryGetComponent<EditorialFrame>(out _))child.gameObject.SetActive(false);
        }
        if(panel.TryGetComponent<EditorialFrame>(out var frame))frame.enabled=false;
        if(panel.TryGetComponent<Image>(out var backing))backing.color=Color.clear;
        if(!panel.TryGetComponent<CanvasGroup>(out _))panel.AddComponent<CanvasGroup>();
        _shadow=CutSheetShadow.Create(root);
        _notch=SkinImage(root,"CutSheetNotch").rectTransform;
        _notch.anchorMin=_notch.anchorMax=_notch.pivot=new Vector2(.5f,.5f);
        _notch.sizeDelta=new Vector2(NotchSize,NotchSize);_notch.localRotation=Quaternion.Euler(0,0,45);
        _face=SkinImage(root,"CutSheetFace");
        var face=_face.rectTransform;face.anchorMin=Vector2.zero;face.anchorMax=Vector2.one;face.offsetMin=face.offsetMax=Vector2.zero;
        // Draw order: shadow, notch, face, grain, then the panel's own text and scroll view.
        _shadow.transform.SetSiblingIndex(0);_notch.SetSiblingIndex(1);face.SetSiblingIndex(2);
        if(theme&&theme.grainSprite)
        {
            var grain=new GameObject("CutSheetGrain",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
            grain.layer=panel.layer;grain.transform.SetParent(face,false);
            var rect=(RectTransform)grain.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var raw=grain.GetComponent<RawImage>();raw.texture=theme.grainSprite.texture;raw.raycastTarget=false;
            var ink=theme.inkPrint;raw.color=new Color(ink.r,ink.g,ink.b,GrainAlpha);
            grain.AddComponent<ScreenPixelTile>();
        }
        ApplySkinColors();
        _shadow.SetDepth(0);
        if(scroll&&scroll.verticalScrollbar)
        {
            var ink=theme?theme.inkPrint:CutSheetShadow.FallbackTint;
            if(scroll.verticalScrollbar.TryGetComponent<Image>(out var track))track.color=new Color(ink.r,ink.g,ink.b,.12f);
            if(scroll.verticalScrollbar.handleRect&&scroll.verticalScrollbar.handleRect.TryGetComponent<Image>(out var thumb))thumb.color=new Color(ink.r,ink.g,ink.b,.55f);
        }
        if(!panel.TryGetComponent<Canvas>(out var layer))
        {
            layer=panel.AddComponent<Canvas>();
            if(!panel.TryGetComponent<GraphicRaycaster>(out _))panel.AddComponent<GraphicRaycaster>();
        }
    }
    // Unity drops overrideSorting set while the panel is inactive (it is a root canvas then), so the
    // order is written only while the panel is active, and re-asserted each shown frame on change.
    void AssertLayer()
    {
        if(!panel.activeInHierarchy||!panel.TryGetComponent<Canvas>(out var layer))return;
        if(!layer.overrideSorting)layer.overrideSorting=true;
        if(layer.sortingOrder!=SortingOrder)layer.sortingOrder=SortingOrder;
    }
    // A closing panel must not swallow clicks meant for what lies under it.
    void BlockRaycasts(bool block)
    {
        if(panel.TryGetComponent<CanvasGroup>(out var group)&&group.blocksRaycasts!=block)group.blocksRaycasts=block;
    }
    Image SkinImage(Transform root,string name)
    {
        var item=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        item.layer=panel.layer;item.transform.SetParent(root,false);
        var image=item.GetComponent<Image>();image.raycastTarget=false;
        return image;
    }
    // Sprite and colour writes happen only when they differ (a theme swap, the first build).
    void ApplySkinColors()
    {
        var theme=DealbreakUIStyle.Theme;if(!theme||!_face)return;
        foreach(var image in new[]{_face,_notch.GetComponent<Image>()})
        {
            if(image.sprite!=theme.panelSprite)image.sprite=theme.panelSprite;
            if(image.type!=Image.Type.Sliced)image.type=Image.Type.Sliced;
            if(image.color!=theme.paper)image.color=theme.paper;
        }
        _shadow.Match(_face,theme.inkPrint,_shadow.Depth);
    }
    static void Style(TMP_Text text,TMP_FontAsset font,float size,Color color)
    {
        if(!text)return;
        if(font&&text.font!=font)text.font=font;
        if(text.enableAutoSizing)text.enableAutoSizing=false;
        if(!Mathf.Approximately(text.fontSize,size))text.fontSize=size;
        if(text.color!=color)text.color=color;
    }
    void ApplyType()
    {
        var theme=DealbreakUIStyle.Theme;if(!theme)return;
        Style(body,theme.bodyFont,BodySize*TextScale,theme.inkPrint);
        Style(title,theme.labelFont?theme.labelFont:theme.headingFont,HeadingSize*TextScale,theme.inkPrint);
    }
    // The scrollbar shows only while the detail overflows. An authoring pass once dropped its link,
    // leaving a fixed handle on every detail; the panel's own scrollbar is linked again here.
    void LinkScrollbar()
    {
        if(!scroll||!panel)return;
        if(!scroll.vertical)scroll.vertical=true;
        if(!scroll.verticalScrollbar)scroll.verticalScrollbar=panel.transform.Find("Detail Scrollbar")?.GetComponent<Scrollbar>();
        if(scroll.verticalScrollbar)scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
    }
    void OnDisable(){CutSheetMotion.Changed-=OnSettingsChanged;if(Instance==this)Instance=null;Hide(true);}
    public void Inspect(MonoBehaviour source){if(!source)return;_source=source;_dismissed=null;_leaveUntil=float.PositiveInfinity;}
    public void Leave(MonoBehaviour source){if(_source==source)_leaveUntil=Time.unscaledTime+.18f;}
    public void Pin(MonoBehaviour source){_pinned=_pinned==source?null:source;_dismissed=null;}
    /// <summary>Drops any focused or pinned detail coming from this object, e.g. when it is picked up to drag.</summary>
    public void Release(Component owner)
    {
        if(!owner)return;
        bool owned(MonoBehaviour value)=>value&&value.transform.IsChildOf(owner.transform);
        if(owned(_source)){_source=null;_leaveUntil=0;}
        if(owned(_pinned))_pinned=null;
        Hide();
    }
    public bool DismissPinned()
    {
        var card=CardManager.Instance?.selectedCard?.GetComponent<CombatInspectUI>();
        var gear=SelectedGearInspect();
        MonoBehaviour selected=_pinned?_pinned:card?card:gear;
        if(!selected||selected==_dismissed)return false;
        _dismissed=selected;
        _source=_pinned=null;Hide();return true;
    }
    // Runs every frame while nothing is inspected, so the rail is found once, not searched for each frame.
    GearRailUI _gearRail;
    CombatInspectUI SelectedGearInspect()
    {
        if(!_gearRail)_gearRail=Object.FindFirstObjectByType<GearRailUI>();
        if(!_gearRail)return null;
        _gearRail.GetComponentsInChildren(_gearSlots);
        foreach(var slot in _gearSlots)if(slot&&slot.IsDetailSelected)return slot.GetComponent<CombatInspectUI>();
        return null;
    }
    static bool PointerOver(MonoBehaviour source,Vector2 screen)
    {
        if(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)source.transform,screen,null))return true;
        var view=source.GetComponent<CardView>();
        return view&&view.visualRoot&&RectTransformUtility.RectangleContainsScreenPoint(view.visualRoot,screen,null);
    }
    // Hides this detail until something else is inspected; a Gear's lime selection clears with it.
    void Dismiss(MonoBehaviour shown)
    {
        _dismissed=shown;_source=_pinned=null;
        if(shown&&shown.GetComponent<GearIconSlotUI>()){if(!_gearRail)_gearRail=Object.FindFirstObjectByType<GearRailUI>();if(_gearRail)_gearRail.ClearObjectSelection();}
        Hide();
    }
    /// <summary>True while the detail is open or opening (not while it closes).</summary>
    public bool IsShown=>panel&&panel.activeSelf&&!_closing;
    // Closes over 70ms (alpha and shadow to 0); instant under Reduce Motion, while dragging and on disable.
    void Hide(bool instant=false)
    {
        _lastHeading=_lastBody=null;
        if(!panel)return;
        if(!panel.activeSelf){_closing=false;return;}
        if(_closing&&!instant)return;
        float seconds=CutSheetMotion.Seconds(CutSheetMetrics.TooltipCloseSeconds);
        if(instant||seconds<=0){Deactivate();return;}
        _closing=true;_closeStart=Time.unscaledTime;_closeFrom=_progress;
        BlockRaycasts(false);
    }
    void Deactivate()
    {
        _closing=false;_progress=0;
        panel.SetActive(false);
        if(panel.TryGetComponent<CanvasGroup>(out var group))group.alpha=0;
        if(_shadow)_shadow.SetDepth(0);
    }
    void TickClose()
    {
        float seconds=CutSheetMotion.Seconds(CutSheetMetrics.TooltipCloseSeconds);
        float t=seconds<=0?1:Mathf.Clamp01((Time.unscaledTime-_closeStart)/seconds);
        if(t>=1){Deactivate();return;}
        _progress=_closeFrom*(1-t);
        ApplyMotion(_progress,false);
    }
    // e is the eased open amount: alpha, shadow depth 0..6 and the 6px slide from the source side.
    void ApplyMotion(float e,bool slide)
    {
        if(panel.TryGetComponent<CanvasGroup>(out var group)&&!Mathf.Approximately(group.alpha,e))group.alpha=e;
        if(_shadow&&!Mathf.Approximately(_shadow.Depth,e*CutSheetMetrics.ShadowPanel))_shadow.SetDepth(e*CutSheetMetrics.ShadowPanel);
        if(slide)
        {
            var rect=(RectTransform)panel.transform;
            var position=RestPosition+NotchDirection*(CutSheetMetrics.TooltipSlide*(1-e));
            if(rect.anchoredPosition!=position)rect.anchoredPosition=position;
        }
    }
    void LateUpdate()
    {
        if(!panel||!body||!title)return;
        if(_closing)TickClose();
        // While an item is dragged its drop preview is the detail; nothing else opens.
        if(ItemDragUI.IsDragging){if(panel.activeSelf)Hide(true);return;}
        bool overPanel=IsShown&&Mouse.current!=null&&RectTransformUtility.RectangleContainsScreenPoint(
            (RectTransform)panel.transform,Mouse.current.position.ReadValue(),null);
        bool inspected=_source is CombatInspectUI combat?combat.IsInspected:_source is ContextualInfoTrigger data&&data.IsInspected;
        MonoBehaviour candidate=_source&&(inspected||Time.unscaledTime<_leaveUntil||overPanel)?_source:_pinned;
        if(!candidate&&CardManager.Instance?.selectedCard!=null)
            candidate=CardManager.Instance.selectedCard.GetComponent<CombatInspectUI>();
        if(!candidate)candidate=SelectedGearInspect();
        if(candidate==_dismissed||!Visible(candidate)||!TryDescribe(candidate,out var info)){Hide();return;}
        // (owner, 2026-10-23) A click anywhere but the detail or what it describes closes it, whatever opened it.
        if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame&&!overPanel&&!PointerOver(candidate,Mouse.current.position.ReadValue()))
        {Dismiss(candidate);return;}
        EnsureSkin();
        ApplyType();
        string heading=Heading(info);
        string text=BodyMarkup(info.Body);
        Position(candidate,heading,text);
        if(!IsShown)
        {
            // Opening, or reopening mid-close from where the close had reached.
            float from=_closing?_progress:0;
            _closing=false;
            _openedAt=Time.unscaledTime-from*CutSheetMotion.Seconds(CutSheetMetrics.TooltipOpenSeconds);
            panel.SetActive(true);
            BlockRaycasts(true);
        }
        AssertLayer();
        float open=CutSheetMotion.Seconds(CutSheetMetrics.TooltipOpenSeconds);
        float t=open<=0?1:Mathf.Clamp01((Time.unscaledTime-_openedAt)/open);
        _progress=1-(1-t)*(1-t);
        ApplyMotion(_progress,true);
        if(heading!=_lastHeading||info.Body!=_lastBody||Mathf.Abs(body.rectTransform.rect.width-_lastWidth)>.5f||!Mathf.Approximately(TextScale,_lastScale))
        {
            title.text=heading;body.text=text;
            // The body is its measured height, so text past the viewport scrolls (S20 at 130%).
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,_bodyHeight+16);
            if(_lastHeading!=heading&&scroll)scroll.verticalNormalizedPosition=1;
            _lastHeading=heading;_lastBody=info.Body;
            _lastWidth=body.rectTransform.rect.width;_lastScale=TextScale;
        }
        if(scroll&&HasPinnedDetail&&Keyboard.current!=null)
        {
            if(Keyboard.current.pageDownKey.wasPressedThisFrame)scroll.verticalNormalizedPosition-=.25f;
            if(Keyboard.current.pageUpKey.wasPressedThisFrame)scroll.verticalNormalizedPosition+=.25f;
        }
    }
    static bool Visible(MonoBehaviour source)
    {
        if(!source||!source.isActiveAndEnabled||!source.gameObject.activeInHierarchy)return false;
        bool allow=source is ContextualInfoTrigger t&&t.allowWhileBlocked;
        if(source.GetComponent<CardView>()&&CardManager.Instance?.CanRouteHandEnhancementTargetClick==true)allow=true;
        if(GameplayInputGate.IsBlocked&&!allow)return false;
        return !HiddenByGroup(source);
    }
    static bool HiddenByGroup(Component item)
    {
        item.GetComponentsInParent(false,ParentGroups);
        foreach(var group in ParentGroups)if(group.alpha<=.01f)return true;
        return false;
    }
    public static bool TryDescribe(MonoBehaviour source,out ContextualInfo info)
    {
        info=default;
        if(source is ContextualInfoTrigger trigger){var value=trigger.Info;if(!value.HasValue)return false;info=value.Value;return true;}
        if(!source)return false;
        if(source.TryGetComponent<CardView>(out var view)&&view.data!=null)
        {
            var card=view.data;var combat=CombatManager.Instance;
            int attack=combat?combat.CalculateCardAttackDamage(card):card.AttackValue;
            int block=combat?combat.CalculateCardDefense(card):card.DefenseValue;
            string mod=card.Enhancement?$"\n\nMOD • {ModSignature.Name(card.Enhancement)}\n{ModSignature.Description(card.Enhancement)}":string.Empty;
            // The selection's block preview reads on the status line under the enemies, not here.
            // S14: the card names itself, so its detail has no heading.
            info=new(null,null,$"ATK {attack}    DEF {block}{mod}");return true;
        }
        if(source.TryGetComponent<EnemyDisplayUI>(out var enemy)&&enemy.DisplayedEnemy!=null&&!enemy.DisplayedEnemy.IsDefeated)
        {
            var data=enemy.DisplayedEnemy;
            // Ability names and rules open from each ability tag under the enemy (owner 2026-10-29): each fact once.
            // HP, attack, Shield, Double Strike/charged and the Headliner reward read on the enemy (S14);
            // the detail adds only what the board does not show.
            string detail=enemy.DetailGuidance;
            if(string.IsNullOrEmpty(detail))return false;
            info=new(data.type.sourceCard?"Headliner":"Enemy",enemy.combatPortraits?enemy.combatPortraits.DisplayName(data):data.DisplayName,detail);return true;
        }
        if(source.TryGetComponent<GearIconSlotUI>(out var gear)&&gear.Gear)
        {info=new("Gear",gear.Gear.displayName,GearRules.OwnedDescription(CardManager.Instance,gear.Gear,gear.InstanceId));return true;}
        if(source.TryGetComponent<DrawDeckStackUI>(out _)&&CardManager.Instance!=null)
        {info=new("Deck","Draw Pile",DrawDeckStackUI.DetailText(CardManager.Instance,RunManager.Instance));return true;}
        return false;
    }
    // Every detail opens next to what it describes. Hand cards sit on the bottom edge, so theirs
    // open above them; everything else opens to the right, then left, above or below as room allows.
    void Position(MonoBehaviour source,string heading,string text)
    {
        var rect=(RectTransform)panel.transform;var parent=(RectTransform)rect.parent;
        var panelGroup=panel.GetComponent<CanvasGroup>();if(panelGroup&&!panelGroup.ignoreParentGroups)panelGroup.ignoreParentGroups=true;
        // Without its own Canvas the panel draws in hierarchy order, under panels that come later (the route map).
        if(!panel.TryGetComponent<Canvas>(out _))
        {
            panel.AddComponent<Canvas>();
            if(!panel.TryGetComponent<GraphicRaycaster>(out _))panel.AddComponent<GraphicRaycaster>();
        }
        var view=source.GetComponent<CardView>();
        bool handCard=view&&view.homeField!=null;
        bool slotDetail=source.GetComponent<GearIconSlotUI>()!=null||
            source is ContextualInfoTrigger&&source.GetComponent<Button>()&&source.name.StartsWith("BackpackSlot");
        // A hand card's detail drops its heading (the card face already names the card), so it stays
        // low enough to clear the enemies and status line above the hand. A source with no name has none either.
        bool showHeading=!handCard&&!string.IsNullOrEmpty(heading);
        float scale=TextScale;
        float width=handCard?Mathf.Min(320*scale,parent.rect.width*.3f):slotDetail?Mathf.Min(340*scale,parent.rect.width*.26f):Mathf.Min(MaxWidth,parent.rect.width*.38f);
        Measure(heading,text,width);
        float top=showHeading?Padding+_headingHeight+HeadingGap:Padding;
        Layout(showHeading,top);
        float chrome=top+Padding+16,minimum=handCard?CompactMinimum:top+Padding+40;
        float height=Mathf.Min(parent.rect.height*.65f,Mathf.Clamp(_bodyHeight+chrome,minimum,460*scale));
        var half=new Vector2(.5f,.5f);
        if(rect.anchorMin!=half||rect.anchorMax!=half||rect.pivot!=half){rect.anchorMin=rect.anchorMax=rect.pivot=half;}
        var size=new Vector2(width,height);
        if(rect.sizeDelta!=size)rect.sizeDelta=size;

        var anchor=view&&view.visualRoot?view.visualRoot:(RectTransform)source.transform;
        bool gearSlot=source.GetComponent<GearIconSlotUI>()!=null;
        // The Gear and Trick rows run across the header, so their details open under the slot.
        bool headerRow=slotDetail&&IsRow(GroupBounds(parent,anchor,gearSlot));
        var target=headerRow?BoundsIn(parent,anchor):slotDetail?SlotGroupBounds(parent,anchor,gearSlot):BoundsIn(parent,anchor);
        var order=handCard?HandOrder:headerRow?RowOrder:BesideOrder;
        Vector2 chosen=Avoid(parent,Beside(order[0],target,size),size,rect,handCard);
        foreach(var side in order)
        {
            var candidate=Avoid(parent,Beside(side,target,size),size,rect,handCard);
            if(Fits(parent,side,candidate,size)){chosen=candidate;break;}
        }
        float halfWidth=width*.5f,halfHeight=height*.5f;
        chosen.x=Mathf.Clamp(chosen.x,parent.rect.xMin+halfWidth+Gap,parent.rect.xMax-halfWidth-Gap);
        chosen.y=Mathf.Clamp(chosen.y,parent.rect.yMin+halfHeight+Gap,parent.rect.yMax-halfHeight-Gap);
        RestPosition=chosen-parent.rect.center;
        // The notch aims at the source's own untilted rect (S7 slots tilt their face, not the root),
        // not the slot group the panel cleared.
        var local=NotchPlacement(new Rect(chosen-size*.5f,size),BoundsIn(parent,anchor),out var direction);
        NotchDirection=direction;
        if(_notch&&_notch.anchoredPosition!=local)_notch.anchoredPosition=local;
    }

    /// <summary>
    /// Where the notch sits (relative to the panel centre) and which way it points: toward the side
    /// the source lies on, slid along that edge toward the source's centre, kept off the corners.
    /// </summary>
    public static Vector2 NotchPlacement(Rect panel,Rect target,out Vector2 direction)
    {
        if(panel.xMin>=target.xMax-1)direction=Vector2.left;
        else if(panel.xMax<=target.xMin+1)direction=Vector2.right;
        else if(panel.yMin>=target.yMax-1)direction=Vector2.down;
        else if(panel.yMax<=target.yMin+1)direction=Vector2.up;
        else
        {
            var delta=target.center-panel.center;
            direction=Mathf.Abs(delta.x)>=Mathf.Abs(delta.y)?new Vector2(delta.x<0?-1:1,0):new Vector2(0,delta.y<0?-1:1);
        }
        float x=Mathf.Clamp(target.center.x,panel.xMin+NotchInset,Mathf.Max(panel.xMin+NotchInset,panel.xMax-NotchInset));
        float y=Mathf.Clamp(target.center.y,panel.yMin+NotchInset,Mathf.Max(panel.yMin+NotchInset,panel.yMax-NotchInset));
        Vector2 edge=direction.x<0?new(panel.xMin,y):direction.x>0?new(panel.xMax,y):direction.y<0?new(x,panel.yMin):new(x,panel.yMax);
        return edge-panel.center;
    }

    // 16px padding all round; the heading sits in the top padding and the body scrolls under it.
    const float Padding=CutSheetMetrics.TooltipPadding,HeadingGap=8f,CompactMinimum=80f;
    // Text is measured only when the heading, body, width or text scale changes.
    void Measure(string heading,string text,float width)
    {
        float scale=TextScale;
        if(heading==_measuredHeading&&text==_measuredBody&&Mathf.Approximately(width,_measuredWidth)&&Mathf.Approximately(scale,_measuredScale))return;
        _measuredHeading=heading;_measuredBody=text;_measuredWidth=width;_measuredScale=scale;
        float inner=width-Padding*2;
        _headingHeight=string.IsNullOrEmpty(heading)?0:title.GetPreferredValues(heading,inner,0).y;
        _bodyHeight=body.GetPreferredValues(text??string.Empty,inner,0).y;
    }
    void Layout(bool show,float top)
    {
        if(title&&title.gameObject.activeSelf!=show)title.gameObject.SetActive(show);
        if(title&&show)
        {
            var t=title.rectTransform;
            Vector2 min=new(Padding,-(Padding+_headingHeight)),max=new(-Padding,-Padding);
            if(t.anchorMin!=new Vector2(0,1)||t.anchorMax!=Vector2.one){t.anchorMin=new Vector2(0,1);t.anchorMax=Vector2.one;t.pivot=new Vector2(.5f,1);}
            if(t.offsetMin!=min)t.offsetMin=min;
            if(t.offsetMax!=max)t.offsetMax=max;
        }
        if(scroll&&scroll.viewport)
        {
            var v=scroll.viewport;Vector2 min=new(Padding,Padding),max=new(-Padding,-top);
            if(v.offsetMin!=min)v.offsetMin=min;
            if(v.offsetMax!=max)v.offsetMax=max;
        }
        // The scrollbar (shown only on overflow) runs in the right padding.
        if(scroll&&scroll.verticalScrollbar&&scroll.verticalScrollbar.transform is RectTransform bar)
        {
            Vector2 min=new(-12,Padding),max=new(-6,-top);
            if(bar.offsetMin!=min)bar.offsetMin=min;
            if(bar.offsetMax!=max)bar.offsetMax=max;
        }
    }

    // "CATEGORY / Title"; rebuilt only when the source's category or title changes.
    string Heading(ContextualInfo info)
    {
        if(_headingText!=null&&info.Category==_headingCategory&&info.Title==_headingTitle)return _headingText;
        _headingCategory=info.Category;_headingTitle=info.Title;
        _headingText=string.IsNullOrEmpty(info.Category)?info.Title??string.Empty:$"<size=82%>{info.Category.ToUpperInvariant()}</size>\n{info.Title}";// 82% of the 22px heading = 18px, the art bible's text floor (owner 2026-10-29)
        return _headingText;
    }

    // Digit runs print in Courier Prime through TMP's font tag (registered by name, as StatusTagUI
    // does); digits inside rich-text tags are left alone. Rebuilt only when the body changes.
    string _markupSource,_markupText,_numberOpen;
    readonly System.Text.StringBuilder _markup=new();
    string BodyMarkup(string text)
    {
        if(text==null)return string.Empty;
        if(_markupText!=null&&text==_markupSource)return _markupText;
        _markupSource=text;
        if(_numberOpen==null)
        {
            var theme=DealbreakUIStyle.Theme;
            if(theme&&theme.numericFont){MaterialReferenceManager.AddFontAsset(theme.numericFont);_numberOpen="<font=\""+theme.numericFont.name+"\">";}
        }
        if(_numberOpen==null){_markupText=text;return text;}
        _markup.Clear();
        bool inNumber=false,inTag=false;
        foreach(char c in text)
        {
            if(c=='<'&&!inTag){if(inNumber){_markup.Append("</font>");inNumber=false;}inTag=true;}
            bool digit=!inTag&&c>='0'&&c<='9';
            if(!inTag&&digit!=inNumber){_markup.Append(digit?_numberOpen:"</font>");inNumber=digit;}
            _markup.Append(c);
            if(c=='>'&&inTag)inTag=false;
        }
        if(inNumber)_markup.Append("</font>");
        _markupText=_markup.ToString();
        return _markupText;
    }

    // The row's horizontal extent with the hovered slot's height: the detail clears the
    // neighbouring slots but stays level with the one it describes.
    Rect SlotGroupBounds(RectTransform parent,RectTransform slot,bool gear)
    {
        var own=BoundsIn(parent,slot);
        var row=GroupBounds(parent,slot,gear);
        return Rect.MinMaxRect(row.xMin,own.yMin,row.xMax,own.yMax);
    }

    Rect GroupBounds(RectTransform parent,RectTransform slot,bool gear)
    {
        if(gear&&slot.parent is RectTransform container)return BoundsIn(parent,container);
        var bounds=BoundsIn(parent,slot);
        foreach(var group in SlotGroups)
        {
            if(System.Array.IndexOf(group,slot)<0)continue;
            foreach(var member in group)
            {
                if(!member||!member.gameObject.activeInHierarchy)continue;
                var other=BoundsIn(parent,member);
                bounds=Rect.MinMaxRect(Mathf.Min(bounds.xMin,other.xMin),Mathf.Min(bounds.yMin,other.yMin),
                    Mathf.Max(bounds.xMax,other.xMax),Mathf.Max(bounds.yMax,other.yMax));
            }
        }
        return bounds;
    }

    Rect BoundsIn(RectTransform parent,RectTransform child)
    {
        child.GetWorldCorners(_positionCorners);
        Vector2 min=new(float.PositiveInfinity,float.PositiveInfinity),max=new(float.NegativeInfinity,float.NegativeInfinity);
        foreach(var corner in _positionCorners){Vector2 point=parent.InverseTransformPoint(corner);min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }

    static Vector2 Beside(Side side,Rect target,Vector2 size) => side switch
    {
        Side.Right=>new(target.xMax+Gap+size.x*.5f,target.center.y),
        Side.Left=>new(target.xMin-Gap-size.x*.5f,target.center.y),
        Side.Above=>new(target.center.x,target.yMax+Gap+size.y*.5f),
        _=>new(target.center.x,target.yMin-Gap-size.y*.5f)
    };

    // Beside placements may slide vertically and stacked ones horizontally, so only the axis
    // that separates the detail from its object has to fit.
    static bool Fits(RectTransform parent,Side side,Vector2 center,Vector2 size)
    {
        var bounds=parent.rect;
        return side is Side.Right or Side.Left
            ? center.x-size.x*.5f>=bounds.xMin+Gap&&center.x+size.x*.5f<=bounds.xMax-Gap
            : center.y-size.y*.5f>=bounds.yMin+Gap&&center.y+size.y*.5f<=bounds.yMax-Gap;
    }

    // Steps sideways past open action panels and status lines (to whichever side fits and stays
    // nearer), and lifts details off the hand unless they describe a hand card.
    Vector2 Avoid(RectTransform parent,Vector2 center,Vector2 size,RectTransform self,bool handCard)
    {
        for(int pass=0;pass<2;pass++)
            for(int i=Obstacles.Count-1;i>=0;i--)
            {
                var obstacle=Obstacles[i];
                if(!obstacle){Obstacles.RemoveAt(i);continue;}
                if(obstacle==self||!obstacle.gameObject.activeInHierarchy||!ObstacleBounds(parent,obstacle,out var bounds))continue;
                if(!Overlaps(center,size,bounds))continue;
                float right=bounds.xMax+Gap+size.x*.5f,left=bounds.xMin-Gap-size.x*.5f;
                bool rightFits=right+size.x*.5f<=parent.rect.xMax-Gap,leftFits=left-size.x*.5f>=parent.rect.xMin+Gap;
                center.x=rightFits&&(!leftFits||right-center.x<=center.x-left)?right:leftFits?left:right;
            }
        if(!handCard&&GetHandBounds(parent,out var hand)&&Overlaps(center,size,hand))
            center.y=hand.yMax+Gap+size.y*.5f;
        return center;
    }

    // A label obstacle is its printed text, not its (often full-width) rect; hidden ones are skipped.
    bool ObstacleBounds(RectTransform parent,RectTransform obstacle,out Rect bounds)
    {
        bounds=default;
        if(HiddenByGroup(obstacle))return false;
        if(!obstacle.TryGetComponent<TMP_Text>(out var text)){bounds=BoundsIn(parent,obstacle);return true;}
        if(!text.enabled||text.alpha<=.01f||string.IsNullOrWhiteSpace(text.text))return false;
        var printed=text.textBounds;if(printed.size.x<=0)return false;
        Vector2 a=parent.InverseTransformPoint(obstacle.TransformPoint(printed.min)),b=parent.InverseTransformPoint(obstacle.TransformPoint(printed.max));
        bounds=Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
        return true;
    }

    static bool Overlaps(Vector2 center,Vector2 size,Rect other) =>
        center.x+size.x*.5f>other.xMin&&center.x-size.x*.5f<other.xMax&&center.y+size.y*.5f>other.yMin&&center.y-size.y*.5f<other.yMax;

    bool GetHandBounds(RectTransform parent,out Rect bounds)
    {
        bounds=default;
        var holder=CardManager.Instance?.handField?.cardsHolder;if(!holder)return false;
        holder.GetComponentsInChildren(false,_positionHandViews);
        bool any=false;
        foreach(var view in _positionHandViews)
        {
            if(!view||view.data==null)continue;
            var card=view.visualRoot?view.visualRoot:view.transform as RectTransform;if(!card)continue;
            var cardBounds=BoundsIn(parent,card);
            bounds=any?Rect.MinMaxRect(Mathf.Min(bounds.xMin,cardBounds.xMin),Mathf.Min(bounds.yMin,cardBounds.yMin),
                Mathf.Max(bounds.xMax,cardBounds.xMax),Mathf.Max(bounds.yMax,cardBounds.yMax)):cardBounds;
            any=true;
        }
        return any;
    }
}
