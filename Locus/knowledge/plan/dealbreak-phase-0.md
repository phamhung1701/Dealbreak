---
id: kd_22b1327f-5061-406a-a48e-de4e343a6f3f
injectMode: inherit
aiEditMode: inherit
---

# Dealbreak Phase 0 — baseline and implementation handoff

## Status and scope

Phase 0 completed: local recovery point verified, current scene/data configuration captured, and baseline gameplay choices confirmed below. No gameplay source, scene, prefab, or asset edits; no Play Mode, recompile, or gameplay tests performed. Later phases require separate go-ahead.

## Recovery baseline

- Local Git baseline commit: `2f68b705b57bb61ddcb3ac7091a7a4db0124924a` (`chore: capture Dealbreak prototype before phase 1`).
- Captures 349 previously untracked files: Assets (including .meta files), Packages (including the embedded Locus package), ProjectSettings, .vsconfig, existing Unity setup/recreation guides, and ignore.conf. Existing .gitignore/.gitattributes remain unchanged.
- Excludes Unity-generated caches, local Plastic workspace metadata (.plastic), and Locus session/workspace data. These excluded paths were not deleted or altered.
- Git LFS integrity verified: `git lfs fsck` passed. No differences or untracked files remained under Assets, Packages, or ProjectSettings immediately after capture.
- This is a LOCAL recovery checkpoint, not an off-machine backup. Nothing pushed or published. Preserve the repository's .git directory, including .git/lfs/objects; a commit ID or pointer-only archive alone is not a full binary backup.
- For recovery, inspect/restore from this commit into a separate working location with Git LFS available. Do not reset the active workspace over subsequent user changes.
- Unity 6000.3.24f1; URP; New Input System. Package restore still requires normal Unity/package availability.

## Verified scene/configuration snapshot

- Active scene: Assets/Scenes/Game.unity; loaded, not dirty at inspection. Five root objects; no instantiated manager components in Edit Mode.
- Enabled build order: MainMenu, Game, Bootstrap. SampleScene is disabled.
- Runtime Bootstrap is created through RuntimeInitializeOnLoadMethod; Bootstrap scene is not the actual initialization authority.
- GameController has handField, dragCanvas, card prefab, and all four relic references assigned.
- HandField.cardsHolder incorrectly points to Canvas.RectTransform. Canvas has nine children, so the eight-card capacity calculation currently prevents dealing. Preserve this defect in the baseline; repair in Phase 1.
- Managers are lazily auto-created; RunManager has no serialized enemy assignments. Normal encounter construction therefore receives null enemy definitions. Repair explicit configuration in Phase 1.
- Enemy assets: Thief HP10/ATK4/gold5; Goblin HP15/ATK6/gold7; Knight HP25/ATK8/gold10. All have no source card and empty ability arrays.
- Relic assets: Club/Spade cost 15; Heart/Diamond cost 20. Effects currently gated by string IDs in CombatManager.
- Existing prefabs: Card, PathNodeButton, ShopItemButton; retain and adapt.

## Confirmed progression requirements for implementation

The user's twelve-map correction supersedes the earlier three-boss interpretation.

- Twelve major maps, one exact face-card boss per map.
- Maps 1–4: all four Jacks, independently shuffled each run.
- Maps 5–8: all four Queens, independently shuffled each run.
- Maps 9–12: all four Kings, independently shuffled each run.
- Retain RunManager.BuildBossDeck's rank-grouped creation/shuffle algorithm, bossIndex, and useful progress helpers.
- Starting usable collection excludes all twelve face cards.
- Defeating a boss grants that exact rank-and-suit card once, for the remainder of the run.
- Explicitly preserve boss identity; do not derive rewards by parsing display names. Existing sourceCard field is a possible minimal implementation route.
- Complete nodes after successful encounter resolution, not selection.
- Preserve owned cards, upgrades, gold, and Artifacts across maps.
- Advance after boss victory/reward resolution, independent of shop UI.
- Grant/record the final King's reward before showing run completion. Do not infer persistent metaprogression.

## Execution sequence after Phase 0

1. Reliable initialization and authoritative card ownership; persistent click selection; fix hand/config wiring; reset cleanly. Keep presentation reusable.
2. One complete combat encounter: player HP, card defense plus residual damage, intentional draw/empty-hand rules, independent encounter result, health UI.
3. Reliable twelve-map boss progression and exact rewards; retain existing rank-grouped boss shuffle.
4. Simple branching templates with explicit shops/resource budgets/reveal rules; encounter groups and small enemy/boss ability hooks.
5. Small Artifact/Enhancement/event slice; optional combo permission only for builds that unlock it.
6. Regression coverage and balancing (tests introduced alongside preceding phases, not postponed entirely).

Avoid a full Unity restart, generic rule scripting, dependency-injection frameworks, a global event bus, complex character systems, or a full poker catalogue. Keep one playable character and single-card default combat.

## Combat decisions confirmed during Phase 0

- Suit powers remain Artifact-gated. Do not implement the earlier recommendation to make all suit powers baseline. Ordinary cards still deal their normal attack value without an Artifact.
- Optional card defense: discard any number of cards to reduce incoming damage, then explicitly take the remainder as HP damage. Zero HP ends the run.
- Start the run with eight cards. Carry the remaining hand between combat encounters. Add only one card when entering each combat encounter, subject to the provisional eight-card cap. Do not refill to eight at each encounter and do not add an automatic draw each player turn. An encounter means a combat node inside a map, not an entire major map; noncombat nodes do not imply a free draw.
- Empty-hand emergency recovery: when empty-handed during the player's action phase, Recover suffers an unblocked enemy attack, then draws one card if the player survives. This is an explicit exception to the no-turn-draw rule, not a free draw. It must not also trigger a second ordinary retaliation for the same action. If the hand empties during defense, finish that defense before exposing the recovery action.
- Existing discard recycling on an empty draw pile is a provisional implementation carryover, not a separately approved new design decision. Review interaction with Hearts and validate that recovery cannot softlock before shipping Phase 2. Detailed balance (starting HP, enemy damage, Hearts behavior) remains for combat implementation/playtesting.
- Standard Ace–Ten starting composition (40 cards) and hand capacity eight remain provisional prototype settings rather than final balance promises. Exact boss reward identity and run-local duration were already confirmed and must not be asked again.

These decisions supersede the earlier steady-turn-draw recommendation. They are recorded here as implementation inputs, not implemented gameplay.

## Acceptance gates

Phase 0: complete. Recovery commit and LFS verified; setup snapshot captured; suit powers, optional defense, encounter drawing, and emergency recovery confirmed. Balance and recovery/recycling edge checks belong to Phase 2.

Phase 1: exactly one initialization; card conservation across model piles and hand; dragging never changes ownership; click-then-Play/Discard works; clean restart; both menu and direct-Game entry work.

Later: full/partial/no defense; last-card and draw effects independent of Destroy timing; twelve unique bosses grouped by rank; no premature progression; each exact face-card reward once; all map routes reach their boss; combo play rejected without unlock; enhancements affect the selected card instance only.
