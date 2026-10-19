using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7EShopSelectionTests
{
    readonly List<Object> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    RunManager _run;
    ShopUI _ui;
    CardEnhancementData _enhancement;
    ConsumableData _enhancementPatch;

    [SetUp]
    public void SetUp()
    {
        var gearA = CreateGear("first", 10);
        var gearB = CreateGear("second", 12);
        _enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        _enhancement.id = "sharp";
        _enhancement.displayName = "Sharpened";
        _enhancement.description = "Attack bonus";
        _enhancement.price = 8;
        _assets.Add(_enhancement);
        _enhancementPatch = ScriptableObject.CreateInstance<ConsumableData>();
        _enhancementPatch.id = "sharp_patch";
        _enhancementPatch.canonicalId = "phase7e_patch";
        _enhancementPatch.displayName = "Sharpened Patch";
        _enhancementPatch.description = "Target: Card. Effect: Apply Sharpened.";
        _enhancementPatch.price = _enhancement.price;
        _enhancementPatch.effectType = ConsumableEffectType.ApplyEnhancement;
        _enhancementPatch.enhancementToApply = _enhancement;
        _enhancementPatch.shopAvailable = true;
        _assets.Add(_enhancementPatch);

        _cards = Make<CardManager>("Cards");
        _cards.Configure(null, null, null, new[] { gearA, gearB }, new[] { _enhancement }, new[] { _enhancementPatch });
        var combat = Make<CombatManager>("Combat");
        combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Run");
        _run.thiefType = CreateEnemy("Thief");
        _run.goblinType = CreateEnemy("Goblin");
        _run.knightType = CreateEnemy("Knight");

        var uiObject = new GameObject("Shop UI");
        _objects.Add(uiObject);
        uiObject.SetActive(false);
        _ui = uiObject.AddComponent<ShopUI>();
        _ui.panel = new GameObject("Panel", typeof(RectTransform));
        _objects.Add(_ui.panel);
        _ui.panel.GetComponent<RectTransform>().sizeDelta = new Vector2(1000f, 800f);
        var offersObject = new GameObject("Offers", typeof(RectTransform));
        offersObject.transform.SetParent(_ui.panel.transform, false);
        offersObject.GetComponent<RectTransform>().sizeDelta = new Vector2(700f, 500f);
        _ui.itemsContainer = offersObject.transform;
        _objects.Add(offersObject);
        var template = new GameObject("Offer template", typeof(RectTransform), typeof(Image), typeof(Button));
        _objects.Add(template);
        var templateLabel = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        templateLabel.transform.SetParent(template.transform);
        _ui.itemButtonPrefab = template;
        _ui.buyButton = Make<Button>("Buy");
        _ui.continueButton = Make<Button>("Continue");
        _ui.selectedOfferText = Make<TextMeshProUGUI>("Details");
        _ui.creditsText = Make<TextMeshProUGUI>("Gold");
        uiObject.SetActive(true);
        InvokeUi("OnEnable"); // MonoBehaviour lifecycle is not dispatched for plain EditMode fixtures.
        _run.StartRunWithSeed("7E-SLICE-1");
        OpenShop();
    }

    [TearDown]
    public void TearDown()
    {
        if (_ui) InvokeUi("OnDisable");
        foreach (var obj in _objects)
            if (obj) Object.DestroyImmediate(obj);
        _objects.Clear();
        foreach (var asset in _assets)
            if (asset) Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void SelectAndSwitch_InspectWithoutSpendingOrGranting()
    {
        _cards.AddCredits(50);
        var offers = _run.GetCurrentShopOffers();
        var first = offers[0];
        var second = offers[1];
        Click(first);
        Assert.That(_ui.SelectedOfferId, Is.EqualTo(first.StableId));
        Assert.That(_ui.selectedOfferText.text, Does.Contain(first.DisplayName));
        Assert.That(_ui.selectedOfferText.text, Does.Contain($"{first.price} Credits"));
        Assert.That(ButtonFor(first).GetComponentInChildren<TMP_Text>().text, Does.Not.StartWith("SELECTED"));
        Assert.That(ButtonFor(first).GetComponent<Outline>().enabled, Is.True);
        Click(second);
        Assert.That(_ui.SelectedOfferId, Is.EqualTo(second.StableId));
        Assert.That(ButtonFor(first).GetComponentInChildren<TMP_Text>().text, Does.Not.StartWith("SELECTED"));
        Assert.That(ButtonFor(first).GetComponent<Outline>().enabled, Is.False);
        Assert.That(ButtonFor(second).GetComponent<Outline>().enabled, Is.True);
        Assert.That(_cards.credits, Is.EqualTo(50));
        Assert.That(_cards.ownedGear, Is.Empty);
        Assert.That(_cards.ownedCards.All(card => card.Enhancement == null), Is.True);
    }

    [Test]
    public void SupplySteppersFollowAuthoritativeMaximumAndShowTotalBeforePurchase()
    {
        _cards.AddCredits(9);
        var collection = (CardCollection)typeof(CardManager)
            .GetField("_cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(_cards);
        Assert.That(collection.TryDiscard(_cards.hand.ToArray()), Is.True);
        _cards.NotifyDeckChanged();
        var offer = ReplaceCurrentServiceWith(ShopOfferKind.Supply);
        Click(offer);
        var down = GetUiField<Button>("_quantityDecreaseButton");
        var up = GetUiField<Button>("_quantityIncreaseButton");
        var input = GetUiField<TMP_InputField>("_investmentStakeInput");
        Assert.That(down.interactable, Is.False, "Quantity one is the minimum.");
        Assert.That(up.interactable, Is.True, $"max={_run.GetSupplyMaximumQuantity()}, gold={_cards.credits}, draw={_cards.deck.Count}, hand={_cards.HandCount}/{_cards.HandCapacity}, quantity={offer.supplyQuantity}");
        up.onClick.Invoke();
        up.onClick.Invoke();
        Assert.That(offer.supplyQuantity, Is.EqualTo(3));
        Assert.That(up.interactable, Is.False, "The current authoritative maximum is three.");
        Assert.That(_ui.selectedOfferText.text, Does.Contain("total 9 Credits"));

        _cards.DrawToHand(CardManager.Instance.HandCapacity - 1);
        Assert.That(_run.GetSupplyMaximumQuantity(), Is.EqualTo(1));
        Assert.That(up.interactable, Is.False);
        Assert.That(_ui.buyButton.interactable, Is.False, "A stale quantity cannot be purchased after hand capacity changes.");
        input.onEndEdit.Invoke("2");
        Assert.That(offer.supplyQuantity, Is.EqualTo(3), "Invalid typed edits do not mutate the authoritative quantity.");
        down.onClick.Invoke();
        down.onClick.Invoke();
        Assert.That(offer.supplyQuantity, Is.EqualTo(1));
        Assert.That(_ui.buyButton.interactable, Is.True);
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.credits, Is.EqualTo(6));
        Assert.That(_cards.HandCount, Is.EqualTo(CardManager.Instance.HandCapacity));
    }

    [Test]
    public void InvestmentSteppersEnforceMinimumMaximumAndRejectStaleKeyboardValue()
    {
        _cards.AddCredits(10);
        var offer = ReplaceCurrentServiceWith(ShopOfferKind.Investment);
        Click(offer);
        var down = GetUiField<Button>("_quantityDecreaseButton");
        var up = GetUiField<Button>("_quantityIncreaseButton");
        var input = GetUiField<TMP_InputField>("_investmentStakeInput");
        Assert.That(offer.investmentStake, Is.EqualTo(1));
        Assert.That(down.interactable, Is.False);
        up.onClick.Invoke();
        up.onClick.Invoke();
        Assert.That(offer.investmentStake, Is.EqualTo(3));
        Assert.That(_ui.selectedOfferText.text, Does.Contain("3 Credits at risk"));
        input.onEndEdit.Invoke("11");
        Assert.That(offer.investmentStake, Is.EqualTo(3));
        Assert.That(input.text, Is.EqualTo("3"));
        Assert.That(_cards.credits, Is.EqualTo(10));
        Assert.That(_run.SetInvestmentStake(offer.StableId, 11), Is.False);
        down.onClick.Invoke();
        Assert.That(offer.investmentStake, Is.EqualTo(2));
    }

    ShopOffer ReplaceCurrentServiceWith(ShopOfferKind kind)
    {
        var offer = _run.GetCurrentShopOffers().First(item => item.IsService);
        offer.kind = kind;
        offer.price = kind == ShopOfferKind.Supply ? 3 : 5;
        offer.supplyQuantity = 1;
        offer.investmentStake = 0;
        InvokeUi("RefreshItems");
        return offer;
    }

    T GetUiField<T>(string fieldName) where T : class =>
        typeof(ShopUI).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(_ui) as T;

    [Test]
    public void Buy_ChargesCachedPriceOnlyAtConfirmation_AndClearsSelection()
    {
        _cards.AddCredits(50);
        var offer = GearOffer();
        Click(offer);
        int price = offer.price;
        offer.gear.price = price + 100;
        Assert.That(_cards.credits, Is.EqualTo(50));
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.credits, Is.EqualTo(50 - price));
        Assert.That(_cards.HasGear(offer.gear), Is.True);
        Assert.That(_ui.SelectedOfferId, Is.Null);
        Assert.That(_ui.buyButton.interactable, Is.False);
    }

    [Test]
    public void InsufficientCredits_AndAlreadyOwnedGear_FailWithoutSpending()
    {
        var offer = GearOffer();
        Click(offer);
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_cards.HasGear(offer.gear), Is.False);
        Assert.That(_ui.selectedOfferText.text, Does.Contain("Need"));
        _cards.AddCredits(50);
        Click(offer);
        _cards.gearIds.Add(offer.gear.id); // Simulate ownership changing after selection, without a UI refresh.
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.credits, Is.EqualTo(50));
        Assert.That(_cards.ownedGear, Is.Empty);
        Assert.That(_ui.selectedOfferText.text, Does.Contain("Already owned"));
    }

    [Test]
    public void NoDirectEnhancementOffer_BuyAddsMappedConsumableWithoutApplyingIt()
    {
        _cards.AddCredits(50);
        Assert.That(_run.GetCurrentShopOffers().Any(offer => offer.kind == ShopOfferKind.Enhancement), Is.False);
        var offer = _run.GetCurrentShopOffers().Single(item => item.kind == ShopOfferKind.Consumable &&
            item.consumable == _enhancementPatch);
        Click(offer);
        Assert.That(_ui.selectedOfferText.text, Does.Contain(_enhancement.displayName));
        Assert.That(_ui.buyButton.interactable, Is.True);

        _ui.buyButton.onClick.Invoke();

        Assert.That(_cards.GetConsumableCount(_enhancementPatch), Is.EqualTo(1));
        Assert.That(_cards.hand.All(card => card.Enhancement == null), Is.True,
            "Purchasing the mapped Trick does not apply its Mod before the player selects a card and uses it.");
        Assert.That(_cards.credits, Is.EqualTo(50 - offer.price));
        Assert.That(_ui.SelectedOfferId, Is.Null);
    }

    [Test]
    public void RemovedCachedOffer_RejectsStaleSelectedBuy()
    {
        _cards.AddCredits(50);
        var offer = GearOffer();
        Click(offer);
        ((List<ShopOffer>)_run.GetCurrentShopOffers()).Remove(offer);
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.credits, Is.EqualTo(50));
        Assert.That(_cards.ownedGear, Is.Empty);
        Assert.That(_ui.SelectedOfferId, Is.Null);
        Assert.That(_ui.selectedOfferText.text, Does.Contain("unavailable"));
    }

    [Test]
    public void InvalidCachedPrice_RejectsBuyWithoutGrantingOrChangingCredits()
    {
        _cards.AddCredits(50);
        var offer = GearOffer();
        Click(offer);
        offer.price = -5;
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.credits, Is.EqualTo(50));
        Assert.That(_cards.ownedGear, Is.Empty);
        Assert.That(_ui.selectedOfferText.text, Does.Contain("Invalid price"));
    }

    [Test]
    public void FullCapacity_OfferCanBeInspectedButBuyFailsWithoutSpendingOrRemovingIt()
    {
        _cards.AddCredits(50);
        var offer = GearOffer();
        for (int i = 0; i < CardManager.BASE_GEAR_CAPACITY; i++)
            Assert.That(_cards.BuyGear(CreateGear($"other_{i}", 0), 0), Is.True);
        int credits = _cards.credits;
        string stableId = offer.StableId;
        int cachedPrice = offer.price;
        Click(offer);
        Assert.That(_ui.SelectedOfferId, Is.EqualTo(stableId));
        Assert.That(_ui.selectedOfferText.text, Does.Contain("Gear Capacity Full"));
        Assert.That(ButtonFor(offer).GetComponent<ContextualInfoTrigger>().Info.Value.Body, Does.Contain("Gear Capacity Full"));
        _ui.buyButton.onClick.Invoke();
        Assert.That(_ui.selectedOfferText.text, Does.Contain("Gear Capacity Full"));
        Assert.That(_cards.credits, Is.EqualTo(credits));
        Assert.That(_cards.ownedGear, Has.Count.EqualTo(5));
        Assert.That(_cards.HasGear(offer.gear), Is.False);
        Assert.That(_run.GetCurrentShopOffers(), Does.Contain(offer));
        Assert.That(offer.StableId, Is.EqualTo(stableId));
        Assert.That(offer.price, Is.EqualTo(cachedPrice));
    }

    // Gear sells like a Trick: select it on the Gear rail inside a Black Market, then Sell.
    [Test]
    public void GearSellsFromTheGearRail_ForHalfItsAuthoredPrice_AndTheShopHasNoSellMode()
    {
        Assert.That(_ui.panel.GetComponentsInChildren<Button>(true).Any(button => button.name == "ShopSellMode"), Is.False);
        var gear = GearOffer().gear;
        gear.price = 11; // Odd authored prices verify integer division rounds down.
        Assert.That(_cards.BuyGear(gear, 0), Is.True);
        long instanceId = _cards.OwnedGearInstances[0].Id;
        int creditsBefore = _cards.credits;

        var canvas = new GameObject("Gear Rail Canvas", typeof(RectTransform), typeof(Canvas));
        _objects.Add(canvas);
        var slots = new GameObject("Gear Slots", typeof(RectTransform));
        slots.transform.SetParent(canvas.transform, false);
        var railObject = new GameObject("Gear Rail");
        _objects.Add(railObject);
        railObject.SetActive(false);
        var rail = railObject.AddComponent<GearRailUI>();
        rail.source = _cards;
        rail.slotsRoot = (RectTransform)slots.transform;
        railObject.SetActive(true);
        typeof(GearRailUI).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(rail, null);
        rail.Refresh();

        var slot = slots.GetComponentsInChildren<GearIconSlotUI>().Single(value => value.InstanceId == instanceId);
        slot.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null)
            { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left });
        Assert.That(rail.SellButton, Is.Not.Null);
        Assert.That(rail.SellButton.gameObject.activeInHierarchy, Is.True);
        Assert.That(rail.SellButton.interactable, Is.True);
        Assert.That(_cards.credits, Is.EqualTo(creditsBefore), "Selecting Gear must not sell it.");

        rail.SellButton.onClick.Invoke();
        Assert.That(_cards.credits, Is.EqualTo(creditsBefore + gear.price / 2));
        Assert.That(_cards.GetGearInstanceById(instanceId), Is.Null);
        Assert.That(rail.SellButton.gameObject.activeInHierarchy, Is.False);
    }

    [Test]
    public void ContinueRejectedByInputGate_LeavesShopAndNodeOpen()
    {
        GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, true);
        try
        {
            var activeShop = _run.ActiveNode;
            Assert.That(activeShop, Is.Not.Null);
            Assert.That(activeShop.kind, Is.EqualTo(MapNodeType.Shop));

            _ui.continueButton.onClick.Invoke();

            Assert.That(_ui.panel.activeSelf, Is.True,
                "The UI should close only after RunManager accepts Shop completion and raises OnHideShop.");
            Assert.That(_run.ActiveNode, Is.SameAs(activeShop));
            Assert.That(activeShop.completed, Is.False);
        }
        finally
        {
            GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, false);
        }
    }

    [Test]
    public void CloseAndRestart_ClearSelectionAndBlockStaleBuy()
    {
        _cards.AddCredits(50);
        var offer = GearOffer();
        Click(offer);
        _ui.continueButton.onClick.Invoke();
        Assert.That(_ui.SelectedOfferId, Is.Null);
        Assert.That(_ui.panel.activeSelf, Is.False);
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.credits, Is.EqualTo(50));

        _run.RestartRun();
        Assert.That(_ui.SelectedOfferId, Is.Null);
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_cards.ownedGear, Is.Empty);
    }

    [Test]
    public void GoodsAndServices_HaveSeparateVisibleContainers_AndRefreshDoesNotRerollServices()
    {
        var labels = _ui.panel.GetComponentsInChildren<TMP_Text>(true);
        Assert.That(labels.Any(label => label.text == "GOODS"), Is.True);
        Assert.That(labels.Any(label => label.text == "SERVICES"), Is.True);
        var services = _run.GetCurrentShopOffers().Where(offer => offer.IsService).ToArray();
        Assert.That(services.Length, Is.EqualTo(2));
        foreach (var service in services)
            Assert.That(ButtonFor(service).transform.parent, Is.Not.SameAs(_ui.itemsContainer));
        _cards.AddCredits(10);
        _ui.itemsContainer.Find("RefreshGoods").GetComponent<Button>().onClick.Invoke();
        Assert.That(_cards.credits, Is.EqualTo(7));
        Assert.That(_run.GoodsRefreshPrice, Is.EqualTo(5));
        Assert.That(_run.GetCurrentShopOffers().Where(offer => offer.IsService), Is.EqualTo(services));
    }

    void InvokeUi(string method) => typeof(ShopUI)
        .GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .Invoke(_ui, null);

    void OpenShop()
    {
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _run.OnPathChosen(shop.id);
        Assert.That(_ui.panel.activeSelf, Is.True);
        Assert.That(_ui.itemsContainer.childCount, Is.GreaterThan(0), "ShopUI must populate offers when the shop opens");
    }

    ShopOffer GearOffer() => _run.GetCurrentShopOffers().First(offer => offer.kind == ShopOfferKind.Gear);

    [Test]
    public void InlineBuy_SelectsWithoutPayment_ThenPurchasesOnceAndRemovesStock()
    {
        _ui.storeComposition=true;_cards.AddCredits(50);_ui.RefreshPresentation();
        var offer=GearOffer();string id=offer.StableId;int before=_cards.credits;
        Click(offer);var view=ButtonFor(offer).GetComponent<ShopOfferUI>();
        Assert.That(_cards.credits,Is.EqualTo(before));
        Assert.That(view.descriptionLabel.text,Does.Contain(offer.gear.description));
        Assert.That(view.purchaseButton.gameObject.activeSelf,Is.True);
        view.purchaseButton.onClick.Invoke();
        Assert.That(_cards.credits,Is.EqualTo(before-offer.price));
        Assert.That(_cards.ownedGear,Has.Count.EqualTo(1));
        Assert.That(_ui.panel.GetComponentsInChildren<ShopOfferUI>().Any(v=>v.Offer.StableId==id),Is.False);
        _ui.PurchaseInline(id); // A queued/stale submit must not charge or grant again.
        Assert.That(_cards.credits,Is.EqualTo(before-offer.price));
        Assert.That(_cards.ownedGear,Has.Count.EqualTo(1));
    }

    [Test]
    public void InlineSelection_SwitchesAndCancelsWithoutPurchasing()
    {
        _ui.storeComposition=true;_cards.AddCredits(50);_ui.RefreshPresentation();
        var offers=_run.GetCurrentShopOffers().Where(o=>!o.IsService).Take(2).ToArray();
        Click(offers[0]);Click(offers[1]);
        Assert.That(ButtonFor(offers[0]).GetComponent<ShopOfferUI>().purchaseButton.gameObject.activeSelf,Is.False);
        Assert.That(ButtonFor(offers[1]).GetComponent<ShopOfferUI>().purchaseButton.gameObject.activeSelf,Is.True);
        _ui.ClearOfferSelection();_ui.PurchaseInline(offers[1].StableId);
        Assert.That(_ui.SelectedOfferId,Is.Null);Assert.That(_cards.credits,Is.EqualTo(50));Assert.That(_cards.ownedGear,Is.Empty);
    }

    [Test]
    public void InlineStake_UsesEmbeddedControlsAndRemovesCompletedService()
    {
        _ui.storeComposition=true;_cards.AddCredits(10);_ui.RefreshPresentation();
        var offer=ReplaceCurrentServiceWith(ShopOfferKind.Investment);
        Click(offer);InvokeUi("AdjustSelectedQuantityUp");
        Assert.That(offer.investmentStake,Is.EqualTo(2));
        var view=ButtonFor(offer).GetComponent<ShopOfferUI>();
        var input=_ui.panel.GetComponentsInChildren<TMP_InputField>().Single();
        Assert.That(input.transform.parent,Is.EqualTo(view.transform));
        Assert.That(view.statusLabel.text,Is.Empty);
        view.purchaseButton.onClick.Invoke();_ui.PurchaseInline(offer.StableId);
        Assert.That(_cards.credits,Is.EqualTo(8));Assert.That(_run.PendingInvestmentCount,Is.EqualTo(1));
        Assert.That(_ui.panel.GetComponentsInChildren<ShopOfferUI>().Any(v=>v.Offer.StableId==offer.StableId),Is.False);
    }

    Button ButtonFor(ShopOffer offer) => _ui.panel.GetComponentsInChildren<Button>(true)
        .Last(button => button && button.name == $"ShopOffer_{offer.StableId.Replace(':', '_')}");

    void Click(ShopOffer offer) => ButtonFor(offer).onClick.Invoke();

    T Make<T>(string name) where T : Component
    {
        var obj = new GameObject(name);
        _objects.Add(obj);
        return obj.AddComponent<T>();
    }

    GearData CreateGear(string id, int price)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = id;
        gear.displayName = id;
        gear.description = "Artifact effect";
        gear.price = price;
        _assets.Add(gear);
        return gear;
    }

    EnemyTypeData CreateEnemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 3, 3);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }
}
