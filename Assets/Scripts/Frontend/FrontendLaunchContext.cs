public enum MainMenuDestination
{
    Home,
    RunSetup
}

public static class FrontendLaunchContext
{
    static RunSaveData _pendingContinue;
    static string _pendingRunSeed;
    static string _pendingRunCharacter;
    static bool _hasPendingRun;
    static bool _pendingTutorial;
    static MainMenuDestination _pendingMenuDestination;
    static bool _hasPendingMenuDestination;

    public static void RequestContinue(RunSaveData data)
    {
        _pendingRunSeed = null; _pendingRunCharacter = null; _hasPendingRun = false; _pendingTutorial = false;
        _pendingContinue = data;
    }

    public static bool TryConsumeContinue(out RunSaveData data)
    {
        data = _pendingContinue; _pendingContinue = null;
        return data != null;
    }

    /// <param name="characterId">The picked <see cref="CharacterData.id"/>; null or empty for none.</param>
    public static void RequestRun(string seed, string characterId = null)
    {
        _pendingContinue = null;
        _pendingTutorial = false;
        _pendingRunSeed = RunRandomContext.NormalizeSeed(seed);
        _pendingRunCharacter = characterId;
        _hasPendingRun = true;
    }

    public static bool TryConsumeRunSeed(out string seed) => TryConsumeRunSeed(out seed, out _);

    public static bool TryConsumeRunSeed(out string seed, out string characterId)
    {
        seed = _pendingRunSeed;
        characterId = _pendingRunCharacter;
        bool hadRequest = _hasPendingRun;
        _pendingRunSeed = null;
        _pendingRunCharacter = null;
        _hasPendingRun = false;
        return hadRequest;
    }

    public static void RequestTutorial()
    {
        _pendingContinue = null;
        _pendingRunSeed = null;
        _pendingRunCharacter = null;
        _hasPendingRun = false;
        _pendingTutorial = true;
    }

    /// <summary>Whether the Game scene is loading for the tutorial; read before the run starts.</summary>
    public static bool HasPendingTutorial => _pendingTutorial;

    public static bool TryConsumeTutorial()
    {
        bool requested = _pendingTutorial;
        _pendingTutorial = false;
        return requested;
    }

    public static void RequestMainMenu(MainMenuDestination destination)
    {
        _pendingMenuDestination = destination;
        _hasPendingMenuDestination = true;
    }

    public static MainMenuDestination ConsumeMainMenuDestination()
    {
        var destination = _hasPendingMenuDestination ? _pendingMenuDestination : MainMenuDestination.Home;
        _hasPendingMenuDestination = false;
        _pendingMenuDestination = MainMenuDestination.Home;
        return destination;
    }

    public static void Clear()
    {
        _pendingContinue = null;
        _pendingRunSeed = null;
        _pendingRunCharacter = null;
        _hasPendingRun = false;
        _pendingTutorial = false;
        _pendingMenuDestination = MainMenuDestination.Home;
        _hasPendingMenuDestination = false;
    }
}
