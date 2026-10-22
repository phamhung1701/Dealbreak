using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "ShopUI")]
public class ShopUI : MonoBehaviour
{
    [Header("References")]
    public GameObject panel;
    public Transform itemsContainer;
    public GameObject itemButtonPrefab;
    public Button continueButton;
    [UnityEngine.Serialization.FormerlySerializedAs("goldText")]
    public TMP_Text creditsText;
    public TMP_Text selectedOfferText;
    public Button buyButton;
    public bool storeComposition;
    Transform _controlHome;
    TMP_InputField _investmentStakeInput;
    TMP_Text _investmentResultText;
    Button _quantityDecreaseButton;
    Button _quantityIncreaseButton;
    Button _quantityMinimumButton;
    Button _quantityMaximumButton;
    ScrollRect _offersScroll;
    Transform _servicesContainer;
    RectTransform _selectedOfferRect;
    Vector2 _selectedOfferAnchorMax;

    string _selectedOfferId;
    public string SelectedOfferId => _selectedOfferId;
    public void RefreshPresentation() => RefreshItems();

    // The price button buys at once; for Supply and Stake it buys the quantity on the controls.
    public void PurchaseInline(string stableId)
    {
        if (GameplayInputGate.IsBlocked) return;
        if (_selectedOfferId != stableId) SelectOffer(stableId);
        if (_selectedOfferId != stableId) return;
        OnBuy(); // Existing RunManager transaction guards, identity checks and payment authority.
    }

    Button[] QuantityControls => new[] { _quantityMinimumButton, _quantityDecreaseButton, _quantityIncreaseButton, _quantityMaximumButton };

    void SetQuantityControlsActive(bool active)
    {
        foreach (var control in QuantityControls)
            if (control) control.gameObject.SetActive(active);
    }
    void ReturnQuantityControls()
    {
        if(!_controlHome)return;
        if(_investmentStakeInput)_investmentStakeInput.transform.SetParent(_controlHome,false);
        foreach(var control in QuantityControls)if(control)control.transform.SetParent(_controlHome,false);
    }
    void ShowInlineSelection()
    {
        if(!storeComposition)return;
        foreach(var view in panel.GetComponentsInChildren<ShopOfferUI>())
        {
            bool selected=view.Offer?.StableId==_selectedOfferId;
            view.SetSelected(selected);
            if(selected&&view.Offer.kind is ShopOfferKind.Supply or ShopOfferKind.Investment)
            {
                // MIN − [quantity] + MAX on their own row above the price button.
                var controls=new Transform[]{_quantityMinimumButton?.transform,_quantityDecreaseButton?.transform,_investmentStakeInput?.transform,_quantityIncreaseButton?.transform,_quantityMaximumButton?.transform};
                float available=((RectTransform)view.transform).rect.width*.90f;
                float[] widths={64,40,0,40,64};
                widths[2]=Mathf.Max(56,available-(widths[0]+widths[1]+widths[3]+widths[4])-16);
                float x=0;
                for(int i=0;i<controls.Length;i++)
                {
                    if(controls[i])
                    {
                        var rect=(RectTransform)controls[i];rect.SetParent(view.transform,false);
                        rect.anchorMin=rect.anchorMax=new Vector2(.05f,0);rect.pivot=new Vector2(0,0);
                        rect.anchoredPosition=new Vector2(x,ShopOfferUI.QuantityRowBottom);rect.sizeDelta=new Vector2(widths[i],44);
                    }
                    x+=widths[i]+4;
                }
            }
        }
        RefreshInlinePrices();
        Canvas.ForceUpdateCanvases();
        var selectedView=panel.GetComponentsInChildren<ShopOfferUI>().FirstOrDefault(v=>v.Offer?.StableId==_selectedOfferId);
        var scroll=selectedView?selectedView.GetComponentInParent<ScrollRect>():null;
        if(scroll&&scroll.viewport&&scroll.content)
        {
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,selectedView.transform);
            float below=scroll.viewport.rect.yMin-bounds.min.y;
            if(below>0)scroll.content.anchoredPosition+=Vector2.up*below;
            scroll.velocity=Vector2.zero;
        }
    }
    void RefreshInlinePrices()
    {
        var run=RunManager.Instance;
        if(!storeComposition||!panel||run==null)return;
        foreach(var view in panel.GetComponentsInChildren<ShopOfferUI>())
        {
            var offer=view.Offer;if(offer==null||!view.statusLabel)continue;
            int cost=offer.kind==ShopOfferKind.Investment?offer.investmentStake:offer.kind==ShopOfferKind.Supply?offer.supplyQuantity*3:offer.price;
            string reason=run.GetShopOfferUnavailableReason(offer.StableId);
            view.RefreshPurchase(reason,cost);
            if(_investmentStakeInput&&_investmentStakeInput.textComponent)
            {var text=_investmentStakeInput.textComponent;text.alignment=TextAlignmentOptions.Midline;text.font=DealbreakUIStyle.Theme.numericFont;text.fontSize=22;}
        }
    }
    public void ClearOfferSelection()
    {
        ReturnQuantityControls();_selectedOfferId=null;
        if(_investmentStakeInput)_investmentStakeInput.gameObject.SetActive(false);
        SetQuantityControlsActive(false);
        ShowInlineSelection();UpdateSelectionDetails();
    }
    void Update()
    {
        if(!storeComposition||!panel||!panel.activeInHierarchy||GameplayInputGate.IsBlocked)return;
        if(UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true)
        {if(!Object.FindFirstObjectByType<PauseMenuUI>())ClearOfferSelection();return;}
        var mouse=UnityEngine.InputSystem.Mouse.current;
        if(mouse?.leftButton.wasPressedThisFrame!=true||UnityEngine.EventSystems.EventSystem.current==null)return;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=mouse.position.ReadValue()};
        var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
        if(hits.Count>0&&!hits[0].gameObject.GetComponentInParent<ShopOfferUI>())ClearOfferSelection();
    }

    void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowShop += Open;
            RunManager.Instance.OnHideShop += Close;
            RunManager.Instance.OnShopOffersChanged += RefreshItems;
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCreditsChanged += HandleCreditsChanged;
            CardManager.Instance.OnBuildChanged += RefreshItems;
            CardManager.Instance.OnDeckChanged += RefreshQuantityLimitDetails;
        }
        if (continueButton) { continueButton.onClick.AddListener(OnContinue); KeyButtonBorderStyle.Apply(continueButton); }
        if (buyButton) { buyButton.onClick.AddListener(OnBuy); KeyButtonBorderStyle.Apply(buyButton); }
        EnsureInvestmentControls();
        EnsureOfferScrolling();
        if (RunManager.Instance != null) RunManager.Instance.OnInvestmentResult += ShowInvestmentResult;
        Close();
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowShop -= Open;
            RunManager.Instance.OnHideShop -= Close;
            RunManager.Instance.OnShopOffersChanged -= RefreshItems;
            RunManager.Instance.OnInvestmentResult -= ShowInvestmentResult;
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCreditsChanged -= HandleCreditsChanged;
            CardManager.Instance.OnBuildChanged -= RefreshItems;
            CardManager.Instance.OnDeckChanged -= RefreshQuantityLimitDetails;
        }
        if (continueButton) continueButton.onClick.RemoveListener(OnContinue);
        if (buyButton) buyButton.onClick.RemoveListener(OnBuy);
        Close();
    }

    void EnsureInvestmentControls()
    {
        if (panel == null || _investmentStakeInput != null) return;
        var card = itemsContainer != null ? itemsContainer.parent : panel.transform;
        _controlHome=card;
        _selectedOfferRect = selectedOfferText != null ? selectedOfferText.rectTransform : null;
        if (_selectedOfferRect != null) _selectedOfferAnchorMax = _selectedOfferRect.anchorMax;
        var inputObject = new GameObject("InvestmentStakeInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(card, false);
        var rect = (RectTransform)inputObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.82f, 0.19f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(150f, 32f);
        var input = inputObject.GetComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.placeholder = CreateInputLabel(inputObject.transform, "Stake (Credits)", true);
        input.textComponent = CreateInputLabel(inputObject.transform, string.Empty, false);
        input.targetGraphic = inputObject.GetComponent<Image>();
        inputObject.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.22f, 1f);
        inputObject.SetActive(false);
        _investmentStakeInput = input;
        input.onEndEdit.AddListener(HandleInvestmentStakeEdited);
        _quantityDecreaseButton = CreateQuantityButton(card, "Decrease Quantity", "−", -98f, AdjustSelectedQuantityDown);
        _quantityIncreaseButton = CreateQuantityButton(card, "Increase Quantity", "+", 98f, AdjustSelectedQuantityUp);
        _quantityMinimumButton = CreateQuantityButton(card, "Minimum Quantity", "MIN", -140f, SetSelectedQuantityToMinimum);
        _quantityMaximumButton = CreateQuantityButton(card, "Maximum Quantity", "MAX", 140f, SetSelectedQuantityToMaximum);
        foreach (var control in new[] { _quantityMinimumButton, _quantityMaximumButton })
            control.GetComponentInChildren<TMP_Text>().fontSize = 16f;
        SetQuantityControlsActive(false);

        var resultObject = new GameObject("InvestmentResult", typeof(RectTransform), typeof(TextMeshProUGUI));
        resultObject.transform.SetParent(card, false);
        var resultRect = (RectTransform)resultObject.transform;
        resultRect.anchorMin = resultRect.anchorMax = new Vector2(0.5f, 0.84f);
        resultRect.anchoredPosition = Vector2.zero;
        resultRect.sizeDelta = new Vector2(680f, 32f);
        _investmentResultText = resultObject.GetComponent<TMP_Text>();
        _investmentResultText.alignment = TextAlignmentOptions.Center;
        _investmentResultText.fontSize = 16f;
        _investmentResultText.raycastTarget = false;
    }

    Button CreateQuantityButton(Transform parent, string objectName, string label, float xOffset, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.82f, 0.19f);
        rect.anchoredPosition = new Vector2(xOffset, 0f);
        rect.sizeDelta = new Vector2(34f, 32f);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.22f, 0.35f, 0.42f, 1f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        var text = CreateInputLabel(go.transform, label, false);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 22f;
        text.raycastTarget = false;
        return button;
    }

    void EnsureOfferScrolling()
    {
        if (_offersScroll != null || itemsContainer is not RectTransform content || content.parent is not RectTransform parent)
            return;
        var viewportObject = new GameObject("ShopOffersViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        var viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(parent, false);
        viewport.anchorMin = new Vector2(0.04f, 0.28f);
        viewport.anchorMax = new Vector2(0.50f, 0.80f);
        viewport.pivot = content.pivot;
        viewport.anchoredPosition = Vector2.zero;
        viewport.sizeDelta = Vector2.zero;
        var image = viewportObject.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout != null) { layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandWidth = true; }
        var fitter = content.TryGetComponent(out ContentSizeFitter existingContentSizeFitter) ? existingContentSizeFitter : content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _offersScroll = viewportObject.GetComponent<ScrollRect>();
        _offersScroll.viewport = viewport;
        _offersScroll.content = content;
        _offersScroll.horizontal = false;
        _offersScroll.vertical = true;
        _offersScroll.movementType = ScrollRect.MovementType.Clamped;

        var serviceViewport = new GameObject("ShopServicesViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        var sr = serviceViewport.GetComponent<RectTransform>();
        sr.SetParent(parent, false);
        sr.anchorMin = new Vector2(0.53f, 0.28f);
        sr.anchorMax = new Vector2(0.96f, 0.80f);
        sr.offsetMin = sr.offsetMax = Vector2.zero;
        serviceViewport.GetComponent<Image>().color = Color.clear;
        var serviceContent = new GameObject("ShopServices", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var cr = serviceContent.GetComponent<RectTransform>();
        cr.SetParent(sr, false);
        cr.anchorMin = new Vector2(0, 1); cr.anchorMax = Vector2.one; cr.pivot = new Vector2(0.5f, 1);
        cr.sizeDelta = Vector2.zero;
        var sl = serviceContent.GetComponent<VerticalLayoutGroup>();
        sl.childControlWidth = sl.childControlHeight = true;
        sl.childForceExpandWidth = true; sl.childForceExpandHeight = false; sl.spacing = 10;
        serviceContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = serviceViewport.GetComponent<ScrollRect>();
        scroll.viewport = sr; scroll.content = cr; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        _servicesContainer = cr;
        if(storeComposition){DealbreakUIStyle.Place(viewport,.055f,.15f,.62f,.80f);DealbreakUIStyle.Place(sr,.66f,.15f,.95f,.80f);}
        foreach (var text in parent.GetComponentsInChildren<TMP_Text>(true))
            if (text.text == "ARTIFACT & ENHANCEMENT SHOP") text.text = "SHOP — GOODS & SERVICES";
    }

    static TMP_Text CreateInputLabel(Transform parent, string value, bool placeholder)
    {
        var labelObject = new GameObject(placeholder ? "Placeholder" : "Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 2f);
        labelRect.offsetMax = new Vector2(-8f, -2f);
        var label = labelObject.GetComponent<TMP_Text>();
        label.text = value;
        label.fontSize = 18f;
        label.color = placeholder ? Color.gray : Color.white;
        return label;
    }

    void HandleInvestmentStakeEdited(string value)
    {
        var selected = FindOffer(_selectedOfferId);
        if(selected==null||selected.kind is not (ShopOfferKind.Supply or ShopOfferKind.Investment))return;
        if (selected != null && selected.kind == ShopOfferKind.Supply)
        {
            var run = RunManager.Instance;
            int current = selected.supplyQuantity;
            if (run == null || !int.TryParse(value, out int quantity) || !run.SetSupplyQuantity(_selectedOfferId, quantity))
            {
                if (_investmentStakeInput) _investmentStakeInput.SetTextWithoutNotify(current > 0 ? current.ToString() : string.Empty);
                UpdateSelectionDetails($"Choose 1–{(run != null ? run.GetSupplyMaximumQuantity() : 0)} cards.");
            }
            else UpdateSelectionDetails();
            return;
        }
        if (!int.TryParse(value, out int stake) || RunManager.Instance == null || string.IsNullOrEmpty(_selectedOfferId))
        {
            UpdateSelectionDetails("Enter a whole Credits amount from 1 to your current Credits.");
            return;
        }
        if (RunManager.Instance == null || !RunManager.Instance.SetInvestmentStake(_selectedOfferId, stake))
        {
            if (_investmentStakeInput && selected != null)
                _investmentStakeInput.SetTextWithoutNotify(selected.investmentStake > 0 ? selected.investmentStake.ToString() : string.Empty);
            UpdateSelectionDetails("Stake must be between 1 and your current Credits.");
        }
        else UpdateSelectionDetails();
    }

    void ShowInvestmentResult(string message)
    {
        if (_investmentResultText) _investmentResultText.text = message;
    }

    void HandleCreditsChanged(int credits)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (creditsText) creditsText.text = CardManager.Instance != null && CardManager.Instance.HasInfiniteMoney
            ? "∞ Credits" : $"{credits} Credits";
#else
        if (creditsText) creditsText.text = $"{credits} Credits";
#endif
        if (panel != null && panel.activeSelf) RefreshItems();
    }

    void Open()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevModeRuntime.Enabled && DevModeRuntime.UnlimitedShopOffers)
        {
            BoardPanelTransition.Hide(panel);
            return;
        }
#endif
        BoardPanelTransition.Show(panel);
        RefreshItems();
    }

    void Close()
    {
        _selectedOfferId = null;
        BoardPanelTransition.Hide(panel);
        UpdateSelectionDetails();
    }

    void RefreshItems()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevModeRuntime.Enabled && DevModeRuntime.UnlimitedShopOffers)
        {
            BoardPanelTransition.Hide(panel);
            return;
        }
#endif
        ReturnQuantityControls();
        _selectedOfferId = null;
        if (_investmentStakeInput) _investmentStakeInput.gameObject.SetActive(false);
        SetQuantityControlsActive(false);
        UpdateSelectionDetails();
        if (itemsContainer == null || itemButtonPrefab == null) return;

        foreach (Transform child in ShopRows())
        {
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        var offers = RunManager.Instance != null
            ? RunManager.Instance.GetCurrentShopOffers()
            : System.Array.Empty<ShopOffer>();
        AddSectionHeading("GOODS", itemsContainer);
        AddGoodsRefreshButton();
        AddSectionHeading("SERVICES", _servicesContainer ?? itemsContainer);
        foreach (var offer in offers.Where(offer=>!offer.purchased).OrderBy(offer => offer.IsService ? 1 : 0))
        {
            string unavailableReason = RunManager.Instance != null
                ? RunManager.Instance.GetShopOfferUnavailableReason(offer.StableId)
                : "Offer unavailable";

            var container = offer.IsService ? _servicesContainer ?? itemsContainer : itemsContainer;
            var btn = Instantiate(itemButtonPrefab, container);
            btn.name = $"ShopOffer_{offer.StableId.Replace(':', '_')}";
            var text = btn.GetComponentInChildren<TMP_Text>();
            if (text)
            {
                string status = string.IsNullOrEmpty(unavailableReason) ? string.Empty : $"\n<color=#DFA0A0>{unavailableReason}</color>";
                string priceLabel = offer.kind == ShopOfferKind.Investment
                    ? (offer.investmentStake > 0 ? $"{offer.investmentStake} Credits at risk" : "Set stake")
                    : offer.kind == ShopOfferKind.Supply ? "3 Credits / card" : $"{offer.price} Credits";
                text.text = $"<b>{offer.DisplayName}</b>\n{priceLabel}{status}";
                text.overflowMode = TextOverflowModes.Overflow;
                var row = btn.TryGetComponent(out LayoutElement existingLayoutElement) ? existingLayoutElement : btn.AddComponent<LayoutElement>();
                float textWidth = Mathf.Max(1f, ((RectTransform)container).rect.width - 32f);
                row.minHeight = Mathf.Max(84f, text.GetPreferredValues(text.text, textWidth, 0f).y + 20f);
                row.preferredHeight = row.minHeight;
            }

            var button = btn.GetComponent<Button>();
            var inspect=btn.GetComponent<ContextualInfoTrigger>()??btn.AddComponent<ContextualInfoTrigger>();
            inspect.pinOnClick=true;inspect.enabled=true;
            string inspectedId=offer.StableId;
            inspect.describe=()=>
            {
                var live=FindOffer(inspectedId);if(live==null)return null;
                string price=live.kind==ShopOfferKind.Investment?$"Stake {live.investmentStake} Credits":
                    live.kind==ShopOfferKind.Supply?$"Total {live.supplyQuantity*3} Credits • {live.supplyQuantity} card(s)":$"Price {live.price} Credits";
                return new ContextualInfo(live.IsService?"Service":"Goods",live.DisplayName,
                    live.Description(CardManager.Instance)+$"\n\n{price}");
            };
            DealbreakUIStyle.ApplyHierarchy(btn.transform);
            var offerView=btn.GetComponent<ShopOfferUI>();if(!offerView)offerView=btn.AddComponent<ShopOfferUI>();
            offerView.Bind(offer,unavailableReason);
            string purchaseId=offer.StableId;offerView.purchaseButton.onClick.AddListener(()=>PurchaseInline(purchaseId));
            if (button)
            {
                // Unavailable offers remain inspectable; Buy always revalidates in RunManager.
                string stableId = offer.StableId;
                button.onClick.AddListener(() => SelectOffer(stableId));
            }
        }
        RefreshInlinePrices();
        // Style the rebuilt rows now; left to the periodic check they show default fonts for a few frames.
        var styles = GetComponentInParent<DealbreakUIRuntime>();
        if (styles) styles.RefreshStyles();
        Canvas.ForceUpdateCanvases();
        if (_offersScroll != null)
        {
            _offersScroll.velocity = Vector2.zero;
            _offersScroll.verticalNormalizedPosition = 1f;
        }
    }

    System.Collections.Generic.IEnumerable<Transform> ShopRows() => itemsContainer.Cast<Transform>()
        .Concat(_servicesContainer != null ? _servicesContainer.Cast<Transform>() : System.Array.Empty<Transform>()).ToArray();

    void AddSectionHeading(string title, Transform container)
    {
        var go = new GameObject(title.StartsWith("GOODS") ? "GoodsHeading" : "ServicesHeading",
            typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(container, false);
        var text = go.GetComponent<TMP_Text>();
        text.text = title;
        text.fontSize = 18;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        var layout = go.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = 36;
    }

    void AddGoodsRefreshButton()
    {
        var run = RunManager.Instance;
        if (run == null) return;
        var go = Instantiate(itemButtonPrefab, itemsContainer);
        go.name = "RefreshGoods";
        var label = go.GetComponentInChildren<TMP_Text>();
        if (label) label.text = $"Refresh Goods • {run.GoodsRefreshPrice} Credits";
        var layout = go.TryGetComponent(out LayoutElement existingLayoutElement) ? existingLayoutElement : go.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = 42;
        var button = go.GetComponent<Button>();
        int expected = run.GoodsRefreshCount;
        if (button)
        {
            button.interactable = run.CanRefreshGoods;
            button.onClick.AddListener(() => { run.RefreshGoods(expected); });
        }
    }

    void SelectOffer(string stableId)
    {
        if (GameplayInputGate.IsBlocked || panel == null || !panel.activeSelf) return;
        var offers = RunManager.Instance != null ? RunManager.Instance.GetCurrentShopOffers() : null;
        if (offers == null) return;
        bool found = false;
        foreach (var offer in offers)
            if (offer.StableId == stableId) { found = true; break; }
        if (!found) return;

        _selectedOfferId = stableId;
        var selectedOffer = FindOffer(stableId);
        bool supplySelected = selectedOffer != null && selectedOffer.kind == ShopOfferKind.Supply;
        bool investmentSelected = selectedOffer != null && selectedOffer.kind == ShopOfferKind.Investment;
        if (_investmentStakeInput)
        {
            _investmentStakeInput.gameObject.SetActive(investmentSelected || supplySelected);
            if (_investmentStakeInput.placeholder is TMP_Text placeholder)
                placeholder.text = supplySelected ? "Card quantity" : "Stake (Credits)";
            if (_selectedOfferRect != null)
                _selectedOfferRect.anchorMax = investmentSelected || supplySelected
                    ? new Vector2(0.7f, _selectedOfferAnchorMax.y) : _selectedOfferAnchorMax;
            if (supplySelected)
            {
                int maximum = RunManager.Instance.GetSupplyMaximumQuantity();
                if (maximum > 0)
                {
                    int quantity = Mathf.Clamp(selectedOffer.supplyQuantity, 1, maximum);
                    RunManager.Instance.SetSupplyQuantity(stableId, quantity);
                    _investmentStakeInput.SetTextWithoutNotify(quantity.ToString());
                }
                else _investmentStakeInput.SetTextWithoutNotify(string.Empty);
            }
            if (investmentSelected)
            {
                if (selectedOffer.investmentStake <= 0 && CardManager.Instance != null && CardManager.Instance.credits > 0)
                    RunManager.Instance.SetInvestmentStake(stableId, 1);
                _investmentStakeInput.SetTextWithoutNotify(selectedOffer.investmentStake > 0 ? selectedOffer.investmentStake.ToString() : string.Empty);
            }
        }
        SetQuantityControlsActive(investmentSelected || supplySelected);
        RefreshQuantityControls();
        foreach (Transform child in ShopRows())
        {
            var button = child.GetComponent<Button>();
            if (!button) continue;
            var colors = button.colors;
            bool selected = child.name == $"ShopOffer_{stableId.Replace(':', '_')}";
            var style=button.GetComponent<EditorialButton>();if(style)style.selected=selected;
            // Persistent outline remains visible even while the button is hovered/focused.
            colors.normalColor = selected ? new Color(0.83f, 0.88f, 0.91f) : Color.white;
            button.colors = colors;
            var outline = child.GetComponent<Outline>();
            if (outline == null) outline = child.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.62f, 0.72f, 0.78f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.enabled = selected;
        }
        UpdateSelectionDetails();
        ShowInlineSelection();
    }

    void RefreshQuantityControls()
    {
        var run = RunManager.Instance;
        var offer = FindOffer(_selectedOfferId);
        if (run == null || offer == null || offer.kind is not (ShopOfferKind.Supply or ShopOfferKind.Investment))
        {
            foreach (var control in QuantityControls)
                if (control) control.interactable = false;
            return;
        }
        int maximum = offer.kind == ShopOfferKind.Supply
            ? run.GetSupplyMaximumQuantity()
            : CardManager.Instance != null ? CardManager.Instance.credits : 0;
        int current = offer.kind == ShopOfferKind.Supply ? offer.supplyQuantity : offer.investmentStake;
        bool canLower = current > 1 && !GameplayInputGate.IsBlocked;
        bool canRaise = current < maximum && !GameplayInputGate.IsBlocked;
        if (_quantityDecreaseButton) _quantityDecreaseButton.interactable = canLower;
        if (_quantityMinimumButton) _quantityMinimumButton.interactable = canLower;
        if (_quantityIncreaseButton) _quantityIncreaseButton.interactable = canRaise;
        if (_quantityMaximumButton) _quantityMaximumButton.interactable = canRaise;
    }

    void RefreshQuantityLimitDetails()
    {
        if (panel != null && panel.activeSelf) UpdateSelectionDetails();
    }

    void AdjustSelectedQuantity(int delta)
    {
        if (GameplayInputGate.IsBlocked || panel == null || !panel.activeSelf || string.IsNullOrEmpty(_selectedOfferId)) return;
        var run = RunManager.Instance;
        var offer = FindOffer(_selectedOfferId);
        if (run == null || offer == null) return;
        int maximum = offer.kind == ShopOfferKind.Supply
            ? run.GetSupplyMaximumQuantity()
            : offer.kind == ShopOfferKind.Investment && CardManager.Instance != null ? CardManager.Instance.credits : 0;
        int current = offer.kind == ShopOfferKind.Supply ? offer.supplyQuantity : offer.investmentStake;
        if (maximum < 1) return;
        int next = Mathf.Clamp(current + delta, 1, maximum);
        bool changed = offer.kind == ShopOfferKind.Supply
            ? run.SetSupplyQuantity(offer.StableId, next)
            : offer.kind == ShopOfferKind.Investment && run.SetInvestmentStake(offer.StableId, next);
        if (!changed) return;
        if (_investmentStakeInput) _investmentStakeInput.SetTextWithoutNotify(next.ToString());
        UpdateSelectionDetails();
    }

    void AdjustSelectedQuantityDown() => AdjustSelectedQuantity(-1);
    void AdjustSelectedQuantityUp() => AdjustSelectedQuantity(1);
    // Clamping inside AdjustSelectedQuantity turns these into "lowest" and "highest allowed".
    void SetSelectedQuantityToMinimum() => AdjustSelectedQuantity(int.MinValue / 2);
    void SetSelectedQuantityToMaximum() => AdjustSelectedQuantity(int.MaxValue / 2);

    void UpdateSelectionDetails(string failure = null)
    {
        ShopOffer selected = null;
        var offers = RunManager.Instance != null ? RunManager.Instance.GetCurrentShopOffers() : null;
        if (offers != null)
            foreach (var offer in offers)
                if (offer.StableId == _selectedOfferId) { selected = offer; break; }
        RefreshQuantityControls();
        RefreshInlinePrices();
        if(buyButton&&storeComposition)buyButton.gameObject.SetActive(false);

        if (buyButton) buyButton.interactable = selected != null &&
            (selected.kind is ShopOfferKind.Supply or ShopOfferKind.Investment
                ? RunManager.Instance.CanPurchaseShopOffer(selected.StableId)
                : selected.kind != ShopOfferKind.Supply);
        if (!selectedOfferText) return;
        if(storeComposition)selectedOfferText.gameObject.SetActive(!string.IsNullOrEmpty(failure));
        if (!string.IsNullOrEmpty(failure))
        {
            selectedOfferText.text = failure;
            return;
        }
        if (selected == null)
        {
            selectedOfferText.text = string.Empty;
            return;
        }
        var reason = RunManager.Instance.GetShopOfferUnavailableReason(_selectedOfferId);
        string status = string.IsNullOrEmpty(reason) ? "Ready to buy" : reason;
        string displayedPrice = selected.kind == ShopOfferKind.Investment
            ? $"{selected.investmentStake} Credits at risk"
            : selected.kind == ShopOfferKind.Supply
                ? $"{selected.supplyQuantity} cards — total {selected.supplyQuantity * 3} Credits (3 each; max {RunManager.Instance.GetSupplyMaximumQuantity()})"
                : $"{selected.price} Credits";
        selectedOfferText.text = $"{selected.DisplayName} — {displayedPrice}\n" +
            (ContextualTooltipUI.Instance ? status : $"{selected.Description(CardManager.Instance)}\n{status}");
    }

    void OnBuy()
    {
        if (GameplayInputGate.IsBlocked || panel == null || !panel.activeSelf || string.IsNullOrEmpty(_selectedOfferId)) return;
        var run = RunManager.Instance;
        if (run == null) return;
        string selectedId = _selectedOfferId;
        if (run.PurchaseShopOffer(selectedId))
        {
            RefreshItems();
            return;
        }

        // Revalidation failure must not commit the purchase; explain it before clearing stale selection.
        string reason = run.GetShopOfferUnavailableReason(selectedId);
        _selectedOfferId = null;
        RefreshItems();
        UpdateSelectionDetails(string.IsNullOrEmpty(reason) ? "Offer unavailable" : reason);
    }

    ShopOffer FindOffer(string stableId)
    {
        if (RunManager.Instance == null || string.IsNullOrEmpty(stableId)) return null;
        foreach (var offer in RunManager.Instance.GetCurrentShopOffers())
            if (offer.StableId == stableId) return offer;
        return null;
    }

    void OnContinue()
    {
        RunManager.Instance.OnShopDone();
    }
}
