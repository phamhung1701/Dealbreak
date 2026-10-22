// Typed GameplayEffectDefinition builders for tests that author Gear or Mods in code.
static class TestEffects
{
    public static GameplayEffectDefinition Effect(GameplayEffectKind kind, GameplayEffectTrigger trigger,
        int amount, params GameplayEffectCondition[] conditions) =>
        new() { kind = kind, trigger = trigger, amount = amount, conditions = conditions };

    public static GameplayEffectDefinition FlatAttack(int amount, params GameplayEffectCondition[] conditions) =>
        Effect(GameplayEffectKind.FlatAttack, GameplayEffectTrigger.AttackCalculated, amount, conditions);

    public static GameplayEffectDefinition AttackMultiplier(int amount, params GameplayEffectCondition[] conditions) =>
        Effect(GameplayEffectKind.AttackMultiplier, GameplayEffectTrigger.AttackCalculated, amount, conditions);

    public static GameplayEffectDefinition FlatBlock(int amount, params GameplayEffectCondition[] conditions) =>
        Effect(GameplayEffectKind.FlatBlock, GameplayEffectTrigger.DefenseCalculated, amount, conditions);

    public static GameplayEffectDefinition HealOnPlay(int amount) =>
        Effect(GameplayEffectKind.Heal, GameplayEffectTrigger.CardCommitted, amount);

    public static GameplayEffectDefinition DrawOnPlay(int amount) =>
        Effect(GameplayEffectKind.Draw, GameplayEffectTrigger.CardCommitted, amount);

    public static GameplayEffectDefinition ReduceAttackByCardValue(params GameplayEffectCondition[] conditions) =>
        Effect(GameplayEffectKind.ReduceAttackByCardValue, GameplayEffectTrigger.CardCommitted, 1, conditions);

    public static GameplayEffectDefinition HealAfterVictory(int amount) =>
        Effect(GameplayEffectKind.Heal, GameplayEffectTrigger.EncounterWon, amount);

    public static GameplayEffectCondition Suit(CardData.Suit suit) =>
        new() { kind = GameplayConditionKind.CardSuit, suit = suit };

    public static GameplayEffectCondition Enhanced =>
        new() { kind = GameplayConditionKind.EnhancedCard, expected = true };
}
