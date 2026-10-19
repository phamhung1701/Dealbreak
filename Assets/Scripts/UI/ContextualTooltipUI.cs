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
    RectTransform _trickActionsPanel;
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
            string mod=card.Enhancement?$"\n\nMOD • {card.Enhancement.displayName}\n{card.Enhancement.description}":"\n\nNo Mod attached.";
            string preview=string.Empty;
            if(combat&&combat.currentState==GameState.EnemyAttacking&&CardManager.Instance?.SelectedCards.Count>0&&
               combat.TryPreviewDefense(CardManager.Instance.SelectedCards,out int blocked,out int remaining))
                preview=$"\n\nBlocks {blocked} pending attacks\nRemaining damage {remaining}";
            info=new("Card",card.DisplayName,$"ATK {attack}    DEF {block}{mod}{preview}\n\nAttack power is before target reactions, action bonuses and chance effects.");return true;
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
    bool GetHandBounds(RectTransform parent,out float left,out float right,out float bottom,out float top)
    {
        left=bottom=float.PositiveInfinity;right=top=float.NegativeInfinity;
        var holder=CardManager.Instance?.handField?.cardsHolder;if(!holder)return false;
        holder.GetComponentsInChildren(false,_positionHandViews);
        foreach(var view in _positionHandViews)
        {
            if(!view||view.data==null)continue;
            var card=view.visualRoot?view.visualRoot:view.transform as RectTransform;if(!card)continue;
            card.GetWorldCorners(_positionCorners);
            foreach(var corner in _positionCorners)
            {
                var point=parent.InverseTransformPoint(corner);
                left=Mathf.Min(left,point.x);right=Mathf.Max(right,point.x);
                bottom=Mathf.Min(bottom,point.y);top=Mathf.Max(top,point.y);
            }
        }
        return !float.IsInfinity(left);
    }

    void PositionTrickTooltip(MonoBehaviour source,RectTransform rect,RectTransform parent,float width,float height)
    {
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(width,height);
        var actions=source.GetComponentInParent<Canvas>()?.GetComponent<ActionButtonsUI>();
        float controlsLeft=float.PositiveInfinity,controlsRight=float.NegativeInfinity;
        if(actions&&actions.backpackSlotButtons!=null)
            foreach(var button in actions.backpackSlotButtons)
            {
                if(!button||!button.gameObject.activeInHierarchy)continue;
                var control=button.transform as RectTransform;control.GetWorldCorners(_positionCorners);
                for(int i=0;i<_positionCorners.Length;i++){float controlX=parent.InverseTransformPoint(_positionCorners[i]).x;controlsLeft=Mathf.Min(controlsLeft,controlX);controlsRight=Mathf.Max(controlsRight,controlX);}
            }
        float halfWidth=width*.5f,halfHeight=height*.5f;
        float x=float.IsInfinity(controlsRight)?parent.rect.xMin+halfWidth+12:controlsRight+halfWidth+12;
        if(x+halfWidth>parent.rect.xMax-12&&!float.IsInfinity(controlsLeft))x=controlsLeft-halfWidth-12;

        float handTop=float.NegativeInfinity;
        var holder=(actions&&actions.handField?actions.handField:CardManager.Instance?.handField)?.cardsHolder;
        if(holder)
        {
            holder.GetComponentsInChildren(false,_positionHandViews);
            foreach(var view in _positionHandViews)
            {
                if(!view||view.data==null)continue;
                var card=view.visualRoot?view.visualRoot:view.transform as RectTransform;if(!card)continue;
                card.GetWorldCorners(_positionCorners);
                for(int i=0;i<_positionCorners.Length;i++)handTop=Mathf.Max(handTop,parent.InverseTransformPoint(_positionCorners[i]).y);
            }
            if(float.IsNegativeInfinity(handTop)){holder.GetWorldCorners(_positionCorners);for(int i=0;i<_positionCorners.Length;i++)handTop=Mathf.Max(handTop,parent.InverseTransformPoint(_positionCorners[i]).y);}
        }
        float y=float.IsNegativeInfinity(handTop)?0:handTop+halfHeight+12;
        if(!_trickActionsPanel&&actions)_trickActionsPanel=actions.transform.Find("Selected Consumable Actions") as RectTransform;
        if(_trickActionsPanel&&_trickActionsPanel.gameObject.activeInHierarchy)
        {
            _trickActionsPanel.GetWorldCorners(_positionCorners);float actionTop=float.NegativeInfinity;
            for(int i=0;i<_positionCorners.Length;i++)actionTop=Mathf.Max(actionTop,parent.InverseTransformPoint(_positionCorners[i]).y);
            y=Mathf.Max(y,actionTop+halfHeight+12);
        }
        rect.anchoredPosition=new(Mathf.Clamp(x,parent.rect.xMin+halfWidth+12,parent.rect.xMax-halfWidth-12),
            Mathf.Clamp(y,parent.rect.yMin+halfHeight+12,parent.rect.yMax-halfHeight-12));
    }

    void Position(MonoBehaviour source,ContextualInfo info)
    {
        var rect=(RectTransform)panel.transform;var parent=(RectTransform)rect.parent;
        var panelGroup=panel.GetComponent<CanvasGroup>();if(panelGroup)panelGroup.ignoreParentGroups=true;
        var layer=panel.GetComponent<Canvas>();if(layer){layer.overrideSorting=true;layer.sortingOrder=Mathf.Max(6,source.GetComponentsInParent<Canvas>().Select(c=>c.sortingOrder).DefaultIfEmpty().Max()+3);}
        bool hand=source.GetComponent<CardView>()?.homeField!=null;
        bool enemy=source.GetComponent<EnemyDisplayUI>()!=null;
        bool trick=source is ContextualInfoTrigger&&source.GetComponent<Button>()&&source.name.StartsWith("BackpackSlot");
        if (DealbreakUIRuntime.CombatActive && parent.GetComponent<SharedContentStageUI>() &&
            (hand || enemy || source.GetComponent<DrawDeckStackUI>() ||
                source is ContextualInfoTrigger && source.GetComponent<Button>() && !trick))
        {
            // Combat inspection stays in the dedicated side rail; Gear/Trick use slot-adjacent placement below.
            DealbreakUIStyle.Place(rect, .889f, .525f, .992f, .89f);
            return;
        }
        bool slotDetail=trick||source.GetComponent<GearIconSlotUI>()!=null;
        if(trick&&!_trickActionsPanel)
        {
            var actionCanvas=source.GetComponentInParent<Canvas>();
            if(actionCanvas)_trickActionsPanel=actionCanvas.transform.Find("Selected Consumable Actions") as RectTransform;
        }
        bool trickActionsOpen=trick&&_trickActionsPanel&&_trickActionsPanel.gameObject.activeInHierarchy;
        float width=slotDetail?Mathf.Min(220,parent.rect.width*.22f):Mathf.Min(390,parent.rect.width*.38f);
        float height=Mathf.Min(parent.rect.height*.65f,Mathf.Clamp(body.GetPreferredValues(info.Body,width-44,0).y+140,220,slotDetail?(trick?(trickActionsOpen?360:260):360):440));
        if(trick){PositionTrickTooltip(source,rect,parent,width,height);return;}
        if(DealbreakUIRuntime.CombatActive&&(hand||enemy||source.GetComponent<DrawDeckStackUI>()||source is ContextualInfoTrigger&&source.GetComponent<Button>()&&!trick))
        {height=Mathf.Min(height,parent.rect.height*.38f);DealbreakUIStyle.Place(rect,.795f,.86f-height/parent.rect.height,.985f,.86f);return;}
        if(source.name.StartsWith("ShopOffer_")||source.name.StartsWith("DeckInspect_"))
        {
            rect.anchorMin=rect.anchorMax=new Vector2(.81f,.68f);rect.pivot=new(.5f,.5f);
            rect.sizeDelta=new(width,height);rect.anchoredPosition=Vector2.zero;
            return;
        }
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new(.5f,.5f);rect.sizeDelta=new(width,height);
        var anchor=(RectTransform)source.transform;
        var corners=_positionCorners;anchor.GetWorldCorners(corners);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,RectTransformUtility.WorldToScreenPoint(null,corners[3]),null,out var point);
        float x=point.x+width*.5f+16;
        if(x+width*.5f>parent.rect.xMax-16)x=point.x-anchor.rect.width-width*.5f-16;
        float y=point.y-height*.5f;
        if(source.GetComponent<GearIconSlotUI>()&&GetHandBounds(parent,out float handLeft,out float handRight,out float handBottom,out float handTop)&&
            x+width*.5f>handLeft&&x-width*.5f<handRight&&y+height*.5f>handBottom&&y-height*.5f<handTop)
            y=handTop+height*.5f+12;
        rect.anchoredPosition=new(Mathf.Clamp(x,parent.rect.xMin+width*.5f+12,parent.rect.xMax-width*.5f-12),
            Mathf.Clamp(y,parent.rect.yMin+height*.5f+12,parent.rect.yMax-height*.5f-12));
        var canvas=panel.GetComponent<Canvas>();
        if(canvas){canvas.overrideSorting=true;canvas.sortingOrder=Mathf.Max(6,source.GetComponentsInParent<Canvas>().Select(c=>c.sortingOrder).DefaultIfEmpty().Max()+3);}
    }
}
