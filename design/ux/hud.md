# HUD Design: Combat

> **Scope**: the combat screen (header inventory, enemies, hand, HP and Credits, actions). Visual language: `design/art/art-bible.md` (Cut Sheet). Behaviour: `Docs/OWNER_DECISIONS.md`, which wins on any conflict.

> **Status**: Revised 2026-10-27 after `/ux-review` (1 blocking, 8 advisory fixed)
> **Author**: owner + ux-designer
> **Last Updated**: 2026-10-27
> **Platform Targets**: PC 16:9 (1920×1080 and up, Expand scaler); mouse primary, keyboard focus opens info; no gamepad or touch
> **Template**: HUD Design

---

## HUD Philosophy

**Decisions visible, the rest on click.** Everything a turn's decision needs
stays on the board: HP, INCOMING, the hand, enemy HP, attack and abilities,
and the selection preview. Rules text and detail open on click, next to the
clicked object, in the one contextual panel.

- Show state and numbers, never how-to. How-to lives only in the tutorial.
- Each fact appears once, in one place and one colour.
- Every HUD object is a printed paper object on the board (Cut Sheet), so a
  new element must earn an edge and a place on the board, or it belongs in
  click detail.

---

## Information Architecture

### Full Information Inventory

Built from the combat UI code (`Assets/Scripts/UI`), `Docs/OWNER_DECISIONS.md`
and art bible section 7, on 2026-10-27. "Today" means the current code.

- **Player state:** HP current/max with meter; Block; Shield charges;
  Credits (chip and number); kill Credits as they land; character portrait;
  character name and perk; HP loss and heal motion.
- **Enemies:** name; portrait; HP with meter; ATTACK n; Shield charges (glyphs,
  grey fill); ability names; ability rules; intent (CHARGED ATTACK NEXT,
  +1 SHIELD NEXT, 2 HITS); detail extras (called suit, Silence/Withering,
  Oppression, Captain bonus); Headliner reward card; flee countdown (Thief);
  INCOMING n; ALL BLOCKED; target state; Perfect Block cue; hit shake and
  defeat fade.
- **Hand and deck:** hand cards (rank, suit, Mod plate, Mod fx); selected
  state; card detail (ATK/DEF and Mod); HAND n/8; draw pile and count;
  draw-pile detail (Mod count, run timer); sort by rank or suit; draw and
  return flights; decked-out loss. There is no discard pile in play: played
  cards return to the draw pile, and "DISCARD" names only the item strip.
- **Header inventory:** Gear row (heading, n/cap, icons, tier); Gear text;
  Tricks row (heading, n/cap, icons, stack and charges); Trick text;
  drop-target marks and drop preview while an item is held; the DISCARD strip.
- **Actions and status:** ATTACK; BLOCK / CONFIRM BLOCK; PASS; PASS info;
  status line (PLAYER TURN, BONUS ACTION, DEFENSE, ENCOUNTER WON, RUN DEFEAT);
  selection preview with attack or block numbers; auto-Recover notice
  ("Recover: take n, draw 1"); tutorial prompts (tutorial run only).
- **Transient feedback:** hit impact; number tints; coin flights and chip
  stamps; trigger hops and tags; Perfect Block; reward card flight; board tone
  and defense dot screen.
- **Navigation:** MAP; Contract progress line and its detail; pending enemy
  drop with LEAVE IT.
- **Not on this screen:** District and Headliner names, and the "Headliner
  reward added" banner (owner rulings, 2026-10-23); the Recover button (Recover
  runs on its own); dev-only infinite HP and Credits readouts.
- **Not inventoried:** audio cues and the pause and settings entry point (see
  Open Questions).

### Categorization

Approved by the owner on 2026-10-27.

| Category | Information |
|---|---|
| **Must Show** | player HP and meter; Credits; each enemy's name, portrait, HP meter, ATTACK and ability names; hand cards; HAND n/8; draw pile and count; Gear and Trick rows (icons, tier, Trick charges); ATTACK and BLOCK; sort buttons; status line; MAP |
| **Contextual** | Block and Shield line (when above 0); enemy Shield glyphs; intents; INCOMING n and ALL BLOCKED (defense); PASS (player turn and defense); target crop marks (while targeting); selection preview (replaces the status text while cards are selected); drop-target marks and the DISCARD strip (while an item is held); auto-Recover notice; Contract line (with an active Contract); pending enemy drop and LEAVE IT |
| **On Demand (click)** | card detail (ATK/DEF and Mod); enemy ability rules; detail extras; flee countdown; Gear and Trick text; character name and perk; draw-pile detail (Mod count, run timer); PASS info while unavailable; Contract objective and reward |
| **Hidden (motion only)** | hit shake and HP drain; number tints; coin flights; trigger hops and tags; Perfect Block; draw and return flights; reward card flight; board tone and the defense dot screen |

**INCOMING is one total.** It is a single INCOMING n tag under the enemy row,
matching the one aggregate retaliation hit. It is not one tag per enemy.

**Duplicates cut** (each fact shown once):
1. The selection preview has one builder, not two (`ActionButtonsUI` and
   `CombatDetailPanelUI.SelectionSummary` both write it today). It is not
   copied into card detail, and ATTACK keeps a plain label instead of restating
   the preview ("ATTACK ACE PAIR").
2. Card detail has no heading; the card face names it.
3. Enemy detail drops "Shield: n", Double Strike/charged and "Headliner
   reward", because the board shows them.
4. No VALID/INVALID TARGET text; the crop marks say it.
5. HAND n/cap appears only by the hand, not in the draw-pile detail.
6. Trick stack and charges appear only on the slot, not in Trick detail.
7. The Gear name list (`ownedGearLabel`) never shows; the slot icons do.

**Contract line:** a click opens the Contract's objective and reward, which
appear nowhere else in combat. It no longer repeats the line.

---

## Layout Zones

Baseline anchors were read from the authoring scripts
(`DealbreakCentralStageAuthoring`, plus `DealbreakUIOverhaulAuthoring` for
PlayerArea and HandFrame). Before treating them as final, check them against
the saved `Game.unity`, opened read-only (never enter Play mode with it open).
Anchors are screen-normalised, with the origin at bottom-left.

**Scene check (done 2026-10-27).** The anchors were verified against the saved
`Game.unity` by reading its YAML read-only (the Editor did not open it, and
Play mode was not used). Elements that code places at runtime were worked out
from the layout code. Results:
- Matches within ±0.01: Gear and Trick rows, pause, MAP, stage, status band,
  INCOMING, action bar and its buttons, hand frame.
- Corrected above: sort buttons, HP panel, draw pile.
- Not observed on screen: the HP raise, chip, portrait, sort, MAP, DISCARD,
  Contract line and HAND n/8 positions are derived from code. Confirm them
  with a dev-run capture when the UI is built.

**Safe-zone margin:** nothing interactive or textual sits closer than 24px to
any screen edge at 1080p, which is .0125 of the width and .022 of the height.
MAP at the right edge already sits at .9875 (24px). This holds on 16:10 (the same pixel
margin) and inside the 16:9 play area on 21:9. PC has no overscan, so there
is no extra TV-safe inset.

| Zone | Anchors (x, y) | Notes |
|---|---|---|
| Header Gear row | x .20–.54, y .885–.99 | 1080p pixel boxes pinned from the top centre (`CanvasPin`); heading and count on the left |
| Header Trick row | x .55–.94, y .885–.99 | same pinning; up to six slots |
| MAP | x .9085–.9875, y .837–.876 | under the header rows, top right; moved in 1px to the 24px edge margin |
| Pause | top-right corner, 58px | keep clear |
| Stage / enemy row | stage x .06–.88, y .18–.86; enemies at about y .49–.846 | the enemy row is **raised about 20px** from today's .466 to make room for the band below |
| INCOMING n | centred under the enemy row, about y .43–.47 | **directly under the enemies**, defense only; one total |
| Status line | centred, x .18–.76, about y .37–.42 | **below INCOMING**; the selection preview replaces its text |
| Pending drop + LEAVE IT | top of the stage, centred, about y .74–.82 | **moved from the centre**: shortest drag to the Trick slots; the run waits on it. It overlaps the enemy row's band on paper, but no enemy stands then: the drop is granted only on victory (`RunManager` victory path, `TryGrantConsumableDropForNode`), after every enemy is defeated or fled. Don't move it to avoid the enemy row |
| Hand | x .17–.77, y .133–.351 | HAND n/8 follows the last card |
| Action bar (ATTACK, BLOCK, PASS) | x .25–.69, y .016–.086 | **below the hand, centred on it** (centre x .47). Today's bar spans .372–.832 (centre about .60), so it moves left and narrows by about 4%, keeping about 29px clear of the HP panel (to .235); the sort buttons ride inside its left end |
| Sort buttons | inside the action bar's left 18% (rank above suit) | placed at runtime by `DealbreakUIRuntime.PositionSortControls`, relative to the bar, so they move with it (about x .254–.325 once the bar is re-centred). They are 29px tall: below the 44px rule, which covers only ATTACK, BLOCK and PASS (advisory) |
| HP panel (with the Block/Shield line above it) | x .025–.235, y .014–.082 | left of the action bar. **Moves down**: today it is raised 114px (scene offset, capped by `CombatDetailPanelUI`) to y ≈.125–.18, which put the left column over the hand frame (owner choice, 2026-10-27) |
| Credits chip | x .025–.134, about y .09–.13 | stacks above HP (`CreditsHudUI`, 210×46px, 10px gap) |
| Portrait | x .025–.145, about y .14–.26 | stacks above the chip (`PlayerAvatarUI`); ends at x .145, clear of the hand frame (from .17) |
| Draw pile | x .871–.98, y .187–.50 | the scene's `DeckDetailsButton` box; its left 17px sits inside the stage's right edge, clear of the enemy container (which ends at x .871) |
| Contract line | x .78–.98, about y .51–.53 | right-aligned above the pile, clear of the DISCARD strip |
| DISCARD strip | x .895–.985, y .55–.83 | only while an item is held; **its top stops at .83** so it clears MAP |

The right-edge column reads top to bottom: MAP, DISCARD strip (while an item
is held), Contract line, draw pile.

**Collisions fixed here**
- The DISCARD strip overlapped MAP; its top now stops at .83.
- The DISCARD strip overlapped the Contract line; the strip starts at .55.
- The band between the enemies and the hand had about 125px for both the
  status line and INCOMING; raising the enemy row about 20px gives both room.
- The left column (HP, chip, portrait) overlapped the hand frame because HP
  was raised 114px; moving HP down to the action-bar level clears it.

```
 x:  0  .06      .17           .47            .77   .88  .905   1.0
 .99 +---------------------------------------------------------------+
     | wordmark   [GEAR n/5 |s|s|s|s|s|]  [TRICKS n/6 |s|s|s|s|s|s|] || |
 .876|                                                     [  MAP  ]    |
 .83 |  +------------- STAGE x .06-.88 ------------+       +-DISCARD-+  |
 .82 |  |        [drop token]  [LEAVE IT]           |       | (item   |  |
 .74 |  |        (only when the backpack is full)   |       |  held)  |  |
     |  |   [ENEMY]        [ENEMY]        [ENEMY]   |       |         |  |
     |  |   name, portrait, HP meter, ATTACK tag,   |       |         |  |
 .49 |  |   abilities, intent                       |       +---------+  |
 .47 |  +-------------------------------------------+ .55               |
 .43 |              [ INCOMING n ]  (defense only)        Contract line  |
 .42 |         [ STATUS LINE / selection preview ]       +-------+      |
 .37 |                                                    | DRAW  |      |
 .351|   portrait   -- hand frame x .17-.77 ------------  | PILE  |      |
     |   [sticker]   fanned hand ............. HAND n/8   |   n   |      |
 .13 |   [CREDITS]  -- hand frame bottom --               +-------+ .285 |
 .086|   [HP panel ]  [rank|suit][ ATTACK ][ BLOCK ][ PASS ]               |
 .014+---------------------------------------------------------------+
```

**Layout choices made by the owner (2026-10-27)**
- The action buttons stay below the hand, centred.
- INCOMING sits directly under the enemy row, with the status line below it.
- The pending enemy drop and LEAVE IT move to the top of the stage, under the
  header.

---

## HUD Element Specifications

These are the Cut Sheet terms from art bible sections 3 and 7.
- Shadows: panel 6px, button 4px, slot, chip and tag 3px, meter 0px; no
  shadow means not interactive.
- Fonts: Anton for display, Barlow Condensed (bold, uppercase, +40) for
  labels, Zilla Slab 20 for body, Courier Prime Bold for numbers.
- Floors: text at least 18px, 20px on kraft.
- Press for every button: 60ms down-right 4px with the shadow to 0, then a
  90ms ease-out-back release.
- Reduce Motion: shake off; hops and slaps settle instantly; tag order and
  `ImpactDelay` are kept.

| Element | Cat | Content | Visual form | Update | Trigger | Animation |
|---|---|---|---|---|---|---|
| Gear row | MS | "GEAR n/cap", slots, icon, tier | Cream heading strip (Barlow); stickers at least 56px with one cut line, 3px shadow, tilt at most ±1°; empty slots are flat dashed outlines | event (gain, merge, move) | — | slap on a new item; 220ms hop on trigger |
| Trick row | MS | "TRICKS n/cap", up to 6 slots, icon, stack ×n, charges | Kraft heading strip with a 2px ink border; the same stickers; charges on an untilted parent | event | — | as Gear; spending a charge shows a tag |
| MAP | MS | "MAP" / "CLOSE MAP", map glyph | cream secondary button | static | — | press |
| Player HP meter | MS | "HP n/max", fill, ticks every 5 HP | strip with a 2px ink border, cream track, red fill, ink ticks; Courier Prime Bold 20 in ink with a 2px cream outline (20, not the 28 numeric tier, because the text sits on the meter) | eased (`DisplayedNumber`), held while trigger heals land | — | drains on `ImpactDelay`; heals count up as chips land |
| Block / Shield line | C | shield glyph and n; "BLOCK n" | cream text and glyph on the board, no shadow, no semantic colour | event | Block or Shield above 0 | tag pop on change |
| Credits chip | MS | coin and number | round notched mustard stamp, ink Courier 28, 3px shadow | eased; waits for coins (`CreditsHudUI.HoldsCredits`) | — | 120ms stamp per landing; coin arcs use the code's timing (0.42s each, 60ms stagger) until Open Question 9 settles it |
| Enemy | MS | name, portrait, HP n/max, ATTACK n, ability names | portrait sticker; larger HP meter (ink fill on cream track, 0px); ATTACK as a small red tag with a glyph; abilities in Barlow 18 cream | HP eased, the rest event | — | 120ms 6px shake after `ImpactDelay`; defeat fade; Credits fly at the killing hit |
| Enemy Shield | C | one glyph per charge (×n after 4) | hatch-grey fill (`#8C8270` plus hatch) with cream shield glyphs on the bar | event | Shield above 0 | fill crossfade; glyph pop on gain |
| Intent | C | CHARGED ATTACK NEXT, +1 SHIELD NEXT, 2 HITS | Barlow 18 cream under the abilities, flat | event | not an ordinary attack | — |
| Target marks | C | valid or targeted | thin teal crop marks (valid); thick marks and a teal strip under the HP meter (targeted); 2px ink outline; outside the portrait; no text | event | targeting, only while two or more enemies stand | snap from thin to thick |
| INCOMING n | C | "INCOMING" and n (one total); ALL BLOCKED | red notched tag, cream Barlow 22 and Anton 32, cream outer edge, 3px shadow; 4px halftone offset on the body only (doubled in defense); numerals solid | event | defense only | pop on change; counts to 0 and goes flat when fully blocked; tilt ±3° settling to ±1.5° |
| Status line | MS | PLAYER TURN, BONUS ACTION, DEFENSE, ENCOUNTER WON, RUN DEFEAT | cream notched tag, Barlow 22 in ink, 3px shadow, no tilt (owner chose a tag over flat text) | event | text replaced by the selection preview | 250ms board-tone crossfade between states |
| Selection preview | C | "n selected • Attack power n", "Blocks b of n • r damage left", "Can't block with this selection", "Royal Flush m/5 • n" | inside the status tag; numbers in Courier; one builder | event | cards selected | fade swap (instant with Reduce Motion) |
| ATTACK | MS | "ATTACK" (plain; "Cancel" while targeting) | the only red-fill button, Anton 32, 4px shadow; unavailable shows hatch and no shadow | event | player turn with a selection | press |
| BLOCK / CONFIRM BLOCK | MS | label | cream button, Anton 32 | event | defense with a legal selection | press |
| PASS | C | "PASS" | cream secondary, Anton 32, never red; unavailable shows hatch and no shadow | event | player turn and defense | press; a click while unavailable opens its info |
| Sort buttons | MS | rank, suit | small cream buttons, Barlow 22 | event (usable when the hand holds 2 or more) | — | press |
| Auto-Recover notice | C | "Recover: take n, draw 1" | text in the status tag | event | empty hand on the player turn | — |
| Hand cards | MS | rank, suit, Mod plate, Mod fx | rounded cards, 8–12px shadow, fan tilt; every rank and suit index visible with 8 cards | event | — | selection lifts at least 24px with crop marks; dragging a card reorders the hand (visual only, like the sort buttons); draw and return flights; Signature wire race |
| HAND n/8 | MS | "HAND n/8" | cream Barlow on the board, no shadow, never tilts | event | — | follows the last card |
| Draw pile | MS | stack of card backs, count | shadows 2/4/6px, Courier count | event | — | cards fly in and out |
| Contract line | C | "Contract • Encounters n/2 • due before District x" | cream text, 20px, no shadow | event | active Contract | a click opens its objective and reward |
| Drop-target marks | C | marks on legal targets | thin crop marks; the hovered target gets thick marks and a lift; illegal targets get none | event | an item is held | snap from thin to thick |
| DISCARD strip | C | "DISCARD" | red strip (keeps red), Anton 32 | event | an item is held | slides in from the right edge |
| Pending drop + LEAVE IT | C | drop token, "LEAVE IT" | item sticker (150px) with a cream cut line; LEAVE IT as a cream button | event | backpack full with a drop waiting | slaps on arrival; the run waits |
| Contextual tooltip | C | every On Demand item, one at a time | cream panel, 3px ink border, 6px shadow, 0°, notch toward the source, max 360px, 16px padding, Zilla 20, no heading when the source names itself | click or keyboard focus; pinned until a second click or a click elsewhere | — | opens in 110ms (slides 6px, shadow 0→6px); closes in 70ms; sorting order 90 |

The portrait sticker is decoration, not information. Clicking it opens the
character's name and perk (On Demand).

### Data Sources

The UI owns no game state. Every element reads one runtime source, and
previews never change state or consume RNG.

| Element | Source (read-only) | Owner |
|---|---|---|
| Player HP meter | `CombatManager.player.CurrentHealth`, `.maxHealth` | `PlayerRuntime` |
| Block / Shield line | `PlayerRuntime.EncounterBlock`, `.ShieldCharges` | `PlayerRuntime` |
| Credits chip | `CardManager.credits`; kill Credits as they land: `CombatManager.SecuredCredits` | `CardManager`, `CombatManager` |
| Enemy | `EnemyRuntime.DisplayName`, `.currentHp`, `.maxHp`, `.currentAttack`, `.AbilitySummary` | `EnemyRuntime` |
| Enemy Shield | `EnemyRuntime.ShieldCharges` | `EnemyRuntime` |
| Intent | `EnemyDisplayUI.CompactIntent(enemy, CombatManager.currentState)`, a formatter over `EnemyRuntime` state | `EnemyRuntime` |
| Target marks | `CombatManager.IsEnemyTargeting`, `GetValidAttackTargets` | `CombatManager` |
| INCOMING n | `CombatManager.IncomingDamageAfterModifiers` (the aggregate pending hit) | `CombatManager` |
| Status line | `CombatManager.currentState`, `IsBonusPlayerAction` | `CombatManager` |
| Selection preview | one builder (below), over `CardManager.SelectedCards`, the shared damage calculation and `CombatManager.TryPreviewDefense` | `CardManager`, `CombatManager` |
| ATTACK / BLOCK / PASS | `CombatManager.currentState`, `GetValidAttackTargets`, `CanPassPlayerTurn`; commands go through `CombatCommands` | `CombatManager` |
| Hand cards, HAND n/8 | `CardManager.hand`, `.HandCapacity` | `CardManager` |
| Draw pile | `CardManager.deck.Count` | `CardManager` |
| Gear row | `CardManager.ownedGear`, `.GearCapacity` | `CardManager` |
| Trick row | `CardManager.Backpack`, `.BackpackCapacity`, `.ConsumablesPerSlot` | `CardManager` |
| Contract line | `RunManager.ActiveContract` (`encounterWins`, `deadlineMapExclusive`), formatted by `RunStatusUI.FormatContractProgress` | `RunManager` |
| Contract detail (click) | `RunContractState.ObjectiveDescription`; reward text from `Resources/QuestContract` and `GameBalance.Current` contracts. There is no runtime accessor for the reward yet, so add a read-only one. | `RunManager` |
| Drop-target marks, DISCARD | `ItemDropRules` (the single drop path) | `ItemDropRules` |
| Pending drop + LEAVE IT | `RunManager.PendingEnemyDrop`, `CanClaimPendingEnemyDrop`; actions `ClaimPendingEnemyDrop` and `LeavePendingEnemyDrop` | `RunManager` |
| Feedback | `EffectTriggerFeed` (gameplay reports as it executes) | gameplay |

**The selection preview has one builder and one writer** (owner, 2026-10-27).
- The builder is `CombatDetailPanelUI.SelectionSummary`. It takes over the
  attack-pattern forms (Ace pair, same rank ×n, Royal Flush m/5, instant
  defeat) and the defense forms ("Blocks b of n • r damage left",
  "Can't block with this selection") that `ActionButtonsUI` builds today.
- The writer is `ActionButtonsUI`, which calls that builder. Nothing else
  writes the line, and `DealbreakUIRuntime` stops writing the ATTACK label.
- Every attack number comes from the `PreviewAction*` calculation that
  execution uses, not a per-card sum.

---

## HUD States by Gameplay Context

| State | Trigger | What changes |
|---|---|---|
| Player turn | `GameState.PlayerTurn` | Status tag reads PLAYER TURN (or "• BONUS ACTION"). ATTACK is shown but disabled until a selection exists. PASS is shown (`CanPassPlayerTurn`). BLOCK is disabled. INCOMING is hidden. Intents show their "…NEXT" forms. |
| Cards selected | selection count above 0 | Selected cards lift at least 24px with crop marks and a deeper shadow. The status tag shows the selection preview (one builder). ATTACK becomes usable if any target is valid. Clicking a selected card pins its detail above it. |
| Targeting | `IsEnemyTargeting` | ATTACK reads "Cancel". Valid enemies get thin crop marks; the hovered or targeted one gets thick marks and the teal strip. Marks show only while two or more enemies stand. PASS and BLOCK are disabled. |
| Defense | `GameState.EnemyAttacking` | Board tone crossfades over 250ms and the dot screen fades in over at least 300ms, never pulsing. The INCOMING n tag appears under the enemy row with a doubled body offset. Status tag reads DEFENSE, or shows the block preview. BLOCK reads CONFIRM BLOCK. PASS is usable. ATTACK is disabled. When everything is blocked, INCOMING counts to 0 and goes flat. |
| Empty hand / auto-Recover | player turn with an empty hand | Status tag reads "Recover: take n, draw 1". 0.8s after the board settles, Recover runs: an HP drain, then a draw flight. Sort buttons are disabled. |
| Trigger playback (board busy) | `EffectTriggerPresenterUI` queue not empty | Commands wait. Played cards stay lifted during the lead-in. Credits and HP hold until their chips land. Buttons keep their look, with no hatch, because hatch means unavailable. |
| Item held | `ItemDragUI` drag starts | An open tooltip closes. The DISCARD strip slides in. Legal targets get thin marks; the hovered one gets thick marks and a lift. A drop preview names the result. |
| Pending drop waiting | `RunManager.PendingEnemyDrop` set | The drop token and LEAVE IT slap in at the top of the stage. The run waits until the drop is dragged into a Trick slot or left. |
| Encounter won | `GameState.GameWon` | Status tag reads ENCOUNTER WON. PASS is hidden. ATTACK and BLOCK are disabled. A Headliner reward card flies into the draw pile (slap and 2px jolt). Reward gates block input. |
| Run defeat | `GameState.GameOver` | Status tag reads RUN DEFEAT. Input is gated and `RunResultUI` takes over. Interactive elements keep their shadows. |
| Paused | the pause button or Escape | `Time.timeScale = 0` and input is gated. Trigger playback freezes and resumes in order. The modal draws at sorting order 100, above the tooltip (90). |
| MAP view | MAP | The route map shows on the stage and the button reads CLOSE MAP. Input gating is to be traced (Open Questions). |
| Deck view | click the draw pile | `DeckViewerUI`; see Open Questions (it is a fixed panel). |
| Tutorial run | the first NEW RUN, until `TutorialProgress` is set | The same HUD plus lesson prompts, the only how-to copy. Controls `TutorialGate` refuses show as unavailable. |
| Exploration | — | The route map is a separate screen; out of scope here. |
| Dialogue / cutscene | — | Incidents are a separate screen. There are no cutscenes. |

**Empty variants.** The layout never shifts.
- No Gear: "GEAR 0/5" and dashed outlines.
- No Tricks: "TRICKS 0/n" and dashed outlines, for `BackpackCapacity` slots only.
- No Contract: no line, and the pile doesn't move.
- No Block or Shield: the line is empty, and the HP meter doesn't move.
- No enemy Shield: plain fill.
- One enemy left: no target marks, and ATTACK targets it directly.
- Empty draw pile: count 0 and an empty outline.
- An enemy with no abilities or intents: those lines collapse, and its click opens nothing.

---

## Information Hierarchy

| Tier | Elements | When hidden, carried by |
|---|---|---|
| **MUST KEEP** | hand cards; enemies (name, portrait, HP, ATTACK, abilities); INCOMING (defense); player HP; ATTACK and BLOCK; status tag and selection preview; PASS (in its phases); pending drop and LEAVE IT (while waiting); target marks (targeting); drop-target marks and the DISCARD strip (drag) | never hidden |
| **SHOULD KEEP** | draw pile and count; HAND n/8; Trick row; Gear row; enemy Shield glyphs; intent line; Block/Shield line; Credits chip | deck view or tooltip; the visible cards; slot detail; trigger tags; grey hatched fill; enemy detail; INCOMING after modifiers; coin flights |
| **CAN HIDE** | MAP; sort buttons; Contract line; auto-Recover notice; portrait | the route map between fights; reading the fan; Contract detail; the HP drain and draw flight; the perk on click |
| **ALWAYS HIDE until clicked** | the contextual tooltip | — |

---

## Visual Budget

The busiest state is defense with an item held, three enemies, a full header
(5 Gear, 6 Tricks), eight cards, an active Contract and Shield showing. The
tooltip is closed, because pressing to drag closes it.

- **At most 26 HUD element groups at once.** Transient tags come on top of
  these. The busiest state reaches exactly 26, so there is **no headroom**: a
  new always-on element must replace one, or drop one per the order below.
- The figures below are estimates from the section C anchors. Measure them in
  the scene during implementation (acceptance criterion 6). If the measured
  chrome share exceeds 30%, apply the drop order.
- **Screen share at 1920×1080:** chrome about 29%, game content (hand and
  portraits) about 22%, about 51% in total.
- **Caps:** chrome at most 30%; chrome plus content at most 55%; red, teal
  and mustard fills together under 10% (about 6% today, the DISCARD strip being
  the largest).
- **Drop order** if a future element breaks a cap: Contract line (hidden while
  an item is held), portrait, sort buttons (collapse to one toggle), intent
  line (moves into enemy detail), MAP (hidden in defense). MUST KEEP is never
  dropped.

---

## Feedback & Notification Systems

**One ordered timeline.** Gameplay reports each activation to
`EffectTriggerFeed` as it executes. `EffectTriggerPresenterUI` plays them in
FIFO order. Hit visuals wait for `AttackCardPresentationUI.ImpactDelay(hitIndex)`,
which is authoritative. Previews never report.

| Type | Triggered by | Position | Duration | Max at once | Priority | Queue |
|---|---|---|---|---|---|---|
| Trigger tag and sticker hop | `EffectTriggerFeed` | on the source card or Gear slot; tags stack per anchor | step 0.3s (HP cost 0.5s); tag 0.8s; hop 220ms | one step starting at a time; about 3 tags visible | ordered, after the pre-hit lead-in | FIFO, never dropped; a backlog speeds steps up by 15% per queued item, to at most 35% faster |
| Credits flight | the killing hit, or a Credits trigger | source to the chip | 0.42s per coin with a 60ms stagger in code (art bible: 350–450ms, 40ms; see Open Questions); chip stamp 120ms | every coin of one award | at the killing hit's `ImpactDelay` | the count holds until coins land |
| Heal / HP cost chip | Heal or `HealthCost` cue | source to the HP bar, or the bar to the card | within the step | one per step | ordered | HP holds until the chip lands |
| Hit shake and HP drain | a combat hit | the target | 120ms, 6px; drain eased | one per hit | `ImpactDelay(hitIndex)` | multi-hit plays in hit order; the defeat fade follows the last hit |
| Perfect Block | `OnDefenseAttackResolved` with `IsPerfectBlock` | over the attacker; rises 20px and fades | about 600ms | one per enemy; repeats in the same frame merge into "×n" | after the block resolves | not queued |
| Reward flight | Headliner reward | to the draw pile | 120ms fall and 80ms squash; 2px jolt | 1 | after the final hit and kill draw, before run completion | — |
| Auto-Recover notice | empty hand on the player turn | the status tag | 0.8s, then Recover runs | 1 | lowest | waits for a busy board to settle |
| Drop slap | `PendingEnemyDrop` set | top of the stage | 120ms plus 80ms | 1 | after the kill's Credits flights | stays until resolved |

---

## Platform Adaptation

PC only: mouse primary, keyboard focus opens info, no gamepad and no touch.
The CanvasScaler is Expand with a 1920×1080 reference.

| Display | Result |
|---|---|
| 1920×1080 | Baseline; section C anchors as given. |
| 2560×1440, 3840×2160 | Identical layout scaled up; every floor holds. Halftone is set in screen pixels, so no moiré. |
| 16:10 (1920×1200, 2560×1600) | The canvas is 120 reference px taller. The header stays pinned (`CanvasPin`), and the stage and the INCOMING/status band gain height. Check that MAP's -134px offset still clears the header. |
| Ultrawide 21:9 | **The play area is capped at 16:9 width and centred (owner choice, 2026-10-27).** The board extends to the sides and every zone keeps its 16:9 position, so mouse travel doesn't grow. |
| Below 1920×1080 | 1280×720 renders the 18px floor at 12 physical px. See Open Questions for the minimum supported resolution. |

There is no runtime HUD scale: it would break the `CanvasPin` pixel boxes and
the stage anchors.

---

## Accessibility

**Tier:** Basic with elevated features, committed 2026-10-27 in
`design/accessibility-requirements.md`. WCAG 2.2 AA applies to text contrast
and non-colour cues. Reduce Motion, the shake toggle, playback speed and
tooltip text size are elevated. This screen meets the tier as follows.

- **Keyboard:** focus opens info. Tab order: Gear slots left to right, Trick
  slots, MAP, enemies left to right, hand cards, ATTACK, BLOCK, PASS, sort rank,
  sort suit, draw pile, Contract line, HP panel, Credits chip, portrait. While
  a drop is waiting, the drop token and LEAVE IT come first. Selecting cards,
  targeting and using items stay mouse-only (owner choice, 2026-10-27). This
  is a known limitation: the screen is not playable by keyboard alone.
- **Contrast and size** (art bible section 4):
  - Ink on cream is 15:1, cream on board 13:1, cream on red 4.6:1.
  - Credits are a mustard stamp with ink type.
  - Banned: red text on the board below 24px bold; teal text on cream; red
    text on kraft.
  - Semantic elements on kraft carry a 2px ink border.
  - Text is at least 18px, 20px on kraft. Ability names sit at 18px, the
    floor, so don't shrink them.
- **Non-colour cues:**

  | Colour | Meaning | Backup |
  |---|---|---|
  | red | damage, danger | notched tag; the words INCOMING, DISCARD, ATTACK; halftone offset; shake; numeric HP |
  | teal | valid target, targeted | thin or thick crop marks outside the portrait; the strip; a hop when targeting starts |
  | teal | selection | a lift of at least 24px; the lift alone reads |
  | mustard | Credits | round stamp, coin glyph, flight to the chip |
  | grey hatch | enemy Shield | one glyph per charge |
  | hatch, no shadow | disabled | never colour alone |

  INCOMING keeps a cream outer edge for protanopia.
- **Reduce Motion** (owner ruling 2026-10-27, not built yet; no setting
  exists): shake off; hops and slaps settle instantly; tilt fixed at rest; the
  board jolt off; the board-tone crossfade and dot-screen fade become instant
  cuts. Trigger tags still appear in order, and `ImpactDelay` is kept.
- **Flashing:** none. The dot screen never pulses, and there is no full-screen
  shake.
- **Screen reader:** out of scope. UGUI exposes no desktop accessibility
  tree. This is a known limitation.

---

## Tuning Knobs

Player settings, all chosen by the owner on 2026-10-27, none built yet. They
are persisted with the `display.*` settings.

| Setting | Range | Default | Cost |
|---|---|---|---|
| Reduce Motion | on / off | off | low–medium: one flag read by the presenter, shake, slap, tilt and `BoardPanelTransition` |
| Screen shake | on / off | on | low; also forced off by Reduce Motion |
| Trigger playback speed | 1× / 1.5× / 2× | 1× | low: scales step, tag and chip timings, keeping order and `ImpactDelay` ratios |
| Tooltip text size | 100 / 115 / 130% | 100% | low–medium: one container with a scroll; its max width grows with it |

Not offered: a global HUD scale and a global text size (they break the pinned
boxes and overflow the header, the status tag and the cards); colourblind
palettes (shape cues already carry every meaning).

Designer constants (in code today):
- `AutoRecoverDelay` 0.8s
- `StepTime` 0.3s; `HealthCostStepTime` 0.5s
- `MinStepScale` 0.35, with a 15% speed-up per queued item
- `TagTime` 0.8s
- `ChipTime` 0.42s; `ChipStagger` 0.06s
- tooltip max width 360px
- selection lift at least 24px

---

## Acceptance Criteria

- [ ] At 1920×1080 every zone sits within ±0.01 of its section C anchors
      (after the scene check), no two zones overlap (the pending drop over the
      empty enemy row excepted), nothing interactive or textual is within 24px
      of a screen edge, the action bar is centred under the hand, and the
      DISCARD strip clears MAP and the Contract line.
- [ ] At 2560×1440 the layout matches 1920×1080 proportionally and no text is
      below 18 reference px. On 21:9 the play area stays 16:9 and centred.
- [ ] With three enemies and two retaliating, defense shows exactly one
      INCOMING n under the enemy row. It equals the aggregate pending damage
      after modifiers, and on a full block it counts to 0 and goes flat.
- [ ] Each state in section E shows only its listed elements:
  - INCOMING only in defense;
  - PASS only on the player turn and in defense;
  - DISCARD and drop marks only while an item is held;
  - the Contract line only with a Contract;
  - the Block/Shield line only when either is above 0;
  - target marks only while two or more enemies stand.
- [ ] Each fact appears once:
  - the selection preview only in the status tag (not in card detail, not in
    the ATTACK label);
  - no heading on card detail;
  - no Shield, Double Strike or Headliner reward in enemy detail;
  - no VALID/INVALID TARGET text;
  - HAND n/cap only by the hand;
  - Trick charges only on the slot.
- [ ] In the busiest state there are at most 26 element groups and at most
      30% chrome, and every card's rank and suit index is visible.
- [ ] A card that fires three Gear triggers and a Mod trigger plays them one
      after another in execution order, one hop and one tag each. Enemy HP
      drains only at `ImpactDelay`. Credits and HP count up as their chips
      land. No command is accepted until playback ends.
- [ ] With Reduce Motion on, there is no shake, hop, slap, tilt or jolt, tags
      still appear in the same order, and hit timing matches Reduce Motion off.
- [ ] Every semantic colour keeps a non-colour cue under a protanopia filter.
      Tab reaches every interactive HUD element in section J order, and focus
      opens its info next to it.
- [ ] With the backpack full, an enemy drop appears at the top of the stage,
      and the route can't advance until it is dragged into a Trick slot or
      left.
- [ ] While paused, trigger playback freezes and then resumes in order, and
      the pause modal draws above an open tooltip.

---

## Open Questions

1. There is no player journey map. Author one from
   `.claude/docs/templates/player-journey.md` to give this screen upstream
   context.
2. Resolved 2026-10-27: the tier is committed in
   `design/accessibility-requirements.md` (Basic with elevated features).
   Keyboard-only play is a known limitation.
3. Audio cues weren't inventoried; section H has no sound column.
4. The pause button sits at the top-right corner by MAP and the sixth Trick
   slot. Confirm its place and its focus order.
5. Verify the section C anchors against `Game.unity`, opened read-only and
   never in Play mode. The authoring scripts disagree across generations.
6. Reduce Motion, the shake toggle, playback speed and tooltip text size are
   ruled but need building.
7. Resolved 2026-10-27: a draw-pile click opens the tooltip detail (draw
   count, Mod count) next to the pile. The full deck list leaves the combat
   click and lives in the deck menu screen.
8. MAP during combat: is combat input gated, and are the enemies and INCOMING
   covered? This isn't traced yet.
9. Credits flight timing: code uses 0.42s with a 60ms stagger; the art bible
   says 350–450ms with 40ms. Pick one.
10. Minimum supported resolution: 1280×720 breaks the 18px floor. Raise the
    minimum to 1600×900, or accept the gap.
11. While the board is busy, buttons show nothing (proposed); confirm.
12. Confirm that controls `TutorialGate` refuses show hatch, with no how-to
    text outside lessons.
