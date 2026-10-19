using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class DealbreakInternalRebrandingTests
{
    [Test]
    public void PreRenameVersionOneSave_RestoresWithoutChangingPayloadOrRandomState()
    {
        var catalog = SaveContentCatalog.Load();
        var store = new RunSaveStore("Assets/Tests/EditMode/Fixtures/LegacyRunV1.json");
        Assert.That(store.TryRead(catalog, out var legacy, out string error), Is.True, error);
        string expected = JsonUtility.ToJson(legacy);
        var objects = new List<GameObject>();
        T Make<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name); objects.Add(go); return go.AddComponent<T>();
        }
        try
        {
            GameplayInputGate.Clear(); DevModeRuntime.Disable();
            var cards = Make<CardManager>();
            cards.Configure(null, null, null, catalog.gearItems, catalog.enhancements, catalog.consumables);
            var combat = Make<CombatManager>(); combat.ConfigurePlayer(30);
            var run = Make<RunManager>(); run.contentCatalog = catalog.runContent;
            run.RestoreSave(legacy, catalog);
            Assert.That(JsonUtility.ToJson(run.CaptureSave()), Is.EqualTo(expected));
            Assert.That(cards.ownedCards.Count, Is.EqualTo(40));
            Assert.That(cards.OwnedGearInstances.Count, Is.EqualTo(2));
            Assert.That(cards.OwnedGearInstances[0].State.GetCounter(-80), Is.EqualTo(7));
            Assert.That(cards.credits, Is.EqualTo(legacy.cards.gold));
            Assert.That(run.headlinerDeck.Count, Is.EqualTo(12));
            Assert.That(run.headlinerIndex, Is.EqualTo(legacy.bossIndex));
            Assert.That(cards.OwnedGearInstances.Select(x => x.Id), Is.EqualTo(legacy.cards.artifacts.Select(x => x.id)));
        }
        finally
        {
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            GameplayInputGate.Clear();
        }
    }

    [Test]
    public void RenamedEnums_PreservePersistentNumbersAndShopIdentifiers()
    {
        Assert.That((int)MapNodeType.Headliner, Is.EqualTo(6));
        Assert.That((int)ShopOfferKind.Gear, Is.Zero);
        var gear = ScriptableObject.CreateInstance<GearData>();
        try
        {
            gear.id = "rel_005";
            var offer = new ShopOffer { kind = ShopOfferKind.Gear, gear = gear };
            Assert.That(offer.StableId, Is.EqualTo("artifact:rel_005"));
            Assert.That(ShopOffer.PersistentGoodsPrefix(offer.kind) + ":" + gear.id, Is.EqualTo(offer.StableId));
            offer.generation = 2;
            Assert.That(offer.StableId, Is.EqualTo("refresh:2:artifact:rel_005"));
        }
        finally { Object.DestroyImmediate(gear); }
    }

    [Test]
    public void AuthoredContentAndPrefabScripts_ResolveAfterAssemblyAndGearRenames()
    {
        var catalog = SaveContentCatalog.Load();
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.gearItems, Is.Not.Empty);
        Assert.That(catalog.gearItems.All(x => x != null && !string.IsNullOrEmpty(x.id)), Is.True);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, AssetDatabase.GetAssetPath(prefab));
        }
    }

    [Test]
    public void BuildScenes_KeepScriptAndMigratedCatalogAndLabelReferences()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var entry in EditorBuildSettings.scenes.Where(x => x.enabled))
            {
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, entry.path + "/" + transform.name);
                var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GameController>(true)).FirstOrDefault();
                if (controller == null) continue;
                Assert.That(controller.contentRegistry, Is.SameAs(SaveContentCatalog.Load()),
                    "The Game scene plays from the same registry that saves resolve against.");
                var status = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<RunStatusUI>(true)).First();
                Assert.That(status.creditsLabel, Is.Not.Null);
                Assert.That(status.headlinerProgressLabel, Is.Not.Null);
                // The original scene leaves the optional text inventory unassigned; the Gear rail owns its presentation.
            }
        }
        finally
        {
            // Batchmode may start without a loaded/active scene, which cannot be restored.
            if (setup.Any(x => x.isLoaded) && setup.Count(x => x.isActive) == 1)
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
