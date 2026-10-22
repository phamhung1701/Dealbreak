using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class AuxiliaryAndArsenalTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;
    GearData _arsenal;
    CardEnhancementData _auxiliary;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _arsenal = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/MemoryCard.asset");
        _auxiliary = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Mods/Backup.asset");
        Assert.That(new Object[] { _arsenal, _auxiliary }, Has.All.Not.Null);

        var canvas = CreateComponent<Canvas>("Auxiliary Arsenal Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Auxiliary Arsenal Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var dragCanvas = CreateComponent<Canvas>("Auxiliary Arsenal Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = CreateGameObject("Auxiliary Arsenal Card Prefab", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = CreateComponent<CardManager>("Auxiliary Arsenal Cards");
        _cards.Configure(_field, dragCanvas, prefab, new[] { _arsenal }, new[]
        {
            _auxiliary,
            AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Mods/Profitable.asset"),
            AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Mods/Quickload.asset")
        });
        _cards.ConfigureRandom(new DeterministicRandom(903));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = CreateComponent<CombatManager>("Auxiliary Arsenal Combat");
        _combat.ConfigurePlayer(30);
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
    public void Auxiliary_IsCanonicalRareTierOneThreeFlatAttackWhileInHand()
    {
        Assert.That(_auxiliary.id, Is.EqualTo("enh_008"));
        Assert.That(_auxiliary.canonicalId, Is.EqualTo("enh_008"));
        Assert.That(_auxiliary.displayName, Is.EqualTo("Backup"));
        Assert.That(_auxiliary.rarity, Is.EqualTo("Rare"));
        Assert.That(_auxiliary.tier, Is.EqualTo(1));
        Assert.That(_auxiliary.price, Is.EqualTo(20), "Temporary prototype price; workbook price is blank.");
        Assert.That(_auxiliary.effects, Has.Length.EqualTo(1));
        Assert.That(_auxiliary.effects[0].kind, Is.EqualTo(GameplayEffectKind.FlatAttack));
        Assert.That(_auxiliary.effects[0].trigger, Is.EqualTo(GameplayEffectTrigger.InHandAttackCalculated));
        Assert.That(_auxiliary.effects[0].amount, Is.EqualTo(3));
    }

    [Test]
    public void TwoAuxiliarySources_AddThreeToEveryAttackCardAndPreviewMatchesExecution()
    {
        var sources = HandViews().Take(2).ToArray();
        Assert.That(sources, Has.Length.EqualTo(2));
        foreach (var source in sources)
            Assert.That(_cards.ApplyEnhancement(source.data.Id, _auxiliary), Is.True);
        var attack = FindView(card => card.Rank != CardData.Rank.Ace, sources.Select(v => v.data).ToArray());
        int expectedAttack = attack.data.BaseAttackValue + 6;
        Assert.That(_combat.CalculateCardAttackDamage(attack.data), Is.EqualTo(expectedAttack));
        var target = StartTarget();
        Assert.That(_combat.CalculateCardAttackDamage(attack.data), Is.EqualTo(expectedAttack));
        _combat.CriticalChancePercent = 0f;

        Assert.That(_combat.TryPlayCards(new[] { attack }, target), Is.True);

        Assert.That(target.currentHp, Is.EqualTo(1000 - expectedAttack));
    }

    [Test]
    public void AuxiliaryCardInHandCanContributeToItsOwnAttackBeforeCommit()
    {
        var auxiliaryCard = FindView(card => card.Rank != CardData.Rank.Ace);
        Assert.That(_cards.ApplyEnhancement(auxiliaryCard.data.Id, _auxiliary), Is.True);
        int expectedAttack = auxiliaryCard.data.BaseAttackValue + 3;
        Assert.That(_combat.CalculateCardAttackDamage(auxiliaryCard.data), Is.EqualTo(expectedAttack));
        var target = StartTarget();
        _combat.CriticalChancePercent = 0f;

        Assert.That(_combat.TryPlayCards(new[] { auxiliaryCard }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(1000 - expectedAttack));
    }

    [Test]
    public void AuxiliaryInDiscard_DoesNotModifyAttackPreviewOrExecution()
    {
        var auxiliaryCard = HandViews()[0];
        Assert.That(_cards.TryDiscard(auxiliaryCard), Is.True);
        Assert.That(_cards.ApplyEnhancement(auxiliaryCard.data.Id, _auxiliary), Is.True);
        var attack = FindView(card => card.Rank != CardData.Rank.Ace);
        int expectedAttack = _combat.CalculateCardAttackDamage(attack.data);
        var target = StartTarget();
        _combat.CriticalChancePercent = 0f;

        Assert.That(_combat.TryPlayCards(new[] { attack }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(1000 - expectedAttack));
    }

    [Test]
    public void AuxiliaryInHandAddsFlatDamageToEachCardInAcePair()
    {
        var auxiliaryCard = FindView(card => card.Rank != CardData.Rank.Ace);
        Assert.That(_cards.ApplyEnhancement(auxiliaryCard.data.Id, _auxiliary), Is.True);
        var ace = FindView(card => card.Rank == CardData.Rank.Ace, auxiliaryCard.data);
        var other = FindView(card => card.Rank != CardData.Rank.Ace, auxiliaryCard.data, ace.data);
        int expectedDamage = _combat.CalculateCardAttackDamage(ace.data) +
            _combat.CalculateCardAttackDamage(other.data);
        var target = StartTarget();
        _combat.CriticalChancePercent = 0f;

        Assert.That(_combat.TryPlayCards(new[] { ace, other }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(1000 - expectedDamage));
    }

    [Test]
    public void Arsenal_IncreasesEffectiveHandCapacityAndResetRestoresBaseEight()
    {
        Assert.That(_cards.HandCapacity, Is.EqualTo(CardManager.HAND_SIZE));
        Assert.That(_cards.HandCount, Is.EqualTo(CardManager.HAND_SIZE));
        string definitionBefore = JsonUtility.ToJson(_arsenal);
        Assert.That(_cards.BuyGear(_arsenal, 0), Is.True);
        Assert.That(_cards.HandCapacity, Is.EqualTo(CardManager.HAND_SIZE + 1));
        Assert.That(_cards.DrawToHand(1), Is.EqualTo(1));
        Assert.That(_cards.HandCount, Is.EqualTo(CardManager.HAND_SIZE + 1));
        Assert.That(_cards.DrawToHand(1), Is.Zero, "Hand draws clamp to the modified capacity.");
        Assert.That(JsonUtility.ToJson(_arsenal), Is.EqualTo(definitionBefore));

        _cards.Reset();
        Assert.That(_cards.HandCapacity, Is.EqualTo(CardManager.HAND_SIZE));
        _cards.BuildDeck();
        _cards.DealHand();
        Assert.That(_cards.HandCount, Is.EqualTo(CardManager.HAND_SIZE));
    }

    EnemyRuntime StartTarget()
    {
        var definition = EnemyTypeData.Create("Auxiliary Arsenal Test Enemy", 1000, 0, 0);
        _created.Add(definition);
        var enemy = new EnemyRuntime(definition);
        _combat.StartEnemy(enemy);
        return enemy;
    }

    CardView FindView(System.Func<CardInstance, bool> predicate, params CardInstance[] preserve)
    {
        for (int pass = 0; pass < 160; pass++)
        {
            var found = HandViews().FirstOrDefault(view => predicate(view.data) && !preserve.Contains(view.data));
            if (found != null) return found;
            var discard = HandViews().FirstOrDefault(view => !preserve.Contains(view.data));
            if (discard == null) break;
            Assert.That(_cards.TryDiscard(discard), Is.True);
            _cards.DrawToHand(1);
        }
        Assert.Fail("Required card could not be found in the 40-card collection.");
        return null;
    }

    CardView[] HandViews() => _field.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(view => view.data != null && _cards.hand.Contains(view.data)).ToArray();

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var gameObject = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }
}
