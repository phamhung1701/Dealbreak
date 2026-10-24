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
    float _lastWidth;
    readonly Vector3[] _positionCorners=new Vector3[4];
    readonly List<CardView> _positionHandViews=new();

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
    void OnEnable(){Instance=this;LinkScrollbar();Hide();}
    // The scrollbar shows only while the detail overflows. An authoring pass once dropped its link,
    // leaving a fixed handle on every detail; the panel's own scrollbar is linked again here.
    void LinkScrollbar()
    {
        if(!scroll||!panel)return;
        if(!scroll.verticalScrollbar)scroll.verticalScrollbar=panel.transform.Find("Detail Scrollbar")?.GetComponent<Scrollbar>();
        if(scroll.verticalScrollbar)scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
    }
    void OnDisable(){if(Instance==this)Instance=null;Hide();}
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
        return _gearRail.GetComponentsInChildren<GearIconSlotUI>().FirstOrDefault(g=>g.IsDetailSelected)?.GetComponent<CombatInspectUI>();
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
    void Hide(){if(panel){panel.SetActive(false);var group=panel.GetComponent<CanvasGroup>();if(group)group.alpha=0;}_lastHeading=_lastBody=null;}
    void LateUpdate()
    {
        if(!panel||!body||!title)return;
        // While an item is dragged its drop preview is the detail; nothing else opens.
        if(ItemDragUI.IsDragging){if(panel.activeSelf)Hide();return;}
        bool overPanel=panel.activeSelf&&Mouse.current!=null&&RectTransformUtility.RectangleContainsScreenPoint(
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
        Position(candidate,info);
        panel.SetActive(true);
        var fade=panel.GetComponent<CanvasGroup>();if(fade)fade.alpha=Mathf.MoveTowards(fade.alpha,1,Time.unscaledDeltaTime*16);
        string heading=string.IsNullOrEmpty(info.Category)?info.Title:$"<size=55%>{info.Category.ToUpperInvariant()}</size>\n{info.Title}";
        if(heading!=_lastHeading||info.Body!=_lastBody||Mathf.Abs(body.rectTransform.rect.width-_lastWidth)>.5f)
        {
            title.text=heading;body.text=info.Body;
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,body.GetPreferredValues(info.Body,body.rectTransform.rect.width,0).y+16);
            if(_lastHeading!=heading&&scroll)scroll.verticalNormalizedPosition=1;
            _lastHeading=heading;_lastBody=info.Body;
            _lastWidth=body.rectTransform.rect.width;
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
        foreach(var group in source.GetComponentsInParent<CanvasGroup>())if(group.alpha<=.01f)return false;
        return true;
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
            info=new("Card",card.DisplayName,$"ATK {attack}    DEF {block}{mod}");return true;
        }
        if(source.TryGetComponent<EnemyDisplayUI>(out var enemy)&&enemy.DisplayedEnemy!=null&&!enemy.DisplayedEnemy.IsDefeated)
        {
            var data=enemy.DisplayedEnemy;
            // Ability names stand out from their rules text.
            string accent=DealbreakUIStyle.Theme?ColorUtility.ToHtmlStringRGB(DealbreakUIStyle.Theme.brick):"A8453A";
            string traits=data.type.abilities==null?string.Empty:string.Join("\n\n",data.type.abilities.Where(a=>a).Select(a=>$"<b><color=#{accent}>{a.displayName.ToUpperInvariant()}</color></b>\n{a.description}"));
            string reward=data.type.sourceCard?$"\n\nReward • {data.type.sourceCard.DisplayName}":string.Empty;
            // HP and attack already read under the enemy; the detail adds only what the board does not show.
            string detail=string.Join("\n\n",new[]{enemy.DetailGuidance,traits,reward.TrimStart('\n')}.Where(part=>!string.IsNullOrEmpty(part)));
            if(string.IsNullOrEmpty(detail))return false;
            info=new(data.type.sourceCard?"Headliner":"Enemy",enemy.combatPortraits?enemy.combatPortraits.DisplayName(data):data.DisplayName,detail);return true;
        }
        if(source.TryGetComponent<GearIconSlotUI>(out var gear)&&gear.Gear)
        {info=new("Gear",gear.Gear.displayName,GearRules.OwnedDescription(CardManager.Instance,gear.Gear,gear.InstanceId));return true;}
        if(source.TryGetComponent<DrawDeckStackUI>(out _)&&CardManager.Instance!=null)
        {var c=CardManager.Instance;info=new("Deck","Draw Pile",$"Draw {c.deck.Count}\nHand {c.HandCount}/{c.HandCapacity}");return true;}
        return false;
    }
    // Every detail opens next to what it describes. Hand cards sit on the bottom edge, so theirs
    // open above them; everything else opens to the right, then left, above or below as room allows.
    void Position(MonoBehaviour source,ContextualInfo info)
    {
        var rect=(RectTransform)panel.transform;var parent=(RectTransform)rect.parent;
        var panelGroup=panel.GetComponent<CanvasGroup>();if(panelGroup)panelGroup.ignoreParentGroups=true;
        // Without its own Canvas the panel draws in hierarchy order, under panels that come later (the route map).
        if(!panel.TryGetComponent<Canvas>(out var layer))
        {
            layer=panel.AddComponent<Canvas>();
            if(!panel.TryGetComponent<GraphicRaycaster>(out _))panel.AddComponent<GraphicRaycaster>();
        }
        layer.overrideSorting=true;layer.sortingOrder=SortingOrder;
        var view=source.GetComponent<CardView>();
        bool handCard=view&&view.homeField!=null;
        bool slotDetail=source.GetComponent<GearIconSlotUI>()!=null||
            source is ContextualInfoTrigger&&source.GetComponent<Button>()&&source.name.StartsWith("BackpackSlot");
        // A hand card's detail drops its heading (the card face already names the card), so it stays
        // low enough to clear the enemies and status line above the hand.
        bool compact=handCard;
        ShowHeading(!compact);
        float width=compact?Mathf.Min(320,parent.rect.width*.3f):slotDetail?Mathf.Min(340,parent.rect.width*.26f):Mathf.Min(390,parent.rect.width*.38f);
        float chrome=compact?CompactChrome:140f,minimum=compact?CompactMinimum:150f;
        float height=Mathf.Min(parent.rect.height*.65f,Mathf.Clamp(body.GetPreferredValues(info.Body,width-44,0).y+chrome,minimum,460));
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new(width,height);

        var anchor=view&&view.visualRoot?view.visualRoot:(RectTransform)source.transform;
        bool gearSlot=source.GetComponent<GearIconSlotUI>()!=null;
        // The Gear and Trick rows run across the header, so their details open under the slot.
        bool headerRow=slotDetail&&IsRow(GroupBounds(parent,anchor,gearSlot));
        var target=headerRow?BoundsIn(parent,anchor):slotDetail?SlotGroupBounds(parent,anchor,gearSlot):BoundsIn(parent,anchor);
        var size=new Vector2(width,height);
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
        rect.anchoredPosition=chosen-parent.rect.center;
    }

    // Compact: 16 above and below the viewport, plus the body's own line padding.
    const float HeadingTop=102f,CompactTop=16f,CompactChrome=52f,CompactMinimum=80f;
    void ShowHeading(bool show)
    {
        if(title&&title.gameObject.activeSelf!=show)title.gameObject.SetActive(show);
        var band=panel.transform.Find("DetailTitleBand");if(band&&band.gameObject.activeSelf!=show)band.gameObject.SetActive(show);
        float top=show?HeadingTop:CompactTop;
        if(scroll&&scroll.viewport&&!Mathf.Approximately(scroll.viewport.offsetMax.y,-top))scroll.viewport.offsetMax=new Vector2(scroll.viewport.offsetMax.x,-top);
        if(scroll&&scroll.verticalScrollbar&&scroll.verticalScrollbar.transform is RectTransform bar&&!Mathf.Approximately(bar.offsetMax.y,-top))
            bar.offsetMax=new Vector2(bar.offsetMax.x,-top);
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
        foreach(var group in obstacle.GetComponentsInParent<CanvasGroup>())if(group.alpha<=.01f)return false;
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
