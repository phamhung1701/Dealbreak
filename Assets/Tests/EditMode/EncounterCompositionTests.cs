using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Route previews and spawned encounters are built by one composer and must always agree.
public sealed class EncounterCompositionTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    RunContentCatalog _catalog;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cards = Make<CardManager>("Composition Cards");
        _cards.Configure(null, null, null, System.Array.Empty<GearData>());
        _cards.BuildDeck();
        _combat = Make<CombatManager>("Composition Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Composition Run");
        _catalog = AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/Data/Run/PrototypeRunContent.asset");
        _run.contentCatalog = _catalog;
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
    public void EveryAuthoredEncounter_PreviewTotalsMatchSpawnedEnemies()
    {
        var cases = _catalog.normalEnemies.Select(enemy => (enemy, MapNodeType.Combat))
            .Concat(_catalog.eliteEnemies.Select(enemy => (enemy, MapNodeType.Elite)));
        foreach (var (enemy, kind) in cases)
        foreach (int mapIndex in new[] { 0, 3, 11 })
        foreach (int nodeId in new[] { 1, 2, 3, 4 })
        {
            var node = new PathNode { id = nodeId, kind = kind, contentId = enemy.name, mapIndex = mapIndex,
                accessible = true, revealed = true };
            _run.currentPath.Clear();
            _run.currentPath.Add(node);
            string preview = _run.GetNodeDesc(node);

            _combat.Reset();
            Assert.That(typeof(RunManager).GetMethod("TryStartEncounter", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_run, new object[] { node }), Is.True);
            long hp = _combat.Enemies.Sum(value => (long)value.maxHp);
            long attack = _combat.Enemies.Sum(value => (long)value.EffectiveAttack);
            string context = $"{enemy.name} {kind} map {mapIndex} node {nodeId}: {preview}";
            Assert.That(preview, Does.Contain($"{hp} HP / {attack} ATK"), context);
            if (_combat.Enemies.Count > 1)
                Assert.That(preview, Does.Contain($"{_combat.Enemies.Count} enemies"), context);
        }
    }

    [Test]
    public void KnightAlly_UsesTheExistingSeededStreamFromMapFourOnly()
    {
        var knight = _catalog.normalEnemies.Single(enemy => enemy.name == "Knight");
        var random = new RunRandomContext(null);
        for (int nodeId = 0; nodeId < 12; nodeId++)
        {
            var early = EncounterComposer.Compose(knight, (10, 2), MapNodeType.Combat, false, 2, nodeId, random);
            Assert.That(early.Count, Is.EqualTo(knight.encounterCount), "No ally before map index 3.");
            var late = EncounterComposer.Compose(knight, (10, 2), MapNodeType.Combat, false, 5, nodeId, random);
            bool expected = random.CreateStream("knight-weak-ally", unchecked(5 * 7919 ^ nodeId)).NextInt(0, 2) == 0;
            Assert.That(late.Count, Is.EqualTo(knight.encounterCount + (expected ? 1 : 0)));
        }
    }

    [Test]
    public void GenericEnforcerAndHeadliner_NeverBringAllies()
    {
        var drummer = _catalog.normalEnemies.Single(enemy => enemy.name == "WarDrummer");
        Assert.That(EncounterComposer.Compose(drummer, (8, 1), MapNodeType.Elite, false, 4, 1, null).Count, Is.EqualTo(1));
        Assert.That(EncounterComposer.Compose(drummer, (8, 1), MapNodeType.Headliner, false, 4, 1, null).Count, Is.EqualTo(1));
        Assert.That(EncounterComposer.Compose(drummer, (8, 1), MapNodeType.Combat, false, 4, 1, null).Count, Is.EqualTo(3));
    }

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
