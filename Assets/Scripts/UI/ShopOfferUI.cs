using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// A Black Market tile. Services are rows: the name, why it cannot be bought (never a plain lack of
/// Credits, which only disables the price button) and a price button that buys at once; Supply and
/// Stake rows always carry their quantity row: MIN − n + MAX. Goods are squares (<see cref="Square"/>):
/// big art, the price and the name; clicking one selects it (lime border) and shows BUY under it,
/// which confirms the purchase. Rules text opens beside the tile on click (ContextualTooltipUI).
/// </summary>
public sealed class ShopOfferUI : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
{
    // (owner, 2026-10-23) A narrow tile: big art on the left; beside it the name, and under the
    // name the price button with any status to its right.
    const float IconSize = 76f;
    const float IconInset = 10f;
    // Kept tight so the services column (two tiles with quantity rows) fits without scrolling. The
    // name box keeps a full 44: TMP's ellipsis drops a line that overflows it vertically.
    const float NameTop = 2f, NameHeight = 44f, BottomPadding = 8f;
    const float ButtonTop = 46f, ButtonHeight = 38f, ButtonWidth = 150f;
    /// <summary>Height of the Supply/Stake quantity row, under the tile's main row.</summary>
    public const float QuantityRowHeight = 44f;
    public static float QuantityRowBottom => 8f;

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
    /// <summary>Supply and Stake tiles carry their quantity controls at all times.</summary>
    public bool HasQuantityControls => Offer!=null&&Offer.kind is ShopOfferKind.Supply or ShopOfferKind.Investment;
    public Button minimumButton,decreaseButton,increaseButton,maximumButton;
    public TMP_Text quantityLabel;
    RectTransform _quantityRow;
    System.Action<int> _adjustQuantity;

    // (owner, 2026-10-24) Goods are squares in a grid; BUY hangs under the selected square.
    public bool Square { get; private set; }
    /// <summary>Height under a square for its BUY button; the grid leaves this much between rows.</summary>
    public const float SquareBuyHeight = 36f, SquareBuyGap = 6f;
    TMP_Text _priceTag;

    public void Bind(ShopOffer offer,string availability,System.Action<int> adjustQuantity=null,bool square=false)
    {
        Offer=offer;
        Square=square;
        _adjustQuantity=adjustQuantity;
        if(!titleLabel)titleLabel=GetComponentInChildren<TMP_Text>();
        if(!statusLabel)statusLabel=Label("OfferAvailability",.05f,.06f,.95f,.22f);
        var surface=GetComponent<EditorialSurface>();if(!surface)surface=gameObject.AddComponent<EditorialSurface>();surface.treatment=EditorialSurface.Treatment.Paper;
        DealbreakUIStyle.Frame(transform,true);
        var old=GetComponent<EditorialButton>();if(old)old.enabled=false;
        titleLabel.text=offer.DisplayName;
        if(square)ItemIconUI.Show(transform,offer.IconSprite,new Vector2(.18f,.27f),new Vector2(.82f,.85f));
        else ItemIconUI.ShowTopLeft(transform,offer.IconSprite,IconSize,new Vector2(IconInset,IconInset));
        ItemTierFxUI.Show(transform,offer.kind==ShopOfferKind.Gear?offer.gear:null);
        statusLabel.text=availability?.Replace("Backpack Full","Trick slots full");
        // The price lives on the buy button and the rules text in the click detail.
        if(priceLabel)priceLabel.gameObject.SetActive(false);
        if(descriptionLabel)descriptionLabel.gameObject.SetActive(false);
        var theme=DealbreakUIStyle.Theme;
        if(theme)
        {
            DealbreakUIStyle.Text(titleLabel,theme.headingFont,26,theme.paper);
            DealbreakUIStyle.Text(statusLabel,theme.bodyFont,16,theme.brick);
        }
        foreach(var label in new[]{titleLabel,statusLabel})
        {label.alignment=TextAlignmentOptions.TopLeft;label.overflowMode=TextOverflowModes.Overflow;label.margin=Vector4.zero;}
        titleLabel.textWrappingMode=TextWrappingModes.NoWrap;titleLabel.overflowMode=TextOverflowModes.Ellipsis;
        titleLabel.alignment=TextAlignmentOptions.MidlineLeft;
        if(square&&!_priceTag)
        {
            _priceTag=Label("PriceTag",0,0,1,1);
            _priceTag.raycastTarget=false;_priceTag.textWrappingMode=TextWrappingModes.NoWrap;
        }
        if(_priceTag)
        {
            _priceTag.gameObject.SetActive(square);
            if(theme)DealbreakUIStyle.Text(_priceTag,theme.labelFont?theme.labelFont:theme.headingFont,16,theme.paper);
        }
        if(square&&theme)
        {
            DealbreakUIStyle.Text(titleLabel,theme.headingFont,19,theme.paper);
            DealbreakUIStyle.Text(statusLabel,theme.bodyFont,13,theme.brick);
        }
        if(!purchaseButton)
        {
            var control=DealbreakUIStyle.Child(transform,"OfferBuy",typeof(Image),typeof(Button));
            purchaseButton=control.GetComponent<Button>();purchaseButton.targetGraphic=control.GetComponent<Image>();
            var text=DealbreakUIStyle.Child(control,"BuyLabel",typeof(TextMeshProUGUI));DealbreakUIStyle.Place(text,0,0,1,1);
            text.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Center;
        }
        if(HasQuantityControls)EnsureQuantityRow();
        else if(_quantityRow)_quantityRow.gameObject.SetActive(false);
        SetSelected(false);
    }

    void EnsureQuantityRow()
    {
        if(!_quantityRow)
        {
            _quantityRow=DealbreakUIStyle.Child(transform,"QuantityRow");
            minimumButton=QuantityButton("Quantity Lowest","MIN",int.MinValue/2);
            decreaseButton=QuantityButton("Decrease Quantity","−",-1);
            var value=DealbreakUIStyle.Child(_quantityRow,"Quantity",typeof(TextMeshProUGUI));
            quantityLabel=value.GetComponent<TMP_Text>();
            quantityLabel.alignment=TextAlignmentOptions.Center;
            quantityLabel.raycastTarget=false;
            increaseButton=QuantityButton("Increase Quantity","+",1);
            maximumButton=QuantityButton("Quantity Highest","MAX",int.MaxValue/2);
        }
        _quantityRow.gameObject.SetActive(true);
        var theme=DealbreakUIStyle.Theme;
        if(theme)DealbreakUIStyle.Text(quantityLabel,theme.numericFont,24,theme.paper);
    }

    Button QuantityButton(string name,string label,int delta)
    {
        var control=DealbreakUIStyle.Child(_quantityRow,name,typeof(Image),typeof(Button));
        var button=control.GetComponent<Button>();button.targetGraphic=control.GetComponent<Image>();
        var text=DealbreakUIStyle.Child(control,"Label",typeof(TextMeshProUGUI));DealbreakUIStyle.Place(text,0,0,1,1);
        var tmp=text.GetComponent<TMP_Text>();tmp.text=label;tmp.alignment=TextAlignmentOptions.Center;tmp.raycastTarget=false;
        button.onClick.AddListener(()=>_adjustQuantity?.Invoke(delta));
        // MIN and MAX are boxed buttons like − and +: their names avoid "min"/"max", which the
        // shared style pass reads as bare-text buttons.
        DealbreakUIStyle.Button(button,EditorialButton.Role.Secondary);
        if(label.Length>1)tmp.fontSize=16;
        return button;
    }

    /// <summary>Shows the current quantity and which steps the authoritative range allows.</summary>
    public void RefreshQuantity(int current,bool canLower,bool canRaise)
    {
        if(!HasQuantityControls||!_quantityRow)return;
        quantityLabel.text=current>0?current.ToString():"–";
        if(minimumButton)minimumButton.interactable=canLower;
        if(decreaseButton)decreaseButton.interactable=canLower;
        if(increaseButton)increaseButton.interactable=canRaise;
        if(maximumButton)maximumButton.interactable=canRaise;
    }

    public void SetSelected(bool selected)
    {
        _selected=selected;
        if(Offer==null||!statusLabel)return;
        if(Square){LayoutSquare();return;}
        bool hasIcon=ItemIconUI.Shown(transform)!=null;
        float textLeft=hasIcon?IconInset+IconSize+14:16;
        float rowWidth=Mathf.Max(200,((RectTransform)transform.parent).rect.width);
        float statusLeft=textLeft+ButtonWidth+10;
        float statusWidth=Mathf.Max(80,rowWidth-statusLeft-12);
        bool hasStatus=!string.IsNullOrEmpty(statusLabel.text);
        float statusHeight=hasStatus?statusLabel.GetPreferredValues(statusLabel.text,statusWidth,0).y+4:0;
        float mainHeight=Mathf.Max(hasIcon?IconSize+IconInset*2:0,ButtonTop+Mathf.Max(ButtonHeight,statusHeight)+BottomPadding);
        float quantity=HasQuantityControls?QuantityRowHeight:0;
        var layout=GetComponent<LayoutElement>();if(!layout)layout=gameObject.AddComponent<LayoutElement>();layout.minHeight=layout.preferredHeight=mainHeight+quantity;
        // The title box must fit a full line: TMP's ellipsis drops a line that overflows vertically.
        var title=titleLabel.rectTransform;title.anchorMin=new Vector2(0,1);title.anchorMax=new Vector2(1,1);title.pivot=new Vector2(.5f,1);
        title.offsetMin=new Vector2(textLeft,-NameTop-NameHeight);title.offsetMax=new Vector2(-12,-NameTop);
        var buy=(RectTransform)purchaseButton.transform;buy.anchorMin=buy.anchorMax=new Vector2(0,1);buy.pivot=new Vector2(0,1);
        buy.sizeDelta=new Vector2(ButtonWidth,ButtonHeight);buy.anchoredPosition=new Vector2(textLeft,-ButtonTop);
        purchaseButton.gameObject.SetActive(true);
        var status=statusLabel.rectTransform;status.anchorMin=status.anchorMax=new Vector2(0,1);status.pivot=new Vector2(0,1);
        status.sizeDelta=new Vector2(statusWidth,Mathf.Max(ButtonHeight,statusHeight));status.anchoredPosition=new Vector2(statusLeft,-ButtonTop);
        statusLabel.alignment=TextAlignmentOptions.MidlineLeft;
        statusLabel.gameObject.SetActive(hasStatus);
        if(HasQuantityControls&&_quantityRow)LayoutQuantityRow(rowWidth);
        RefreshBorder();
    }

    // The square: price across the top, art in the middle, the name (and any status) along the foot;
    // BUY hangs under the square, shown only while it is selected.
    void LayoutSquare()
    {
        void Box(RectTransform rect,float x1,float y1,float x2,float y2){rect.anchorMin=new Vector2(x1,y1);rect.anchorMax=new Vector2(x2,y2);rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        bool hasStatus=!string.IsNullOrEmpty(statusLabel.text);
        Box(titleLabel.rectTransform,.06f,hasStatus?.12f:.05f,.94f,hasStatus?.27f:.22f);
        titleLabel.alignment=TextAlignmentOptions.Center;
        Box(statusLabel.rectTransform,.06f,.02f,.94f,.13f);
        statusLabel.alignment=TextAlignmentOptions.Center;statusLabel.textWrappingMode=TextWrappingModes.NoWrap;
        statusLabel.overflowMode=TextOverflowModes.Ellipsis;
        statusLabel.gameObject.SetActive(hasStatus);
        if(_priceTag){Box(_priceTag.rectTransform,.07f,.86f,.93f,.97f);_priceTag.alignment=TextAlignmentOptions.MidlineLeft;}
        var buy=(RectTransform)purchaseButton.transform;
        buy.anchorMin=new Vector2(0,0);buy.anchorMax=new Vector2(1,0);buy.pivot=new Vector2(.5f,1);
        buy.sizeDelta=new Vector2(0,SquareBuyHeight);buy.anchoredPosition=new Vector2(0,-SquareBuyGap);
        purchaseButton.gameObject.SetActive(_selected);
        RefreshBorder();
    }

    // MIN − n + MAX across the bottom of the tile.
    void LayoutQuantityRow(float rowWidth)
    {
        _quantityRow.anchorMin=new Vector2(0,0);_quantityRow.anchorMax=new Vector2(1,0);_quantityRow.pivot=new Vector2(.5f,0);
        _quantityRow.offsetMin=new Vector2(16,QuantityRowBottom);_quantityRow.offsetMax=new Vector2(-16,QuantityRowBottom+QuantityRowHeight-8);
        var controls=new RectTransform[]{(RectTransform)minimumButton.transform,(RectTransform)decreaseButton.transform,
            quantityLabel.rectTransform,(RectTransform)increaseButton.transform,(RectTransform)maximumButton.transform};
        float[] widths={64,44,0,44,64};
        float available=rowWidth-32;
        widths[2]=Mathf.Max(56,available-(widths[0]+widths[1]+widths[3]+widths[4])-16);
        float x=0;
        for(int i=0;i<controls.Length;i++)
        {
            var rect=controls[i];
            rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,.5f);
            rect.offsetMin=new Vector2(x,0);rect.offsetMax=new Vector2(x+widths[i],0);
            x+=widths[i]+4;
        }
    }

    void RefreshBorder(){var frame=transform.Find("Editorial Frame")?.GetComponent<EditorialFrame>();var theme=DealbreakUIStyle.Theme;if(frame&&theme){frame.rim=_selected?theme.ochre:_hover||_focus?theme.paper:theme.slate;frame.border=_selected||_hover||_focus?2:1;frame.Refresh();}}
    public void OnPointerEnter(PointerEventData _){_hover=true;RefreshBorder();}
    public void OnPointerExit(PointerEventData _){_hover=false;RefreshBorder();}
    public void OnSelect(BaseEventData _){_focus=true;RefreshBorder();}
    public void OnDeselect(BaseEventData _){_focus=false;RefreshBorder();}

    /// <param name="reason">Why it cannot be bought, other than Credits; empty when only Credits are short.</param>
    /// <param name="affordable">False disables the price button without any text.</param>
    public void RefreshPurchase(string reason,int cost,bool affordable=true)
    {
        if(!purchaseButton)return;
        purchaseButton.interactable=affordable&&string.IsNullOrEmpty(reason)&&!GameplayInputGate.IsBlocked;
        string price=cost>1?$"{cost} Credits":cost==1?"1 Credit":"Free";
        // A square shows its price on the tile; its button is the confirmation.
        purchaseButton.GetComponentInChildren<TMP_Text>(true).text=Square?"BUY":price;
        if(_priceTag)_priceTag.text=price;
        DealbreakUIStyle.Button(purchaseButton,EditorialButton.Role.Primary);
        string shown=(reason??string.Empty).Replace("Backpack Full","Trick slots full");
        if(statusLabel&&statusLabel.text!=shown)
        {
            statusLabel.text=shown;
            if(!_sizing){_sizing=true;SetSelected(_selected);_sizing=false;}
        }
    }
    TMP_Text Label(string name,float x1,float y1,float x2,float y2)
    {var r=DealbreakUIStyle.Child(transform,name,typeof(TextMeshProUGUI));DealbreakUIStyle.Place(r,x1,y1,x2,y2);return r.GetComponent<TMP_Text>();}
}
