using System;
using UnityEngine;

/// <summary>
/// Plays <see cref="AudioCue"/>s from a small voice pool. Presentation only: it never touches game
/// state, an input gate or a seeded stream, and nothing waits for it, so a run ends the same with
/// sound on or off. A cue with no clip, or with its bus at 0%, is silent. Lives on the Bootstrap
/// object; with no instance (EditMode) every call does nothing.
/// </summary>
public sealed class AudioService : MonoBehaviour
{
    const int VoiceCount = 12;

    static AudioService _instance;

    /// <summary>Test isolation: while set, nothing plays.</summary>
    public static bool Silenced { get; set; }

    readonly AudioSource[] _voices = new AudioSource[VoiceCount];
    readonly AudioCue[] _voiceCues = new AudioCue[VoiceCount];
    readonly AudioClip[] _clips = new AudioClip[Enum.GetValues(typeof(AudioCue)).Length];
    readonly bool[] _clipLoaded = new bool[Enum.GetValues(typeof(AudioCue)).Length];
    readonly float[] _lastPlayed = new float[Enum.GetValues(typeof(AudioCue)).Length];
    // Pitch variety only. Never a RunRandomContext stream: sound must not consume gameplay RNG.
    readonly System.Random _variety = new(1701);

    /// <summary>Plays a cue, optionally shifted up by <paramref name="semitones"/> (a rising trigger chain).</summary>
    public static void Play(AudioCue cue, int semitones = 0)
    {
        if (Silenced || !_instance) return;
        _instance.PlayCue(cue, semitones);
    }

    /// <summary>The pitch of a cue: the semitone shift, spread by <paramref name="variation"/> in -1..1.</summary>
    public static float PitchFor(int semitones, float spread, float variation) =>
        Mathf.Pow(2f, semitones / 12f) * (1f + spread * variation);

    void Awake()
    {
        if (_instance && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
        for (int i = 0; i < VoiceCount; i++)
        {
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.ignoreListenerPause = true;
            _voices[i] = voice;
        }
        Array.Fill(_lastPlayed, float.NegativeInfinity);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void PlayCue(AudioCue cue, int semitones)
    {
        var spec = AudioCues.Spec(cue);
        float volume = spec.Volume * SoundSettings.Gain(spec.Bus);
        if (volume <= 0f) return;
        int index = (int)cue;
        float now = Time.unscaledTime;
        if (now - _lastPlayed[index] < spec.MinGapSeconds) return;
        var clip = ClipFor(index, spec);
        if (!clip) return;

        AudioSource free = null;
        int freeIndex = -1, playing = 0;
        for (int i = 0; i < VoiceCount; i++)
        {
            if (_voices[i].isPlaying)
            {
                if (_voiceCues[i] == cue) playing++;
            }
            else if (!free)
            {
                free = _voices[i];
                freeIndex = i;
            }
        }
        if (!free || playing >= spec.MaxVoices) return;

        _lastPlayed[index] = now;
        _voiceCues[freeIndex] = cue;
        free.clip = clip;
        free.volume = volume;
        free.pitch = PitchFor(semitones, spec.PitchSpread, (float)(_variety.NextDouble() * 2d - 1d));
        free.Play();
    }

    AudioClip ClipFor(int index, AudioCueSpec spec)
    {
        if (_clipLoaded[index]) return _clips[index];
        _clipLoaded[index] = true;
        return _clips[index] = Resources.Load<AudioClip>(AudioCues.ResourceFolder + spec.Clip);
    }
}
