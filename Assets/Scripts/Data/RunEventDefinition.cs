using System;
using UnityEngine;

public enum RunEffectType
{
    GainCredits,
    LoseCredits,
    Heal,
    LoseHealth,
    GainMaxHealth,
    DrawCards,
    RevealMapNode,
    GainConsumable,
    RandomGearEnhancementOrNothing,
    // A penalty, not a payment: takes up to `amount` Credits and never blocks the choice.
    LoseCreditsUpTo,
    // A penalty, not a payment: takes up to `amount` HP, never below 1, and never blocks the choice.
    LoseHealthNonLethal
}

[Serializable]
public struct RunEffect
{
    public RunEffectType type;
    [Min(0)] public int amount;
    public string consumableId;
}

public enum RunEventChoiceInteraction
{
    Immediate = 0,
    ChooseEnhancementTarget = 1,
    DiscardTwoForRandomEnhancement = 2,
    DiscardCardsForTotalValue = 3,
    HammerRetry = 4,
    HammerDecline = 5
}

[Serializable]
public class RunEventChoice
{
    public string label;
    [TextArea] public string description;
    public RunEffect[] effects;
    public RunEventChoiceInteraction interaction;
    [Min(0)] public int requiredDiscardValue;
}

// How an event's choices resolve. Authored choices apply their RunEffects; the other flows are
// RunManager-owned sequences with their own state. Dispatch on this, never on an event's id.
public enum RunEventFlow
{
    Authored = 0,
    BloodRitual = 1,
    BloodRitualReplacement = 2,
    ContractSelection = 3,
    HammerReward = 4
}

[CreateAssetMenu(fileName = "RunEvent", menuName = "Game/Incident")]
public class RunEventDefinition : ScriptableObject
{
    public string id;
    public MapNodeType category = MapNodeType.Event;
    [Tooltip("Authored for ordinary Incidents. Blood Ritual marks the rare Risk with its own reward flow.")]
    public RunEventFlow flow = RunEventFlow.Authored;
    public string title;
    [TextArea] public string description;
    public RunEventChoice[] choices;
}
