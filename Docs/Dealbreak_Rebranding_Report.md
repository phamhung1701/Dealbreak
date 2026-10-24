# Dealbreak Branding and GameData Rework Report

This is the earlier content-name audit. The subsequent internal code/assembly renames and compatibility exceptions are recorded in [Dealbreak_Internal_Rebranding_Report.md](Dealbreak_Internal_Rebranding_Report.md).

**Status:** Rebranding/content-name pass completed for matched content. The confirmed source of truth was `Dealbreak_GameData_IDs_Terminology_Fixed.xlsx`. Gameplay values, serialized IDs, asset filenames/GUIDs, class/field/enum names, and save keys were preserved. Unresolved GameData/runtime conflicts are recorded below.

## Updated

- Updated README and active project guidance to use Dealbreak terminology and point to the confirmed workbook; historical Dealbreak technical/design documents now identify their terminology as superseded.
- Updated **120 ScriptableObject display names** from the Rename Index, including Gear, Mods, Tricks, Incidents, implemented Enemies, and the twelve generated Headliner names. Updated **113 descriptions** from workbook fields; the 13 Incident assets also received workbook descriptions and choice labels/results. For Gear/Mods/Tricks, descriptions use labeled workbook cells (Trigger/Condition/Effect/Value/Limit or the applicable category fields).
- Updated runtime player-facing labels for Credits, Gear, Mods, Tricks, Black Market, Incident, District, Enforcer, Headliner, and Perfect Block; changed the service labels to Stake, Intel, and Contract.
- Updated Main Menu title/credits and Game-scene labels, including Gear, Black Market, Headliner, Incident, and Credits text. `ProjectSettings.asset` already had `productName: Dealbreak` in the pre-existing worktree change; it was not edited here.
- Updated affected test expectations to the new canonical names/labels.

## Missing or mismatched content

- Workbook rows **Ghost Lens (`gear_035`)** and **Dealbreaker (`gear_036`, Legendary)** are Draft and have no matching Unity Gear asset. `Dealbreaker` remains distinct from the game title and was not fabricated. **Network Map (`gear_132`)** is explicitly deferred and has no matching asset.
- The workbook lists `Dealbreak ID` → `Legacy Unity ID` mappings. A read-only audit matched 117 content crosswalk objects; **44 runtime `id` fields differ from the workbook’s Legacy Unity ID**. For most Gear/Mods/Tricks, `canonicalId` carries the legacy value while save lookup uses a descriptive `id` slug; the four early Gear rows have no `canonicalId`. No ID was changed.

| Category | Runtime `id` → workbook Legacy Unity ID |
|---|---|
| Gear (10) | `artisan_tools`→`rel_101`; `golden_compass`→`rel_102`; `reinforced_sleeves`→`rel_103`; `victory_draught`→`rel_104`; `club_power`→`rel_001`; `fire_emblem`→`rel_018`; `diamond_power`→`rel_003`; `heart_power`→`rel_002`; `mimic_emblem`→`rel_017`; `spade_power`→`rel_004` |
| Mod (9) | `sharpened`→`enh_001`; `reinforced`→`enh_002`; `mending`→`enh_003`; `quickdraw`→`enh_004`; `devouring`→`enh_005`; `lethal`→`enh_006`; `explosive`→`enh_009`; `vampiric`→`enh_010`; `shifting`→`enh_011` |
| Incident (6) | `roadside_cache`→`evt_001`; `lost_caravan`→`evt_002`; `gamblers_shrine`→`evt_003`; `dangerous_shortcut`→`evt_004`; `field_lessons`→`evt_101`; `training_grounds`→`evt_102` |
| Trick (19) | `bread`→`con_001`; `throwing_knife`→`con_002`; `whetstone`→`con_003`; `mirror`→`con_004`; `torch`→`con_005`; `bomb`→`con_006`; `iron_plate`→`con_007`; `needle_thread`→`con_008`; `quiver`→`con_009`; `soul_stone`→`con_010`; `venom`→`con_011`; `midas_coin`→`con_012`; `talisman`→`con_013`; `blood_vial`→`con_014`; `prism`→`con_015`; `spade_rune`→`con_016`; `heart_rune`→`con_017`; `club_rune`→`con_018`; `diamond_rune`→`con_019` |
- Three implemented/acceptance-pending Enemy names were synchronized: Goblin → Scrapper, Thief → Pickpocket, and Master Thief → Ghost. Draft Enemy renames (Knight/Security Guard, Brute/Heavy, Shieldbearer/Riot Guard, War Drummer/Hype Man, Duelist/Challenger, Goblin Captain/Crew Leader, Royal Knight/Riot Captain) were left unchanged because current runtime logic branches on some legacy `enemyName` values. Refactoring those checks is a separate task. `EnemyTypeData` has no serialized description field; workbook ability-description text was not moved into a new field or used to alter current AI behavior.
- Twelve generated Headliners now use workbook names by rank/suit. `headliner_013` / The House remains an undefined optional placeholder and was not added as an encounter. Headliner names and ability/scaling proposals are marked Draft in the workbook; no proposed mechanics were implemented.
- The workbook guide says IDs remain stable, while the Rename Index provides new canonical prefixes (`gear_*`, `mod_*`, `foe_*`, `headliner_*`, `incident_*`, `service_*`) mapped to legacy IDs (`rel_*`, `enh_*`, `enemy_*`, `boss_*`, `evt_*`, `shop_*`). The runtime lookup/save IDs may additionally be descriptive slugs. A migration must resolve this conflict explicitly; no IDs were changed here.
- The four early Gear rows (`gear_101`–`gear_104`) provide names but no structured effect/description fields. Their display names were updated, but existing descriptions remain pending authoring rather than being invented.

## Conflicts and future implementation

- Runtime has a Supply Service, absent from the workbook’s canonical Services list.
- Workbook Contract cost is **10 Credits**, while runtime charges **20 Credits**. This task changed terminology/display text only; the value mismatch remains for a separate gameplay decision.
- The workbook’s newly proposed Enemy roster, Headliner ability pool, district scaling, and other Draft content remain unimplemented. Workbook Gear rows also include Draft, deferred, and acceptance-pending entries; status must be checked row-by-row before treating them as shipped.

## Serialization risks and verification

- Save capture/restore resolves Gear, Mods, Tricks, and Incidents by existing `id` fields; their identifiers were left intact. Renaming IDs or asset files would risk save compatibility and Unity references. Remaining legacy identifiers include `RelicData`/`ArtifactRuntimeInstance`, `CardEnhancementData`, `ConsumableData`, `MapNodeType.Boss/Elite/Event/Shop`, old field names such as `gold`/`ownedArtifacts`, and the old dev-mode PlayerPrefs prefix (renamed to `Dealbreak.DevMode.*` on 2026-10-25). `Assets/Editor/GameDataGenerator.cs` still contains legacy generated content names/descriptions despite its updated menu titles; do not run it until it is updated to use the workbook. These items are candidates for a separate serialization-aware refactor.
- Unity compilation and domain reload completed successfully. Five focused EditMode tests passed: canonical asset immutability, canonical content definitions, Game-scene readability, 12-Headliner progression, and result-screen summary/retry. Two stale assertions were updated to the new Gear/credits/District labels; the full test suite was not run.
- Git boundary approved: stage the rebranding task files and touched documentation; leave `.plastic` metadata and all unrelated pre-existing changes unstaged.
