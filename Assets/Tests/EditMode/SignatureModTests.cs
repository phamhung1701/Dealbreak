using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Overclock (rel_141, Legendary): every modded card plays its Mod's Signature version.
public sealed class SignatureModTests
{
    readonly List<UnityEngine.Object> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    GearData _overclock;
    Field _field;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        var canvas = Make<Canvas>("Signature Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = new GameObject("Signature Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _objects.Add(holder);
        holder.transform.SetParent(canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var drag = Make<Canvas>("Signature Drag Canvas");
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = new GameObject("Signature Card Prefab", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        _objects.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = Make<CardManager>("Signature Cards");
        _overclock = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/Overclock.asset");
        _cards.Configure(_field, drag, prefab, new[] { _overclock }, Array.Empty<CardEnhancementData>());
        _cards.ConfigureRandom(new DeterministicRandom(41));
        _combat = Make<CombatManager>("Signature Combat");
        _combat.ConfigurePlayer(30);
        _combat.ConfigureCriticalRandom(new FixedRoll(.99f));
        _run = Make<RunManager>("Signature Run");
        _run.thiefType = Enemy("Pickpocket", 50, 3);
        _run.goblinType = Enemy("Scrapper", 50, 3);
        _run.knightType = Enemy("SecurityGuard", 50, 3);
        _run.StartRunWithSeed("SIGNATURE-SEED");
        _combat.CriticalChancePercent = 0;
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        foreach (var go in _objects.AsEnumerable().Reverse())
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        foreach (var asset in _assets)
            if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
    }

    [Test]
    public void Overclock_IsARegisteredLegendary_AndEveryModHasASignature()
    {
        Assert.That((_overclock.canonicalId, _overclock.displayName, _overclock.rarity, _overclock.specialRule),
            Is.EqualTo(("rel_141", "Overclock", "Legendary", GearSpecialRule.SignatureMods)));
        Assert.That(SaveContentCatalog.Load().gearItems, Does.Contain(_overclock));
        Assert.That(_overclock.iconSprite, Is.Not.Null);
        foreach (var mod in SaveContentCatalog.Load().enhancements)
        {
            Assert.That(mod.HasSignature, Is.True, mod.displayName);
            Assert.That(mod.signatureName, Is.Not.EqualTo(mod.displayName), mod.displayName);
            Assert.That(mod.signatureDescription, Is.Not.Empty, mod.displayName);
        }
    }

    [Test]
    public void OwningOverclock_SwitchesAModdedCardToItsSignatureNameAndEffects()
    {
        var card = FindView(_ => true).data;
        Assert.That(_cards.ApplyEnhancement(card.Id, Mod("Boosted")), Is.True);
        int normal = GameplayEffectResolver.CalculateAttack(card, _cards);
        Assert.That(ModSignature.Name(card.Enhancement), Is.EqualTo("Boosted"));

        Buy(_overclock);
        Assert.That(ModSignature.Name(card.Enhancement), Is.EqualTo("Afterburner"));
        Assert.That(card.DisplayName, Does.Contain("[Afterburner]"));
        Assert.That(ModSignature.Description(card.Enhancement), Is.EqualTo(Mod("Boosted").signatureDescription));
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(normal + 2), "+3 becomes +5.");
        Assert.That(card.Enhancement, Is.SameAs(Mod("Boosted")), "The card keeps its Mod; nothing is saved.");
    }

    [Test]
    public void ChainStrike_HitsOnceMoreForEachOtherChainStrikeCardHeld()
    {
        Buy(_overclock);
        var enemy = StartTarget(500, 1);
        var views = EnsureInHand(_ => true, 3);
        foreach (var view in views) Assert.That(_cards.ApplyEnhancement(view.data.Id, Mod("DoubleStrike")), Is.True);
        var played = new[] { views[0].data };
        int perHit = _combat.PreviewActionDamage(played, enemy);
        Assert.That(_combat.PreviewHitCount(played), Is.EqualTo(4));
        Assert.That(_combat.PreviewActionTotalDamage(played, enemy), Is.EqualTo(perHit * 4));
        int hits = 0;
        _combat.OnDamageResolved += result => { if (ReferenceEquals(result.Request.Target, enemy)) hits++; };

        Assert.That(_combat.TryPlayCards(played, enemy), Is.True);
        Assert.That(hits, Is.EqualTo(4));
        Assert.That(enemy.currentHp, Is.EqualTo(500 - perHit * 4));
    }

    [Test]
    public void Afterburner_CarriesAKillsExcessDamageToTheNextEnemy()
    {
        Buy(_overclock);
        var weak = new EnemyRuntime(Type("Weak", 1, 1));
        var next = new EnemyRuntime(Type("Next", 100, 1));
        _combat.StartEncounter(new[] { weak, next });
        var view = FindView(_ => true);
        Assert.That(_cards.ApplyEnhancement(view.data.Id, Mod("Boosted")), Is.True);
        int damage = _combat.PreviewActionDamage(new[] { view.data }, weak);

        Assert.That(_combat.TryPlayCards(new[] { view.data }, weak), Is.True);
        Assert.That(weak.IsDefeated, Is.True);
        Assert.That(next.currentHp, Is.EqualTo(100 - (damage - 1)));
    }

    [Test]
    public void CoverFire_EachAttackShootsTheWeakestOtherEnemy()
    {
        Buy(_overclock);
        var target = new EnemyRuntime(Type("Target", 100, 1));
        var weakest = new EnemyRuntime(Type("Weakest", 50, 1));
        var other = new EnemyRuntime(Type("Other", 80, 1));
        _combat.StartEncounter(new[] { target, weakest, other });
        var held = FindView(_ => true);
        Assert.That(_cards.ApplyEnhancement(held.data.Id, Mod("Backup")), Is.True);
        var attacker = FindView(_ => true, held.data);

        Assert.That(_combat.TryPlayCards(new[] { attacker.data }, target), Is.True);
        Assert.That(weakest.currentHp, Is.EqualTo(47));
        Assert.That(other.currentHp, Is.EqualTo(80));
        Assert.That(_cards.hand, Does.Contain(held.data), "Cover Fire fires from the hand.");
    }

    [Test]
    public void Executioner_AlwaysCritsBelowHalfHealth_ForTripleDamage()
    {
        Buy(_overclock);
        var enemy = StartTarget(200, 1);
        var view = FindView(_ => true);
        Assert.That(_cards.ApplyEnhancement(view.data.Id, Mod("Deadeye")), Is.True);
        int damage = _combat.PreviewActionDamage(new[] { view.data }, enemy);

        // At full health the 25% roll misses (the test roll is 0.99).
        Assert.That(_combat.TryPlayCards(new[] { view.data }, enemy), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(200 - damage));
        _combat.TakeRemainingDamage();

        enemy.currentHp = 99;
        var again = FindView(_ => true);
        Assert.That(_cards.ApplyEnhancement(again.data.Id, Mod("Deadeye"), replaceExisting: true), Is.True);
        int second = _combat.PreviewActionDamage(new[] { again.data }, enemy);
        Assert.That(_combat.TryPlayCards(new[] { again.data }, enemy), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(Math.Max(0, 99 - second * 3)));
    }

    [Test]
    public void Redline_AddsMissingHealthAsDamage_AndCostsNoHealth()
    {
        Buy(_overclock);
        _combat.TakeRunDamage(10);
        var enemy = StartTarget(500, 1);
        var view = FindView(_ => true);
        Assert.That(_cards.ApplyEnhancement(view.data.Id, Mod("Overclocked")), Is.True);
        int cardDamage = _combat.CalculateCardAttackDamage(view.data);
        Assert.That(_combat.PreviewActionDamage(new[] { view.data }, enemy), Is.EqualTo(cardDamage + 10));
        int health = _combat.player.currentHealth;

        Assert.That(_combat.TryPlayCards(new[] { view.data }, enemy), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(500 - cardDamage - 10));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(health), "Redline has no HP cost.");
    }

    [Test]
    public void Bulwark_ReturnsToHandAfterBlocking_OncePerEnemyTurn()
    {
        Buy(_overclock);
        var enemy = StartTarget(500, 80);
        var bulwark = FindView(_ => true);
        var card = bulwark.data;
        Assert.That(_cards.ApplyEnhancement(card.Id, Mod("Plated")), Is.True);
        Assert.That(_combat.CalculateCardDefense(card), Is.EqualTo(card.BaseAttackValue + 5));
        var attacker = FindView(_ => true, card);
        Assert.That(_combat.TryPlayCards(new[] { attacker.data }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));

        int shuffles = _cards.deck.Count;
        Assert.That(_combat.TryDefendWithCards(new[] { card }, enemy), Is.True);
        Assert.That(_cards.hand, Does.Contain(card), "Bulwark stays in hand after blocking.");
        Assert.That(_cards.deck.Count, Is.EqualTo(shuffles), "It never went into the draw pile.");
        Assert.That(Views().Any(view => view.data == card), Is.True, "Its hand view stays.");
        Assert.That(_combat.pendingDamage, Is.GreaterThan(0));

        Assert.That(_combat.TryDefendWithCards(new[] { card }, enemy), Is.True);
        Assert.That(_cards.deck, Does.Contain(card), "A second block in the same enemy turn returns it to the draw pile.");
    }

    [Test]
    public void FeedingFrenzy_ReturnsToHandOnAKill_AndStillGrows()
    {
        Buy(_overclock);
        var weak = new EnemyRuntime(Type("Prey", 1, 1));
        var other = new EnemyRuntime(Type("Survivor", 100, 1));
        _combat.StartEncounter(new[] { weak, other });
        var view = FindView(_ => true);
        var card = view.data;
        Assert.That(_cards.ApplyEnhancement(card.Id, Mod("Predatory")), Is.True);

        Assert.That(_combat.TryPlayCards(new[] { card }, weak), Is.True);
        Assert.That(_cards.hand, Does.Contain(card));
        Assert.That(card.PermanentAttackBonus, Is.EqualTo(1));
    }

    [Test]
    public void Interest_PaysOneCreditPerFiveHeld_WhenTheFightIsWon()
    {
        Buy(_overclock);
        _cards.AddCredits(50);
        var enemy = StartTarget(1, 1);
        var held = FindView(_ => true);
        Assert.That(_cards.ApplyEnhancement(held.data.Id, Mod("Profitable")), Is.True);
        var attacker = FindView(_ => true, held.data);
        int credits = _cards.credits;

        Assert.That(_combat.TryPlayCards(new[] { attacker.data }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameWon));
        Assert.That(_cards.credits, Is.EqualTo(credits + credits / 5));
    }

    [Test]
    public void Tutor_DrawsMoreTheFewerCardsAreHeld()
    {
        Buy(_overclock);
        var enemy = StartTarget(500, 1);
        var tutor = FindView(_ => true);
        Assert.That(_cards.ApplyEnhancement(tutor.data.Id, Mod("Quickload")), Is.True);
        var others = Views().Where(view => view != tutor).ToArray();
        Assert.That(_cards.TryDiscardCards(others.Take(others.Length - 3).ToArray()), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(4));

        // Three cards held after playing it: one card, plus one at 3 or fewer.
        Assert.That(_combat.TryPlayCards(new[] { tutor.data }, enemy), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(5));
    }

    [Test]
    public void Transfusion_TurnsHealingPastFullIntoOneShield_OncePerFightAcrossAllCards()
    {
        Buy(_overclock);
        _cards.DrawToHand(CardManager.HAND_SIZE);
        var held = Views()[0];
        var second = Views()[1];
        Assert.That(_cards.ApplyEnhancement(held.data.Id, Mod("Regenerative")), Is.True);
        Assert.That(_cards.ApplyEnhancement(second.data.Id, Mod("Regenerative")), Is.True);
        var enemy = StartTarget(500, 1);
        Assert.That(_combat.player.shieldCharges, Is.EqualTo(1),
            "Full HP at turn start: the heal becomes Shield, once however many Transfusion cards are held.");

        var attacker = FindView(_ => true, held.data, second.data);
        Assert.That(_combat.TryPlayCards(new[] { attacker.data }, enemy), Is.True);
        _combat.TakeRemainingDamage();
        Assert.That(_combat.player.shieldCharges, Is.Zero, "The Shield took the hit; no second Shield this fight.");
        Assert.That(_combat.player.currentHealth, Is.EqualTo(_combat.player.maxHealth));
    }

    [Test]
    public void ChainReaction_AnEnemyTheBlastKillsExplodesToo()
    {
        Buy(_overclock);
        var primary = new EnemyRuntime(Type("Primary", 500, 1));
        var fuse = new EnemyRuntime(Type("Fuse", 1, 1));
        var tough = new EnemyRuntime(Type("Tough", 500, 1));
        _combat.StartEncounter(new[] { primary, fuse, tough });
        var card = FindView(_ => true).data;
        Assert.That(_cards.ApplyEnhancement(card.Id, Mod("Volatile")), Is.True);
        int d = _combat.PreviewActionDamage(new[] { card }, primary);
        Assert.That(d, Is.GreaterThan(1));
        tough.currentHp = d + 1;

        Assert.That(_combat.TryPlayCards(new[] { card }, primary), Is.True);
        // The hit, the card's blast (kills Fuse, leaves Tough on 1), Fuse's blast (kills Tough), Tough's blast.
        Assert.That((fuse.IsDefeated, tough.IsDefeated), Is.EqualTo((true, true)));
        Assert.That(primary.currentHp, Is.EqualTo(500 - d * 3));
    }

    [Test]
    public void Joker_CountsAsAnyRankForAcePairsHandsSetsAndTheRoyalFlush()
    {
        var hands = AssetDatabase.FindAssets("t:GearData", new[] { "Assets/Data/Gear" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GearData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Single(gear => gear.canonicalId == "rel_012");
        _cards.Configure(_field, _cards.dragCanvas, _cards.cardPrefab, new[] { _overclock, hands }, Array.Empty<CardEnhancementData>());
        Buy(hands);
        var joker = Make(CardData.Suit.Hearts, CardData.Rank.Two);
        Assert.That(joker.TryApplyEnhancement(Mod("Wildcard")), Is.True);
        var seven = Make(CardData.Suit.Clubs, CardData.Rank.Seven);
        var otherSeven = Make(CardData.Suit.Spades, CardData.Rank.Seven);
        var royal = new[]
        {
            Make(CardData.Suit.Spades, CardData.Rank.Ten), Make(CardData.Suit.Spades, CardData.Rank.Jack),
            Make(CardData.Suit.Spades, CardData.Rank.Queen), Make(CardData.Suit.Spades, CardData.Rank.King), joker
        };
        Assert.That(GameplayEffectResolver.CanPlayAsAcePair(new[] { joker, seven }), Is.False, "Wildcard alone keeps its rank.");
        Assert.That(AttackSelectionRules.IsRoyalFamily(royal), Is.False);

        Buy(_overclock);
        Assert.That(ModSignature.Name(joker.Enhancement), Is.EqualTo("Joker"));
        Assert.That(joker.MatchesSuit(CardData.Suit.Spades), Is.True, "Joker keeps Wildcard's any suit.");
        Assert.That(GameplayEffectResolver.CanPlayAsAcePair(new[] { joker, seven }), Is.True, "Joker stands in for the Ace.");
        Assert.That(GameplayEffectResolver.IsHandsMultiCardAction(new[] { seven, joker, otherSeven }, _cards), Is.True);
        Assert.That(GameplayEffectResolver.IsHandsMultiCardAction(new[] { seven, joker, Make(CardData.Suit.Hearts, CardData.Rank.Eight) }, _cards),
            Is.False, "The other cards still share one rank.");
        Assert.That(AttackSelectionRules.IsRoyalFamily(royal), Is.True, "Joker fills the missing Ace of a one-suit Royal Flush.");
        Assert.That(AttackSelectionRules.IsRoyalFamily(royal.Take(4).Append(Make(CardData.Suit.Hearts, CardData.Rank.Ace)).ToArray()),
            Is.False, "A plain off-suit Ace still breaks the flush.");
        var kingsOnly = new GameplayEffectCondition { kind = GameplayConditionKind.CardRank, rank = CardData.Rank.King };
        Assert.That(kingsOnly.Matches(joker), Is.True, "Gear rank conditions count the Joker too.");
        Assert.That(kingsOnly.Matches(seven), Is.False);
        Assert.That((joker.IsFaceCard, joker.RanksBelow(CardData.Rank.Ace), joker.RanksAbove(CardData.Rank.King)),
            Is.EqualTo((true, true, true)));
    }

    [Test]
    public void ModTricks_NameTheSignatureTheyGive_WhileOverclockIsOwned()
    {
        var booster = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Tricks/BoosterShot.asset");
        var bandage = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Tricks/Bandage.asset");
        Assert.That(ModSignature.TrickDescription(booster), Is.EqualTo(booster.description));
        Buy(_overclock);
        Assert.That(ModSignature.TrickDescription(booster), Does.StartWith("Give a hand card Afterburner:"));
        Assert.That(ModSignature.TrickDescription(bandage), Does.StartWith("Give a hand card Transfusion:"));
        foreach (var mod in SaveContentCatalog.Load().enhancements)
            Assert.That(mod.signatureTrickText, Does.StartWith(mod.signatureName + ":"), mod.displayName);
    }

    [Test]
    public void WithoutOverclock_ModsPlayNormally()
    {
        var enemy = StartTarget(500, 1);
        var views = EnsureInHand(_ => true, 3);
        foreach (var view in views) Assert.That(_cards.ApplyEnhancement(view.data.Id, Mod("DoubleStrike")), Is.True);
        Assert.That(_combat.PreviewHitCount(new[] { views[0].data }), Is.EqualTo(2));
        Assert.That(ModSignature.Name(views[0].data.Enhancement), Is.EqualTo("Double Strike"));
    }

    static CardEnhancementData Mod(string asset) =>
        AssetDatabase.LoadAssetAtPath<CardEnhancementData>($"Assets/Data/Mods/{asset}.asset");

    void Buy(GearData gear) => Assert.That(_cards.BuyGear(gear, 0), Is.True);

    CardInstance Make(CardData.Suit suit, CardData.Rank rank)
    {
        var definition = CardData.Create(suit, rank);
        _assets.Add(definition);
        return new CardInstance(definition, 9000 + (int)rank + (int)suit * 20);
    }

    EnemyRuntime StartTarget(int hp, int attack)
    {
        _combat.StartEnemy(new EnemyRuntime(Type("Signature Target", hp, attack)));
        return _combat.currentEnemy;
    }

    EnemyTypeData Enemy(string name, int hp, int attack)
    {
        var type = EnemyTypeData.Create(name, hp, attack, 0);
        type.name = name;
        _assets.Add(type);
        return type;
    }

    EnemyTypeData Type(string name, int hp, int attack)
    {
        var type = EnemyTypeData.Create(name, hp, attack, 0);
        _assets.Add(type);
        return type;
    }

    CardView[] Views() => _field.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(v => v.data != null && _cards.hand.Contains(v.data)).ToArray();

    CardView FindView(Func<CardInstance, bool> predicate, params CardInstance[] preserve) =>
        EnsureInHand(predicate, 1, preserve)[0];

    CardView[] EnsureInHand(Func<CardInstance, bool> predicate, int count, params CardInstance[] preserve)
    {
        if (_cards.HandCount == 0) _cards.DrawToHand(CardManager.HAND_SIZE);
        var found = new List<CardView>();
        for (int pass = 0; pass < 120 && found.Count < count; pass++)
        {
            foreach (var view in Views().Where(v => predicate(v.data) &&
                !preserve.Contains(v.data) && found.All(existing => existing.data != v.data)))
            {
                found.Add(view);
                if (found.Count == count) break;
            }
            if (found.Count >= count) break;
            var discard = Views().FirstOrDefault(v => !preserve.Contains(v.data) &&
                found.All(existing => existing.data != v.data));
            if (discard == null) break;
            _cards.TryDiscard(discard);
            _cards.DrawToHand(1);
        }
        Assert.That(found, Has.Count.EqualTo(count), "Required cards could not be found in hand.");
        return found.Take(count).ToArray();
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _objects.Add(go);
        return go.AddComponent<T>();
    }

    sealed class FixedRoll : IRandomSource
    {
        readonly float _roll;
        public FixedRoll(float roll) => _roll = roll;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        public float NextFloat() => _roll;
    }
}
