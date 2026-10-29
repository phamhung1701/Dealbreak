using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// HUD slice S19: trigger playback speed (1x, 1.5x, 2x) divides the step, tag and chip timings,
/// keeping trigger order and the backlog speed-up. The commit lead-in derives from the trigger
/// timeline, so it scales with it and hits (and the HP drops timed by the same
/// <see cref="AttackCardPresentationUI.ImpactDelay"/>) still land when the triggers finish; the card
/// flight itself is not trigger playback and keeps its time.
/// </summary>
public sealed class PlaybackSpeedTests
{
    readonly List<Object> _created = new();
    int _speedIndex;

    [SetUp]
    public void SetUp()
    {
        CutSheetMotion.ReduceMotionOverride = null;
        _speedIndex = DisplaySettingsService.PlaybackSpeedIndex;
    }

    [TearDown]
    public void TearDown()
    {
        DisplaySettingsService.PlaybackSpeedIndex = _speedIndex;
        CutSheetMotion.ReduceMotionOverride = null;
        TriggerTimelineProbe.Release(_created);
    }

    static readonly EffectCue[] Sequence =
        { EffectCue.Credits, EffectCue.HealthCost, EffectCue.Heal, EffectCue.Credits, EffectCue.HealthCost, EffectCue.Heal };
    static readonly int[] Hits = { 0, 1, 2, 5, 7, 11 };

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Schedule_ScalesByOneOverSpeed_KeepingOrderAndBacklogSpeedUp(int speedIndex)
    {
        var presenter = TriggerTimelineProbe.Presenter(_created);
        DisplaySettingsService.PlaybackSpeedIndex = 0;
        var baseline = TriggerTimelineProbe.Run(presenter, Sequence, 10f);
        DisplaySettingsService.PlaybackSpeedIndex = speedIndex;
        float speed = DisplaySettingsService.PlaybackSpeed;
        var fast = TriggerTimelineProbe.Run(presenter, Sequence, 10f);

        Assert.That(fast.Cues, Is.EqualTo(baseline.Cues), "same order");
        for (int i = 0; i < Sequence.Length; i++)
        {
            Assert.That(fast.Speeds[i], Is.EqualTo(speed), $"step {i} keeps the speed read when queued");
            Assert.That(fast.Starts[i] * speed, Is.EqualTo(baseline.Starts[i]).Within(1e-4f), $"step {i} start");
            if (i > 0) Assert.That(fast.Starts[i], Is.GreaterThan(fast.Starts[i - 1]), $"step {i} after step {i - 1}");
        }
        Assert.That(fast.End * speed, Is.EqualTo(baseline.End).Within(1e-4f), "timeline end");

        // The backlog speed-up (MinStepScale) is kept: the same ratio between a late and an early step.
        float early = EffectTriggerPresenterUI.StepSpan(EffectCue.Heal, 0, speed);
        float late = EffectTriggerPresenterUI.StepSpan(EffectCue.Heal, 9, speed);
        Assert.That(late, Is.LessThan(early));
        Assert.That(late / early, Is.EqualTo(EffectTriggerPresenterUI.StepSpan(EffectCue.Heal, 9) /
            EffectTriggerPresenterUI.StepSpan(EffectCue.Heal, 0)).Within(1e-5f));
    }

    [TestCase(1f)]
    [TestCase(1.5f)]
    [TestCase(2f)]
    public void TagAndChipTimes_ScaleByOneOverSpeed(float speed)
    {
        Assert.That(EffectTriggerPresenterUI.TagSeconds(speed) * speed, Is.EqualTo(EffectTriggerPresenterUI.TagSeconds(1f)).Within(1e-5f));
        Assert.That(EffectTriggerPresenterUI.ChipSeconds(speed) * speed, Is.EqualTo(EffectTriggerPresenterUI.ChipSeconds(1f)).Within(1e-5f));
        Assert.That(EffectTriggerPresenterUI.ChipStaggerSeconds(speed) * speed,
            Is.EqualTo(EffectTriggerPresenterUI.ChipStaggerSeconds(1f)).Within(1e-5f));
        Assert.That(EffectTriggerPresenterUI.TagSeconds(1f), Is.GreaterThan(0f));
    }

    [Test]
    public void Speed_IsReadWhenEachTriggerIsQueued()
    {
        var presenter = TriggerTimelineProbe.Presenter(_created);
        DisplaySettingsService.PlaybackSpeedIndex = 0;
        presenter.Enqueue(new EffectTrigger(null, null, EffectCue.Credits, 2), false, 0f);
        DisplaySettingsService.PlaybackSpeedIndex = 2;
        presenter.Enqueue(new EffectTrigger(null, null, EffectCue.Heal, 2), false, 0f);
        var steps = TriggerTimelineProbe.Read(presenter);
        Assert.That(steps.Speeds, Is.EqualTo(new[] { 1f, 2f }));
    }

    [Test]
    public void Speed_IsIndependentOfReduceMotion()
    {
        var presenter = TriggerTimelineProbe.Presenter(_created);
        DisplaySettingsService.PlaybackSpeedIndex = 2;
        CutSheetMotion.ReduceMotionOverride = false;
        var off = TriggerTimelineProbe.Run(presenter, Sequence, 0f);
        CutSheetMotion.ReduceMotionOverride = true;
        var on = TriggerTimelineProbe.Run(presenter, Sequence, 0f);
        Assert.That(on.Starts, Is.EqualTo(off.Starts));
        Assert.That(on.End, Is.EqualTo(off.End));
    }

    [TestCase(1)]
    [TestCase(2)]
    public void ImpactDelay_LeadInScalesWithTheTimeline_AndHitsLandWhenTriggersEnd(int speedIndex)
    {
        var presenter = TriggerTimelineProbe.Presenter(_created);
        var attack = TriggerTimelineProbe.Attack(_created, .2f);
        DisplaySettingsService.PlaybackSpeedIndex = 0;
        var baseline = TriggerTimelineProbe.MeasureImpact(presenter, attack, Sequence, Hits);
        DisplaySettingsService.PlaybackSpeedIndex = speedIndex;
        float speed = DisplaySettingsService.PlaybackSpeed;
        var fast = TriggerTimelineProbe.MeasureImpact(presenter, attack, Sequence, Hits);

        Assert.That(baseline.LeadIn, Is.GreaterThan(0f), "the queued triggers delay the hit");
        Assert.That(fast.LeadIn * speed, Is.EqualTo(baseline.LeadIn).Within(1e-4f), "lead-in scales with the timeline");
        for (int i = 0; i < Hits.Length; i++)
        {
            // Hits land exactly when the triggers queued before them have played, plus the card flight.
            Assert.That(fast.Delays[i], Is.EqualTo(fast.TriggersEnd + .2f + Mathf.Min(Hits[i], 7) * .065f).Within(1e-4f), $"hit {Hits[i]}");
            // The card flight and hit spacing are not trigger playback: unchanged between hits.
            Assert.That(fast.Delays[i] - fast.Delays[0], Is.EqualTo(baseline.Delays[i] - baseline.Delays[0]).Within(1e-4f), $"hit {Hits[i]} spacing");
            // HP drops read the same frozen delay as the hit numbers (EnemyGroupUI).
            Assert.That(attack.ImpactDelay(Hits[i]), Is.EqualTo(fast.Delays[i]), $"hit {Hits[i]} HP drop");
        }
    }

    // Gate counting only: these triggers have nothing on screen, so Play settles without the
    // speed-scaled waits. It pins that 2x still releases the board gate once, after the last step.
    [Test]
    public void AtDoubleSpeed_BoardGateReleasesOnceAfterTheLastStep()
    {
        var presenter = TriggerTimelineProbe.Presenter(_created);
        var attack = TriggerTimelineProbe.Attack(_created, 0f);
        typeof(AttackCardPresentationUI).GetField("_pendingCommits", TriggerTimelineProbe.Private).SetValue(attack, 0);
        int completed = 0, cancelled = 0;
        attack.OnPresentationCompleted += () => completed++;
        attack.OnPresentationCancelled += () => cancelled++;
        DisplaySettingsService.PlaybackSpeedIndex = 2;
        foreach (var cue in Sequence) presenter.Enqueue(new EffectTrigger(null, null, cue, 3), true, 0f);
        Assert.That(EffectTriggerPresenterUI.CombatBusy, Is.True);
        Assert.That(attack.IsBusy, Is.True, "the board waits for the triggers");

        // Play the steps in queue order, as Update does when each falls due.
        var queue = TriggerTimelineProbe.Queue(presenter);
        var play = typeof(EffectTriggerPresenterUI).GetMethod("Play", TriggerTimelineProbe.Private);
        while (queue.Count > 0)
        {
            var step = queue[0];
            queue.RemoveAt(0);
            var routine = (IEnumerator)play.Invoke(presenter, new[] { step });
            int guard = 0;
            while (routine.MoveNext() && guard++ < 10000) { }
            Assert.That(completed, Is.EqualTo(queue.Count == 0 ? 1 : 0), $"released only after the last step ({queue.Count} left)");
        }
        attack.NotifyExternalIdle();
        Assert.That(completed, Is.EqualTo(1), "a later idle check does not release again");
        Assert.That(cancelled, Is.EqualTo(0));
        Assert.That(EffectTriggerPresenterUI.CombatBusy, Is.False);
    }
}

/// <summary>Builds a real trigger presenter in EditMode and reads its queued schedule (reflection).</summary>
internal static class TriggerTimelineProbe
{
    public const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public sealed class Schedule
    {
        public readonly List<EffectCue> Cues = new();
        public readonly List<float> Starts = new();
        public readonly List<float> Speeds = new();
        public float End;
    }

    public sealed class Impact
    {
        public float[] Delays;
        public float LeadIn, TriggersEnd;
    }

    static void SetInstance(System.Type type, object value) =>
        type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).SetValue(null, value);

    public static EffectTriggerPresenterUI Presenter(List<Object> created)
    {
        var root = new GameObject("Trigger Probe Canvas", typeof(RectTransform), typeof(Canvas));
        created.Add(root);
        var presenter = EffectTriggerPresenterUI.Install(root.GetComponent<Canvas>());
        Assert.That(presenter, Is.Not.Null);
        SetInstance(typeof(EffectTriggerPresenterUI), presenter);
        return presenter;
    }

    public static AttackCardPresentationUI Attack(List<Object> created, float travel)
    {
        var go = new GameObject("Trigger Probe Attack", typeof(RectTransform));
        created.Add(go);
        var attack = go.AddComponent<AttackCardPresentationUI>();
        typeof(AttackCardPresentationUI).GetField("_pendingCommits", Private).SetValue(attack, 1);
        typeof(AttackCardPresentationUI).GetField("_commitImpactDelay", Private).SetValue(attack, travel);
        SetInstance(typeof(AttackCardPresentationUI), attack);
        return attack;
    }

    public static void Release(List<Object> created)
    {
        if (EffectTriggerPresenterUI.Instance) EffectTriggerPresenterUI.Instance.CancelAll();
        SetInstance(typeof(EffectTriggerPresenterUI), null);
        SetInstance(typeof(AttackCardPresentationUI), null);
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    public static IList Queue(EffectTriggerPresenterUI presenter) =>
        (IList)typeof(EffectTriggerPresenterUI).GetField("_queue", Private).GetValue(presenter);

    static float Field(EffectTriggerPresenterUI presenter, string name) =>
        (float)typeof(EffectTriggerPresenterUI).GetField(name, Private).GetValue(presenter);

    /// <summary>The queued steps; starts and end relative to <paramref name="origin"/>.</summary>
    public static Schedule Read(EffectTriggerPresenterUI presenter, float origin = 0f)
    {
        var schedule = new Schedule();
        foreach (var step in Queue(presenter))
        {
            var type = step.GetType();
            schedule.Cues.Add(((EffectTrigger)type.GetField("Trigger").GetValue(step)).Cue);
            schedule.Starts.Add((float)type.GetField("StartAt").GetValue(step) - origin);
            schedule.Speeds.Add((float)type.GetField("Speed").GetValue(step));
        }
        schedule.End = Field(presenter, "_timelineEnd") - origin;
        return schedule;
    }

    /// <summary>Clears the presenter, queues <paramref name="cues"/> at <paramref name="now"/> and reads the schedule.</summary>
    public static Schedule Run(EffectTriggerPresenterUI presenter, EffectCue[] cues, float now)
    {
        presenter.CancelAll();
        foreach (var cue in cues) presenter.Enqueue(new EffectTrigger(null, null, cue, 3), false, now);
        return Read(presenter, now);
    }

    /// <summary>A fresh commit with <paramref name="cues"/> reported before its first hit: the hits' delays.</summary>
    public static Impact MeasureImpact(EffectTriggerPresenterUI presenter, AttackCardPresentationUI attack,
        EffectCue[] cues, int[] hits)
    {
        presenter.CancelAll();
        presenter.BeginCommit(new CardInstance[0], new RectTransform[0]);
        float start = Field(presenter, "_commitStart");
        foreach (var cue in cues) presenter.Enqueue(new EffectTrigger(null, null, cue, 3), false, start);
        var impact = new Impact { TriggersEnd = Field(presenter, "_timelineEnd") - start, Delays = new float[hits.Length] };
        for (int i = 0; i < hits.Length; i++) impact.Delays[i] = attack.ImpactDelay(hits[i]);
        impact.LeadIn = presenter.FreezeLeadIn(0f);
        return impact;
    }
}
