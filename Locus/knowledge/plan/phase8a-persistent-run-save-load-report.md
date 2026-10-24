---
id: kd_3d76f43b-b61d-4ec6-b1b8-96e3dc06a86d
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# Phase 8A — Persistent Run Save/Load handoff

## Result

Implemented one active checkpoint save under `Application.persistentDataPath/active-run.json`, with versioned DTOs, integrity validation, atomic replacement, backup recovery, safe autosaving, direct restoration, Continue and New Run replacement confirmation. No gameplay balance or authored effect definition changed.

The architecture, checkpoint rules and Phase 8B boundary are documented in `DEALBREAK_DEVELOPMENT.md` under **Phase 8A — Persistent Run Save/Load**.

## Ownership and persistence

- Runtime authority remains in CardManager, CombatManager, RunManager and their existing models. Their persistence partials explicitly capture/restore values; they do not regenerate the map/bosses or replay grants, purchases, draws, node effects or encounter startup.
- Version 1 uses a `safe-node` checkpoint. DTOs contain stable content/card/Artifact IDs, exact collection ordering, mutable per-source state and Artifact identity allocation, all Shop caches/guards, run counters, Contract/Investment state, generated bosses, route flags and timing.
- The three stateful random sources (cards, criticals, Duelist suit) restore exact PRNG states. Already-generated outcomes and deterministic context counters are retained.
- `Assets/Resources/RunSaveContent.asset` provides definitions independently of scene initialization. It currently mirrors 74 Artifacts, 12 Enhancements, 19 Consumables and the Game scene's encounter/event catalogs. Update this asset alongside content catalogs; explicitly version incompatible changes.
- Storage uses flushed same-directory staging files and atomic primary move/replace. `.bak` is independently validated; an invalid primary cannot replace a healthy backup. A deletion tombstone prevents terminal saves from reappearing through backup recovery. Checksums are integrity checks, not anti-cheat or encryption.

## Supported boundaries

- Settled route, normal Shop, authored Event/Risk, or cached Upgrade choice.
- Before route commitment/encounter startup and after completed Shop purchases, refreshes, sales and other stable transactions. A 0.25-second safe-state poll plus pause/quit/menu flush covers remaining changes without writing unchanged state every frame.
- Combat, resolver work and unfinished reward/Artifact/Hammer/Blood Ritual/Quest selection sequences retain the previous safe snapshot. Closing during combat returns to the route immediately before encounter selection, with the earlier HP/cards/RNG restored. Temporary prompts and callbacks are not saved.
- Final victory, defeat and explicit Abandon Run clear all active recovery files. Entering Infinite Mode after victory begins a new checkpoint sequence. Dev Mode does not overwrite or delete the normal saved run.
- Continue restores models first, then presentation. Direct Game-scene entry also resumes a valid save. New Run requires confirmation before replacing existing files, including invalid ones; cancellation preserves them.

## Verification actually performed

### Compilation and suites

- Unity compilation completed successfully after the implementation and final fixes.
- Added **20 EditMode test cases** in `Assets/Tests/EditMode/PersistentRunSaveTests.cs`.
- Added **1 PlayMode regression case** in `Assets/Tests/PlayMode/SaveLoadSceneTransitionTests.cs`.
- Automated results: **0 executed / 0 passed / 0 failed**. Focused and combined EditMode/PlayMode requests were rejected because Unity Test tools are disabled. This is **not** a passing regression gate.
- Luna completed the initial read-only state/checkpoint audit. The frontend implementation delegation stopped at its usage limit before editing; the main agent implemented and reviewed the frontend locally. No independent final review is claimed.

### Staged runtime/storage checks

These were Unity runtime/API and UI-callback checks, not NUnit runs or a natural playthrough:

1. Exact initial JSON round-trip: 40 owned cards, 12 generated bosses and 18 route nodes.
2. Saved Shop restored after a Play Mode/domain restart with identical non-timing snapshot digest. Continue-button Game → Main Menu → Game reload also matched exactly, with eight hand views for eight model cards and the Shop visible. The staged state included 77 Gold, checkpoint 16, Refresh count 1, an enhanced/suit-changed card with permanent attack +4, Artifact counter 42, and a backpack Consumable.
3. RNG continuation matched the next critical result **20977**, with identical subsequent shuffled deck ordering.
4. Exiting mid-combat refused a new save and preserved the previous JSON. Restart restored the exact pre-combat digest, HP **26 → saved 30**, Idle combat state and the original available route.
5. Corrupted primary recovered the valid backup; both corrupt copies were rejected. Deletion blocked resurrection and a subsequent valid write restored normal operation. A missing-primary rewrite retained its earlier backup. An incompatible on-disk version with a valid checksum was rejected.
6. New Run cancellation retained the existing seed. Confirmation created the requested replacement seed with 40 cards, zero Gold and Map 1; its backup belonged to the replacement run rather than the old run.
7. Quest objective selection was excluded while pending; confirmation completed both the route node and Shop cache and produced a valid save with the active Contract.
8. Accelerated defeat and final-boss victory cleared the save and blocked subsequent autosave resurrection. The final-boss path still awarded its exact face card through the existing reward path.
9. Game/Main Menu/Continue presentation passed after fixing an inactive-parent Shop fade-coroutine error. Main Menu Continue was visibly enabled for a valid save and disabled after cleanup.

No save existed in the production slot before testing. Smoke-test run files were cleared afterward; additional storage fixtures live only under `Library/Locus/tmp/`.

### Workspace/tooling status

The workspace moved from the old project folder to `D:/Dealbreak/Dealbreak` during the task. The implementation was present in the active checkout. Unity temporarily disconnected during import, then reconnected without a forced restart; the final missing-primary and incompatible-file checks ran successfully there.

The last pre-move Console check after the UI fix had **0 errors and 1 unrelated Unity account-service warning**. The post-move Console also reports a project-file-generator sharing violation on generated `.csproj` files. No save/load runtime error was reported by the final storage checks. These tooling issues were not suppressed by modifying gameplay or Locus packages.

## Task-related files

### New persistence foundation

- `Assets/Scripts/Persistence/RunSaveData.cs`
- `Assets/Scripts/Persistence/RunSaveValidation.cs`
- `Assets/Scripts/Persistence/RunSaveStore.cs`
- `Assets/Scripts/Persistence/SaveGameService.cs`
- `Assets/Scripts/Persistence/SaveContentCatalog.cs`
- `Assets/Resources/RunSaveContent.asset`
- `Assets/Scripts/Managers/CardManager.Persistence.cs`
- `Assets/Scripts/Managers/CombatManager.Persistence.cs`
- `Assets/Scripts/Managers/RunManager.Persistence.cs`

### Owner contracts and integration

- `Assets/Scripts/Combat/GameplayEffects.cs`
- `Assets/Scripts/Data/CardCollection.cs`
- `Assets/Scripts/Data/CardInstance.cs`
- `Assets/Scripts/Data/ConsumableInstance.cs`
- `Assets/Scripts/Data/PlayerRuntime.cs`
- `Assets/Scripts/Data/RunTimingStatistics.cs`
- `Assets/Scripts/Randomness/DeterministicRandom.cs`
- `Assets/Scripts/Managers/CardManager.cs`
- `Assets/Scripts/Managers/CombatManager.cs`
- `Assets/Scripts/Managers/RunManager.cs`

### Frontend, tests and documentation

- `Assets/Scripts/Frontend/FrontendLaunchContext.cs`
- `Assets/Scripts/Frontend/GameFlowService.cs`
- `Assets/Scripts/Frontend/RunSaveMenuUI.cs`
- `Assets/Scripts/Frontend/SavePromptUI.cs`
- `Assets/Scripts/UI/GameController.cs`
- `Assets/Scripts/UI/MainMenuUI.cs`
- `Assets/Scripts/UI/BoardPanelTransition.cs`
- `Assets/Tests/EditMode/PersistentRunSaveTests.cs`
- `Assets/Tests/PlayMode/SaveLoadSceneTransitionTests.cs`
- `DEALBREAK_DEVELOPMENT.md`
- `Locus/knowledge/plan/phase8a-persistent-run-save-load-report.md`

Include Unity-generated `.meta` files for new assets/scripts/folders. Existing scene/prefab GUIDs and references were preserved. The unrelated README edit, TMP fallback font, Plastic/Locus metadata, untracked technical reference, workbook and Docs files remain outside the Phase 8A commit.

## Remaining limitations / next verification

- Enable/repair Unity Test tools and run the new tests, adjacent frontend/progression/effect/economy suites and full regression suites.
- A packaged executable close/relaunch, actual OS crash/power-loss injection, other target platforms and a full twelve-map run were not performed. The relaunch check used a Unity Play Mode/domain restart.
- Pending transactions deliberately roll back to their prior safe checkpoint. There is no mid-combat save, prompt continuation, multiple slot/cloud sync or automatic old-version migration.
- Cross-build determinism requires compatible content/schema versions. Oversized or structurally invalid files are rejected, not partially loaded.
- Phase 8B should add a new combat checkpoint DTO and explicit between-action boundaries, including enemy state, source-aligned pending hits, ordered reactions, turn/overflow state and reward guards. Keep the storage envelope and existing owner contracts; never restore combat by replaying encounter-start effects or serializing callbacks.
