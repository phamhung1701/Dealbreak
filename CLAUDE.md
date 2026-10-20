# Dealbreak — project instructions

Unity 6 card combat roguelike. A task prompt defines the work; this file supplies context, rules and hard-won traps. Verify claims against current code: code, authored assets and passing tests are the evidence.

## Game and terminology

- A run is 12 Districts: four J (Runner) Headliners, then four Q (Operator), then four K (Kingpin). Beating a Headliner awards that exact face card. The starting deck is exactly 40 cards (A–10 in four suits, no face cards). Cards have runtime identity and authoritative deck, hand and discard zones.
- Combat: persistent player HP, blocking by discarding cards, empty-hand Recover, multiple enemies with exact targeting, kill draws, secured rewards, Thief fleeing. Runs use seeded random streams.
- `Dealbreak_GameData_IDs_Terminology_Fixed.xlsx` is authoritative for player-facing names and terms. Use Credits, Gear, Mod, Trick, Black Market, Incident, District, Enforcer, Headliner and Perfect Block. Keep legacy class, field, serialized-ID, asset-path and save identifiers. A workbook Draft status is not approval to implement.

## Branches

- Solo project: work directly on `main`, which has the new Dealbreak look and all later work. Origin is `phamhung1701/Dealbreak` (the old `Cardpg` URL redirects).
- Older branches (`ui-new-look`, `ui-polish`, `architecture-audit-fixes`, `codex/combat-ui-vertical-slice`) are merged into `main`; don't build on them.
- If a screen shows the old look (flat blue panels, a TAKE DAMAGE button, ELITE/SHOP node labels), you're on a stale commit.

## Working rules

1. Check `git status` first. Preserve unrelated dirty changes, and make focused diffs.
2. Inspect callers and tests, then change the smallest coherent slice. Don't rescan the whole project for small tasks.
3. Determinism: keep existing RNG streams and their consumption order. New randomness gets its own `RunRandomContext.CreateStream("name", key)`. Previews never change state or consume RNG. Never derive gameplay order from scene hierarchy, event subscription order, hash iteration or instance IDs.
4. Runtime state lives on runtime objects, not ScriptableObject definitions. Preserve serialized fields, GUIDs, `.meta` files and scene/prefab references.
5. Combat resolves on the committed `CardInstance` and exact `EnemyRuntime`. `CardView` is only a UI adapter.
6. No content-ID or name checks in managers. Use typed `GearData`, `CardEnhancementData` and `EnemyAbility` data. One mechanic has one execution path.
7. Commit once per problem solved; end each message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Push only when asked.
8. The regression gate is full EditMode plus PlayMode, green. Report what actually ran, the result, and what couldn't run.

## Combat invariants

- Surviving-enemy retaliation is one aggregate pending-damage instance; don't split it.
- Card damage = `(base + Mod attack bonus) × Gear multipliers + Gear flat bonuses`; for example `(4 + 3) × 2 + 3 = 17`. Preview and execution share one calculation.
- Timing order:
  - discard before on-play draw or recycling
  - Gear side effects before Mod play effects
  - damage before a surviving target's post-card ability
  - kill draw before encounter progression
  - Pickpocket per completed card action
- Fleeing is not death: no kill draw and no Credits. Death, reward and terminal results each happen once. The final Headliner reward comes before run completion.
- Event HP costs and resource payments are not combat damage: they never consume Shield or trigger reactions.

## Architecture contracts

- Combat resolution: `DamageRequest`/`DamageResult`/`CombatResolver`, per-hit Shield, multi-hit, and a FIFO `CombatReactionQueue`. Recover is one aggregate hit that may consume one Shield charge.
- Every combat command ends through `CombatManager.TryResolveTerminalOutcome`. Encounter results publish after the outermost command unwinds. A reaction runaway ends as `EncounterResult.Aborted` (save kept), never as Defeat.
- UI attack and block go through `CombatCommands`.
- Gear special rules live only in `GearRules*.cs`, keyed by `GearSpecialRule`. Their persisted counters are `GearCounterKeys`, which are save data; never renumber them. Prefer typed `GameplayEffectDefinition` data.
- Enemy rules key on authored data (`minMapIndex`, `eligibleAsEliteBase`, `allies`), never on `enemyName`. Spawns and previews both use `EncounterComposer`.
- Events dispatch on `RunEventDefinition.flow`. Headliner content lives in `Resources/HeadlinerCatalog`; its pool order is seeded.
- `Resources/RunSaveContent` is the single content registry for play and saves (`ContentRegistryTests`). A schema change bumps `RunSaveValidation.Version` and adds a `RunSaveMigrations` step.
- Input-gate reasons are owned: release only the reasons your code set.
- Where things live:
  - `RunManager` partials: `.Shop`, `.Rewards`, `.Events`, `.Contracts`, `.Headliners`, `.Persistence`
  - `CardManager.Hand`
  - `Assets/Scripts/Randomness/`
- History: `DEALBREAK_ARCHITECTURE_AUDIT.md` (the October 2026 audit with its resolution table) and `DEALBREAK_DEVELOPMENT.md` (the milestone log).

## Owner UX decisions

- **No in-game guidance; a tutorial is coming.** Show state and numbers (damage and block previews, Recover cost, enemy intents, choice requirements), never how-to hints. `ActionGuidanceCopyTests` pins this.
- **No bloated UI:** show each fact once. For example, pending damage appears only under the enemies as "INCOMING n".
- **Hover info opens next to the hovered object**, never in a fixed panel. The ATTACK and BLOCK buttons have none, and on the route map only a scouted node does.
- No Take Damage button; PASS skips the blocking phase.
- Every played card (attack or Block) and every Incident discard goes straight back into the draw pile, which reshuffles. The discard pile no longer fills in play; Ace Protocol and Diamond Driver take their card from the draw pile.
- Supply is repeatable within a Black Market.
- Black Market Goods: each slot is Gear 75% / Trick 25%; Gear rarity 6:3:1 Common/Rare/Epic, never Legendary. Shop Gear is standalone (`BuyStandaloneGear`): no tier-upgrade preference or replacement, and owned copies are sold.
- Tricks and Gear are dragged like cards (`ItemDropRules`, `ItemDragUI`): a card Trick onto a hand card (one card per drop, one charge each), damage onto an enemy, heal onto the HP bar, either onto the Black Market to sell with no confirmation, a Gear onto a matching copy to merge them into the next tier (the result takes the slot it was dropped on), and either onto the red DISCARD strip that covers the run header while an item is held. There is no SELL panel, Sell Items mode, Merge button or Trick USE button. The HP panel stays visible beside the compact hand.
- Enemy drops go straight into the backpack when there is room. With the backpack full the drop waits on the board (`RunManager.PendingEnemyDrop`) and the run waits with it until it is dragged in or left (LEAVE IT).
- Clone Vial (formerly Copycat) works like Balatro's Cryptid: copies of a hand card (`ConsumableData.copies`, 2) go into the hand.
- Card Tricks only ever target cards in hand. Mods earned outside the Black Market (Incidents, Hammer) arrive as their Mod Trick; there is no select-a-card dialog and no Training (Upgrade) node.
- Choosing a route has no map button. Inside a fight, Incident or Black Market a MAP button (top right) opens the route map view-only above the screen; CLOSE MAP returns to it.
- CONTINUE TO MAP sits right of the Black Market menu. Each offer tile has a price button ("5 Credits") that buys at once; its rules text opens beside the tile on click. Supply and Stake have MIN/MAX.
- Scouting (Black Market Intel, the Late-Night Diner tip, Lantern) reveals one seeded-random reachable node whose contents are unknown, of any kind. Fights show their enemies only once scouted (`PathNode.scouted`); Headliners are always known.

## UI architecture (new look)

- Gear and Trick icons are painted item art reprinted as screen prints in their own colours by `Tools/PrintArt/make_item_icons.py` (sources in `Tools/PrintArt/item_sources`, output in `Assets/Art/Items`), assigned as `iconSprite` and drawn through `ItemIconUI`. One icon per Gear family; every tier shares it.
- Art direction (owner, 2026-10-21): modern neo-punk editorial, not retro comic or handmade paper. Near-black stage, off-white type, signal red for attack/damage/danger, acid lime for selection and valid targets. Functional UI is flat rectangles with 1px hairlines; a border appears only for grouping, selection or state. Texture (halftone, grain) lives in artwork and one faint background grain. Buttons differ by weight (`EditorialButton.Role`: solid red, solid off-white, outline, bare text, red outline), not by shape. Scene corrections live in `DealbreakNeoPunkAuthoring`, the last authoring pass.
- Styling: `DealbreakUIRuntime` and `DealbreakUIStyle`. Theme color fields keep legacy names: ink = stage, charcoal = surface, slate = hairline/disabled grey, paper = off-white, brick = signal red, ochre = acid lime. Components: `EditorialFrame`, `EditorialButton`, `EditorialSurface`.
- Many layouts are written by editor authoring scripts in `Assets/Editor/` (for example `DealbreakUIArtCorrectionAuthoring`). When moving an element, change the scene and its authoring script together.
- `ContextualTooltipUI` is the single hover detail. Sources are `CombatInspectUI` (cards, enemies, Gear) and `ContextualInfoTrigger` (everything else, including map nodes).
  - Placement: hand cards open above the card; everything else opens right, then left, above, below.
  - It draws at sorting order 90: above gameplay, below modal menus (100).
  - Panels that open beside slots must call `ContextualTooltipUI.RegisterObstacle(rect)`.
- Timing and motion:
  - `AttackCardPresentationUI.ImpactDelay(hitIndex)` is when a hit visibly lands; enemy HP drain, shake and the defeat fade wait for it.
  - `DisplayedNumber` eases HP and Credits.
  - `BoardPanelTransition` fades panels (`FadeIn` for modal overlays).
- Map: `MapNodePresentation` draws node glyphs; an ochre edge marks scouted nodes. `RunManager.GetNodeDetail`, `IsScoutedNode` and `NodeDetailsVisible` define what a node reveals.

## Driving Unity (Locus MCP)

- Load the tools with ToolSearch (`+locus unity`). The loop is: edit, then `unity_recompile`, then `unity_test_run` (EditMode: `Dealbreak.EditModeTests`, about 40 s). Baseline: **EditMode 675 passed + 1 skipped, PlayMode 10/10**.
- Tool calls are capped at about 5 minutes.
  - Run PlayMode with `groups: ["!TwelveDistricts"]`, then the 12-District soak test separately (80–100 s, often over the cap).
  - After the soak, wait until `Application.isPlaying` is false, then read `C:\Users\Admin\AppData\LocalLow\hwng\Dealbreak\TestResults.xml` and check its timestamp.
- **Never enter Play mode with `Game.unity` open: it continues the owner's real save.**
  - Open `MainMenu.unity` (only if it has no unsaved changes), enter Play, then call `GameFlowService.Instance.StartDevRun(seed, false, false, false)`.
  - Dev runs never save. `StartRun` would offer to replace the real save; never confirm it.
- If PlayMode hangs, check `Time.timeScale`. A test failing while a menu has it at 0 stalls every later test.
- Edit scenes and prefabs through the Unity API (`EditorSceneManager`, `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`) or with careful, verified YAML edits.
- If Locus is down: `dotnet build Dealbreak.Runtime.csproj -p:OutDir=<scratch>/` (also `Assembly-CSharp-Editor`, `Dealbreak.EditModeTests`, `Dealbreak.PlayModeTests`). This only proves it compiles; say so in the commit.

## Repository traps

- `Packages/com.farlocus.locus`, `Locus/` and `.plastic/` are live-updated by tools, and Unity locks `locus_native.dll`. A checkout or stash touching them fails half-way. To switch branches:
  1. Back up the dirty files.
  2. Merge in a `git worktree` outside the project.
  3. Point HEAD at the branch (`git symbolic-ref`, then `git reset`).
  4. Check out only the non-Locus paths.
- Line endings are mixed per file. With Python, use `newline=''` and match each file's newline style; the `Edit` tool handles this itself.
- In Python heredocs, a C# `"\n"` becomes a real newline unless written as a raw string. Check scripted edits with `grep -n '"[A-Z]*$'`.
- **Unity fake null:** `GetComponent<T>() ?? AddComponent<T>()` fails in the Editor. Use `TryGetComponent` or `== null`.
