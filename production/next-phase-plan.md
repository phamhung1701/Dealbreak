# Next phase plan: Sound, Motion, Ship Readiness

Written 2026-10-31 from code and docs at `c3e203ee`. Nothing here was checked in a running game (Locus was down). Lines marked **verify** are inferred from code search and need a look before the slice starts.

The combat HUD is done (S7-S20). The game plays fully but is silent, only combat has the Cut Sheet look, and no player build exists with any of that work. This phase makes the whole run look, sound and feel like the combat screen, then gets it into a build people can play.

## Progress (2026-10-31, code-only pass)

This pass was written without Unity. The gate then ran on `17239384` and passed: EditMode 2144 passed + 1 skipped, PlayMode 11 passed + 1 skipped, soak passed in 101 s. Nothing has been seen or heard in the game yet.

**Built:**
- Sound layer: `AudioService`, `AudioCue`, `SoundSettings`, 16 cues hooked to presenters.
- Music, SFX and UI volume rows in Settings, built at runtime.
- Synthesised placeholder clips (`Tools/Audio/make_placeholder_sfx.py`).
- Draft sound bible, accessibility audit, `hud.md` OQ3 and OQ9.
- Small fixes: Contract line reads the balance number, coin stagger 40ms, how-to copy removed from unavailable-button details, one per-frame allocation removed.

**Changed from the plan below:** no `AudioMixer` asset and no cue bank asset, because neither can be authored safely without the Editor. Levels are set in code and cues are a code table, so a clip is replaced by overwriting its `.wav`. Add a mixer later if the mix needs fades or ducking.

**Check first in Unity:**
1. Open Settings from both menus: the three volume rows sit right of the main rows, at x 365-950, a position worked out from scene data and not seen.
2. Play a fight with sound on and listen to every cue.

**Not done, needs the Editor or your eyes:** everything else in this plan, including Track S, board tones, Track M motion, music and the Phase 0 captures.

## Where things stand

| Area | Today | Gap |
|---|---|---|
| SFX | No audio files, and no `AudioSource`, `AudioClip` or `AudioMixer` in any script | Everything |
| Music | None | Everything |
| Audio settings | None; the accessibility doc requires separate Music, SFX and UI volume before any audio ships | Sliders and persistence |
| Audio direction | No sound bible; `hud.md` section H has no sound column (OQ3) | Direction doc and cue list |
| Combat motion | Strong: card hit pipeline, trigger hops, slaps, coin flights, tier auras, Mod FX, shake, plus Reduce Motion and playback speed | A few moments (see Track M) |
| Board moods | Theme has one state field, `boardDefense`; the art bible defines eight state tones with a 250ms crossfade | Six tones and the crossfade |
| Cut Sheet screens | Combat HUD done, `PathScreenUI` partly | 12 screens have no Cut Sheet code (see Track S) |
| Deck list | No entry point since S14 (`hud.md` OQ7) | **A player-facing regression** |
| Player build | Last is `Builds/FinalQA`, 2026-10-16, 460 MB | No build has any Cut Sheet work |
| Replay value | No unlocks, achievements or difficulty tiers in code; all six characters open | Owner call |
| Art licence | The Tricks & Gears pack has no licence file and is untracked; Credits names it with no author | **Blocks any public build** |

## Order

```
Phase 0  Close out HUD, restore the deck list, cut a build
Phase 1  Audio engine + placeholder cues        (Track A)
Phase 2  Screens with their motion              (Track S + M, one slice per screen)
         Music runs alongside                   (Track B)
Phase 3  Final SFX and mix                      (needs Phase 2's timing)
Phase 4  Ship readiness                         (Track R)
```

Why this order: the engine goes first so every later motion slice can place its sound as it is built. Final SFX comes after motion because sounds are cut to the final timing. Each screen's reskin and motion ship as one slice, since animating a screen about to be reskinned is wasted work. Music touches none of this, so it can start at any time.

## Phase 0: Close out

1. Run the owed human captures: S16 full backpack, S18 side by side, S19 at 2x (clone pulse over a block flip), S20 at 130%.
2. Restore a deck list entry point (needs owner decision 4). Size S.
3. Close the HUD open questions that are still live: OQ4 pause button, OQ8 MAP during combat, OQ9 coin timing (code 0.42s and 60ms, bible 350-450ms and 40ms), OQ10 minimum resolution, OQ11 busy buttons, OQ12 refused controls. The 21:9 cap (engine-notes concern 8) had no implementation when planned: **verify**.
4. Clear the small known debts: `RunStatusUI.FormatContractProgress` hardcodes "/2", wordmark `#E8322B`, sort latch over an unsorted drawn card.
5. Cut a Windows build and play one full run in it. That is the first real check of the Cut Sheet work outside the Editor.

S3b glyphs stay blocked on the hand-drawn SVGs; they don't hold up anything else.

## Track A: Audio engine (Phase 1)

Use Unity's built-in audio and one `AudioMixer`. Fades and layers are all this game needs, so FMOD or Wwise would add a dependency for nothing.

- **A1. Sound bible.** Use `audio-director` with `.claude/docs/templates/sound-bible.md`. Proposed identity, following Cut Sheet: everything is a physical print object. Paper slaps, rubber stamps, tape, card snaps and real coins over a dry, close room, with no synth UI blips. Add the sound column to `hud.md` section H and rule it into `OWNER_DECISIONS.md`.
- **A2. Mixer and service.** Buses: Master, Music, SFX, UI. An `AudioService` that survives scene loads (**verify** whether it belongs beside `GameFlowService`), a pooled `AudioSource` set, per-cue voice caps and retrigger gaps. Music fades and ducks on pause.
- **A3. Cue data.** A `Resources/AudioCueBank` asset. Each `AudioCueId` holds clips, volume, pitch range and bus. A cue without a clip stays silent, so code can land before audio does. IDs are appended, never renumbered, like `GearCounterKeys`.
- **A4. Settings.** Music, SFX and UI sliders in `SettingsMenuUI`, stored through `DisplaySettingsService`. Change both scenes and the owning authoring script together. Keep copy state-only.
- **A5. Tests.** Service off under test, like `MotionSettingsTestIsolation`. Bank completeness. Volume persistence.

Rules for all audio work:

1. **Audio follows what the player sees, not what the rules resolve.** `CombatManager` events fire at resolution, before the hit lands on screen. Cues fire from presenters: `ImpactDelay`, `EffectTriggerPresenterUI` steps, coin landings. Don't subscribe to `OnDamageResolved` for sound. `ScreenShakeUI` queues from that event, but its cue plays when each hit's shake starts, so it stays with the visual. The Perfect Block chime plays with its text, which appears before the card lands; check that by ear.
2. Pitch and clip choice use a private `System.Random`, never a `RunRandomContext` stream or anything gameplay reads. A seeded run must end the same with audio on or off.
3. Audio never holds an input gate and is never awaited. "Board busy" stays visual.
4. No game information by sound alone, per the accessibility doc. Update its SFX audit as cues land.
5. Pause sets `Time.timeScale` to 0, so UI cues and music run on unscaled time.
6. At 2x playback, cues keep their order and voice caps stop pile-ups. Reduce Motion doesn't change audio.
7. Import: short SFX decompress on load; music streams.

## Track B: Music (runs alongside)

Eight states already defined by the art bible mood table, each mapped to one cue:

| State | Art bible mood | Music |
|---|---|---|
| Menu | collected, poster-like | Title theme |
| Route map | calm, cartographic | Sparse loop |
| Combat: player turn | poised control | Combat loop, base layer |
| Combat: defense | bracing | Same loop, tension layer fades in |
| Black Market | shady, single lamp | Own loop |
| Incident | suspenseful, sparse | Near silent bed |
| Headliner | | J, Q and K variants, rising through the run |
| Victory / Defeat | payoff / spent | Stingers |

Proposed direction, for the owner to confirm: dusty, sampled crime jazz. Upright bass, brushes and tape wear suit paper and ink, and leave room for card SFX. Defense must stay the same track with a layer added, not a track change, because the phases swap often. Minimum set is about 8 loops and 3 stingers, with Headliner variants the first cut if budget is tight.

- **B1.** Music state machine in `AudioService`, driven by `GameState` and the run screen events. Crossfades and the defense layer. Needs only placeholder loops.
- **B2.** Final tracks in, loop points checked, levels set.

## Track S + M: Screens and motion (Phase 2)

Each slice applies the Cut Sheet look, its board tone, its motion and its placeholder cues together. Use the art bible's animation table for timings, `CutSheetMotion.Seconds` so Reduce Motion works, and the slice checklist from `engine-notes.md`.

- **S21. Board tones.** Add the missing state tones and the 250ms crossfade first, since every slice below uses them. Reduce Motion cuts instead.
- **S22. Route map.** Finish `PathScreenUI`: kraft sheet, stamped nodes, eye glyph, Headliner sticker. Motion: a stamp on node entry, route reveal, scout reveal.
- **S23. Black Market.** `ShopUI`: kraft price board, pinned stickers, mustard stamps, reroll restyle. Motion: goods slap in, reroll shuffle, service row close.
- **S24. Incident.** `RunEventUI`: one clipping sheet, taped choices. Motion: sheet lands, the result stamps.
- **S25. Rewards.** `ConsumableRewardUI`, Headliner reward. Motion: slaps with the 2px board jolt, face-card flip-in. Rewards are the top payoff moment in the bible.
- **S26. Run end.** `RunResultUI`: loud victory, and defeat as a cream receipt that prints line by line.
- **S27. Menus.** `MainMenuUI`, `RunSetupUI`, `CharacterPickerUI`, `SettingsMenuUI`, `PauseMenuUI`, `ConfirmationDialogUI`, `LoadingScreenUI`, deck view, tutorial overlay. Can split further.
- **S28. Combat motion gaps.** Each is **verify**, as code search only showed they have no dedicated routine:
  - Cards dealt from the draw pile into the hand: `CardView` only smooths toward its layout position.
  - Enemy attack: `EnemyDisplayUI` has hit, defeat and Perfect Block routines but no wind-up.
  - Headliner entrance: a poster-style reveal for each of the 12 fights.
  - Low HP state: motion and shape backed, kept off during defense's no-idle rule.
  - Enemy idle on the player's turn only, since the bible rules no idle motion in defense.
  - Thief flee, distinct from a death.

Leave out full-screen shake (ruled out), and don't add tilt to panels, buttons, meters or numbers.

## Phase 3: Final SFX and mix

About 45 cues. Hook sites below were confirmed in code unless marked.

| Group | Cues | Hook |
|---|---|---|
| UI | Press, release, tooltip open and close, disabled | `EditorialButton`, `ContextualTooltipUI` |
| Cards | Select, deal, commit, flip, into deck | `CardView`, `AttackCardPresentationUI.AnimateCard`, `FlipToBack`, `PopDeck` |
| Hits | Enemy hit by weight, blocked, Shield, instant defeat, player hurt | `EnemyDisplayUI.HitCueAfter`, player side **verify** |
| Defense | Block, Perfect Block, Recover | `PerfectBlockCueRoutine`, `Flash`, Recover **verify** |
| Enemies | Ability callout, defeat, flee, Headliner entrance | `EnemyAbilityCalloutUI`, `DefeatCueRoutine` |
| Triggers | Hop, tag, Gear by tier, a pitch that rises along a chain | `EffectTriggerPresenterUI`, `CutSheetSlotUI.Hop` |
| Credits | Coin landing, chip stamp | `CreditsHudUI` |
| Items | Pick up, drop, merge, sell, slot slap, discard, full backpack | `ItemDragUI`, `CutSheetSlotUI.Slap`, `EnemyDropUI` |
| Run | Node stamp, scout, reroll, buy, Incident sheet, reward, victory, defeat | Track S slices |

The rising pitch on trigger chains is the one sound worth prototyping early. It's where the game's build payoff will be heard, as in Balatro.

Finish with a full mix pass: one complete run at speakers and headphones, at 1x and 2x. Then re-audit the accessibility doc's auditory section.

## Track R: Ship readiness (Phase 4)

- **R1. Art licence.** Find the pack's terms and author, keep the licence with the project, and complete Credits. Add audio credits. Do this early; it needs no engine time, and the answer could force art replacement.
- **R2. Build size and cleanup.** The build is 460 MB and ships `DirectML.dll`. Check whether anything uses `com.unity.ai.inference` or the other AI, Multiplayer, Visual Scripting and Analytics packages, and remove what's unused. Don't commit `Assets/_Recovery` or the test init scene; decide their fate with the owner.
- **R3. Balance.** `BalanceAnalysis.md` is an old code-only read with no measured rates. Run `balance-analyst` on recorded player and bot stats after the owner's uncommitted `GameBalance` edits land.
- **R4. Human playtest.** The GDD still lists this as not done. Use `/playtest-report`, with first-time players through the tutorial.
- **R5. Performance.** `/perf-profile` on a build, mainly the worst combat trigger chains with audio on. `engine-notes` lists per-frame allocations that weren't all reached.
- **R6. Docs.** Add a player journey map (`hud.md` OQ1) and refresh the GDD's status section.

Not planned: localization and gamepad stay out unless the owner asks.

## Owner decisions

| # | Decision | Recommendation | Blocks |
|---|---|---|---|
| 1 | What is this phase aiming at: public demo, full release, or private polish? | Public demo | Scope of Track R and option 5 |
| 2 | Where audio comes from: licensed library, commissioned, Unity AI generation, or a mix? | Library for SFX and placeholders, commission the music if budget allows; check licence terms for any AI output | Phases 1-3 assets |
| 3 | Sound and music direction | Paper-and-ink SFX, dusty crime jazz | A1, B2 |
| 4 | Deck list entry point (OQ7) | A DECK button in the header area or pause menu; `hud.md` calls this the "deck menu screen" | Phase 0 |
| 5 | Add replay systems (unlocks, difficulty tiers, achievements)? | Decide after the first playtest; add one difficulty ladder if replay is weak | Possible later phase |

## Done means

- Every screen uses the Cut Sheet look and its board tone.
- Every listed cue has a final clip, and music covers every state.
- Volume sliders work and persist, and no game information is sound-only.
- Reduce Motion and 2x playback hold on all new work.
- EditMode, PlayMode and soak are green, and a seeded run matches with audio on and off.
- The art licence is settled, and a Windows build has passed a full run and an outside playtest.

## How to run it

Use `/team-audio` for Track A and B, `/team-ui` for the screen slices, `invariant-reviewer` before non-trivial commits, and `unity-tester` for the gate. Keep one commit per slice, as with the HUD work.
