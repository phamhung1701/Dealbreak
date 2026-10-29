using NUnit.Framework;

// Assembly-wide: tests run with the default motion settings (Reduce Motion off, shake on, 1x, 100%)
// and never read or overwrite the player's own choices in PlayerPrefs.
[SetUpFixture]
public sealed class MotionSettingsTestIsolation
{
    [OneTimeSetUp]
    public void Isolate() => DisplaySettingsService.MotionSettingsIsolated = true;

    [OneTimeTearDown]
    public void Restore() => DisplaySettingsService.MotionSettingsIsolated = false;
}
