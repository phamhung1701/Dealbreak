using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DeckMutationTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    ConsumableData _mirror, _torch, _spade;

    [SetUp]
    public void Setup()
    {
        GameplayInputGate.Clear();
        _mirror = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/Mirror.asset");
        _torch = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/Torch.asset");
        _spade = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/SpadeRune.asset");
        Assert.That(new Object[] { _mirror, _torch, _spade }, Has.All.Not.Null);
        var cardsObject = new GameObject("Mutation cards"); _created.Add(cardsObject);
        _cards = cardsObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, System.Array.Empty<GearData>(),
            System.Array.Empty<CardEnhancementData>(), new[] { _mirror, _torch, _spade });
        _cards.BuildDeck();
        var combatObject = new GameObject("Mutation combat"); _created.Add(combatObject);
        _combat = combatObject.AddComponent<CombatManager>();
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
    }

    [Test]
    public void Mirror_ClonesPermanentStateWithoutTransientState_AndGivesNewIdentity()
    {
        var source = _cards.ownedCards[0];
        var enhancement = ScriptableObject.CreateInstance<CardEnhancementData>(); _created.Add(enhancement);
        Assert.That(_cards.ApplyEnhancement(source.Id, enhancement), Is.True);
        source.GainPermanentAttackBonus(4);
        source.TryChangeSuit(CardData.Suit.Spades);
        source.EffectState.ClearEncounter();
        Assert.That(_cards.AddConsumable(_mirror), Is.True);
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { source.Id }), Is.True);
        var copy = _cards.ownedCards.Last();
        Assert.That(copy.Id, Is.Not.EqualTo(source.Id));
        Assert.That(copy.Definition, Is.SameAs(source.Definition));
        Assert.That(copy.Suit, Is.EqualTo(source.Suit));
        Assert.That(copy.Enhancement, Is.SameAs(enhancement));
        Assert.That(copy.PermanentAttackBonus, Is.EqualTo(4));
        Assert.That(copy.EffectState, Is.Not.SameAs(source.EffectState));
        Assert.That(_cards.deck.Contains(copy), Is.True);
        Assert.That(_cards.hand.Contains(copy), Is.False);
    }

    [Test]
    public void Mirror_InCombat_RequiresHandAndAddsCopyToHand()
    {
        _cards.DrawToHand(1);
        var enemy = EnemyTypeData.Create("Mutation enemy", 100, 1, 0); _created.Add(enemy);
        _combat.StartEnemy(new EnemyRuntime(enemy));
        Assert.That(_cards.AddConsumable(_mirror), Is.True);
        var outside = _cards.deck[0];
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { outside.Id }), Is.False);
        int oldCount = _cards.ownedCards.Count;
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { _cards.hand[0].Id }), Is.True);
        var copy = _cards.ownedCards.Last();
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(oldCount + 1));
        Assert.That(_cards.hand.Contains(copy), Is.True);
        Assert.That(_cards.discardPile.Contains(copy), Is.False);
    }

    [Test]
    public void Mirror_InCombat_AddsCopyToHandEvenWhenHandIsFull()
    {
        _cards.DrawToHand(40);
        var enemy = EnemyTypeData.Create("Full hand Mirror enemy", 100, 1, 0); _created.Add(enemy);
        _combat.StartEnemy(new EnemyRuntime(enemy));
        Assert.That(_cards.AddConsumable(_mirror), Is.True);
        int capacity = _cards.HandCapacity;
        Assert.That(_cards.hand.Count, Is.EqualTo(capacity));
        var source = _cards.hand[0];
        int oldOwnedCount = _cards.ownedCards.Count;

        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { source.Id }), Is.True);

        var copy = _cards.ownedCards.Last();
        Assert.That(copy.Id, Is.Not.EqualTo(source.Id));
        Assert.That(_cards.hand.Count, Is.EqualTo(capacity + 1));
        Assert.That(_cards.hand.Contains(copy), Is.True);
        Assert.That(_cards.deck.Count + _cards.hand.Count + _cards.discardPile.Count, Is.EqualTo(_cards.ownedCards.Count));
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(oldOwnedCount + 1));
    }

    [Test]
    public void Torch_RejectsDuplicateIds_AndPreservesLastCard()
    {
        Assert.That(_cards.AddConsumable(_torch), Is.True);
        int first = _cards.ownedCards[0].Id;
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { first, first }), Is.False);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
        var ids = _cards.ownedCards.Select(card => card.Id).Skip(1).ToArray();
        foreach (int id in ids)
            Assert.That(_cards.DestroyOwnedCard(_cards.FindOwnedCard(id)), Is.True);
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { first }), Is.False);
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(1));
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(1));
    }

    [Test]
    public void Rune_SameSuitAndCancellationCostNothing_ThenOneChargeChangesUpToThreeHandCards()
    {
        _cards.DealHand();
        Assert.That(_cards.AddConsumable(_spade), Is.True);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
        var same = _cards.hand.FirstOrDefault(card => card.Suit == CardData.Suit.Spades);
        if (same != null)
            Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { same.Id }), Is.False);
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(_spade.uses));
        var others = _cards.hand.Where(card => card.Suit != CardData.Suit.Spades).Take(3).ToArray();
        Assert.That(others.Length, Is.GreaterThan(0));
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, others.Select(card => card.Id).ToArray()), Is.True);
        foreach (var card in others)
        {
            Assert.That(card.Suit, Is.EqualTo(CardData.Suit.Spades));
            Assert.That(card.DisplayName, Does.Contain("♠"));
            Assert.That(card.MatchesSuit(CardData.Suit.Spades), Is.True);
        }
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero, "Runes are single-use since the 1-charge content change.");
    }

    [Test]
    public void CapacityAndNewRunReset_AreIndependentOfRuneCharges()
    {
        Assert.That(_cards.AddConsumable(_spade, 3), Is.True);
        Assert.That(_cards.AddConsumable(_mirror), Is.False);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(3));
        _cards.Reset();
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
        _cards.BuildDeck();
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(40));
        Assert.That(_cards.ownedCards.All(card => !card.HasSuitOverride), Is.True);
    }

    [Test]
    public void Torch_RemovingAHandCardAlsoRemovesItsTrackedView()
    {
        var holder = new GameObject("Mutation hand", typeof(RectTransform), typeof(Field));
        _created.Add(holder);
        var field = holder.GetComponent<Field>();
        field.cardsHolder = holder.GetComponent<RectTransform>();
        var canvasObject = new GameObject("Mutation canvas", typeof(Canvas));
        _created.Add(canvasObject);
        var prefab = AssetDatabase.LoadAssetAtPath<CardView>("Assets/Prefabs/Card.prefab");
        Assert.That(prefab, Is.Not.Null);
        _cards.Configure(field, canvasObject.GetComponent<Canvas>(), prefab,
            System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>(),
            new[] { _mirror, _torch, _spade });
        _cards.DrawToHand(1);
        Assert.That(field.CardCount, Is.EqualTo(1));
        var target = _cards.hand[0];
        Assert.That(_cards.AddConsumable(_torch), Is.True);
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { target.Id }), Is.True);
        Assert.That(field.CardCount, Is.Zero);
        Assert.That(_cards.FindOwnedCard(target.Id), Is.Null);
        Assert.That(_cards.hand.Contains(target), Is.False);
    }

    [Test]
    public void InputGate_BlocksMutationWithoutSpendingCharges()
    {
        Assert.That(_cards.AddConsumable(_spade), Is.True);
        var target = _cards.ownedCards.First(card => card.Suit != CardData.Suit.Spades);
        GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, true);
        try
        {
            Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { target.Id }), Is.False);
            Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(_spade.uses));
        }
        finally { GameplayInputGate.Clear(); }
    }

    [Test]
    public void CatalogDefinitions_AreAvailableFromShopDropsAndEvents()
    {
        var expected = new[] { "con_004", "con_005", "con_016", "con_017", "con_018", "con_019" };
        var items = AssetDatabase.FindAssets("t:ConsumableData", new[] { "Assets/Data/Consumables" })
            .Select(id => AssetDatabase.LoadAssetAtPath<ConsumableData>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(item => item != null && expected.Contains(item.canonicalId)).ToArray();
        CollectionAssert.AreEquivalent(expected, items.Select(item => item.canonicalId));
        Assert.That(items.Count(item => item.effectType == ConsumableEffectType.ChangeSuit), Is.EqualTo(4));
        foreach (var item in items)
        {
            Assert.That(item.shopAvailable, Is.True);
            Assert.That(item.dropAvailable, Is.True);
            Assert.That(item.eventAvailable, Is.True);
        }
        Assert.That(items.Where(item => item.effectType == ConsumableEffectType.ChangeSuit)
            .All(item => item.uses == 1), Is.True);
    }
}
