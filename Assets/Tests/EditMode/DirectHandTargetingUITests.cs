using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Focused regression coverage for hand-first consumable target selection.</summary>
public sealed class DirectHandTargetingUITests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    ConsumableData _mirror, _torch, _spade;
    EventSystem _eventSystem;
    EnhancementTargetUI _picker;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _mirror = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/Mirror.asset");
        _torch = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/Torch.asset");
        _spade = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/SpadeRune.asset");
        Assert.That(new Object[] { _mirror, _torch, _spade }, Has.All.Not.Null);

        var handObject = Make("Direct target hand", typeof(RectTransform), typeof(Field));
        var field = handObject.GetComponent<Field>();
        field.cardsHolder = handObject.GetComponent<RectTransform>();
        var canvasObject = Make("Direct target canvas", typeof(Canvas));
        var prefabObject = Make("Direct target card prefab", typeof(RectTransform), typeof(Image), typeof(CardView));
        _cards = Make("Direct target cards", typeof(CardManager)).GetComponent<CardManager>();
        _cards.Configure(field, canvasObject.GetComponent<Canvas>(), prefabObject.GetComponent<CardView>(),
            System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>(), new[] { _mirror, _torch, _spade });
        _cards.ConfigureRandom(new DeterministicRandom(62019));
        _cards.BuildDeck();
        _combat = Make("Direct target combat", typeof(CombatManager)).GetComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        _eventSystem = Make("Direct target event system", typeof(EventSystem)).GetComponent<EventSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        foreach (var item in _created.AsEnumerable().Reverse())
            if (item) Object.DestroyImmediate(item);
        _created.Clear();
    }

    [Test]
    public void MirrorHandTarget_ConfirmConsumesOnceAndCreatesCopyThenClosesPicker()
    {
        Assert.That(_cards.AddConsumable(_mirror), Is.True);
        StartCombat();
        _cards.DealHand();
        int oldOwnedCount = _cards.ownedCards.Count;
        int oldHandCount = _cards.hand.Count;
        var view = HandView(_cards.hand[0]);
        CreatePicker();
        _picker.OpenConsumableTarget(0);
        view.OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });

        Assert.That(_picker.panel.activeSelf, Is.True);
        Assert.That(_picker.feedbackLabel.text, Does.Contain("1/1"));
        Assert.That(_picker.confirmButton.interactable, Is.True);
        Assert.That(_cards.hand.Count, Is.EqualTo(oldHandCount), "Targeting must not add a second hand entry.");
        _picker.confirmButton.onClick.Invoke();
        _picker.confirmButton.onClick.Invoke();

        Assert.That(_picker.panel.activeSelf, Is.False);
        Assert.That(_cards.IsHandEnhancementTargeting, Is.False);
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(oldOwnedCount + 1));
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
        Assert.That(_cards.hand.Count, Is.EqualTo(oldHandCount + 1));
        Assert.That(_cards.hand.Contains(_cards.ownedCards.Last()), Is.True);
    }

    [Test]
    public void MirrorHandTarget_BackCancelsWithoutCopyOrChargeAndRestoresCombatSelection()
    {
        Assert.That(_cards.AddConsumable(_mirror), Is.True);
        StartCombat();
        _cards.DealHand();
        var view = HandView(_cards.hand[0]);
        int oldOwnedCount = _cards.ownedCards.Count;
        CreatePicker();
        _picker.OpenConsumableTarget(0);
        view.OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        Assert.That(_picker.confirmButton.interactable, Is.True);

        _picker.backButton.onClick.Invoke();

        Assert.That(_picker.panel.activeSelf, Is.False);
        Assert.That(_cards.IsHandEnhancementTargeting, Is.False);
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(oldOwnedCount));
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
        Assert.That(_cards.SelectedCards, Is.Empty);
        _cards.HandleCardClick(view);
        Assert.That(_cards.SelectedCards, Does.Contain(view), "Normal combat card input resumes after cancellation.");
    }

    [Test]
    public void SuitRunePicker_EnablesAtMinimumCapsAtThreeAndCommitsSelectedHandCards()
    {
        Assert.That(_cards.AddConsumable(_spade), Is.True);
        StartCombat();
        _cards.DealHand();
        foreach (var card in _cards.hand)
            if (card.Suit == CardData.Suit.Spades) card.TryChangeSuit(CardData.Suit.Clubs);
        var targets = _cards.hand.Take(4).ToArray();
        CreatePicker();
        _picker.OpenConsumableTarget(0);
        Assert.That(_picker.confirmButton.interactable, Is.False);

        foreach (var card in targets.Take(3))
            HandView(card).OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        Assert.That(_picker.feedbackLabel.text, Does.Contain("3/3"));
        Assert.That(_picker.confirmButton.interactable, Is.True);
        HandView(targets[3]).OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        Assert.That(_picker.feedbackLabel.text, Does.Contain("3/3"), "Selection cannot exceed the rune maximum.");

        _picker.confirmButton.onClick.Invoke();

        Assert.That(targets.Take(3).All(card => card.Suit == CardData.Suit.Spades), Is.True);
        Assert.That(targets[3].Suit, Is.Not.EqualTo(CardData.Suit.Spades));
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(2));
        Assert.That(_picker.panel.activeSelf, Is.False);
    }

    [Test]
    public void ConsumablePicker_AutoClosesWhenItsSourceIsRemoved()
    {
        Assert.That(_cards.AddConsumable(_mirror), Is.True);
        StartCombat();
        _cards.DealHand();
        CreatePicker();
        _picker.OpenConsumableTarget(0);
        Assert.That(_picker.IsTargeting, Is.True);
        var source = _cards.GetConsumableInstanceAtSlot(0);

        Assert.That(_cards.RemoveConsumableInstanceAtSlot(0, source), Is.True);

        Assert.That(_picker.panel.activeSelf, Is.False);
        Assert.That(_cards.IsHandEnhancementTargeting, Is.False);
    }

    [Test]
    public void ConsumablePicker_AutoClosesWhenSelectedTargetIsRemoved()
    {
        Assert.That(_cards.AddConsumable(_mirror), Is.True);
        StartCombat();
        _cards.DealHand();
        var target = _cards.hand[0];
        CreatePicker();
        _picker.OpenConsumableTarget(0);
        HandView(target).OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        Assert.That(_picker.confirmButton.interactable, Is.True);

        Assert.That(_cards.DestroyOwnedCard(target), Is.True);

        Assert.That(_picker.panel.activeSelf, Is.False);
        Assert.That(_cards.IsHandEnhancementTargeting, Is.False);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
    }

    [Test]
    public void GearMergeMode_RoutesEligibleClicksOnlyToMergeCallback()
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.name = "Merge test artifact";
        _created.Add(gear);
        var slotObject = Make("Merge test artifact slot", typeof(RectTransform), typeof(Image), typeof(GearIconSlotUI));
        var slot = slotObject.GetComponent<GearIconSlotUI>();
        int detailClicks = 0;
        int mergeClicks = 0;
        slot.Bind(gear, null, null, _ => detailClicks++, 42, (_, _) => mergeClicks++,
            mergeSelected: false, mergeMode: true, mergeEligible: true);

        slot.OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        Assert.That(detailClicks, Is.Zero);
        Assert.That(mergeClicks, Is.EqualTo(1));

        slot.Bind(gear, null, null, _ => detailClicks++, 42, (_, _) => mergeClicks++,
            mergeSelected: false, mergeMode: true, mergeEligible: false);
        slot.OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        Assert.That(detailClicks, Is.Zero);
        Assert.That(mergeClicks, Is.EqualTo(1));
    }

    [Test]
    public void RealHandCardClick_RoutesToTargetHandlerWithoutAddingHandEntries()
    {
        _cards.DealHand();
        var view = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>(true)
            .Single(card => card.data == _cards.hand[0]);
        int before = _cards.hand.Count;
        CardView targeted = null;
        _cards.BeginHandTargeting(card => targeted = card, _ => true);

        view.OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });

        Assert.That(targeted, Is.SameAs(view));
        Assert.That(_cards.hand.Count, Is.EqualTo(before));
        Assert.That(_cards.handField.CardCount, Is.EqualTo(before));
        Assert.That(_cards.SelectedCards, Is.Empty);
        _cards.EndHandEnhancementTargeting();
    }

    [Test]
    public void MultiTargetConsumables_ExposeAuthoritativeMinimumMaximumAndRejectInvalidSets()
    {
        Assert.That(_cards.AddConsumable(_torch), Is.True);
        Assert.That(_cards.GetConsumableTargetMinimum(0), Is.EqualTo(1));
        Assert.That(_cards.GetConsumableTargetMaximum(0), Is.EqualTo(2));
        var torchTargets = _cards.ownedCards.Take(2).Select(card => card.Id).ToArray();
        Assert.That(_cards.CanConfirmConsumableTargets(0, torchTargets), Is.True);
        Assert.That(_cards.CanConfirmConsumableTargets(0, new[] { torchTargets[0], torchTargets[0] }), Is.False);
        Assert.That(_cards.CanConfirmConsumableTargets(0, _cards.ownedCards.Take(3).Select(card => card.Id).ToArray()), Is.False);

        Assert.That(_cards.AddConsumable(_spade), Is.True);
        _cards.DealHand();
        Assert.That(_cards.GetConsumableTargetMinimum(1), Is.EqualTo(1));
        Assert.That(_cards.GetConsumableTargetMaximum(1), Is.EqualTo(3));
        var runeTargets = _cards.hand.Where(card => card.Suit != CardData.Suit.Spades)
            .Take(3).Select(card => card.Id).ToArray();
        Assert.That(runeTargets, Has.Length.EqualTo(3));
        Assert.That(_cards.CanConfirmConsumableTargets(1, runeTargets.Take(2).ToArray()), Is.True);
        Assert.That(_cards.CanConfirmConsumableTargets(1, runeTargets), Is.True);
        Assert.That(_cards.CanConfirmConsumableTargets(1, runeTargets.Concat(new[] { runeTargets[0] }).ToArray()), Is.False);
    }

    [Test]
    public void TargetEligibility_RejectsRemovedTargetAndRemovedConsumableSource()
    {
        Assert.That(_cards.AddConsumable(_torch), Is.True);
        int targetId = _cards.ownedCards[0].Id;
        Assert.That(_cards.CanTargetConsumableAtSlot(0, targetId), Is.True);
        Assert.That(_cards.DestroyOwnedCard(_cards.FindOwnedCard(targetId)), Is.True);
        Assert.That(_cards.CanTargetConsumableAtSlot(0, targetId), Is.False);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
        Assert.That(_cards.CanConfirmConsumableTargets(0, new[] { targetId }), Is.False);
        Assert.That(_cards.GetConsumableAtSlot(0), Is.SameAs(_torch));

        var source = _cards.GetConsumableInstanceAtSlot(0);
        Assert.That(_cards.RemoveConsumableInstanceAtSlot(0, source), Is.True);
        Assert.That(_cards.GetConsumableAtSlot(0), Is.Null);
        Assert.That(_cards.GetConsumableTargetMaximum(0), Is.Zero);
        Assert.That(_cards.CanConfirmConsumableTargets(0, new[] { _cards.ownedCards[0].Id }), Is.False);
    }

    [Test]
    public void TargetingCancellation_ClearsPendingSelectionAndRestoresNormalCardSelection()
    {
        StartCombat();
        _cards.DealHand();
        var view = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>(true)
            .Single(card => card.data == _cards.hand[0]);
        _cards.BeginHandTargeting(_ => { }, _ => true);
        view.OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        Assert.That(_cards.IsHandEnhancementTargeting, Is.True);
        _cards.EndHandEnhancementTargeting();
        Assert.That(_cards.IsHandEnhancementTargeting, Is.False);
        Assert.That(_cards.SelectedCards, Is.Empty);

        view.OnPointerClick(new PointerEventData(_eventSystem) { button = PointerEventData.InputButton.Left });
        Assert.That(_cards.SelectedCards, Does.Contain(view));
    }

    CardView HandView(CardInstance card) => _cards.handField.cardsHolder.GetComponentsInChildren<CardView>(true)
        .Single(view => view.data == card);

    void StartCombat()
    {
        var enemy = EnemyTypeData.Create("Direct targeting enemy", 100, 1, 0);
        _created.Add(enemy);
        _combat.StartEnemy(new EnemyRuntime(enemy));
    }

    void CreatePicker()
    {
        var root = Make("Direct target picker root", typeof(RectTransform));
        root.SetActive(false);
        _picker = root.AddComponent<EnhancementTargetUI>();
        var panel = Make("Direct target picker panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        _picker.panel = panel;

        var dialog = Make("Direct target dialog", typeof(RectTransform), typeof(Image));
        dialog.transform.SetParent(panel.transform, false);
        _picker.titleLabel = MakeChild<TextMeshProUGUI>(dialog.transform, "Title");
        _picker.feedbackLabel = MakeChild<TextMeshProUGUI>(dialog.transform, "Feedback");
        _picker.confirmButton = MakeChild<Button>(dialog.transform, "Confirm");
        _picker.backButton = MakeChild<Button>(dialog.transform, "Back");
        var list = Make("Direct target off-hand list", typeof(RectTransform));
        list.transform.SetParent(dialog.transform, false);
        _picker.cardsContainer = list.transform;
        _picker.cardButtonPrefab = Make("Direct target off-hand card", typeof(RectTransform), typeof(Image), typeof(Button));
        root.SetActive(true);
        const System.Reflection.BindingFlags lifecycle = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(EnhancementTargetUI).GetMethod("OnDisable", lifecycle).Invoke(_picker, null);
        typeof(EnhancementTargetUI).GetMethod("OnEnable", lifecycle).Invoke(_picker, null);
    }

    T MakeChild<T>(Transform parent, string name) where T : Component
    {
        var go = Make(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    GameObject Make(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        _created.Add(go);
        return go;
    }
}