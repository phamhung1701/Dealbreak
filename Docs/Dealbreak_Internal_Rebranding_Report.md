# Dealbreak internal rebranding

Date: 2026-10-13. This follows the content-name audit in [Dealbreak_Rebranding_Report.md](Dealbreak_Rebranding_Report.md).

## Completed renames

- Assemblies and their definition files: the three old-name assemblies (runtime, EditMode tests, PlayMode tests) → `Dealbreak.*`. Assembly references and explicit Unity `m_EditorClassIdentifier` metadata follow the new names. Runtime types remain in the global namespace; no new namespace boundary was introduced.
- Gear: `RelicData` → `GearData`; `ArtifactRuntimeInstance` → `GearRuntimeInstance`; `ArtifactUpgradeResolver` → `GearUpgradeResolver`; `ArtifactRailUI`/`ArtifactIconSlotUI` → `GearRailUI`/`GearIconSlotUI`. Gear capacity/special-rule enums, inventory/catalog fields, purchase, reward, merge, sale, reaction-source, choice, and effect APIs use Gear. The redundant `BuyRelic` wrapper was consolidated into `BuyGear`.
- Headliner: deck/index, current definition/description, generation, reward, encounter flags, suit abilities, and presentation references use Headliner. `MapNodeType.Headliner` retains numeric value 6.
- Credits: runtime currency fields, reward fields, gain/spend APIs, change events, and effect names use Credits. Canonical content symbols such as `GoldenVault` and Golden Mod remain unchanged.
- Dependent editor scripts and EditMode/PlayMode tests follow the new APIs. Renamed scripts, assembly definitions, and test files retain their original `.meta` files. Historical root/design documents have Dealbreak filenames and explicit notices pointing here for current API names.
- The Game scene's twelve Gear/Headliner/Credits hierarchy labels and their readability-test paths now use current names. GameDataGenerator's 49 Gear/Mod constructor name/description pairs and Enemy/ability labels were synchronized from the canonical authored assets captured before the rename; IDs, prices, effect parameters, and generation order remain unchanged. The generator was not run to mutate gameplay content.

## Compatibility boundary

- Existing `.meta` contents and GUIDs are preserved. Asset, scene, and prefab edits are confined to explicit runtime type metadata; serialized field keys and object references remain intact. `FormerlySerializedAs` maps renamed Unity fields, and `MovedFrom` records the old assembly/type for Unity object types and renamed Gear enum types.
- `RunSaveData.cs` remains the version-1 wire contract. Its legacy keys/types (`gold`, `artifacts`, `artifactId`, `artifactIdWatermark`, `bossIndex`, `bosses`, `preBossShopGoods`, `ArtifactSaveData`, `BossSaveData`) are deliberately preserved. Capture/restore translates the runtime names at this boundary. No save schema/content/envelope version changes.
- Stable Gear, Consumable, Enhancement, Card, Incident and ability IDs, canonical IDs, upgrade IDs, card/runtime ownership IDs, and numeric enum values remain unchanged. Unity content asset filenames and data directories remain stable because authoring tools and tests address them by path; renaming every content file offers no compatibility benefit.
- Persistent route strings (`boss`, `shop-preboss`), shop/refresh/purchase identifiers (`artifact:`, `enhancement:`, `consumable:`), RNG stream names and legacy seed constants and the dev-mode PlayerPrefs keys remained unchanged at the time (renamed 2026-10-25). Shop purchase history now uses an explicit stable prefix for Gear instead of deriving it from the renamed enum member.
- `CardManager`, `CombatManager`, `CardEnhancementData`, and the Consumable definition/runtime APIs remain accurate technical names. Mod/Trick are their player-facing categories. Draft enemy/content renames and the prior audit's unresolved content/economy decisions are outside this rename; gameplay name checks are unchanged.
- The repository/root directory, remote repository URL, product/company settings, persistent save path, historical workbook filename, and historical Locus plan filenames remain unchanged. The original content audit and dated snapshots retain legacy names when explaining past implementation or compatibility contracts.
- Unity recovery-scene backups retain their original bytes and historical type metadata. They are not active build scenes.

## Verification

Unity **6000.3.24f1** compiled the renamed runtime, editor, EditMode and PlayMode code successfully in batchmode. Verification used a cached, isolated project copy exported from the final Git index; the original open Editor and its generated files were not used for batch execution.

| Check | Result |
|---|---|
| Focused persistence, Gear, Headliner, and migration cases in the final suite | **60/60 passed**, including all four new rebranding tests |
| Complete EditMode suite after fixes | **604/624 passed; 20 baseline failures** |
| Complete PlayMode suite | **7/7 passed** |
| Captured pre-rename version-1 save | Original checksum/envelope validated; restore/capture preserves exact payload, card zones/IDs, Gear ownership/state, Headliner sequence, and RNG state |
| Existing player save, using a read-only copy | **1/1 probe passed**; original version-1 data validates and restores/captures identically; input file unchanged |
| Active build scenes and prefabs | No missing script components detected; Gear catalogs and populated Credits/Headliner label references survive migration |
| Existing metadata | **512 existing `.meta` files byte-identical**, including renamed files |
| Authored text assets | **163 checked**; data values, serialized keys, object references and content IDs unchanged; differences confined to explicit type metadata and twelve reviewed hierarchy labels |
| Save contract, randomness and preferences | Wire members/schema unchanged; seed defaults, hashing inputs and PlayerPrefs keys unchanged |
| Git whitespace check | Passed |

The broader run exposed stale content-name assertions left by the earlier audit and five reflection-based cases using old currency member names. Those assertions/references were corrected without changing behavior assertions or content values. A separate pre-rename baseline probe ran 194 affected cases: 157 passed and 37 failed. **Every remaining final failure also fails on that pre-rename source and asset snapshot.**

| Remaining baseline failures | Cases |
|---|---:|
| Trick availability/charge assertions (`DeckMutationTests`) | 3 |
| Deck viewer snapshot/layout assertions (`DeckViewerUITests`) | 5 |
| Headless UI button width thresholds (`GameSceneReadabilityTests`) | 2 |
| Intel reveal assertion (`LowerEffortShopTests`) | 1 |
| Captain encounter preview totals (`Phase2EliteCombatTests`) | 3 |
| Map node-count expectation (`Phase4DeterminismTests`) | 1 |
| Traveler Pack stacking/shrink assertion (`Phase6GearTests`) | 1 |
| Hammer retry event assertion (`Phase7GearMechanicsTests`) | 1 |
| EditMode destruction log (`Phase7BMultiEnemyTests`) | 1 |
| Duplicate Gear acquisition assertions (`Phase7EGearCapacityTests`) | 2 |

These existing gameplay/UI disagreements were preserved to honor the no-behavior-change scope. No manual visual/input smoke test was performed; the headless PlayMode and scene/prefab checks above are the presentation verification completed here. Local XML/log evidence is under `Logs/Rebranding/` (ignored by Git); the synthetic legacy fixture is committed under `Assets/Tests/EditMode/Fixtures/LegacyRunV1.json`. The existing player's save and temporary probe are excluded from the commit.

The original worktree contained unrelated code, tests, project settings, art, workbook and tool metadata changes. These are preserved and excluded from the rebranding commit; verification of the final staged source is performed on an isolated Unity project copy.
