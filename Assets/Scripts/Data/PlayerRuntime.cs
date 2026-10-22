using System;

public sealed class PlayerRuntime : ICombatDamageTarget
{
    public const int DefaultMaxHealth = 30;

    public int maxHealth { get; private set; }
    public int currentHealth { get; private set; }
    public int shieldCharges { get; private set; }
    public int encounterBlock { get; private set; }
    public Func<bool> HealingAllowed { get; set; }

    public string CombatDisplayName => "Player";
    public int CurrentHealth => currentHealth;
    public int ShieldCharges => shieldCharges;
    public int EncounterBlock => encounterBlock;
    public bool IsDefeated => currentHealth <= 0;

    public PlayerRuntime(int maxHealth = DefaultMaxHealth)
    {
        ValidateMaxHealth(maxHealth);
        this.maxHealth = maxHealth;
        currentHealth = maxHealth;
    }

    public void Configure(int maxHealth)
    {
        ValidateMaxHealth(maxHealth);
        this.maxHealth = maxHealth;
        currentHealth = Math.Min(currentHealth, maxHealth);
    }

    public void RestorePersistentState(int maximum, int health, int shield, int block)
    {
        ValidateMaxHealth(maximum);
        if (health <= 0 || health > maximum || shield < 0 || block < 0)
            throw new ArgumentException("Invalid saved player state.");
        maxHealth = maximum;
        currentHealth = health;
        shieldCharges = shield;
        encounterBlock = block;
    }

    public void Reset()
    {
        currentHealth = maxHealth;
        ResetShield();
        ResetEncounterBlock();
    }

    public int ModifyIncomingCombatDamage(DamageRequest request, CombatManager context, int damage)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevModeRuntime.InfiniteHealth) return 0;
#endif
        // Run/Event HP costs use TakeDamage directly and never enter this combat pipeline.
        if (context == null || request.Origin is not (
                CombatDamageOrigin.Card or CombatDamageOrigin.EnemyAggregate or
                CombatDamageOrigin.Recovery or CombatDamageOrigin.Reactive))
            return Math.Max(0, damage);
        int modified = GameplayEffectResolver.ModifyIncomingCombatDamage(
            new IncomingDamageEffectContext(request, context, CardManager.Instance, damage));
        int damageTaken = request.Origin is CombatDamageOrigin.EnemyAggregate or CombatDamageOrigin.Recovery
            ? ConsumeEncounterBlock(modified) : modified;
        return context.TryNegateLethalHit(request, damageTaken) ? 0 : damageTaken;
    }

    public int ApplyResolvedCombatDamage(int amount) => TakeDamage(amount);

    public int GainShield(int amount)
    {
        if (amount <= 0 || IsDefeated) return 0;
        int previous = shieldCharges;
        shieldCharges = (int)Math.Min(int.MaxValue, (long)shieldCharges + amount);
        return shieldCharges - previous;
    }

    public bool TryConsumeShield()
    {
        if (shieldCharges <= 0) return false;
        shieldCharges--;
        return true;
    }

    public void ResetShield()
    {
        shieldCharges = 0;
    }

    public int GainEncounterBlock(int amount)
    {
        if (amount <= 0 || IsDefeated) return 0;
        int previous = encounterBlock;
        encounterBlock = (int)Math.Min(int.MaxValue, (long)encounterBlock + amount);
        return encounterBlock - previous;
    }

    public int ConsumeEncounterBlock(int amount)
    {
        if (amount <= 0 || encounterBlock <= 0) return Math.Max(0, amount);
        int absorbed = Math.Min(encounterBlock, amount);
        encounterBlock -= absorbed;
        return amount - absorbed;
    }

    public void ResetEncounterBlock()
    {
        encounterBlock = 0;
    }

    public int TakeDamage(int amount)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevModeRuntime.InfiniteHealth) return 0;
#endif
        if (amount <= 0 || IsDefeated)
            return 0;

        int previousHealth = currentHealth;
        currentHealth = Math.Max(0, currentHealth - amount);
        return previousHealth - currentHealth;
    }

    public int Heal(int amount)
    {
        if (amount <= 0 || IsDefeated || HealingAllowed != null && !HealingAllowed())
            return 0;

        int previousHealth = currentHealth;
        currentHealth = (int)Math.Min(maxHealth, (long)currentHealth + amount);
        return currentHealth - previousHealth;
    }

    public int SetMaxHealthAndClamp(int value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        int previous = maxHealth;
        maxHealth = value;
        currentHealth = Math.Min(currentHealth, maxHealth);
        return maxHealth - previous;
    }

    public int IncreaseMaxHealth(int amount, bool healIncrease = true)
    {
        if (amount <= 0) return 0;

        long requestedMax = (long)maxHealth + amount;
        int nextMax = (int)Math.Min(int.MaxValue, requestedMax);
        int actualIncrease = nextMax - maxHealth;
        maxHealth = nextMax;
        if (healIncrease && actualIncrease > 0 && (HealingAllowed == null || HealingAllowed()))
            currentHealth = (int)Math.Min(maxHealth, (long)currentHealth + actualIncrease);
        return actualIncrease;
    }

    static void ValidateMaxHealth(int maxHealth)
    {
        if (maxHealth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHealth), "Maximum health must be greater than zero.");
    }
}
