# Dealbreak Final QA Report

## Executive summary

**Release assessment: gameplay regression checks pass; standalone build verification and the remaining manual checks determine final freeze readiness.** This is a stability assessment, not a balance certification or a claim that every content combination was exercised.

Guidance read: `AGENTS.md`, `DEALBREAK_TECHNICAL_HANDOFF.md`, relevant historical sections of `DEALBREAK_DEVELOPMENT.md`, current implementation/tests, and the authoritative workbook's Switch entries. Existing scene/art/font/project-setting changes were preserved. An independent telemetry commit (`aad2ae7`) arrived during this work and was not rewritten or included as a QA change.

Testing was initially blocked by missing `Locus/config.json`. Restoring the existing registered workspace ID and enabling test tools through native Locus settings repaired discovery. This local workspace repair is not gameplay code and is excluded from the QA commit.

## Confirmed bugs and fixes

| ID | Severity | Root cause / reproduction | Fix and verification |
|---|---|---|---|
| QA-01 | P2 | Obtain any Switch Trick; description/workbook promise three charges, but all four assets granted one. Historical changes and tests disagreed. | User explicitly selected **three charges**. Set only `uses` to 3 on the four Rune assets via Unity APIs. Charge consumption, cancellation, invalid-target and stack tests executed. Existing up-to-three-hand-target activation behavior was preserved. |
| QA-02 | P2 | Start an authored Goblin Captain encounter with Scrapper companions. Companions lacked the intended +1 attack aura because combat checked the old display name `Goblin`. | Resolve the existing companion identity through its stable asset name, retaining the legacy runtime-name fallback. All 11 `Phase2EliteCombatTests` passed, including scaled aura values and captain death. |
| QA-03 | P2 | Inspect a Captain or War Drummer route. Captain lookup searched the renamed display name; Drummer preview attempted to find an enemy named `2 Goblins`. Totals omitted companions. | Reuse `FindNormalEnemyExact("Goblin")` for previews; keep count separate from identity and use the authored display name in text. Added Drummer total assertion; Captain scaled-preview tests pass. |
| QA-04 | P2 | Inspect Silence above 50% HP: detailed guidance appended both active and inactive messages. | Changed the inactive branch to `else if`. Added a PlayMode regression checking both sides of the health threshold; focused test passed. |
| QA-05 | P2 | At 1280×800, pre-existing uncommitted sort-button offsets placed Sort Rank/Suit over HP. | At the user's explicit direction, the existing runtime layout now stacks both controls in the unused action-bar space left of Attack, using responsive anchors. The real-scene regression asserts no overlap with HP/Attack; button callbacks and before/after captures were checked. Other scene edits were not overwritten. |

No confirmed P0/P1 save corruption, progression softlock, or duplicate reward bug was found in the executed checks. That statement is limited to observed coverage.

### Test-infrastructure corrections, not gameplay changes

- The previously unrun disk-error EditMode test used a PlayMode-only scene API, then assumed runtime `OnEnable` executes in EditMode. It now temporarily names the runner-owned scene and initializes the writer dependency explicitly, restoring scene name afterward. Real disk failure and exit policy now pass.
- Updated obsolete assertions for removed tagline objects, two mandatory shop checkpoints, a shared Pass/Take Damage button, duplicate Gear instances, and the current hand-targeting policy. Corrected a duplicate node ID in the Intel fixture and an empty-hand Hammer retry fixture.
- Updated Royal Guard UI tests to inspect `DetailGuidance`, rather than assuming expanded text is always rendered in the compact combat label.
- The new real-scene test fixture now destroys its persistent gameplay singleton managers at teardown, but preserves the application bootstrap, which is initialized only once per Play session. Initial suite integration exposed both singleton contamination and over-aggressive bootstrap cleanup; these harness errors were corrected and the complete 10-test PlayMode suite subsequently passed.
- The first full-run test's explicit `LogAssert.NoUnexpectedReceived` promoted an unrelated Unity AI account-connectivity warning to failure. Removed that blanket warning assertion; ordinary unexpected error/exception failure handling remains enabled.

## Tests actually executed

### Result chronology

| Stage | Result |
|---|---|
| Prior hardening fixtures, initial run | **FAILED:** 101 passed, 1 failed (invalid EditMode scene setup). |
| Disk-error fixture after corrections | **VERIFIED:** 1 passed. Intermediate changed attempts exposed scene-addition and uninitialized-writer fixture problems; these were fixed before each retry. |
| Initial broad request (`edit|play`) | **FAILED:** 642 passed, 14 failed, 1 skipped out of 657 reported cases. This did not establish PlayMode coverage; explicit separate mode runs were subsequently used. |
| Changed EditMode fixtures | 66 passed, 3 failed; the newly exposed failures identified the real Captain aura rename bug. |
| Captain fixture after fix | **VERIFIED:** 11 passed, 0 failed. |
| Switch UI/command fixtures after approved charge change | **VERIFIED:** 27 passed, 0 failed. |
| First full-run test | Gameplay reached terminal victory and second-run assertions, but test **FAILED** on unrelated Unity AI warning. Not counted as a passed test. |
| First explicit PlayMode suite | 5 passed, 4 failed; real full-run and interrupted-combat tests passed. Follow-up isolated harness singleton contamination and an obsolete compact-UI assertion. |
| Focused Royal Guard/Silence PlayMode after corrections | **VERIFIED:** 2 passed, 0 failed. |
| Final EditMode suite | **VERIFIED:** **656 passed, 0 failed, 1 skipped** / 657; run ID `39f826fd8ebd447ab77c6d0470f6e6d9`, duration 77.987 s. |
| Final PlayMode suite | **VERIFIED:** **10 passed, 0 failed, 0 skipped**; run ID `b619ebbb59644c3c9dec0f52f7159eef`, duration 104.291 s. |
| Late user-requested sort-layout fix | **VERIFIED:** affected real-scene layout/interrupted-restore regression passed, run ID `936393e1864d4d3d81cf459ce39a9093`. This focused check ran after the full suites; the full suites were not rerun for this final layout-only change. |

The skipped EditMode case is `GameSceneReadabilityTests.ImportantLabelsAndPrimaryButtonsFitTheir1920ReferenceRects`: `Canvas/PlayerArea/SelectionLabel` has no authored preview text. It is **NOT TESTED**, not a pass. No unchanged failing case was repeatedly rerun to seek a lucky result; retries followed code, fixture or prerequisite changes.

### Executed regression coverage

The final EditMode suite includes attacks, multi-target selection, Ace/critical rules, Gear/Mod reactions, blocks/Perfect Block, incoming damage/defeat, pass/recover, card zones/draw/recycling/duplication, enemy mechanics, shop capacity/transactions/quantity/services, contracts, reward idempotency, run progression, save validation/recovery and frontend interaction fixtures. This is automated fixture coverage, not a claim that every one of those interactions was mouse-driven in the shipping scene.

Existing PlayMode fixtures cover pointer selection/input locks, non-rightmost enemy drag targeting and fade, overflow action ordering, shielded multi-hit, Royal Guard telegraphs, dev modal controls, and inactive-parent scene-teardown behavior. New real-scene tests are in `Assets/Tests/PlayMode/FinalPortfolioRunPlayModeTests.cs`.

## Full-run coverage — VERIFIED with explicit limitations

Seed: `FINAL-QA-12-DISTRICTS`. Real `Game` and `MainMenu` scenes, production route/combat/shop/upgrade commands and normal save pipeline. The run exercised:

- 12 Districts, 63 encounters including 13 Enforcers, 24 Black Markets, and all 12 Headliners.
- Real card attacks, incoming responses, empty-hand Recover, card recycling, enemy abilities, theft/flee, critical Mod effects and encounter rewards.
- Repeated route/close commands, card-zone count/identity/ownership invariants, nonnegative Credits and route reachability checks during the loop.
- Eleven Headliner-boundary checkpoint → MainMenu → Continue transitions, checking map index and serialized card/inventory state.
- All 12 face-card rewards, a 52-card final collection, terminal active-save deletion, return to MainMenu and another 40-card run.
- Waiting for actual enemy defeat presentation before subsequent route selection. The separate drag/fade test checks the disappearing enemy's original lane and release of the presentation barrier.

**Limitations:** the test grants 100,000 HP through `ConfigurePlayer` solely to isolate progression from balance; dev-mode save/economy bypasses are not enabled. It chooses a deterministic attack-heavy route and calls gameplay commands rather than simulating every physical pointer action. It visits/closes shops but does not purchase every offer on that run; focused transaction fixtures cover buying/selling, quantities, Quest, Supply, cancellation and stale/repeated input. It is not proof that a normal-HP player can win this seed. Final-victory visual timing still merits human review.

## Save/load stress coverage

- **VERIFIED in real PlayMode:** begin an encounter, commit an enemy response, attempt an unsafe checkpoint, exit to MainMenu and Continue. The entire safe snapshot is compared after restore, excluding elapsed timing only: HP, Credits, card identities/order/zones, Gear/Tricks/Mods, route, effect counters and RNG state roll back together.
- **VERIFIED in real PlayMode:** eleven map-boundary restores during the full run; final-victory save deletion; starting another run afterward.
- **VERIFIED by EditMode fixtures:** round-trip state, individual charges, shop transaction state, backup recovery, missing/corrupt primary, validation rejection, deletion tombstones, terminal states and failure-safe exit policy.
- **NOT TESTED exhaustively:** OS process termination at every write instruction, disk-full/permission variants on every filesystem, actual power loss, platform migration and all save-version combinations. Sequential restore still has no general rollback for an unexpected exception after validation.

## UI and build verification

### UI — VERIFIED, bounded coverage

- Captured the real route view at 1920×1080 (16:9): selectable starting nodes highlighted, hand visible in a compact strip, no duplicate overlay observed.
- Captured multi-enemy combat at 1280×800 (16:10): found the HP/sort overlap, then verified its correction. Eight cards, all three enemies, HP and the action bar remain visible. Invoked Sort Rank, Sort Suit and Pass through their actual `Button.onClick` callbacks; the enemy-response state had 6 pending damage and eight cards afterward.
- Evidence: `Docs/FinalQA/map-1920x1080.png`, `Docs/FinalQA/combat-1280x800-before.png`, and `Docs/FinalQA/combat-1280x800-fixed.png`.
- The later 1366×768 viewport switch was interrupted by an Editor restart before capture; it is **NOT VERIFIED**. No claim of exhaustive hover, click interception, every aspect ratio or physical drag testing is made. Existing automated pointer/drag tests passed.

### Build

Standalone Windows64 support is installed. First attempt was rejected because Unity prohibits build output under `Library`; it returned no BuildReport. The output was corrected to the ignored deliverable directory `Builds/FinalQA/Dealbreak.exe`. Final result pending.

## Remaining issues and manual testing required

1. **P2 documentation/design mismatch — unresolved target semantics:** Switch descriptions/workbook say one card per charge and any owned card outside combat; current implementation and current targeted-UI tests allow 1–3 hand cards per activation, including outside combat. This pass changed only the charge count explicitly approved by the user. Reproduce by opening a Switch target picker with three eligible hand cards, selecting all three and confirming; one of its three charges is spent. Decide whether to update the documented rule or change targeting in a separately reviewed change.
2. **Design question, not a newly proven defect:** prepared attacks from a defeated attacker can remain when other enemies survive. Existing defense logic treats pending responses as snapshots. Decide cancellation policy before changing this behavior.
3. Complete a normal-HP, player-driven run and assess difficulty, tutorials and readability. Exercise final boss fade/result timing by eye.
4. Test the built executable on the intended portfolio machine: cold start, display/fullscreen changes, dragging, hover tooltips near edges, pause/save/continue, audio/input devices and alt-tab.
5. Run broader seed/content combinations, repeated restart soak, long infinite-mode sessions and crash-recovery fault injection. One seeded run plus fixtures is not exhaustive.
6. The authored preview-label fit test remains skipped. Runtime screenshots supplement but do not replace complete dynamic-text layout testing.

## Scope and reproducibility

Re-run EditMode and PlayMode separately using Unity Test Framework. The real-scene tests use unique temporary save directories and clean them up; the player's default save is not used. No art style redesign, new gameplay feature, broad refactor or arbitrary balance change was made. The only content-value change is the user-approved Switch charge count. Pre-existing dirty assets/settings and independent telemetry work remain outside the QA commit.
