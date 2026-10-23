# Dealbreak — project instructions

Unity 6 card combat roguelike. A task prompt defines the work; this file supplies context, rules and hard-won traps. Verify claims against current code: code, authored assets and passing tests are the evidence.

## Game and terminology

- A run is 12 Districts: four J (Runner) Headliners, then four Q (Operator), then four K (Kingpin). Beating a Headliner awards that exact face card. The starting deck is exactly 40 cards (A–10 in four suits, no face cards). Cards have runtime identity and authoritative deck, hand and discard zones.
- Combat: persistent player HP, blocking by discarding cards, empty-hand Recover, multiple enemies with exact targeting, kill draws, secured rewards, Thief fleeing. Runs use seeded random streams.
- `Dealbreak_GameData_IDs_Terminology_Fixed.xlsx` is authoritative for player-facing names and terms. Use Credits, Gear, Mod, Trick, Black Market, Incident, District, Enforcer, Headliner and Perfect Block. Keep legacy class, field, serialized-ID and save identifiers. Content asset files and folders use the Dealbreak names (`Assets/Data/Gear`, `Mods`, `Tricks`, `Enemies`, `Run/Incidents`; `Scanner.asset`, higher Gear tiers add the rarity: `ClubAmplifierRare`); name new assets the same way. A fight node's saved `contentId` is its enemy's asset name, so renaming an enemy asset needs a `RunSaveMigrations` step (v5 did this). A workbook Draft status is not approval to implement.

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
- Kill Credits are secured at the defeat and paid only on victory (an aborted or lost fight grants none); the HUD shows them as their coins land (`CombatManager.SecuredCredits`).
- Fleeing is not death: no kill draw and no Credits. Death, reward and terminal results each happen once. The final Headliner reward comes before run completion.
- Event HP costs and resource payments are not combat damage: they never consume Shield or trigger reactions. A Credits payment (`RunEffectType.LoseCredits`) needs the Credits; a penalty (`LoseCreditsUpTo`, `LoseHealthNonLethal`: Back-Alley Trap's run) takes what there is, never below 1 HP, and never blocks the choice.
- Overclock (`rel_141`, Legendary, `GearSpecialRule.SignatureMods`): while owned, every modded card plays its Mod's Signature version (`CardEnhancementData.signatureName`/`signatureDescription`/`signatureEffects`). `ModSignature` is the one switch: the effect resolver, card names, plates and info all read through it, so nothing is saved and previews match execution. Owner rules (2026-10-24): a Signature changes how the card plays, never just a bigger number. Cover Fire shoots the weakest other enemy (no RNG); a Bulwark card blocks without leaving the hand, once per enemy turn; Transfusion gives at most 1 Shield per fight however many cards have it; Joker counts as any rank for every rank check (groupings and Gear alike, via `CardInstance.MatchesRank`/`IsFaceCard`/`RanksBelow`/`RanksAbove`/`SharesRankWith`). A Mod Trick's text names the Signature it gives (`ModSignature.TrickDescription`, authored `signatureTrickText`), and a Signature card's look (`CardModFxUI`) runs faster with two wires racing round it. Card-side counters are `ModCounterKeys` (save data; never renumber); numbers are `gear.executionerHealthPercent`, `tutor*`, `chainReactionMaxBlasts`.
- Dealbreaker (`rel_036`, Legendary): once per combat, an enemy hit that would be lethal and that Shield will not absorb destroys one random hand card (picked by card id from a node-keyed `dealbreaker` stream) and is negated. It runs only in `PlayerRuntime.ModifyIncomingCombatDamage` via `CombatManager.TryNegateLethalHit`, never in previews; an empty hand keeps the charge.

## Architecture contracts

- Combat resolution: `DamageRequest`/`DamageResult`/`CombatResolver`, per-hit Shield, multi-hit, and a FIFO `CombatReactionQueue`. Recover is one aggregate hit that may consume one Shield charge.
- Every combat command ends through `CombatManager.TryResolveTerminalOutcome`. Encounter results publish after the outermost command unwinds. A reaction runaway ends as `EncounterResult.Aborted` (save kept), never as Defeat.
- UI attack and block go through `CombatCommands`.
- Gear special rules live only in `GearRules*.cs`, keyed by `GearSpecialRule`. Their persisted counters are `GearCounterKeys`, which are save data; never renumber them. Prefer typed `GameplayEffectDefinition` data; it is the only effect path for Gear and Mods (the legacy hook fields are gone).
- Balance numbers live in `Resources/GameBalance` (`GameBalance.Current`): District scaling, Enforcers, Black Market, Contracts, Redline Deal, Gear rules (`gear`), enemy abilities (`enemyAbilities`) and the route layout (`route`). Rules read them; don't add literals. Player-facing text that quotes a number stays authored, and `BalanceTextDriftTests` pins it to the balance, so change both together. Contract choice text is `Resources/QuestContract`.
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

- **No in-game guidance outside the tutorial.** Show state and numbers (damage and block previews, Recover cost, enemy intents, choice requirements), never how-to hints. `ActionGuidanceCopyTests` pins this. How-to copy lives only in the scripted tutorial run (`Assets/Scripts/Tutorial`, lessons in `TutorialScript`, content in `Resources/TutorialRun`); the first NEW RUN starts it until `TutorialProgress` is set, it never touches the run save, and its steps refuse off-script commands through `TutorialGate`.
- **No bloated UI:** show each fact once. For example, pending damage appears only under the enemies as "INCOMING n".
- **Info opens on click, next to the clicked object** (owner, 2026-10-21), never on hover and never in a fixed panel; a second click or a click anywhere else closes it, whatever opened it (a selected card or Gear included, 2026-10-23). A selected hand card or Gear shows its own; clicking an enemy targets it and opens its info; a button whose click acts (PASS) shows its info on click only while unavailable. Keyboard focus also opens it. The ATTACK and BLOCK buttons have none, and on the route map only a scouted node does.
- No Take Damage button; PASS skips the blocking phase. No Recover button either (owner, 2026-10-23): on the player's turn with an empty hand, once the board settles, Recover runs on its own after a short beat (`ActionButtonsUI.TickAutoRecover`, calling `CombatManager.Recover`).
- Every played card (attack or Block) and every Incident discard goes straight back into the draw pile, which reshuffles. The discard pile no longer fills in play; Ace Protocol and Diamond Driver take their card from the draw pile.
- Supply is repeatable within a Black Market.
- Nuclear Option (`rel_100`, Legendary): one attack may commit up to 5 cards of any kind (`gear.nuclearOptionMaxCards`) on one enemy, each card hitting separately. Ace pairs and Hands same-rank sets keep their rules; a one-suit Royal Flush (10-J-Q-K-A) defeats instantly.
- Black Market Goods: each slot is Gear 75% / Trick 25%; Gear rarity 6:3:1 Common/Rare/Epic, never Legendary. Shop Gear is standalone (`BuyStandaloneGear`): no tier-upgrade preference or replacement, and owned copies are sold.
- Tricks and Gear are dragged like cards (`ItemDropRules`, `ItemDragUI`): a card Trick onto a hand card (one card per drop, one charge each), damage onto an enemy, heal onto the HP bar, either onto the Black Market to sell with no confirmation, a Gear onto a matching copy to merge them into the next tier (the result takes the slot it was dropped on), a Gear onto another Gear or an empty Gear slot to move it there (`CardManager.MoveGear`; the order is the trigger order), a Trick onto another Trick slot to move it, and either onto the red DISCARD strip that stands down the right edge (between the MAP button and the draw pile) while an item is held. There is no SELL panel, Sell Items mode, Merge button or Trick USE button. The HP panel stays visible beside the compact hand.
- Enemy drops go straight into the backpack when there is room. With the backpack full the drop waits on the board (`RunManager.PendingEnemyDrop`) and the run waits with it until it is dragged in or left (LEAVE IT).
- Clone Vial (formerly Copycat) works like Balatro's Cryptid: copies of a hand card (`ConsumableData.copies`, 2) go into the hand.
- Card Tricks only ever target cards in hand. Mods earned outside the Black Market (Incidents, Hammer) arrive as their Mod Trick; there is no select-a-card dialog and no Training (Upgrade) node.
- (owner, 2026-10-22) A Mod Trick replaces a card's existing Mod. A Crystal also takes a card already of its suit and still spends a charge (Gear that counts Trick uses needs it). Destroying the last owned card loses the run at once (`CombatManager.ResolveDeckedOut`).
- Item text (owner, 2026-10-22): each Gear and Trick reads as one short sentence, the workbook's "Ingame description". Gear prices follow rarity: Common 10, Rare 20, Epic 30.
- Choosing a route has no map button. Inside a fight, Incident or Black Market a MAP button (top right) opens the route map view-only above the screen; CLOSE MAP returns to it.
- The Black Market is two narrow columns, Goods and Services, with CONTINUE TO MAP pinned at the foot of the services (outside the services scroll, so it is always visible; the tile geometry in `ShopOfferUI` is tight so the four services fit). Each offer tile has big art and, beside it, the name over a price button ("5 Credits") that buys at once; its rules text opens beside the tile on click, without the price. A bought tile leaves a gap that closes, so the rows below slide up. In dev mode the Black Market stocks every Gear, Trick and service. (owner, 2026-10-23) A plain lack of Credits shows no text, it only disables the price button (`RunManager.GetShopOfferBlockedReason` gives the reasons that do show). Supply and Stake tiles always show MIN − n + MAX (all four boxed buttons; clicking the tile adds no other controls). REROLL GOODS is a solid acid-lime button (`RerollButtonStyle`), not a tile. A Contract needs no earlier Black Market purchase. Bought Gear and Tricks fly from their tile into the slot they land in.
- Scouting (Black Market Intel, the Late-Night Diner tip, Lantern) reveals one seeded-random reachable node whose contents are unknown, of any kind. Fights show their enemies only once scouted (`PathNode.scouted`); Headliners are always known. Ghost Lens (`rel_035`, Legendary, `RevealAllMapNodes`) scouts every node ahead, on purchase and on each new District; like all Legendary Gear it is never sold.

## UI architecture (new look)

- Gear and Trick icons are painted item art reprinted as halftone cut-outs (own colours, thin off-white cut line, no shadow) by `Tools/PrintArt/make_item_icons.py` (sources in `Tools/PrintArt/item_sources`, output in `Assets/Art/Items`), assigned as `iconSprite` and drawn through `ItemIconUI`. One icon per Gear family; every tier shares it.
- Art direction (owner, 2026-10-21): modern neo-punk editorial, not retro comic or handmade paper. Near-black stage, off-white type, signal red for attack/damage/danger, acid lime for selection and valid targets. Functional UI is flat rectangles with 1px hairlines; a border appears only for grouping, selection or state. Texture (halftone, grain) lives in artwork and one faint background grain. Buttons differ by weight (`EditorialButton.Role`: solid red, solid off-white, outline, bare text, red outline), not by shape. Scene corrections live in `DealbreakNeoPunkAuthoring`, the last authoring pass.
- Styling: `DealbreakUIRuntime` and `DealbreakUIStyle`. Theme color fields keep legacy names: ink = stage, charcoal = surface, slate = hairline/disabled grey, paper = off-white, brick = signal red, ochre = acid lime. Components: `EditorialFrame`, `EditorialButton`, `EditorialSurface`.
- Many layouts are written by editor authoring scripts in `Assets/Editor/` (for example `DealbreakUIArtCorrectionAuthoring`). When moving an element, change the scene and its authoring script together.
- HUD layout (owner, 2026-10-23): the header carries no District or Headliner name (the route map shows them), and no "Headliner reward added" banner shows (the reward card is seen flying into the draw pile). Each row sits on its own panel with a coloured heading block (`InventoryPanelUI`): Gear slate, Tricks signal red. The hand size ("HAND 6/8") rides at the hand's right end and moves with it. The route map's District title has a second line naming the Headliner and its abilities (click for the rules). Gear and Tricks run as two rows of slots across the header, Balatro style, each with its heading and count on its left (`DealbreakCentralStageAuthoring.InventoryLayout`, menu Dealbreak/UI/Apply Header Inventory Layout). The shared stage (fight, map, Black Market, Incident) spans x .06-.88, y .18-.86 and centres over the hand (`StageLayout`). An active Contract's progress is the only run-progress text, a small line above the draw pile.
- `ContextualTooltipUI` is the single detail panel (click-pinned). Sources are `CombatInspectUI` (cards, enemies, Gear) and `ContextualInfoTrigger` (everything else, including map nodes).
  - Placement: hand cards open above the card; Gear and Trick slots (header rows) open below the slot; everything else opens right, then left, above, below.
  - It draws at sorting order 90: above gameplay, below modal menus (100).
  - Panels that open beside slots must call `ContextualTooltipUI.RegisterObstacle(rect)`.
- Timing and motion:
  - `AttackCardPresentationUI.ImpactDelay(hitIndex)` is when a hit visibly lands; enemy HP drain, shake and the defeat fade wait for it.
  - `DisplayedNumber` eases HP and Credits.
  - Enemy HP is a meter too (bigger, ATTACK beside it). While an enemy has Shield its fill is grey and one shield glyph per charge (`ShieldGlyph`, drawn in code) sits on the bar; an enemy's abilities are named under it.
  - The player's HP is a meter (`HealthBarBacking.Look.Meter`: hairline track, inset red fill, "HP n/max" on it); Block and Shield sit on a small line above it, not on the bar.
  - Credits show as a credit chip and a number just above the HP panel (`CreditsHudUI`, sprite `DealbreakUITheme.creditsIcon`, printed from `Artifacts/24`). A defeated enemy's Credits fly into the coin as its killing hit lands (`EnemyDisplayUI.DefeatHitLanded`); the count waits for the board and the coins (`CreditsHudUI.HoldsCredits`).
  - `BoardPanelTransition` fades panels (`FadeIn` for modal overlays).
  - Mod and Gear triggers (owner, 2026-10-23, after Balatro): gameplay reports each activation to `EffectTriggerFeed` as it executes (an `EffectTriggerTrace` records attack and block values from the committing calculation; previews never report). `EffectTriggerPresenterUI` plays them in order: the card or Gear hops, a tag says what it did, Credits fly into the chip and healing onto the HP bar, and an HP cost (Overclocked) drains the bar and flies into the card. Activations before the hit play first while the played cards stand lifted out of the hand (`AttackCardPresentationUI` lead-in); the board stays busy until combat triggers finish, and Credits and HP count up as their chips land.
- An authored Incident whose every choice is unavailable offers LEAVE (`RunManager.CanLeaveBlockedEvent`/`LeaveBlockedEvent`): nothing gained or lost, so a run never stalls.
- Map: where two routes cross, the overpass breaks and hops over the other in the route colour (`PathScreenUI.DrawHop`). `MapNodePresentation` draws node glyphs as wide tags with the label inside; a lime outer ring marks scouted nodes and a known Enforcer prints in signal red. A scouted node opens its info on the first click and is entered on the second; other nodes are entered on the first click. `RunManager.GetNodeDetail`, `IsScoutedNode` and `NodeDetailsVisible` define what a node reveals.

## Driving Unity (Locus MCP)

- Load the tools with ToolSearch (`+locus unity`). The loop is: edit, then `unity_recompile`, then `unity_test_run` (EditMode: `Dealbreak.EditModeTests`, about 2 min). Baseline (2026-10-23): **EditMode 1815 passed + 1 skipped (1816 cases, the per-Gear matrices and balance drift tests included), PlayMode 10/10 plus the 12-District soak**.
- Tool calls are capped at about 5 minutes.
  - The full EditMode run (and some batches) blocks the main thread long enough that `unity_test_run` aborts with `native_main_thread_unresponsive` while the run goes on. Start it from `unity_execute` instead: `UnityTestApi.Start(new Locus.UnityTestRunOptions { Mode = Locus.UnityTestMode.Edit, Assemblies = new[]{"Dealbreak.EditModeTests"}, ResultDetail = Locus.UnityTestResultDetail.Failures })`, then poll `UnityTestApi.Status(runId)` in later calls. PlayMode and the soak work the same way (`Mode = Play`, `Groups`).
  - Run PlayMode with `groups: ["!TwelveDistricts"]`, then the 12-District soak test separately (80–100 s, often over the cap). Never poll it with `unity_execute` and `request_editor_status: "editing"`: that leaves Play mode and aborts the soak; wait on `TestResults.xml` instead.
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
