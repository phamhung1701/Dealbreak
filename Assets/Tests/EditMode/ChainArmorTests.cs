using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ChainArmorTests
{
    readonly List<UnityEngine.Object> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    GearData _chainArmor;
    GearData _leatherArmor;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _chainArmor = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/TacticalPlating.asset");
        _leatherArmor = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/StreetJacket.asset");
        Assert.That(_chainArmor, Is.Not.Null);
        Assert.That(_leatherArmor, Is.Not.Null);

        var canvas = Make<Canvas>("Chain Armor Test Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var handObject = new GameObject("Chain Armor Test Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _objects.Add(handObject);
        handObject.transform.SetParent(canvas.transform, false);
        var field = handObject.AddComponent<Field>();
        field.cardsHolder = (RectTransform)handObject.transform;

        var dragCanvas = Make<Canvas>("Chain Armor Test Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = new GameObject("Chain Armor Test Card", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        _objects.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();

        _cards = Make<CardManager>("Chain Armor Test Cards");
        _cards.Configure(field, dragCanvas, prefab, new[] { _chainArmor, _leatherArmor },
            System.Array.Empty<CardEnhancementData>());
        _combat = Make<CombatManager>("Chain Armor Test Combat");
        _combat.ConfigurePlayer(30);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }

    [Test]
    public void Asset_UsesCanonicalRareTierOneDefinitionAndDoesNotMutateAtRuntime()
    {
        Assert.That(_chainArmor.id, Is.EqualTo("rel_019"));
        Assert.That(_chainArmor.canonicalId, Is.EqualTo("rel_019"));
        Assert.That(_chainArmor.displayName, Is.EqualTo("Tactical Plating"));
        Assert.That(_chainArmor.rarity, Is.EqualTo("Rare"));
        Assert.That(_chainArmor.tier, Is.EqualTo(1));
        Assert.That(_chainArmor.capacityCategory, Is.EqualTo(GearCapacityCategory.Persistent));
        Assert.That(_chainArmor.price, Is.EqualTo(25), "Prototype price only; workbook price is blank.");
        Assert.That(_chainArmor.effects, Has.Length.EqualTo(1));
        Assert.That(_chainArmor.effects[0].kind, Is.EqualTo(GameplayEffectKind.IncomingCombatDamageReduction));
        Assert.That(_chainArmor.effects[0].trigger, Is.EqualTo(GameplayEffectTrigger.IncomingDamageCalculated));
        Assert.That(_chainArmor.effects[0].amount, Is.EqualTo(8));

        string before = JsonUtility.ToJson(_chainArmor);
        Assert.That(_cards.BuyGear(_chainArmor, 0), Is.True);
        ResolveHit(0, 5);
        Assert.That(JsonUtility.ToJson(_chainArmor), Is.EqualTo(before));
    }

    [Test]
    public void ReducesEachIncomingCombatHitByEight_FloorsAtZero_BeforeShield()
    {
        Assert.That(_cards.BuyGear(_chainArmor, 0), Is.True);
        var first = ResolveHit(0, 6);
        Assert.That(first.ModifiedDamage, Is.Zero);
        Assert.That(first.ShieldConsumed, Is.False);
        Assert.That(first.ActualHpLost, Is.Zero);

        _combat.GrantPlayerShield(1);
        var shielded = ResolveHit(1, 12);
        Assert.That(shielded.ModifiedDamage, Is.EqualTo(4));
        Assert.That(shielded.ShieldConsumed, Is.True);
        Assert.That(shielded.ActualHpLost, Is.Zero);

        var nextIndependentHit = ResolveHit(2, 12);
        Assert.That(nextIndependentHit.ModifiedDamage, Is.EqualTo(4));
        Assert.That(nextIndependentHit.ActualHpLost, Is.EqualTo(4));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(26));
    }

    [Test]
    public void StacksAdditivelyWithLeatherArmor_AndDoesNotReduceDirectRunHpCosts()
    {
        Assert.That(_cards.BuyGear(_leatherArmor, 0), Is.True);
        Assert.That(_cards.BuyGear(_chainArmor, 0), Is.True);
        var hit = ResolveHit(0, 15);
        Assert.That(hit.ModifiedDamage, Is.EqualTo(4), "Leather (-3) and Chain Armor (-8) apply before Shield.");
        var clamped = ResolveHit(1, 10);
        Assert.That(clamped.ModifiedDamage, Is.Zero);

        Assert.That(_combat.TakeRunDamage(10), Is.EqualTo(10));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(16), "Direct non-combat/run HP cost bypasses incoming combat modifiers.");
    }

    DamageResult ResolveHit(int hitIndex, int rawDamage)
    {
        var targetDefinition = EnemyTypeData.Create("Chain Armor Test Enemy", 100, 1, 0);
        _objects.Add(targetDefinition);
        var enemy = new EnemyRuntime(targetDefinition);
        var action = new CombatActionContext(1, CombatActionOrigin.EnemyRetaliation, sourceEnemy: enemy);
        var request = new DamageRequest(action, hitIndex, CombatDamageOrigin.EnemyAggregate,
            enemy, _combat.player, rawDamage);
        return new CombatResolver().Resolve(request, _combat);
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _objects.Add(go);
        return go.AddComponent<T>();
    }
}
