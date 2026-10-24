# Dealbreak

Dealbreak is a single-player, seed-driven card roguelike RPG prototype built with Unity. It combines card-based combat with branching routes and run-specific build choices. The project is actively evolving; descriptions here reflect the current prototype, not a finished or balanced release.

## The run

- Begin with a 40-card collection: Ace through Ten in each of the four suits.
- Travel through branching Districts containing combat, Enforcer, Black Market, Incident, Upgrade, Risk, and Headliner nodes.
- Fight through twelve Districts: four J / Runner-tier Headliners, four Q / Operator-tier Headliners, then four K / Kingpin-tier Headliners. Defeating a Headliner adds that exact face card to the current run.
- Manage persistent HP, your hand, and optional defense between encounters. Each encounter attempts to draw one card, subject to hand capacity.
- Collect Gear and apply Mods to individual cards to shape a build.

## Black Markets and economy

Each District keeps five route depths and has two mandatory, zero-depth Black Market checkpoints: one after Depth 2 and another after Depth 5, immediately before the Headliner. Black Markets do not replace route encounters; all routes pass through both checkpoints, and leaving without buying is allowed. Their inventory, used Services, purchases and Goods Refresh state are separate for each checkpoint within the run.

Black Markets present two sections:

- **Goods:** five slots, each Gear 75% of the time and a Trick (Mod Tricks included) 25%. Gear rolls its rarity 6:3:1 for Common, Rare and Epic; Legendary Gear is never sold. Every Gear is a standalone item: owned Gear and its tiers are not considered, a higher tier never replaces a lower one, and copies of owned Gear can be bought (two copies merge).
- **Services:** the workbook currently names Stake, Intel and Contract. The runtime also has a Supply service; Supply is not listed in the workbook and is recorded as a GameData mismatch in the task report.

**Supply** draws a player-chosen quantity exclusively from the existing Draw Pile into the Hand for 3 Credits per card. Quantity is capped by available Credits, Draw Pile count and free Hand capacity. It never recycles discards or creates cards. The paid **Refresh Goods** action replaces Goods only and leaves Services unchanged. Its price increases within each Black Market from 3 → 5 → 8 → 12 → 17 → 23 → … Credits, then resets at the next checkpoint. Goods already purchased in that Black Market are not offered again.

## Combat and builds

Play a card to attack an enemy. During the defense window, discard cards to block distinct incoming attacks, or choose to take the remaining damage. Recover is available with an empty hand. Encounters may contain multiple independently targetable enemies. Ace pairing combines an Ace and one other card into a single seeded-critical attack. The Hands Emblem enables two- or three-card same-rank attacks; with the Royal Family Heirloom, the exact 10–J–Q–K–A sequence in one effective suit can instantly defeat its target. Other general poker hands are not baseline rules.

**Perfect Block** triggers when the total effective Block exactly matches positive incoming damage after the existing Gear and Mod damage modifiers, across the whole enemy-response defense window. It awards at most one card from the Draw Pile, subject to hand capacity. Only cards already in the Draw Pile before the defense can qualify; an empty pile never recycles discards or defense cards to create a reward.

Gear provide run-level effects; Mods belong to a specific card instance. Cards can hold one Mod. The current build also includes tiered Gear upgrades, charged Runes stored in three non-stacking backpack slots, and Tricks that can be used during a run. Overflow auto-plays up to two cards (Common) or three cards (Rare) drawn while the hand is full, sharing that budget across nested draws within one player action. Rare Cheater grants one non-recursive bonus action before the enemy responds; Common Cheater retains its first-turn behavior. These are prototype systems and their values remain subject to balance review.

## Development milestones

The current Dealbreak GameData workbook defines canonical content names and terminology; its Implemented, acceptance-pending and Draft labels remain distinct. Unity content has not been comprehensively migrated yet. The workbook maps new Gear/Mod names and IDs to legacy Unity asset IDs; those persisted IDs are intentionally unchanged in this rebranding task. Draft content, unresolved ID mappings, and mechanics not in runtime are documented in `Docs/Dealbreak_Rebranding_Report.md`.

Content implementation and acceptance status should be checked against the workbook and current source; older milestone summaries are historical and may be stale.

The most recent recorded regression checkpoint reports **329 EditMode tests and 3 PlayMode tests passing**. This is historical verification from the project notes; it has not been rerun as part of this README update. The Combat & Shop Economy Rebalance changes compiled successfully and passed targeted runtime checks, but its Unity Test Framework suites were **not run** because test tools could not be enabled for this workspace. Do not treat historical counts as verification of this rebalance.

## Seeds and replay

New Run accepts an optional seed. Leaving it blank generates a fresh seed when the run starts; entering a seed lets you replay the corresponding deterministic run. Victory and Defeat results show the actual normalized seed and offer Retry Same Seed. Gameplay does not keep the seed prominently displayed during normal play.

## Open and run the project

1. Install **Unity 6000.3.24f1** with the project's required URP and Input System packages.
2. Open this repository in Unity Hub and allow the project to import and compile.
3. Open `Assets/Scenes/MainMenu.unity` and enter Play Mode. If starting directly in gameplay for development, open `Assets/Scenes/Game.unity`.

There is no standalone release build documented for this prototype. Run save/load support exists; consult the current implementation and compatibility report before changing saved content identifiers.

## Dev Mode

The Editor sandbox is available from `Assets/Scenes/Game.unity` via **Dealbreak → Development → Dev Mode**. Windows builds made with **Development Build** enabled also expose a Dev Mode entry on the Main Menu. Choose an optional seed and the Infinite Health, Infinite Credits, and All Eligible Black Market Goods toggles, then select **Start Dev Run**; ordinary New Run remains unaffected. During an active dev run, the top-of-screen DEV MODE banner and the DEV control open the in-run panel. Cheats are off on application launch, reset when returning to the Main Menu or starting a normal run, and remain enabled across Retry Same Seed. **Disable Cheats** affects only future actions; it does not restore spent HP/Credits or reverse purchases. **Lose Run Now** requires confirmation and routes through the ordinary defeat result.

To make a Windows test build, select **File → Build Profiles → Windows → Build**, enable **Development Build**, and build to a test folder. Development controls and cheat hooks are compiled out of ordinary non-development player builds. A Development Build is intentionally not protected from its users. Dev overrides do not bypass Gear capacity, duplicate prevention, Mod eligibility, or purchase validation. The Editor shop browser remains an Editor-only tool.

## Development notes

- `DEALBREAK_DEVELOPMENT.md` is the historical implementation and design handoff. `Docs/Dealbreak_Internal_Rebranding_Report.md` records current internal names and preserved serialization/save contracts.
- `Locus/knowledge/plan/combat-shop-economy-rebalance-report.md` records combat, mandatory Black Market, Service, Supply and Goods Refresh implementation decisions and verification limits.
- `Locus/knowledge/plan/dealbreak-effect-architecture.md` describes the shared typed Gear/Mod effect system and its current limits.
- `Dealbreak_GameData_IDs_Terminology_Fixed.xlsx` is authoritative for player-facing terminology and content names. Older `the old game-data workbook (removed 2026-10-25)` material is superseded for content terminology and identities.
- `Docs/Dealbreak_Rebranding_Report.md` records synchronized names, remaining legacy-ID mismatches, deferred Draft content, serialization risks, and verification results.

The runtime keeps card identity, ownership, and zones in model state rather than deriving gameplay from UI objects. Runs use independently derived deterministic random streams, and the shared typed effect foundation keeps Gear and Mod behavior out of name-based combat branches.

## Project status

This is a playable prototype, not a final release. UI and artwork are placeholders in places, balance values are provisional, and there is no online play or cross-version determinism guarantee. Consult the development handoff before treating a planned feature as implemented.
