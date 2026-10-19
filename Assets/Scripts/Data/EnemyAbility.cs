using UnityEngine;

public enum EnemyAbilityEffect
{
    IncreaseAttackOnEncounterStart,
    IncreaseAttackAfterPlayerCard,
    HealAfterPlayerCard,
    ReduceIncomingCardDamage,
    StartWithShield,
    AlternateChargedAttack,
    Captaincy,
    RoyalGuard,
    HeavySwing,
    Desperation,
    Regeneration,
    Guarded,
    Silence,
    Withering,
    Oppression,
    DoubleStrike,
    SuitCall,
    WarDrum,
    MasterThief,
    FullCoverageBlock
}

[CreateAssetMenu(fileName = "EnemyAbility", menuName = "Game/Enemy Ability")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "EnemyAbility")]
public class EnemyAbility : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    public EnemyAbilityEffect effect;
    [Min(0)] public int amount = 1;

    public virtual int ModifyIncomingDamage(
        EnemyRuntime enemy,
        CombatManager context,
        DamageRequest request,
        int damage)
    {
        return request.Origin == CombatDamageOrigin.Card
            ? ModifyIncomingCardDamage(enemy, context, damage)
            : damage;
    }

    public virtual int ModifyIncomingCardDamage(EnemyRuntime enemy, CombatManager context, int damage)
    {
        return effect == EnemyAbilityEffect.ReduceIncomingCardDamage
            ? Mathf.Max(0, damage - amount)
            : damage;
    }

    public virtual void OnEncounterStarted(EnemyRuntime enemy, CombatManager context)
    {
        if (effect == EnemyAbilityEffect.IncreaseAttackOnEncounterStart && amount > 0)
        {
            enemy.IncreaseAttack(amount);
            enemy.NotifyAbilityTriggered(this);
        }
        else if (effect == EnemyAbilityEffect.StartWithShield && enemy.GainShield(amount) > 0)
            enemy.NotifyAbilityTriggered(this);
    }

    public virtual void OnIncomingHitResolved(
        EnemyRuntime enemy,
        CombatManager context,
        DamageResult result)
    {
    }

    public virtual void OnPlayerCardResolved(EnemyRuntime enemy, CombatManager context)
    {
        if (enemy == null || enemy.IsDefeated) return;

        if (effect == EnemyAbilityEffect.IncreaseAttackAfterPlayerCard && amount > 0)
        {
            enemy.IncreaseAttack(amount);
            enemy.NotifyAbilityTriggered(this);
        }
        else if (effect == EnemyAbilityEffect.HealAfterPlayerCard && amount > 0 && enemy.currentHp < enemy.maxHp)
        {
            enemy.Heal(amount);
            enemy.NotifyAbilityTriggered(this);
        }
    }
}
