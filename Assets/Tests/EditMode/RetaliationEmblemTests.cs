using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class RetaliationEmblemTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;
    GearData _retaliation;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _retaliation = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Relics/RetaliationEmblem.asset");
        Assert.That(_retaliation, Is.Not.Null);

        var canvas = CreateComponent<Canvas>("Retaliation Hand Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Retaliation Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var dragCanvas = CreateComponent<Canvas>("Retaliation Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = CreateGameObject("Retaliation Card Prefab", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = CreateComponent<CardManager>("Retaliation Card Manager");
        _cards.Configure(_field, dragCanvas, prefab, new[] { _retaliation },
            System.Array.Empty<CardEnhancementData>());
        _cards.ConfigureRandom(new DeterministicRandom(301));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = CreateComponent<CombatManager>("Retaliation Combat Manager");
        _combat.ConfigurePlayer(30);
        Assert.That(_cards.BuyGear(_retaliation, 0), Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void Asset_IsCanonicalCommonTierOneTypedFiveDamageBlockReaction()
    {
        Assert.That(_retaliation.id, Is.EqualTo("rel_016"));
        Assert.That(_retaliation.canonicalId, Is.EqualTo("rel_016"));
        Assert.That(_retaliation.displayName, Is.EqualTo("Counterpunch Gloves"));
        Assert.That(_retaliation.rarity, Is.EqualTo("Common"));
        Assert.That(_retaliation.tier, Is.EqualTo(1));
        Assert.That(_retaliation.capacityCategory, Is.EqualTo(GearCapacityCategory.Persistent));
        Assert.That(_retaliation.price, Is.EqualTo(20), "Temporary prototype price; workbook price is blank.");
        Assert.That(_retaliation.effects, Has.Length.EqualTo(1));
        Assert.That(_retaliation.effects[0].kind, Is.EqualTo(GameplayEffectKind.CounterDamage));
        Assert.That(_retaliation.effects[0].trigger, Is.EqualTo(GameplayEffectTrigger.AttackBlocked));
        Assert.That(_retaliation.effects[0].amount, Is.EqualTo(5));
    }

    [Test]
    public void OneFullyBlockedAttack_CounterattacksItsExactSourceOnce()
    {
        var enemy = StartDefenseWindow(2).Single();
        int hpBeforeBlock = enemy.currentHp;
        var damage = new List<DamageResult>();
        _combat.OnDamageResolved += damage.Add;
        var defense = FindView(card => _combat.CalculateCardDefense(card) >= 2);

        Assert.That(_combat.TryDefendWithCards(new[] { defense }, enemy), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(hpBeforeBlock - 5));
        var retaliationHits = damage.Where(result => result.Request.Origin == CombatDamageOrigin.Reactive).ToArray();
        Assert.That(retaliationHits, Has.Length.EqualTo(1));
        Assert.That(retaliationHits[0].Request.Target, Is.SameAs(enemy));
        Assert.That(retaliationHits[0].Request.RequestedDamage, Is.EqualTo(5));
        Assert.That(_combat.pendingDamage, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void MultiEnemyDefense_CounterattacksEachActuallyBlockedEnemyOnceInStableEncounterOrder()
    {
        var enemies = StartDefenseWindow(2, 3);
        int firstHp = enemies[0].currentHp;
        int secondHp = enemies[1].currentHp;
        var damage = new List<DamageResult>();
        _combat.OnDamageResolved += damage.Add;
        var two = FindView(card => card.Rank == CardData.Rank.Two);
        var three = FindView(card => card.Rank == CardData.Rank.Three, two.data);
        Assert.That(_combat.CalculateCardDefense(two.data), Is.GreaterThanOrEqualTo(2));
        Assert.That(_combat.CalculateCardDefense(three.data), Is.GreaterThanOrEqualTo(3));

        Assert.That(_combat.TryDefendWithCards(new[] { three, two }, enemies[0]), Is.True);
        Assert.That(enemies[0].currentHp, Is.EqualTo(firstHp - 5));
        Assert.That(enemies[1].currentHp, Is.EqualTo(secondHp - 5));
        var retaliationTargets = damage
            .Where(result => result.Request.Origin == CombatDamageOrigin.Reactive)
            .Select(result => result.Request.Target).ToArray();
        CollectionAssert.AreEqual(new ICombatDamageTarget[] { enemies[0], enemies[1] }, retaliationTargets);
        Assert.That(_combat.PendingAttackCount, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void ShieldAbsorbingRemainingDamage_IsNotACardBlockAndDoesNotRetaliate()
    {
        var enemy = StartDefenseWindow(2).Single();
        int hpBefore = enemy.currentHp;
        var damage = new List<DamageResult>();
        _combat.OnDamageResolved += damage.Add;
        _combat.GrantPlayerShield(1);

        _combat.TakeRemainingDamage();

        Assert.That(enemy.currentHp, Is.EqualTo(hpBefore));
        Assert.That(damage.Count(result => result.Request.Origin == CombatDamageOrigin.Reactive), Is.Zero);
        Assert.That(_combat.player.ShieldCharges, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void CounterattackDefeatingLastEnemy_ResolvesVictoryExactlyOnce()
    {
        var enemy = StartEnemy(6, 1);
        var ace = FindView(card => card.Rank == CardData.Rank.Ace);
        Assert.That(_combat.TryPlayCards(new[] { ace }, enemy), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(5));
        var resultCount = 0;
        EncounterResult? terminal = null;
        _combat.OnEncounterResult += result => { resultCount++; terminal = result; };
        var defense = FindView(card => _combat.CalculateCardDefense(card) >= 1);

        Assert.That(_combat.TryDefendWithCards(new[] { defense }, enemy), Is.True);
        Assert.That(enemy.IsDefeated, Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameWon));
        Assert.That(terminal, Is.EqualTo(EncounterResult.Victory));
        Assert.That(resultCount, Is.EqualTo(1));
        Assert.That(_combat.TryDefendWithCards(new[] { defense }, enemy), Is.False);
        Assert.That(resultCount, Is.EqualTo(1));
    }

    EnemyRuntime[] StartDefenseWindow(params int[] attacks)
    {
        var enemies = new EnemyRuntime[attacks.Length];
        for (int i = 0; i < attacks.Length; i++)
            enemies[i] = MakeEnemy($"Retaliation Attacker {i}", 100, attacks[i]);
        _combat.StartEncounter(enemies);
        var attackCard = FindView(card => card.Rank == CardData.Rank.Four);
        Assert.That(_combat.TryPlayCards(new[] { attackCard }, enemies[0]), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        return enemies;
    }

    EnemyRuntime StartEnemy(int hp, int attack)
    {
        var enemy = MakeEnemy("Retaliation Terminal Attacker", hp, attack);
        _combat.StartEnemy(enemy);
        return enemy;
    }

    EnemyRuntime MakeEnemy(string name, int hp, int attack)
    {
        var definition = EnemyTypeData.Create(name, hp, attack, 0);
        _created.Add(definition);
        return new EnemyRuntime(definition);
    }

    CardView FindView(System.Func<CardInstance, bool> predicate, params CardInstance[] preserve)
    {
        for (int pass = 0; pass < 160; pass++)
        {
            var found = HandViews().FirstOrDefault(view => predicate(view.data) &&
                !preserve.Contains(view.data));
            if (found != null) return found;
            var discard = HandViews().FirstOrDefault(view => !preserve.Contains(view.data));
            if (discard == null) break;
            Assert.That(_cards.TryDiscard(discard), Is.True);
            _cards.DrawToHand(1);
        }
        Assert.Fail("Required card could not be found in the 40-card collection.");
        return null;
    }

    List<CardView> HandViews()
    {
        var views = new List<CardView>();
        for (int i = 0; i < _field.cardsHolder.childCount; i++)
            if (_field.cardsHolder.GetChild(i).TryGetComponent<CardView>(out var view) &&
                view.data != null && _cards.hand.Contains(view.data)) views.Add(view);
        return views;
    }

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var gameObject = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }
}
