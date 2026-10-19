using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyRuntime : ICombatDamageTarget
{
    public EnemyTypeData type;
    public int maxHp;
    public int currentHp;
    public int currentAttack;
    public int ShieldCharges { get; private set; }
    public int PlayerTurnsCompleted { get; private set; }

    readonly int _instanceNumber;
    readonly int _encounterSize;
    readonly bool _alternatesChargedAttack;
    int _responsesPrepared;
    bool _currentResponseIsCharged;
    int _regenerationActivations;
    readonly Dictionary<EnemyRuntime, int> _warDrumBonuses = new();
    bool _guardedReady;
    bool _guardedHitConsumed;
    bool _duelistCallConsumed;
    bool _masterThiefHasStolen;
    int _stolenCredits;
    CardData.Suit _announcedSuit;
    EnemyRuntime _captain;
    public bool HasCaptaincy { get; }
    public bool HasRoyalGuard { get; }
    public bool HasDoubleStrike => HasAbility(EnemyAbilityEffect.DoubleStrike);
    public bool HasSilence => HasAbility(EnemyAbilityEffect.Silence) && (long)currentHp * 2L > maxHp;
    public bool HasWithering => HasAbility(EnemyAbilityEffect.Withering) && (long)currentHp * 2L > maxHp;
    public bool HasOppression => HasAbility(EnemyAbilityEffect.Oppression);
    public bool HasSuitCall => HasAbility(EnemyAbilityEffect.SuitCall);
    public bool HasWarDrum => HasAbility(EnemyAbilityEffect.WarDrum);
    public bool HasMasterThief => HasAbility(EnemyAbilityEffect.MasterThief);
    public bool RequiresFullCoverageBlock => HasAbility(EnemyAbilityEffect.FullCoverageBlock);
    public int StolenCredits => _stolenCredits;
    public CardData.Suit AnnouncedSuit => _announcedSuit;
    public bool DuelistCallConsumed => _duelistCallConsumed;
    public int PreparedResponseBudget { get; private set; }
    public int CaptainAttackBonus => _captain != null && !_captain.IsDefeated ? 1 : 0;
    public int EffectiveAttack => (int)Math.Min(int.MaxValue,
        Math.Max(0L, (long)currentAttack + CaptainAttackBonus));
    public int GetWarDrumBonus(EnemyRuntime ally) =>
        ally != null && _warDrumBonuses.TryGetValue(ally, out int bonus) ? bonus : 0;
    public int PreparedCaptainAttackBonus { get; private set; }

    public event Action OnHpChanged;
    public event Action OnAttackChanged;

    public EnemyRuntime(EnemyTypeData enemyType, int instanceNumber = 1, int encounterSize = 1,
        int? startingHp = null, int? startingAttack = null)
    {
        type = enemyType != null ? enemyType : throw new ArgumentNullException(nameof(enemyType));
        maxHp = startingHp ?? enemyType.maxHp;
        currentHp = maxHp;
        currentAttack = startingAttack ?? enemyType.baseAttack;
        _instanceNumber = Mathf.Max(1, instanceNumber);
        _encounterSize = Mathf.Max(1, encounterSize);
        HasCaptaincy = HasAbility(EnemyAbilityEffect.Captaincy);
        HasRoyalGuard = HasAbility(EnemyAbilityEffect.RoyalGuard);
        _guardedReady = HasAbility(EnemyAbilityEffect.Guarded);
        _alternatesChargedAttack = HasRoyalGuard || HasAbility(EnemyAbilityEffect.AlternateChargedAttack) ||
            HasAbility(EnemyAbilityEffect.HeavySwing);
    }

    public bool HasAbilityEffect(EnemyAbilityEffect effect) => HasAbility(effect);

    bool HasAbility(EnemyAbilityEffect effect) => type.abilities != null &&
        type.abilities.Any(ability => ability != null && ability.effect == effect);

    public void SetAnnouncedSuit(CardData.Suit suit)
    {
        _announcedSuit = suit;
        _duelistCallConsumed = false;
    }

    /// <summary>Suit Call: each player turn announces a seeded suit whose next hit is halved.</summary>
    public CardData.Suit AnnounceSuit(IRandomSource random)
    {
        var suit = (CardData.Suit)random.NextInt(0, Enum.GetValues(typeof(CardData.Suit)).Length);
        SetAnnouncedSuit(suit);
        return suit;
    }

    /// <summary>Master Thief: the most Credits one steal takes, from the ability's authored amount.</summary>
    public int MasterThiefStealLimit
    {
        get
        {
            if (type.abilities != null)
                foreach (var ability in type.abilities)
                    if (ability != null && ability.effect == EnemyAbilityEffect.MasterThief)
                        return Mathf.Max(0, ability.amount);
            return 0;
        }
    }

    public bool IsSupportedBy(EnemyRuntime captain) => ReferenceEquals(_captain, captain);

    public void AssignCaptain(EnemyRuntime captain)
    {
        _captain = captain;
        OnAttackChanged?.Invoke();
    }

    public bool NextAttackIsCharged => _alternatesChargedAttack && (_responsesPrepared & 1) == 1;
    public bool CurrentResponseIsCharged => _currentResponseIsCharged;
    public int ResponseAttack
    {
        get
        {
            float multiplier = 1f;
            if (HasAbility(EnemyAbilityEffect.Desperation) && (long)currentHp * 2L < maxHp)
                multiplier *= 1.25f;
            if (_currentResponseIsCharged)
                multiplier *= HasAbility(EnemyAbilityEffect.HeavySwing) ? 1.5f : 2f;
            return RoundHalfUp(EffectiveAttack * multiplier);
        }
    }

    static int RoundHalfUp(float value)
    {
        if (float.IsNaN(value) || value <= 0f) return 0;
        if (float.IsInfinity(value) || value >= int.MaxValue) return int.MaxValue;
        return Mathf.FloorToInt(value + 0.5f);
    }

    public int PrepareResponseAttack(int heldCardCount = 0)
    {
        _currentResponseIsCharged = NextAttackIsCharged;
        PreparedCaptainAttackBonus = CaptainAttackBonus;
        if (HasRoyalGuard && !_currentResponseIsCharged)
            GainShield(1);
        _responsesPrepared++;
        int budget = ResponseAttack;
        if (HasOppression)
        {
            long oppressed = (long)budget + (long)Mathf.Max(0, heldCardCount - 3) * 2L;
            budget = (int)Math.Min(int.MaxValue, oppressed);
        }
        PreparedResponseBudget = budget;
        return budget;
    }

    public int[] PrepareResponseHits(int heldCardCount)
    {
        int budget = PrepareResponseAttack(heldCardCount);
        if (!HasDoubleStrike) return new[] { budget };
        int hitDamage = RoundHalfUp(budget * 0.6f);
        return new[] { hitDamage, hitDamage };
    }

    public bool TryBeginMasterThiefSteal()
    {
        if (!HasMasterThief || _masterThiefHasStolen || IsDefeated) return false;
        _masterThiefHasStolen = true;
        return true;
    }

    public void SetStolenCredits(int amount) => _stolenCredits = Mathf.Max(0, amount);

    public int ClaimStolenCredits()
    {
        int amount = _stolenCredits;
        _stolenCredits = 0;
        return amount;
    }

    public void CompleteEnemyResponse(IReadOnlyList<EnemyRuntime> enemies = null)
    {
        if (IsDefeated) return;
        _guardedReady = HasAbility(EnemyAbilityEffect.Guarded);
        _guardedHitConsumed = false;
        if (HasWarDrum && enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                var ally = enemies[i];
                if (ally == null || ally.IsDefeated || ReferenceEquals(ally, this)) continue;
                int currentBonus = GetWarDrumBonus(ally);
                if (currentBonus >= 3) continue;
                ally.IncreaseAttack(1);
                _warDrumBonuses[ally] = currentBonus + 1;
            }
        }
        if (!HasAbility(EnemyAbilityEffect.Regeneration) || _regenerationActivations >= 3) return;
        int heal = RoundHalfUp(maxHp * 0.05f);
        if (heal <= 0 || currentHp >= maxHp) return;
        Heal(heal);
        _regenerationActivations++;
    }

    public void ResetResponseIntent()
    {
        _responsesPrepared = 0;
        _currentResponseIsCharged = false;
        PreparedCaptainAttackBonus = 0;
        PreparedResponseBudget = 0;
        _regenerationActivations = 0;
        _warDrumBonuses.Clear();
        _duelistCallConsumed = false;
        _guardedReady = HasAbility(EnemyAbilityEffect.Guarded);
        _guardedHitConsumed = false;
    }

    public string DisplayName => _encounterSize > 1 ? $"{type.enemyName} {_instanceNumber}" : type.enemyName;
    public string CombatDisplayName => DisplayName;
    public int CurrentHealth => currentHp;
    public int CreditsReward => type.creditsReward;
    public bool IsDefeated => currentHp <= 0;
    public bool ShouldFlee => !IsDefeated && type.fleeAfterPlayerTurns > 0 &&
        PlayerTurnsCompleted >= type.fleeAfterPlayerTurns;
    public string AbilitySummary => type.abilities == null
        ? string.Empty
        : string.Join("  •  ", type.abilities.Where(ability => ability != null).Select(ability => ability.displayName));

    public int ModifyIncomingCombatDamage(DamageRequest request, CombatManager context, int damage)
    {
        int resolvedDamage = Mathf.Max(0, damage);
        if (type.abilities != null)
        {
            for (int i = 0; i < type.abilities.Length; i++)
            {
                var ability = type.abilities[i];
                if (ability != null)
                    resolvedDamage = ability.ModifyIncomingDamage(this, context, request, resolvedDamage);
            }
        }
        // Headliner immunity is based on the Headliner's source-card suit and the action's existing
        // first-card suit snapshot. Multi-card actions therefore use their first selected card.
        // Apply it after incoming modifiers but before Guarded so immune hits consume neither Guarded nor Shield.
        if (request.Origin == CombatDamageOrigin.Card && type.sourceCard != null &&
            request.Action.SuitSnapshot.HasValue &&
            request.Action.SuitSnapshot.Value == type.sourceCard.suit)
            resolvedDamage = 0;

        CardData.Suit? hitSuit = request.HitCard != null ? request.HitCard.Suit : request.Action.SuitSnapshot;
        if (request.Origin == CombatDamageOrigin.Card && HasSuitCall && !_duelistCallConsumed &&
            hitSuit.HasValue && hitSuit.Value == _announcedSuit)
        {
            resolvedDamage = RoundHalfUp(resolvedDamage * 0.5f);
            _duelistCallConsumed = true;
        }

        if (_guardedReady && !_guardedHitConsumed && resolvedDamage > 0)
        {
            resolvedDamage = RoundHalfUp(resolvedDamage * 0.5f);
            _guardedHitConsumed = true;
            _guardedReady = false;
        }
        return Mathf.Max(0, resolvedDamage);
    }

    public int ApplyResolvedCombatDamage(int amount)
    {
        int previousHp = currentHp;
        currentHp = Mathf.Max(0, currentHp - Mathf.Max(0, amount));
        OnHpChanged?.Invoke();
        return previousHp - currentHp;
    }

    public void DefeatInstantly()
    {
        if (IsDefeated) return;
        currentHp = 0;
        OnHpChanged?.Invoke();
    }

    public int GainShield(int amount)
    {
        if (amount <= 0 || IsDefeated) return 0;
        int previous = ShieldCharges;
        ShieldCharges = (int)Math.Min(int.MaxValue, (long)ShieldCharges + amount);
        OnHpChanged?.Invoke();
        return ShieldCharges - previous;
    }

    public bool TryConsumeShield()
    {
        if (ShieldCharges <= 0) return false;
        ShieldCharges--;
        OnHpChanged?.Invoke();
        return true;
    }

    public void ResetShield()
    {
        ShieldCharges = 0;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDefeated) return;
        currentHp = (int)Math.Min(maxHp, (long)currentHp + amount);
        OnHpChanged?.Invoke();
    }

    public void ReduceAttack(int amount)
    {
        currentAttack = (int)Math.Max(0L, (long)currentAttack - amount);
        OnAttackChanged?.Invoke();
    }

    public void IncreaseAttack(int amount)
    {
        if (amount <= 0) return;
        currentAttack = (int)Math.Min(int.MaxValue, (long)currentAttack + amount);
        OnAttackChanged?.Invoke();
    }

    public void NotifyPlayerTurnCompleted()
    {
        if (!IsDefeated && PlayerTurnsCompleted < int.MaxValue)
            PlayerTurnsCompleted++;
    }

}
