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
    Vector2 _handAnchorMin,_handAnchorMax,_countAnchorMin,_countAnchorMax;
    // The compact hand fills the strip under route, Black Market and Incident panels, so its
    // size counter moves beside it instead of under it.
    static readonly Vector2 CompactHandMin=new(.17f,.02f),CompactHandMax=new(.83f,.46f);
    static readonly Vector2 CompactCountMin=new(.835f,.04f),CompactCountMax=new(.91f,.14f);
    void Start()
    {
        if(!handFrame&&actions&&actions.handField)handFrame=actions.handField.transform.parent as RectTransform;
        if(handFrame){_handAnchorMin=handFrame.anchorMin;_handAnchorMax=handFrame.anchorMax;}
        var enemies=FindFirstObjectByType<EnemyGroupUI>();
        _enemyContainer=enemies?enemies.enemyContainer:null;
        if(handCount){_countAnchorMin=handCount.rectTransform.anchorMin;_countAnchorMax=handCount.rectTransform.anchorMax;}
        RefreshStyles();BindTricks();BindControls();PositionSortControls();
        var canvas=actions?actions.GetComponentInParent<Canvas>():GetComponentInParent<Canvas>();
        if(canvas){ScreenShakeUI.Install(canvas.rootCanvas);EnemyDropUI.Install(canvas.rootCanvas);}
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
            var trigger=button.GetComponent<ContextualInfoTrigger>()??button.gameObject.AddComponent<ContextualInfoTrigger>();
            trigger.pinOnClick=true;
            trigger.describe=()=>{
                var cards=CardManager.Instance;var item=cards?.GetConsumableInstanceAtSlot(slot);if(item==null)return null;
                return new ContextualInfo("Trick",item.Definition.displayName,$"{item.Definition.description}\n\nStack {cards.GetConsumableStackCountAtSlot(slot)} • Charges {item.RemainingCharges}/{item.Definition.uses}");
            };
        }
    }
    void BindControls()
    {
        if(!actions)return;
        // Attack and Block show no hover detail.
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
            var headliner=status.headlinerProgressLabel.GetComponent<ContextualInfoTrigger>();
            if(headliner)headliner.describe=()=>new ContextualInfo("Headliner",RunManager.Instance?.CurrentHeadliner?.enemyName,RunManager.Instance?.CurrentHeadlinerDescription);
            var progress=status.runProgressLabel.GetComponent<ContextualInfoTrigger>();
            if(progress)progress.describe=()=>new ContextualInfo("District",status.runProgressLabel.text,null);
        }
    }
    void LateUpdate()
    {
        if(handCount&&CardManager.Instance!=null)handCount.text=$"{CardManager.Instance.HandCount}/{CardManager.Instance.HandCapacity}";
        if(trickCapacity&&CardManager.Instance!=null)trickCapacity.text=$"{CardManager.Instance.BackpackSlotsUsed}/{CardManager.Instance.BackpackCapacity}";
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
        if(handCount)
        {
            var count=handCount.rectTransform;
            count.anchorMin=Vector2.Lerp(count.anchorMin,compactHand?CompactCountMin:_countAnchorMin,ease);
            count.anchorMax=Vector2.Lerp(count.anchorMax,compactHand?CompactCountMax:_countAnchorMax,ease);
            if(count.parent&&count.GetSiblingIndex()!=count.parent.childCount-1)count.SetAsLastSibling();
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
            if(actions.recoverButton)actions.recoverButton.gameObject.SetActive(CombatManager.Instance.currentState==GameState.PlayerTurn&&cards.HandCount==0);
        }
        if(Time.unscaledTime<_nextStyleCheck)return;_nextStyleCheck=Time.unscaledTime+.35f;
        if(market&&market.panel&&market.panel.activeInHierarchy&&CardManager.Instance)
        {
            int credits=CardManager.Instance.credits;
            if(credits!=_lastShopCredits){_lastShopCredits=credits;market.RefreshPresentation();}
        }
        else _lastShopCredits=int.MinValue;
        int signature=GetComponentsInChildren<Button>(true).Length+((overlay?1:0)<<16);
        if(signature!=_lastSignature){_lastSignature=signature;RefreshStyles();}
    }
}
