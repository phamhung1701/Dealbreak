using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Resources/RunSaveContent is the single content registry for play and saves.
// Authored content that is missing from it is unreachable in play and unloadable from saves.
public sealed class ContentRegistryTests
{
    static T[] Assets<T>(string folder) where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null).ToArray();

    [Test]
    public void EveryAuthoredGearModAndTrick_IsRegisteredOnceWithAUniqueId()
    {
        var registry = SaveContentCatalog.Load();
        Assert.That(registry, Is.Not.Null);
        CollectionAssert.AreEquivalent(Assets<GearData>("Assets/Data/Relics"), registry.gearItems);
        CollectionAssert.AreEquivalent(Assets<CardEnhancementData>("Assets/Data/Enhancements"), registry.enhancements);
        CollectionAssert.AreEquivalent(Assets<ConsumableData>("Assets/Data/Consumables"), registry.consumables);
        Assert.That(registry.gearItems.Select(item => item.id), Is.Unique);
        Assert.That(registry.enhancements.Select(item => item.id), Is.Unique);
        Assert.That(registry.consumables.Select(item => item.id), Is.Unique);
    }

    [Test]
    public void EveryAuthoredIncidentAndEnemy_IsReachableFromTheRunContent()
    {
        var run = SaveContentCatalog.Load().runContent;
        Assert.That(run, Is.Not.Null);
        var events = run.events.Concat(run.risks).Concat(run.upgrades).Where(value => value != null).ToArray();
        CollectionAssert.AreEquivalent(Assets<RunEventDefinition>("Assets/Data/Run/Events"), events);
        Assert.That(events.Select(value => value.id), Is.Unique);

        var enemies = run.normalEnemies.Concat(run.eliteEnemies).ToArray();
        CollectionAssert.AreEquivalent(Assets<EnemyTypeData>("Assets/Data/Enemies"), enemies);
        foreach (var ally in enemies.SelectMany(enemy => enemy.allies ?? System.Array.Empty<EncounterAllySpec>()))
            Assert.That(run.normalEnemies, Does.Contain(ally.type), "Encounter allies must be registered enemies.");
    }
}
