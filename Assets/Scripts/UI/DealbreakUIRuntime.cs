using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation context and late-created control styling. No gameplay state is changed.</summary>
[DefaultExecutionOrder(100)]
public sealed class DealbreakUIRuntime : MonoBehaviour
{
    /// <summary>An attack is still landing or an enemy's defeat is still playing.</summary>
    public static bool BoardPresentationBusy => AttackCardPresentationUI.Instance && AttackCardPresentationUI.Instance.IsBusy;
    public static bool CombatActive => CombatManager.Instance!=null&&CombatManager.Instance.Enemies.Count>0&&
        CombatManager.Instance.currentState is GameState.PlayerTurn or GameState.EnemyAttacking;
    public TMP_Text handCount,trickCapacity;
    public RectTransform playerState;
    public RectTransform handFrame;
    public CanvasGroup[] combatGroups;
    public CanvasGroup[] inventoryGroups;
    public ActionButtonsUI actions;
    public PathScreenUI map;
    public ShopUI market;
    public RunEventUI incident;
    float _nextStyleCheck;
    int _lastSignature;
    readonly System.Collections.Generic.HashSet<Object> _styled=new();
    int _lastShopCredits=int.MinValue;
    bool? _lastDefenseLayout;
    bool _compactHealthLayout;
    RectTransform _enemyContainer;
    Vector2 _handAnchorMin,_handAnchorMax;
    // The compact hand fills the strip under route, Black Market and Incident panels; its hand
    // size travels with it (AttachHandCount).
    static readonly Vector2 CompactHandMin=new(.17f,.02f),CompactHandMax=new(.83f,.46f);
    void Start()
    {
        if(!handFrame&&actions&&actions.handField)handFrame=actions.handField.transform.parent as RectTransform;
        if(handFrame){_handAnchorMin=handFrame.anchorMin;_handAnchorMax=handFrame.anchorMax;}
        var enemies=FindFirstObjectByType<EnemyGroupUI>();
        _enemyContainer=enemies?enemies.enemyContainer:null;
        AttachHandCount();
        MergeStatusLines();
        RefreshStyles();BindTricks();BindControls();PositionSortControls();
        // Credits sit just above the HP panel, so they show wherever it does.
        if(playerState&&actions&&actions.TryGetComponent<RunStatusUI>(out var status))CreditsHudUI.Install(playerState,status.creditsLabel);
        // The run's character sits above the Credits.
        if(playerState&&actions)PlayerAvatarUI.Install(playerState);
        var canvas=actions?actions.GetComponentInParent<Canvas>():GetComponentInParent<Canvas>();
        if(canvas){ScreenShakeUI.Install(canvas.rootCanvas);EnemyDropUI.Install(canvas.rootCanvas);}
        // One trigger presenter, on the game HUD (the frontend overlay has no hand or HP panel).
        if(canvas&&actions)EffectTriggerPresenterUI.Install(canvas.rootCanvas);
        InstallInventoryPanels();
    }
    // (owner, 2026-10-23) Each header row sits on its own panel with a coloured heading block, so
    // Gear (slate) and Tricks (signal red) read apart.
    void InstallInventoryPanels()
    {
        var theme=DealbreakUIStyle.Theme;if(!actions||!theme)return;
        var rail=GetComponentInChildren<GearRailUI>(true);
        var gearHeading=rail?rail.transform.Find("GearHeader")?.GetComponent<TMP_Text>():null;
        if(rail&&rail.slotsRoot)InventoryPanelUI.Install("Gear Panel",(RectTransform)rail.transform,
            new[]{gearHeading?gearHeading.rectTransform:null,rail.capacityLabel?rail.capacityLabel.rectTransform:null,rail.slotsRoot},
            new[]{gearHeading,rail.capacityLabel},theme.slate,theme.paper);
        var slots=System.Array.FindAll(actions.backpackSlotButtons,b=>b);if(slots.Length==0)return;
        var area=(RectTransform)slots[0].transform.parent;
        var trickHeading=area.Find("TricksHeading")?.GetComponent<TMP_Text>();
        var members=new System.Collections.Generic.List<RectTransform>{trickHeading?trickHeading.rectTransform:null,trickCapacity?trickCapacity.rectTransform:null};
        foreach(var slot in slots)members.Add((RectTransform)slot.transform);
        InventoryPanelUI.Install("Tricks Panel",area,members.ToArray(),new[]{trickHeading,trickCapacity},theme.brick,theme.paper);
    }
    // The selection preview and the turn state share one line under the enemies: while cards are
    // selected their preview stands in for PLAYER TURN or DEFENSE, so it never collides with INCOMING.
    // Both lines and INCOMING are obstacles a hand card's detail steps around.
    void MergeStatusLines()
    {
        if(!actions||!actions.selectionLabel||!actions.turnStateLabel)return;
        var line=actions.selectionLabel.rectTransform;var turn=actions.turnStateLabel.rectTransform;
        line.SetParent(turn.parent,false);
        line.anchorMin=turn.anchorMin;line.anchorMax=turn.anchorMax;line.pivot=turn.pivot;
        line.offsetMin=turn.offsetMin;line.offsetMax=turn.offsetMax;
        actions.selectionLabel.alignment=TextAlignmentOptions.Center;
        ContextualTooltipUI.RegisterObstacle(line);ContextualTooltipUI.RegisterObstacle(turn);
        if(actions.pendingDamageLabel)ContextualTooltipUI.RegisterObstacle(actions.pendingDamageLabel.rectTransform);
    }
    void ShowOneStatusLine()
    {
        if(!actions||!actions.selectionLabel||!actions.turnStateLabel)return;
        bool preview=actions.selectionLabel.enabled&&!string.IsNullOrWhiteSpace(actions.selectionLabel.text);
        float alpha=preview?0f:1f;
        if(!Mathf.Approximately(actions.turnStateLabel.alpha,alpha))actions.turnStateLabel.alpha=alpha;
    }
    // (owner, 2026-10-23) The hand size rides at the hand's right end, as part of the hand, so it
    // travels with the hand between the board and the compact strip instead of on its own path.
    void AttachHandCount()
    {
        if(!handCount||!handFrame)return;
        var count=handCount.rectTransform;
        count.SetParent(handFrame,false);
        count.anchorMin=count.anchorMax=new Vector2(1,0);count.pivot=new Vector2(0,0);
        count.sizeDelta=new Vector2(120,64);count.anchoredPosition=new Vector2(14,8);
        handCount.alignment=TextAlignmentOptions.BottomLeft;
        handCount.textWrappingMode=TextWrappingModes.NoWrap;
        handCount.fontSize=28;
        handCount.raycastTarget=false;
    }
    // The hand frame widens on wide screens while the cards keep their spacing, so the hand size
    // follows the last card's slot rather than the frame's edge.
    readonly System.Collections.Generic.List<CardView> _handViews=new();
    readonly Vector3[] _cardCorners=new Vector3[4];
    void FollowHandEnd()
    {
        if(!handCount||!handFrame||CardManager.Instance==null)return;
        var holder=CardManager.Instance.handField?CardManager.Instance.handField.cardsHolder:null;
        float x=14f;
        if(holder)
        {
            holder.GetComponentsInChildren(false,_handViews);
            float right=float.NegativeInfinity;
            foreach(var view in _handViews)
            {
                if(!view||view.data==null||view.transform.parent!=holder)continue;
                ((RectTransform)view.transform).GetWorldCorners(_cardCorners);
                for(int i=0;i<4;i++)right=Mathf.Max(right,handFrame.InverseTransformPoint(_cardCorners[i]).x);
            }
            if(!float.IsNegativeInfinity(right))x=Mathf.Min(14f,right-handFrame.rect.xMax+14f);
        }
        var count=handCount.rectTransform;
        if(count.parent==handFrame&&!Mathf.Approximately(count.anchoredPosition.x,x))count.anchoredPosition=new Vector2(x,count.anchoredPosition.y);
    }
    void PositionSortControls()
    {
        if (!actions || !actions.playButton) return;
        var bar = actions.playButton.transform.parent as RectTransform;
        if (!bar) return;
        var min = bar.anchorMin;
        var size = bar.anchorMax - min;
        // The action bar reserves its first 18% before Attack. Stack sorting there,
        // using anchors so authored offsets cannot push the controls over player HP.
        if (actions.rankSortButton && actions.rankSortButton.transform.parent == bar.parent)
            DealbreakUIStyle.Place(actions.rankSortButton.transform, min.x + size.x * .01f,
                min.y + size.y * .52f, min.x + size.x * .17f, min.y + size.y * .94f);
        if (actions.suitSortButton && actions.suitSortButton.transform.parent == bar.parent)
            DealbreakUIStyle.Place(actions.suitSortButton.transform, min.x + size.x * .01f,
                min.y + size.y * .06f, min.x + size.x * .17f, min.y + size.y * .48f);
    }

    // Styles each control once; restyling live ones reset fonts and colors for a frame (a visible flash).
    public void RefreshStyles(){_styled.RemoveWhere(item=>!item);DealbreakUIStyle.ApplyHierarchy(transform,_styled);}
    void BindTricks()
    {
        if(!actions)return;
        ContextualTooltipUI.RegisterSlotGroup(System.Array.ConvertAll(actions.backpackSlotButtons,b=>b?(RectTransform)b.transform:null));
        for(int i=0;i<actions.backpackSlotButtons.Length;i++)
        {
            var button=actions.backpackSlotButtons[i];if(!button)continue;
            int slot=i;
            // The slot's own click (ActionButtonsUI) pins this detail, after a drag's click suppression.
            var trigger=button.GetComponent<ContextualInfoTrigger>()??button.gameObject.AddComponent<ContextualInfoTrigger>();
            trigger.describe=()=>{
                var cards=CardManager.Instance;var item=cards?.GetConsumableInstanceAtSlot(slot);if(item==null)return null;
                return new ContextualInfo("Trick",item.Definition.displayName,$"{ModSignature.TrickDescription(item.Definition)}\n\nStack {cards.GetConsumableStackCountAtSlot(slot)} • Charges {item.RemainingCharges}/{item.Definition.uses}");
            };
        }
    }
    void BindControls()
    {
        if(!actions)return;
        // Attack and Block show no detail; the others show theirs when clicked while unavailable.
        var buttons=new[]{actions.passButton,actions.recoverButton,actions.rankSortButton,actions.suitSortButton};
        var details=new[]{"Pass the current action; while blocking, take the pending damage.","Recover when your hand is empty.","Sort your current hand by rank.","Sort your current hand by suit."};
        for(int i=0;i<buttons.Length;i++)
        {
            var button=buttons[i];if(!button)continue;string description=details[i];
            var trigger=button.GetComponent<ContextualInfoTrigger>()??button.gameObject.AddComponent<ContextualInfoTrigger>();
            trigger.describe=()=>button==actions.passButton&&CombatManager.Instance?.currentState==GameState.EnemyAttacking
                ?new ContextualInfo("Defense","PASS",$"Take {CombatManager.Instance.pendingDamage} remaining damage. Shield and other combat effects resolve normally.")
                :button.IsInteractable()?null:new ContextualInfo("Combat action",button.GetComponentInChildren<TMP_Text>().text,
                description+"\n\nUnavailable in the current state.");
        }
        if(actions.GetComponent<RunStatusUI>() is RunStatusUI status)
        {
            // The header no longer names the District or Headliner; the progress line is an active Contract's.
            var progress=status.runProgressLabel.GetComponent<ContextualInfoTrigger>();
            if(progress)progress.describe=()=>new ContextualInfo("Contract",status.runProgressLabel.text,null);
        }
    }
    void LateUpdate()
    {
        if(handCount&&CardManager.Instance!=null)handCount.text=$"<size=55%>HAND</size>\n{CardManager.Instance.HandCount}/{CardManager.Instance.HandCapacity}";
        if(trickCapacity&&CardManager.Instance!=null)trickCapacity.text=$"{CardManager.Instance.BackpackSlotsUsed}/{CardManager.Instance.BackpackCapacity}";
        ShowOneStatusLine();
        FollowHandEnd();
        // A drop waiting on the board after a fight also brings the hand up, so Tricks can make room.
        // The board stays up until the last card lands and the last enemy's defeat has played.
        bool presenting=BoardPresentationBusy;
        bool board=CombatActive||presenting;
        bool overlay=map&&map.panel&&map.panel.activeInHierarchy||market&&market.panel&&market.panel.activeInHierarchy||
            incident&&incident.panel&&incident.panel.activeInHierarchy||
            !presenting&&RunManager.Instance!=null&&RunManager.Instance.PendingEnemyDrop!=null;
        bool compactHand=overlay&&!RunResultUI.IsShowingResult;
        float ease=1f-Mathf.Exp(-Time.unscaledDeltaTime*9f);
        if(handFrame)
        {
            handFrame.anchorMin=Vector2.Lerp(handFrame.anchorMin,compactHand?CompactHandMin:_handAnchorMin,ease);
            handFrame.anchorMax=Vector2.Lerp(handFrame.anchorMax,compactHand?CompactHandMax:_handAnchorMax,ease);
        }
        bool choosingCard=CardManager.Instance&&CardManager.Instance.IsHandEnhancementTargeting;
        if(combatGroups!=null)for(int i=0;i<combatGroups.Length;i++)
        {
            var group=combatGroups[i];if(!group)continue;
            // Keep the original hand renderer visible in a compact bottom strip on route/shop screens.
            bool handVisual=compactHand&&i==0;
            bool handCounter=compactHand&&i==1;
            // The HP panel stays beside the compact hand: healing Tricks are dropped on it.
            bool health=compactHand&&playerState&&group.transform==playerState;
            bool kept=handVisual||handCounter||health;
            // After the fight ends only the enemies, hand and HP stay up while the last defeat plays,
            // and the draw pile while a Headliner reward card flies into it.
            var rewardDeck=AttackCardPresentationUI.Instance?AttackCardPresentationUI.Instance.RewardDeckTarget:null;
            bool shownWhilePresenting=CombatActive||i<=1||playerState&&group.transform==playerState||
                _enemyContainer&&_enemyContainer.IsChildOf(group.transform)||rewardDeck&&rewardDeck.IsChildOf(group.transform);
            bool hidden=overlay&&!kept||RunResultUI.IsShowingResult||(!board||!shownWhilePresenting)&&!choosingCard&&!kept||choosingCard&&i>1;
            group.alpha=hidden?0:1;group.blocksRaycasts=!hidden;group.interactable=!hidden;
        }
        // Beside the compact hand the HP panel is a single short bar in the bottom-left corner.
        if(compactHand&&playerState&&actions&&!_compactHealthLayout)
        {
            DealbreakUIStyle.Place(playerState,.025f,.035f,.2f,.16f);
            DealbreakUIStyle.Place(actions.playerHealthLabel.transform,.055f,.1f,.945f,.9f);
            _compactHealthLayout=true;
            _lastDefenseLayout=null;
        }
        bool hideInventory=RunResultUI.IsShowingResult;
        if(inventoryGroups!=null)foreach(var group in inventoryGroups)if(group){group.alpha=hideInventory?0:1;group.blocksRaycasts=!hideInventory;group.interactable=!hideInventory;}
        if(actions&&CombatActive)
        {
            bool defending=CombatManager.Instance.currentState==GameState.EnemyAttacking;
            if(playerState&&_lastDefenseLayout!=defending)
            {
                DealbreakUIStyle.Place(playerState,.025f,.035f,.235f,.25f);
                DealbreakUIStyle.Place(actions.playerHealthLabel.transform,.055f,.1f,.945f,.94f);
                _lastDefenseLayout=defending;
                _compactHealthLayout=false;
            }
            var cards=CardManager.Instance;
            if(actions.selectionLabel&&cards!=null&&cards.SelectedCards.Count==0&&!cards.IsEnemyTargeting&&!cards.IsHandEnhancementTargeting)
                actions.selectionLabel.text=cards.HandCount==0?actions.selectionLabel.text:string.Empty;
            else if(actions.selectionLabel&&cards!=null&&cards.SelectedCards.Count>0&&!cards.IsEnemyTargeting&&!cards.IsHandEnhancementTargeting&&CombatManager.Instance.currentState==GameState.PlayerTurn)
                actions.selectionLabel.text=CombatDetailPanelUI.SelectionSummary(cards);
            if(actions.playButton)actions.playButton.GetComponentInChildren<TMP_Text>().text=cards.IsEnemyTargeting?"CANCEL":"ATTACK";
            if(actions.recoverButton&&actions.recoverButton.gameObject.activeSelf)actions.recoverButton.gameObject.SetActive(false);
        }
        if(Time.unscaledTime<_nextStyleCheck)return;_nextStyleCheck=Time.unscaledTime+.35f;
        if(market&&market.panel&&market.panel.activeInHierarchy&&CardManager.Instance)
        {
            int credits=CardManager.Instance.credits;
            if(credits!=_lastShopCredits){_lastShopCredits=credits;market.RequestRefresh();}
        }
        else _lastShopCredits=int.MinValue;
        int signature=GetComponentsInChildren<Button>(true).Length+((overlay?1:0)<<16);
        if(signature!=_lastSignature){_lastSignature=signature;RefreshStyles();}
    }
}
