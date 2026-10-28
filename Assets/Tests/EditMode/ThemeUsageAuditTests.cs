using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pins the theme audit (production/ui/hud/engine-notes.md, S6a and S6b): Credits read <c>mustard</c>,
/// type on stock and print borders read <c>inkPrint</c>, <c>ochre</c> is left to selection, and the theme
/// asset and its code defaults hold the Cut Sheet values and text fonts.
/// </summary>
public sealed class ThemeUsageAuditTests
{
    static readonly string[] SourceRoots = { "Assets/Scripts/UI", "Assets/Scripts/View", "Assets/Scripts/Frontend", "Assets/Scripts/Tutorial" };

    static (string File, int Line, string Text)[] SourceLines() =>
        SourceRoots.SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .SelectMany(file => File.ReadAllLines(file).Select((text, i) => (file.Replace('\\', '/'), i + 1, text)))
            .ToArray();

    // (token, field, hex) from visual-design sections 1a and 1b, after the S1 eyedropper result.
    static readonly object[] CutSheetValues =
    {
        new object[] { "ink", "1C1814" }, new object[] { "charcoal", "241E18" }, new object[] { "slate", "5C5247" },
        new object[] { "paper", "EFE4CC" }, new object[] { "brick", "A84538" }, new object[] { "ochre", "4AA6B8" },
        new object[] { "inkPrint", "121212" }, new object[] { "kraft", "B8956A" }, new object[] { "mustard", "E6AB55" },
        new object[] { "muted", "8C8270" }, new object[] { "boardDefense", "15110E" },
    };

    [TestCaseSource(nameof(CutSheetValues))]
    public void ThemeAsset_HoldsTheCutSheetValue(string field, string hex)
    {
        var theme = Resources.Load<DealbreakUITheme>("DealbreakUITheme");
        Assert.That(theme, Is.Not.Null);
        var asset = (Color)typeof(DealbreakUITheme).GetField(field).GetValue(theme);
        var fresh = ScriptableObject.CreateInstance<DealbreakUITheme>();
        var code = (Color)typeof(DealbreakUITheme).GetField(field).GetValue(fresh);
        Object.DestroyImmediate(fresh);
        Assert.That(ColorUtility.ToHtmlStringRGB(asset), Is.EqualTo(hex), $"{field} in the theme asset");
        Assert.That(ColorUtility.ToHtmlStringRGB(code), Is.EqualTo(hex), $"{field} code default");
    }

    [Test]
    public void ThemeAsset_UsesTheCutSheetTextFonts()
    {
        var theme = Resources.Load<DealbreakUITheme>("DealbreakUITheme");
        Assert.That(theme.bodyFont.name, Is.EqualTo("Zilla Slab UI"));
        Assert.That(theme.numericFont.name, Is.EqualTo("Courier Prime UI"));
    }

    [Test]
    public void CreditsTriggerTag_UsesMustard_OtherGainsDoNot()
    {
        var theme = DealbreakUIStyle.Theme;
        Assert.That(EffectTriggerPresenterUI.Style(EffectCue.Credits), Is.EqualTo(EffectTriggerPresenterUI.TagStyle.Credits));
        Assert.That(EffectTriggerPresenterUI.Style(EffectCue.Heal), Is.EqualTo(EffectTriggerPresenterUI.TagStyle.Gain));
        Assert.That(EffectTriggerPresenterUI.Fill(EffectTriggerPresenterUI.TagStyle.Credits), Is.EqualTo(theme.mustard));
        Assert.That(EffectTriggerPresenterUI.Fill(EffectTriggerPresenterUI.TagStyle.Gain), Is.EqualTo(theme.ochre));
    }

    [Test]
    public void CreditsAndPriceCode_NeverReadsTheSelectionAccent()
    {
        // Lime is the trigger presenter's alias for ochre; "market" covers the Black Market sell price.
        var credits = new Regex(@"credit|price|coin|market", RegexOptions.IgnoreCase);
        var accent = new Regex(@"\.ochre\b|\bLime\b");
        var offenders = SourceLines()
            .Where(l => accent.IsMatch(l.Text) && credits.IsMatch(l.Text))
            .Select(l => $"{l.File}:{l.Line}")
            .ToArray();
        Assert.That(offenders, Is.Empty, "Credits read Theme.mustard; ochre is selection only.");
    }

    [Test]
    public void TextOnPaper_ReadsPrintInkNotTheStage()
    {
        // A label chosen for a paper fill ("paper ? theme.ink") is type on stock: it reads inkPrint.
        var onPaper = new Regex(@"[Pp]aper\s*\?\s*(\w+\.)?[Tt]heme\.ink\b");
        var offenders = SourceLines()
            .Where(l => onPaper.IsMatch(l.Text))
            .Select(l => $"{l.File}:{l.Line}")
            .ToArray();
        Assert.That(offenders, Is.Empty);
    }
}
