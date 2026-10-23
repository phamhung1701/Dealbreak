using System;
using System.Collections.Generic;

public static class RunEffectResolver
{
    public static bool CanApply(RunEventChoice choice, CardManager cards, CombatManager combat) =>
        CanApply(choice, cards, combat, null);

    public static bool CanApply(RunEventChoice choice, CardManager cards, CombatManager combat, RunManager run)
    {
        return string.IsNullOrEmpty(GetFailureReason(choice, cards, combat, run));
    }

    public static string GetFailureReason(RunEventChoice choice, CardManager cards, CombatManager combat) =>
        GetFailureReason(choice, cards, combat, null);

    public static string GetFailureReason(RunEventChoice choice, CardManager cards, CombatManager combat, RunManager run)
    {
        if (choice == null || cards == null || combat == null) return "Choice unavailable";

        bool infiniteCredits = cards.HasInfiniteMoney;
        bool infiniteHealth = combat.HasInfiniteHealthForDev;
        int simulatedCredits = infiniteCredits ? int.MaxValue : cards.credits;
        int simulatedHealth = infiniteHealth ? int.MaxValue : combat.player.currentHealth;
        int simulatedMaxHealth = infiniteHealth ? int.MaxValue : combat.player.maxHealth;
        // Validates the whole choice first, so Apply never stops part-way with effects already granted.
        var consumableRequests = new List<(ConsumableData, int)>();
        foreach (var effect in choice.effects ?? Array.Empty<RunEffect>())
        {
            switch (effect.type)
            {
                case RunEffectType.GainCredits:
                    if (!infiniteCredits) simulatedCredits += effect.amount;
                    break;
                case RunEffectType.LoseCredits:
                    if (!infiniteCredits && simulatedCredits < effect.amount)
                        return $"Need {effect.amount} Credits (have {simulatedCredits} Credits)";
                    if (!infiniteCredits) simulatedCredits -= effect.amount;
                    break;
                case RunEffectType.LoseCreditsUpTo:
                    if (!infiniteCredits) simulatedCredits -= Math.Min(simulatedCredits, effect.amount);
                    break;
                case RunEffectType.Heal:
                    if (!infiniteHealth) simulatedHealth = Math.Min(simulatedMaxHealth, simulatedHealth + effect.amount);
                    break;
                case RunEffectType.LoseHealth:
                    if (!infiniteHealth && simulatedHealth <= effect.amount)
                        return $"Requires more than {effect.amount} HP";
                    if (!infiniteHealth) simulatedHealth -= effect.amount;
                    break;
                case RunEffectType.GainMaxHealth:
                    if (!infiniteHealth)
                    {
                        simulatedMaxHealth += effect.amount;
                        simulatedHealth += effect.amount;
                    }
                    break;
                case RunEffectType.RevealMapNode:
                    if (run == null || !run.CanRevealEligibleHiddenNode())
                        return "No eligible hidden node to reveal";
                    break;
                case RunEffectType.GainConsumable:
                    var consumable = cards.FindConsumable(effect.consumableId);
                    if (consumable == null || !consumable.eventAvailable) return "Trick unavailable";
                    consumableRequests.Add((consumable, Math.Max(1, effect.amount)));
                    if (!cards.CanAddConsumables(consumableRequests))
                        return "Backpack Full";
                    break;
                case RunEffectType.RandomGearEnhancementOrNothing:
                    if (run == null) return "Reward unavailable";
                    break;
            }
        }
        return string.Empty;
    }

    public static bool Apply(RunEventChoice choice, CardManager cards, CombatManager combat) =>
        Apply(choice, cards, combat, null);

    public static bool Apply(RunEventChoice choice, CardManager cards, CombatManager combat, RunManager run)
    {
        if (!CanApply(choice, cards, combat, run)) return false;

        foreach (var effect in choice.effects ?? Array.Empty<RunEffect>())
        {
            switch (effect.type)
            {
                case RunEffectType.GainCredits:
                    cards.AddCredits(effect.amount);
                    break;
                case RunEffectType.LoseCredits:
                    cards.SpendCredits(effect.amount);
                    break;
                case RunEffectType.LoseCreditsUpTo:
                    if (!cards.HasInfiniteMoney) cards.SpendCredits(Math.Min(cards.credits, effect.amount));
                    break;
                case RunEffectType.Heal:
                    combat.HealPlayer(effect.amount);
                    break;
                case RunEffectType.LoseHealth:
                    combat.TakeRunDamage(effect.amount);
                    break;
                case RunEffectType.GainMaxHealth:
                    combat.IncreasePlayerMaxHealth(effect.amount);
                    break;
                case RunEffectType.DrawCards:
                    cards.DrawToHand(effect.amount);
                    break;
                case RunEffectType.RevealMapNode:
                    if (run == null || !run.TryRevealHiddenNodeFromEvent()) return false;
                    break;
                case RunEffectType.GainConsumable:
                    var consumable = cards.FindConsumable(effect.consumableId);
                    if (consumable == null || !cards.AddConsumable(consumable, Math.Max(1, effect.amount)))
                        return false;
                    break;
                case RunEffectType.RandomGearEnhancementOrNothing:
                    if (run == null || !run.TryGrantRandomSwampReward()) return false;
                    break;
            }
        }
        return true;
    }
}
