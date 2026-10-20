using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Compact store tile: the offer's name, its availability, and a buy button that always shows
/// the price. Its rules text opens beside the tile when the tile is clicked (ContextualTooltipUI);
/// a selected Supply or Stake tile also carries the quantity controls.
/// </summary>
public sealed class ShopOfferUI : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
{
    const float TitleHeight = 58f;
    const float ButtonRowHeight = 60f;
    // Gear and Trick offers show their item art down the tile's left side; name and status sit to its
    // right. It fits the tile's base height (TitleHeight + ButtonRowHeight + 12), so tiles don't grow.
    const float IconSize = 106f;
    const float IconInset = 12f;
    /// <summary>Height of the Supply/Stake quantity row, which sits above the price button.</summary>
    public const float QuantityRowHeight = 56f;
    public static float QuantityRowBottom => ButtonRowHeight + 6f;

    bool _selected,_hover,_focus;
    bool _sizing;
    float _lastWidth;
    void OnRectTransformDimensionsChange()
    {
        if(_sizing||Offer==null||!statusLabel)return;
        float width=((RectTransform)transform).rect.width;
        if(Mathf.Abs(width-_lastWidth)<1)return;
        _lastWidth=width;_sizing=true;SetSelected(_selected);_sizing=false;
    }
    public TMP_Text titleLabel,priceLabel,statusLabel,descriptionLabel;
    public Button purchaseButton;
    public ShopOffer Offer { get; private set; }
    /// <summary>Whether this tile leaves room for the Supply/Stake quantity controls.</summary>
    public bool HasQuantityControls => _selected&&Offer!=null&&Offer.kind is ShopOfferKind.Supply or ShopOfferKind.Investment;
    public void Bind(ShopOffer offer,string availability)
    {
        Offer=offer;
        if(!titleLabel)titleLabel=GetComponentInChildren<TMP_Text>();
        if(!statusLabel)statusLabel=Label("OfferAvailability",.05f,.06f,.95f,.22f);
        var surface=GetComponent<EditorialSurface>();if(!surface)surface=gameObject.AddComponent<EditorialSurface>();surface.treatment=EditorialSurface.Treatment.Paper;
        DealbreakUIStyle.Frame(transform,true);
        var old=GetComponent<EditorialButton>();if(old)old.enabled=false;
        titleLabel.text=offer.DisplayName;
        ItemIconUI.ShowTopLeft(transform,offer.IconSprite,IconSize,new Vector2(IconInset,IconInset));
        statusLabel.text=availability?.Replace("Backpack Full","Trick slots full");
        // The price lives on the buy button and the rules text in the click detail.
        if(priceLabel)priceLabel.gameObject.SetActive(false);
        if(descriptionLabel)descriptionLabel.gameObject.SetActive(false);
        var theme=DealbreakUIStyle.Theme;
        if(theme)
        {
            DealbreakUIStyle.Text(titleLabel,theme.headingFont,29,theme.ink);
            DealbreakUIStyle.Text(statusLabel,theme.bodyFont,18,theme.brick);
        }
        foreach(var label in new[]{titleLabel,statusLabel})
        {label.alignment=TextAlignmentOptions.TopLeft;label.overflowMode=TextOverflowModes.Overflow;label.margin=Vector4.zero;}
        titleLabel.textWrappingMode=TextWrappingModes.NoWrap;titleLabel.overflowMode=TextOverflowModes.Ellipsis;
        if(!purchaseButton)
        {
            var control=DealbreakUIStyle.Child(transform,"OfferBuy",typeof(Image),typeof(Button));
            purchaseButton=control.GetComponent<Button>();purchaseButton.targetGraphic=control.GetComponent<Image>();
            var text=DealbreakUIStyle.Child(control,"BuyLabel",typeof(TextMeshProUGUI));DealbreakUIStyle.Place(text,0,0,1,1);
            text.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Center;
        }
        SetSelected(false);
    }
    public void SetSelected(bool selected)
    {
        _selected=selected;
        if(Offer==null||!statusLabel)return;
        bool hasIcon=ItemIconUI.Shown(transform)!=null;
        float textLeft=hasIcon?IconInset+IconSize+14:0;float textAnchor=hasIcon?0:.05f;
        float width=Mathf.Max(180,((RectTransform)transform.parent).rect.width*.90f-textLeft);
        bool hasStatus=!string.IsNullOrEmpty(statusLabel.text);
        float statusHeight=hasStatus?statusLabel.GetPreferredValues(statusLabel.text,width,0).y+6:0;
        float height=TitleHeight+statusHeight+ButtonRowHeight+12+(HasQuantityControls?QuantityRowHeight:0);
        var layout=GetComponent<LayoutElement>();if(!layout)layout=gameObject.AddComponent<LayoutElement>();layout.minHeight=layout.preferredHeight=height;
        var title=titleLabel.rectTransform;title.anchorMin=new Vector2(textAnchor,1);title.anchorMax=new Vector2(.95f,1);title.offsetMin=new Vector2(textLeft,-TitleHeight);title.offsetMax=new Vector2(0,-14);
        var status=statusLabel.rectTransform;status.anchorMin=new Vector2(textAnchor,1);status.anchorMax=new Vector2(.95f,1);status.pivot=new Vector2(.5f,1);status.anchoredPosition=new Vector2(textLeft/2,-TitleHeight);status.sizeDelta=new Vector2(-textLeft,statusHeight);
        statusLabel.gameObject.SetActive(hasStatus);
        purchaseButton.gameObject.SetActive(true);
        var buy=(RectTransform)purchaseButton.transform;buy.anchorMin=new Vector2(.58f,0);buy.anchorMax=new Vector2(.95f,0);buy.offsetMin=new Vector2(0,12);buy.offsetMax=new Vector2(0,12+ButtonRowHeight-14);
        RefreshBorder();
    }
    void RefreshBorder(){var frame=transform.Find("Editorial Frame")?.GetComponent<EditorialFrame>();var theme=DealbreakUIStyle.Theme;if(frame&&theme){frame.rim=_selected?theme.brick:_hover||_focus?theme.ochre:theme.ink;frame.border=_selected||_hover||_focus?5:3;frame.Refresh();}}
    public void OnPointerEnter(PointerEventData _){_hover=true;RefreshBorder();}
    public void OnPointerExit(PointerEventData _){_hover=false;RefreshBorder();}
    public void OnSelect(BaseEventData _){_focus=true;RefreshBorder();}
    public void OnDeselect(BaseEventData _){_focus=false;RefreshBorder();}
    public void RefreshPurchase(string reason,int cost)
    {
        if(!purchaseButton)return;
        purchaseButton.interactable=string.IsNullOrEmpty(reason)&&!GameplayInputGate.IsBlocked;
        purchaseButton.GetComponentInChildren<TMP_Text>(true).text=$"{cost} Credits";
        DealbreakUIStyle.Button(purchaseButton,EditorialButton.Role.Primary);
        if(statusLabel&&statusLabel.text!=(reason??string.Empty).Replace("Backpack Full","Trick slots full"))
        {
            statusLabel.text=(reason??string.Empty).Replace("Backpack Full","Trick slots full");
            if(!_sizing){_sizing=true;SetSelected(_selected);_sizing=false;}
        }
    }
    TMP_Text Label(string name,float x1,float y1,float x2,float y2)
    {var r=DealbreakUIStyle.Child(transform,name,typeof(TextMeshProUGUI));DealbreakUIStyle.Place(r,x1,y1,x2,y2);return r.GetComponent<TMP_Text>();}
}
