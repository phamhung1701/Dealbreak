using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One beat of the tutorial: what the coach says, what it points at, and when it moves on.
/// A step without <see cref="Done"/> is a modal explanation closed with its button; a step with
/// one waits for the player to do it.
/// </summary>
public sealed class TutorialStep
{
    public string Id;
    public string Lesson;
    public Func<string> Title;
    public Func<string> Body;
    /// <summary>Highlighted objects; the coach opens beside them.</summary>
    public Func<IEnumerable<RectTransform>> Targets;
    /// <summary>The step shows only once this holds (and the board is idle).</summary>
    public Func<bool> Ready;
    public Func<bool> Done;
    /// <summary>A step that no longer applies, such as a Trick lesson after the Trick was used.</summary>
    public Func<bool> Skip;
    public Action Enter;
    /// <summary>Commands this step refuses; null allows everything.</summary>
    public TutorialGate.CommandFilter Filter;
    public Func<string> Refusal;
    /// <summary>The side of its first target the coach tries first; zero picks by screen position.</summary>
    public Vector2 Side;
    public string Button = "NEXT";

    public bool IsModal => Done == null;
}
