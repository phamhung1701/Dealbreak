# Dealbreak Development Handoff

> **Internal rename update (2026-10-13):** The runtime assembly is now `Dealbreak.Runtime`; Gear uses `GearData`/`GearRuntimeInstance`, and progression APIs use Headliner. This document is a historical implementation snapshot; see `Docs/Dealbreak_Internal_Rebranding_Report.md` for the current API and compatibility boundary.

> Historical implementation and architecture notes. The confirmed Dealbreak GameData workbook is authoritative for player-facing content names and terms; identifiers and mechanics below reflect the documented implementation snapshot and are not automatically migrated by workbook renames.

Current implementation baseline when this handoff was created: branch `main`, commit `3c1e643` (`feat: complete phase 3 boss progression`). Unity project: Unity 6000.3.24f1, URP, New Input System.

## 1. Game Concept

CardPG is a card-based roguelike/RPG inspired primarily by Regicide's card-combat foundation and Balatro's build progression, rule modification, and run variety.

Baseline combat is single-card play. The player uses cards to attack and may discard cards to reduce incoming damage while also managing persistent player HP. Poker hands and other multi-card combinations are **not** baseline combat. They may later exist as optional, unlockable build archetypes provided by Artifacts, encounters, or other rule-changing effects.

The current prototype has one playable character. Avoid building a large character framework until additional characters are actually required.

## 2. Core Design Rules

These decisions are established and must not be casually reinterpreted:

- A normal run contains **12 major maps**, each ending with one face-card boss.
- Maps 1–4 use the four Jacks, maps 5–8 the four Queens, and maps 9–12 the four Kings.
- Suit order is randomized independently within each rank group each run.
- The player starts without the 12 face cards.
- The starting collection is exactly Ace through Ten of all four suits: **40 cards**.
- Defeating a boss awards that exact rank-and-suit face card for the remainder of the run.
- The twelfth and final King reward is granted before the run completes.
- Current hand capacity is eight cards.
- Each combat encounter attempts to add exactly one card, subject to hand capacity. There is no automatic per-turn draw.
- Player HP persists between encounters and resets on run reset.
- Defense is optional. Defensive discards reduce pending incoming damage.
- The player may explicitly accept the remaining incoming damage through **Take Damage**.
- **Recover** is available with an empty hand during `PlayerTurn`. Recover applies one unblocked current enemy attack and, if the player survives, draws one card without causing a second retaliation.
- Suit powers are Artifact-gated rather than baseline card behavior.
- Poker/multi-card combination gameplay is not baseline combat and may later become an unlockable build archetype.
- One playable character is sufficient for the current prototype.

## 3. Current Architecture

### Architectural contract

Gameplay state must not be derived from Unity UI hierarchy, child counts, or Transform placement.

Card ownership and card zones are authoritative runtime model state. UI is presentation/input only: it may observe state and issue commands, but it must not become the source of truth.

### Runtime responsibilities

- **`CardInstance`** — One owned runtime card with a stable per-run unique ID, immutable `CardData` definition, at most one `CardEnhancementData`, and run-local Enhancement effect state for future counters/scaling. Shared definition assets are not mutated.
- **`CardCollection`** — Plain runtime model that owns the authoritative `OwnedCards`, `Deck`, `Hand`, and `DiscardPile` zones. It centralizes zone mutations and prevents one instance from occupying multiple zones.
- **`CardManager`** — Run-level card collection/build façade. It owns authoritative card zones, gold, Artifact definitions plus run-owned `ArtifactRuntimeInstance` state, the five-slot Persistent capacity/NonSlot exemption, dynamic typed hand capacity (base eight), and one Enhancement per stable CardInstance. Reset clears all runtime effect state while preserving definition assets and scene references.
- **`RelicData` / `CardEnhancementData`** — Immutable-in-run content definitions with legacy/runtime ID, explicit canonical spreadsheet ID, rarity/tier/parent metadata, typed effect definitions and inline typed Rule Modifier definitions. Existing legacy hook fields are a compatibility adapter only for definitions with no typed effects.
- **`GameplayEffectResolver` / `GameplayEffectSource` / typed contexts** — Shared Artifact/Enhancement foundation for side-effect-free numeric modifiers and deterministic reactive effects. It centralizes attack/block preview and execution, uses composable typed conditions, and routes in-hand attack calculation, AttackCommitted, CardCommitted, PlayerTurnStart, and AttackBlocked reactions through bounded deterministic queues. Typed rule variants cover only approved extra-turn and same-rank Hands rules.
- **`ShopOffer`** — Runtime typed Artifact/Enhancement offer with stable slot-based Enhancement identity independent of the eventual chosen card, cached price, and presentation data.
- **`CardView` / `Field`** — Presentation and input only. CardView displays a `CardInstance`, supports persistent selection, layered hover/selection, and cursor-centered drag. Field owns the responsive centered fan, drag-slot reflow/hysteresis, and visual-only Rank/Suit sorting; Transform order never changes card ownership, CardInstance identity, combat selection rules, or deck/discard zones.
- **`PlayerRuntime`** — Plain mutable player-health state: configured maximum health, current health, reset/full heal, clamped damage, and defeat status.
- **`CombatManager`** — Authoritative encounter state machine. It uses the shared effect resolver for attack/block calculation and reactive content, preserves deterministic action/hit/reaction ordering, exposes one player-turn-start boundary for current turn-entry paths, and retains Ace-pair, defense, Shield/multi-hit, death/reward and one-shot result authority.
- **`EncounterResult`** — Independent `Victory` / `Defeat` terminal result used to decouple combat resolution from UI and run progression.
- **`RunManager`** — Owns seeded run startup/restart, the grouped/shuffled 12-boss sequence, exact boss source-card identity, current generated map, active node, node completion/unlocks, deterministic mixed shop offers, deterministic Upgrade-node Enhancement choices, event resolution, exact face-card rewards, boss index, run completion, and the separate run timing/statistics lifecycle. Selection commits a route; successful resolution completes it.
- **`RunTimingStatistics` / `RunMapSplit`** — Plain runtime telemetry state for scaled active-run elapsed time, completed map splits, the current incomplete map, reset/restart, defeat, and abandonment. It has no RNG or UI authority and supports arbitrary map indices.
- **`RunRandomContext` / `DeterministicRandom`** — Stable run-seed hashing and independently derived deterministic streams. Map, boss, card, and shop random consumption do not share mutable random state.
- **`RunMapGenerator`** — Generates a deterministic branching/merging map for any non-negative map index. Current maps contain combat, elite, shop, event, upgrade, risk, and boss nodes with partial hidden information.
- **`RunContentCatalog` / `RunEventDefinition` / `RunEffectResolver`** — Authorable Phase 4 encounter traits and non-combat choices. Prototype effects are limited to HP/max HP, gold, and card draw operations.
- **`EnemyAbility`** — Authorable encounter-start, post-card, healing, attack-growth, and damage-modification hooks used by elites and suit-specific bosses.
- **`GameController`** — Explicitly creates/configures the runtime managers before dependent UI subscriptions, injects Game-scene references and enemy assets, configures provisional player max HP, and consumes the one-shot frontend seed request before starting the established run initialization path.
- **`GameFlowService` / `FrontendLaunchContext`** — Persistent scene-flow boundary and one-shot launch handoff. They route Main Menu/Game transitions and requested destinations without owning run state or creating a second randomization path.
- **`GameplayMenuCoordinator` / `GameplayInputGate`** — Own the stack of blocking gameplay overlays, time suspension, Canvas/input gating, drag cancellation, and selection restoration. Gameplay managers remain authoritative and defensively reject user commands while blocked.
- **`DisplaySettingsService`** — Validates, persists, and applies the functional prototype display surface: resolution, window mode, and VSync.
- **`Singleton<T>`** — Performs lookup and lifetime enforcement but does not silently create unconfigured managers. Manager creation/configuration is explicit through GameController.

### Current UI responsibilities

- **`ActionButtonsUI`** — Reusable combat action-bar binding: Play uses attack validation, Block uses existing discard-to-defend validation, Sort Rank/Suit invokes presentation-only hand ordering, and Recover remains available with an empty hand. The bottom-right HP panel shows incoming damage and Take Damage contextually; combat guidance remains state-driven.
- **`EnemyGroupUI` / `EnemyDisplayUI`** — One reusable enemy prefab per runtime enemy in a centered responsive row (one to three tested), with enemy identity, HP/attack, guidance, and intact enemy-drop targeting.
- **`RunStatusUI`** — Compact top run/progression/timing state, Gold on the left, hand/draw/Enhancement counts; no gameplay seed or separate Discard HUD. Its former full-name Artifact label stays unused in the Game scene for compatibility with prior HUD tests.
- **`ArtifactRailUI` / `ArtifactIconSlotUI`** — Right-side scrollable owned-Artifact glyph rail, status count from authoritative capacity, hover tooltip and click-to-open detail. Slots are created/reused from `CardManager.ownedArtifacts` on build changes and clear on reset; no duplicate inventory.
- **`DeckViewerUI`** — Left Draw Deck control opens a scrollable modal listing each card currently remaining in `CardManager.deck` (including Enhancement names). No discard-pile viewer/HUD; defense still discards through the original combat command. The viewer closes on run and route/modal transitions.
- **`DrawDeckStackUI`** — Visual-only red card-back stack/count inside that same clickable Deck button. DeckViewerUI still owns opening the viewer; the stack observes authoritative draw-deck changes and fades at zero.
- **`AttackCardPresentationUI`** — Disposable attack and Block card snapshots travel through a separate non-interactive Canvas while gameplay resolves immediately. Play launches directly; a dragged attack winds up from the cursor; Block travels directly to the enemy row with distinct impact color and no lunge. Its shared completion barrier defers map/final-victory presentation until all committed-card visuals finish, without delaying rewards or combat state.
- **`PathScreenUI`** — Builds the current route selection overlay and displays cycle/run-completion state. A combat-presentation completion barrier delays showing the route/final victory panel until committed-card visuals finish.
- **`ShopUI` / `EnhancementTargetUI`** — Shop selects/inspects cached offers before Buy. Enhancement Buy opens a shared owned-card picker; selecting a target spends nothing, and confirmation authoritatively validates and applies/charges once. The same picker supports free Upgrade rewards with Back/cancel.
- **`RunEventUI`** — Displays authored event/risk choices and deterministic free Enhancement choices used by Upgrade nodes; Upgrade Enhancement clicks open the shared target picker without changing Event resolution.
- **`RunSeedUI`** — The former persistent gameplay seed readout is disabled in the Game scene; the authoritative run seed remains on `RunManager` and appears in terminal results.
- **`RunResultUI`** — Displays exact boss-card reward feedback plus reusable terminal victory/defeat results with seed, progression, bosses defeated, gold, Artifact/build details, total run time, completed map splits, an incomplete defeat split, Retry Same Seed, New Run, and Main Menu actions. The final victory overlay waits for combat presentation to drain; run rewards/state resolve immediately.
- **`MainMenuUI` / `FrontendMenuNavigator` / `RunSetupUI` / `CreditsUI`** — Present Main Menu, optional seed entry (blank generates a fresh seed on launch), and editable prototype Credits without owning gameplay state.
- **`PauseMenuUI` / `SettingsMenuUI` / `ConfirmationDialogUI`** — Reusable pause, functional display settings, and destructive-action confirmation presentation. Settings is shared by Main Menu and Pause.

## 4. Completed Development Phases

### Phase 0 — Baseline and Design Decisions

Established the twelve-boss progression, exact run-local face-card rewards, Artifact-gated suit powers, optional card defense, eight-card hand capacity, one attempted draw per encounter, persistent HP, and empty-hand emergency recovery. A local source-control recovery baseline was created before gameplay refactoring.

### Phase 1 — Card State / Initialization Foundation

- Introduced `CardInstance` and `CardCollection` runtime models.
- Added stable unique per-run card IDs and authoritative owned/deck/hand/discard state.
- Card views are generated from model state; Transform children are not gameplay state.
- Added persistent click selection and presentation-only drag/reordering.
- Consolidated run startup into exactly one initialization path.
- Made manager creation/configuration explicit and corrected Game-scene references.
- Reset removes stale cards, views, selection, gold, relic ownership, and generated definitions.
- Verified card conservation, identity, restart behavior, direct Game entry, and Main Menu → Game entry.

### Phase 2 — Combat Foundation

- Added `PlayerRuntime` and persistent player HP.
- Implemented optional defense, pending/residual damage, explicit Take Damage, and full-block behavior.
- Implemented empty-hand Recover with one unblocked attack and one draw on survival, without a second retaliation.
- Encounter start now attempts to add one card rather than refilling the hand.
- Added one-shot `EncounterResult` events and duplicate terminal-result/reward protection.
- Integrated player health and combat actions into the prototype UI.
- Verified partial defense, full defense, residual damage, recovery, lethal defeat, one-card encounter draw, victory reward, and no duplicate results.

### Phase 3 — Twelve-Boss Run Progression

- Starting collection is now 40 Ace–Ten cards with zero face cards.
- Rewarded face cards receive unique runtime IDs and exact rank/suit identity.
- Retained four randomized Jacks → four randomized Queens → four randomized Kings.
- Generated bosses preserve an exact source card rather than relying on display-name parsing.
- Node selection no longer completes nodes or unlocks successors prematurely.
- Victory completes the active node; defeat grants no progression.
- Boss rewards and boss-index advancement occur immediately on victory and no longer depend on shop completion.
- The final King is awarded before run completion.
- Restart restores the intended initial state.
- A full accelerated twelve-boss Play Mode run was verified through final victory.

### Phase 3.5 — Prototype UI Rebuild

- Replaced the legacy Game Canvas with a responsive 1920×1080 reference layout using anchored Unity UI/TMP panels.
- Added a persistent run/resource header, focused enemy panel, central combat feedback, separated player/action area, bottom-anchored hand, and modal path/deck/shop/result overlays.
- Added `RunStatusUI` and `RunResultUI` so resource/progression display and terminal/reward presentation remain separate from combat input.
- Rebuilt `Card.prefab` without its per-card world-space Canvas and removed debug Hover/Drag labels while preserving persistent selection and drag/reordering.
- Rebuilt path/shop button prefabs with readable dimensions, naming, and interaction colors.
- Added exact boss-card reward feedback and final run victory/defeat restart presentation without moving gameplay authority into UI.
- Verified the existing 30 EditMode tests, direct Game entry, Main Menu → Game, card selection/reordering, Play, defense, Take Damage, Recover, status/resource updates, boss rewards, run completion/defeat, restart, and a 16:9 Game view.

### Phase 4 — Map / Encounter Expansion + Fully Seeded Run

- Added one player-visible string seed controlling the complete prototype run.
- Added stable seed normalization/hashing and independently derived streams for boss order, deck operations, map layout/content, and shop offers.
- Removed gameplay use of `UnityEngine.Random`, unseeded `System.Random`, and runtime-dependent string hashing.
- Replaced isolated linear lanes with deterministic five-column, three-row branching maps containing forks, merges, hidden nodes, and a final boss.
- Added proper combat, elite, shop, event, upgrade, risk/reward, and boss node flows.
- Converted the post-combat shop into an explicit route node with deterministic offer ordering.
- Added six authored non-combat prototype events with immediate HP/max-HP, gold, and draw effects; lethal health costs are rejected.
- Activated the enemy ability pipeline with an elite Momentum trait and four suit-specific boss traits.
- Added run-seed display/input, event choice presentation, route connections, node-type styling, and enemy trait feedback to the Phase 3.5 UI.
- Restart now replays the same seed while restoring baseline maximum HP, 40 owned Ace–Ten cards, zero face cards, zero gold, and boss index zero. A separate New Seed action creates a different run.
- Verified 39 EditMode tests, deterministic replay/stream independence, arbitrary map indices, shop determinism, all Phase 4 node flows, elite/boss traits, Main Menu → Game, and an accelerated seeded twelve-boss victory.

### Phase 5 — Build Progression

- Replaced hard-coded suit-relic ID checks with authorable Artifact filters and effect hooks while preserving the four established suit powers.
- Added four new prototype Artifacts for enhanced-card attack/defense synergies, post-victory healing, and bonus gold.
- Added per-instance card Enhancements. One owned card may receive one persistent run-local Enhancement without changing its unique ID, definition, or matching cards.
- Added canonical Tier I Enhancements Sharpened, Hardened (retained runtime ID `reinforced`), Mending and Quickdraw on the shared typed-effect architecture; Hardened adds block and Mending now triggers at player-turn start while its CardInstance is in hand.
- Converted Upgrade nodes into deterministic free Enhancement choices tied to eligible owned card instances.
- Expanded shops to cached deterministic mixed Artifact/Enhancement offers with stable offer identities, exact prices, ownership/eligibility checks, and duplicate-purchase rejection.
- Kept build random generation on independently derived context streams so card/deck random consumption does not alter shop or Upgrade offers.
- Added card and run-header presentation for Enhancement/build state and updated the shop presentation for mixed offers.
- Preserved restart, card conservation, single-card combat, seeded replay, and all Phase 1–4 run/map/encounter contracts.
- Poker/multi-card combat remains intentionally deferred as an optional future archetype.
- Verified 45 EditMode tests plus Play Mode shop purchase, Upgrade choice, enhanced-card/Artifact synergy, same-seed restart cleanup, UI presentation, and clean Console behavior.

### Phase 6 — Regression / Balance / Polish

Completed and verified on **September 17, 2026**.

- Added explicit ATK/DEF values to card presentation, including Enhancement bonuses.
- Added state-specific combat guidance for attacking, defense, empty-hand Recover, route selection, victory, and defeat.
- Added visible reasons for disabled shop and event choices, including insufficient gold, owned/ineligible offers, and lethal health costs.
- Expanded Deck Details with compact owned-Artifact and enhanced-card summaries.
- Added explicit terminal **Restart Same Seed** and **New Random Seed** actions; all run-start paths clear stale result/reward UI.
- Closed Deck Details automatically across route, encounter, event, shop, Upgrade, result, and restart transitions.
- Made numeric Artifact attack stacking acquisition-order independent using a canonical multiplier-then-flat-bonus rule while keeping Spade/Heart/Diamond side effects separate.
- Fixed cached Artifact offers to charge the displayed cached price even if the backing definition changes later.
- Rejected negative spending/prices and improved ordered event-cost validation and failure explanations.
- Added empty-shop and no-eligible-Enhancement Upgrade handling coverage.
- Performed a conservative balance review. The 30 HP baseline, current enemy/boss curve, route cadence, Artifact prices, Enhancement prices, and event values remain the accepted prototype baseline; Golden Compass economy remains a future monitoring point rather than receiving speculative churn.
- Added 7 focused Phase 6 regression tests, bringing the EditMode suite to 52 passing tests.
- Play Mode verified card readability, disabled-choice feedback, build inspection, terminal seed actions, modal cleanup, clean restart state, and Main Menu → Game initialization.

### Phase 7 — Frontend & Menu Flow

Completed and verified on **September 17, 2026**.

- Rebuilt `Assets/Scenes/MainMenu.unity` into a reusable frontend with New Run, Settings, Credits, and Quit. No non-functional Continue, difficulty, or audio controls were added.
- Added a dedicated Run Setup screen with random seed generation, custom seed entry, seed preview, and Back navigation. All launches feed the existing normalized fully seeded run path.
- Added a persistent scene-flow service and one-shot launch context so Main Menu/Game transitions do not duplicate or own gameplay state.
- Added a centralized gameplay modal coordinator with nested Pause → Settings/Confirmation behavior, cached time-scale restoration, Canvas/raycast gating, drag cancellation, selection discipline, and manager-level input guards.
- Added Resume, reusable Settings, confirmed Restart Same Seed, and confirmed Abandon Run actions. Scene transitions defensively restore global runtime state.
- Added functional persisted display settings for available resolution, Windowed/Borderless/Exclusive Fullscreen mode, and VSync, including unavailable-value normalization. Audio remains intentionally absent until an audio system exists.
- Added an editable prototype Credits screen.
- Replaced the terminal overlay with a reusable Victory/Defeat results screen showing seed, map progress, bosses defeated, gold, Artifacts, enhanced-card count, and final reward when available, with Retry Same Seed, New Run, and Main Menu actions.
- Added 6 focused Phase 7 frontend tests, bringing the EditMode suite to 58 passing tests.
- Play Mode verified application Main Menu startup, random/custom seed starts, deterministic same-seed restart, pause/resume, nested settings, confirmation cancel/confirm behavior, blocked gameplay input, abandon-to-menu cleanup, victory/defeat results, all results actions, functional settings presentation, saved clean scenes, and a clean Unity Console.

## 5. Post-Phase-6 Player Playtest Revision Roadmap

The current repository, including the completed Phase 7 frontend/menu flow, approved Phase 7A interaction foundation, and completed frontend seed follow-up, is the implementation baseline. Phase 7B and Phase 7C are implemented; Phase 7D is accepted with the existing five-depth map topology and Shop placement (explicit zero-depth change). **Phase 7E Slices 1–4 are complete.** Preserve the current authored Artifacts, Enhancements, Events, Elites, Bosses, shops, progression, and deterministic-run contracts unless a later revision explicitly changes them.

### Phase 7A — Card Interaction & Selection Foundation — Complete and Approved

Implemented and verified on **September 17, 2026**.

- Hand dragging now uses a temporary visual slot so pickup does not collapse the hand; meaningful horizontal crossings reorder only presentation order, while cancelled/invalid drags restore cleanly without changing authoritative card zones.
- Card selection is ordered presentation/input state with numbered defensive selection, deterministic toggling, drag-set reuse, and cleanup on actions, encounters, modals, resets, and restarts. PlayerTurn permits exactly zero or one selected card: selecting another replaces the prior selection, while clicking the selected card deselects it.
- EnemyAttacking permits ordered multi-card defense selection under the current per-attacker rule: each selected card must fully block one distinct pending attack. Selection rejects insufficient or unmatched additions; deselection remains available. Artifact and Enhancement defense effects resolve through `CombatManager.CalculateCardDefense`.
- The enemy presentation is the context-sensitive card drop target: PlayerTurn drops attack that target, while EnemyAttacking drops defend against that target's pending attack. The target boundary accepts an `EnemyRuntime` and remains compatible with later independently targetable enemies without implementing Phase 7B.
- The separate Play and Discard-to-Defend controls were replaced by one contextual primary action button. The temporary dedicated defense drop zone was removed.
- Defensive actions accept an atomically validated selected-card set through either the enemy drop target or contextual button, consume each accepted card exactly once, and preserve all cards on invalid attempts. Each card blocks at most one distinct attack; excess defense does not carry to another attacker.
- Added 7 focused Phase 7A regression tests, bringing the EditMode suite to 65 passing tests.
- The original Phase 7A Play Mode approval covered the then-current additive defense rule. The September 24, 2026 per-attacker defense revision was code/test verified; its later Game-scene UI-event smoke is recorded in the final milestone pass below.
- Phase 7A received manual player approval and unlocked Phase 7B.

### Phase 7B — Multi-Enemy & Enemy Identity — Complete

Implemented and verified on **September 17, 2026**.

- Combat encounters now own an ordered collection of independently targetable `EnemyRuntime` instances while retaining a selected target for contextual-button input and compatibility with single-enemy content.
- Enemy presentations use three reusable bound slots. Card drops attack or defend against the exact bound runtime enemy, clicking an enemy selects the contextual-button target, defeated/fled enemies are removed, and remaining presentations close ranks.
- Surviving enemies snapshot separate positive attacks for the defense window. Each defense card may fully block one distinct attack; unresolved attacks remain individually tracked for defense, then resolve as one aggregate combat-damage hit when the player takes the remainder. Recovery damage still uses the total surviving enemy attack.
- Normal Goblin encounters create three distinct Goblins. Each has 3 HP, 2 attack, and a secured 3-gold reward; each survivor contributes its own 2 damage.
- Defeating any enemy draws exactly one card. Defeated-enemy gold is secured during the encounter and paid only after encounter victory, so player defeat still grants no encounter reward.
- Thief now has 12 HP, 1 attack, and a 15-gold defeated reward. If still alive after the second completed player turn, it flees, clears the encounter, grants no Thief gold, and does not trigger the kill draw.
- Elite and boss construction remains single-enemy and did not inherit normal Goblin group size or Thief flee behavior. Knight and unrelated enemy/boss balance was unchanged.
- Added 5 focused Phase 7B regression tests, bringing the EditMode suite to 70 passing tests.
- Final Play Mode integration verified Main Menu → Game initialization, three distinct Goblin targets, drag and clicked-button targeting, kill draws, survivor attack totals, 9-gold Goblin victory, Thief timing/no-reward flee, single-enemy presentation, restart cleanup, and a clean Console.

### Playtest Telemetry — Run Timer and Map Splits — Complete

Implemented and focused-verified on **September 17, 2026**.

- Added UI-independent runtime timing statistics for total run duration, completed map splits, and the current incomplete map, without participating in deterministic RNG.
- Timing starts fresh for every new run, same-seed restart, and random-seed run; defeat and abandonment stop the timer without leaking state into the next attempt.
- Scaled gameplay delta time counts route, combat, shop, event, and upgrade participation while explicit Pause contributes no elapsed time.
- Boss completion finalizes the current map split and begins a fresh timer for the next map. Map indices are not capped at twelve.
- Added a compact gameplay header readout for Run Time and Map Time plus terminal Victory/Defeat timing summaries with hour-safe formatting and incomplete defeat labeling.
- Added focused timing, split, restart, arbitrary-map-index, duration-format, and results-summary coverage. Nine directly relevant EditMode tests passed, and focused Play Mode verification confirmed live advance, Pause freeze, resume, and runtime label updates with a clean Console.

### Phase 7C — Ace Pairing — Implemented

Implemented and code-verified on **September 23, 2026**. An Ace pairs with exactly one other card (including another Ace); both are discarded atomically as one action against one enemy. Damage sums the two cards' independently modified attacks, then doubles on a critical. Each card's matching Artifact and Enhancement on-play effects executes once in selection order. The default critical chance is configurable at 25% on CombatManager; successful pair commitments roll an independent `combat-critical` seeded stream, while single cards and invalid pairs do not consume it. Player-turn selection permits a legal Ace pair without enabling general multi-card poker combat. Earlier focused EditMode regressions passed 46/46 and the then-current complete suite passed 111/111. The Game-scene UI-event Ace-pair smoke was subsequently performed in the final milestone pass below. Prototype critical balance remains provisional.

### Pre-Phase-7D Playtest Corrections — Implemented

Implemented and code-verified on **September 24, 2026**.

- Combat and Elite route nodes show only `Combat` or `Elite`; authored enemy identity remains hidden until the encounter begins. Node descriptions and encounter selection are unchanged.
- Enemy retaliation preserves one attack requirement per positive surviving enemy. One card can fully block at most one attack when its defense meets that attack; excess is lost, deterministic auto-matching uses strongest cards against the strongest attacks they can block, partial commits are allowed, and invalid/unmatched batches consume nothing.
- Remaining unblocked enemy attacks still resolve together as one `EnemyAggregate` damage hit, preserving the existing one-charge Shield behavior.
- Focused map/defense and related 7A/7B/7B.5 regressions passed **44/44**; the then-current complete EditMode suite passed **121/121** and discovered PlayMode suite passed **1/1**. Later UI-event Game-scene checks of generic node wording and per-attacker defense are recorded in the final milestone pass below.

### Phase 7D — Map Revision — Accepted with Current Topology

- Distant Elite nodes remain revealed and retain identifying color even before they are accessible. Hidden Event/Risk nodes remain hidden. Focused map tests passed 8/8 and the complete EditMode suite passed 113/113 on September 23, 2026.
- **Approved September 24, 2026: explicit zero-depth change.** Keep the current five route depths; do not add the proposed 2–3. Combat presentation/animations are still planned and expected to increase actual encounter/run duration, so expanding map topology preemptively is not warranted.
- Keep existing map topology and Shop placement unchanged. No Investment/Betting or unrelated economy changes. Phase 7D is accepted on that basis; no additional shop-access/affordability guarantee is asserted. The twelve-map pacing study is intentionally deferred, not a blocker for this prototype milestone. Final interactive checks have their own evidence and limits below.

### Phase 7E — Shop / Build UX — Complete (Slices 1–4)

Implemented and verified on **September 24, 2026**. Clicking an offer now selects it without purchasing, shows its cached price/description/availability in a compact Shop detail area and gives the selected offer a persistent tint/label. A separate **Buy Selected** button confirms through `RunManager.PurchaseShopOffer`, which rechecks the active uncompleted Shop node, cached offer/price, Artifact identity/ownership, Enhancement target eligibility, Gold and existing input/purchase guards. Failed attempts spend/grant nothing and show the existing unavailable reason. Shop refresh, close, node completion and run reset/restart clear selection; a purchased or otherwise unavailable offer cannot retain a stale selection. Existing offer generation, target selection, economy and map topology are unchanged.

- New `Phase7EShopSelectionTests`: 7/7 passed for inspect/switch with no spend/grant, successful cached-price Buy, insufficient Gold, duplicate Artifact, ineligible Enhancement, removed cached offer, invalid cached price and close/restart cleanup. Focused Shop tests: 12/12; full EditMode: 128/128.
- Game-scene Play Mode smoke: inspected and switched offers at 0 Gold, confirmed no Gold/item change until Buy, observed insufficient-Gold reason, bought an Artifact for its displayed 15g (50g → 35g), observed duplicate rejection with no extra charge, checked selected-state/detail/button presentation and zero Console warnings/errors. This was an accelerated Shop-node smoke, not a full twelve-map playtest.
- Files changed for this slice: `Assets/Scripts/UI/ShopUI.cs`, `Assets/Scripts/Managers/RunManager.cs` (purchase validation guard only), `Assets/Scenes/Game.unity` (Shop-only Buy/detail layout and references), `Assets/Tests/EditMode/Phase7EShopSelectionTests.cs` and generated `.meta`; roadmap/handoff documents updated. No later 7E slice was started.

**Slice 2 implemented and verified September 24, 2026.** Shop Enhancement offers and free Upgrade choices retain deterministic Enhancement identity without embedding a final target CardInstance. Shop Buy opens a shared scrollable owned-card picker; selecting/switching/back never spends Gold. Confirm atomically revalidates Shop node, cached offer/price, affordability and chosen owned unenhanced card, then charges exactly once and applies to that exact card. Upgrade choices use the same explicit target selection and confirmation while remaining free; the node completes only on a successful application. Ineligible cards remain visible but distinguished with reasons; a failed confirmation leaves the offer/node and Gold intact for another choice. Picker state clears on close and run reset/restart. No target-choice RNG consumption, Enhancement-balance, Shop placement or other gameplay changes.

- Files changed for Slice 2: `Assets/Scripts/Data/ShopOffer.cs`, `Assets/Scripts/Managers/RunManager.cs`, `Assets/Scripts/UI/ShopUI.cs`, `Assets/Scripts/UI/RunEventUI.cs`, new `Assets/Scripts/UI/EnhancementTargetUI.cs` (+ `.meta`), the target-picker portion of `Assets/Scenes/Game.unity`, updated `Assets/Tests/EditMode/Phase5BuildProgressionTests.cs` and `Assets/Tests/EditMode/Phase7EShopSelectionTests.cs`, new `Assets/Tests/EditMode/Phase7EEnhancementTargetTests.cs` (+ `.meta`), and handoff/roadmap documents. Pre-existing dirty-worktree changes remain user-owned.
- Eleven new Slice 2 EditMode tests passed 11/11; focused 7E/Phase 5 tests passed **24/24**, full EditMode passed **139/139**. Tests cover explicit target and same-rank/suit instance independence, stale/removed/already-enhanced targets, retry without double charge, cached price, single payment, free Upgrade completion, cancel/reset, and target-independent deterministic offer/map signatures.
- Game-scene Play Mode Shop and Upgrade smokes: inspected/select-switched, cancelled before purchase, rejected an invalidated target without Gold loss, then confirmed a different card and charged the cached 15g once (40g → 25g). Upgrade cancelled without completing; rejected an ineligible card and applied to another owned instance for zero Gold. Picker displayed 40 owned cards with eligibility/selection feedback; Console warnings/errors **0**. These were accelerated scene smokes, not a full twelve-map playtest.

**Slice 3 implemented and focused-verified September 24, 2026.** The normal combat HUD now shows a compact, bounded, scrollable list of all owned Artifact icons/names directly from `CardManager.ownedArtifacts`, beneath the run header and above enemies. It refreshes through `RunStatusUI`'s existing `OnBuildChanged` subscription and the existing run/reset notifications; the zero-owned state says “None owned.” No second inventory, arbitrary display cap, Artifact rule/capacity, or Deck Details changes. Authored descriptions remain in Artifact data; no separate tooltip/inspection framework was added.

- Slice 3 files changed relative to this session: `Assets/Scripts/UI/RunStatusUI.cs`, the owned-Artifact strip and serialized label binding in `Assets/Scenes/Game.unity`, new `Assets/Tests/EditMode/Phase7EArtifactHudTests.cs` (+ generated `.meta`), and the three handoff/roadmap documents. All other pre-existing dirty changes were preserved.
- Focused EditMode **5/5 passed**: empty display, acquisition event updates, all eight Artifact names/icons, `CardManager.Reset`, and seeded run start/restart. The full EditMode suite was **not run**, as requested for this isolated UI slice; the previous Slice 2 full-suite checkpoint remains 139/139.
- Game-scene Play Mode smoke entered a Combat node and viewed 0, 1, then all 8 authored Artifacts. All eight were identifiable in the bounded strip at the captured Game-view size, with hand, enemy and action controls unobscured. Restart showed 0 again, still with 40 starting cards. Console warnings/errors: **0**.

**Slice 4 implemented and Phase 7E accepted September 24, 2026.** `CardManager` defines the single base capacity of **5 occupied persistent Artifact slots**, counted from `ownedArtifacts`. All eight existing authored Artifacts default to Persistent. `RelicData` has a category flag for future **NonSlot** temporary/system/quest Artifacts (including a possible Contract); none is currently authored, and no temporary-Artifact system was built. Duplicates are rejected first. At five occupied slots, a sixth persistent acquisition is rejected before spending or modifying ownership; nothing is replaced, sold or removed, and there are no capacity upgrades. `RunManager` surfaces **Artifact Capacity Full** on cached Shop offers without hiding them. The existing select → inspect → Buy flow and cached prices remain intact; Enhancements remain purchasable at full capacity. The existing combat strip and header show occupied slots as `0/5` through `5/5`; the list still shows all owned Artifacts, including future NonSlot items.

- Files changed for Slice 4 relative to this session: `Assets/Scripts/Data/RelicData.cs`, `Assets/Scripts/Managers/CardManager.cs`, `Assets/Scripts/Managers/RunManager.cs`, `Assets/Scripts/UI/RunStatusUI.cs`, updated `Assets/Tests/EditMode/Phase7EArtifactHudTests.cs` and `Assets/Tests/EditMode/Phase7EShopSelectionTests.cs`, new `Assets/Tests/EditMode/Phase7EArtifactCapacityTests.cs` (+ generated `.meta`), and the three handoff/roadmap documents. No scene or authored Artifact asset needed modification; existing dirty scene and other user changes were preserved.
- Focused Phase 7E/Phase 5 EditMode gate passed **35/35**; full EditMode regression passed **150/150**, zero failures/skips. Tests cover fifth/sixth boundaries, Gold/inventory safety, duplicates, NonSlot exemption, cached Shop offer and unavailable reason, selectable over-cap offer, Enhancement at full capacity, HUD count, and reset/restart/new run. All eight authored Artifact assets were checked in Editor as Persistent.
- Accelerated Game-scene Play Mode gate: bought four Artifacts through the authoritative model and the fifth from the Shop for its cached 15g (200g → 185g); the sixth Shop offer remained inspectable and showed **Artifact Capacity Full** but Buy spent no Gold and granted nothing. Shop Enhancement targeting still applied for its cached 12g (185g → 173g) at 5/5. Combat HUD visibly listed five owned Artifacts and `5/5` without obscuring the enemy/hand/actions. Same-seed restart restored zero Artifacts, `0/5`, zero Gold and 40 cards. Console warnings/errors: **0**. No full twelve-map gameplay pacing claim is made.

**Phase 7E acceptance:** all four slices passed their specified focused and Game-scene checks; the final full EditMode gate passed 150/150. No further 7E implementation is pending.

### Remaining future work (outside Phase 7E)

- Future Artifact capacity upgrades, replacement/sale and temporary/Contract content are not implemented; the five-slot base rule is complete.
- Investment/Betting is undecided and excluded.

### Approved Frontend Seed Follow-up — Complete

Implemented and verified on **September 24, 2026**. Run Setup now opens with blank optional seed entry; blank (including whitespace) generates a new seed only on Start, not the fixed `CARDPG` fallback. Explicit custom seeds retain the established trim-only normalization, and the existing Randomize button still supplies a generated seed when requested. The existing one-shot `GameFlowService` → `FrontendLaunchContext` → `RunManager.StartRunWithSeed` handoff remains unchanged. The Game scene's persistent seed label and updater are disabled without removing run seed data. Victory and Defeat results still read the actual `RunManager.RunSeed`; Retry Same Seed uses `RestartRun`, while New Run returns to blank setup.

- Files changed for this follow-up: `Assets/Scripts/Frontend/RunSetupUI.cs`, `Assets/Scenes/Game.unity` (seed label/updater flags only relative to this follow-up), `Assets/Tests/EditMode/Phase7FrontendTests.cs`, `DEALBREAK_DEVELOPMENT.md`, and both roadmap handoff documents. All pre-existing dirty-worktree changes remained user-owned; no gameplay/RNG architecture was changed.
- Focused frontend EditMode **8/8**; relevant frontend/seeded-run/timing/Phase 5/critical regression **39/39**, zero failures/skips. Tests cover blank/fresh seeds, custom normalization and deterministic replay, one-shot handoff, generated-seed Victory and Defeat text, same-seed retry, and a subsequent blank new run not reusing the previous seed. A separate full EditMode suite was not run for this follow-up; the prior Phase 7E full-suite checkpoint remains 150/150.
- Main Menu → blank setup → Game Play Mode smoke produced `D3B53FC8` with the gameplay label hidden; accelerated Combat defeat displayed that seed in Results, Retry retained it, then New Run returned to blank setup and produced `D47D3507`. Explicit `  Share-7E  ` started as `Share-7E`, appeared on Defeat Results and survived Retry. Results were reached through an accelerated encounter defeat, not a complete twelve-map victory playthrough; Victory result seed was checked in EditMode. The Console showed zero warnings/errors at the Play Mode smoke check, but a later final check found one unrelated Unity AI Toolkit account-service warning (`Account API did not become accessible within 30 seconds`); therefore a warning-free final Console cannot be claimed. Earlier combat-input and twelve-map pacing playtests remain Pending and outside this follow-up.

### Final Prototype Regression and Handoff — September 24, 2026

**Scope:** approved Phase 7D, Phase 7E Slices 1–4, and frontend seed follow-up are implemented; this pass changed **documentation only** (`DEALBREAK_DEVELOPMENT.md`, `Locus/knowledge/plan/cardpg-remaining-roadmap-implementation.md`, `Locus/knowledge/plan/cardpg-roadmap-resume.md`). The initial dirty tree, scenes, assets, gameplay code, balance and RNG architecture were not intentionally changed; no files were staged or committed. The remaining roadmap implementation plan is complete **for the currently approved scope**, not for future content or polish.

**Automated:** Unity Test Framework discovered **152 EditMode** tests; full run **152/152 passed**, 0 failed/skipped/inconclusive. It discovered exactly **one PlayMode** test, `Phase7B5CombatPlayModeTests.ShieldedMultiHit_ResolvesAsIndependentRuntimeHits`, **1/1 passed**; no existing PlayMode tests directly cover Ace-pair input, Shop/Enhancement targeting, Artifact capacity or frontend handoff. The suite includes card collection/zones/identity and 40-card setup, twelve-boss J/Q/K progression and exact rewards, deterministic run/map/shop/crit streams, single/paired card and defense rules, multi-enemy behavior, Shield/hit lifecycle, build/Shop/Enhancement capacity and target guards, timing/reset, and blank/custom/one-shot frontend seed behavior. This is test/code evidence, not a fresh twelve-map playthrough.

**Game-scene UI-event smoke (PASS unless qualified):** from Main Menu blank setup, seed `133F8702` launched Game with 40 owned cards and 12 bosses; an available Goblin map button said only `Combat`, and entered three individually bound 3-HP/2-ATK Goblins. Pointer-dispatched enemy/card clicks and action buttons targeted Goblin 3, produced three independent pending attacks; two selected defense cards blocked two distinct attacks, leaving one 2-damage pending attack. Take Damage resolved one aggregate hit, consumed one staged player Shield, left HP 30/30, and returned to PlayerTurn. This preserves independent **block requirements**, not three separate incoming HP hits. In a staged single-target scene encounter, a three-hit card action against two enemy Shield charges produced hit indices 0/1/2, two blocked hits and one 9-HP hit, discarding only one card. Another staged three-hit action against a shielded Goblin killed only its selected target after hit 1, stopped hit 2, retained two other Goblins and emitted no premature terminal result; a staged single-enemy lethal repeat emitted exactly one terminal event/reward and rejected a second action. **Limit:** multi-hit count used `CombatManager.TryPlayCards(..., hitCount: 3)` in live Play Mode; no authored card/player-facing control currently issues three hits, so an authored multi-hit card input smoke is **PENDING**, not a player-UI PASS. The per-hit runtime behavior and scene state passed.

**Ace-pair UI-event smoke (PASS for the exercised path):** ordinary non-Ace selection replaced the previous single card; clicking Ace + a Four selected two, with an attempted third click rejected and a `PLAY ACE PAIR` action label. Button commit consumed each card ID once, logged Mending +2 HP then Quickdraw +1 draw once each in order, and used the first seeded `combat-critical` roll (13.993 < 25%) to double combined damage to 10 against a staged 120-HP enemy. A defense-card click/button then blocked its 2-ATK retaliation; a subsequent ordinary non-Ace single attack dealt 10 without losing the ability to continue. Separate EditMode tests cover Ace+Ace, invalid pair no RNG consumption, and deterministic critical replay. UI events were dispatched through Unity EventSystem and Button callbacks in Play Mode, not a human mouse/drag session; unperformed physical drag targeting remains **PENDING**.

**Compact flow smoke (PASS for the exercised path):** map combat was followed by an accelerated encounter victory to reach an existing Shop node (not a natural economy playthrough). Shop select/inspect spent 0g; Buy paid the cached 20g price for Heart Power from staged 200g → 180g. Four additional existing Artifacts were granted for staging; HUD showed 5/5 and five names. The second inspectable Shop Artifact reported `Artifact Capacity Full` and Buy neither charged Gold nor granted a sixth. At 5/5, an Enhancement offer opened a 40-card target picker; selecting card #1 spent nothing and Confirm applied Sharpened exactly to #1 for its cached 12g (180g → 168g). An accelerated Defeat result showed `Seed  133F8702`; Retry Same Seed restored that seed, 40 cards, 0 Artifacts/Enhancements and 0 Gold. Main Menu → Run Setup then launched custom `  Milestone-Custom  ` as `Milestone-Custom`, with gameplay seed label hidden. Full Victory result seed and frontend one-shot/replay cases passed EditMode; a natural final-boss victory was **not** replayed here.

**Console:** this pass's warnings/errors query returned **0** during and after the Game-scene smoke. The earlier frontend follow-up logged one Unity AI Toolkit account-service warning (`Account API did not become accessible within 30 seconds`); its stack originated in `com.unity.ai.assistant` cloud/account availability, not CardPG gameplay. No CardPG warning/error was observed in this pass. Do not extrapolate to a permanently warning-free Editor or modify gameplay to suppress unrelated service noise.

**Remaining debt / next-development options:** Recover–Shield policy is decided per approved issue #14: one aggregate Recover hit uses applicable incoming combat modifiers, consumes at most one Shield charge if positive residual damage remains, draws once only on survival, and causes no second enemy attack/Retaliation; direct Run/Event HP costs bypass Shield. No authored multi-hit card exists for a player-driven input check; physical mouse/drag interaction and a complete meaningful twelve-map pacing study remain unperformed. The pacing study is deliberately deferred because 7D kept five route depths and future combat animation/presentation may change run time; it does **not** block this milestone. Potential separately scoped next steps include combat presentation/animations, enemy artwork, and playtest-led pacing/Shop decisions. New content, Artifact upgrades/replacement, extra depths, balance and unrelated systems remain future decisions, not this milestone.


### Content Expansion Architecture — Shared Effects + Canonical Tier I Core — Complete

Implemented and verified on **September 25, 2026**.

- Added one shared typed foundation for Artifact and Enhancement numeric modifiers/reactive effects, with deterministic source/effect order, composable suit/enhanced-card conditions, authoritative CardCommitted and PlayerTurnStart contexts, and a separate typed Rule Modifier extension point. No name/ID behavior dispatch or generic scripting language was added.
- Added run-owned `ArtifactRuntimeInstance` state and CardInstance-owned Enhancement state. Shared ScriptableObject definitions remain immutable during play; reset detaches/clears runtime state. Future counters have a home without implementing their mechanics.
- Centralized attack/block semantics for preview and execution. Current order is base + card-local flat, then multipliers, then Artifact flats. This preserves attack stacking and implements `(base block + Hardened 3) ×2` for Spades. Club applies ×2 per qualifying card before Ace-pair summation.
- Added explicit canonical metadata while retaining GUID-safe runtime IDs. Crosswalk: `rel_001`→`club_power`, `rel_002`→`heart_power`, `rel_003`→`diamond_power`, `rel_004`→`spade_power`; `enh_001`→`sharpened`, `enh_002`→`reinforced` (display **Hardened**), `enh_003`→`mending`, `enh_004`→`quickdraw`.
- Migrated only the approved canonical core: Club ×2 per Club card; Heart heals 1 per committed Heart; Diamond fixed Draw 2 up to capacity; Spade block ×2 with no ATK reduction; Sharpened +3 attack; Hardened +3 block; Mending heals 2 once per in-hand instance at every player-turn start and not on play; Quickdraw draws 1 on play. Conflicting Heart recycle, rank-based Diamond draw, Spade ATK reduction and Mending on-play heal are absent from these definitions.
- Preserved five Persistent slots, NonSlot exemption, cached Shop prices/offers, explicit Enhancement targets, one Enhancement per card, zones/identity, Ace pairing, critical RNG, Shield/multi-hit and restart semantics. No Tier II/III, upgrades, rarity distribution, map effects, extra turns, poker rules or other workbook content was implemented.
- Changed implementation/content files: new `Assets/Scripts/Combat/GameplayEffects.cs` (+ `.meta`), `Assets/Scripts/Combat/CombatReactionQueue.cs`, `Assets/Scripts/Data/RelicData.cs`, `CardEnhancementData.cs`, `CardInstance.cs`, `Assets/Scripts/Managers/CardManager.cs`, `CombatManager.cs`, `Assets/Editor/GameDataGenerator.cs` (safety alignment only; not executed), the four existing core Artifact assets, four existing Enhancement assets, and new `Assets/Tests/EditMode/CoreGameplayEffectsTests.cs` (+ `.meta`). Architecture/handoff documents were updated; no scene reference change was required. During the unexpected Editor restart Unity generated untracked `Assets/_Recovery.meta` and `Assets/_Recovery/`; they were not used, modified or removed by the implementation.
- Verification: focused architecture/core content **8/8**; full EditMode **160/160**; discovered PlayMode Shield/multi-hit **1/1**. Game-scene smoke passed Club+Sharpened, Heart, fixed Diamond Draw 2, Quickdraw, Spade+Hardened order/no old ATK reduction, and Mending turn transitions/no play heal. Final smoke Console warnings/errors: **0**.

The detailed contracts and extension guidance are in `Locus/knowledge/plan/cardpg-effect-architecture.md`. Stop before another content batch.

### Artifact Content Batch — Leather Armor, Dagger, Tome, Sword — Complete

Implemented and verified on **September 25, 2026**. Four authored Tier I Artifacts were added through the existing shared typed-effect system and registered in the existing Game-scene catalog: `rel_005` Leather Armor (Common), `rel_006` Dagger (Common), `rel_007` Tome (Common), and `rel_023` Sword (Rare). Spreadsheet prices are blank, so their Shop prices are explicitly **temporary prototype values**: respectively **15g, 15g, 18g, and 25g**; other prices/economy were not rebalanced.

- Reusable extensions only: a typed player incoming-combat reduction applied before Shield; typed action-flat and action-total-cap modifiers; a typed attack-value threshold evaluated after card-local/suit/base modifiers but before its own bonus, caps and criticals; and an EncounterStart Draw reaction ordered after the baseline encounter draw. Existing effect enum values were kept numerically stable for serialized definitions. No ID/name branches, parallel effect engine, new Tier system or additional workbook content.
- Leather reduces each resolved player **combat** damage instance by 3, floored at zero, then Shield handles any remaining positive damage; Event/non-combat HP costs bypass this modifier. **Approved Recover policy (#14):** Recover remains one aggregate combat-damage instance and applies incoming modifiers before checking Shield; positive residual consumes at most one Shield charge, zero residual preserves it. Recover draws once only when the player survives and causes no additional enemy attack or Retaliation. Direct Run/Event HP costs also bypass Shield.
- Dagger adds 3 at the action level after per-card aggregation; a **single action-wide 10-damage budget** is applied before critical doubling. Ace pairs are capped only after summing cards (e.g. Ace + Eight → 10, guaranteed crit → 20). Staged multi-hit actions share that same budget across hits; Shield does not refill it.
- Tome attempts one additional draw after the baseline encounter draw, once per encounter and within the existing hand cap. Sword tests each played card independently against its own pre-Sword attack value (>8) after Sharpened/Club modifiers; qualifying cards gain 10 before action caps and crit. Sword cannot self-qualify.
- Changed for this batch: `Assets/Scripts/Combat/GameplayEffects.cs`, `Assets/Scripts/Combat/CombatReactionQueue.cs`, `Assets/Scripts/Data/PlayerRuntime.cs`, `Assets/Scripts/Managers/CombatManager.cs`, `Assets/Editor/GameDataGenerator.cs` (safety alignment only; not run), four new assets and `.meta` under `Assets/Data/Relics/` (`LeatherArmor`, `Dagger`, `Tome`, `Sword`), four new references in `Assets/Scenes/Game.unity/GameController`, and new `Assets/Tests/EditMode/ArtifactContentBatch1Tests.cs` (+ `.meta`). This handoff, the effect-architecture document and the roadmap resume checkpoint were updated. Existing GUIDs and unrelated user/tool changes were preserved.
- Focused batch EditMode **11/11 passed**; full EditMode **171/171 passed**, zero failures/skips; discovered PlayMode **1/1 passed**. Game-scene smoke used accelerated runtime encounters and verified Tome 6→8 hand after baseline+extra draw, Leather 5→2 HP loss and post-reduction Shield block, Dagger five→8 and Ace-pair capped crit→20, Sword eight→8, ten→20 and Club+Sharpened five→26. The last connected Console warning/error query during the smoke returned **0**. A subsequent final Editor readback lost its Unity connection; the Editor log tail showed external UnityTls certificate-verification errors but no identified CardPG fault, so a post-disconnect clean-Console claim is not made. This is not a natural twelve-map/economy playtest.

**Scope note for the Leather Armor / Dagger / Tome / Sword batch:** its stop condition was Chain Armor, extra-turn/poker/map systems, Tier II/III, rarity-based distribution, new enemies and other workbook content. Subsequent incremental passes selected Chain Armor and Ace Emblem under separate authorization; other content remains unimplemented.

### Artifact Rule Batch — Cheater Emblem, Hands Emblem, Dwarf Emblem — Complete

Implemented and verified on September 25, 2026. Added and registered only `rel_011` Cheater Emblem (Common, Tier 1, temporary prototype price **20g**), `rel_012` Hands Emblem (Common, Tier 1, temporary **20g**), and `rel_013` Dwarf Emblem (Common, Tier 1, temporary **18g**). Spreadsheet prices are blank; these prices are not canonical balance. Their typed rule definitions are inline on the Artifact assets; the Game scene catalog includes all three. No `rel_014` or later content was started.

- Extra-turn grants are queued after a committed player action completes, in Artifact acquisition order then rule-array order. Cheater qualifies after the first completed player turn per encounter; its once-per-encounter counter is in the run-owned Artifact runtime state and is reset on encounter start. Dwarf qualifies when **all** committed ranks are below Five (Ace/1 through Four), grants at most once for that action, and may qualify again on a later granted turn. Each grant remains distinct: when both qualify, Cheater's grant enters the next normal PlayerTurnStart first and Dwarf's independent grant remains queued for the following player turn. Both use the same PlayerTurnStart path as Healing Light/Mending. Terminal results clear queued grants; no recursive combat transition is used.
- Hands permits 2–3 same-rank cards only while owned, rejects a fourth or a mismatched extension, and applies its typed **action-total** 10-damage cap after the ordinary per-card pipeline. The existing two-card Ace-pair legality and critical behavior take precedence and remain unchanged; a three-card same-rank action—including three Aces—is Hands, not a critical Ace pair. Hands and Dagger caps compose through the existing minimum-cap calculation, without a second application. Single-card attacks remain uncapped by Hands.
- Changed for this batch: `Assets/Scripts/Combat/GameplayEffects.cs`, `Assets/Scripts/Managers/CombatManager.cs`, `Assets/Scripts/Managers/CardManager.cs`, `Assets/Scripts/UI/ActionButtonsUI.cs`, `Assets/Scenes/Game.unity` (catalog references), new `Assets/Data/Relics/CheaterEmblem.asset`, `HandsEmblem.asset`, `DwarfEmblem.asset` (+ `.meta`), new `Assets/Tests/EditMode/ArtifactRuleBatchTests.cs` (+ `.meta`), and these development/handoff documents. Existing user-owned and prior-batch worktree changes were preserved; no Git staging or other repository history operation was performed.
- Focused `ArtifactRuleBatchTests`: **12/12 passed**. Full EditMode suite: **194/194 passed**; discovered PlayMode suite: **1/1 passed**. These are the last successful regression results; the Editor bridge disconnected before a fresh final rerun could be made. The last connected Console warning/error query returned **0**.
- Accelerated Game-scene Play Mode smoke: Cheater first completed low-rank action granted its extra turn; Dwarf low-rank action granted an extra turn, and another low-rank action on that turn granted again; a high-rank action proceeded to enemy flow. With both Artifacts, the first low-rank completion queued two distinct grants in Cheater-then-Dwarf order and both entered normal PlayerTurnStart; Healing Light/Mending triggered through that path. Hands accepted two and three same-rank selections, rejected a mismatched third and fourth card, displayed the same-rank action label, and capped damage at 10. A normal Ace pair afterward retained pair UI/critical resolution and did not use the Hands cap. These checks used staged targets, accelerated scene setup and programmatic Unity UI events, not a natural run or human pointer/drag playthrough.

The rel_011–rel_013 batch ended at its approved stop point; the current separate incremental-content authorization has since selected and completed rel_019 Chain Armor, rel_014 Ace Emblem, and rel_016 Retaliation Emblem. Their verification is recorded below.

The September 26, 2026 incremental draft-content pass selected the smallest safe new entry, `rel_019` Chain Armor. Its Rare Tier I typed effect reuses the existing incoming combat reduction and Shield ordering; it adds no resolver type or combat branch. The draft workbook has no price, so the existing Rare prototype price convention supplies temporary **25g**. This is explicitly interpreted as reduction of each incoming **combat** damage instance before Shield, clamped at zero; direct non-combat/run HP costs remain unchanged.

- Added `Assets/Data/Relics/ChainArmor.asset` (+ `.meta`) and registered it in `Assets/Scenes/Game.unity/GameController` (catalog now contains 19 Artifacts). No generator was run and no other workbook content was added.
- Added focused `ChainArmorTests`: **3/3 passed**; full EditMode: **197/197 passed**; discovered PlayMode: **1/1 passed**. Tests cover canonical definition/immutability, per-hit reduction and zero floor, remaining damage passed to Shield, additive stacking with Leather Armor, and direct run HP-cost bypass.
- Serialized Game-scene catalog and asset values were inspected. No dedicated interactive Shop/combat scene smoke was performed for Chain Armor. Final Console warning/error query: **0**; an earlier query during this pass observed unrelated Unity AI Toolkit account-service noise, with no CardPG error/warning.

**Next-content policy:** continue incrementally—verify the exact draft entry and repository state, prefer definitions expressible by existing typed effects, add focused tests, then run regression before choosing another entry. Stop/defer on a regression or unresolved rule ambiguity. Higher-risk overflow/replay, destructive discard, AoE, Tier upgrades, and persistent per-card scaling are not included in this increment.

The September 26, 2026 incremental draft-content pass selected `rel_019` Chain Armor, then `rel_014` Ace Emblem after clarifying its interaction with single-Ace and three-card Hands attacks. Chain Armor reuses the typed incoming combat reduction; Ace Emblem adds a typed card-rank-filtered critical chance override. Neither added an Artifact-ID/name branch.

- `rel_019` is Rare Tier 1 with a temporary **25g** prototype price (workbook price blank). Each player combat damage instance is reduced by 8 before Shield and floored at zero; direct non-combat/run HP costs remain unchanged. It stacks with Leather Armor.
- `rel_014` is Common Tier 1 with a temporary **20g** prototype price (workbook price blank). Any attack action containing at least one committed Ace—including a single Ace, an Ace pair, or a three-card Hands action with Aces—uses a 50% critical chance. The action makes one seeded `combat-critical` roll total, not one roll per Ace. Actions without an Ace and runs without Ace Emblem keep existing critical eligibility and chance behavior. Critical damage remains after the existing action-total cap.
- Assets `Assets/Data/Relics/ChainArmor.asset` and `AceEmblem.asset` are registered in `Assets/Scenes/Game.unity/GameController`; the catalog contains 20 Artifacts. Added focused tests in `Assets/Tests/EditMode/ChainArmorTests.cs` and `AceEmblemTests.cs` (+ `.meta`). No bulk data generator was run and no other workbook row was implemented.
- Chain Armor focused tests **3/3**; Ace Emblem focused tests **7/7**; full EditMode **204/204**; PlayMode **1/1** passed. Final Console warning/error query returned **0**. One unrelated Unity AI Toolkit account-service warning appeared in an earlier query during the preceding Chain Armor pass; no CardPG warning/error was observed.
- These checks were automated; there was no separate interactive Game-scene Shop/combat smoke for either new Artifact.

**Next-content policy:** continue incrementally—verify the exact draft entry and repository state, prefer definitions expressible by existing typed effects, add focused tests, then run regression before choosing another entry. Stop/defer on a regression or unresolved rule ambiguity. More complex overflow/replay, retaliation-on-block, destructive discard, AoE, and persistent per-card scaling are not included in these increments.

### Incremental draft content — Retaliation Emblem (`rel_016`) — Complete

Implemented the workbook's Common Tier I `rel_016` as a typed `CounterDamage` reaction. Price is blank in the workbook; temporary prototype price **20g**. One successful card block triggers one 5-damage counterattack against the exact `EnemyRuntime` whose pending attack that defense card blocked. A multi-card defense triggers once per blocked attack, in stable encounter order; blocking an attack with a player Shield by taking remaining damage is not a card-block trigger. Pending attack snapshots now retain their source enemy so retaliation never depends on current target selection or list re-discovery.

- Added `Assets/Data/Relics/RetaliationEmblem.asset` (+ `.meta`), registered in the Game scene (catalog now contains 21 Artifacts), `Assets/Tests/EditMode/RetaliationEmblemTests.cs` (+ `.meta`), and typed `AttackBlocked` context/queue support. Existing aggregate incoming damage and player Shield consumption remain unchanged.
- Focused Retaliation tests **5/5**; defense/Shield regression **23/23**; full EditMode **209/209**; PlayMode **1/1** passed. Coverage includes exact single/multi-enemy attacker attribution, one reaction per block, Shield-only non-trigger, and lethal-counter Victory resolution exactly once.
- Final Console warning/error query returned **0**. Game-scene asset/catalog were verified; no separate interactive Shop/combat smoke was performed.

## 6. Verified Invariants / Regression Requirements

Future changes must preserve these behaviors unless the user explicitly approves a design change:

- Every owned runtime card has a unique ID.
- Card ownership is conserved across owned/deck/hand/discard operations unless a deliberate system adds or removes ownership.
- UI hierarchy and Transform children are never authoritative card state.
- Reset removes stale runtime state and restores the intended starting state.
- A new run starts with 40 Ace–Ten cards and zero face cards.
- A boss reward adds exactly one matching face card.
- Duplicate boss rewards are rejected.
- Boss progression is grouped Jack → Queen → King.
- Each rank group contains all four suits exactly once.
- Node completion occurs after successful encounter resolution, not selection.
- Defeat does not grant node or boss progression.
- Player HP persists between encounters.
- Encounter terminal results, rewards, and gold cannot be emitted repeatedly.
- Final boss progression cannot index beyond the twelve-boss list.
- Restart restores 40 owned cards, zero face cards, boss index zero, baseline maximum/current HP, and incomplete run state.
- The same normalized run seed plus the same player decisions reproduces boss order, map generation/content, deck operations, shop offers, events, rewards, and combat randomness.
- Random consumption in one named subsystem does not change unrelated subsystem results.
- Map generation accepts arbitrary map indices and is not architected around a hard twelve-map generation limit.
- Card Enhancements belong to one `CardInstance`; applying one preserves that card's ID and does not mutate its `CardData` or another matching card.
- A card currently accepts at most one Enhancement, and invalid/duplicate applications do not spend gold or complete an Upgrade twice.
- Artifact and Enhancement effects share one typed resolver/source model; canonical behavior does not branch on display names or IDs.
- Shared ScriptableObject definitions remain immutable during a run. Artifact state is run-owned; Enhancement mutable state is CardInstance-owned; reset creates/clears runtime state.
- Attack and block preview/execution share one modifier path. Sharpened/Club and Hardened/Spade ordering follows the approved canonical rules.
- The action damage pipeline computes per-card attack (including conditional Sword), sums committed cards, adds action modifiers and applies the tightest action-total cap across hits **before** critical multiplication. Dagger does not change card-defense values or reset its cap per hit.
- Player incoming combat modifiers apply to each resolved hit before Shield; Leather Armor reduces by 3 and Chain Armor by 8, each clamped to zero in the shared incoming-damage stage. When both are owned their reductions stack; Event/non-combat costs bypass this combat-only modifier. Encounter-start Artifact reactions occur after baseline encounter draw.
- Ace Emblem overrides critical chance to 50% for an attack action containing any committed Ace, including a single Ace and a three-card Hands action. There is one critical roll per action, after the existing action-total cap; without the Artifact existing Ace-pair-only eligibility and configured default chance remain unchanged.
- Persistent Artifacts occupy one of five base slots; a sixth cannot be acquired or purchased, costs no Gold, and does not replace ownership. NonSlot temporary/system/quest categories are exempt but still obey duplicate rules. Enhancements are unaffected.
- Shop offers are cached for the active node and are deterministic for seed/map/node regardless of card-stream consumption.
- Upgrade Enhancement choices are deterministic for seed/map/node; the final owned card target is chosen by the player and validated at confirmation, not baked into the offer or RNG.
- Restart clears gold, Artifacts, Enhancements, active build offers, and stale build UI while restoring the 40-card baseline.
- Numeric Artifact attack results do not depend on Artifact acquisition order; multipliers resolve before flat bonuses.
- Shop purchases charge the cached displayed offer price, not a later-mutated definition price.
- Negative prices or spending requests cannot grant content or increase gold.
- Event affordability/lethality validation follows authored effect order and supplies a player-visible failure reason.
- New-run, same-seed restart, and route/modal transitions clear stale terminal, reward, and deck-detail overlays.
- Main Menu/Game transitions use a one-shot seed handoff and the established `RunManager.StartRunWithSeed` path; frontend menus never become authoritative run state.
- Blocking frontend menus suspend time, disable underlying gameplay UI/raycasts, cancel transient card interaction, and prevent manager commands until the final blocking layer closes.
- Nested Settings and confirmation dialogs unwind one layer at a time and cannot prematurely resume gameplay.
- Restart/Retry Same Seed preserves the normalized run seed and deterministic initial map/hand signature; New Run returns to seeded Run Setup.
- Scene transitions and modal teardown restore time scale and gameplay input state.
- Display settings expose only functional resolution, supported display mode, and VSync controls; invalid persisted values normalize to supported values.
- PlayerTurn selection permits one ordinary card or one legal two-card Ace pair by default. Hands ownership additionally permits 2–3 same-rank cards; those are Hands actions, not Ace-pair actions. Without Ace Emblem they do not crit as Ace pairs; with Ace Emblem, any committed Ace—including in a three-card Hands action—enables the separate 50% action-level critical rule.
- Successful defense assignments retain the exact source enemy for each snapshotted pending attack. A typed AttackBlocked reaction runs once per card-blocked attack, targets that source enemy, and resolves counter damage deterministically; player Shield absorbing remaining aggregate damage does not count as a card block.
- Enemy presentations resolve both attack and defense drops contextually through authoritative combat actions; there is no dedicated defense drop zone. Defense drops use the selected batch and deterministic auto-matching rather than assigning a card to the visual target.
- Combat and Elite route labels do not reveal authored enemy identity before entry.
- Encounters may contain multiple distinct `EnemyRuntime` instances; actions validate their exact target, each surviving enemy contributes its attack, and victory occurs only after no active enemies remain.
- Defeating an enemy draws exactly one card. Encounter gold from defeated enemies is paid only on encounter victory and remains unavailable on player defeat.
- Defeat preserves completed timing splits and exposes the current map as incomplete; boss victory records exactly one split and starts the next map timer.
- New runs and both restart paths clear all prior timing data; abandonment stops active timing before returning to the frontend.
- Run timing uses scaled gameplay delta time, remains frozen during explicit Pause, and never affects seeded gameplay outcomes.

- Rule-batch verification on September 25, 2026: focused `ArtifactRuleBatchTests` **12/12**, full EditMode **194/194**, and discovered PlayMode **1/1**. The subsequent `rel_019` Chain Armor, `rel_014` Ace Emblem, and `rel_016` Retaliation Emblem increments are documented above with their focused/full test counts. No claim of interactive scene smoke is made for these increments.

## 7. Known Temporary / Legacy Systems

Working gameplay foundations should be preserved, but the following systems are intentionally temporary:

- The gameplay UI was rebuilt in Phase 3.5; its layout and visuals remain temporary rather than final art.
- The Phase 4 map uses a fixed five-column/three-row prototype template; richer generation rules and additional topology variety remain future content work.
- Seven normal enemies and three authored Enforcers are registered in `PrototypeRunContent`; generic Enforcers use scaled normal-enemy stats plus one authored trait. Allies, map gating and Enforcer eligibility are authored on `EnemyTypeData`.
- Shops now have 22 authored Artifact definitions: 18 canonical IDs (`rel_001`–`rel_014`, `rel_016`, `rel_019`, `rel_023`, and `rel_028`) plus four retained non-workbook prototype Artifacts. Six canonical Tier I Enhancements (`enh_001`–`enh_004`, `enh_007`, `enh_008`) are authored. Recently added canonical batch prices are temporary; Tier upgrades/replacement and rarity-weighted distribution are not implemented.
- One Enhancement per card remains the prototype rule. Replacement, removal, stacking and card-target reroll remain deferred; Shops and Upgrade rewards now let the player explicitly choose a valid owned card at confirmation.
- Upgrade nodes offer deterministic free Enhancement choices; player-selected owned-card targeting replaces the old preselected-card behavior. Older authored health-focused Upgrade event assets remain unused prototype content.
- General poker/multi-card combat remains deferred. The current exceptions are Ace pairing and the Hands Emblem's same-rank 2–3-card action; Ace Emblem can also grant a 50% critical chance to any Ace-containing action. No player-facing authored multi-hit card exists for a physical input smoke, although combat runtime supports staged multi-hit actions.
- Bosses now have suit-specific prototype traits, but rank-specific rules and more elaborate boss restrictions are deferred.
- Multi-enemy combat is implemented for Phase 7B. The current scene provides three reusable enemy presentation slots because authored Phase 7B groups contain at most three enemies; larger future groups would require additional slots or dynamic pooling.
- Player maximum HP 30 is provisional.
- Enemy/boss stats, hand size, prices, and other balance values are prototype tuning.
- Boss rewards currently enter the shuffled draw deck; this may be tuned later.
- Gold, Artifact ownership, and build catalogs still live in CardManager. Phase 8A persistence uses explicit owner-specific capture/restore contracts rather than moving that runtime authority.
- The rebuilt card UI uses suit-tinted temporary panels and text rather than final card art.
- The enemy portrait remains a styled placeholder label rather than authored enemy artwork.
- Seed generation is intentionally local-only; cross-version determinism, networking and cloud seed exchange are not implemented. Phase 8A now provides local checkpoint save/load.
- The frontend has a single-run Continue flow as of Phase 8A, but no profiles, achievements, metaprogression, character selection, fake difficulty or placeholder audio settings.
- Display settings are prototype desktop controls. Additional Audio, Gameplay, and Accessibility categories are structured for later expansion but contain no fake options.
- `Unity6_Recreation_Guide.md` predates the Phase 1–4 implementation and still describes the older 52-card starting-deck prototype. It is historical reference, not current design authority; the repository now starts runs with 40 Ace–Ten cards.

### Phase 3.5 cleanup outcome

- Removed the stale `DeckViewerUI.contentContainer` field.
- Replaced generic button-label and ambiguous status object names with responsibility-specific names.
- Removed `Card.prefab/Panel/StateLabel` debug interaction text and the per-card world-space Canvas, CanvasScaler, and GraphicRaycaster.
- Retained the dedicated screen-space `DragCanvas`, which remains required by card drag presentation.
- Kept `GameController` under `Assets/Scripts/UI/` to avoid unrelated file relocation during the UI-only phase.

## 8. Roadmap Status

### Phase 3.5 — Prototype UI Rebuild — Complete

The temporary gameplay UI was rebuilt without changing the Phase 1–3 gameplay architecture. Further visual polish belongs in later UX/art work rather than Phase 4 system implementation.

### Phase 4 — Map / Encounter Expansion + Fully Seeded Run — Complete

Phase 4 provides deterministic run replay, branching/hidden maps, proper route-node resolution, representative normal/elite/shop/event/upgrade/risk/boss content, and extensible authored enemy hooks. The implementation deliberately avoids card enhancements, new artifact progression, expanded economy content, and other Phase 5 systems.

### Phase 5 — Build Progression — Complete

Phase 5 provides authorable Artifacts, per-instance card Enhancements, deterministic mixed shops, Upgrade-node Enhancement choices, and prototype build synergies while preserving baseline single-card combat. The optional poker/multi-card archetype was explicitly deferred.

### Phase 6 — Regression / Balance / Polish — Complete

Phase 6 adds focused regression hardening, clearer build/combat decision feedback, modal/run-flow cleanup, explicit terminal seed choices, and an evidence-based conservative prototype balance baseline. No new gameplay architecture or post-roadmap system was introduced.

### Phase 7 — Frontend & Menu Flow — Complete

Phase 7 adds a coherent Main Menu, seeded Run Setup, reusable Pause/Settings/confirmation navigation, editable Credits, functional persisted display settings, and reusable Victory/Defeat results while preserving the Phase 0–6 gameplay and deterministic-run architecture. It is now part of the approved baseline.

### Phase 7A — Card Interaction & Selection Foundation — Complete and Approved

Phase 7A provides stable physical hand dragging/reordering, combat-state selection policies, context-sensitive enemy attack/defense drops, capped atomic multi-card defense, and one contextual card-action control while preserving authoritative card/combat state.

### Phase 7B — Multi-Enemy & Enemy Identity — Complete

Phase 7B provides independently targetable runtime enemy groups, per-survivor attack totals, kill-triggered draws, secured per-enemy rewards, three-Goblin encounters, and the two-turn Thief flee rule without changing unrelated enemy, elite, or boss balance.

### Phase 7C — Ace Pairing — Implemented

Ace pairing and configurable seeded criticals are implemented; current full EditMode regression and the EventSystem-dispatched Game-scene pair/button check passed. Human mouse/drag targeting remains Pending, as distinguished in Section 5.

### Phases 7D–7E — Approved Playtest Revisions — 7D Accepted / 7E Complete

Phase 7D retains five depths and current Shop placement (explicit zero-depth change). Phase 7E includes Shop inspect/select → Buy, player-selected Enhancement targets, owned-Artifact combat HUD and a five-slot base capacity for Persistent Artifacts. NonSlot Artifact category support is structural only; no Contract content or replacement/capacity-upgrade system exists. The frontend seed follow-up is complete: blank setup generates a seed on Start, gameplay hides its seed label, and both Results modes retain the actual seed for replay and retry. Investment/Betting remains undecided.

### Content Expansion Architecture — Core Tier I Migration — Complete

Artifacts and Enhancements now share the typed source/resolver architecture documented in `Locus/knowledge/plan/cardpg-effect-architecture.md`. The eight approved canonical Tier I entries are migrated with explicit canonical metadata and GUID-safe legacy ID crosswalks. The current gate is **160/160 EditMode**, focused **8/8**, and **1/1 PlayMode**.

### Four-Artifact content batch — Complete

`rel_005` Leather Armor, `rel_006` Dagger, `rel_007` Tome, and `rel_023` Sword are authored and active through the existing typed foundation. Focused **11/11**, full EditMode **171/171**, and PlayMode **1/1** passed; scene smokes and the temporary prices are recorded in Section 5 and `Locus/knowledge/plan/cardpg-effect-architecture.md`. No further content was started.

### Incremental draft content — Chain Armor (`rel_019`) — Complete

`rel_019` Chain Armor is the first low-risk addition selected from the remaining workbook draft: it reuses the existing incoming combat damage modifier and was implemented as data, without changing resolver/combat code. Rare Tier I; temporary prototype price **25g** because the workbook price is blank. Each player combat damage instance is reduced by 8 before Shield and floored at zero. With Leather Armor, the two reductions stack; direct non-combat/run HP costs remain unchanged.

Focused `ChainArmorTests` **3/3**, full EditMode **197/197**, and PlayMode **1/1** passed. Console final query returned 0 errors/warnings; the pass had earlier unrelated Unity AI Toolkit account-service noise. The live Game scene catalog was verified to include Chain Armor. No dedicated interactive Shop/combat smoke was performed. Continue with one candidate at a time; defer mechanics that need new lifecycle/rule architecture or have unresolved design semantics.

### Incremental draft content — Ace Emblem (`rel_014`) — Complete

Implemented only after the user clarified that an Ace in **any attack action** enables the Artifact's 50% critical chance, including single-Ace attacks and 3-card Hands actions containing Aces. Common Tier 1; the workbook price is blank, so the temporary prototype price is **20g**. A typed `CriticalChanceOverride` plus `CardRank` condition is resolved against the committed action. It consumes at most one roll from the existing `combat-critical` stream per action; without an Ace match, existing critical eligibility/chance stays unchanged. Critical damage remains after the existing action-total cap.

- Added `Assets/Data/Relics/AceEmblem.asset` (+ `.meta`) and registered it in `Assets/Scenes/Game.unity/GameController` (catalog now contains 20 Artifacts). Added the reusable typed critical chance stage and rank filter to `Assets/Scripts/Combat/GameplayEffects.cs`, and used it from `Assets/Scripts/Managers/CombatManager.cs`. Added `Assets/Tests/EditMode/AceEmblemTests.cs` (+ `.meta`).
- Focused Ace Emblem tests **7/7**, full EditMode **204/204**, and PlayMode **1/1** passed. Coverage includes the 50% boundary, single Ace, non-Ace no-roll behavior, Ace pair, three-card Hands action with three Aces, one roll/action, and unchanged single-Ace behavior without the Artifact.
- Final Console warning/error query returned **0**. No separate interactive Game-scene combat/Shop smoke was performed. No other workbook content was added.

### Incremental draft content — Retaliation Emblem (`rel_016`) — Complete

Implemented the workbook's Common Tier I `rel_016` as a typed `CounterDamage` reaction. Price is blank in the workbook; temporary prototype price **20g**. One successful card block triggers one 5-damage counterattack against the exact `EnemyRuntime` whose pending attack that defense card blocked. A multi-card defense triggers once per blocked attack, in stable encounter order; blocking an attack with a player Shield by taking remaining damage is not a card-block trigger. The pending attack snapshot now retains its source enemy so retaliation never depends on current target selection or list re-discovery.

- Added `Assets/Data/Relics/RetaliationEmblem.asset` (+ `.meta`), registered in the Game scene (catalog now contains 21 Artifacts), `Assets/Tests/EditMode/RetaliationEmblemTests.cs` (+ `.meta`), and typed `AttackBlocked` context/queue support. Existing aggregate incoming damage and player Shield consumption remain unchanged.
- Focused Retaliation tests **5/5**; defense/Shield regression **23/23**; full EditMode **209/209**; PlayMode **1/1** passed. Coverage includes exact single/multi-enemy attacker attribution, one reaction per block, Shield-only non-trigger, and lethal-counter Victory resolution exactly once.
- Final Console warning/error query returned **0**. Game-scene asset/catalog were verified; no separate interactive Shop/combat smoke was performed.

### Incremental draft content — Golden (`enh_007`) — Complete

Implemented the Rare Tier I workbook effect using the existing typed `BonusGold` reaction with a new action-level `AttackCommitted` boundary. The workbook price is blank; the serialized asset carries a temporary **20g** prototype price. Each Golden Enhancement currently in hand grants +2 Gold once per committed attack action. The committed cards are removed first, then an authoritative copy of the remaining hand is captured before any card-commit reactions; thus a Golden card being played does not pay itself, and a Golden drawn by a later effect does not join that action's snapshot. Ace pairs and multi-hit attack actions still count once per action; each separate Golden in the snapshot contributes independently. No RNG is used.

- The `Golden.asset` definition and Game scene Enhancement catalog entry were already present when inspected and were preserved. The serialized definition matches `enh_007` / Rare / Tier 1 / `BonusGold`, `AttackCommitted`, amount 2. Added shared snapshot/source resolution in `Assets/Scripts/Combat/GameplayEffects.cs`, the action-commit lifecycle call in `Assets/Scripts/Managers/CombatManager.cs`, and focused `Assets/Tests/EditMode/GoldenEnhancementTests.cs`.
- Focused Golden tests **5/5**, full EditMode **214/214**, and discovered PlayMode **1/1** passed. Tests cover two in-hand Golden sources per action, multi-hit only paying once, a played Golden not paying itself, discard-zone exclusion, and one payment for an Ace-pair action. Final Console warning/error query returned **0**; an unrelated Unity AI Toolkit account-service warning had appeared earlier in the session.
- No separate interactive Game-scene smoke was performed. No other workbook content was implemented.

### Incremental draft content — Auxiliary (`enh_008`) and Arsenal Emblem (`rel_028`) — Complete

Implemented two low-risk draft entries in this increment. Rare Tier I `enh_008` Auxiliary has a blank workbook price and a temporary prototype price **20g**. Each Auxiliary Enhancement currently in hand contributes **+3 flat attack to every card's attack calculation**. The source is active at the shared `AttackCalculated` boundary while the card is in hand; therefore an Auxiliary card being committed still contributes to its own/current action's card calculations, and preview and execution use the same resolver path.

Common Tier I `rel_028` Arsenal Emblem has a blank workbook price and a temporary prototype price **20g**. Its typed passive effect increases the base hand cap from 8 to **9** while owned. Draws and the run-status display use the calculated capacity; reset removes the bonus and restores 8. No card zones or starting collection rules change.

- `ArsenalEmblem.asset` and `Auxiliary.asset` are registered in the Game scene; catalogs now contain 22 Artifacts and 6 Enhancements. Added `Assets/Tests/EditMode/AuxiliaryAndArsenalTests.cs`.
- Focused tests **6/6**, full EditMode **220/220**, and discovered PlayMode **1/1** passed. Auxiliary tests cover two sources stacking, same-card application, discard exclusion, Ace-pair preview/execution parity; Arsenal tests cover capacity 8→9, clamping, reset, and definition immutability.
- No separate interactive Shop/combat smoke was performed. Final Console warning/error query returned **0**.

### Standalone Dev Mode follow-up — implementation and tests complete; player build blocked

The standalone Dev Mode UI is now generated at runtime only in the Editor/Development Build. Main Menu receives a Dev Mode entry in the previously reserved bottom slot; its setup panel accepts an optional seed and the three approved toggles. Gameplay receives a persistent DEV MODE indicator plus a coordinator-managed panel with active-setting status, Disable Cheats, and a confirmation dialog for Lose Run Now. The release compilation excludes the runtime Dev UI and all active cheat code via `UNITY_EDITOR || DEVELOPMENT_BUILD` gates; ordinary New Run/Main Menu return disable the session, while Retry Same Seed leaves it configured.

`DevModeRuntime.ResetSession()` clears both in-memory activation and the Editor's legacy activation key. The Editor window's configure-before-Play path remains intact through its Editor lifecycle. Added an EditMode regression for stale activation reset and a PlayMode UI test for setup entry, overlay visibility, menu coordinator time/input blocking, close, and disable.

Verification so far: Unity script recompile succeeded; focused DevMode EditMode **5/5**, full EditMode **330/330**, prior complete PlayMode suite **3/3**, and new DevMode UI PlayMode test **1/1**. The new menu UI is generated from code; no scene YAML or build scene ordering edits were made.

Windows x64 Build Support reports installed. BuildPipeline builds were attempted in `D:/locus/data/temp/CardPG-DevMode-Builds` because Unity rejected the plan's suggested `Library/Locus/tmp` as an internal work directory. Both Development and release attempts failed before producing an executable: Unity's Player script compilation path raised `InvalidCastException` in `EditorCompilation.CompleteActiveBuildWhilePumping`; Bee then reported the generated Player DAG could not be loaded and Unity reported scripts had compiler errors, with no actionable source diagnostic. A subsequent release attempt similarly failed while scripts were compiling. Therefore no EXE launch, release exclusion audit, Player log inspection, or interactive shop/retry/defeat smoke is claimed. This environment/build-pipeline blocker remains for a clean Editor restart or independent command-line build retry.

Manual procedure once a Windows Development Build is available: launch Main Menu → Dev Mode → set seed/toggles → Start Dev Run; verify the DEV banner, visit a Shop and scroll all eligible offers, test health/money behavior, open DEV → confirm Lose Run Now and inspect normal defeat result, Retry Same Seed (same options), Disable Cheats (no retroactive refunds/heals), then return to Main Menu and start a normal run. Close and relaunch the app to verify dev mode starts off. In a non-Development release EXE, verify no Dev Mode entry or overlay and normal health, money, and shop behavior even if `CardPG.DevMode.Enabled` exists in PlayerPrefs.




Added a removable Unity Editor developer tool at `CardPG/Development/Dev Mode`. It starts Play Mode from the existing `Assets/Scenes/Game.unity` and keeps the dev controls in a separate Editor window—no Main Menu button, extra game scene, or Build Settings entry. The runtime overrides are guarded by `UNITY_EDITOR`; the DevMode Editor window is under `Assets/Editor/DevMode/`.

- **Infinite health:** combat and direct Run/Event HP costs are suppressed while enabled.
- **Infinite money:** spending and affordability checks are bypassed; the gameplay HUD shows `∞`.
- **Unlimited Shop offers:** each Shop exposes all currently unowned Artifact catalog entries and all Enhancement definitions in the scrollable Editor Dev Shop browser. Purchases still use RunManager's cached-offer, duplicate, targeting, and price validation paths. The normal five-Persistent-Artifact cap and one-Enhancement-per-card rule remain enforced. The standard Shop panel is hidden only during this Editor dev mode.
- **Lose Run Now:** invokes the existing one-shot `EncounterResult.Defeat` path and displays the normal Defeat results.
- EditMode `DevModeTests`: **4/4** passed; full EditMode **224/224**; discovered PlayMode **1/1**. Programmatic Play Mode smoke verified no combat/event HP loss, no Gold spending, all **28** catalog offers (22 Artifacts + 6 Enhancements), the five-slot Artifact cap, the normal Shop panel hidden, and the Dev window present. The Lose Run path produced GameOver and the results screen. The Dev window's actual GUI buttons were not physically clicked.
- Build Settings were not changed; only MainMenu, Game, and Bootstrap remain enabled for the player build. To remove the feature at release, delete `Assets/Editor/DevMode/` and `Assets/Scripts/Development/DevModeRuntime.cs`, then remove the small `#if UNITY_EDITOR` hooks in the runtime managers/UI. No DevMode scene or MainMenu wiring is required.

No new workbook content was added in this dev-mode task.

### Incremental workbook Event — `evt_009` Noble Family — Complete (September 27, 2026)

Added the Common Tier I spreadsheet Event using only existing event effects: **Refuse** has no effect; **Accept** grants +10 Gold. Authored `Assets/Data/Run/Events/NobleFamily.asset` and appended it to `PrototypeRunContent.asset`'s standard Event catalog. No resolver, UI, map topology, Shop, RNG, or combat changes were needed. The spreadsheet has no price for this Event; it is not a Shop offer.

- Added `Assets/Tests/EditMode/NobleFamilyEventTests.cs` for canonical definition/catalog registration and both choice outcomes through the existing `RunEffectResolver`.
- Focused Noble Family tests **2/2**; adjacent event/map regression **4/4**; full EditMode **245/245** (0 failed/skipped/inconclusive); discovered PlayMode suite **1/1**. Final Unity Console error/warning query **0**. No dedicated manual Game-scene event interaction was performed; the authored data, catalog link, and resolver outcomes are covered by EditMode tests.
- No other workbook content was added in this increment.

### Incremental workbook Event — `evt_006` Tavern — Complete (September 27, 2026)

Added the Common Tier I Tavern event. **Ask for Guidance** reveals exactly one reachable, normally hidden Event/Risk node using the same deterministic route-depth → row → node-ID policy as Lantern; it changes visibility only, preserves route links/topology, notifies map UI, and consumes no RNG. The choice is unavailable when no eligible node remains. **Rest** applies the existing ordered effects: spend 2 Gold, then heal 5 HP (clamped by normal player health rules). The event has no Shop price.

- Added the typed `RunEffectType.RevealMapNode` run/event effect and reused RunManager's shared hidden-node eligibility/selection logic; no combat, seeded stream, or route-generation changes.
- Added `Assets/Tests/EditMode/TavernEventTests.cs`. Focused Tavern + prior event/Lantern regressions **8/8**; full EditMode **249/249** (0 failed/skipped/inconclusive); discovered PlayMode **1/1**. Final Console error/warning query **0**. EditMode exercised the authoritative RunManager choice flow; no separate manual Game-scene click-through was performed.
- No other workbook content was added in this increment. Blacksmith's Enhancement selection/target semantics and Shop Guidance's purchase interaction remain deferred as requested; no behavior was inferred for them.

One presentation-only Game-scene batch replaced the previous row/hand/action geometry with a Balatro-like centered curved fan (dynamic, modest overlap; hover and selected elevation; cursor-centered drag, hysteresis, smooth reflow and return), visual-only Rank/Suit sorts, centered responsive 1–3-enemy prefab row, compact top run state and left Gold/Draw Deck control, right icon-only Artifact rail with tooltip/detail and capacity count, bottom Play/Sort/Block actions, and bottom-right HP/incoming damage/Take Damage. Recover stays reachable with an empty hand. The draw viewer now lists the **remaining draw deck cards**, not discarded cards or a build summary. The gameplay seed remains hidden; no separate Discard action or pile HUD was introduced. Combat legality, card identity/zones, run generation, effects, damage, and defense assignment remain authoritative in their existing managers/models. No enemy death animations or unrelated presentation were started.

Reusable components: `Field` (hand fan and visual sorting), `CardView` (layered card interaction), existing `EnemyGroupUI` + `EnemyView.prefab` (responsive shared enemy row), `ActionButtonsUI` (action bar/HP state), `DeckViewerUI` (remaining-deck modal), and new `ArtifactRailUI` + `ArtifactIconSlotUI` (event-driven owned-icon rail). Scene wiring is in `Assets/Scenes/Game.unity`; enemy width bounds are in `Assets/Prefabs/EnemyView.prefab`. New EditMode coverage: `HandFanTests` and `ArtifactRailUITests`.

Verification: full EditMode **230/230** and discovered PlayMode **1/1** passed after final code/layout changes. Play Mode Game-scene smoke checked 1/2/3 enemies bound to the same prefab (440-wide panels at reference resolution, three panels compressing to 356/289 wide in simulated 1100/900-wide row space), hand fan at eight and five cards, Rank/Suit sorting with unchanged CardInstance model order, drag center delta **0 pixels**, drag drop onto enemy #2 (HP 60→57), two-card Block of two distinct incoming attacks, remaining damage 1 and Take Damage (HP 30→29), deck viewer showing all 32 remaining cards (32-card list across eight rows), and Artifact rail acquisition/hover/detail/close/reset (1/5→0/5). Screenshots confirmed visible hand/modal layering and the viewer grid at the Editor's **837×471** Game viewport. The EditMode hand tests additionally cover three- and seven-card compression, stable sorting, and drag-slot threshold. Human mouse feel, actual physical hover/click/drag, and other Game-view aspect ratios were **not** exercised; the smoke used programmatically dispatched UI callbacks and pointer events. No full twelve-map pacing claim. Unity/TMP also dirtied the pre-existing fallback font asset while rendering glyphs during Play Mode; this generated asset was not intentionally edited as part of the UI batch.

### Combat Presentation — Deck, Attack/Block, and Exit Barrier — Complete (September 27, 2026)

`DrawDeckStackUI` presents the existing clickable Draw Deck control as a layered `Back Red` stack, clear count, and faded empty ghost. It subscribes to the authoritative `CardManager.OnDeckChanged` event (Build/encounter draws, recycle/return, boss reward insertion, shuffle notifications, discard callbacks and reset); the viewer continues to open through its original DeckViewerUI button binding. Focused tests verify count/depth after draw, discard-to-discard does not change draw count, recycle return and boss reward insertion, reset/rebuild, and zero-raycast stack layers.

`AttackCardPresentationUI` now handles both attack and defense presentation. Attack keeps direct Play travel and cursor-centered drag wind-up/lunge. Block snapshots selected cards before the existing `TryDefendWithCards` call, animates the cards in selected order directly toward the enemy row (no attack wind-up), uses a distinct cool impact flash, then performs the same red-back/fly-away/cleanup. The presenter never computes damage, defense, or RNG and never invokes gameplay a second time. `ActionButtonsUI` and `CardActionDropTarget` forward Play/Block intents to it, with direct CombatManager fallback if the component is absent.

A pending-commit reservation makes `AttackCardPresentationUI.IsBusy` true **before** the synchronous combat call can emit its Victory/map event. `PathScreenUI` defers map/victory-panel visibility, and `RunResultUI` defers the final victory overlay until the presenter signals completion. CombatManager/RunManager still commit damage, card zones, enemy removal, node completion, rewards, and result exactly once immediately; the defeated EnemyGroupUI view is removed synchronously, while the small card/impact presentation completes before the route/results transition. Reset/new encounter/run restart cancels and clears presentation. No enemy death/slash/loot animation system was started.

Files: `Assets/Scenes/Game.unity`, `Assets/Scripts/UI/ActionButtonsUI.cs`, `CardActionDropTarget.cs`, `PathScreenUI.cs`, `RunResultUI.cs`, `AttackCardPresentationUI.cs`, `DrawDeckStackUI.cs`, and `Assets/Tests/EditMode/DrawDeckStackUITests.cs` (+ generated `.meta`). Full EditMode **243/243** and discovered PlayMode **1/1** passed. Game-scene smoke verified a single Block (two attacks→one, one card consumed, HP unchanged, zero damage events, one visual) and two-card Block (two→zero, two cards consumed, no damage event, two ordered visuals), then cleanup and Take Damage resolving remaining two-enemy damage as one aggregate hit. A three-card Hands action that killed the last enemy emitted one card-damage event, completed its node/reward/result once while the route/final-victory presentation remained hidden through all three card visuals; it appeared only after presenter idle. A new encounter and run restart cleared in-flight visuals. Draw stack tests verify authoritative updates on draw, `ReturnRandomDiscardToDeck`, `AddBossReward`, reset and rebuild; in-scene count tracked 32/24/16/8/0. The final Console query showed one unrelated Unity AI Toolkit account-service warning and no CardPG/game warning or error. Input callbacks/pointer events were programmatically dispatched; human mouse-feel and non-16:9 viewport checks remain pending.

## 9. Next Task

Continue gradual content expansion from `CardPG_GameData_organized.xlsx`; inspect exact definitions and current repository support first. Completed incremental entries include `evt_006` Tavern, `evt_009` Noble Family, `rel_014` Ace Emblem, `rel_016` Retaliation Emblem, `rel_019` Chain Armor, `rel_028` Arsenal Emblem, `enh_007` Golden, and `enh_008` Auxiliary. No next content entry is selected. Favor content that fits current systems, add focused tests, then regress before proceeding. Stop/defer on regressions or unresolved semantics.

## Combat & Shop Economy Rebalance — implemented; regression gate blocked

- Perfect Parry tracks cumulative effective card Block across the whole enemy-response window (user-confirmed boundary), against positive incoming damage after the existing typed modifiers. Exact coverage awards at most one capacity-limited draw from cards present in the Draw Pile when that window began. Normal defense recycling is preserved, but cannot supply the reward; new strict draws never recycle discards or use overflow.
- Five route depths remain. Mandatory, visible Shop checkpoints follow Depths 2 and 5; random route Shops are replaced with combat nodes. Checkpoints retain one-shot node-completion/Artifact hooks but are excluded from route-depth display. Boss stats, starting HP, authored prices and reward values are unchanged.
- RunManager retains separate run-local inventory, purchases, Service usage and Refresh state per map/checkpoint. Two distinct seeded Services are offered from Guidance, Quest (existing Contract), Investment and Supply; at least one checkpoint per map includes Supply. Existing Quest prerequisites and other Service mechanics remain.
- Supply is one purchase per Shop at 3g per successfully drawn existing card, bounded by Gold, deck count and hand capacity. Goods-only Refresh costs 3/5/8/12/17/23/...; purchased Goods cannot return, Service objects remain unchanged, and refresh-generation IDs reject stale requests. Default configurable Goods slot counts (Artifact/Enhancement/Consumable) are 2/2/1 mid-map and 1/1/3 pre-boss.
- Shop UI presents separate Goods/Services columns, quantity entry and Refresh. Runtime compilation succeeded and the final Console check returned zero warnings/errors. Targeted runtime/scene checks passed; 18 focused EditMode cases were added, but **no automated tests ran** because Unity Test tools are disabled and the user cannot load the workspace settings to enable them. Independent final Luna review was also blocked by quota. Do not treat this phase as regression-accepted until EditMode/PlayMode suites run. Details: `Locus/knowledge/plan/combat-shop-economy-rebalance-report.md`.

## Phase 8A — Persistent Run Save/Load

### Architecture and ownership

- `Assets/Scripts/Persistence/RunSaveData.cs` defines version-1 value-only DTOs (`version`, `contentVersion`, and `checkpoint = "safe-node"`). Data contains no Unity object references, delegates, views, selections or animations. Owner-specific partial contracts live in `CardManager.Persistence.cs`, `CombatManager.Persistence.cs` and `RunManager.Persistence.cs`; loading does not call run startup, map/boss generation, purchases, draws or gameplay-effect hooks.
- Snapshots preserve the seed; generated boss order, stats and abilities; route graph and completion flags; active non-combat node; player resources; exact owned/deck/hand/discard ordering and card IDs; Enhancements, suit changes, permanent bonuses and effect counters; Artifact identities/acquisition order and the identity allocator; backpack slot/item order and charges; all Shop inventories, purchase guards, Service state and Refresh generations; Upgrade offers; Investment stakes, Contract progress, reward guards, Alchemist caches and run timing.
- The card, critical and Duelist RNG owners expose and restore exact nonzero xorshift state. Context-derived streams remain seed/context-derived, with their context counters and already-generated results saved. Loading cannot advance these streams. Offline/menu time is not added to saved run timing.
- `Assets/Resources/RunSaveContent.asset` is the persistent content registry, currently referencing the Game scene's 74 Artifacts, 12 Enhancements, 19 Consumables, enemy fallbacks and run-content catalog. Definitions remain authored assets, resolved by stable content IDs. Keep this registry aligned when changing GameController catalogs; bump the content/save version when incompatible rules or schemas change. No old-version migration is silently inferred.

### Storage and safe checkpoints

- `SaveGameService` coordinates the runtime; `RunSaveStore` independently reads/writes one `active-run.json` under `Application.persistentDataPath`. The JSON envelope has a format version and SHA-256 integrity checksum. Writes validate first, flush a same-directory temporary file and atomically move/replace the primary. A `.bak` recovery copy is validated independently; malformed, missing, unknown-content and incompatible saves fail gracefully. An 8 MiB file limit bounds input. A `.deleted` tombstone prevents backup resurrection after terminal/deletion interruption.
- Autosaves run only when the model is settled: on the route; in a normal Shop, authored Event/Risk or cached Upgrade choice; before committing a route selection; and after completed Shop/purchase/Refresh/sale/resource transactions. A quarter-second safe-state poll covers remaining state changes, while pause/quit and returning to the main menu flush a safe checkpoint. Unchanged gameplay snapshots do not repeatedly write just because the timer advances.
- **No mid-combat serialization.** Closing during a battle returns to the map checkpoint immediately before the encounter was selected, restoring HP, card order, counters and RNG from that boundary. The player can select the route again; no enemy, hit, action queue or animation is reconstructed.
- Unresolved reward/Artifact prompts, Hammer/Blood Ritual continuations and Quest-objective selection are unfinished transactions. They keep the previous safe checkpoint; exiting during them rolls back that unfinished sequence. Their UI/callback state is deliberately excluded. Ordinary Event/Upgrade/Shop interfaces are reconstructed from saved node IDs and cached offers without applying their effects again.
- Final victory and defeat delete the primary, backup and temporary save. Explicit **Abandon Run** retains its existing destructive meaning and also clears the save. Entering Infinite Mode after victory starts a new active checkpoint sequence. Dev Mode is a sandbox: it neither writes nor deletes the normal run's save.

### Frontend and restore sequence

Main Menu creates a Continue button enabled only for a validated primary/recovery save, with a saved-run/recovery status. Starting New Run asks before replacing existing save files; cancellation preserves them. `FrontendLaunchContext` carries a one-shot saved DTO separately from a seed. `GameController` configures managers, restores validated model state, then invokes presentation-only notifications. Direct Game-scene entry also resumes a valid save; invalid files are not silently replaced. Save failures are surfaced rather than silently reported as successful.

Shop completion now marks the checkpoint cache complete on every completion path, including Quest confirmation, not only the Shop Continue button. Scene teardown also hides inactive board panels without starting a coroutine on an inactive parent. No balance values or content effects changed.

### Verification and limitations

- Added **20 EditMode cases** in `PersistentRunSaveTests` plus **1 PlayMode transition case** in `SaveLoadSceneTransitionTests`. **Automated execution: 0 run, 0 passed, 0 failed** because Unity Test tools remain disabled; this is not a passing regression gate.
- Unity compilation passed. Staged runtime checks verified exact JSON round-trip; Shop-state equality after a Play Mode/domain restart and Continue-button scene reload; RNG continuation and exact deck order; pre-combat rollback (runtime HP 26 restored to saved HP 30); backup recovery/corruption rejection; tombstone deletion/rewrite; New Run cancel/confirm; Quest completion persistence; and final-victory/defeat deletion without resurrection. The last connected Console check after the transition fix showed zero errors and one unrelated Unity account-service warning.
- These are accelerated Editor checks, not a packaged executable relaunch, a full twelve-map playthrough or a full automated regression run. After a workspace move/import Unity reconnected without a forced restart; additional missing-primary backup preservation and incompatible-on-disk-version checks passed. The post-move Console contains a project-file-generator sharing-violation error and a Unity account-service warning, not a reported save/load runtime failure. See the Phase 8A handoff report for the final verification status.

### Phase 8B extension boundary

Retain this storage envelope, content-ID registry and owner contracts. Add a new versioned combat checkpoint DTO/discriminator containing exact enemy runtimes/identities, pending hit/source assignments, Shield/Block, turn/action counters, death/reward guards and ordered resumable reaction/extra-turn/overflow state. Establish explicit between-action capture points first. Do not serialize callbacks or replay encounter-start effects to rebuild combat. Add deliberate migration/compatibility policy and mid-combat determinism tests before enabling that checkpoint kind; version 1 accepts only `safe-node`.

## Architecture Audit Remediation — October 19, 2026

`DEALBREAK_ARCHITECTURE_AUDIT.md` (branch `architecture-audit-fixes`, one commit per finding). Verified: EditMode 651/651, PlayMode 7/7.

- Correctness: lethal Tricks while blocking no longer softlock (single terminal check); the menu coordinator no longer clears gameplay input-gate reasons; enemy rules no longer compare `enemyName` (the rebrand had disabled the Captain aura and broken Captain/War Drummer previews); copies of owned Gear stay tracked after an upgrade; previews share the execution damage calculation; Incident Trick rewards validate atomically; reaction faults abort the encounter and keep the save; encounter results publish after the combat action unwinds.
- Structure: all Gear special rules in `GearRules*.cs` with named `GearCounterKeys`; `EncounterComposer` for spawns and previews; typed `RunEventFlow`; `RunManager` and `CardManager` split into partial files; combat commands on `CardInstance`; `CombatCommands` UI routing; Headliner content in `Resources/HeadlinerCatalog`; one content registry (`RunSaveContent`) for play and saves, which also made the Double Strike Patch obtainable; `RunSaveMigrations` chain; event-driven autosave; read-only manager state with internal test seams.
- Removed: legacy enemy damage paths, unused per-suit Headliner abilities and their assets, unused stubs, `Bootstrap.unity`, `SampleScene` and `_Recovery` scenes.
- Deliberately not changed: per-card runtime `CardData` instances (no defect; large blast radius); `CardManager` lookups into `RunManager` (edit-mode tests rely on them without `RunManager.Awake`).

## 10. Instructions for Future Locus Sessions

1. Read `DEALBREAK_DEVELOPMENT.md` before beginning substantial project work.
2. Treat the current repository state as the implementation source of truth.
3. This document provides historical/design context but does not override working code when they disagree. Investigate discrepancies rather than blindly trusting either source.
4. Do not broadly re-audit completed phases unless there is evidence of a regression, inconsistency, or architectural issue.
5. Before modifying a system, inspect only the code, scenes, assets, and necessary dependencies relevant to the current task.
6. Do not assume deferred features are already implemented.
7. Preserve established gameplay invariants unless the user explicitly approves a design change.
8. Update this document after each major completed phase when gameplay contracts, architecture, temporary systems, or roadmap status materially change.
9. Keep updates concise. This file exists to reduce context usage, not become a transcript or development diary.
10. Do not copy completion reports or tool logs verbatim into this document.
