using System.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class Phase8BloodRitualGenerationTests
{
    [Test]
    public void RitualRiskRollIsSeedDeterministicAndAppearsOnTheDedicatedRareBranch()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/Data/Run/PrototypeRunContent.asset");
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.risks.Count(item => item != null && item.id == "evt_100"), Is.EqualTo(1));
        Assert.That(catalog.risks.Single(item => item != null && item.flow == RunEventFlow.BloodRitual).id,
            Is.EqualTo("evt_100"), "The ritual is identified by its authored flow; its id stays the save identity.");

        var first = RunMapGenerator.Generate(new RunRandomContext("phase8-ritual-same-seed"), 0, catalog);
        var repeat = RunMapGenerator.Generate(new RunRandomContext("phase8-ritual-same-seed"), 0, catalog);
        Assert.That(first.Single(node => node.kind == MapNodeType.Risk).contentId,
            Is.EqualTo(repeat.Single(node => node.kind == MapNodeType.Risk).contentId));

        int ritualCount = 0;
        for (int seed = 0; seed < 500; seed++)
        {
            var map = RunMapGenerator.Generate(new RunRandomContext($"phase8-ritual-weight-{seed}"), 0, catalog);
            if (map.Single(node => node.kind == MapNodeType.Risk).contentId == "evt_100") ritualCount++;
        }
        Assert.That(ritualCount, Is.GreaterThan(0));
        Assert.That(ritualCount, Is.LessThan(500));
    }
}
