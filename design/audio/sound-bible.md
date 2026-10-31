# Dealbreak sound bible

**Status: draft, not an owner ruling.** The direction below is proposed in `production/next-phase-plan.md`. It becomes binding once the owner confirms it in `Docs/OWNER_DECISIONS.md`.

## Identity

Cut Sheet says everything on screen is a printed paper object, so everything you hear is too: paper slaps, rubber stamps, tape, card snaps and real coins in a dry, close room. No synth UI blips.

Music is proposed as dusty, sampled crime jazz: upright bass, brushes and tape wear. It sits under the card sounds, which stay the focus.

## Mood by state

These follow the art bible's mood table. Sound never contradicts the board.

| State | Target | Sound |
|---|---|---|
| Player turn | Poised control | Tactile and unhurried |
| Defense | Bracing | Same music with a tension layer, never a track change |
| Route map | Calm planning | Sparse |
| Black Market | Temptation | Warm, a little shady |
| Incident | Uncertainty | Near silent |
| Rewards | Payoff | The loudest moments in the game |
| Defeat | Spent | Quiet and flat |

## Rules

1. Sound follows what the player sees. Cues fire from presenters at the visible moment, not from `CombatManager` events, which fire before the hit lands on screen.
2. Sound never consumes gameplay RNG, holds an input gate or gets waited on. A seeded run ends the same with sound on or off.
3. No gameplay information is carried by sound alone.
4. Reduce Motion doesn't change sound. At 2x playback, cue order holds and voice limits stop pile-ups.
5. UI cues and music run on unscaled time, because pause sets `Time.timeScale` to 0.

## How it's built

- `AudioCue` lists the cues and `AudioCues.Spec` sets each one's clip, bus, level, pitch spread and pile-up limits.
- Clips live in `Assets/Resources/Audio/Sfx`, named by the spec. To replace a sound, overwrite its `.wav` under the same name.
- `AudioService` plays cues from a 12-voice pool. Levels are set in code, without an `AudioMixer`, so a fade or duck would need code changes.
- Volumes are `SoundSettings`: Music, SFX and UI, 0-100%. Gain is the square of the level.
- The current clips are synthesised placeholders from `Tools/Audio/make_placeholder_sfx.py`. They hold each cue's place and were measured but never listened to.
