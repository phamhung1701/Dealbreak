/// <summary>
/// The motion preferences the Cut Sheet presenters read: Reduce Motion, hit shake and playback speed.
/// Read-only defaults (off, on, 1) until the settings slice wires them to the player's options, so
/// zone slices can honour Reduce Motion from the start.
/// </summary>
public static class CutSheetMotion
{
    public static bool ReduceMotion => false;
    public static bool ShakeEnabled => !ReduceMotion;
    public static float PlaybackSpeed => 1f;

    // A tween's duration under the current preferences: instant under Reduce Motion.
    public static float Seconds(float seconds) =>
        ReduceMotion || seconds <= 0f ? 0f : seconds / (PlaybackSpeed > 0f ? PlaybackSpeed : 1f);
}
