using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class GearContentBatch1Tests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    GearData _leather, _dagger, _tome, _sword, _club;
    CardEnhancementData _sharpened;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _leather = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/StreetJacket.asset");
        _dagger = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/Scalpel.asset");
        _tome = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/FieldTablet.asset");
        _sword = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/PowerBaton.asset");
        _club = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/ClubAmplifier.asset");
        _sharpened = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Mods/Boosted.asset");
        Assert.That(new UnityEngine.Object[] { _leather, _dagger, _tome, _sword, _club, _sharpened }, Has.All.Not.Null);

        var canvas = Make<Canvas>("Batch Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var hand = new GameObject("Batch Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _created.Add(hand);
        hand.transform.SetParent(canvas.transform, false);
        var field = hand.AddComponent<Field>();
        field.cardsHolder = (RectTransform)hand.transform;
        var drag = Make<Canvas>("Batch Drag Canvas");
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefab = new GameObject("Batch Card View", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        _created.Add(prefab);
        var view = prefab.AddComponent<CardView>();
        view.face = prefab.GetComponent<Image>();
        view.canvasGroup = prefab.GetComponent<CanvasGroup>();
        _cards = Make<CardManager>("Batch Cards");
        _cards.Configure(field, drag, view, new[] { _leather, _dagger, _tome, _sword, _club },
            new[] { _sharpened });
        _cards.ConfigureRandom(new DeterministicRandom(41));
        _combat = Make<CombatManager>("Batch Combat");
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
    public void Assets_AreCanonicalAndDefinitionsStayImmutableDuringRun()
    {
        var assets = new[] { _leather, _dagger, _tome, _sword };
        var ids = new[] { "rel_005", "rel_006", "rel_007", "rel_023" };
        var names = new[] { "Street Jacket", "Scalpel", "Field Tablet", "Power Baton" };
        var snapshots = assets.Select(JsonUtility.ToJson).ToArray();
        for (int i = 0; i < assets.Length; i++)
        {
            Assert.That(assets[i].canonicalId, Is.EqualTo(ids[i]));
            Assert.That(assets[i].id, Is.EqualTo(ids[i]));
            Assert.That(assets[i].displayName, Is.EqualTo(names[i]));
            Assert.That(assets[i].tier, Is.EqualTo(1));
            Assert.That(assets[i].rarity, Is.EqualTo(i == 3 ? "Rare" : "Common"));
            Assert.That(assets[i].capacityCategory, Is.EqualTo(GearCapacityCategory.Persistent));
            Assert.That(assets[i].effects, Is.Not.Empty);
            Assert.That(_cards.BuyGear(assets[i], 0), Is.True);
        }
        _cards.Reset();
        for (int i = 0; i < assets.Length; i++)
            Assert.That(JsonUtility.ToJson(assets[i]), Is.EqualTo(snapshots[i]));
    }

    [Test]
    public void Leather_ReducesEachCombatInstance_FloorsBeforeShield_AndDoesNotAffectEventCosts()
    {
        Assert.That(_cards.BuyGear(_leather, 0), Is.True);
        var enemy = Target(100, 5);
        var action = new CombatActionContext(1, CombatActionOrigin.EnemyRetaliation, sourceEnemy: enemy);
        var resolver = new CombatResolver();
        DamageResult Hit(int index, int amount, CombatDamageOrigin origin = CombatDamageOrigin.EnemyAggregate) =>
            resolver.Resolve(new DamageRequest(action, index, origin, enemy, _combat.player, amount), _combat);

        Assert.That(Hit(0, 2).ModifiedDamage, Is.Zero);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
        _combat.GrantPlayerShield(1);
        Assert.That(Hit(1, 3).ModifiedDamage, Is.Zero);
        Assert.That(_combat.player.ShieldCharges, Is.EqualTo(1), "Fully reduced damage does not consume Shield.");
        var shielded = Hit(2, 5);
        Assert.That(shielded.ModifiedDamage, Is.EqualTo(2));
        Assert.That(shielded.ShieldConsumed, Is.True);
        Assert.That(shielded.ActualHpLost, Is.Zero);
        var next = Hit(3, 5, CombatDamageOrigin.Reactive);
        Assert.That(next.ModifiedDamage, Is.EqualTo(2));
        Assert.That(next.ActualHpLost, Is.EqualTo(2));
        Assert.That(Hit(4, 5).ActualHpLost, Is.EqualTo(2));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(26));
        Assert.That(_combat.TakeRunDamage(5), Is.EqualTo(5));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(21));
        Assert.That(Hit(5, 5, CombatDamageOrigin.Legacy).ModifiedDamage, Is.EqualTo(5));
    }

    [Test]
    public void Dagger_AddsThreeBelowTen_CapsWholeAction_AndNeverChangesDefense()
    {
        Assert.That(_cards.BuyGear(_dagger, 0), Is.True);
        var low = FindCard(c => c.Rank == CardData.Rank.Two);
        var high = FindCard(c => c.Rank == CardData.Rank.Nine, low.data);
        Assert.That(_combat.CalculateCardAttackDamage(low.data), Is.EqualTo(2));
        Assert.That(_combat.CalculateCardDefense(low.data), Is.EqualTo(2));
        var enemy = Target(100, 0);
        _combat.StartEnemy(enemy);
        Assert.That(_combat.TryPlayCards(new[] { low }, enemy), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(95));
        Assert.That(_combat.TryPlayCards(new[] { high }, enemy), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(85), "Nine + three is capped at ten, once per action.");
    }

    [Test]
    public void Dagger_AcePairCapsAfterAggregation_ThenGuaranteedCritCanReachTwenty()
    {
        Assert.That(_cards.BuyGear(_dagger, 0), Is.True);
        var ace = FindCard(c => c.Rank == CardData.Rank.Ace);
        var eight = FindCard(c => c.Rank == CardData.Rank.Eight, ace.data);
        var enemy = Target(100, 0);
        _combat.StartEnemy(enemy);
        _combat.CriticalChancePercent = 100f;
        Assert.That(_combat.TryPlayCards(new[] { ace, eight }, enemy), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(80), "(1 + 8 + 3) capped at 10, then ×2 crit.");
    }

    [Test]
    public void Dagger_MultiHitUsesOneActionBudgetRatherThanResettingTheCapPerHit()
    {
        Assert.That(_cards.BuyGear(_dagger, 0), Is.True);
        var two = FindCard(c => c.Rank == CardData.Rank.Two);
        var enemy = Target(100, 0);
        _combat.StartEnemy(enemy);
        enemy.GainShield(1);
        var requested = new List<int>();
        _combat.OnDamageResolved += result =>
        {
            if (result.Request.Origin == CombatDamageOrigin.Card)
                requested.Add(result.Request.RequestedDamage);
        };
        Assert.That(_combat.TryPlayCards(new[] { two }, enemy, 3), Is.True);
        Assert.That(requested, Is.EqualTo(new[] { 5, 5 }));
        Assert.That(requested.Sum(), Is.EqualTo(10));
        Assert.That(enemy.ShieldCharges, Is.Zero);
        Assert.That(enemy.currentHp, Is.EqualTo(95), "Shield consumes the first budgeted hit.");
    }

    [Test]
    public void Tome_DrawsAfterBaseline_OnlyOncePerEncounter_AndClampsAtFullHand()
    {
        Assert.That(_cards.BuyGear(_tome, 0), Is.True);
        Assert.That(_cards.TryDiscard(Views()[0]), Is.True);
        Assert.That(_cards.TryDiscard(Views()[0]), Is.True);
        int beforeHand = _cards.HandCount, deck = _cards.deck.Count;
        _combat.StartEnemy(Target(100, 0));
        Assert.That(_cards.HandCount, Is.EqualTo(beforeHand + 2));
        Assert.That(_cards.deck.Count, Is.EqualTo(deck - 2));
        int after = _cards.deck.Count;
        _combat.StartEnemy(Target(100, 0)); // An active encounter cannot be re-entered.
        Assert.That(_cards.deck.Count, Is.EqualTo(after));
        _combat.Reset();
        _cards.RefillHand();
        after = _cards.deck.Count;
        _combat.StartEnemy(Target(100, 0));
        Assert.That(_cards.HandCount, Is.EqualTo(CardManager.HAND_SIZE));
        Assert.That(_cards.deck.Count, Is.EqualTo(after));
        _combat.Reset();
        Assert.That(_cards.TryDiscard(Views()[0]), Is.True);
        after = _cards.deck.Count;
        _combat.StartEnemy(Target(100, 0));
        Assert.That(_cards.HandCount, Is.EqualTo(CardManager.HAND_SIZE));
        Assert.That(_cards.deck.Count, Is.EqualTo(after - 1), "Baseline draw has first claim on the last slot.");
    }

    [Test]
    public void Tome_CardStreamConsumptionDoesNotChangeIndependentMapOrShopStreams()
    {
        var first = new RunRandomContext("TOME-STREAMS");
        var second = new RunRandomContext("TOME-STREAMS");
        _cards.ConfigureRandom(first.CreateStream("cards"));
        _cards.BuildDeck();
        _cards.DealHand();
        _cards.TryDiscard(Views()[0]);
        _cards.TryDiscard(Views()[0]);
        _cards.BuyGear(_tome, 0);
        _combat.StartEnemy(Target(100, 0));
        var mapA = first.CreateStream("map-layout", 2);
        var mapB = second.CreateStream("map-layout", 2);
        var shopA = first.CreateStream("shop-artifacts", 5);
        var shopB = second.CreateStream("shop-artifacts", 5);
        for (int i = 0; i < 12; i++)
        {
            Assert.That(mapA.NextInt(0, 1000), Is.EqualTo(mapB.NextInt(0, 1000)));
            Assert.That(shopA.NextInt(0, 1000), Is.EqualTo(shopB.NextInt(0, 1000)));
        }
    }

    [Test]
    public void Sword_UsesPreSwordPerCardThreshold_WithSharpenedAndClub()
    {
        Assert.That(_cards.BuyGear(_sword, 0), Is.True);
        var eight = Card(CardData.Suit.Hearts, CardData.Rank.Eight);
        var nine = Card(CardData.Suit.Hearts, CardData.Rank.Nine);
        var clubFour = Card(CardData.Suit.Clubs, CardData.Rank.Four);
        var clubFive = Card(CardData.Suit.Clubs, CardData.Rank.Five);
        Assert.That(_combat.CalculateCardAttackDamage(eight), Is.EqualTo(8));
        Assert.That(_combat.CalculateCardAttackDamage(nine), Is.EqualTo(19));
        Assert.That(_cards.ApplyEnhancement(_cards.ownedCards.First(c => c.Rank == CardData.Rank.Eight).Id,
            _sharpened), Is.True);
        var enhancedEight = _cards.ownedCards.First(c => c.Rank == CardData.Rank.Eight && c.Enhancement != null);
        Assert.That(_combat.CalculateCardAttackDamage(enhancedEight), Is.EqualTo(21));
        Assert.That(_cards.BuyGear(_club, 0), Is.True);
        Assert.That(_combat.CalculateCardAttackDamage(clubFour), Is.EqualTo(8));
        Assert.That(_combat.CalculateCardAttackDamage(clubFive), Is.EqualTo(20));
        Assert.That(_combat.CalculateCardDefense(clubFive), Is.EqualTo(5));
    }

    [Test]
    public void Sword_EvaluatesAcePairCardsIndependentlyAndCannotSelfQualify()
    {
        Assert.That(_cards.BuyGear(_sword, 0), Is.True);
        var ace = FindCard(c => c.Rank == CardData.Rank.Ace);
        var nine = FindCard(c => c.Rank == CardData.Rank.Nine, ace.data);
        var enemy = Target(100, 0);
        _combat.StartEnemy(enemy);
        _combat.CriticalChancePercent = 0f;
        Assert.That(_combat.CalculateCardAttackDamage(ace.data), Is.EqualTo(1));
        Assert.That(_combat.CalculateCardAttackDamage(nine.data), Is.EqualTo(19));
        Assert.That(_combat.TryPlayCards(new[] { ace, nine }, enemy), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(80));
    }

    [Test]
    public void CombinedGear_KeepActionAndPerCardOrderRegardlessOfAcquisitionOrder()
    {
        var ace = FindCard(c => c.Rank == CardData.Rank.Ace);
        var club = FindCard(c => c.Suit == CardData.Suit.Clubs && c.Rank == CardData.Rank.Five, ace.data);
        foreach (var gear in new[] { _sword, _dagger, _club })
            Assert.That(_cards.BuyGear(gear, 0), Is.True);
        var enemy = Target(100, 0);
        _combat.StartEnemy(enemy);
        _combat.CriticalChancePercent = 0f;
        Assert.That(_combat.CalculateCardAttackDamage(club.data), Is.EqualTo(20));
        Assert.That(_combat.TryPlayCards(new[] { ace, club }, enemy), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(90), "1 + (5×2 + 10), then +3, capped once at 10.");
        _combat.Reset();
        _cards.Reset();
        _cards.ConfigureRandom(new DeterministicRandom(41));
        _cards.BuildDeck();
        _cards.DealHand();
        foreach (var gear in new[] { _club, _dagger, _sword })
            Assert.That(_cards.BuyGear(gear, 0), Is.True);
        var replacementClub = Card(CardData.Suit.Clubs, CardData.Rank.Five);
        Assert.That(_combat.CalculateCardAttackDamage(replacementClub), Is.EqualTo(20));
        Assert.That(_cards.GearSlotsUsed, Is.EqualTo(3));
    }

    [Test]
    public void CachedShopPurchaseAndRestartRetainCapacityAndResetEffectState()
    {
        var run = Make<RunManager>("Batch Run");
        run.thiefType = Type("Pickpocket", 10, 1);
        run.goblinType = Type("Scrapper", 10, 2);
        run.knightType = Type("SecurityGuard", 10, 3);
        run.StartRunWithSeed("BATCH-FOUR");
        var shop = run.currentPath.First(n => n.kind == MapNodeType.Shop);
        shop.accessible = true;
        _cards.AddCredits(100);
        run.OnPathChosen(shop.id);
        var offer = run.GetCurrentShopOffers().First(o => o.kind == ShopOfferKind.Gear);
        int price = offer.price, credits = _cards.credits;
        Assert.That(run.PurchaseShopOffer(offer.StableId), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(credits - price));
        Assert.That(run.PurchaseShopOffer(offer.StableId), Is.False);
        _cards.GetGearInstance(offer.gear).State.SetCounter(0, 12);
        run.RestartRun();
        Assert.That(_cards.GearSlotsUsed, Is.Zero);
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(40));
        Assert.That(_cards.BuyGear(offer.gear, 0), Is.True);
        Assert.That(_cards.GetGearInstance(offer.gear).State.GetCounter(0), Is.Zero);
    }

    CardInstance Card(CardData.Suit suit, CardData.Rank rank)
    {
        var definition = CardData.Create(suit, rank);
        _created.Add(definition);
        return new CardInstance(definition, 1000 + _created.Count);
    }

    EnemyTypeData Type(string name, int hp, int attack)
    {
        var type = EnemyTypeData.Create(name, hp, attack, 0);
        _created.Add(type);
        return type;
    }

    EnemyRuntime Target(int hp, int attack) => new(Type("Batch Target", hp, attack));

    CardView[] Views() => _cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(v => v.data != null && _cards.hand.Contains(v.data)).ToArray();

    CardView FindCard(Func<CardInstance, bool> predicate, params CardInstance[] preserve)
    {
        for (int i = 0; i < 40; i++)
        {
            var found = Views().FirstOrDefault(v => predicate(v.data) && !preserve.Contains(v.data));
            if (found != null) return found;
            var discard = Views().FirstOrDefault(v => !preserve.Contains(v.data));
            if (discard == null) break;
            _cards.TryDiscard(discard);
            _cards.DrawToHand(1);
        }
        Assert.Fail("Required card was not found in the 40-card collection.");
        return null;
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go.AddComponent<T>();
    }
}
