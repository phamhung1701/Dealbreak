using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct EncounterAllySpec
{
    public EnemyTypeData type;
    [Min(1)] public int count;
    [Tooltip("Zero-based District index from which these allies join.")]
    [Min(0)] public int minMapIndex;
    [Tooltip("0 or 1: always join. N > 1: a seeded 1-in-N chance per route node.")]
    [Min(0)] public int oneInChance;
    [Tooltip("Named run-random stream for the chance roll. Changing it changes seeded outcomes.")]
    public string chanceStream;
}

public readonly struct EncounterMember
{
    public EnemyTypeData Type { get; }
    public int Hp { get; }
    public int Attack { get; }
    public int Instance { get; }
    public int GroupSize { get; }

    public EncounterMember(EnemyTypeData type, int hp, int attack, int instance, int groupSize)
    {
        Type = type;
        Hp = hp;
        Attack = attack;
        Instance = instance;
        GroupSize = groupSize;
    }
}

/// <summary>
/// Pure encounter composition shared by spawning and route previews, so both always agree.
/// Ally chance rolls use their own context-derived streams; composing never consumes shared RNG.
/// </summary>
public static class EncounterComposer
{
    public static (int hp, int attack) ScaleStats(int hp, int attack, int mapIndex) =>
        mapIndex >= InfiniteRunScaling.FirstInfiniteMapIndex
            ? InfiniteRunScaling.Scale(hp, attack, mapIndex)
            : EnemyMapScaling.Scale(hp, attack, mapIndex);

    /// <param name="authoredElite">True when an Elite node uses an authored Enforcer asset.
    /// Generic Enforcers and Headliners never bring allies.</param>
    public static List<EncounterMember> Compose(EnemyTypeData primary, (int hp, int attack) primaryStats,
        MapNodeType kind, bool authoredElite, int mapIndex, int nodeId, RunRandomContext random)
    {
        var members = new List<EncounterMember>();
        if (primary == null) return members;
        int primaryCount = kind == MapNodeType.Combat ? Mathf.Max(1, primary.encounterCount) : 1;
        for (int i = 0; i < primaryCount; i++)
            members.Add(new EncounterMember(primary, primaryStats.hp, primaryStats.attack, i + 1, primaryCount));

        bool alliesApply = kind == MapNodeType.Combat || kind == MapNodeType.Elite && authoredElite;
        if (!alliesApply || primary.allies == null) return members;

        var allyTypes = new List<EnemyTypeData>();
        foreach (var spec in primary.allies)
        {
            if (spec.type == null || mapIndex < spec.minMapIndex ||
                !RollsChance(spec, mapIndex, nodeId, random)) continue;
            for (int i = 0; i < Mathf.Max(1, spec.count); i++) allyTypes.Add(spec.type);
        }
        foreach (var allyType in allyTypes)
        {
            var stats = ScaleStats(allyType.maxHp, allyType.baseAttack, mapIndex);
            int groupSize = 0, instance = 1;
            foreach (var other in allyTypes) if (ReferenceEquals(other, allyType)) groupSize++;
            foreach (var member in members) if (ReferenceEquals(member.Type, allyType)) instance++;
            members.Add(new EncounterMember(allyType, stats.hp, stats.attack, instance, groupSize));
        }
        return members;
    }

    static bool RollsChance(EncounterAllySpec spec, int mapIndex, int nodeId, RunRandomContext random)
    {
        if (spec.oneInChance <= 1) return true;
        if (random == null || string.IsNullOrWhiteSpace(spec.chanceStream)) return false;
        return random.CreateStream(spec.chanceStream, unchecked(mapIndex * 7919 ^ nodeId))
            .NextInt(0, spec.oneInChance) == 0;
    }

    public static bool HasAbility(EnemyTypeData type, EnemyAbilityEffect effect)
    {
        if (type?.abilities == null) return false;
        foreach (var ability in type.abilities)
            if (ability != null && ability.effect == effect) return true;
        return false;
    }

    /// <summary>A Captain supports the ally types it is authored to bring into its encounters.</summary>
    public static bool IsCaptainSupported(EnemyTypeData captain, EnemyTypeData ally)
    {
        if (captain == null || ally == null || ReferenceEquals(captain, ally) ||
            !HasAbility(captain, EnemyAbilityEffect.Captaincy) || captain.allies == null) return false;
        foreach (var spec in captain.allies)
            if (ReferenceEquals(spec.type, ally)) return true;
        return false;
    }

    /// <summary>Total HP and effective ATK, including the Captain aura of +1 per supported ally.</summary>
    public static (int hp, int attack) Totals(IReadOnlyList<EncounterMember> members)
    {
        long hp = 0, attack = 0;
        EnemyTypeData captain = null;
        foreach (var member in members)
            if (captain == null && HasAbility(member.Type, EnemyAbilityEffect.Captaincy)) captain = member.Type;
        foreach (var member in members)
        {
            hp += Math.Max(0, member.Hp);
            attack += Math.Max(0, member.Attack) + (IsCaptainSupported(captain, member.Type) ? 1 : 0);
        }
        return ((int)Math.Min(int.MaxValue, hp), (int)Math.Min(int.MaxValue, attack));
    }
}
