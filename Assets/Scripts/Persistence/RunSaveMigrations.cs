using System;
using System.Collections.Generic;

/// <summary>
/// Upgrades older run saves to the current schema before validation. Each step converts one
/// version to the next in place. Saves from a newer build are rejected, never guessed at.
/// When the schema changes: bump RunSaveValidation.Version, add the step for the previous
/// version here, and keep a fixture save of that version in the tests.
/// </summary>
public static class RunSaveMigrations
{
    // [N] upgrades a version-N payload to version N + 1.
    static readonly Dictionary<int, Action<RunSaveData>> Steps = new();

    public static bool TryUpgrade(RunSaveData data, out string error) => TryUpgrade(data, Steps, out error);

    internal static bool TryUpgrade(RunSaveData data, IReadOnlyDictionary<int, Action<RunSaveData>> steps,
        out string error)
    {
        if (data == null) { error = "Missing save data."; return false; }
        if (data.version > RunSaveValidation.Version)
        {
            error = $"Save version {data.version} is newer than this build supports.";
            return false;
        }
        while (data.version < RunSaveValidation.Version)
        {
            if (!steps.TryGetValue(data.version, out var step))
            {
                error = $"No migration from save version {data.version}.";
                return false;
            }
            step(data);
            data.version++;
        }
        error = null;
        return true;
    }
}
