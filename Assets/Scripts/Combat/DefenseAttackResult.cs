/// <summary>Authoritative per-hit result from a committed player Block assignment.</summary>
public readonly struct DefenseAttackResult
{
    public EnemyRuntime Attacker { get; }
    public int AttackIndex { get; }
    public int IncomingDamage { get; }
    public int EffectiveBlock { get; }
    public int BlockedDamage { get; }
    public int RemainingDamage { get; }
    /// <summary>Spade Shield Rare/Epic: a Spade blocked this attack, so an over-block still counts.</summary>
    public bool SpadeOverblockAllowed { get; }

    public bool IsPerfectBlock => IncomingDamage > 0 && BlockedDamage == IncomingDamage && RemainingDamage == 0 &&
        (EffectiveBlock == IncomingDamage || SpadeOverblockAllowed && EffectiveBlock > IncomingDamage);

    public DefenseAttackResult(EnemyRuntime attacker, int attackIndex, int incomingDamage,
        int effectiveBlock, int blockedDamage, int remainingDamage, bool spadeOverblockAllowed = false)
    {
        SpadeOverblockAllowed = spadeOverblockAllowed;
        Attacker = attacker;
        AttackIndex = attackIndex;
        IncomingDamage = incomingDamage;
        EffectiveBlock = effectiveBlock;
        BlockedDamage = blockedDamage;
        RemainingDamage = remainingDamage;
    }
}
