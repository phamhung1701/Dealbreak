---
id: kd_250b9cd2-1600-42e2-b7ac-42a0d2162a47
injectMode: inherit
aiEditMode: inherit
---

# Dealbreak Phase 2 — completion report

## Scope completed

Phase 2 only: one complete combat encounter foundation with player health, optional card defense, residual damage, one-card encounter draw, emergency recovery, encounter-result events, and health/action UI. Twelve-boss rewards, initial face-card exclusion, node-completion timing, map/shop restructuring, and other Phase 3+ work remain deferred.

## Implementation

- Added `PlayerRuntime` with provisional configurable maximum health (default 30), current health, full run reset, clamped damage, and defeat state.
- `GameController` exposes `playerMaxHealth` (30 in `Assets/Scenes/Game.unity`), validates it, and configures CombatManager before run start.
- Player HP persists between encounters and resets only when a run resets.
- `StartEnemy` now attempts to add exactly one card, subject to the existing eight-card hand capacity. It no longer refills the hand.
- After a nonlethal card play, the enemy attack becomes pending damage.
- Defensive discards reduce pending damage. Full defense immediately returns to PlayerTurn without HP loss.
- Partial or zero defense remains in EnemyAttacking until the player explicitly chooses Take Damage. The unblocked remainder damages HP, then combat returns to PlayerTurn if the player survives.
- Emptying the hand during defense does not cause automatic defeat and does not leave defense state; Take Damage remains available.
- Recover is available only with an empty hand during PlayerTurn. It applies one unblocked current enemy attack and, if the player survives, draws one card. It does not trigger a second retaliation.
- Added one-shot `EncounterResult.Victory` / `Defeat` event independent of UI. Terminal results, gold, and result events are guarded against duplication.
- ShopUI now opens from a Victory encounter result rather than interpreting GameState directly. Existing mandatory post-victory shop progression is intentionally retained until the later run-flow phase.
- Added Take Damage and Recover buttons plus a Player HP label to `Assets/Scenes/Game.unity` and wired them through `ActionButtonsUI`.
- Artifact-gated suit behavior remains unchanged.

## Automated verification

Compilation/domain reload completed successfully.

EditMode tests: 26/26 passed.
- Existing 11 card collection/identity/conservation tests.
- New player-health tests covering default/configured health, invalid maximums, configure/clamp behavior, reset, partial/exact/overkill damage, actual damage returned, no-op damage, and defeat state.

## Play Mode verification

Initial integration:
- Player initialized at 30/30 HP.
- New Take Damage, Recover, and Player HP references were valid.
- No Console errors or warnings.

Optional defense:
- Test attack 10 with a defense value of 5 left 5 pending.
- Take Damage reduced HP from 30 to 25, cleared pending damage, and returned PlayerTurn.
- A later attack of 1 was fully blocked by a defensive card; HP stayed 25 and combat returned PlayerTurn.

Emergency recovery:
- With an empty hand in PlayerTurn, Recover was enabled.
- A recovery against attack 4 reduced HP from 25 to 21, drew exactly one card, left pending damage at zero, and remained PlayerTurn.
- No encounter result or second retaliation occurred.

Defeat:
- Lethal recovery produced 0 HP, GameOver, exactly one Defeat result, zero pending damage, and no victory shop.
- A repeated Recover call did not duplicate the result.

Encounter draw and victory result:
- A seven-card hand became eight when the encounter began, confirming exactly one attempted encounter draw under the hand cap.
- Defeating the test enemy produced GameWon, exactly one Victory result, one gold reward, and opened the shop through the result event.
- Repeating the play command did not duplicate the result/reward.
- Card ownership remained conserved at 52 during the runtime checks.

Final scene is saved and not dirty. Active scene is `Assets/Scenes/Game.unity`. No Console errors or warnings. Generated PlayerSettings/TMP atlas changes from testing were restored.

## Files affected

- `Assets/Scenes/Game.unity`
- `Assets/Scripts/Data/PlayerRuntime.cs`
- `Assets/Scripts/Data/EncounterResult.cs`
- `Assets/Scripts/Managers/CombatManager.cs`
- `Assets/Scripts/UI/ActionButtonsUI.cs`
- `Assets/Scripts/UI/GameController.cs`
- `Assets/Scripts/UI/ShopUI.cs`
- `Assets/Tests/EditMode/PlayerRuntimeTests.cs`

## Deliberately deferred

- Excluding face cards from the starting deck.
- Exact face-card boss rewards.
- Moving node completion to successful encounter resolution.
- Removing shop-dependent map progression and making shops actual nodes.
- Run result/restart presentation.
- Boss/enemy ability hooks, multi-enemy encounters, and map generation expansion.
- Final balance for player HP, enemy values, hand size, Hearts recycling, and recovery damage.

The default 30 HP is a prototype tuning value, not a final design commitment.
