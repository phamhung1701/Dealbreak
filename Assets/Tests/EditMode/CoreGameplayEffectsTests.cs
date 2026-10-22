using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class CoreGameplayEffectsTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    GearData _club, _heart, _diamond, _spade;
    CardEnhancementData _sharpened, _hardened, _mending, _quickdraw;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _club = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/ClubAmplifier.asset");
        _heart = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/HeartMeds.asset");
        _diamond = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/DiamondDriver.asset");
        _spade = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/SpadeShield.asset");
        _sharpened = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Mods/Boosted.asset");
        _hardened = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Mods/Plated.asset");
        _mending = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Mods/Regenerative.asset");
        _quickdraw = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Mods/Quickload.asset");
        Assert.That(new UnityEngine.Object[] { _club, _heart, _diamond, _spade, _sharpened, _hardened, _mending, _quickdraw },
            Has.All.Not.Null);

        var canvas = Make<Canvas>("Effect Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var hand = new GameObject("Effect Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _created.Add(hand);
        hand.transform.SetParent(canvas.transform, false);
        var field = hand.AddComponent<Field>();
        field.cardsHolder = (RectTransform)hand.transform;
        var drag = Make<Canvas>("Effect Drag Canvas");
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefab = new GameObject("Effect View", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        _created.Add(prefab);
        var view = prefab.AddComponent<CardView>();
        view.face = prefab.GetComponent<Image>();
        view.canvasGroup = prefab.GetComponent<CanvasGroup>();
        _cards = Make<CardManager>("Effect Cards");
        _cards.Configure(field, drag, view,
            new[] { _club, _heart, _diamond, _spade },
            new[] { _sharpened, _hardened, _mending, _quickdraw });
        _cards.ConfigureRandom(new DeterministicRandom(41));
        _combat = Make<CombatManager>("Effect Combat");
        _combat.ConfigurePlayer(30);
        _cards.BuildDeck();
        _cards.DealHand();
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void CoreAssets_HaveCanonicalMetadataAndOnlyOneTypedEffectPath()
    {
        var gearItems = new[] { _club, _heart, _diamond, _spade };
        var enhancements = new[] { _sharpened, _hardened, _mending, _quickdraw };
        for (int i = 0; i < 4; i++)
        {
            Assert.That(gearItems[i].canonicalId, Is.EqualTo($"rel_{i + 1:000}"));
            Assert.That(gearItems[i].effects, Has.Length.EqualTo(1));
            Assert.That(gearItems[i].tier, Is.EqualTo(1));
            Assert.That(gearItems[i].rarity, Is.EqualTo("Common"));
            Assert.That(gearItems[i].capacityCategory, Is.EqualTo(GearCapacityCategory.Persistent));
            Assert.That(enhancements[i].canonicalId, Is.EqualTo($"enh_{i + 1:000}"));
            Assert.That(enhancements[i].effects, Has.Length.EqualTo(1));
            Assert.That(enhancements[i].tier, Is.EqualTo(1));
            Assert.That(enhancements[i].rarity, Is.EqualTo("Common"));
        }
        Assert.That(_hardened.displayName, Is.EqualTo("Plated"));
        Assert.That(_hardened.id, Is.EqualTo("reinforced"), "Keep stable legacy Shop IDs and GUIDs.");
    }

    [Test]
    public void ClubAndSharpened_AffectOnlyMatchingCardBeforeAcePairSum()
    {
        Assert.That(_cards.BuyGear(_club, 0), Is.True);
        var club = FindCard(c => c.Suit == CardData.Suit.Clubs && c.Rank != CardData.Rank.Ace);
        var ace = FindCard(c => c.Rank == CardData.Rank.Ace && c.Suit != CardData.Suit.Clubs, club.data);
        var unrelated = FindCard(c => c.Suit == CardData.Suit.Hearts && c.Rank != CardData.Rank.Ace, club.data, ace.data);
        Assert.That(_cards.ApplyEnhancement(club.data.Id, _sharpened), Is.True);
        Assert.That(club.data.AttackValue, Is.EqualTo(club.data.BaseAttackValue + 3));
        Assert.That(unrelated.data.AttackValue, Is.EqualTo(unrelated.data.BaseAttackValue));
        Assert.That(_combat.CalculateCardAttackDamage(club.data), Is.EqualTo((club.data.BaseAttackValue + 3) * 2));
        Assert.That(_combat.CalculateCardAttackDamage(ace.data), Is.EqualTo(ace.data.BaseAttackValue));
        var target = Target(100, 0);
        _combat.StartEnemy(target);
        _combat.CriticalChancePercent = 0;
        int expected = _combat.CalculateCardAttackDamage(ace.data) + _combat.CalculateCardAttackDamage(club.data);
        Assert.That(_combat.TryPlayCards(new[] { ace, club }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(100 - expected));
        Assert.That(_cards.deck.Count(c => c.Id == ace.data.Id), Is.EqualTo(1));
        Assert.That(_cards.deck.Count(c => c.Id == club.data.Id), Is.EqualTo(1));
    }

    [Test]
    public void Heart_HealsOncePerCommittedCardAndClampsAtMax()
    {
        Assert.That(_cards.BuyGear(_heart, 0), Is.True);
        var ace = FindCard(c => c.Suit == CardData.Suit.Hearts && c.Rank == CardData.Rank.Ace);
        var other = FindCard(c => c.Suit == CardData.Suit.Hearts && c.Rank != CardData.Rank.Ace, ace.data);
        var target = Target(100, 0);
        _combat.TakeRunDamage(5);
        _combat.StartEnemy(target);
        int before = _combat.player.currentHealth;
        int deckBefore = _cards.deck.Count;
        _combat.CriticalChancePercent = 0;
        Assert.That(_combat.TryPlayCards(new[] { ace, other }, target), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(before + 2));
        Assert.That(_cards.deck.Count, Is.EqualTo(deckBefore + 2), "Both played Hearts return to the draw pile.");
        var nonHeart = FindCard(c => c.Suit != CardData.Suit.Hearts);
        int health = _combat.player.currentHealth;
        Assert.That(_combat.TryPlayCards(new[] { nonHeart }, target), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(health));
        _combat.HealPlayer(100);
        var nextHeart = FindCard(c => c.Suit == CardData.Suit.Hearts);
        Assert.That(_combat.TryPlayCards(new[] { nextHeart }, target), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
    }

    [Test]
    public void Diamond_DrawsFixedOneRegardlessOfRankAndRespectsHandLimit()
    {
        Assert.That(_cards.BuyGear(_diamond, 0), Is.True);
        var highDiamond = FindCard(c => c.Suit == CardData.Suit.Diamonds && c.Rank >= CardData.Rank.Eight);
        var target = Target(100, 0);
        _combat.StartEnemy(target);
        while (_cards.HandCount > 4)
            Assert.That(_cards.TryDiscard(Views().First(v => v != highDiamond)), Is.True);
        int before = _cards.HandCount;
        Assert.That(_combat.TryPlayCards(new[] { highDiamond }, target), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(before - 1 + 1));
        var nextDiamond = FindCard(c => c.Suit == CardData.Suit.Diamonds);
        _cards.RefillHand();
        Assert.That(_cards.HandCount, Is.EqualTo(CardManager.HAND_SIZE));
        Assert.That(_combat.TryPlayCards(new[] { nextDiamond }, target), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(CardManager.HAND_SIZE), "Only one free slot after commitment.");
    }

    [Test]
    public void SpadeAndHardened_BlockUsesCardFlatBeforeSuitMultiplier_AndDoesNotReduceAttack()
    {
        Assert.That(_cards.BuyGear(_spade, 0), Is.True);
        var spade = FindCard(c => c.Suit == CardData.Suit.Spades && c.Rank >= CardData.Rank.Two);
        var other = FindCard(c => c.Suit != CardData.Suit.Spades, spade.data);
        Assert.That(_cards.ApplyEnhancement(spade.data.Id, _hardened), Is.True);
        Assert.That(_combat.CalculateCardDefense(spade.data), Is.EqualTo((spade.data.BaseAttackValue + 3) * 2));
        Assert.That(_combat.CalculateCardDefense(other.data), Is.EqualTo(other.data.BaseAttackValue));
        var type = EnemyTypeData.Create("Shielded Defender", 100, 2, 0);
        _created.Add(type);
        var enemies = new[] { new EnemyRuntime(type, 1, 2), new EnemyRuntime(type, 2, 2) };
        _combat.StartEncounter(enemies);
        var attack = Views().First(v => v != spade && v != other);
        Assert.That(_combat.TryPlayCards(new[] { attack }, enemies[0]), Is.True);
        Assert.That(enemies.All(e => e.currentAttack == 2), Is.True, "Old Spade ATK reduction must be gone.");
        Assert.That(_combat.TryDefendWithCards(new[] { spade, other }), Is.True);
        Assert.That(_combat.PendingAttackCount, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void Mending_TriggersAtPlayerTurnStartOnlyForEachHandInstance_NotOnPlay()
    {
        var first = Views()[0];
        var second = Views()[1];
        var inDeck = _cards.deck[0];
        Assert.That(_cards.ApplyEnhancement(first.data.Id, _mending), Is.True);
        Assert.That(_cards.ApplyEnhancement(second.data.Id, _mending), Is.True);
        Assert.That(_cards.ApplyEnhancement(inDeck.Id, _mending), Is.True);
        _combat.TakeRunDamage(10);
        var target = Target(100, 2);
        _combat.StartEnemy(target);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(24), "Exactly two hand instances heal at turn start.");
        Assert.That(_combat.TryPlayCards(new[] { first }, target), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(24), "Playing Mending is not a heal trigger.");
        _combat.TakeRemainingDamage();
        Assert.That(_combat.player.currentHealth, Is.EqualTo(24), "One remaining hand instance heals 2 after taking 2 damage.");
        Assert.That(_cards.deck.Contains(first.data), Is.True);
        _combat.HealPlayer(100);
        Assert.That(_combat.TryPlayCards(new[] { Views().First(v => v != second) }, target), Is.True);
        _combat.TakeRemainingDamage();
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30), "Turn-start healing clamps at max HP.");
    }

    [Test]
    public void Quickdraw_DrawsExactlyOnceFromSourceCardAndRespectsCapacity()
    {
        var quick = Views()[0];
        Assert.That(_cards.ApplyEnhancement(quick.data.Id, _quickdraw), Is.True);
        var target = Target(100, 0);
        _combat.StartEnemy(target);
        int initialHand = _cards.HandCount;
        int initialDeck = _cards.deck.Count;
        Assert.That(_combat.TryPlayCards(new[] { quick }, target, 3), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(initialHand));
        Assert.That(_cards.deck.Count, Is.EqualTo(initialDeck), "The played card returns as one card is drawn.");
        Assert.That(_cards.hand.Count(c => c.Id == quick.data.Id) + _cards.deck.Count(c => c.Id == quick.data.Id), Is.EqualTo(1));
    }

    [Test]
    public void SharedSources_UseAuthoredOrderWithoutNamesOrHierarchy_AndRuntimeStateResets()
    {
        var first = NewGear("first", "First", new GameplayEffectDefinition { kind = GameplayEffectKind.Heal,
            trigger = GameplayEffectTrigger.CardCommitted, amount = 1 });
        var second = NewGear("second", "Second", new GameplayEffectDefinition { kind = GameplayEffectKind.Heal,
            trigger = GameplayEffectTrigger.CardCommitted, amount = 2 });
        var enhancement = NewEnhancement("third", "Third", new GameplayEffectDefinition { kind = GameplayEffectKind.Heal,
            trigger = GameplayEffectTrigger.CardCommitted, amount = 3 });
        _cards.BuyGear(first, 0);
        _cards.BuyGear(second, 0);
        var view = Views()[0];
        int cardId = view.data.Id;
        Assert.That(_cards.ApplyEnhancement(cardId, enhancement), Is.True);
        view.transform.SetAsLastSibling(); // Presentation order is not an effect-source ordering authority.
        var instance = _cards.GetGearInstance(first);
        instance.State.SetCounter(0, 5);
        view.data.EffectState.SetCounter(0, 4);
        string gearJson = JsonUtility.ToJson(first);
        string enhancementJson = JsonUtility.ToJson(enhancement);
        var logs = new List<string>();
        _combat.OnCombatLog += logs.Add;
        _combat.TakeRunDamage(10);
        var target = Target(100, 0);
        _combat.StartEnemy(target);
        // Display labels are presentation only: change both without altering effect definitions.
        first.displayName = "Unrelated label";
        enhancement.displayName = "Also unrelated";
        Assert.That(_combat.TryPlayCards(new[] { view }, target), Is.True);
        CollectionAssert.AreEqual(new[] { "Unrelated label: Healed 1 HP.",
            "Second: Healed 2 HP.", "Also unrelated: Healed 3 HP." },
            logs.Where(text => text.Contains("Healed")).ToArray());
        Assert.That(_combat.player.currentHealth, Is.EqualTo(26));
        first.displayName = "First";
        enhancement.displayName = "Third";
        Assert.That(JsonUtility.ToJson(first), Is.EqualTo(gearJson));
        Assert.That(JsonUtility.ToJson(enhancement), Is.EqualTo(enhancementJson));
        _cards.Reset();
        Assert.That(_cards.ownedGear, Is.Empty);
        Assert.That(_cards.FindOwnedCard(cardId), Is.Null);
        _cards.BuyGear(first, 0);
        Assert.That(_cards.GetGearInstance(first).State.GetCounter(0), Is.Zero);
        Assert.That(instance.State.GetCounter(0), Is.EqualTo(5), "Old run state is detached, not saved on the definition.");
        _cards.BuildDeck();
        Assert.That(_cards.ownedCards[0].EffectState.GetCounter(0), Is.Zero);
    }

    GearData NewGear(string id, string name, params GameplayEffectDefinition[] effects)
    {
        var value = ScriptableObject.CreateInstance<GearData>();
        value.id = id;
        value.displayName = name;
        value.effects = effects;
        _created.Add(value);
        return value;
    }

    CardEnhancementData NewEnhancement(string id, string name, params GameplayEffectDefinition[] effects)
    {
        var value = ScriptableObject.CreateInstance<CardEnhancementData>();
        value.id = id;
        value.displayName = name;
        value.effects = effects;
        _created.Add(value);
        return value;
    }

    EnemyRuntime Target(int hp, int attack)
    {
        var type = EnemyTypeData.Create("Effect Target", hp, attack, 0);
        _created.Add(type);
        return new EnemyRuntime(type);
    }

    CardView[] Views() => _cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(v => v.data != null && _cards.hand.Contains(v.data)).ToArray();

    CardView FindCard(Func<CardInstance, bool> predicate, params CardInstance[] keep)
    {
        for (int i = 0; i < 40; i++)
        {
            var match = Views().FirstOrDefault(v => predicate(v.data) && !keep.Contains(v.data));
            if (match != null) return match;
            var candidate = Views().FirstOrDefault(v => !keep.Contains(v.data));
            if (candidate == null) break;
            Assert.That(_cards.TryDiscard(candidate), Is.True);
            _cards.DrawToHand(1);
        }
        Assert.Fail("Unable to find required card while preserving chosen hand cards.");
        return null;
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go.AddComponent<T>();
    }
}
