using System;
using System.Collections.Generic;

public enum TutorialCommand { Attack, Block, Pass }

/// <summary>
/// Lets the running tutorial step refuse a player command it is not teaching, such as a Block
/// that is not a Perfect Block while the Perfect Block lesson is up. Checked by the UI command
/// routes (CombatCommands, PASS) before anything reaches combat. With no tutorial, everything passes.
/// </summary>
public static class TutorialGate
{
    public delegate bool CommandFilter(TutorialCommand command, IReadOnlyList<CardInstance> cards, EnemyRuntime target);

    static CommandFilter _filter;

    /// <summary>Raised when a command is refused, so the coach can point back at the step.</summary>
    public static event Action<TutorialCommand> Refused;

    public static void SetFilter(CommandFilter filter) => _filter = filter;
    public static void Clear() => _filter = null;

    public static bool Allows(TutorialCommand command, IReadOnlyList<CardInstance> cards = null, EnemyRuntime target = null)
    {
        if (_filter == null || _filter(command, cards ?? Array.Empty<CardInstance>(), target)) return true;
        Refused?.Invoke(command);
        return false;
    }

    public static bool Allows(TutorialCommand command, IReadOnlyList<CardView> views, EnemyRuntime target)
    {
        if (_filter == null) return true;
        var cards = new List<CardInstance>(views?.Count ?? 0);
        if (views != null) foreach (var view in views) if (view != null && view.data != null) cards.Add(view.data);
        return Allows(command, cards, target);
    }
}
