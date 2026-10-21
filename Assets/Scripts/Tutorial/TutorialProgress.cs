using UnityEngine;

/// <summary>
/// Whether this player has finished or skipped the tutorial. Stored per machine in PlayerPrefs,
/// never in the run save: until it is set, NEW RUN starts the tutorial instead of a run.
/// </summary>
public static class TutorialProgress
{
    const string CompletedKey = "Dealbreak.Tutorial.Completed";

    public static bool Completed => PlayerPrefs.GetInt(CompletedKey, 0) != 0;

    public static void MarkCompleted()
    {
        PlayerPrefs.SetInt(CompletedKey, 1);
        PlayerPrefs.Save();
    }

    public static void Reset()
    {
        PlayerPrefs.DeleteKey(CompletedKey);
        PlayerPrefs.Save();
    }
}
