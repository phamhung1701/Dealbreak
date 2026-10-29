using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class CreditsHudTests
{
    const string CoinPath = "Assets/Art/UI/Sprites/Credits_Coin.png";
    readonly List<Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var item in _created) if (item) Object.DestroyImmediate(item);
        _created.Clear();
    }

    (RectTransform health, TMP_Text label) Fixture()
    {
        var canvas = new GameObject("Credits Canvas", typeof(RectTransform), typeof(Canvas));
        _created.Add(canvas);
        var health = new GameObject("HealthPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        health.SetParent(canvas.transform, false);
        var label = new GameObject("CreditsLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.rectTransform.SetParent(canvas.transform, false);
        return (health, label);
    }

    static Sprite Coin() => AssetDatabase.LoadAllAssetsAtPath(CoinPath).OfType<Sprite>().FirstOrDefault();

    [Test]
    public void Install_PutsTheChipJustAboveTheHpPanelAndKeepsOneChip()
    {
        var (health, label) = Fixture();

        var hud = CreditsHudUI.Install(health, label);

        Assert.That(hud, Is.Not.Null);
        var chip = (RectTransform)hud.transform;
        Assert.That(chip.parent, Is.SameAs(health), "The chip follows the HP panel wherever it shows.");
        Assert.That((chip.anchorMin, chip.anchorMax), Is.EqualTo((new Vector2(0f, 1f), new Vector2(0f, 1f))), "It hangs off the panel's top edge.");
        Assert.That(chip.sizeDelta, Is.EqualTo(new Vector2(210f, 46f)));
        Assert.That(chip.anchoredPosition.y - chip.sizeDelta.y * chip.pivot.y, Is.GreaterThan(0f), "Its bottom sits above the panel.");
        Assert.That(label.transform.parent, Is.SameAs(chip));
        Assert.That(hud.Coin, Is.Not.Null);
        Assert.That(CreditsHudUI.Install(health, label), Is.SameAs(hud), "Installing twice keeps one chip.");
    }

    [Test]
    public void Install_BuildsAMustardDiscOverAMustardPlateWithTheCoinArtAndAnInkNumber()
    {
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null);
        var coin = Coin();
        Assert.That(coin, Is.Not.Null, CoinPath + " imports as a sprite.");
        var (health, label) = Fixture();
        var corner = health.gameObject.AddComponent<PlayerCornerUI>();
        corner.coinSprite = coin;

        var hud = CreditsHudUI.Install(health, label);

        Assert.That(hud.Disc.sprite, Is.SameAs(theme.chipSprite), "The coin disc is the scalloped chip sprite.");
        Assert.That(hud.Disc.color, Is.EqualTo(theme.mustard));
        Assert.That(hud.Plate.sprite, Is.SameAs(theme.tagSprite), "The number plate is the tag sprite.");
        Assert.That(hud.Plate.color, Is.EqualTo(theme.mustard));
        Assert.That(hud.Plate.rectTransform.rect.height, Is.EqualTo(38f).Within(.01f), "The plate is 38px tall.");
        Assert.That(hud.Coin.rect.width, Is.EqualTo(46f).Within(.01f), "The disc is 46px.");
        Assert.That(hud.Plate.transform.parent.GetSiblingIndex(), Is.LessThan(hud.Coin.GetSiblingIndex()), "The plate sits behind the disc.");
        Assert.That(hud.CoinArt.sprite, Is.SameAs(coin), "The coin art is Credits_Coin.");
        Assert.That(hud.CoinSprite, Is.SameAs(coin), "Defeat flights carry the same coin.");
        Assert.That(label.font, Is.SameAs(theme.numericFont));
        Assert.That(label.fontSize, Is.EqualTo(28f));
        Assert.That(label.color, Is.EqualTo(theme.inkPrint));
        foreach (var face in new[] { hud.Disc, hud.Plate })
        {
            var shadow = face.transform.parent.GetComponentInChildren<CutSheetShadow>(true);
            Assert.That(shadow, Is.Not.Null, face.name + " has a shadow.");
            Assert.That(shadow.Depth, Is.EqualTo(CutSheetMetrics.ShadowSlot), face.name + " shadow depth 3.");
        }
    }

    [Test]
    public void Install_ShowsAPaperRingOnlyOnHover()
    {
        var (health, label) = Fixture();
        var hud = CreditsHudUI.Install(health, label);
        var hover = hud.GetComponent<CutSheetHoverRing>();
        Assert.That(hover, Is.Not.Null);
        Assert.That(hover.RingShown, Is.False);

        hover.OnPointerEnter(null);
        Assert.That(hover.RingShown, Is.True);
        Assert.That(hud.transform.localScale, Is.EqualTo(Vector3.one), "Hover never lifts or scales the chip.");
        hover.OnPointerExit(null);
        Assert.That(hover.RingShown, Is.False);
    }

    [Test]
    public void Theme_HasTheCreditsCoinIcon()
    {
        var theme = Resources.Load<DealbreakUITheme>("DealbreakUITheme");
        Assert.That(theme, Is.Not.Null);
        Assert.That(theme.creditsIcon, Is.Not.Null, "The coin comes from Tools/PrintArt/make_item_icons.py (Hud/Credits).");
    }
}
