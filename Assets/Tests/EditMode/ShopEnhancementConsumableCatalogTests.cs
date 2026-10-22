using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ShopEnhancementConsumableCatalogTests
{
    readonly List<Object> _assets = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _assets)
            if (asset) Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void EveryEnhancementHasExactlyOneShopAvailableMappedConsumable()
    {
        var enhancements = AssetDatabase.FindAssets("t:CardEnhancementData", new[] { "Assets/Data/Mods" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<CardEnhancementData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null).ToArray();
        var consumables = AssetDatabase.FindAssets("t:ConsumableData", new[] { "Assets/Data/Tricks" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<ConsumableData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null && asset.shopAvailable && asset.effectType == ConsumableEffectType.ApplyEnhancement)
            .ToArray();

        Assert.That(enhancements, Is.Not.Empty);
        foreach (var enhancement in enhancements)
            Assert.That(consumables.Count(consumable => consumable.enhancementToApply == enhancement), Is.EqualTo(1),
                $"{enhancement.name} must have exactly one explicit Shop Consumable mapping.");
        Assert.That(consumables.Select(consumable => consumable.enhancementToApply).Distinct().Count(),
            Is.EqualTo(consumables.Length), "A mapped Consumable must not ambiguously represent multiple offers.");
    }
}
