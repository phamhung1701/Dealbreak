using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>One shared detail frame for all screens, with live data and bounded scrolling.</summary>
public sealed class ContextualTooltipUI : MonoBehaviour
{
    public static ContextualTooltipUI Instance {get;private set;}
    public GameObject panel;
    public TMP_Text title,body;
    public ScrollRect scroll;
    public bool HasPinnedDetail => _pinned!=null;
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

    /// <summary>
    /// Registers a panel a tooltip must step around, such as the Trick and Gear action panels that
    /// open beside their slots. Inactive or destroyed panels are ignored.
    /// </summary>
    public static void RegisterObstacle(RectTransform obstacle){if(obstacle&&!Obstacles.Contains(obstacle))Obstacles.Add(obstacle);}
    void OnEnable(){Instance=this;Hide();}
    void OnDisable(){if(Instance==this)Instance=null;Hide();}
    public void Inspect(MonoBehaviour source){if(!source)return;_source=source;_dismissed=null;_leaveUntil=float.PositiveInfinity;}
    public void Leave(MonoBehaviour source){if(_source==source)_leaveUntil=Time.unscaledTime+.18f;}
    public void Pin(MonoBehaviour source){_pinned=_pinned==source?null:source;_dismissed=null;}
    public bool DismissPinned()
    {
        var card=CardManager.Instance?.selectedCard?.GetComponent<CombatInspectUI>();
        var gear=Object.FindFirstObjectByType<GearRailUI>()?.GetComponentsInChildren<GearIconSlotUI>().FirstOrDefault(g=>g.IsDetailSelected)?.GetComponent<CombatInspectUI>();
        MonoBehaviour selected=_pinned?_pinned:card?card:gear;
        if(!selected||selected==_dismissed)return false;
        _dismissed=selected;
        _source=_pinned=null;Hide();return true;
    }
    void Hide(){if(panel){panel.SetActive(false);var group=panel.GetComponent<CanvasGroup>();if(group)group.alpha=0;}_lastHeading=_lastBody=null;}
    void LateUpdate()
    {
        if(!panel||!body||!title)return;
        bool overPanel=panel.activeSelf&&Mouse.current!=null&&RectTransformUtility.RectangleContainsScreenPoint(
            (RectTransform)panel.transform,Mouse.current.position.ReadValue(),null);
        bool inspected=_source is CombatInspectUI combat?combat.IsInspected:_source is ContextualInfoTrigger data&&data.IsInspected;
        MonoBehaviour candidate=_source&&(inspected||Time.unscaledTime<_leaveUntil||overPanel)?_source:_pinned;
        if(!candidate&&CardManager.Instance?.selectedCard!=null)
            candidate=CardManager.Instance.selectedCard.GetComponent<CombatInspectUI>();
        if(!candidate)
            candidate=Object.FindFirstObjectByType<GearRailUI>()?.GetComponentsInChildren<GearIconSlotUI>()
                .FirstOrDefault(g=>g.IsDetailSelected)?.GetComponent<CombatInspectUI>();
        if(candidate==_dismissed||!Visible(candidate)||!TryDescribe(candidate,out var info)){Hide();return;}
        if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame&&!overPanel&&_pinned&&
            !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_pinned.transform,Mouse.current.position.ReadValue(),null))
        {DismissPinned();return;}
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
            string mod=card.Enhancement?$"\n\nMOD • {card.Enhancement.displayName}\n{card.Enhancement.description}":string.Empty;
            string preview=string.Empty;
            if(combat&&combat.currentState==GameState.EnemyAttacking&&CardManager.Instance?.SelectedCards.Count>0&&
               combat.TryPreviewDefense(CardManager.Instance.SelectedCards,out int blocked,out int remaining))
                preview=$"\n\nBlocks {blocked} pending attacks\nRemaining damage {remaining}";
            info=new("Card",card.DisplayName,$"ATK {attack}    DEF {block}{mod}{preview}");return true;
        }
        if(source.TryGetComponent<EnemyDisplayUI>(out var enemy)&&enemy.DisplayedEnemy!=null&&!enemy.DisplayedEnemy.IsDefeated)
        {
            var data=enemy.DisplayedEnemy;
            string traits=data.type.abilities==null?string.Empty:string.Join("\n\n",data.type.abilities.Where(a=>a).Select(a=>$"{a.displayName}\n{a.description}"));
            string reward=data.type.sourceCard?$"\n\nReward • {data.type.sourceCard.DisplayName}":string.Empty;
            info=new(data.type.sourceCard?"Headliner":"Enemy",enemy.combatPortraits?enemy.combatPortraits.DisplayName(data):data.DisplayName,$"HP {data.currentHp}/{data.maxHp} • Attack {data.EffectiveAttack}\n{enemy.DetailGuidance}\n\n{traits}{reward}");return true;
        }
        if(source.TryGetComponent<GearIconSlotUI>(out var gear)&&gear.Gear)
        {info=new("Gear",gear.Gear.displayName,gear.Gear.description);return true;}
        if(source.TryGetComponent<DrawDeckStackUI>(out _)&&CardManager.Instance!=null)
        {var c=CardManager.Instance;info=new("Deck","Draw Pile",$"Draw {c.deck.Count}\nDiscard {c.discardPile.Count}\nHand {c.HandCount}/{c.HandCapacity}");return true;}
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
        float width=slotDetail?Mathf.Min(220,parent.rect.width*.22f):Mathf.Min(390,parent.rect.width*.38f);
        float height=Mathf.Min(parent.rect.height*.65f,Mathf.Clamp(body.GetPreferredValues(info.Body,width-44,0).y+140,220,slotDetail?360:440));
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new(width,height);

        var anchor=view&&view.visualRoot?view.visualRoot:(RectTransform)source.transform;
        var target=BoundsIn(parent,anchor);
        var size=new Vector2(width,height);
        var order=handCard?HandOrder:BesideOrder;
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

    // Steps past open action panels to their right, and lifts details off the hand unless they describe a hand card.
    Vector2 Avoid(RectTransform parent,Vector2 center,Vector2 size,RectTransform self,bool handCard)
    {
        for(int pass=0;pass<2;pass++)
            for(int i=Obstacles.Count-1;i>=0;i--)
            {
                var obstacle=Obstacles[i];
                if(!obstacle){Obstacles.RemoveAt(i);continue;}
                if(obstacle==self||!obstacle.gameObject.activeInHierarchy)continue;
                var bounds=BoundsIn(parent,obstacle);
                if(Overlaps(center,size,bounds))center.x=bounds.xMax+Gap+size.x*.5f;
            }
        if(!handCard&&GetHandBounds(parent,out var hand)&&Overlaps(center,size,hand))
            center.y=hand.yMax+Gap+size.y*.5f;
        return center;
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
