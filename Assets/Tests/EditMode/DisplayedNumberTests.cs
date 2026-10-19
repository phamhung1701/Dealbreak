using NUnit.Framework;
using UnityEngine;

public sealed class DisplayedNumberTests
{
    [Test]
    public void FirstAndUnanimatedValues_ShowAtOnce()
    {
        var number = new DisplayedNumber();
        number.SetTarget(30, true);
        Assert.That(number.Shown, Is.EqualTo(30), "The first value has nothing to ease from.");
        number.SetTarget(12, false);
        Assert.That(number.Shown, Is.EqualTo(12));
        Assert.That(number.IsAnimating, Is.False);
        Assert.That(number.Tint(Color.white), Is.EqualTo(Color.white));
    }

    [Test]
    public void AnimatedChange_EasesToTargetAndTintFadesBack()
    {
        var number = new DisplayedNumber();
        number.SetTarget(30, true);
        number.SetTarget(10, true);
        Assert.That(number.Shown, Is.EqualTo(30));
        Assert.That(number.Tint(Color.white), Is.Not.EqualTo(Color.white), "A loss tints the label.");

        number.Tick(0.1f);
        Assert.That(number.Shown, Is.InRange(11, 29));
        int ticks = 0;
        while (number.Tick(1f / 60f) && ticks++ < 600) { }
        Assert.That(number.Shown, Is.EqualTo(10));
        Assert.That(number.IsAnimating, Is.False);
        Assert.That(number.Tint(Color.white), Is.EqualTo(Color.white));
    }

    [Test]
    public void RetargetMidAnimation_ContinuesFromTheShownValue()
    {
        var number = new DisplayedNumber();
        number.SetTarget(0, true);
        number.SetTarget(100, true);
        number.Tick(0.1f);
        int midway = number.Shown;
        number.SetTarget(50, true);
        Assert.That(number.Shown, Is.EqualTo(midway), "Retargeting must not jump.");
        while (number.Tick(1f / 60f)) { }
        Assert.That(number.Shown, Is.EqualTo(50));
    }
}
