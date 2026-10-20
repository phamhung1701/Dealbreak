using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Card Tricks are dropped on hand cards; the drop rules validate, preview and commit.</summary>
public sealed class DirectHandTargetingUITests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    ConsumableData _mirror, _torch, _spade;
    EventSystem _eventSystem;

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
    public void MirrorDroppedOnHandCard_PreviewsCopyAndCreatesItInHand()
    {
        Assert.That(_cards.AddConsumable(_mirror), Is.True);
        StartCombat();
        _cards.DealHand();
        var source = _cards.hand[0];
        int oldOwnedCount = _cards.ownedCards.Count;

        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.HandCard(source), out var preview), Is.True);
        Assert.That(preview, Is.EqualTo("COPY"));
        Assert.That(ItemDropRules.DropTrick(0, ItemDropTarget.HandCard(source)), Is.True);

        Assert.That(_cards.ownedCards.Count, Is.EqualTo(oldOwnedCount + 1));
        Assert.That(_cards.hand.Contains(_cards.ownedCards.Last()), Is.True);
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
    }

    [Test]
    public void CardTricks_RejectDrawPileCardsEnemiesAndTheHpBar()
    {
        Assert.That(_cards.AddConsumable(_torch), Is.True);
        StartCombat();
        _cards.DrawToHand(1);
        var deckCard = _cards.deck[0];

        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.HandCard(deckCard), out _), Is.False);
        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.OnEnemy(_combat.Enemies[0]), out _), Is.False);
        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.PlayerHealth, out _), Is.False);
        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.BlackMarket, out _), Is.False, "Selling needs a Black Market.");
        Assert.That(ItemDropRules.DropTrick(0, ItemDropTarget.HandCard(deckCard)), Is.False);
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(2));

        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.HandCard(_cards.hand[0]), out var preview), Is.True);
        Assert.That(preview, Is.EqualTo("DESTROY"));
    }

    [Test]
    public void SuitRuneDrop_ChangesOneHandCardAndSpendsOneCharge()
    {
        Assert.That(_cards.AddConsumable(_spade), Is.True);
        StartCombat();
        _cards.DealHand();
        var target = _cards.hand.First(card => card.Suit != CardData.Suit.Spades);
        var spade = _cards.hand.FirstOrDefault(card => card.Suit == CardData.Suit.Spades);
        if (spade != null)
            Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.HandCard(spade), out _), Is.False,
                "A card already of the suit is not a target.");

        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.HandCard(target), out var preview), Is.True);
        Assert.That(preview, Is.EqualTo("→ ♠"));
        Assert.That(ItemDropRules.DropTrick(0, ItemDropTarget.HandCard(target)), Is.True);

        Assert.That(target.Suit, Is.EqualTo(CardData.Suit.Spades));
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(2));
    }

    [Test]
    public void ModTrickDroppedOnHandCard_AppliesItsModToThatCardOnly()
    {
        var whetstone = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/Whetstone.asset");
        Assert.That(whetstone, Is.Not.Null);
        _cards.consumableCatalog.Add(whetstone);
        Assert.That(_cards.AddConsumable(whetstone), Is.True);
        StartCombat();
        _cards.DealHand();
        var target = _cards.hand[1];

        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.HandCard(target), out var preview), Is.True);
        Assert.That(preview, Is.EqualTo("+ " + whetstone.enhancementToApply.displayName.ToUpperInvariant()));
        Assert.That(ItemDropRules.DropTrick(0, ItemDropTarget.HandCard(target)), Is.True);

        Assert.That(target.Enhancement, Is.SameAs(whetstone.enhancementToApply));
        Assert.That(_cards.hand.Count(card => card.Enhancement != null), Is.EqualTo(1));
        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.HandCard(target), out _), Is.False, "The Trick is spent.");
    }

    [Test]
    public void TrickDroppedOnAnotherSlot_Reorders()
    {
        Assert.That(_cards.AddConsumable(_mirror), Is.True);
        Assert.That(_cards.AddConsumable(_torch), Is.True);
        Assert.That(ItemDropRules.CanDropTrick(0, ItemDropTarget.TrickSlot(0), out _), Is.False);
        Assert.That(ItemDropRules.DropTrick(0, ItemDropTarget.TrickSlot(1)), Is.True);
        Assert.That(_cards.GetConsumableAtSlot(0), Is.SameAs(_torch));
        Assert.That(_cards.GetConsumableAtSlot(1), Is.SameAs(_mirror));
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
    public void CardConsumables_TargetExactlyOneHandCardPerUse()
    {
        Assert.That(_cards.AddConsumable(_torch), Is.True);
        Assert.That(_cards.AddConsumable(_spade), Is.True);
        Assert.That(_cards.CanConfirmConsumableTargets(0, new[] { _cards.deck[0].Id }), Is.False,
            "Draw Pile cards are never targets.");
        _cards.DealHand();
        for (int slot = 0; slot < 2; slot++)
        {
            Assert.That(_cards.GetConsumableTargetMinimum(slot), Is.EqualTo(1));
            Assert.That(_cards.GetConsumableTargetMaximum(slot), Is.EqualTo(1));
        }
        var targets = _cards.hand.Where(card => card.Suit != CardData.Suit.Spades)
            .Take(2).Select(card => card.Id).ToArray();
        Assert.That(targets, Has.Length.EqualTo(2));
        Assert.That(_cards.CanConfirmConsumableTargets(0, new[] { targets[0] }), Is.True);
        Assert.That(_cards.CanConfirmConsumableTargets(0, targets), Is.False);
        Assert.That(_cards.CanConfirmConsumableTargets(1, new[] { targets[0] }), Is.True);
        Assert.That(_cards.CanConfirmConsumableTargets(1, targets), Is.False);
    }

    [Test]
    public void TargetEligibility_RejectsRemovedTargetAndRemovedConsumableSource()
    {
        Assert.That(_cards.AddConsumable(_torch), Is.True);
        _cards.DrawToHand(1);
        int targetId = _cards.hand[0].Id;
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

    void StartCombat()
    {
        var enemy = EnemyTypeData.Create("Direct targeting enemy", 100, 1, 0);
        _created.Add(enemy);
        _combat.StartEnemy(new EnemyRuntime(enemy));
    }

    GameObject Make(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        _created.Add(go);
        return go;
    }
}