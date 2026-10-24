---
id: kd_25e4f402-bcab-42c5-81cf-37292572126b
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# GitHub issue implementation pass — 2026-10-04

## Scope and disposition

Read open issues from `phamhung1701/Dealbreak` on branch `main` (starting commit `5682d54`). Changes are local, uncommitted, and reviewable. No GitHub comments, issue closures, commits, or pushes were made. Existing dirty settings, Plastic metadata, workspace state, recovery scene, and miscellaneous files were not intentionally edited.

| Issue | Result in this pass |
| --- | --- |
| #24 — hit numbers / critical feedback | Implemented authoritative per-result HP-loss labels, explicit gold `Crit` styling, Shield `Blocked` feedback, multi-hit separation, lethal target-position capture, and cleanup/barrier participation. Critical metadata is passed by combat resolution, including critical-amplified explosions; UI does not recalculate damage. |
| #18 — route map access / readability | Implemented guarded OPEN MAP / CLOSE MAP control, compact labels without descriptions, and stronger links. The control renders/raycasts above the route scrim and remains usable after a temporary reward popup closes. Final victory, active encounters, reset, and blocked input retain guards. |
| #19 — variable choices | Event, Upgrade, discard-selection and artifact-discard choice lists now scroll; rows resize for wrapped text/current viewport width. Descriptions have a separate bounded scroll area. New lists reset scrolling. Event transactions are unchanged. |
| #15 — Block regressions / readability | Added explicit blocking-phase guidance and per-enemy pending damage / BLOCKED from authoritative snapshots. Boss/Ace checks did not establish a combat-validation bug. The user selected preservation of the existing rule: each card must fully cover one attack. Ace blocks 1 damage, not 8. Do not describe this as a change to Block rules or a confirmed regression fix. |
| #20 — Shop interaction / selling / early Map 1 | Selection uses persistent tint + outline rather than a `SELECTED` prefix. Added explicit exact-instance Artifact/Consumable selling for half authored price rounded down, confirmed before removal/Gold grant. Map 1 route depths 0–1 contain no Shops without changing layout RNG consumption. |
| #21 — duplicate Artifact merge | Migrated ownership/effects to stable ordered runtime Artifact instances. Duplicate Tier I/II copies occupy capacity independently; manual rail selection merges two exact same-definition/same-tier instances into the unique canonical next tier with fresh tier state and no Gold cost. |
| #22 — Investment / Guidance | Investment accepts a whole-Gold stake from 1 through current Gold, keeps seeded 50% success and a 3× payout, settles exactly once at the next Shop, and reports wins/losses/payout. Investment and Guidance each use independent seeded 50% Shop availability. |
| #23 — deck inspection / Block cycling | Deck viewer now shows authoritative noninteractive card snapshots in a responsive scrollable layout. Block cards return immediately to the draw deck and shuffle once through the existing seeded card stream; attack cards still enter discard. |
| #16 — Enhancement targeting | Shop, Upgrade, and direct Event/Hammer acquisitions target actual hand CardViews. A different Enhancement replacement requires explicit confirmation, preserves CardInstance/permanent state, clears Enhancement runtime counters, and charges once. |
| #17 — Consumable interaction | Backpack slots support authoritative drag reorder. Enhancement/Rune consumables target the hand directly; Runes select 1–3 cards atomically and have one use. Throwing Knife targets a specific enemy and remains usable during blocking without advancing defense. |
| #13 — authored multi-hit | Added the typed Double Strike Enhancement and included it in the Game catalog. Normal Play/drag derives two full-damage hits from the committed card; one action critical applies to both, Shield is per hit, lethal stops later hits, and card/on-play effects commit once. |
| #4 / #7 — visual pipelines | Per user direction, implemented assignment pipelines rather than final art: optional enemy portrait sprites and Artifact icon sprites with explicit fallbacks, while boss source-card faces keep priority. Added a visual-asset audit command. |
| #5 — defeat cue | Added authoritative lethal flash/fade/shrink presentation and included it in the existing presentation barrier; run-start/disable cleanup cancels retained views safely. |

These are implementation/verification results, not a claim that every GitHub acceptance gate is complete. Human interaction and full automated regression remain pending.

## Principal changed files

- `Assets/Scripts/Combat/DamageRequest.cs`: immutable critical-result metadata.
- `Assets/Scripts/Managers/CombatManager.cs`: critical metadata propagation and read-only per-enemy pending-damage query; no damage/defense balance changes.
- `Assets/Scripts/UI/AttackCardPresentationUI.cs`: resolved-hit feedback and lifetime handling.
- `Assets/Scripts/UI/ActionButtonsUI.cs`, `EnemyDisplayUI.cs`: defense presentation.
- `Assets/Scripts/UI/PathScreenUI.cs`, new `PathNodeButtonUI.cs`: route presentation/control.
- `Assets/Scripts/UI/RunEventUI.cs`: scrollable, width-aware choice/description layout.
- `Assets/Scripts/UI/ShopUI.cs`: selection outline.
- `Assets/Scripts/Managers/CardManager.cs`, `CombatManager.cs`, `RunManager.cs`, and `GameplayEffects.cs`: exact Artifact instances/merges, approved Block recycling, targeted Consumables, Double Strike, and flexible Shop services.
- `Assets/Scripts/UI/DeckViewerUI.cs`, `EnhancementTargetUI.cs`, `ConsumableSlotDragUI.cs`, `ArtifactRailUI.cs`, and `ShopUI.cs`: the second-batch interaction flows.
- `Assets/Scripts/Data/EnemyTypeData.cs` and `RelicData.cs`: optional authored sprite pipelines with existing fallbacks.
- Authored data: four Suit Runes now have one use; new `Assets/Data/Enhancements/DoubleStrike.asset`; `Assets/Scenes/Game.unity` includes Double Strike in the Enhancement catalog.
- Added/updated focused EditMode test source across combat, Artifact, Shop, map, deck-viewer, targeting, Consumable, and visual-pipeline fixtures.

## Verification actually performed

- Unity compilation and domain reload completed after the final source/test changes.
- Final Unity Console error query: zero errors.
- `git diff --check`: passed.
- Staged Game-scene checks at the existing 1920x1080 Game viewport, using actual runtime components and programmatic commands/callbacks:
  - Non-critical lethal hit reported actual HP loss (3), retained its label after target removal, and entered defense normally.
  - A guaranteed-critical two-card Ace action produced one resolved-hit event and one visible `Crit 3` label, not one report per animated card.
  - Direct two-hit action produced two result events/two labels; reset cleared feedback and busy state.
  - Final-enemy lethal hit deferred route display until feedback completed; 40-card zone conservation held.
  - Map button raycast hit precedes PathPanel; close/reopen works at route selection, including after the consumable-reward modal is declined, and hides during combat.
  - Boss smoke: Ace accepted against 1-damage attack; rejected against 8 without consuming the card.
  - Two-enemy defense smoke: blocking the 5-damage enemy showed BLOCKED while the other enemy and aggregate pending damage remained 3.
  - 40-card artifact-discard selection + Cancel (41 rows) produced content height 4492 in a 300-high viewport; last Cancel remained reachable at scroll bottom. Long description stayed in its own bounded viewport.
  - Reducing choice viewport width to 360 expanded a long row from 285.56 to 613.28, rather than overlapping its successor.
  - Visual draw-deck viewer displayed all 32 authoritative remaining cards, with exact CardInstance identity and no interactive/raycastable snapshots.
  - Hand-field Enhancement purchase selected an actual hand CardView, charged once, closed targeting, and restored the Shop; the compact prompt no longer covers the hand.
  - Block commit moved the exact card into the deck, increased deck count once, left attack discard unchanged, and preserved ownership conservation.
  - Duplicate Artifact rewards produced distinct runtime IDs and independent capacity entries; the rail selected two exact instances and merged them to Tier II without changing Gold.
  - Investment accepted an 11g stake and produced an exactly-once seeded loss notification; Shop offer lists now scroll with readable row heights.
  - Selling removed the selected exact Artifact and granted floor(authored price / 2) once; the Sell control and rows are attached to the Shop card rather than the fullscreen overlay.
  - Double Strike used the normal Play path and resolved two full hits (10 total from a 5-damage card) while discarding the card once.
  - A Club Rune selected three hand cards, changed all three atomically, and consumed its single use.
  - Throwing Knife damaged the selected enemy during `EnemyAttacking`, stayed in defense state, and left pending damage unchanged.
  - Lethal enemy presentation remained busy with one retained view during the cue and cleared the view/barrier after completion.
- Screenshots were inspected for route links, critical feedback/defense state, and long-description/last-choice layout. Temporary test state was not saved; Editor was returned to Edit Mode.

### Automated-test limitation

The test-tool setting remains unavailable, and the user explicitly asked to continue without it. Full EditMode/PlayMode NUnit suites were therefore not executed. Unity compilation/domain reload completed repeatedly after integration, final Console error checks returned zero errors, `git diff --check` passed, and staged runtime checks are listed above. Do not describe those checks as a passing automated suite.

## Follow-up decisions and remaining work

- #4/#7: sprite assignment pipelines and fallbacks are complete, but final enemy portraits and Artifact icons remain unassigned by the user’s pipeline-only decision.
- #6: current source already contains the expanded typed enemy slice (Shieldbearer, Brute, Duelist, War Drummer, Goblin Captain, Royal Knight, and Master Thief) with focused encounter/ability tests; no new roster was added in this pass.
- #8/#9/#10/#11/#12: physical mouse feel, non-16:9 human viewport validation, a meaningful recorded 12-map playtest, evidence-led balance review, and final human acceptance remain inherently manual. Staged callbacks/screenshots do not close those gates.
- Final automated NUnit suites remain unrun because the Locus test-tool workspace setting fails to load.

## Human follow-up

At 16:9, 16:10 and 4:3, verify map close/reopen after victory and after claiming/declining rewards; long choice and description wheel/drag scrolling; Shop outline visibility; critical/multi-hit readability; two-/three-enemy defense text; and pause/restart during hit feedback. Verify actual mouse feel rather than only invoking callbacks.
