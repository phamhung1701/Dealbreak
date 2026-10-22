# Dealbreak Technical Handoff

## Scope and confidence

This pass audited the current gameplay architecture and applied targeted stability fixes, without changing intended balance or adding gameplay features. Findings below distinguish code-inspection facts, observed Play Mode results, and unverified risks. This is not a claim that every content combination is bug-free.

The workspace already contained unrelated scene, art, font, project-setting, Plastic SCM, and local-tool changes. Those are excluded from this task's commit. Runtime experiments used `Library/Locus/tmp/hardening-smoke/active-run.json`, not the player's active save.

## Current architecture and implemented systems — verified by inspection

- **Run orchestration:** `Assets/Scripts/Managers/RunManager.cs` owns seeded initialization, route selection, node completion, Headliner progression, events, shops, contracts, reward prompts, restart, and infinite continuation. `RunManager.Persistence.cs` captures/restores safe checkpoints. `Assets/Scripts/Data/RunMapGenerator.cs` generates routes including mandatory shop checkpoints; completion unlocks direct successors and guards repeated completion.
- **Combat:** `Assets/Scripts/Managers/CombatManager.cs` owns the turn/encounter lifecycle, multiple enemies, attack/defense selection, pending responses, reactive effects, overflow auto-play, victory and defeat. `Assets/Scripts/Combat/CombatResolver.cs`, `DamageRequest.cs`, `DamageResult.cs`, `CombatActionContext.cs`, and `CombatReactionQueue.cs` separate damage requests, results, action identity and ordered reactions. `AttackSelectionRules.cs` handles card-selection eligibility. `EnemyRuntime.cs` holds per-enemy ability and response state.
- **Cards and inventory:** `Assets/Scripts/Managers/CardManager.cs` connects runtime cards to hand views, selection/targeting, credits, Gear and Trick inventory. `Assets/Scripts/Data/CardCollection.cs` owns deck/hand/discard transitions and card identities. `CardInstance.cs` separates permanent suit/enhancement/attack changes from effect state; duplication copies permanent state rather than transient effect counters. `DeckMutationService.cs` validates duplication, destruction and suit changes.
- **Effects:** Gear, Enhancements/Mods and Consumables/Tricks are ScriptableObject content in `Assets/Data/Gear`, `Assets/Data/Mods`, and `Assets/Data/Tricks`. `Assets/Scripts/Combat/GameplayEffects.cs`, `Assets/Scripts/Data/RunEffectResolver.cs` and manager integration implement combat and run effects. Gear acquisition, capacity, merge/upgrade and ordered runtime instances are implemented. Consumable stacks retain individual instance charges.
- **Economy:** `RunManager` owns authoritative shop offers, stable offer IDs, purchase guards, refresh/service state, sales and checkpoint persistence; `CardManager` owns inventory and credits. Shop commands validate affordability/capacity and reject stale or already-purchased offers. `ShopOffer.cs`, `ShopCheckpointState.cs`, and `GearUpgradeResolver.cs` contain supporting models.
- **Persistence:** `Assets/Scripts/Persistence/RunSaveStore.cs` uses a checksummed envelope, validation, flushed temporary files, primary/backup recovery, and deletion tombstones. `RunSaveValidation.cs` validates snapshots before restore. `SaveContentCatalog.cs` resolves content IDs; `RunSaveData.cs` defines persisted data. `SaveGameService.cs` attaches an autosave writer that polls every 0.25 seconds but writes only at safe boundaries and when content changes (or when forced). Combat and unfinished reward/choice prompts are not persisted mid-resolution. Main-menu exit during combat retains the last safe checkpoint rather than saving the current combat.
- **Frontend/UI:** `Assets/Scripts/Frontend/GameFlowService.cs` transitions MainMenu/Game and starts/continues runs. `GameplayMenuCoordinator.cs` owns modal layers, pause state and selection restoration. `GameplayInputGate.cs` composes input-block reasons. `SavePromptUI.cs` handles replacement and disk-error prompts. UI lives primarily in `Assets/Scripts/UI`, with scene wiring in `Assets/Scenes/MainMenu.unity` and `Assets/Scenes/Game.unity`.
- **Determinism:** `Assets/Scripts/Randomness/DeterministicRandom.cs` supplies seeded streams and restorable RNG state. Run timing, contracts, investments, shop state and resolved reward contexts are included in checkpoint data.

## Confirmed defects fixed

1. **Disk write failure on exit:** menu/quit previously ignored checkpoint failure. Exit now aborts on a reported write failure and presents the error. A non-error refusal at an unsafe combat boundary still permits last-safe-checkpoint exit.
2. **Final enemy killed by a Trick during blocking:** victory was checked only in PlayerTurn. A lethal Trick now resolves victory during EnemyAttacking too, clearing the pending encounter response through normal resolution.
3. **Reset leaked combat context:** queued overflow auto-plays, overflow budget/root/execution flags and bonus-action state now reset with the combat manager.
4. **Invalid encounter input partially mutated combat:** enemy lists are validated and snapshotted before clearing existing state, also avoiding an aliased caller list being cleared during startup.
5. **Reset/rebuild leaked enemy targeting:** clearing card state now cancels enemy targeting and notifies targeting listeners.
6. **Modal layers erased unrelated input locks:** coordinator updates now use owner-scoped reasons, preserving rewards, gear choices and independently held same-bit locks. Global Clear still clears all owners for lifecycle resets.
7. **Bare relative save filenames failed:** save write/delete now create a parent directory only when the path has one.
8. **Save prompt/menu overlap could leave gameplay permanently blocked:** the prompt now owns and releases its own input reason rather than restoring a snapshot of another owner's effective bit. Closing an error prompt and then its parent menu releases all relevant locks correctly.

Regression additions are in `PersistentRunSaveTests.cs`, `Phase7FrontendTests.cs`, `Phase7B5CombatFoundationTests.cs`, `TargetedConsumableTests.cs`, `ContextualConsumableAndEnemyTargetingTests.cs`, and `GameplayMenuCoordinatorGateTests.cs` under `Assets/Tests/EditMode`.

## Verification

### Automated suites — NOT RUN

A focused EditMode run was requested through the Unity Test tool, but the tool reported: **Unity Test tools are disabled for this workspace. Enable them in Settings > Unity Connection.** The user explicitly chose to continue without suites. Accordingly, no focused, broad EditMode, or automated PlayMode pass is claimed. The unchanged blocked request was not repeatedly retried.

After enabling tools, run the six changed fixtures first, then the complete `Dealbreak.EditModeTests` and `Dealbreak.PlayModeTests` assemblies. Existing broad coverage spans combat calculations and reactions, enemy abilities, card mutations, gear rules, shops, events, progression, persistence, frontend interactions, and Play Mode selection/overflow/presentation.

### Observed Unity verification

- Unity 6000.3.24f1 completed compilation and domain reload after the initial fixes.
- Real MainMenu → Game startup with isolated save: seed `hardening-smoke-2026`, 40 owned cards, hand of 8, 18 route nodes, Idle state and a valid safe checkpoint.
- Entered a real encounter, reduced the enemy to a controlled 5 HP through the damage API, granted a Throwing Knife, then used PassPlayerTurn to enter EnemyAttacking with 3 pending damage. Using the actual inventory item resolved GameWon, removed the enemy, zeroed pending damage and completed one route node.
- Forced checkpoint → MainMenu → Continue restored seed, 40 cards, hand of 8, completed-node count and unblocked input.
- A temporary modal preserved an external ConsumableReward input lock after closing.
- Advanced a second encounter via controlled damage commands and entered the mandatory shop. With controlled test credits, a 20-credit Gear purchase succeeded once; the second purchase was rejected without additional debit (36 → 16 → 16). Save/continue restored the active shop, purchased offer and one owned Gear.
- Restart returned to 40 cards, hand of 8, zero credits/Gear/completed nodes, Idle state, and cleared pending enemy targeting/input locks.
- Pointing the temporary store at an existing directory forced an actual write failure. ShowMainMenu left the Game scene active and displayed the save-error prompt.
- With controlled player HP of 1, a normal prepared enemy response caused GameOver at 0 HP, removed the isolated active save, and applied the RunResult input gate.
- Unity Console had no error-level entries during these checks. The deliberate disk-write failure produced an expected warning.

These were scripted smoke checks of real scene wiring and public gameplay commands, not full player-driven runs or balance validation. Controlled damage/credits/HP setup was used to reach edge cases quickly.

Final convergence reported assemblies up to date and the bridge ready. A second Play Mode smoke verified coordinator Push → save-error prompt → Cancel preserved FrontendMenu while the parent menu remained open; parent Pop then produced gate None and time scale 1. The Console again had zero error-level entries. The Editor was returned to Edit Mode with the original MainMenu scene. Task-scoped whitespace checks passed; pre-existing whitespace in unrelated scene/project-setting edits was left untouched.

## Known limitations and remaining technical debt

- **Unverified:** automated regression suites remain unexecuted. Compilation and smoke checks are not a substitute for the full suites.
- **Design question:** prepared pending strikes are snapshots. Killing one attacker with a Trick while others survive currently retains its prepared strike, except explicitly implemented aura adjustments. Existing tests establish snapshot behavior for attack-stat changes but do not settle cancellation-on-death. No rule change was assumed here.
- **Recovery risk:** save validation precedes restore, but restoration mutates managers sequentially. An unexpected runtime exception can leave partially restored in-memory state; restore suspends autosaving on failure. Transactional restore is a future hardening candidate, not a demonstrated corruption fixed in this pass.
- **Platform risk:** power loss, disk-full behavior, locked files and cross-platform atomic replacement have not received exhaustive fault-injection testing. The smoke covered an actual write failure, not all filesystem failure modes.
- **Checkpoint limitation:** combat/unfinished prompts are intentionally unsaved. Timer-only elapsed changes are omitted from the autosave content fingerprint; a crash can lose elapsed timing since the last state-changing/forced checkpoint.
- **Maintainability:** CardManager, CombatManager and RunManager remain large singleton-oriented orchestration classes. Public mutable state and synchronous event callbacks increase coupling; retain focused command guards and tests rather than attempting an unscoped rewrite.
- **UI consistency:** some controls remain visually interactable while input is blocked, although authoritative gameplay commands reject blocked actions. This is a presentation consistency concern, not a verified gameplay bypass.
- **Coverage limitation:** a complete twelve-Headliner victory run, infinite-run soak, every effect combination, platform build and subjective UI/gameplay feel were not verified.

## Recommended next priorities

1. Enable Unity Test tools; run changed fixtures, then both complete test assemblies. Triage failures once, separate pre-existing failures from regressions, and retain machine-readable results in CI.
2. Add cross-platform save fault-injection and true scene-transition integration coverage, including disk-full, locked primary/backup, interrupted replacement and failed restore.
3. Decide and document the defeated-attacker pending-strike rule, then add explicit single-/multi-enemy tests.
4. Add seeded full-run/infinite-run soak coverage checking card-zone uniqueness, nonnegative resources, reward idempotency and reachable progression after every command.
5. Gradually extract manager responsibilities only behind existing behavioral tests; prioritize transactional restore and clear input-lock ownership over cosmetic refactoring.
