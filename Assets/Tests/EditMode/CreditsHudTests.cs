using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CreditsHudTests
{
    readonly List<Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var item in _created) if (item) Object.DestroyImmediate(item);
        _created.Clear();
    }

    [Test]
    public void Install_PutsACoinAndTheCreditsLabelInAChipJustAboveTheHpPanel()
    {
        var canvas = new GameObject("Credits Canvas", typeof(RectTransform), typeof(Canvas));
        _created.Add(canvas);
        var health = new GameObject("HealthPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        health.SetParent(canvas.transform, false);
        var label = new GameObject("CreditsLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.rectTransform.SetParent(canvas.transform, false);

        var hud = CreditsHudUI.Install(health, label);

        Assert.That(hud, Is.Not.Null);
        var chip = (RectTransform)hud.transform;
        Assert.That(chip.parent, Is.SameAs(health), "The chip follows the HP panel wherever it shows.");
        Assert.That((chip.anchorMin, chip.anchorMax, chip.pivot), Is.EqualTo((new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero)),
            "It sits on the panel's top edge, above the HP bar.");
        Assert.That(chip.anchoredPosition.y, Is.GreaterThan(0f));
        Assert.That(label.transform.parent, Is.SameAs(chip));
        Assert.That(hud.Coin, Is.Not.Null);
        Assert.That(CreditsHudUI.Install(health, label), Is.SameAs(hud), "Installing twice keeps one chip.");
        var theme = DealbreakUIStyle.Theme;
        if (theme && theme.creditsIcon)
            Assert.That(hud.Coin.GetComponentInChildren<Image>(true).sprite, Is.SameAs(theme.creditsIcon));
    }

    [Test]
    public void Theme_HasTheCreditsCoinIcon()
    {
        var theme = Resources.Load<DealbreakUITheme>("DealbreakUITheme");
        Assert.That(theme, Is.Not.Null);
        Assert.That(theme.creditsIcon, Is.Not.Null, "The coin comes from Tools/PrintArt/make_item_icons.py (Hud/Credits).");
    }
}
