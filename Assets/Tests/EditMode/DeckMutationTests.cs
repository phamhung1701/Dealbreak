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
        _mirror = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Tricks/CloneVial.asset");
        _torch = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Tricks/Shears.asset");
        _spade = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Tricks/SpadeCrystal.asset");
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
        Assert.That(_cards.AddConsumable(_mirror), Is.True);
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { _cards.deck[0].Id }), Is.False,
            "Card Tricks are dropped on hand cards, so a Draw Pile card is never a target.");
        _cards.DrawToHand(1);
        var source = _cards.hand[0];
        var enhancement = ScriptableObject.CreateInstance<CardEnhancementData>(); _created.Add(enhancement);
        Assert.That(_cards.ApplyEnhancement(source.Id, enhancement), Is.True);
        source.GainPermanentAttackBonus(4);
        source.TryChangeSuit(CardData.Suit.Spades);
        source.EffectState.ClearEncounter();
        int oldOwnedCount = _cards.ownedCards.Count;
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { source.Id }), Is.True);
        Assert.That(_mirror.copies, Is.EqualTo(2), "Clone Vial makes two copies, like Balatro's Cryptid.");
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(oldOwnedCount + 2));
        var copies = _cards.ownedCards.Skip(oldOwnedCount).ToArray();
        Assert.That(copies.Select(copy => copy.Id).Distinct().Count(), Is.EqualTo(2));
        foreach (var copy in copies)
        {
            Assert.That(copy.Id, Is.Not.EqualTo(source.Id));
            Assert.That(copy.Definition, Is.SameAs(source.Definition));
            Assert.That(copy.Suit, Is.EqualTo(source.Suit));
            Assert.That(copy.Enhancement, Is.SameAs(enhancement));
            Assert.That(copy.PermanentAttackBonus, Is.EqualTo(4));
            Assert.That(copy.EffectState, Is.Not.SameAs(source.EffectState));
            Assert.That(_cards.hand.Contains(copy), Is.True, "Outside combat the copies also join the hand.");
            Assert.That(_cards.deck.Contains(copy), Is.False);
        }
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
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(oldCount + 2));
        foreach (var copy in _cards.ownedCards.Skip(oldCount))
        {
            Assert.That(_cards.hand.Contains(copy), Is.True);
            Assert.That(_cards.discardPile.Contains(copy), Is.False);
        }
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
        Assert.That(_cards.hand.Count, Is.EqualTo(capacity + 2), "Copies join the hand even past its size.");
        Assert.That(_cards.hand.Contains(copy), Is.True);
        Assert.That(_cards.deck.Count + _cards.hand.Count + _cards.discardPile.Count, Is.EqualTo(_cards.ownedCards.Count));
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(oldOwnedCount + 2));
    }

    [Test]
    public void Torch_TakesOneHandCardPerUse_AndDestroyingTheLastCardLosesTheRun()
    {
        Assert.That(_cards.AddConsumable(_torch), Is.True);
        _cards.DrawToHand(2);
        int first = _cards.hand[0].Id, second = _cards.hand[1].Id;
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { first, first }), Is.False);
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { first, second }), Is.False,
            "Each drop destroys one card.");
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { second }), Is.True);
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(1));
        var ids = _cards.ownedCards.Select(card => card.Id).Where(id => id != first).ToArray();
        foreach (int id in ids)
            Assert.That(_cards.DestroyOwnedCard(_cards.FindOwnedCard(id)), Is.True);
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(1));
        Assert.That(_combat.currentState, Is.Not.EqualTo(GameState.GameOver));
        EncounterResult? result = null;
        _combat.OnEncounterResult += value => result = value;

        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { first }), Is.True,
            "Shears may take the last card.");

        Assert.That(_cards.ownedCards, Is.Empty);
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
        Assert.That(result, Is.EqualTo(EncounterResult.Defeat), "With no cards left the run is lost at once.");
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameOver));
    }

    [Test]
    public void DestroyingTheLastCardInCombat_IsADefeat()
    {
        var enemy = EnemyTypeData.Create("Deck-out enemy", 100, 1, 0); _created.Add(enemy);
        _combat.StartEnemy(new EnemyRuntime(enemy));
        EncounterResult? result = null;
        _combat.OnEncounterResult += value => result = value;
        foreach (var card in _cards.ownedCards.ToArray())
            Assert.That(_cards.DestroyOwnedCard(card), Is.True);

        Assert.That(result, Is.EqualTo(EncounterResult.Defeat));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameOver));
    }

    [Test]
    public void Rune_SpendsOneChargePerHandCard_SameSuitIncluded_ThenSpendsThreeInOneSlot()
    {
        Assert.That(_cards.AddConsumable(_spade), Is.True);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
        var outside = _cards.deck.First();
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { outside.Id }), Is.False,
            "A Draw Pile card is never a target.");
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(3));
        _cards.DrawToHand(8);
        var same = _cards.hand.First(card => card.Suit != CardData.Suit.Spades);
        same.TryChangeSuit(CardData.Suit.Spades);
        Assert.That(_cards.CanTargetDeckMutationAtSlot(0, same.Id), Is.True);
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { same.Id }), Is.True,
            "A card already of the suit is a valid target, so Gear that counts Trick uses still fires.");
        Assert.That(same.Suit, Is.EqualTo(CardData.Suit.Spades));
        Assert.That(_cards.GetConsumableInstanceAtSlot(0).RemainingCharges, Is.EqualTo(2));
        var others = _cards.hand.Where(card => card.Suit != CardData.Suit.Spades).Take(2).ToArray();
        Assert.That(others.Length, Is.EqualTo(2));
        foreach (var card in others)
        {
            Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { card.Id }), Is.True);
            Assert.That(card.Suit, Is.EqualTo(CardData.Suit.Spades));
            Assert.That(card.DisplayName, Does.Contain("♠"));
            Assert.That(card.MatchesSuit(CardData.Suit.Spades), Is.True);
            Assert.That(_cards.BackpackSlotsUsed, Is.LessThanOrEqualTo(1));
        }
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
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
        var items = AssetDatabase.FindAssets("t:ConsumableData", new[] { "Assets/Data/Tricks" })
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
            .All(item => item.uses == 3), Is.True);
    }
}
