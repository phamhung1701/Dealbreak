using System;
using System.Collections.Generic;

/// <summary>What a Mod or Gear just did, for its trigger animation.</summary>
public enum EffectCue
{
    Credits,
    Heal,
    HealthCost,
    Draw,
    AttackBonus,
    AttackMultiplier,
    BlockBonus,
    BlockMultiplier,
    DamageReduction,
    Counter,
    Critical,
    DoubleStrike,
    AreaDamage,
    Growth,
    Weaken,
    Recycle,
    Destroy,
    Shield,
    Block,
    Retrigger,
    Save,
    ReturnToHand,
    MaxHealth,
    Carry,
    CoverFire
}

/// <summary>One Mod or Gear activation: its source (a card's Mod or an owned Gear), what it did and how much.</summary>
public readonly struct EffectTrigger
{
    public CardInstance Card { get; }
    public GearRuntimeInstance Gear { get; }
    public EffectCue Cue { get; }
    public int Amount { get; }

    public EffectTrigger(CardInstance card, GearRuntimeInstance gear, EffectCue cue, int amount)
    {
        Card = card;
        Gear = gear;
        Cue = cue;
        Amount = amount;
    }
}

/// <summary>
/// Presentation feed: gameplay reports each Mod or Gear activation as it executes, so the UI can play
/// its trigger animation. Gameplay never reads it, and previews never report to it.
/// </summary>
public static class EffectTriggerFeed
{
    public static event Action<EffectTrigger> Triggered;

    public static void Report(GameplayEffectSource source, EffectCue cue, int amount = 0)
    {
        if (source.Gear != null) Report(source.Gear, cue, amount);
        else if (source.Card != null) Report(source.Card, cue, amount);
    }

    public static void Report(GearRuntimeInstance gear, EffectCue cue, int amount = 0)
    {
        if (gear != null) Triggered?.Invoke(new EffectTrigger(null, gear, cue, amount));
    }

    public static void Report(CardInstance card, EffectCue cue, int amount = 0)
    {
        if (card != null) Triggered?.Invoke(new EffectTrigger(card, null, cue, amount));
    }
}

/// <summary>
/// Collects the Mods and Gear that changed one value while it is calculated on the execution path,
/// then reports them in calculation order. Previews pass no trace.
/// </summary>
public sealed class EffectTriggerTrace
{
    readonly List<EffectTrigger> _entries = new();

    public int Count => _entries.Count;

    public void Add(GameplayEffectSource source, EffectCue cue, int amount)
    {
        if (source.Gear != null) Add(source.Gear, cue, amount);
        else if (source.Card != null) _entries.Add(new EffectTrigger(source.Card, null, cue, amount));
    }

    public void Add(GearRuntimeInstance gear, EffectCue cue, int amount)
    {
        if (gear != null) _entries.Add(new EffectTrigger(null, gear, cue, amount));
    }

    public void Add(CardInstance card, EffectCue cue, int amount)
    {
        if (card != null) _entries.Add(new EffectTrigger(card, null, cue, amount));
    }

    public void Publish()
    {
        foreach (var entry in _entries)
        {
            if (entry.Gear != null) EffectTriggerFeed.Report(entry.Gear, entry.Cue, entry.Amount);
            else EffectTriggerFeed.Report(entry.Card, entry.Cue, entry.Amount);
        }
        _entries.Clear();
    }
}
