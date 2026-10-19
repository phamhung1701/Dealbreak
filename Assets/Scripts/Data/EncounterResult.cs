public enum EncounterResult
{
    Victory,
    Defeat,
    // A combat rule fault (reaction runaway) halted the encounter. The run is not lost:
    // no rewards are granted and the last safe checkpoint is kept for Continue.
    Aborted
}
