---
id: kd_41cce26b-b93f-4a14-bc2a-b96b2490f7b4
injectMode: inherit
aiEditMode: inherit
---

# Dealbreak Phase 3 — completion report

## Scope completed

Phase 3 only: reliable twelve-map boss progression, exact face-card identity and rewards, a 40-card starting collection, and victory-driven map node completion. Branching-map redesign, shop nodes, enemy/boss abilities, multi-enemy combat, and other Phase 4+ systems remain deferred.

## Implementation

- Starting collection is exactly 40 cards: Ace through Ten in all four suits. No Jack, Queen, or King is initially owned.
- Card IDs remain stable and unique. Initial IDs are 1–40; boss rewards continue from the next ID.
- Added atomic runtime insertion of a newly owned card into the draw deck.
- `CardManager.AddBossReward` validates face cards, rejects duplicate rank+suit rewards, clones the exact boss card identity into player-owned runtime data, adds it to owned cards and the draw deck, shuffles, and notifies listeners.
- Retained the existing boss generation structure:
  - Bosses 1–4 are all four Jacks in shuffled suit order.
  - Bosses 5–8 are all four Queens in shuffled suit order.
  - Bosses 9–12 are all four Kings in shuffled suit order.
- Every generated boss now retains its exact runtime `sourceCard`. Run reset owns and cleans up both boss definitions and source-card definitions.
- RunManager now tracks the currently selected node and listens to independent CombatManager encounter results.
- Selecting a path node commits/reveals the route and starts combat, but does not mark the node completed or unlock successors.
- Victory completes the selected node exactly once and unlocks its outgoing nodes. Defeat clears the active encounter without granting progression.
- Boss victory grants the exact source card once, emits `OnBossRewardGranted`, and increments `bossIndex` immediately.
- The next map is generated and announced immediately after boss victory but remains hidden behind the current shop until Continue. Progression no longer depends on `OnShopDone` detecting a completed boss.
- `OnShopDone` now only returns to the prepared current map when the run is not complete.
- Defeating boss 12 grants the final King first, then sets run completion and emits the victory event without indexing beyond the boss list.
- The combat status label reports the exact gained card, for example `Boss defeated! Gained J♣.`

## Automated verification

Compilation/domain reload completed successfully with no Console errors or warnings.

EditMode tests: 30/30 passed.
- Existing card-state and player-health tests remain passing.
- New progression tests verify:
  - Exact 40-card Ace–Ten starting collection and zone conservation.
  - Exact face-card reward insertion, unique IDs, and rejection of duplicate/non-face/null rewards.
  - Twelve boss definitions grouped Jack/Queen/King, each group containing four unique suits and preserved source cards.
  - Node selection reveals/commits without premature completion or successor unlocking.

## Play Mode verification

Initial run:
- 40 owned cards, 32 draw deck, 8 hand, and zero face cards.
- Twelve bosses grouped as four Jacks, four Queens, then four Kings.
- Every rank group contained all four suits.

Full accelerated run:
- Completed 43 total encounters across all twelve generated maps.
- Every selected node was incomplete before combat and completed after victory.
- Every active node cleared after resolution.
- Exactly twelve boss victories produced exactly twelve rewards.
- Reward order exactly matched the generated boss order:
  - `J♦, J♥, J♣, J♠`
  - `Q♥, Q♣, Q♠, Q♦`
  - `K♣, K♥, K♠, K♦`
  for the verified run.
- Final collection contained 52 cards, twelve unique face cards, and 52 unique IDs.
- `bossIndex` reached 12 only after the twelfth boss.
- Final state was GameWon with the `VICTORY!` run screen.
- Final reward feedback identified the exact final King.

Randomization/restart:
- Multiple run starts produced different within-rank suit orders while preserving Jack → Queen → King grouping.
- Restart restored 40 owned cards, zero face cards, eight-card hand, boss index zero, and incomplete run state.

Shop independence:
- First boss reward was already owned, `bossIndex` had already advanced, and the next map was already prepared while the victory shop was still open.
- Continuing from the shop only revealed the already-prepared map.

Final scene is saved and not dirty. No Console errors or warnings. Final read-only review found no Phase 3 blockers.

## Files affected

- `Assets/Scripts/Data/CardCollection.cs`
- `Assets/Scripts/Managers/CardManager.cs`
- `Assets/Scripts/Managers/RunManager.cs`
- `Assets/Scripts/UI/ActionButtonsUI.cs`
- `Assets/Tests/EditMode/RunProgressionTests.cs`

## Deliberately deferred

- Replacing the current isolated-lane map with richer forks and merges.
- Explicit shop/event/elite/upgrade node kinds and route resource budgets.
- Removing the temporary shop-after-every-victory behavior.
- Enemy groups and multiple active enemies.
- Executable normal-enemy and boss ability hooks.
- Boss-specific restrictions and rule modifications.
- Final reward presentation, run-results UI, difficulty selection, and metaprogression.

The rank stat values and reward insertion into the shuffled draw deck remain prototype decisions and can be tuned later without changing the progression foundation.
