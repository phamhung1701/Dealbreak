using System.Collections.Generic;

/// <summary>
/// The UI's single route for attack and block commands: through the attack presentation when it
/// exists (it commits first, then animates snapshots), otherwise straight to combat.
/// </summary>
public static class CombatCommands
{
    public static bool Attack(IReadOnlyList<CardView> selection, EnemyRuntime target, CardView draggedCard = null)
    {
        var presentation = AttackCardPresentationUI.Instance;
        if (presentation != null) return presentation.TryPlayCards(selection, target, draggedCard);
        var combat = CombatManager.Instance;
        return combat != null && combat.TryPlayCards(selection, target);
    }

    public static bool Block(IReadOnlyList<CardView> selection, EnemyRuntime target)
    {
        var presentation = AttackCardPresentationUI.Instance;
        if (presentation != null) return presentation.TryDefendCards(selection, target);
        var combat = CombatManager.Instance;
        return combat != null && combat.TryDefendWithCards(selection, target);
    }
}
