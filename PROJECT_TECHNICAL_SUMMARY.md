# Dealbreak Technical Summary

> **Internal rename update (2026-10-13):** The runtime assembly is now `Dealbreak.Runtime`; Gear uses `GearData`/`GearRuntimeInstance`, and progression APIs use Headliner. This document is a historical implementation snapshot; see `Docs/Dealbreak_Internal_Rebranding_Report.md` for the current API and compatibility boundary.

> Historical technical snapshot. For current player-facing content names and terminology, use `Dealbreak_GameData_IDs_Terminology_Fixed.xlsx`; the legacy symbols and values documented here remain implementation details and are not serialized-ID migration instructions.

Repository review date: **September 18, 2026**  
Unity version: **6000.3.24f1**  
Repository branch: **`main`**

> This document describes the current working repository, not only the latest Git commit. The working tree contains implemented Phase 4–7B systems and run-timing telemetry beyond commit `3c1e643`.

## 1. Project Summary

### What Dealbreak is

Dealbreak is a single-player card-based roguelike/RPG prototype. Its combat foundation is inspired by Regicide: ordinary cards are played as attacks, cards may be discarded to defend against incoming damage, and player health persists between encounters. Its run structure adds branching Districts, seeded procedural content, Black Markets, Incidents, Gear, per-card Mods, Headliners, and build progression.

The baseline game is intentionally **not** poker-hand combat. Normal attacks use one card. Multi-card selection currently exists for defense, while broader combinations remain deferred.

### Main implemented gameplay systems

- A twelve-map run with four Jacks, four Queens, and four Kings as ordered boss rank groups.
- Randomized boss suit order within each rank group.
- A 40-card starting collection containing Ace through Ten in all four suits.
- Exact face-card rewards: defeating a boss adds that boss's rank-and-suit card to the run collection.
- Persistent run health, gold, owned Artifacts, and per-card Enhancements.
- Single-card attacks and ordered multi-card defensive discards.
- Explicit Take Damage and empty-hand Recover actions.
- Multi-enemy encounters with independent runtime enemy identities and target selection.
- Three-enemy Goblin encounters and a two-turn Thief flee rule.
- Deterministic branching map generation with combat, elite, shop, event, upgrade, risk, and boss nodes.
- Fully seeded deterministic random streams for bosses, cards, maps, shops, and upgrades.
- Data-driven enemies, abilities, Artifacts, Enhancements, events, and run content using ScriptableObjects.
- Deterministic mixed Artifact/Enhancement shops and free Upgrade-node Enhancement choices.
- Main Menu, run setup, custom/random seeds, pause/settings/confirmation flows, results, and scene transitions.
- Run-duration telemetry with total time, completed map splits, and an incomplete defeat split.
- EditMode automated tests plus targeted manual Play Mode verification.

### Current project scope

The current scope is a desktop-oriented prototype with:

- One playable character.
- Twelve major maps per normal run.
- Three authored normal enemy definitions, generated elite variants, and twelve generated face-card bosses.
- Eight prototype Artifacts and four prototype Enhancements.
- Six authored event/risk/upgrade content assets in the current run catalog.
- A fixed five-column, three-row map template plus a boss node.
- Three reusable enemy presentation slots.
- Temporary UI styling and placeholder visuals rather than final production art.

There is no save/resume system, metaprogression, profile system, achievements, networking, cloud seed exchange, character roster, production audio system, or finalized balance layer.

## 2. Tech Stack

### Unity and C#

- **Unity 6000.3.24f1** — Engine, scene/prefab serialization, GameObject/component lifecycle, UI, runtime asset loading, and Play Mode execution.
- **C#** — All gameplay, runtime models, managers, deterministic generation, UI controllers, frontend flow, and tests.
- **Assembly Definitions** — `Assets/Scripts/Dealbreak.Runtime.asmdef` separates runtime code; `Assets/Tests/EditMode/Dealbreak.EditModeTests.asmdef` defines the Editor-only test assembly.

### Verified Unity packages and systems actually used

- **Universal Render Pipeline 17.3.0** — The project is configured around URP assets in `Assets/Settings/` and project graphics settings.
- **Input System 1.20.0** — Used for pause input in `PauseMenuUI` and by the scene's `InputSystemUIInputModule`.
- **Unity UI/UGUI 2.0.0** — Used for buttons, canvases, images, sliders, layout groups, raycasters, drag/drop, menus, and HUD presentation.
- **TextMesh Pro / `Unity.TextMeshPro`** — Used for card labels, combat information, map/shop/event screens, run status, settings, seed UI, and results.
- **Unity Test Framework 1.6.0 with NUnit** — Used for the EditMode test assembly and its current 77 discovered test cases.
- **Unity `PlayerPrefs`** — Used by `DisplaySettingsService` to persist resolution, display mode, and VSync settings.
- **Unity scene management** — `Bootstrap`, `GameFlowService`, and `FrontendLaunchContext` coordinate Main Menu/Game transitions and one-shot launch state.

### ScriptableObjects

ScriptableObjects are used as authorable shared definitions rather than mutable run instances:

- `CardData`
- `EnemyTypeData`
- `EnemyAbility`
- `RelicData`
- `CardEnhancementData`
- `RunContentCatalog`
- `RunEventDefinition`

They allow content to be configured as assets under `Assets/Data/` while runtime state remains in classes such as `CardInstance`, `EnemyRuntime`, `PlayerRuntime`, and the managers.

### Version control

- **Git** — The repository uses branch `main`; `.gitattributes` normalizes text files to LF and keeps Unity YAML assets text-diffable.
- **Git LFS** — Configured for binary models, textures, audio, video, fonts, compiled libraries, and archives. Current LFS-tracked files include TextMesh Pro font/sprite binaries and embedded editor-tooling binaries.
- **Unity text serialization** — Scenes, prefabs, materials, assets, controllers, and `.meta` files remain normal text rather than being placed in LFS.

### Installed but not claimed as gameplay technology

The package manifest contains additional Unity packages such as 2D tools, AI packages, Navigation, Timeline, Visual Scripting, Analytics, and Multiplayer Center. The inspected gameplay code does not establish them as part of Dealbreak's implemented runtime architecture, so they should not be presented as project features.

## 3. Architecture

### Overall structure

The runtime is divided into four broad layers:

1. **Definition data** — ScriptableObjects describe cards, enemies, abilities, Artifacts, Enhancements, and events.
2. **Plain runtime state** — `CardInstance`, `CardCollection`, `PlayerRuntime`, `EnemyRuntime`, and `RunTimingStatistics` hold mutable per-run/per-encounter state.
3. **Gameplay coordinators** — `CardManager`, `CombatManager`, and `RunManager` apply rules and coordinate systems.
4. **Presentation/input** — `CardView`, `Field`, gameplay UI components, and frontend components display state and submit commands.

The managers communicate through direct method calls and C# events. UI classes subscribe to manager events, refresh presentation, and forward player intent. UI hierarchy is not authoritative gameplay state.

### Run and progression

`RunManager` owns the run seed, boss sequence, current map, active node, node completion, shop/upgrade offers, event resolution, boss rewards, run completion, and run timing lifecycle.

Important behavior:

- `StartRunWithSeed` resets combat, cards, and run state before building the new run.
- `BuildBossDeck` creates Jack, Queen, and King groups and shuffles each group using a dedicated deterministic stream.
- `GenerateMapForIndex` delegates to `RunMapGenerator`.
- `OnPathChosen` commits a node and dispatches to combat, shop, upgrade, event, or risk handling.
- `HandleEncounterResult` completes a node only after victory.
- `HandleBossVictory` grants the exact face-card reward, records a map split, advances the boss/map index, and completes the run after the final boss.

### Card instances, collection, and zones

`CardData` is a shared definition. `CardInstance` is one owned runtime card with:

- A stable per-run integer ID.
- A reference to its card definition.
- An optional per-instance Enhancement.

`CardCollection` is a plain C# model with four authoritative lists:

- `OwnedCards`
- `Deck`
- `Hand`
- `DiscardPile`

All zone changes are centralized in `CardCollection`. Initialization rejects duplicate IDs. Batch discard validates every card before moving any of them, preventing partial mutations. When the deck empties, the discard pile is recycled and deterministically shuffled.

`CardManager` wraps this model for run-level operations and presentation integration. It creates the 40-card starting collection, assigns IDs, creates card views, manages selection, performs draws/discards, owns gold and build catalogs, grants boss cards, and applies Enhancements.

### Combat and state flow

`CombatManager` is the authoritative encounter state machine. Its states are:

- `Idle`
- `PlayerTurn`
- `EnemyAttacking`
- `GameOver`
- `GameWon`

It owns the active `EnemyRuntime` list, selected enemy target, pending incoming damage, secured encounter gold, player runtime health, and one-shot encounter resolution.

Core flow:

- `StartEncounter` validates unique enemies, subscribes to enemy events, draws one card, and enters `PlayerTurn`.
- `TryPlayCards` validates a single selected hand card and exact target, discards atomically, applies Artifact/Enhancement hooks, damages the target, processes death/flee rules, and starts the surviving enemies' combined attack.
- `TryDefendWithCards` totals authoritative defense, discards the complete validated card set, and reduces pending damage.
- `TakeRemainingDamage` applies unblocked damage.
- `Recover` applies the current total enemy attack, then draws one card if the player survives.
- `ResolveEncounter` emits exactly one `EncounterResult`, pays secured rewards only on victory, and prevents duplicate terminal processing.

### Enemy system

`EnemyTypeData` contains authored enemy definitions: health, attack, reward, group count, flee timing, source boss card, and abilities.

Each encounter creates independent `EnemyRuntime` objects. Runtime enemies track current HP, current attack, completed player turns, instance numbering, and events for HP/attack changes.

`EnemyAbility` supplies typed hooks for:

- Attack increases at encounter start.
- Attack increases after a player card.
- Healing after a player card.
- Incoming card-damage reduction.

`CombatManager` owns encounter-level rules, while `EnemyRuntime` applies per-enemy state and authored hooks. `EnemyGroupUI` binds the current ordered enemies to presentation slots without owning their state.

### Map generation

`RunMapGenerator.Generate` creates a deterministic graph for a non-negative map index:

- Five route columns.
- Three nodes per route column.
- A final boss node.
- Guaranteed same-row links plus one deterministic diagonal link where possible.
- Column-specific node-type mixes.
- Hidden event/risk information.
- Content IDs selected from the run catalog using a separate content stream.

Generated `PathNode` objects contain IDs, node type, map index, graph links, coordinates, visibility, accessibility, and completion state.

### Fully seeded deterministic RNG

`DeterministicRandom` is a small custom PRNG implementing `IRandomSource`. `RunRandomContext`:

- Normalizes visible seed text.
- Converts it to a stable FNV-style hash.
- Derives named streams from the root seed, stream name, and optional context integer.

Examples of independent streams include:

- `cards`
- `boss-order`
- `map-layout`
- `map-content`
- `shop-artifacts`
- `shop-enhancement-cards`
- `shop-enhancements`
- `upgrade-cards`
- `upgrade-enhancements`

This prevents random consumption in one subsystem from shifting unrelated outcomes. Card shuffling can change without changing the generated map or shop, provided stream contracts remain unchanged.

### Artifacts and Enhancements

`RelicData` represents Artifacts with authored filters and effects. Filters can restrict effects by suit or require an enhanced card. Effects include:

- Attack multipliers.
- Flat attack bonuses.
- Defense bonuses.
- Enemy attack reduction.
- Discard recycling.
- Card draw.
- Post-victory healing.
- Bonus gold.

`CardEnhancementData` describes one-card upgrades such as attack, defense, healing-on-play, and draw-on-play.

Enhancements are attached to `CardInstance`, not `CardData`. This preserves the distinction between a shared definition and one upgraded owned card. A card currently accepts at most one Enhancement.

### Shops, upgrades, and events

`ShopOffer` is a typed runtime offer for either an Artifact or an Enhancement targeted at a stable card ID.

`RunManager` generates and caches shop offers from deterministic streams. Purchase validation checks the cached offer, price, ownership, card existence, Enhancement eligibility, and available gold. Purchases use the cached displayed price rather than re-reading a potentially changed asset price.

Upgrade nodes generate deterministic free Enhancement offers for eligible owned card instances. Event and risk nodes use `RunEventDefinition`, `RunEventChoice`, and ordered `RunEffect` arrays.

`RunEffectResolver` first simulates effects in authored order to produce a failure reason. Only valid choices are then applied to gold, health, maximum health, or card draw.

### UI and frontend

`GameController` has an early execution order and explicitly configures or creates the three managers before UI subscriptions depend on them.

Gameplay UI components subscribe to manager events:

- `ActionButtonsUI` presents combat state and actions.
- `EnemyDisplayUI` and `EnemyGroupUI` present enemy state.
- `PathScreenUI`, `ShopUI`, and `RunEventUI` present run choices.
- `RunStatusUI` presents run resources, progression, and live timing.
- `RunResultUI` presents terminal results and map splits.

Frontend flow is separated from run authority:

- `Bootstrap` creates a persistent `GameFlowService` and resets runtime managers on Main Menu load.
- `FrontendLaunchContext` carries a one-shot run seed or menu destination between scenes.
- `GameFlowService` restores global time/input state and loads scenes.
- `GameplayMenuCoordinator` manages a stack of blocking overlays, pauses time, disables gameplay raycasts, cancels transient card interaction, and restores selection/state after the final overlay closes.
- `GameplayInputGate` provides manager-level defensive input rejection, so blocking is not dependent only on disabled UI controls.

### Run statistics and timing

`RunTimingStatistics` is a plain runtime model owned by `RunManager`. It tracks:

- Total elapsed run seconds.
- Current map elapsed seconds.
- Completed `RunMapSplit` records.
- Current incomplete split.
- Active/stopped state.

`RunManager.Update` advances it with scaled `Time.deltaTime`, so explicit pause at `Time.timeScale = 0` contributes no time. New runs and restarts reset it, boss completion records a split, defeat stops it while preserving the incomplete map, and abandonment stops the timer.

The HUD and results screen read this state; they do not own or calculate authoritative timing.

## 4. Important Engineering Decisions

### Authoritative runtime state instead of UI state

**Problem:** The original prototype could easily treat Transform children, card view counts, or visual positions as card ownership and zone state. That makes drag/drop, resets, tests, and non-visual simulation fragile.

**Solution:** `CardCollection` owns card zones, managers own gameplay rules, and views only bind to runtime objects. UI submits commands and listens for events.

**Why:** Plain models can enforce invariants, run without a scene, and be tested directly. Presentation can be rebuilt without rewriting rules.

**Tradeoffs:** Managers must explicitly keep views synchronized and validate view-to-model references. There is more coordination code than in a UI-driven prototype.

### Stable `CardInstance` identity

**Problem:** Two cards with the same suit/rank still need to be distinct owned objects, especially after rewards and Enhancements.

**Solution:** Every `CardInstance` receives a unique per-run ID. Shops and Enhancements target IDs, and collections reject duplicate IDs.

**Why:** Stable identity supports exact ownership, per-card upgrades, duplicate validation, conservation tests, and deterministic offer targeting.

**Tradeoffs:** IDs are currently run-local rather than persistent save identifiers. Save/load would require a serialization policy and migration rules.

### Independent deterministic RNG streams

**Problem:** A single mutable random sequence causes unrelated content to change when another system consumes one extra random value.

**Solution:** `RunRandomContext` derives named streams from a normalized seed and context value.

**Why:** The same seed and decisions reproduce the run while map, boss, card, shop, and upgrade systems remain isolated.

**Tradeoffs:** Stream names and context formulas become compatibility contracts. Renaming a stream or changing generation order can change replay results across versions; cross-version determinism is not promised.

### Data-driven ScriptableObject content

**Problem:** Hard-coded enemy, Artifact, event, and boss behavior would make content expansion require repeated manager edits and string-based branching.

**Solution:** Shared definitions are authored as ScriptableObjects with typed fields and effect enums/hooks.

**Why:** Designers can configure content in Unity, shared data remains inspectable, and runtime classes consume consistent typed definitions.

**Tradeoffs:** Current effect vocabulary is deliberately limited. More complex conditional behavior may require new typed fields, subclasses, or a more expressive effect system. Runtime-created boss/elite ScriptableObjects also require deliberate cleanup.

### Correct run reset and restart behavior

**Problem:** Persistent singleton managers can leak cards, UI state, health changes, gold, offers, generated definitions, timers, or modal state between attempts and scenes.

**Solution:** `StartRunWithSeed` funnels starts and restarts through one reset path. `CardManager.Reset`, `CombatManager.Reset`, and `RunManager.ResetRuntimeState` clear their domains. `Bootstrap` resets persistent managers on Main Menu load. Frontend transitions restore time scale and input gates.

**Why:** One initialization path reduces drift between direct Game entry, random runs, same-seed retries, and scene transitions.

**Tradeoffs:** Reset methods must remain comprehensive as systems grow. Persistent managers simplify scene transitions but increase the importance of teardown discipline.

### Card drag and selection architecture

**Problem:** Dragging a card out of a layout can collapse the hand, accidental clicks can corrupt selection, and visual reorder should not change card ownership.

**Solution:**

- `CardManager` owns persistent ordered selection.
- `CardView` handles pointer state and drag presentation.
- `Field` inserts a temporary layout placeholder and commits only visual sibling reordering.
- `CardActionDropTarget` resolves the exact enemy and forwards an attack/defense command.
- Invalid or cancelled drags restore the original visual state.

**Why:** This separates interaction presentation from authoritative card-zone mutations and supports both targeted attacks and multi-card defense.

**Tradeoffs:** The interaction has several cooperating classes and requires careful cleanup on pause, reset, encounter transitions, and invalid drops. Hand ordering is currently presentation-only rather than a saved gameplay property.

### Atomic validation before mutation

**Problem:** Multi-card actions, purchases, or events can corrupt state if half of an operation succeeds before a later check fails.

**Solution:** Batch defense validates all views and card instances before discarding; event choices are simulated before application; purchases validate cached offers before spending.

**Why:** Failed actions leave state unchanged and are easier to test and reason about.

**Tradeoffs:** Some validation logic duplicates the structure of application logic and must be kept aligned.

### Event-driven UI updates

**Problem:** Constant UI polling of many systems would couple presentation to implementation and create unnecessary work.

**Solution:** Managers expose focused events such as `OnDeckChanged`, `OnBuildChanged`, `OnStateChanged`, `OnEnemiesChanged`, `OnEncounterResult`, and run-screen events.

**Why:** UI components update in response to meaningful state changes and remain presentation-oriented.

**Tradeoffs:** Subscription order and cleanup matter. `GameController` therefore initializes managers before dependent UI `OnEnable` subscriptions.

### Testing strategy

**Problem:** Roguelike state interactions produce regressions that are difficult to catch through manual play alone.

**Solution:** Plain models and manager APIs are exercised through focused EditMode NUnit tests. Manual Play Mode checks cover scene wiring, interaction feel, visual behavior, and complete flows that are expensive to automate.

**Why:** EditMode tests are fast and precise for invariants; Play Mode is reserved for Unity lifecycle, input, drag/drop, menus, and visual integration.

**Tradeoffs:** There is currently no dedicated PlayMode test assembly; some scene-level verification remains manual and documented rather than continuously automated.

## 5. Code Map

The following study order moves from definitions and plain models toward orchestration and UI integration.

### 1. `Assets/Scripts/Data/CardData.cs`

- **Responsibility:** Shared card suit/rank definition, labels, colors, attack values, and face-card classification.
- **Dependencies:** `UnityEngine.ScriptableObject`.
- **Study goal:** Understand the difference between immutable/shared definition data and owned runtime card state.

### 2. `Assets/Scripts/Data/PlayerRuntime.cs`

- **Responsibility:** Plain player maximum/current health, damage, healing, maximum-health increases, and reset.
- **Dependencies:** None beyond core C#.
- **Study goal:** See a small, testable mutable runtime model with clamping and invariants.

### 3. `Assets/Scripts/Data/CardEnhancementData.cs` and `Assets/Scripts/Data/RelicData.cs`

- **Responsibility:** Authorable Enhancement and Artifact filters/effects.
- **Dependencies:** `CardData`, `CardInstance`, ScriptableObjects.
- **Study goal:** Understand the current typed, data-driven effect vocabulary and its limits.

### 4. `Assets/Scripts/Randomness/DeterministicRandom.cs`

- **Responsibility:** Custom PRNG, stable seed hashing, and named stream derivation.
- **Dependencies:** Core C#.
- **Study goal:** Understand deterministic replay, stream independence, hashing, and versioning tradeoffs.

### 5. `Assets/Scripts/Data/CardInstance.cs`

- **Responsibility:** Stable owned-card identity plus one per-instance Enhancement.
- **Dependencies:** `CardData`, `CardEnhancementData`.
- **Study goal:** Understand why two equal card definitions are still distinct runtime entities.

### 6. `Assets/Scripts/Data/CardCollection.cs`

- **Responsibility:** Authoritative owned/deck/hand/discard zones and all zone mutations.
- **Dependencies:** `CardInstance`, `IRandomSource`, shuffle extension.
- **Study goal:** Follow conservation, duplicate prevention, atomic discard, capacity limits, and discard recycling.

### 7. `Assets/Scripts/View/Field.cs` and `Assets/Scripts/View/CardView.cs`

- **Responsibility:** Hand layout, temporary drag slot, visual reorder, pointer states, and drag/drop forwarding.
- **Dependencies:** UGUI, EventSystem, `CardManager`, `CardActionDropTarget`.
- **Study goal:** Separate visual manipulation from authoritative ownership and actions.

### 8. `Assets/Scripts/Managers/CardManager.cs`

- **Responsibility:** Run card façade, view synchronization, stable ID allocation, draws/discards, selection, gold, Artifacts, Enhancements, and boss rewards.
- **Dependencies:** `CardCollection`, `CardView`, `Field`, `CombatManager`, definition catalogs.
- **Study goal:** Understand how a manager bridges plain state and Unity presentation without making presentation authoritative.

### 9. `Assets/Scripts/Data/EnemyTypeData.cs`, `EnemyAbility.cs`, and `EnemyRuntime.cs`

- **Responsibility:** Enemy definitions, typed behavior hooks, and per-encounter mutable enemy state.
- **Dependencies:** ScriptableObjects, `CombatManager`.
- **Study goal:** Compare shared enemy configuration with independently targetable runtime enemy instances.

### 10. `Assets/Scripts/Managers/CombatManager.cs`

- **Responsibility:** Encounter state machine, card actions, defense, enemy attacks, targeting, health, rewards, and terminal results.
- **Dependencies:** `CardManager`, `EnemyRuntime`, `PlayerRuntime`, Artifact/Enhancement data, input gate.
- **Study goal:** Trace validation-before-mutation, one-shot resolution, multi-enemy behavior, and event publication.

### 11. `Assets/Scripts/Data/RunMapGenerator.cs`

- **Responsibility:** Deterministic map graph topology and content assignment.
- **Dependencies:** `RunRandomContext`, `PathNode`, `MapNodeType`, `RunContentCatalog`.
- **Study goal:** Understand deterministic graph generation and separation of layout/content streams.

### 12. `Assets/Scripts/Data/RunEventDefinition.cs`, `RunEffectResolver.cs`, and `ShopOffer.cs`

- **Responsibility:** Typed events/effects, ordered validation/application, and stable mixed shop offers.
- **Dependencies:** `CardManager`, `CombatManager`, Artifact/Enhancement definitions.
- **Study goal:** Understand safe data-driven choices and cached transaction data.

### 13. `Assets/Scripts/Managers/RunManager.cs`

- **Responsibility:** Complete run orchestration: start/reset, bosses, maps, route nodes, encounters, shops, events, upgrades, rewards, progression, completion, and timing.
- **Dependencies:** Almost every runtime subsystem.
- **Study goal:** This is the central integration file. Study it after understanding the models it coordinates.

### 14. `Assets/Scripts/Data/RunTimingStatistics.cs`

- **Responsibility:** UI-independent run time and map splits.
- **Dependencies:** Core C#; advanced by `RunManager`.
- **Study goal:** Understand monotonic gameplay-time accumulation, pause semantics, and terminal snapshots.

### 15. `Assets/Scripts/UI/GameController.cs` and `Assets/Scripts/Extensions/Singleton.cs`

- **Responsibility:** Explicit manager creation/configuration and persistent singleton lifetime.
- **Dependencies:** Scene references and all three managers.
- **Study goal:** Understand initialization order and why singleton discovery does not silently create unconfigured managers.

### 16. `Assets/Scripts/Frontend/`

Important files:

- `Bootstrap.cs`
- `GameFlowService.cs`
- `FrontendLaunchContext.cs`
- `GameplayMenuCoordinator.cs`
- `GameplayInputGate.cs`
- `PauseMenuUI.cs`
- `DisplaySettingsService.cs`

- **Responsibility:** Scene flow, one-shot launch context, pause/modal stacking, input blocking, and persisted display settings.
- **Dependencies:** SceneManager, Input System, UGUI, managers.
- **Study goal:** Understand cross-scene state, global-state cleanup, and layered modal behavior.

### 17. `Assets/Scripts/UI/`

Key files:

- `ActionButtonsUI.cs`
- `CardActionDropTarget.cs`
- `EnemyDisplayUI.cs`
- `EnemyGroupUI.cs`
- `PathScreenUI.cs`
- `ShopUI.cs`
- `RunEventUI.cs`
- `RunStatusUI.cs`
- `RunResultUI.cs`

- **Responsibility:** Presentation, event subscriptions, and command forwarding.
- **Dependencies:** Managers and UGUI/TMP.
- **Study goal:** Identify where the UI observes state versus where managers perform mutations.

### 18. `Assets/Tests/EditMode/`

- **Responsibility:** Model, integration, regression, frontend, interaction, multi-enemy, and timing tests.
- **Dependencies:** Runtime assembly, Unity Test Framework, NUnit.
- **Study goal:** Tests provide concise executable examples of intended invariants and edge cases.

### 19. `Assets/Data/`

- **Responsibility:** Authored enemy, ability, event, Artifact, Enhancement, and content-catalog assets.
- **Dependencies:** ScriptableObject classes under `Assets/Scripts/Data/`.
- **Study goal:** Compare the code's typed fields with real configured content.

## 6. Data Flow Examples

### Starting a new run

1. `RunSetupUI.StartRun` normalizes the seed and calls `GameFlowService.StartRun`.
2. `GameFlowService` restores time/input globals, stores the seed through `FrontendLaunchContext.RequestRun`, and loads the Game scene.
3. `GameController.Awake` obtains or creates `CardManager`, `CombatManager`, and `RunManager`, then injects scene and content references.
4. `GameController.Start` consumes the one-shot seed and calls `RunManager.StartRunWithSeed`.
5. `RunManager` creates `RunRandomContext`, resets all managers, configures the card RNG stream, builds/deals the starting cards, creates the boss sequence, starts timing, raises `OnRunStarted`, and generates map 1.
6. `PathScreenUI` responds to run events and presents accessible nodes.

### Drawing and playing a card

1. `CombatManager.StartEncounter` asks `CardManager.DrawToHand(1)` for the encounter draw.
2. `CardManager` calls `CardCollection.DrawToHand`, then creates a `CardView` if scene references exist.
3. The player selects a view; `CardManager.ToggleCardSelection` validates the selection policy through `CombatManager.CanAddCardToSelection`.
4. A contextual button or `CardActionDropTarget` submits the selected snapshot and exact `EnemyRuntime` target.
5. `CombatManager.TryPlayCards` validates one hand card, calculates Artifact/Enhancement-adjusted damage, and calls `CardManager.TryDiscardCards`.
6. The card instance moves from hand to discard before effects and target damage resolve.
7. Artifact side effects, Enhancement hooks, enemy abilities, death, kill draw, flee checks, and the surviving-enemy attack are processed.

### Defending with cards

1. Enemy survivors produce combined pending damage in `CombatManager.BeginEnemyAttack`.
2. During `EnemyAttacking`, `CardManager` permits ordered multi-selection only while selected defense is below pending damage.
3. The final selected card may over-block; further additions are rejected.
4. `CombatManager.TryDefendWithCards` validates the complete view set and target.
5. `CalculateSelectedDefense` uses each `CardInstance` plus matching Artifact bonuses.
6. `CardManager.TryDiscardCards` atomically moves every selected card to discard.
7. Pending damage is reduced. At zero or below, combat returns to `PlayerTurn`; otherwise the player may defend again or call `TakeRemainingDamage`.

### Completing an encounter

1. Each defeated enemy adds its reward to `_earnedGoldReward`, leaves the active enemy list, and triggers one card draw.
2. Gold remains secured but unpaid while any encounter enemy remains.
3. When no enemies remain, `CombatManager.ResolveEncounter(Victory)` applies Artifact bonus gold and post-victory healing.
4. `CombatManager` enters `GameWon` and raises `OnEncounterResult` exactly once.
5. `RunManager.HandleEncounterResult` completes the active node.
6. Non-boss victories return to the path screen. Boss victories continue into map completion.
7. On defeat, secured encounter gold is not paid, progression is not granted, timing stops, and `RunResultUI` presents the incomplete split.

### Completing a map

1. The boss encounter resolves as a victory.
2. `RunManager.CompleteActiveNode` marks the boss node completed.
3. `HandleBossVictory` identifies `CurrentBoss` and calls `CardManager.AddBossReward` with its exact source card.
4. `CardManager` rejects duplicates, creates a new definition and unique `CardInstance`, adds it to owned/deck state, and shuffles the deck.
5. `RunTimingStatistics.CompleteCurrentMap` records the split.
6. `bossIndex` advances. If bosses remain, `NextCycle` generates the next indexed map with a fresh map timer.
7. After the twelfth boss, timing stops and `OnRunCompleted` opens victory results.

### Buying an Artifact or Enhancement

1. Entering a Shop node causes `RunManager.PrepareShopOffers` to create cached deterministic offers.
2. Artifact definitions are filtered for ownership and shuffled by a shop-specific stream.
3. Eligible unenhanced `CardInstance` targets and Enhancement definitions are ordered, independently shuffled, and paired into offers.
4. `ShopUI` displays `ShopOffer.StableId`, cached price, target description, and availability reason.
5. `RunManager.PurchaseShopOffer` revalidates the cached offer.
6. Artifact purchases call `CardManager.BuyArtifact`; Enhancement purchases call `CardManager.BuyEnhancement(cardId, enhancement, price)`.
7. Gold is deducted only after validation. Build/deck events refresh presentation.
8. Closing the shop completes the route node; purchasing itself does not prematurely complete it.

### Restarting the same seed

1. The Pause or Results UI calls `RunManager.RestartRun`.
2. `RestartRun` calls `StartRunWithSeed` with the current normalized seed.
3. Combat state, health, cards, views, selection, gold, Artifacts, Enhancements, offers, nodes, generated bosses, completion state, and timing are reset.
4. Named deterministic streams are recreated from the same root seed.
5. The same initial boss order, deck/map signatures, and deterministic offers are reproduced, assuming the same game version and player decisions.

## 7. Testing

### Automated setup

- Test assembly: `Assets/Tests/EditMode/Dealbreak.EditModeTests.asmdef`.
- Framework: Unity Test Framework with NUnit.
- Mode: Editor-only EditMode tests.
- Runtime dependency: `Dealbreak.Runtime` assembly.
- Current Unity discovery: **77 EditMode test cases** across 11 test classes/groups.

### Main tested invariants

- Card IDs are unique and exact instances are conserved across zones.
- Invalid, duplicate, foreign, or repeated card operations are rejected.
- Hand capacity and discard recycling behave correctly.
- The starting collection is 40 Ace–Ten cards with no face cards.
- Boss rewards have exact identity and cannot be duplicated.
- Boss sequence grouping and map progression are correct.
- Map selection does not prematurely complete nodes or unlock successors.
- Same seeds reproduce boss/deck/map signatures; different seeds change runs.
- Named random streams remain independent.
- Map generation supports arbitrary indices.
- Event choices reject lethal or unaffordable costs in authored order.
- Artifact/Enhancement offers and Upgrade choices are deterministic.
- Cached shop prices, negative-price guards, and duplicate purchases are enforced.
- Enhancement state belongs to one card instance and resets correctly.
- Artifact attack stacking is acquisition-order independent.
- PlayerTurn and defense selection policies are enforced.
- Batch defense is atomic.
- Drag placeholders and cancellation restore visual order without corrupting zones.
- Multi-enemy targeting, survivor attacks, kill draws, secured rewards, and Thief flee timing work as specified.
- Frontend launch handoff, modal stacking, input blocking, display settings, and result retry behavior are covered.
- Timing reset, completed splits, incomplete defeat state, map indices beyond twelve, and hour-safe formatting are covered.

### Test groups and discovered counts

- `CardCollectionTests`: 11
- `PlayerRuntimeTests`: 15
- `RunProgressionTests`: 5
- `Phase4DeterminismTests`: 3
- `Phase4RunIntegrationTests`: 6
- `Phase5BuildProgressionTests`: 6
- `Phase6RegressionTests`: 7
- `Phase7AInteractionTests`: 7
- `Phase7BMultiEnemyTests`: 5
- `Phase7FrontendTests`: 6
- `RunTimingStatisticsTests`: 6

### Verification status

The current repository contains **77 discovered EditMode cases**. The development handoff records the 70-test Phase 7B baseline as passing. Seven timing-related additions are present afterward; all seven were included in the latest focused timing run, which passed 9/9 tests when combined with two related existing frontend tests. A new all-77 suite run was not performed as part of this documentation-only task.

### EditMode versus manual Play Mode

EditMode tests cover deterministic logic, models, manager integration, transaction safety, interaction rules, and selected UI-controller behavior without requiring a full manual run.

Manual Play Mode verification documented in `DEALBREAK_DEVELOPMENT.md` covers:

- Direct and Main Menu initialization.
- Combat actions and edge cases.
- Complete twelve-boss progression.
- Shops, events, upgrades, and build effects.
- Drag/drop and contextual target behavior.
- Multi-enemy presentation and targeting.
- Pause/settings/confirmation flows.
- Results and restart actions.
- Runtime timer advance, pause freeze, and resume.
- Visual layout and clean Console checks.

## 8. CV / Portfolio Material

### CV-ready bullet points

- Built a Unity/C# card-roguelike prototype with twelve-map progression, branching encounters, persistent health, multi-enemy combat, shops, events, and build upgrades.
- Designed authoritative runtime card state using stable per-run identities and centralized owned/deck/hand/discard zones, decoupled from Unity UI hierarchy.
- Implemented fully seeded deterministic generation with independently derived RNG streams for card shuffling, boss order, maps, shops, and upgrades.
- Created data-driven ScriptableObject systems for enemies, abilities, Artifacts, per-card Enhancements, events, and run content.
- Developed atomic card-selection, drag/drop, targeted attack, and multi-card defense flows with manager-level validation and presentation-only reordering.
- Maintained 77 discovered Unity EditMode test cases covering state conservation, deterministic replay, progression, economy guards, frontend flow, multi-enemy combat, and run timing.

### Short portfolio description

Dealbreak is a Unity card-based roguelike/RPG prototype built around deterministic twelve-map runs. It combines authoritative runtime card-zone models, single-card attacks, multi-card defense, independently targetable enemy groups, branching maps, seeded shops/events, ScriptableObject-driven content, per-card Enhancements, and reusable frontend/results flows. The project emphasizes deterministic replay, reset correctness, UI/state separation, and focused automated regression coverage.

### Key technical features worth mentioning

- Stable runtime entity identity.
- Model-view separation in a GameObject-based Unity project.
- Deterministic procedural generation with isolated random streams.
- Typed, data-driven ScriptableObject content.
- Event-driven UI presentation.
- Atomic validation and transaction safety.
- Multi-enemy encounter state management.
- Persistent singleton lifecycle and cross-scene reset discipline.
- Unity Test Framework/NUnit regression suite.
- Pause-safe gameplay telemetry and map split tracking.

## 9. Interview / Study Guide

### Runtime models versus ScriptableObject definitions

- **Why Dealbreak uses it:** Shared definitions should not carry mutable per-run state.
- **Where:** `CardData`, `CardInstance`, `EnemyTypeData`, `EnemyRuntime`, `RelicData`, `CardEnhancementData`.
- **Possible questions:** Why not mutate ScriptableObjects at runtime? How would shared asset mutation affect two matching cards? How would you serialize runtime instances?

### Entity identity and collection invariants

- **Why Dealbreak uses it:** Equal-looking cards need independent ownership and Enhancements.
- **Where:** `CardInstance.Id`, `CardCollection.Initialize`, `AddOwnedToDeck`, `CardManager.FindOwnedCard`.
- **Possible questions:** What makes an entity identity stable? How do you prevent duplicate ownership or a card appearing in two zones? Why target upgrades by ID?

### State machines

- **Why Dealbreak uses it:** Combat actions must be legal only during specific phases.
- **Where:** `GameState` and `CombatManager` action validation/transitions.
- **Possible questions:** How are illegal transitions prevented? How would you add a new phase? What state should own pending damage?

### Deterministic random generation

- **Why Dealbreak uses it:** Runs need seed replay without unrelated systems affecting each other.
- **Where:** `DeterministicRandom`, `RunRandomContext`, `RunMapGenerator`, `RunManager` offer generation.
- **Possible questions:** Why not use `UnityEngine.Random`? What is stream independence? How can code changes break cross-version replay?

### Graph-based procedural maps

- **Why Dealbreak uses it:** The player needs route choices, merges, hidden content, and a final boss.
- **Where:** `RunMapGenerator`, `PathNode`, `PathScreenUI`.
- **Possible questions:** How do you guarantee connectivity? How are deterministic layout and content separated? How would you add more topology variation?

### Validation before mutation

- **Why Dealbreak uses it:** Failed multi-card, shop, or event actions must not partially change state.
- **Where:** `CardCollection.TryDiscard`, `CombatManager.CanPlayCards`, `CanDefendWithCards`, `RunEffectResolver`, shop validation.
- **Possible questions:** What makes an operation atomic? Where could rollback otherwise be required? How do cached prices prevent data races or inconsistency?

### Event-driven presentation

- **Why Dealbreak uses it:** UI should react to state without becoming authoritative.
- **Where:** Manager events and UI `OnEnable`/`OnDisable` subscriptions.
- **Possible questions:** What are the risks of C# events in Unity? How do you prevent stale subscriptions? When is polling appropriate?

### Drag/drop and UI layout behavior

- **Why Dealbreak uses it:** Cards must remain visually stable during drag, reorder cleanly, and submit actions without moving model ownership.
- **Where:** `CardView`, `Field`, `CardActionDropTarget`, `CardManager` selection methods.
- **Possible questions:** Why use a placeholder? How are cancelled drags restored? Why is sibling order not authoritative card order?

### Multi-enemy targeting

- **Why Dealbreak uses it:** Encounters contain independent enemies with separate health, attack, rewards, abilities, and flee behavior.
- **Where:** `CombatManager.Enemies`, `EnemyRuntime`, `EnemyGroupUI`, `EnemyDisplayUI`.
- **Possible questions:** How is exact target identity validated? How are combined attacks calculated? When is secured gold awarded?

### Dependency and lifecycle management

- **Why Dealbreak uses it:** Persistent managers must be configured before UI subscriptions and reset across scenes.
- **Where:** `GameController`, `Singleton<T>`, `Bootstrap`, `GameFlowService`.
- **Possible questions:** What problems can service-locator singletons cause? Why is initialization order explicit? How would dependency injection change this design?

### Testing Unity gameplay logic

- **Why Dealbreak uses it:** Many critical rules can be tested without full Play Mode.
- **Where:** `Assets/Tests/EditMode/`.
- **Possible questions:** What belongs in EditMode versus PlayMode tests? Why are plain models easier to test? How would you automate drag/drop or scene flow?

### Pause-safe timing

- **Why Dealbreak uses it:** Playtest pacing should count active participation but exclude explicit pause.
- **Where:** `RunTimingStatistics`, `RunManager.Update`, `GameplayMenuCoordinator`.
- **Possible questions:** Why use scaled delta time instead of wall-clock time? What happens if slow motion is added? How would save/resume affect the timer?

## 10. Prototype Limitations

### Completed and currently implemented

- Twelve-map Jack/Queen/King boss progression.
- Exact boss-card rewards.
- Authoritative card zones and stable card identity.
- Single-card attack and multi-card defense.
- Multi-enemy encounters and exact target selection.
- Branching deterministic maps.
- Seeded bosses, cards, maps, shops, upgrades, and events.
- Artifact and per-card Enhancement build systems.
- Shops, upgrade nodes, events, risks, elites, and boss abilities.
- Main Menu, run setup, settings, pause, confirmation, and results flows.
- Run timer and map splits.
- Focused EditMode test coverage and documented Play Mode verification.

### Prototype or temporary implementations

- UI layout, styling, card visuals, and enemy portraits are temporary.
- Map topology uses a fixed five-column/three-row template.
- The scene has three fixed enemy presentation slots rather than dynamic pooling for arbitrary group sizes.
- Enemy, boss, economy, health, hand size, Artifact, Enhancement, and event values remain prototype balance.
- Bosses have suit-specific abilities, but not richer rank-specific rules.
- Boss rewards currently enter the shuffled draw deck.
- Gold, Artifact ownership, and build catalogs remain in `CardManager`; a larger save/progression architecture may justify separation later.
- Enhancements are limited to one per card and Upgrade nodes currently preselect target cards.
- Display settings are limited to resolution, window mode, and VSync.
- Seed determinism is intended within the current game version, not guaranteed across versions.

### Deferred or not implemented

- Ace pairing and deterministic critical hits planned for Phase 7C.
- Phase 7D map revisions.
- Phase 7E shop inspection/confirmation, player-chosen Enhancement targeting, combat Artifact display, and an Artifact capacity rule.
- General poker-hand or broad multi-card attack combat.
- Save/load or Continue.
- Metaprogression, profiles, achievements, and character selection.
- Networking, online services, and cloud seed exchange.
- Production audio and audio settings.
- Final artwork, animation, accessibility suite, and production UX polish.
- Endless Mode itself; only arbitrary map-index compatibility is present in relevant systems.

## Recommended Study Path

1. Read `CardData`, `PlayerRuntime`, `CardInstance`, and `CardCollection`.
2. Run or read `CardCollectionTests`, `PlayerRuntimeTests`, and `RunProgressionTests`.
3. Study `DeterministicRandom` and `RunMapGenerator`, followed by the Phase 4 tests.
4. Study `CardManager`, then `CardView`/`Field` to compare authority and presentation.
5. Study `EnemyRuntime` and `CombatManager`, then the Phase 7A/7B tests.
6. Study `RelicData`, `CardEnhancementData`, shop/event models, and Phase 5/6 tests.
7. Finish with `RunManager`, frontend flow, UI subscribers, and timing/results code.

The tests are useful as executable documentation: each test name describes a rule the production code is expected to preserve.
