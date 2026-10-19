using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class Phase7EEnhancementTargetTests
{
    readonly List<Object> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    RunManager _run;
    CardEnhancementData _enhancement;
    EnhancementTargetUI _picker;
    EventSystem _eventSystem;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        _enhancement.id = "sharp";
        _enhancement.displayName = "Sharp";
        _enhancement.description = "Adds attack";
        _enhancement.price = 9;
        _assets.Add(_enhancement);
        _cards = Make<CardManager>("Cards");
        _eventSystem = Make<EventSystem>("Event System");
        var handField = Make<Field>("Hand Field");
        handField.cardsHolder = Make<RectTransform>("Hand Cards");
        handField.cardsHolder.SetParent(handField.transform, false);
        var canvas = Make<Canvas>("Drag Canvas");
        var prefabObject = new GameObject("Card View", typeof(RectTransform), typeof(Image), typeof(CardView));
        _objects.Add(prefabObject);
        _cards.Configure(handField, canvas, prefabObject.GetComponent<CardView>(),
            System.Array.Empty<GearData>(), new[] { _enhancement });
        var combat = Make<CombatManager>("Combat");
        combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Run");
        _run.thiefType = Enemy("Thief");
        _run.goblinType = Enemy("Goblin");
        _run.knightType = Enemy("Knight");
        _run.StartRunWithSeed("7E-TARGET-CHOICE");
    }

    [TearDown]
    public void TearDown()
    {
        if (_picker) _picker.gameObject.SetActive(false);
        GameplayInputGate.Clear();
        foreach (var obj in _objects)
            if (obj) Object.DestroyImmediate(obj);
        foreach (var asset in _assets)
            if (asset) Object.DestroyImmediate(asset);
        _objects.Clear();
        _assets.Clear();
    }

    [Test]
    public void Shop_AppliesToChosenInstanceOnly_AndChargesCachedPriceExactlyOnce()
    {
        var offer = OpenShopOffer();
        _cards.AddCredits(50);
        var chosen = _cards.hand[0];
        var untouched = _cards.hand[1];
        var notInHand = _cards.ownedCards.First(card => !_cards.hand.Contains(card));
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, notInHand.Id), Is.False,
            "Acquisition targeting must not expand from the hand to the rest of the owned deck.");
        Assert.That(_cards.credits, Is.EqualTo(50));
        int price = offer.price;
        offer.enhancement.price += 200;
        Assert.That(_run.PurchaseShopOffer(offer.StableId), Is.False, "Implicit target cannot purchase");
        Assert.That(_cards.credits, Is.EqualTo(50));
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, chosen.Id), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(50 - price));
        Assert.That(chosen.Enhancement, Is.SameAs(_enhancement));
        Assert.That(untouched.Enhancement, Is.Null);
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, chosen.Id), Is.False);
        Assert.That(_cards.credits, Is.EqualTo(50 - price));
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(40));
    }

    [Test]
    public void Shop_RequiresExplicitReplacementConfirmation_AndChargesOnlyAfterIt()
    {
        var offer = OpenShopOffer();
        _cards.AddCredits(40);
        var invalid = _cards.hand[0];
        var replacement = ScriptableObject.CreateInstance<CardEnhancementData>();
        replacement.id = "old-sharp";
        replacement.displayName = "Old Sharp";
        _assets.Add(replacement);
        Assert.That(_cards.ApplyEnhancement(invalid.Id, replacement), Is.True);
        invalid.EffectState.SetCounter(0, 7);
        invalid.EffectState.SetEncounterCounter(0, 4);
        invalid.GainPermanentAttackBonus(3);
        CardData.Suit preservedSuit = (CardData.Suit)(((int)invalid.Suit + 1) % 4);
        Assert.That(invalid.TryChangeSuit(preservedSuit), Is.True);
        int stableId = invalid.Id;
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, invalid.Id), Is.False,
            "Replacement must require explicit confirmation.");
        Assert.That(invalid.Enhancement, Is.SameAs(replacement));
        Assert.That(invalid.EffectState.GetCounter(0), Is.EqualTo(7));
        Assert.That(invalid.EffectState.GetEncounterCounter(0), Is.EqualTo(4));
        Assert.That(invalid.TryApplyEnhancement(replacement, true), Is.False,
            "Reapplying the exact Enhancement reference is a no-op.");
        Assert.That(invalid.EffectState.GetCounter(0), Is.EqualTo(7));
        Assert.That(invalid.EffectState.GetEncounterCounter(0), Is.EqualTo(4));
        Assert.That(_cards.credits, Is.EqualTo(40));
        var newEnhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        newEnhancement.id = "new-sharp";
        newEnhancement.displayName = "New Sharp";
        _assets.Add(newEnhancement);
        offer.enhancement = newEnhancement;
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, invalid.Id, true), Is.True);
        Assert.That(invalid.Enhancement, Is.SameAs(newEnhancement));
        Assert.That(invalid.Id, Is.EqualTo(stableId));
        Assert.That(invalid.EffectState.GetCounter(0), Is.Zero);
        Assert.That(invalid.EffectState.GetEncounterCounter(0), Is.Zero);
        Assert.That(invalid.PermanentAttackBonus, Is.EqualTo(3));
        Assert.That(invalid.Suit, Is.EqualTo(preservedSuit));
        Assert.That(_cards.credits, Is.EqualTo(40 - offer.price));
        invalid.EffectState.SetCounter(0, 9);
        invalid.EffectState.SetEncounterCounter(0, 6);
        int creditsAfterReplacement = _cards.credits;
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, invalid.Id, true), Is.False);
        Assert.That(_run.GetEnhancementAcquisitionTargetUnavailableReason(invalid.Id, newEnhancement),
            Is.EqualTo("Card already has this Mod"));
        Assert.That(_cards.credits, Is.EqualTo(creditsAfterReplacement));
        Assert.That(invalid.EffectState.GetCounter(0), Is.EqualTo(9));
        Assert.That(invalid.EffectState.GetEncounterCounter(0), Is.EqualTo(6));
    }

    [Test]
    public void RemovedOwnedCard_IsRejectedAtConfirmation_WithoutSpendingOrLosingOffer()
    {
        var offer = OpenShopOffer();
        _cards.AddCredits(40);
        int removedId = _cards.hand[0].Id;
        var collection = (CardCollection)typeof(CardManager)
            .GetField("_cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(_cards);
        collection.Initialize(_cards.ownedCards.Where(card => card.Id != removedId).ToArray());
        Assert.That(_cards.FindOwnedCard(removedId), Is.Null);
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, removedId), Is.False);
        Assert.That(_cards.credits, Is.EqualTo(40));
        Assert.That(_run.GetCurrentShopOffers(), Does.Contain(offer));
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(39));
        _cards.DealHand();
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, _cards.hand[0].Id), Is.True);
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(39));
        Assert.That(_cards.credits, Is.EqualTo(40 - offer.price));
    }

    [Test]
    public void Shop_RejectsRemovedOfferInvalidPriceAndClosedNode()
    {
        var offer = OpenShopOffer();
        _cards.AddCredits(40);
        int cardId = _cards.hand[0].Id;
        offer.price = -1;
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, cardId), Is.False);
        offer.price = 9;
        ((List<ShopOffer>)_run.GetCurrentShopOffers()).Remove(offer);
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, cardId), Is.False);
        Assert.That(_cards.credits, Is.EqualTo(40));
        Assert.That(_cards.FindOwnedCard(cardId).Enhancement, Is.Null);
        _run.OnShopDone();
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, cardId), Is.False);
    }

    [Test]
    public void Upgrade_AppliesToPlayerChosenCard_OnlyOnce_WithoutChargingCredits()
    {
        var offer = OpenUpgradeOffer();
        var chosen = _cards.hand[0];
        var other = _cards.hand[1];
        _cards.AddCredits(18);
        Assert.That(_run.ChooseUpgradeOffer(0, chosen.Id), Is.True);
        Assert.That(chosen.Enhancement, Is.SameAs(offer.enhancement));
        Assert.That(other.Enhancement, Is.Null);
        Assert.That(_cards.credits, Is.EqualTo(18));
        Assert.That(_run.ActiveNode, Is.Null);
        Assert.That(_run.ChooseUpgradeOffer(0, other.Id), Is.False);
    }

    [Test]
    public void Upgrade_InvalidTargetLeavesNodeAndOfferIntact_ForRetry()
    {
        var offer = OpenUpgradeOffer();
        var invalid = _cards.hand[0];
        _cards.ApplyEnhancement(invalid.Id, _enhancement);
        Assert.That(_run.ChooseUpgradeOffer(0, invalid.Id), Is.False,
            "Already enhanced cards require explicit replacement confirmation.");
        Assert.That(_run.ChooseUpgradeOffer(0, 9999), Is.False);
        Assert.That(_run.ActiveNode.kind, Is.EqualTo(MapNodeType.Upgrade));
        Assert.That(_run.GetCurrentUpgradeOffers(), Does.Contain(offer));
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(40));
        Assert.That(_run.ChooseUpgradeOffer(0, _cards.hand[1].Id), Is.True);
    }

    [Test]
    public void Upgrade_ReplacementRequiresExplicitConfirmationAndPreservesCardIdentity()
    {
        var offer = OpenUpgradeOffer();
        var existing = ScriptableObject.CreateInstance<CardEnhancementData>();
        existing.id = "upgrade-old";
        existing.displayName = "Existing";
        _assets.Add(existing);
        var target = _cards.hand[0];
        int stableId = target.Id;
        Assert.That(_cards.ApplyEnhancement(stableId, existing), Is.True);
        Assert.That(_run.ChooseUpgradeOffer(0, stableId), Is.False);
        Assert.That(target.Enhancement, Is.SameAs(existing));
        Assert.That(_run.ActiveNode.kind, Is.EqualTo(MapNodeType.Upgrade));
        Assert.That(_run.ChooseUpgradeOffer(0, stableId, true), Is.True);
        Assert.That(target.Id, Is.EqualTo(stableId));
        Assert.That(target.Enhancement, Is.SameAs(offer.enhancement));
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(40));
    }

    [Test]
    public void EqualSuitRankInstances_RemainIndependent_WhenOnlyOneIsChosen()
    {
        var offer = OpenShopOffer();
        _cards.AddCredits(40);
        var original = _cards.hand[0];
        var duplicate = new CardInstance(original.Definition, 9000);
        // Establish a second owned instance of the same definition through the model, not a card view.
        var collection = (CardCollection)typeof(CardManager)
            .GetField("_cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(_cards);
        Assert.That(collection.AddOwnedToDeck(duplicate), Is.True);
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, duplicate.Id), Is.False);
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, original.Id), Is.True);
        Assert.That(original.Enhancement, Is.SameAs(_enhancement));
        Assert.That(duplicate.Enhancement, Is.Null);
        Assert.That(original.Suit, Is.EqualTo(duplicate.Suit));
        Assert.That(original.Rank, Is.EqualTo(duplicate.Rank));
    }

    [Test]
    public void TargetChoice_DoesNotChangeOfferIdentityOrOtherSeededStreams()
    {
        var first = OpenShopOffer();
        var offerIds = string.Join(",", _run.GetCurrentShopOffers().Select(o => o.StableId));
        string mapSignature = string.Join(",", _run.GenerateMapForIndex(4).Select(n => $"{n.id}:{n.kind}:{n.contentId}"));
        _cards.AddCredits(40);
        Assert.That(_run.PurchaseShopEnhancement(first.StableId, _cards.hand[0].Id), Is.True);
        string afterChoice = string.Join(",", _run.GenerateMapForIndex(4).Select(n => $"{n.id}:{n.kind}:{n.contentId}"));
        Assert.That(afterChoice, Is.EqualTo(mapSignature));
        Assert.That(string.Join(",", _run.GetCurrentShopOffers().Select(o => o.StableId)), Is.EqualTo(offerIds));

        _run.RestartRun();
        for (int i = 0; i < 5; i++) _cards.ShuffleDeck();
        var repeated = OpenShopOffer();
        Assert.That(string.Join(",", _run.GetCurrentShopOffers().Select(o => o.StableId)), Is.EqualTo(offerIds));
        Assert.That(repeated.StableId, Does.StartWith("enhancement:"));
        Assert.That(repeated.slot, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void UpgradeOfferIdentity_AndUnrelatedMapRng_DoNotDependOnChosenTarget()
    {
        OpenUpgradeOffer();
        string ids = string.Join(",", _run.GetCurrentUpgradeOffers().Select(o => o.StableId));
        string map = string.Join(",", _run.GenerateMapForIndex(5).Select(n => $"{n.id}:{n.kind}:{n.contentId}"));
        Assert.That(_run.ChooseUpgradeOffer(0, _cards.hand[0].Id), Is.True);
        Assert.That(string.Join(",", _run.GenerateMapForIndex(5).Select(n => $"{n.id}:{n.kind}:{n.contentId}")), Is.EqualTo(map));
        _run.RestartRun();
        for (int i = 0; i < 5; i++) _cards.ShuffleDeck();
        OpenUpgradeOffer();
        Assert.That(string.Join(",", _run.GetCurrentUpgradeOffers().Select(o => o.StableId)), Is.EqualTo(ids));
        Assert.That(_run.ChooseUpgradeOffer(0, _cards.hand[0].Id), Is.True);
        Assert.That(string.Join(",", _run.GenerateMapForIndex(5).Select(n => $"{n.id}:{n.kind}:{n.contentId}")), Is.EqualTo(map));
    }

    [Test]
    public void HandFieldClick_ArmsRealCardView_RequiresExplicitReplacementAndChargesOnce()
    {
        var offer = OpenShopOffer();
        _cards.AddCredits(40);
        var oldEnhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        oldEnhancement.id = "old";
        oldEnhancement.displayName = "Old Sharp";
        _assets.Add(oldEnhancement);
        var target = _cards.hand[0];
        Assert.That(_cards.ApplyEnhancement(target.Id, oldEnhancement), Is.True);
        MakePicker();
        _picker.OpenShop(offer.StableId);

        Assert.That(_cards.IsHandEnhancementTargeting, Is.True);
        Assert.That(_picker.cardsContainer.GetComponentInParent<ScrollRect>(true).gameObject.activeSelf, Is.False,
            "Acquisition targets are selected directly in the hand, not a second card list.");
        var dialog = _picker.titleLabel.rectTransform.parent as RectTransform;
        Assert.That(dialog.sizeDelta, Is.EqualTo(new Vector2(440f, 170f)));
        var view = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>(true)
            .Single(card => card.data == target);
        view.OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        Assert.That(_picker.SelectedCardId, Is.EqualTo(target.Id));
        int creditsBefore = _cards.credits;
        _picker.confirmButton.onClick.Invoke();
        Assert.That(_picker.feedbackLabel.text, Does.Contain("Confirm again"));
        Assert.That(_cards.credits, Is.EqualTo(creditsBefore), "First confirmation only authorizes replacement.");
        Assert.That(target.Enhancement, Is.SameAs(oldEnhancement));
        _picker.confirmButton.onClick.Invoke();
        Assert.That(target.Enhancement, Is.SameAs(_enhancement));
        Assert.That(_cards.credits, Is.EqualTo(creditsBefore - offer.price));
        Assert.That(_cards.IsHandEnhancementTargeting, Is.False);
        _picker.confirmButton.onClick.Invoke();
        Assert.That(_cards.credits, Is.EqualTo(creditsBefore - offer.price), "Repeated confirmation cannot charge twice.");
    }

    [Test]
    public void HandFieldTarget_CancelAndRestartClearPendingChoiceWithoutPayment()
    {
        var offer = OpenShopOffer();
        _cards.AddCredits(40);
        var shopUI = Make<ShopUI>("Shop UI");
        var sourceModal = new GameObject("Shop Modal", typeof(RectTransform));
        _objects.Add(sourceModal);
        shopUI.panel = sourceModal;
        sourceModal.SetActive(true);
        MakePicker();
        _picker.OpenShop(offer.StableId);
        Assert.That(sourceModal.activeSelf, Is.False);
        Assert.That(_picker.panel.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(460f, 190f)));
        var view = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>(true)
            .Single(card => card.data == _cards.hand[0]);
        view.OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        _picker.backButton.onClick.Invoke();
        Assert.That(_cards.IsHandEnhancementTargeting, Is.False);
        Assert.That(sourceModal.activeSelf, Is.True, "Cancellation restores the originating Shop modal.");
        Assert.That(_picker.titleLabel.rectTransform.parent.GetComponent<RectTransform>().sizeDelta,
            Is.EqualTo(new Vector2(920f, 760f)), "The authored card layout is restored after targeting.");
        Assert.That(_picker.cardsContainer.GetComponentInParent<ScrollRect>(true).gameObject.activeSelf, Is.True);
        Assert.That(_cards.credits, Is.EqualTo(40));

        _picker.OpenShop(offer.StableId);
        Assert.That(sourceModal.activeSelf, Is.False);
        view = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>(true)
            .Single(card => card.data == _cards.hand[1]);
        view.OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        _run.RestartRun();
        Assert.That(_cards.IsHandEnhancementTargeting, Is.False);
        Assert.That(_picker.panel.activeSelf, Is.False);
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_cards.ownedCards.All(card => card.Enhancement == null), Is.True);
    }

    [Test]
    public void UpgradeTargetCancel_DoesNotCompleteNode()
    {
        var offer = OpenUpgradeOffer();
        MakePicker();
        _picker.OpenUpgrade(0);
        var view = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>(true)
            .Single(card => card.data == _cards.hand[0]);
        view.OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        _picker.backButton.onClick.Invoke();
        Assert.That(_run.ActiveNode.kind, Is.EqualTo(MapNodeType.Upgrade));
        Assert.That(_run.GetCurrentUpgradeOffers(), Does.Contain(offer));
        Assert.That(_cards.IsHandEnhancementTargeting, Is.False);
    }

    ShopOffer OpenShopOffer()
    {
        var node = _run.currentPath.First(n => n.kind == MapNodeType.Shop);
        node.accessible = true;
        _run.OnPathChosen(node.id);
        // This fixture exercises the retained legacy target-purchase API directly;
        // production Shop generation is separately required to expose only mapped Tricks.
        var legacyOffer = new ShopOffer { kind = ShopOfferKind.Enhancement,
            enhancement = _enhancement, basePrice = _enhancement.price, price = _enhancement.price };
        ((List<ShopOffer>)_run.GetCurrentShopOffers()).Add(legacyOffer);
        return legacyOffer;
    }

    ShopOffer OpenUpgradeOffer()
    {
        var node = _run.currentPath.First(n => n.kind == MapNodeType.Upgrade);
        node.accessible = true;
        _run.OnPathChosen(node.id);
        return _run.GetCurrentUpgradeOffers()[0];
    }

    void MakePicker()
    {
        var root = new GameObject("Picker");
        _objects.Add(root);
        root.SetActive(false);
        _picker = root.AddComponent<EnhancementTargetUI>();
        _picker.panel = new GameObject("Target panel", typeof(RectTransform), typeof(Image));
        _picker.panel.transform.SetParent(root.transform, false);
        _picker.panel.SetActive(false);
        _objects.Add(_picker.panel);
        var dialogObject = new GameObject("TargetPickerCard", typeof(RectTransform), typeof(Image));
        _objects.Add(dialogObject);
        dialogObject.transform.SetParent(_picker.panel.transform, false);
        var dialogRect = dialogObject.GetComponent<RectTransform>();
        dialogRect.sizeDelta = new Vector2(920f, 760f);

        var viewportObject = new GameObject("CardViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        _objects.Add(viewportObject);
        viewportObject.transform.SetParent(dialogObject.transform, false);
        var contentObject = new GameObject("OwnedCardGrid", typeof(RectTransform));
        _objects.Add(contentObject);
        contentObject.transform.SetParent(viewportObject.transform, false);
        var scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewportObject.GetComponent<RectTransform>();
        scroll.content = contentObject.GetComponent<RectTransform>();
        _picker.cardsContainer = contentObject.transform;

        var prefab = new GameObject("Card choice prefab", typeof(RectTransform), typeof(Image), typeof(Button));
        _objects.Add(prefab);
        new GameObject("Card label", typeof(RectTransform), typeof(TextMeshProUGUI)).transform.SetParent(prefab.transform);
        _picker.cardButtonPrefab = prefab;
        _picker.titleLabel = MakeChild<TextMeshProUGUI>(dialogObject.transform, "Title");
        _picker.feedbackLabel = MakeChild<TextMeshProUGUI>(dialogObject.transform, "Feedback");
        _picker.confirmButton = MakeChild<Button>(dialogObject.transform, "Confirm");
        _picker.backButton = MakeChild<Button>(dialogObject.transform, "Back");
        root.SetActive(true);
        const System.Reflection.BindingFlags lifecycle = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(EnhancementTargetUI).GetMethod("OnDisable", lifecycle).Invoke(_picker, null);
        typeof(EnhancementTargetUI).GetMethod("OnEnable", lifecycle).Invoke(_picker, null);
    }

    T MakeChild<T>(Transform parent, string name) where T : Component
    {
        var obj = new GameObject(name, typeof(RectTransform));
        _objects.Add(obj);
        obj.transform.SetParent(parent, false);
        return obj.AddComponent<T>();
    }

    T Make<T>(string name) where T : Component
    {
        var obj = new GameObject(name);
        _objects.Add(obj);
        return obj.AddComponent<T>();
    }

    EnemyTypeData Enemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 3, 3);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }
}
