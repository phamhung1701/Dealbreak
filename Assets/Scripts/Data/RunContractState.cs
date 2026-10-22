using System;

public enum RunContractObjective
{
    WinTwoEncounters,
    DefeatEliteThisMap,
    WinCombatWithoutHpLoss
}

[Serializable]
public sealed class RunContractState
{
    public RunContractObjective objective;
    public int startedAtMapIndex;
    public int startedAtNodeId;
    public int deadlineMapExclusive;
    public int encounterWins;
    public string stableId;

    public string ObjectiveDescription => objective switch
    {
        RunContractObjective.WinTwoEncounters => $"Win 2 encounters within {RunLength.Districts} Districts.",
        RunContractObjective.DefeatEliteThisMap => "Defeat an Enforcer before leaving this District.",
        RunContractObjective.WinCombatWithoutHpLoss => "Win 1 encounter without losing HP.",
        _ => "Complete the contract objective."
    };
}
