using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase1EnemyEncounterTests
{
    readonly System.Collections.Generic.List<Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var item in _created)
            if (item != null) Object.DestroyImmediate(item);
        _created.Clear();
    }

    [Test]
    public void AuthoredShieldbearerAndBrute_HaveApprovedStatsAndAbilities()
    {
#if UNITY_EDITOR
        var shieldbearer = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyTypeData>("Assets/Data/Enemies/RiotGuard.asset");
        var brute = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyTypeData>("Assets/Data/Enemies/Heavy.asset");
        Assert.That(shieldbearer, Is.Not.Null);
        Assert.That(brute, Is.Not.Null);
        Assert.That((shieldbearer.maxHp, shieldbearer.baseAttack, shieldbearer.creditsReward), Is.EqualTo((10, 3, 0)));
        Assert.That((brute.maxHp, brute.baseAttack, brute.creditsReward), Is.EqualTo((18, 3, 0)));
        Assert.That(shieldbearer.abilities.Any(a => a != null && a.effect == EnemyAbilityEffect.StartWithShield && a.amount == 1), Is.True);
        Assert.That(brute.abilities.Any(a => a != null && a.effect == EnemyAbilityEffect.AlternateChargedAttack), Is.True);
#endif
    }

    [Test]
    public void ShieldbearerStartsEncounterWithOneShield_AndBruteAlternatesTelegraphedResponses()
    {
#if UNITY_EDITOR
        var shieldType = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyTypeData>("Assets/Data/Enemies/RiotGuard.asset");
        var bruteType = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyTypeData>("Assets/Data/Enemies/Heavy.asset");
        Assert.That(shieldType, Is.Not.Null);
        Assert.That(bruteType, Is.Not.Null);

        var shield = new EnemyRuntime(shieldType);
        var brute = new EnemyRuntime(bruteType);
        foreach (var ability in shield.type.abilities) ability.OnEncounterStarted(shield, null);

        Assert.That(shield.ShieldCharges, Is.EqualTo(1));
        Assert.That(brute.NextAttackIsCharged, Is.False);
        Assert.That(brute.ResponseAttack, Is.EqualTo(3));
        Assert.That(brute.PrepareResponseAttack(), Is.EqualTo(3));
        Assert.That(brute.CurrentResponseIsCharged, Is.False);
        Assert.That(brute.NextAttackIsCharged, Is.True, "The next response should be telegraphed as charged.");
        Assert.That(brute.ResponseAttack, Is.EqualTo(3), "The currently executing response remains normal.");
        Assert.That(brute.PrepareResponseAttack(), Is.EqualTo(6));
        Assert.That(brute.CurrentResponseIsCharged, Is.True);
        Assert.That(brute.NextAttackIsCharged, Is.False);
        Assert.That(brute.PrepareResponseAttack(), Is.EqualTo(3));
        Assert.That(brute.CurrentResponseIsCharged, Is.False);
        Assert.That(brute.NextAttackIsCharged, Is.True);
#endif
    }

    [Test]
    public void MapGeneration_ExcludesKnightBeforeMapThree_AndNewEnemiesOnlyFromNormalEncounters()
    {
        var goblin = CreateEnemy("Scrapper", 3, 2, 3);
        var knight = CreateEnemy("SecurityGuard", 16, 4, 10);
        var shieldbearer = CreateEnemy("RiotGuard", 10, 3, 0);
        var brute = CreateEnemy("Heavy", 18, 3, 0);
        knight.minMapIndex = 2;
        shieldbearer.eligibleAsEliteBase = false;
        brute.eligibleAsEliteBase = false;
        var enemies = new[] { goblin, knight, shieldbearer, brute };

        for (int mapIndex = 0; mapIndex < 2; mapIndex++)
        {
            var nodes = RunMapGenerator.Generate(new RunRandomContext("phase1-enemy-map-rules"), mapIndex, null, enemies);
            Assert.That(nodes.Where(n => n.kind == MapNodeType.Combat || n.kind == MapNodeType.Elite)
                .Any(n => n.contentId == knight.name), Is.False, $"Knight should be excluded on map index {mapIndex}.");
        }

        bool knightEligibleOnMapThree = false;
        for (int seed = 0; seed < 32; seed++)
        {
            var mapThree = RunMapGenerator.Generate(new RunRandomContext($"phase1-enemy-map-rules-{seed}"), 2, null, enemies);
            knightEligibleOnMapThree |= mapThree.Where(n => n.kind == MapNodeType.Combat).Any(n => n.contentId == knight.name);
        }
        Assert.That(knightEligibleOnMapThree, Is.True,
            "Knight should be eligible for normal combat on map index 2 (Map 3).");

        for (int mapIndex = 0; mapIndex < 8; mapIndex++)
        {
            var nodes = RunMapGenerator.Generate(new RunRandomContext("phase1-enemy-map-rules"), mapIndex, null, enemies);
            Assert.That(nodes.Where(n => n.kind == MapNodeType.Elite).Any(n => n.contentId == shieldbearer.name || n.contentId == brute.name), Is.False,
                "Shieldbearer and Brute are normal-only and must not be selected for Elite encounters.");
        }
    }

    [Test]
    public void AuthoredEnemyAssets_CarryCompositionRulesAsData()
    {
        EnemyTypeData Load(string name) => UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyTypeData>($"Assets/Data/Enemies/{name}.asset");
        var goblin = Load("Scrapper");
        var knight = Load("SecurityGuard");
        var shieldbearer = Load("RiotGuard");
        var brute = Load("Heavy");
        var drummer = Load("HypeMan");
        var captain = Load("CrewLeader");

        Assert.That(knight.minMapIndex, Is.EqualTo(2));
        Assert.That((shieldbearer.eligibleAsEliteBase, brute.eligibleAsEliteBase), Is.EqualTo((false, false)));
        // Unity serializes an unset string as "", so compare normalized fields.
        static (EnemyTypeData, int, int, int, string) Spec(EncounterAllySpec spec) =>
            (spec.type, spec.count, spec.minMapIndex, spec.oneInChance, spec.chanceStream ?? string.Empty);
        Assert.That(Spec(drummer.allies.Single()), Is.EqualTo((goblin, 2, 0, 0, string.Empty)));
        Assert.That(Spec(shieldbearer.allies.Single()), Is.EqualTo((goblin, 1, 0, 0, string.Empty)));
        Assert.That(Spec(captain.allies.Single()), Is.EqualTo((goblin, 2, 0, 0, string.Empty)));
        Assert.That(Spec(knight.allies.Single()), Is.EqualTo((goblin, 1, 3, 2, "knight-weak-ally")));
        Assert.That(EncounterComposer.IsCaptainSupported(captain, goblin), Is.True,
            "Captain support follows asset identity, so renaming the Scrapper cannot disable the aura.");
    }

    [Test]
    public void MapGeneration_IsDeterministicForSameSeedAndMapIndex()
    {
        var enemies = new[] { CreateEnemy("Scrapper", 3, 2, 3), CreateEnemy("SecurityGuard", 16, 4, 10),
            CreateEnemy("RiotGuard", 10, 3, 0), CreateEnemy("Heavy", 18, 3, 0) };
        var first = RunMapGenerator.Generate(new RunRandomContext("phase1-deterministic"), 2, null, enemies);
        var second = RunMapGenerator.Generate(new RunRandomContext("phase1-deterministic"), 2, null, enemies);
        CollectionAssert.AreEqual(first.Select(Signature), second.Select(Signature));
    }

    EnemyTypeData CreateEnemy(string name, int hp, int attack, int credits)
    {
        var type = EnemyTypeData.Create(name, hp, attack, credits);
        type.name = name;
        _created.Add(type);
        return type;
    }

    T CreateComponent<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go.AddComponent<T>();
    }

    static string Signature(PathNode node) =>
        $"{node.id}:{node.kind}:{node.contentId}:{node.col}:{node.row}:{string.Join(",", node.next)}";
}
