using System.Collections.Generic;

/// <summary>
/// The UI's single route for attack and block commands: through the attack presentation when it
/// exists (it commits first, then animates snapshots), otherwise straight to combat. A running
/// tutorial step may refuse a command first (TutorialGate).
/// </summary>
public static class CombatCommands
{
    public static bool Attack(IReadOnlyList<CardView> selection, EnemyRuntime target, CardView draggedCard = null)
    {
        if (!TutorialGate.Allows(TutorialCommand.Attack, selection, target)) return false;
        var presentation = AttackCardPresentationUI.Instance;
        if (presentation != null) return presentation.TryPlayCards(selection, target, draggedCard);
        var combat = CombatManager.Instance;
        return combat != null && combat.TryPlayCards(selection, target);
    }

    public static bool Block(IReadOnlyList<CardView> selection, EnemyRuntime target)
    {
        if (!TutorialGate.Allows(TutorialCommand.Block, selection, target)) return false;
        var presentation = AttackCardPresentationUI.Instance;
        if (presentation != null) return presentation.TryDefendCards(selection, target);
        var combat = CombatManager.Instance;
        return combat != null && combat.TryDefendWithCards(selection, target);
    }
}
