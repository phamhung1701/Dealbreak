using NUnit.Framework;

// Assembly-wide: tests run with the default motion settings (Reduce Motion off, shake on, 1x, 100%)
// and never read or overwrite the player's own choices in PlayerPrefs. Sound stays off and volumes
// stay at their defaults for the same reason.
[SetUpFixture]
public sealed class MotionSettingsTestIsolation
{
    [OneTimeSetUp]
    public void Isolate()
    {
        DisplaySettingsService.MotionSettingsIsolated = true;
        SoundSettings.Isolated = true;
        AudioService.Silenced = true;
    }

    [OneTimeTearDown]
    public void Restore()
    {
        DisplaySettingsService.MotionSettingsIsolated = false;
        SoundSettings.Isolated = false;
        AudioService.Silenced = false;
    }
}
