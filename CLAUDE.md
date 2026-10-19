# Dealbreak — notes for Claude sessions

@AGENTS.md

AGENTS.md holds the project rules, combat invariants and architecture contracts. This file adds working knowledge from earlier sessions: how to drive Unity here, traps that have already bitten, and the owner's UX decisions.

## Branches — check before you start

- `ui-new-look` is the current working branch. It is the new Dealbreak look (`codex/combat-ui-vertical-slice`) with the architecture-audit fixes, polish, scouting and later UI work merged on top. It is pushed to `origin`.
- `main` still has the **old look**. Never start UI work from `main`. A session once built a day of work on `main` by mistake and had to merge it onto the new look.
- `ui-polish` and `architecture-audit-fixes` are older branches already contained in `ui-new-look`.
- The GitHub repo was renamed to `phamhung1701/Dealbreak`; the old `Cardpg` URL redirects.
- If a screen looks "old" (flat blue panels, TAKE DAMAGE button, ELITE/SHOP node labels), you are on the wrong branch.

## Owner preferences

- Commit once per problem solved, with a clear message ending in `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Push only when asked.
- Report what actually ran (test counts, visual checks) and what could not be run.
- **No in-game guidance text.** A tutorial is planned. Show state and numbers (damage and block previews, Recover cost, enemy intents, choice requirements), never how-to hints. `ActionGuidanceCopyTests` pins this.
- **Avoid bloated UI.** Show each fact once; for example, pending damage appears only on the HP panel ("INCOMING n").
- **Hover info opens next to the hovered object**, never in a fixed panel (see ContextualTooltipUI below).
- Decided mechanics/UX:
  - No Take Damage button; PASS skips the blocking phase.
  - Supply can be bought repeatedly in one Black Market.
  - Gear is sold from the Gear rail inside a Black Market (SELL panel plus confirmation), like Tricks. There is no Sell Items mode.
  - CONTINUE TO MAP sits right of the Black Market menu; BUY is inline on each offer.
  - Scouting (Black Market Intel, the Late-Night Diner tip, Lantern) reveals one seeded-random reachable "?" node of any kind. A scouted node shows its enemies or Incident on hover.

## Driving Unity (Locus MCP)

- Load tools with ToolSearch (`+locus unity`). If the server is down, compile outside Unity: `dotnet build Dealbreak.Runtime.csproj -p:OutDir=<scratch>/` (also `Assembly-CSharp-Editor`, `Dealbreak.EditModeTests`, `Dealbreak.PlayModeTests`). The generated `.csproj` files are git-ignored. This checks compilation only; say so in commits.
- Workflow: edit, then `unity_recompile`, then `unity_test_run` (EditMode assembly `Dealbreak.EditModeTests`, about 40 s). Baseline: **EditMode 691 passed + 1 skipped, PlayMode 10/10**.
- Tool calls cap at about 5 minutes. Run PlayMode as `groups: ["!TwelveDistricts"]`, then run `FinalPortfolioRunPlayModeTests.TwelveDistricts...` separately. That soak takes 80–100 s but often exceeds the call cap. Wait until `Application.isPlaying` is false, then read `C:\Users\Admin\AppData\LocalLow\hwng\Dealbreak\TestResults.xml` (check its timestamp).
- **Never enter Play mode with `Game.unity` open.** GameController continues the owner's real saved run. Open `MainMenu.unity` (if not dirty), enter Play, then call `GameFlowService.Instance.StartDevRun(seed, false, false, false)`. Dev runs never save or delete saves. `StartRun` would offer to replace the real save, so don't confirm it.
- A PlayMode test that fails while a menu has `Time.timeScale = 0` hangs every later test (defeat fades never finish). If a run hangs, check `Time.timeScale`.
- Scene and prefab changes go through the Unity API (`EditorSceneManager`, `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`) or careful, verified YAML edits. Many layouts are written by editor authoring scripts under `Assets/Editor/` (for example `DealbreakUIArtCorrectionAuthoring`). When you move an element, change the scene and the authoring script together.

## Repository traps

- `Packages/com.farlocus.locus`, `Locus/` and `.plastic/` are live-updated by tools. Unity locks `locus_native.dll`, so a `git checkout`/`stash` that touches it fails half-way. To change branches safely:
  1. Back up the dirty files.
  2. Do merges in a `git worktree` outside the project.
  3. Point HEAD at the branch (`git symbolic-ref` + `git reset`).
  4. Check out only the non-Locus paths.
- Line endings are mixed (LF and CRLF per file). With Python, read and write using `newline=''` and convert the replacement text to the file's newline style. The `Edit` tool handles this.
- Python heredocs: a `"\n"` meant for C# becomes a real newline unless you use a raw string. Check `grep -n '"[A-Z]*$'` after scripted edits.
- **Unity fake null:** `GetComponent<T>() ?? AddComponent<T>()` returns a destroyed-looking object in the Editor. Use `TryGetComponent(out x)` or an explicit `== null` check.
- Determinism: new randomness gets its own named `RunRandomContext.CreateStream("name", key)` keyed on stable state, so existing streams are untouched. Previews must not consume RNG.

## UI architecture (new look)

- `DealbreakUIRuntime`, `DealbreakUIStyle` (Theme: ink/charcoal/slate/paper/brick/ochre) and `EditorialFrame/Button/Surface` style everything at runtime.
- `ContextualTooltipUI` is the single hover detail for every source (cards, enemies, Gear, Tricks, the draw pile, offers, map nodes via `ContextualInfoTrigger`).
  - Placement: hand cards open above the card; everything else opens Right, then Left, Above, Below.
  - It draws at sorting order 90: above gameplay, below modal menus (100).
  - Panels that open beside slots must call `ContextualTooltipUI.RegisterObstacle(rect)`.
  - `CombatDetailPanelUI` is legacy and used only when no ContextualTooltipUI exists.
- Presentation timing: `AttackCardPresentationUI.ImpactDelay(hitIndex)` is when a committed hit visibly lands. Enemy HP drain, hit shake and the defeat fade wait for it. `DisplayedNumber` eases HP and Credits. `BoardPanelTransition` handles panel fades (`FadeIn` for modal overlays).
- Map: `MapNodePresentation` draws node glyphs (an ochre edge marks scouted nodes). `RunManager.GetNodeDetail`, `IsScoutedNode` and `NodeDetailsVisible` define what a node reveals.

## Useful history

- `DEALBREAK_ARCHITECTURE_AUDIT.md` contains the October 2026 audit and a resolution table (TD-01..TD-23).
- `DEALBREAK_DEVELOPMENT.md` is the milestone log.
