using System.IO;
using NUnit.Framework;
using UnityEngine;

// Assembly-wide: test runs never record into, or write over, the real balance stats.
[SetUpFixture]
public sealed class BalanceTelemetryTestIsolation
{
    bool _previousEnabled;
    string _directory;

    [OneTimeSetUp]
    public void Isolate()
    {
        _previousEnabled = GameplayBalanceTelemetry.Enabled;
        GameplayBalanceTelemetry.Enabled = false;
        _directory = Path.Combine(Application.temporaryCachePath, "BalanceTelemetryTests");
        GameplayBalanceTelemetry.ExportDirectoryOverride = _directory;
    }

    [OneTimeTearDown]
    public void Restore()
    {
        GameplayBalanceTelemetry.Finish("test");
        GameplayBalanceTelemetry.Enabled = _previousEnabled;
        GameplayBalanceTelemetry.ExportDirectoryOverride = null;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
