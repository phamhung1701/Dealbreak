using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// HUD slice S10a (hud.md section C, visual-design 3b): the player's HP meter, Block/Shield line and
// the one writer of the HP panel's anchors.
public sealed class PlayerCornerTests
{
    const string PresetPath = "Assets/Art/Fonts/Courier Prime UI - Paper Outline.mat";
    readonly List<Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var item in _created) if (item) Object.DestroyImmediate(item);
        _created.Clear();
        CutSheetMotion.ReduceMotionOverride = null;
    }

    // A 1920x1080 reference root with PlayerArea (0-.39 tall) and the HP panel inside it.
    (RectTransform root, RectTransform panel, TMP_Text label) Fixture()
    {
        var root = new GameObject("Reference Root", typeof(RectTransform)).GetComponent<RectTransform>();
        _created.Add(root.gameObject);
        root.sizeDelta = new Vector2(PlayerCornerLayout.RefWidth, PlayerCornerLayout.RefHeight);
        var area = new GameObject("PlayerArea", typeof(RectTransform)).GetComponent<RectTransform>();
        area.SetParent(root, false);
        DealbreakUIStyle.Place(area, 0f, 0f, 1f, .39f);
        var panel = new GameObject("HealthPanel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(area, false);
        PlayerCornerLayout.Apply(panel, false);
        var label = new GameObject("PlayerHealthLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.rectTransform.SetParent(panel, false);
        return (root, panel, label);
    }

    [Test]
    public void Meter_DrawsAnInkFrameAroundAPaperTrackWithABrickFill()
    {
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null);
        var (_, panel, label) = Fixture();

        var corner = PlayerCornerUI.For(panel, label);
        corner.SetHealth(30, 40, .75f, false);

        Assert.That(corner.Frame.sprite, Is.SameAs(theme.panelSprite));
        Assert.That(corner.Frame.color, Is.EqualTo(theme.inkPrint), "Frame: panel sprite in solid ink.");
        Assert.That(corner.Track.color, Is.EqualTo(theme.paper));
        Assert.That(corner.Track.rectTransform.offsetMin, Is.EqualTo(new Vector2(2f, 2f)), "The track is inset 2px.");
        Assert.That(corner.Fill.color, Is.EqualTo(theme.brick));
        Assert.That(corner.Fill.rectTransform.anchorMax.x, Is.EqualTo(.75f).Within(.0001f));
        Assert.That(corner.Ring.enabled, Is.False, "The paper ring shows only on hover or focus.");
        Assert.That(panel.GetComponentsInChildren<CutSheetShadow>(true), Is.Empty, "The meter has no shadow (depth 0).");
    }

    [Test]
    public void Meter_TicksEvery5HpAndEvery10WhenDense()
    {
        var (_, panel, label) = Fixture();
        var corner = PlayerCornerUI.For(panel, label);

        corner.SetHealth(40, 40, 1f, false);
        Assert.That(corner.CurrentTickStep, Is.EqualTo(5));
        Assert.That(corner.ActiveTickCount, Is.EqualTo(7), "Ticks at 5..35 for 40 HP.");
        var tick = corner.Ticks[0];
        Assert.That(tick.rectTransform.sizeDelta.x, Is.EqualTo(2f));
        Assert.That(tick.color, Is.EqualTo(DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.inkPrint : CutSheetShadow.FallbackTint));

        // A 399px track: 5 HP of 400 is under 6px, so ticks fall back to every 10.
        corner.SetHealth(400, 400, 1f, false);
        Assert.That(corner.CurrentTickStep, Is.EqualTo(10));
        Assert.That(corner.ActiveTickCount, Is.EqualTo(39));
        Assert.That(PlayerCornerUI.TickStepFor(100, 399f), Is.EqualTo(5));
        Assert.That(PlayerCornerUI.TickStepFor(400, 399f), Is.EqualTo(10));
    }

    [Test]
    public void HpNumber_IsCourierPrimeBold20InkWithThePaperOutlinePreset()
    {
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null);
        var preset = AssetDatabase.LoadAssetAtPath<Material>(PresetPath);
        Assert.That(preset, Is.Not.Null, PresetPath);
        var (_, panel, label) = Fixture();
        panel.gameObject.AddComponent<PlayerCornerUI>().hpNumberMaterial = preset;

        var corner = PlayerCornerUI.For(panel, label);
        corner.SetHealth(37, 60, 37f / 60f, false);

        Assert.That(label.font, Is.SameAs(theme.numericFont));
        Assert.That(label.fontSize, Is.EqualTo(20f));
        Assert.That(label.color, Is.EqualTo(theme.inkPrint));
        Assert.That(label.fontSharedMaterial, Is.SameAs(preset), "The shared preset, not a per-label outline instance.");
        Assert.That(label.text, Is.EqualTo("HP 37/60"));
    }

    [Test]
    public void DefenseLine_IsHiddenAtZeroAndShowsBlockAndShield()
    {
        CutSheetMotion.ReduceMotionOverride = true;
        var (_, panel, label) = Fixture();
        var corner = PlayerCornerUI.For(panel, label);

        corner.SetDefense(0, 0);
        Assert.That(corner.DefenseLine.gameObject.activeSelf, Is.False, "Hidden at 0.");

        corner.SetDefense(6, 2);
        Assert.That(corner.DefenseLine.gameObject.activeSelf, Is.True);
        Assert.That(corner.BlockLabel.text, Is.EqualTo("BLOCK 6"));
        Assert.That(corner.ShieldCountLabel.text, Is.EqualTo("2"));
        Assert.That(corner.ShieldCountLabel.fontSize, Is.EqualTo(28f));
        Assert.That(corner.BlockLabel.fontSize, Is.EqualTo(22f));
        Assert.That(corner.DefenseLine.localScale, Is.EqualTo(Vector3.one), "No pop under Reduce Motion.");

        corner.SetDefense(0, 0);
        Assert.That(corner.DefenseLine.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void Layout_PlacesTheHpPanelAtX025To235AndY014To082OnBoardAndCompact()
    {
        var (root, panel, _) = Fixture();

        Fractions(panel, root, out var min, out var max);
        Assert.That(min.x, Is.EqualTo(.025f).Within(.01f));
        Assert.That(max.x, Is.EqualTo(.235f).Within(.01f));
        Assert.That(min.y, Is.EqualTo(.014f).Within(.01f));
        Assert.That(max.y, Is.EqualTo(.082f).Within(.01f));
        Assert.That(PlayerCornerLayout.Apply(panel, false), Is.False, "A second apply writes nothing.");

        Assert.That(PlayerCornerLayout.Apply(panel, true), Is.True);
        Fractions(panel, root, out min, out max);
        Assert.That(min.y, Is.EqualTo(.014f).Within(.01f), "The compact layout keeps the same rows.");
        Assert.That(max.y, Is.EqualTo(.082f).Within(.01f));
        Assert.That(max.x, Is.LessThan(.17f), "Clear of the compact hand strip.");
    }

    [Test]
    public void ChipAndPortrait_SitAboveThePanelAtTheirSectionCRects()
    {
        var (root, panel, label) = Fixture();
        var credits = new GameObject("CreditsLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        credits.rectTransform.SetParent(root, false);

        var chip = CreditsHudUI.Install(panel, credits);
        var avatar = PlayerAvatarUI.Install(panel);

        Fractions((RectTransform)chip.transform, root, out var min, out var max);
        Assert.That(min.x, Is.EqualTo(.025f).Within(.01f), "Chip left.");
        Assert.That(max.x, Is.EqualTo(.134f).Within(.01f), "Chip right.");
        Assert.That(min.y, Is.EqualTo(.09f).Within(.01f), "Chip bottom.");
        Assert.That(max.y, Is.EqualTo(.13f).Within(.01f), "Chip top.");
        Fractions((RectTransform)avatar.transform, root, out var pMin, out var pMax);
        Assert.That(pMin.x, Is.EqualTo(.025f).Within(.01f), "Portrait left.");
        Assert.That(pMax.x, Is.EqualTo(.145f).Within(.01f), "Portrait right.");
        Assert.That(pMin.y, Is.EqualTo(.14f).Within(.01f), "Portrait bottom.");
        Assert.That(pMax.y, Is.EqualTo(.26f).Within(.01f), "Portrait top.");
        Fractions(panel, root, out var hMin, out var hMax);
        Assert.That(hMax.y, Is.LessThanOrEqualTo(min.y), "The chip clears the HP panel.");
        Assert.That(max.y, Is.LessThanOrEqualTo(pMin.y), "The portrait clears the chip.");
        foreach (var left in new[] { hMin.x, min.x, pMin.x })
            Assert.That(left * PlayerCornerLayout.RefWidth, Is.GreaterThanOrEqualTo(24f), "24px edge margin.");
    }

    [Test]
    public void Portrait_IsAShadowedStickerWithNoFrameBehindIt()
    {
        var (_, panel, _) = Fixture();

        var avatar = PlayerAvatarUI.Install(panel);

        Assert.That(avatar.GetComponent<Image>().color.a, Is.EqualTo(0f), "No frame or fill behind the sticker.");
        Assert.That(avatar.transform.Find("Fill"), Is.Null, "The charcoal fill is gone.");
        if (avatar.Portrait.sprite) Assert.That(avatar.Shadow.Depth, Is.EqualTo(CutSheetMetrics.ShadowSlot));
        var hover = avatar.GetComponent<CutSheetHoverRing>();
        var rest = avatar.Portrait.rectTransform.anchoredPosition;
        hover.OnPointerEnter(null);
        Assert.That(avatar.Portrait.rectTransform.anchoredPosition - rest, Is.EqualTo(CutSheetMetrics.HoverFaceOffset), "Hover lifts 2px.");
        Assert.That(hover.RingShown, Is.False, "The ring is focus only.");
        hover.OnPointerExit(null);
        hover.OnSelect(null);
        Assert.That(hover.RingShown, Is.True);
        Assert.That(avatar.Portrait.rectTransform.anchoredPosition, Is.EqualTo(rest));
    }

    [Test]
    public void HpPanelAnchors_StayPutAcrossSeveralFramesOfBothLateUpdates()
    {
        var (_, panel, label) = Fixture();
        var host = new GameObject("HUD", typeof(RectTransform));
        _created.Add(host);
        var runtime = host.AddComponent<DealbreakUIRuntime>();
        runtime.playerState = panel;
        var detail = host.AddComponent<CombatDetailPanelUI>();
        detail.playerStatePanel = panel;
        var runtimeLate = typeof(DealbreakUIRuntime).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var detailLate = typeof(CombatDetailPanelUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var min = panel.anchorMin;
        var max = panel.anchorMax;

        for (int frame = 0; frame < 5; frame++)
        {
            detailLate.Invoke(detail, null);
            runtimeLate.Invoke(runtime, null);
            Assert.That((panel.anchorMin, panel.anchorMax), Is.EqualTo((min, max)), $"Frame {frame}: one writer, no fight.");
        }
    }

    static void Late(PlayerCornerUI corner) =>
        typeof(PlayerCornerUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(corner, null);

    [Test]
    public void DefenseLine_FollowsThePlayersLiveBlockAndShieldEachLateUpdate()
    {
        CutSheetMotion.ReduceMotionOverride = true;
        var (root, panel, label) = Fixture();
        var credits = new GameObject("CreditsLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        credits.rectTransform.SetParent(root, false);
        var corner = PlayerCornerUI.For(panel, label);
        var chip = CreditsHudUI.Install(panel, credits);
        var player = new PlayerRuntime(30);
        corner.PlayerOverride = player;

        Late(corner);
        Assert.That(corner.DefenseLine.gameObject.activeSelf, Is.False, "Hidden at 0.");

        // No event is raised: the line reads the values itself.
        player.RestorePersistentState(30, 30, 2, 3);
        Late(corner);
        Assert.That(corner.DefenseLine.gameObject.activeSelf, Is.True);
        Assert.That(corner.BlockLabel.text, Is.EqualTo("BLOCK 3"));
        Assert.That(corner.ShieldCountLabel.text, Is.EqualTo("2"));
        Fractions(corner.DefenseLine, root, out var lineMin, out _);
        Fractions((RectTransform)chip.transform, root, out _, out var chipMax);
        Assert.That(lineMin.x, Is.GreaterThanOrEqualTo(chipMax.x), "The line sits clear of the Credits chip.");

        player.RestorePersistentState(30, 30, 0, 0);
        Late(corner);
        Assert.That(corner.DefenseLine.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void HpNumber_DrawsAboveEveryTickAndTheFill()
    {
        var (root, panel, label) = Fixture();
        var credits = new GameObject("CreditsLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        credits.rectTransform.SetParent(root, false);
        var corner = PlayerCornerUI.For(panel, label);
        corner.SetHealth(30, 30, 1f, false);
        CreditsHudUI.Install(panel, credits);
        PlayerAvatarUI.Install(panel);

        Late(corner);

        Assert.That(label.transform.parent, Is.SameAs(panel));
        Assert.That(label.transform.GetSiblingIndex(), Is.EqualTo(panel.childCount - 1), "The number is the panel's last child.");
        int meter = corner.Frame.transform.GetSiblingIndex();
        Assert.That(corner.Frame.transform.parent, Is.SameAs(panel));
        Assert.That(meter, Is.LessThan(label.transform.GetSiblingIndex()));
        Assert.That(corner.ActiveTickCount, Is.GreaterThan(0));
        for (int i = 0; i < corner.ActiveTickCount; i++)
            Assert.That(corner.Ticks[i].transform.IsChildOf(corner.Frame.transform), Is.True, "Ticks belong to the meter layer under the number.");
        Assert.That(corner.Fill.transform.IsChildOf(corner.Frame.transform), Is.True);
    }

    // A rect's corners in fractions of the reference root.
    static void Fractions(RectTransform rect, RectTransform root, out Vector2 min, out Vector2 max)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var rootCorners = new Vector3[4];
        root.GetWorldCorners(rootCorners);
        var size = (Vector2)(rootCorners[2] - rootCorners[0]);
        min = Vector2.Scale((Vector2)(corners[0] - rootCorners[0]), new Vector2(1f / size.x, 1f / size.y));
        max = Vector2.Scale((Vector2)(corners[2] - rootCorners[0]), new Vector2(1f / size.x, 1f / size.y));
    }
}
