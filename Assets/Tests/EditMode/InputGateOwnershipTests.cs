using NUnit.Framework;
using UnityEngine;

public sealed class InputGateOwnershipTests
{
    GameObject _host;
    float _timeScale;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _timeScale = Time.timeScale;
        _host = new GameObject("Gate Coordinator");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_host);
        GameplayInputGate.Clear();
        Time.timeScale = _timeScale;
    }

    [Test]
    public void MenuOpenAndClose_PreservesGameplayOwnedReasons()
    {
        var coordinator = _host.AddComponent<GameplayMenuCoordinator>();
        var panel = new GameObject("Pause Panel");
        panel.transform.SetParent(_host.transform);
        var gameplay = GameplayInputBlockReason.GearChoice | GameplayInputBlockReason.ConsumableReward;
        GameplayInputGate.Set(gameplay, true);

        Assert.That(coordinator.Push("pause", panel, null), Is.True);
        Assert.That(GameplayInputGate.Reasons, Is.EqualTo(gameplay | GameplayInputBlockReason.FrontendMenu));

        Assert.That(coordinator.Pop("pause"), Is.True);
        Assert.That(GameplayInputGate.Reasons, Is.EqualTo(gameplay),
            "Closing a menu must not unblock a pending reward or Gear choice.");
    }

    [Test]
    public void ClearAll_ReleasesOnlyMenuReasons()
    {
        var coordinator = _host.AddComponent<GameplayMenuCoordinator>();
        var first = new GameObject("First");
        var second = new GameObject("Second");
        first.transform.SetParent(_host.transform);
        second.transform.SetParent(_host.transform);
        GameplayInputGate.Set(GameplayInputBlockReason.GearChoice, true);
        coordinator.Push("menu", first, null);
        coordinator.Push("result", second, null, GameplayInputBlockReason.RunResult);

        coordinator.ClearAll();

        Assert.That(GameplayInputGate.Reasons, Is.EqualTo(GameplayInputBlockReason.GearChoice));
    }
}
