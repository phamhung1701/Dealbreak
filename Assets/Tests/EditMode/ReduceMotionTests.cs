using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// HUD slice S18: Reduce Motion and Screen Shake consumers. Reduce Motion drops shake, hops, slaps,
/// tilt and the board jolt and turns board transitions into cuts, but never changes trigger order or
/// when hits land (<see cref="AttackCardPresentationUI.ImpactDelay"/>), which drives HP drops.
/// Screen Shake off drops only the shake.
/// </summary>
public sealed class ReduceMotionTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> _created = new();
    bool _screenShake;

    [SetUp]
    public void SetUp()
    {
        CutSheetMotion.ReduceMotionOverride = null;
        _screenShake = DisplaySettingsService.ScreenShake;
        DisplaySettingsService.ScreenShake = true;
    }

    [TearDown]
    public void TearDown()
    {
        CutSheetMotion.ReduceMotionOverride = null;
        DisplaySettingsService.ScreenShake = _screenShake;
        TriggerTimelineProbe.Release(_created);
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        _created.Add(go);
        return go.AddComponent<T>();
    }

    // ---- Order and timing ------------------------------------------------------------------

    static readonly EffectCue[] Sequence =
        { EffectCue.Credits, EffectCue.HealthCost, EffectCue.Heal, EffectCue.Credits, EffectCue.HealthCost, EffectCue.Heal };

    static readonly int[] Hits = { 0, 1, 2, 5, 7, 11 };

    // The same triggers queued on a real presenter with Reduce Motion off and on.
    [Test]
    public void TriggerSchedule_IsIdenticalWithReduceMotionOnAndOff()
    {
        var presenter = TriggerTimelineProbe.Presenter(_created);
        CutSheetMotion.ReduceMotionOverride = false;
        var off = TriggerTimelineProbe.Run(presenter, Sequence, 5f);
        CutSheetMotion.ReduceMotionOverride = true;
        var on = TriggerTimelineProbe.Run(presenter, Sequence, 5f);
        Assert.That(on.Cues, Is.EqualTo(off.Cues));
        Assert.That(on.Starts, Is.EqualTo(off.Starts));
        Assert.That(on.End, Is.EqualTo(off.End));
        Assert.That(off.End, Is.GreaterThan(0f));
        for (int i = 1; i < on.Starts.Count; i++)
            Assert.That(on.Starts[i], Is.GreaterThan(on.Starts[i - 1]), $"step {i} starts after step {i - 1}");
    }

    // Triggers queued before the first hit set the commit lead-in, so the presenter must be live.
    [TestCase(false)]
    [TestCase(true)]
    public void ImpactDelay_IsIdenticalWithReduceMotionOnAndOff(bool dragged)
    {
        var presenter = TriggerTimelineProbe.Presenter(_created);
        var attack = TriggerTimelineProbe.Attack(_created, dragged ? .24f : .2f);
        CutSheetMotion.ReduceMotionOverride = false;
        var off = TriggerTimelineProbe.MeasureImpact(presenter, attack, Sequence, Hits);
        CutSheetMotion.ReduceMotionOverride = true;
        var on = TriggerTimelineProbe.MeasureImpact(presenter, attack, Sequence, Hits);
        Assert.That(off.LeadIn, Is.GreaterThan(0f), "the queued triggers delay the hit");
        Assert.That(on.LeadIn, Is.EqualTo(off.LeadIn));
        for (int i = 0; i < Hits.Length; i++)
        {
            Assert.That(on.Delays[i], Is.EqualTo(off.Delays[i]), $"hit {Hits[i]}");
            Assert.That(off.Delays[i], Is.GreaterThan(off.LeadIn), $"hit {Hits[i]} lands after the triggers and the travel");
        }
    }

    // ---- Shake -----------------------------------------------------------------------------

    [Test]
    public void ScreenShake_MovesOnlyWithShakeOnAndReduceMotionOff()
    {
        CutSheetMotion.ReduceMotionOverride = false;
        Assert.That(ScreenShakeUI.Moves, Is.True);
        CutSheetMotion.ReduceMotionOverride = true;
        Assert.That(ScreenShakeUI.Moves, Is.False, "Reduce Motion forces shake off");
        CutSheetMotion.ReduceMotionOverride = false;
        DisplaySettingsService.ScreenShake = false;
        Assert.That(ScreenShakeUI.Moves, Is.False, "Screen Shake off");
        Assert.That(CutSheetMotion.ReduceMotion, Is.False, "Screen Shake off leaves Reduce Motion alone");
    }

    EnemyDropUI DropUI()
    {
        var canvas = Make<Canvas>("Reduce Motion Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        EnemyDropUI.Install(canvas);
        var ui = canvas.GetComponentInChildren<EnemyDropUI>(true);
        Assert.That(ui, Is.Not.Null);
        Assert.That(ui.Token, Is.Not.Null);
        ui.Token.anchoredPosition = ui.TokenHome + new Vector2(40f, 0f);
        return ui;
    }

    static IEnumerator Routine(EnemyDropUI ui, string name) =>
        (IEnumerator)typeof(EnemyDropUI).GetMethod(name, Private).Invoke(ui, null);

    [TestCase("Shake")]
    [TestCase("ReturnHome")]
    public void DropToken_SnapsHomeUnderReduceMotion(string routine)
    {
        var ui = DropUI();
        CutSheetMotion.ReduceMotionOverride = true;
        Assert.That(Routine(ui, routine).MoveNext(), Is.False, "no frames of motion");
        Assert.That(ui.Token.anchoredPosition, Is.EqualTo(ui.TokenHome));
    }

    [Test]
    public void DropToken_StillWobblesWithScreenShakeOff()
    {
        var ui = DropUI();
        DisplaySettingsService.ScreenShake = false;
        Assert.That(Routine(ui, "Shake").MoveNext(), Is.True, "the full-backpack wobble is feedback, not screen shake");
    }

    [Test]
    public void DropToken_ShakesWithMotionOn()
    {
        var ui = DropUI();
        Assert.That(Routine(ui, "Shake").MoveNext(), Is.True, "the wobble plays over frames");
    }

    // ---- Board ------------------------------------------------------------------------------

    [Test]
    public void BoardPanelTransition_IsACutUnderReduceMotion()
    {
        CutSheetMotion.ReduceMotionOverride = false;
        Assert.That(BoardPanelTransition.ShowSeconds, Is.GreaterThan(0f));
        Assert.That(BoardPanelTransition.HideSeconds, Is.GreaterThan(0f));
        Assert.That(BoardPanelTransition.OverlayFadeSeconds, Is.GreaterThan(0f));
        CutSheetMotion.ReduceMotionOverride = true;
        Assert.That(BoardPanelTransition.ShowSeconds, Is.EqualTo(0f));
        Assert.That(BoardPanelTransition.HideSeconds, Is.EqualTo(0f));
        Assert.That(BoardPanelTransition.OverlayFadeSeconds, Is.EqualTo(0f));
    }

    [Test]
    public void BoardTone_CutsToTheDefenseToneUnderReduceMotion()
    {
        var board = Make<UnityEngine.UI.Image>("Reduce Motion Board");
        var tone = BoardTone.Install(board);
        CutSheetMotion.ReduceMotionOverride = true;
        tone.Set(true, 10f);
        Assert.That(board.color, Is.EqualTo(BoardTone.ToneFor(true)));
        Assert.That(tone.DotScreen.color.a, Is.EqualTo(BoardTone.DotScreenAlpha).Within(.0001f));
    }
}
