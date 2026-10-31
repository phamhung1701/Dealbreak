using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// The sound layer's data and settings. Whether a cue sounds right is a human check.
public sealed class AudioCueTests
{
    static IEnumerable<AudioCue> AllCues => (AudioCue[])Enum.GetValues(typeof(AudioCue));

    [Test]
    public void EveryCue_NamesItsOwnClipAndALimit()
    {
        var clips = new HashSet<string>();
        foreach (var cue in AllCues)
        {
            var spec = AudioCues.Spec(cue);

            Assert.That(spec.Clip, Is.Not.Null.And.Not.Empty, cue.ToString());
            Assert.That(clips.Add(spec.Clip), Is.True, $"{cue} shares a clip");
            Assert.That(spec.Volume, Is.InRange(.01f, 1f), cue.ToString());
            Assert.That(spec.MaxVoices, Is.GreaterThan(0), cue.ToString());
        }
    }

    [Test]
    public void EveryCue_LoadsItsClipFromResources()
    {
        foreach (var cue in AllCues)
        {
            string path = AudioCues.ResourceFolder + AudioCues.Spec(cue).Clip;

            Assert.That(Resources.Load<AudioClip>(path), Is.Not.Null, path);
        }
    }

    [Test]
    public void PitchFor_AnOctaveDoublesAndSpreadStaysInBounds()
    {
        Assert.That(AudioService.PitchFor(12, 0f, 0f), Is.EqualTo(2f).Within(1e-4f));
        Assert.That(AudioService.PitchFor(0, .05f, 1f), Is.EqualTo(1.05f).Within(1e-4f));
        Assert.That(AudioService.PitchFor(0, .05f, -1f), Is.EqualTo(.95f).Within(1e-4f));
    }

    [Test]
    public void Play_WithoutAService_DoesNothing()
    {
        bool silenced = AudioService.Silenced;
        AudioService.Silenced = false;
        try { Assert.DoesNotThrow(() => AudioService.Play(AudioCue.EnemyHit)); }
        finally { AudioService.Silenced = silenced; }
    }
}
