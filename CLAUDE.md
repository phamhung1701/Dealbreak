# Dealbreak — project instructions

Unity 6 card combat roguelike. A task prompt defines the work; this file supplies context, rules and hard-won traps. Verify claims against current code: code, authored assets and passing tests are the evidence.

**Owner rulings on how each feature looks and behaves live in [`Docs/OWNER_DECISIONS.md`](Docs/OWNER_DECISIONS.md).** Read the section for the area you touch (combat UI, items, Black Market, map, characters, art, HUD, tooltips, motion) before changing it. New owner rulings go there, not here.

## Game and terminology

- A run is 12 Districts: four J (Runner) Headliners, then four Q (Operator), then four K (Kingpin). Beating a Headliner awards that exact face card. The starting deck is exactly 40 cards (A–10 in four suits, no face cards). Cards have runtime identity and authoritative deck, hand and discard zones.
- Combat: persistent player HP, blocking by discarding cards, empty-hand Recover, multiple enemies with exact targeting, kill draws, secured rewards, Thief fleeing. Runs use seeded random streams.
- `Dealbreak_GameData_IDs_Terminology_Fixed.xlsx` is authoritative for player-facing names and terms: Credits, Gear, Mod, Trick, Black Market, Incident, District, Enforcer, Headliner, Perfect Block. A workbook Draft status is not approval to implement.
- Keep legacy class, field, serialized-ID and save identifiers. Content asset files and folders use the Dealbreak names (`Assets/Data/Gear`, `Mods`, `Tricks`, `Enemies`, `Characters`, `Run/Incidents`; `Scanner.asset`, higher Gear tiers add the rarity: `ClubAmplifierRare`). A fight node's saved `contentId` is its enemy's asset name, so renaming an enemy asset needs a `RunSaveMigrations` step (v5 did this).

## Branches

- Solo project: work directly on `main`. Origin is `phamhung1701/Dealbreak`.
- Older branches (`ui-new-look`, `ui-polish`, `architecture-audit-fixes`, `codex/combat-ui-vertical-slice`) are merged; don't build on them. A screen with flat blue panels, a TAKE DAMAGE button or ELITE/SHOP node labels means a stale commit.

## Working rules

1. Check `git status` first. Preserve unrelated dirty changes, and make focused diffs.
2. Inspect callers and tests, then change the smallest coherent slice. Don't rescan the whole project for small tasks.
3. Determinism: keep existing RNG streams and their consumption order. New randomness gets its own `RunRandomContext.CreateStream("name", key)`. Previews never change state or consume RNG. Never derive gameplay order from scene hierarchy, event subscription order, hash iteration or instance IDs.
4. Runtime state lives on runtime objects, not ScriptableObject definitions. Preserve serialized fields, GUIDs, `.meta` files and scene/prefab references.
5. Combat resolves on the committed `CardInstance` and exact `EnemyRuntime`. `CardView` is only a UI adapter.
6. No content-ID or name checks in managers. Use typed `GearData`, `CardEnhancementData` and `EnemyAbility` data. One mechanic has one execution path.
7. Commit once per problem solved; end each message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Push only when asked.
8. The regression gate is full EditMode plus PlayMode, green. Report what actually ran, the result, and what couldn't run.

## Subagents (`.claude/agents/`)

Use them for work that would otherwise flood this context; each reads this file itself.

- `unity-tester`: runs the regression gate (or a filtered subset) through Locus, handling the abort/soak traps, and returns counts plus failures only. Use it after every gameplay change instead of polling tests inline.
- `invariant-reviewer`: read-only review of the current diff against the rules, invariants and contracts here and the owner decisions. Use it before committing anything non-trivial.
- `content-author`: adds or edits Gear, Mods, Tricks, Enemies or Characters from the workbook end to end (asset, registry, balance, authored text, tests).
- `balance-analyst`: answers balance questions from the recorded run stats (player and bot telemetry) and `GameBalance`, with numbers and sample sizes; recommends, never changes values.

## Combat invariants

- Surviving-enemy retaliation is one aggregate pending-damage instance; don't split it.
- Card damage = `(base + Mod attack bonus) × Gear multipliers + Gear flat bonuses`; for example `(4 + 3) × 2 + 3 = 17`. Preview and execution share one calculation.
- Timing order: discard before on-play draw or recycling; Gear side effects before Mod play effects; damage before a surviving target's post-card ability; kill draw before encounter progression; Pickpocket per completed card action.
- Kill Credits are secured at the defeat and paid only on victory (an aborted or lost fight grants none); the HUD shows them as their coins land (`CombatManager.SecuredCredits`).
- Fleeing is not death: no kill draw and no Credits. Death, reward and terminal results each happen once. The final Headliner reward comes before run completion.
- Event HP costs and resource payments are not combat damage: they never consume Shield or trigger reactions. A Credits payment (`RunEffectType.LoseCredits`) needs the Credits; a penalty (`LoseCreditsUpTo`, `LoseHealthNonLethal`) takes what there is, never below 1 HP, and never blocks the choice.
- Previews never run once-per-combat saves or side effects (Dealbreaker runs only in `PlayerRuntime.ModifyIncomingCombatDamage`). Gear with special rules (Overclock, Dealbreaker, Nuclear Option, Ghost Lens) is specified in the owner decisions.

## Architecture contracts

- Combat resolution: `DamageRequest`/`DamageResult`/`CombatResolver`, per-hit Shield, multi-hit, and a FIFO `CombatReactionQueue`. Recover is one aggregate hit that may consume one Shield charge.
- Every combat command ends through `CombatManager.TryResolveTerminalOutcome`. Encounter results publish after the outermost command unwinds. A reaction runaway ends as `EncounterResult.Aborted` (save kept), never as Defeat.
- UI attack and block go through `CombatCommands`.
- Gear special rules live only in `GearRules*.cs`, keyed by `GearSpecialRule`. Their persisted counters are `GearCounterKeys` (and Mod counters `ModCounterKeys`): save data, never renumber. Typed `GameplayEffectDefinition` data is the only effect path for Gear and Mods (the legacy hook fields are gone).
- Balance numbers live in `Resources/GameBalance` (`GameBalance.Current`): District scaling, Enforcers, Black Market, Contracts, Redline Deal, Gear rules (`gear`), enemy abilities (`enemyAbilities`) and the route layout (`route`). Rules read them; don't add literals. Player-facing text that quotes a number stays authored, and `BalanceTextDriftTests` pins it to the balance, so change both together. Contract choice text is `Resources/QuestContract`.
- Enemy rules key on authored data (`minMapIndex`, `eligibleAsEliteBase`, `allies`), never on `enemyName`. Spawns and previews both use `EncounterComposer`.
- Events dispatch on `RunEventDefinition.flow`. Headliner content lives in `Resources/HeadlinerCatalog`; its pool order is seeded.
- `Resources/RunSaveContent` is the single content registry for play and saves (`ContentRegistryTests`). A schema change bumps `RunSaveValidation.Version` (now 6) and adds a `RunSaveMigrations` step.
- Input-gate reasons are owned: release only the reasons your code set.
- Balance stats: `GameplayBalanceTelemetry` journals every non-tutorial run (unless Tools/Dealbreak/Balance/Record New Runs is off) to `persistentDataPath/BalanceTelemetry`; at each run end `BalanceStats.cs` upserts a summary into `run-stats.json` and rewrites `Stats/*.csv`; Tools/Dealbreak/Balance/Stats shows them. A continued run reopens its journal cut back to the last save checkpoint. Test assemblies turn recording off (`BalanceTelemetryTestIsolation`). New purchase, gain or choice paths should report through it.
- Where things live: `RunManager` partials (`.Shop`, `.Rewards`, `.Events`, `.Contracts`, `.Headliners`, `.Persistence`), `CardManager.Hand`, `Assets/Scripts/Randomness/`, tutorial in `Assets/Scripts/Tutorial`.
- History: `DEALBREAK_ARCHITECTURE_AUDIT.md` (October 2026 audit and resolution table) and `DEALBREAK_DEVELOPMENT.md` (milestone log). The other root docs (`DEALBREAK_COMPLETE_TECHNICAL_REFERENCE`, `TECHNICAL_HANDOFF`, `PROJECT_TECHNICAL_SUMMARY`, `FINAL_QA_REPORT`, `Unity6_*`) are older snapshots: never trust them over code.

## UI essentials

Full specs are in `Docs/OWNER_DECISIONS.md`; these are the rules most often broken.

- No how-to guidance outside the tutorial (`ActionGuidanceCopyTests`); show state and numbers only. Show each fact once.
- Info opens on click next to the clicked object, never on hover or in a fixed panel, through the single `ContextualTooltipUI`.
- Neo-punk editorial look: near-black stage, off-white type, signal red = damage/danger, acid lime = selection/valid target, 1px hairline rectangles, halftone-printed fills. Theme fields keep legacy names: ink = stage, charcoal = surface, slate = hairline/disabled grey, paper = off-white, brick = signal red, ochre = acid lime. Styling goes through `DealbreakUIRuntime`, `DealbreakUIStyle`, `EditorialFrame`, `EditorialButton`, `EditorialSurface`.
- Many layouts are written by editor authoring scripts in `Assets/Editor/` (`DealbreakNeoPunkAuthoring`, which runs `DealbreakUiQaAuthoring` last; `DealbreakCentralStageAuthoring`; `DealbreakUIArtCorrectionAuthoring`). When moving an element, change the scene and its authoring script together.

## Driving Unity (Locus MCP)

- Load the tools with ToolSearch (`+locus unity`). The loop is: edit, then `unity_recompile`, then `unity_test_run` (EditMode: `Dealbreak.EditModeTests`, about 2 min). Baseline (2026-10-24): **EditMode 1882 passed + 1 skipped (1883 cases), PlayMode 10/10 plus the 12-District soak**.
- Tool calls are capped at about 5 minutes.
  - The full EditMode run blocks the main thread long enough that `unity_test_run` aborts with `native_main_thread_unresponsive` while the run goes on. Start it from `unity_execute` instead: `UnityTestApi.Start(new Locus.UnityTestRunOptions { Mode = Locus.UnityTestMode.Edit, Assemblies = new[]{"Dealbreak.EditModeTests"}, ResultDetail = Locus.UnityTestResultDetail.Failures })`, then poll `UnityTestApi.Status(runId)` in later calls. PlayMode and the soak work the same way (`Mode = Play`, `Groups`).
  - Run PlayMode with `groups: ["!TwelveDistricts"]`, then the 12-District soak separately (80–100 s, often over the cap). Never poll it with `unity_execute` and `request_editor_status: "editing"`: that leaves Play mode and aborts the soak. After the soak, wait until `Application.isPlaying` is false, then read `C:\Users\Admin\AppData\LocalLow\hwng\Dealbreak\TestResults.xml` and check its timestamp.
- **Never enter Play mode with `Game.unity` open: it continues the owner's real save.** Open `MainMenu.unity` (only if it has no unsaved changes), enter Play, then call `GameFlowService.Instance.StartDevRun(seed, false, false, false)`. Dev runs never save. `StartRun` would offer to replace the real save; never confirm it.
- If PlayMode hangs, check `Time.timeScale`. A test failing while a menu has it at 0 stalls every later test.
- Edit scenes and prefabs through the Unity API (`EditorSceneManager`, `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`) or with careful, verified YAML edits.
- If Locus is down: `dotnet build Dealbreak.Runtime.csproj -p:OutDir=<scratch>/` (also `Assembly-CSharp-Editor`, `Dealbreak.EditModeTests`, `Dealbreak.PlayModeTests`). This only proves it compiles; say so in the commit.

## Repository traps

- `Packages/com.farlocus.locus`, `Locus/` and `.plastic/` are live-updated by tools, and Unity locks `locus_native.dll`. Don't commit their tool churn (only `Locus/knowledge` notes you edited on purpose). A checkout or stash touching them fails half-way. To switch branches: back up the dirty files, merge in a `git worktree` outside the project, point HEAD at the branch (`git symbolic-ref`, then `git reset`), then check out only the non-Locus paths.
- Line endings are mixed per file. With Python, use `newline=''` and match each file's newline style; the `Edit` tool handles this itself.
- In Python heredocs, a C# `"\n"` becomes a real newline unless written as a raw string. Check scripted edits with `grep -n '"[A-Z]*$'`.
- **Unity fake null:** `GetComponent<T>() ?? AddComponent<T>()` fails in the Editor. Use `TryGetComponent` or `== null`.
