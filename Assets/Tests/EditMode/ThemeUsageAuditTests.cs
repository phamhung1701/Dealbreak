using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pins the S6a theme usage audit (production/ui/hud/engine-notes.md): Credits read <c>mustard</c>, type
/// on stock and print borders read <c>inkPrint</c>, and <c>ochre</c> is left to selection. Until S6b moves
/// the values, the new fields hold the legacy ones, so the audit changes nothing on screen.
/// </summary>
public sealed class ThemeUsageAuditTests
{
    static readonly string[] SourceRoots = { "Assets/Scripts/UI", "Assets/Scripts/View", "Assets/Scripts/Frontend", "Assets/Scripts/Tutorial" };

    static (string File, int Line, string Text)[] SourceLines() =>
        SourceRoots.SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .SelectMany(file => File.ReadAllLines(file).Select((text, i) => (file.Replace('\\', '/'), i + 1, text)))
            .ToArray();

    [Test]
    public void ThemeAsset_NewPrintFieldsHoldTheLegacyValuesUntilS6b()
    {
        var theme = Resources.Load<DealbreakUITheme>("DealbreakUITheme");
        Assert.That(theme, Is.Not.Null);
        Assert.That(theme.inkPrint, Is.EqualTo(theme.ink), "S6a moves reads, not values; S6b sets #121212.");
        Assert.That(theme.mustard, Is.EqualTo(theme.ochre), "S6a moves reads, not values; S6b sets #E6AB55.");
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
