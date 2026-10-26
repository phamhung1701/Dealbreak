# Art Bible: Dealbreak (UI)

> **Status**: Draft
> **Owned By**: art-director
> **Last Updated**: 2026-10-27
> **Art Director Sign-Off (AD-ART-BIBLE)**: SKIPPED 2026-10-27 — solo mode
> **Scope**: UI chrome only. Sections 1–4 and 7 authored; 5, 6, 8 and 9 were left out by the owner's choice of scope.

`/art-bible` writes these nine sections in this order and replaces each
section's `[To be designed]` line as that section is approved. A section still
holding its placeholder is incomplete; a later run fills only those.

## 1. Visual Identity Statement

**Direction: Cut Sheet (paper collage).** Scope is UI chrome only; card faces,
item icons and portraits keep their halftone-print art.

**Rule:** Everything on screen is a physical printed object: an edge, a shadow,
ink on stock. If it has no edge, it isn't UI.

- **Cards are tactical resources (pillar 1).** Cards and UI share one material,
  so a card never reads as a foreign object on a panel.
  *Test:* when a card and a panel compete, the card gets the thicker edge and
  the higher drop shadow.
- **Readable risk (pillar 3).** Danger gets the only red ink: a misregistered
  red halftone offset, the same device the card pips use.
  *Test:* when two things both look important, red means "this hurts you" and
  nothing else may be red.
- **Builds change rules (pillar 2).** Gear, Trick and Mod slots are die-cut
  stickers with the item art's off-white cut line, so a trigger can visibly
  peel and slap.
  *Test:* when a rule-changer fires, move the sticker (hop and tilt); never
  make it glow.

Source material: the cards are cream paper with grain, black condensed slab
numerals, a thick rounded black border and a red halftone misregistration
shadow. Portraits and items are comic ink with halftone shading, flat teal,
mustard and skin fills, and a thin off-white cut-line sticker edge.

## 2. Mood & Atmosphere

In this 2D UI, "lighting" means the board's tone, its warm or cool cast,
contrast and shadow strength. The material stays the same in every state:
dark board, paper stock, ink, hard offset shadows. Only the lighting and the
energy change.

| State | Target | Board / lighting | Descriptors | Energy | Carrier |
|---|---|---|---|---|---|
| Combat: player turn | poised control | `#1C1814` neutral-warm, medium-high contrast, full shadows (cards 8px) | tactile, deliberate, ready, warm | medium | Hand sits highest; a selected card lifts to 12px; teal crop marks on valid enemies |
| Combat: defense | tension, bracing | `#15110E`, highest contrast, full shadows; a red halftone dot screen fades in along the top edge behind the enemies | compressed, alarmed, urgent | high but still (no idle motion) | INCOMING n tag doubles its red misregistration offset; anything that can't block drops to 70% value |
| Route map | calm planning | `#221C16` warm, low contrast, soft 4px shadows | measured, cartographic, quiet | low | Route printed on one large kraft sheet pinned to the board; nodes are ink stamps; the Headliner is a sticker at the top |
| Black Market | temptation | `#241B10` amber (single-lamp), medium contrast | shady, tempting, crowded but ordered | medium | Wares are stickers pinned at a slight tilt on a kraft price board; prices are mustard stamps |
| Incident | uncertainty | `#1A1917` neutral, high contrast at the centre with the rest dimmed, one 6px shadow | suspenseful, sparse, documentary | low–medium | One cream "clipping" sheet with mono text; choices are taped strips, each cost shown once in red (HP) or mustard (Credits) |
| Home and menus | identity, invitation | `#1C1814`, strongest grain, medium contrast | collected, poster-like, confident | medium-low | NEW RUN is the largest stamped button; character pick shows big pinned portrait stickers; deck view lays cards out in a grid; Settings is a plain untilted cream sheet |
| Rewards / victory | payoff | `#261D10` mustard cast, full shadows, the most saturation | punchy, loud, celebratory | highest | Reward stickers slap down with tilt and overshoot; the face card flips in; Credits fly to the chip |
| Defeat / run end | loss, reflection | `#1A1A19` neutral grey-brown, low contrast, shadows flatten to 2px | quiet, flattened, spent | lowest | Run summary is a cream receipt strip with mono figures, each stat once; no dot screens |

## 3. Shape Language

UI only: character and environment silhouettes belong to the fixed art.

**Shadows** are always ink, hard (no blur), and offset down-right at 45°. Their
depth encodes importance and pressability: 12 / 8 / 6 / 4 / 3 / 0 px at 1080p.
No shadow means flat and not interactive. Border weights are baked into the
sprites at 1080p.

| Element | Edge / corner | Border | Shadow |
|---|---|---|---|
| Panel (tooltip, sheets, dialogs) | square, 0 radius | 3px ink | 6px |
| Button | square, 0 radius | 3px ink | 4px; pressed = 0px and the button moves 4px down-right |
| Slot (Gear/Trick header) | sticker with a 2px cream cut line around the item, ~6px rounded | cut line only | 3px |
| Chip (Credits, counters) | round stamp | 2px ink | 3px |
| Meter (player HP, enemy HP) | square strip, segmented by ink ticks | 2px ink | 0px (read, not pressed) |
| Tag (INCOMING n, status, triggers) | ticket stub with notched ends | 2px ink | 3px |

- UI corners are square on purpose: cards are rounded, so chrome never reads
  as a card.
- The tooltip is a cream panel next to the clicked object, with a notch
  pointing at it. It never tilts.
- INCOMING n is a red notched tag with cream type: one total under the enemy
  row, matching the single aggregate retaliation hit.

**Tilt** is a visual-only child transform; UGUI layout stays axis-aligned.
Angles come from a fixed per-slot table, never from `Random` or instance IDs.
- Allowed: slot stickers ±2° at rest; tags spawn at ±3° and settle to ±1.5°;
  reward and victory stickers up to ±4° during the slap; the fanned hand.
- Never: panels, the tooltip, buttons, meters, number fields, multi-line text.

**Hierarchy**
- Hero: the hand (8–12px shadow, full saturation, tilt) and the enemies
  (portrait, with the INCOMING tag and the HP strip hugging it).
- Mid: the tooltip and primary buttons (4–6px).
- Supporting: the header inventory (stickers at reduced scale, 3px) and the status
  line (a cream notched tag, 3px; owner choice 2026-10-27).
- When two elements share a shadow depth but differ in importance, reduce the
  less important one.

**Sprite set (8).** Shadows need no sprite: each one is the element's own
sprite tinted ink.
1. `ui_panel_stock_9s`: square, 3px ink border, grain baked into a light
   neutral fill, tinted per role (cream, kraft, red, teal, mustard). Used for
   panels, buttons, slot frames and meter frames.
2. `ui_tag_notch_9s`: ticket stub with a 2px border, for tags, INCOMING n,
   status labels and banners.
3. `ui_chip_stamp`: round stamp (not 9-sliced), for the Credits chip, counters
   and Mod badges.
4. `ui_sticker_cutline_9s`: rounded cream cut-line ring, for slots and the
   hover lift.
5. `ui_mark_crop`: one corner bracket, used four times rotated, for the
   valid-target and selection crop marks.
6. `ui_overlay_grain`: tiling paper grain, for the board and panels.
7. `ui_overlay_halftone`: tintable tiling dot screen, for the red danger
   misregistration and the defense-state edge.
8. `ui_overlay_hatch`: tiling diagonal hatch, for disabled and unavailable
   elements.

## 4. Color System

**Primary palette.** These hex values were sampled by eye from `1c.png`,
`Rook.png`, `GoldPistol.png` and `Scanner.png`. Confirm them with an eyedropper
before they go into `DealbreakUITheme`.

| Name | Hex | Source in the art | Meaning |
|---|---|---|---|
| Board | `#1C1814` | UI only, warm ink-brown (owner chose this over green felt) | the stage, never pressed |
| Cream stock | `#EFE4CC` | card paper | panels, the tooltip, primary type on the board |
| Kraft | `#B8956A` | warm neutral | secondary surfaces: map sheet, price board, status strips |
| Ink | `#121212` | card border and pips | type on stock, borders, every shadow |
| Signal red | `#B8382E` | card halftone shadow dots | damage and danger only |
| Teal | `#2E9AA8` | Rook's bandana, Scanner | selection and valid target only (replaces acid lime) |
| Mustard | `#E0A82E` | Gold Pistol | Credits only |

**Semantic rules**
- Red: damage, danger, HP costs, INCOMING n and the defense-state halftone
  edge. Never decorative, never a state cast.
- Teal: selection and valid targets. It is the only cool hue in the UI, so it
  stands out on the warm board without a glow.
- Mustard: Credits (prices, the chip, flying coins, the victory cast).
- Disabled (can't afford, not usable now): fill at 45% value toward the board
  (cream → `#8C8270`), the hatch overlay, no shadow; text stays readable.
- Unavailable (sold, used, locked): the same, and the sticker or panel also
  lies flat at 0°.
- Each fact is shown once, in one colour: a Credits cost is one mustard stamp.

**Contrast (WCAG)**

| Pair | Ratio | Rule |
|---|---|---|
| Ink on cream | ≈15:1 | |
| Cream on board | ≈13:1 | |
| Cream on red | ≈4.6:1 | fine for tag text |
| Red on board | ≈3.1:1 | fails for small text: red is a fill behind cream text, or red text only at ≥24px bold |
| Teal on board | ≈5.3:1 | |
| Teal on cream | ≈2.6:1 | fails for text: never teal text on cream; use a teal fill with ink text (≈5.6:1) or a teal ring |
| Mustard on board | ≈8:1 | |

**UI palette against the art palette.** The UI uses only hues found in the
art, so chrome and art read as one print run. The art keeps the highest
saturation on screen: red, teal and mustard fills together cover less than
about 10% of any UI screen. Teal also appears in portraits (Rook), so a valid
target is told apart by its crop-mark shape, not by colour alone.

**Per-state board tone:** see the table in section 2. No state is tinted teal
or red overall.

**Colourblind safety**
- Red against teal: deuteranopia separates them by lightness (mostly passes);
  protanopia darkens red toward the board and fails without a backup.
- Red against mustard: both collapse to a yellow-brown axis under
  deuteranopia and protanopia and fail at small sizes.
- Red against kraft: borderline under protanopia.
- Tritanopia passes.

Every semantic colour therefore also carries a shape and a motion cue:
- Danger/damage: the notched tag, the halftone misregistration offset and a
  short shake on hit; INCOMING n is spelled out in words.
- Valid target: four crop-mark corners and a one-frame hop when targeting
  starts.
- Selection: a lift to a 12px shadow plus the crop marks. The lift alone must
  be enough to read it.
- Credits: the round stamp, the coin glyph and the fly-to-chip motion.
- Disabled and unavailable: the hatch and the lost shadow, never colour alone.

## 5. Character Design Direction

[To be designed] — the player character's visual archetype, how players tell
enemies, NPCs and allies apart at a glance, expression and pose style, and how
much detail survives at game camera distance (LOD philosophy).

## 6. Environment Design Language

[To be designed] — architectural style and its tie to the world's culture,
texture philosophy (painted, PBR or stylised, and why), prop density rules per
area type, and environmental storytelling guidelines.

## 7. UI/HUD Visual Direction

**Framing.** Every HUD element is a physical paper object resting on the board
(stock, border, hard shadow). Placement stays screen-space: Screen Space
canvas, `CanvasPin`, Expand scaler, no world space, perspective or parallax.
The stage (x .06–.88, y .18–.86) has no frame: the board is the stage, and the
objects on it define it.

**Layout (unchanged positions)**
- Header: the Gear and Trick rows are strips with the heading and count on the
  left ("GEAR 3/5"). Gear's heading is cream and Tricks' is kraft (red is
  danger only). Slots are stickers.
- MAP sits top right under the header rows: a cream button with a map glyph.
- The fanned hand is at the bottom; "HAND n/8" is cream on the board, follows
  the last card, and never tilts.
- The HP panel and Credits chip sit at the left above the hand, with the
  portrait sticker above the chip.
- The draw pile sits at the right edge as a stack of card backs (shadows
  2/4/6px) with a mono count; Contract progress is one small cream line above
  it.
- The red DISCARD strip slides in from the right edge (between MAP and the
  draw pile) only while an item is held.

**Readability floors.** These override any depth or tilt value above.
- Ink shadows and borders on the board are about 1.1:1, so depth there is
  garnish, never the only cue. Anything interactive on the bare board gets a
  cream or kraft edge.
- Selecting a card, or picking one to block with, moves it up at least 24px
  plus crop marks. Its shadow also deepens.
- Valid target: thin teal crop marks. Targeted: thick crop marks plus a teal
  strip under the enemy's HP meter. Marks sit outside the portrait bounds
  (some portraits contain teal) and carry a 2px ink outline.
- While an item is dragged, every legal drop target (card, enemy, HP bar,
  slot, Black Market, DISCARD) shows thin crop marks, and the hovered one
  snaps to thick marks plus the lift. Illegal targets get no mark. DISCARD
  keeps its red. Hit areas stay the untilted rects.
- Fanned hand of eight: lift along the card's local up and cap overlap so
  every rank and suit index stays visible.
- Header: a single cut line (the icon's own); tilt at most ±1°; icons at least
  56px; charge counts on an untilted parent; empty slots are flat dashed
  outlines.
- Credits always appear as a mustard stamp fill with ink type, never as
  mustard text on cream or kraft. Red never appears as text on kraft.
- Any semantic element on kraft carries its 2px ink border (teal, mustard and
  red sit at 1.2–2.1:1 against kraft).
- Grain contrast stays at or under about 5% behind text, there is no dot
  screen behind body copy, and halftone scale is set in screen pixels, so
  Expand-scaled canvases don't moiré.
- Interactive elements keep their shadow in every state, the defeat screen
  included. Only decorative elements flatten.
- Primary buttons use an ink shadow and get their weight from size and the
  display font. Only ATTACK may use a red fill, because it deals damage.

**Typography**

| Role | Font | Weights | Use |
|---|---|---|---|
| Display | Anton (kept) | Regular | titles, button labels, big tag numbers |
| Labels | Barlow Condensed (kept) | SemiBold/Bold, uppercase, tracking +40 | headings, tags, HAND n/8, sticker names |
| Body | Zilla Slab, OFL (replaces Space Grotesk) | Medium 500, SemiBold 600 | tooltip and Incident text |
| Numbers | Courier Prime, OFL (replaces IBM Plex Mono) | Bold | HP, Credits, counts, receipts; fixed width stops counters jittering |

- Sizes at 1080p: XL 72 Anton (victory, defeat, the NEW RUN title); L 48 Anton
  (screen and District titles); M 32 Anton (primary button labels, the
  INCOMING number); numeric 28 Courier Prime Bold (Credits, draw count; HP text on its meter is 20); label 22
  Barlow; body 20 Zilla Slab.
- The minimum is 18px anywhere, and 20px on kraft.
- Check Anton against the card numerals' source. Rokkitt ExtraBold (OFL) is
  the fallback display slab if Anton reads too smooth.
- The switch means regenerating the TMP font assets and the fallback chain.

**Iconography** (shield, coin, hand, deck, map, close, eye)
- Chunky ink silhouettes on a 4px grid at 24/32/48px; strokes at least 3px at
  24px; corners slightly irregular (cut, not vector-perfect).
- On the board: cream fill with a 2px ink outline. On stock: ink only.
- Halftone only at 32px and up, and only as a semantic offset (red dots on
  danger glyphs).
- Shapes: shield is a heater; coin is a round stamp with a notched rim (the
  Credits chip is redrawn to it); hand is three fanned cards; deck is three
  stacked offset cards; map is a folded sheet with a pin; close is two crossed
  tape strips; eye marks scouted nodes.
- All glyphs live in the `Dealbreak Symbols` font so they work inline in TMP.
  `ShieldGlyph`, drawn in code, is redrawn to match.

**Animation feel**
- A shadow tracks lift: shadow distance = rest distance + lift ÷ 2.
- Button press: down 60ms ease-out-quad (moves 4px down-right, shadow 0);
  release 90ms ease-out-back.
- Sticker hop (a trigger): 220ms. Up 90ms ease-out-cubic (rises 10px, scale
  1.08, ±3°); return 130ms ease-out-back.
- Slap (a new item, a reward): fall 120ms ease-in-quad from scale 1.25 with a
  14px shadow; squash 80ms to 0.96; then it settles at its table tilt. On
  rewards only, the board jolts 2px.
- Tag: pop 100ms ease-out-back (scale 0 → 1.1); settle 180ms ease-out
  (±3° → ±1.5°); exit 120ms (fades and drops 6px).
- Panel and tooltip: open 110ms ease-out-cubic (slides 6px out from the source,
  shadow 0 → 6px, fades in, no bounce); close 70ms linear.
- Credits fly: each coin arcs for 350–450ms ease-in-out-sine, staggered 40ms.
  The chip stamps (120ms, scale 1.12) on each landing, and `DisplayedNumber`
  counts up as coins land.
- Order: hit visuals (HP drain, a 120ms 6px shake, the defeat fade) wait for
  `ImpactDelay(hitIndex)`. Triggers play strictly in order through
  `EffectTriggerPresenterUI`, one hop and one tag each.
- The board tone crossfades over 250ms between states. The defense dot screen
  fades in over at least 300ms and never pulses.
- No full-screen shake at any setting.
- Reduce Motion setting (owner ruling, 2026-10-27): shake off; hops and slaps
  become an instant settle; tilt fixed at rest values. Trigger tags still
  appear in order, and `ImpactDelay` sequencing is kept.

**Treatments**
- **Tooltip:** cream stock, 6px shadow, 0° tilt, a notch pointing at the
  source; max width 360px with 16px padding; Zilla Slab 20 in ink with
  numbers in Courier Prime; no heading when the source names itself.
  Placement and sorting order 90 are unchanged.
- **Black Market goods:** each cell is a square sticker of the item art on the
  kraft price board at ±2°. The price is a mustard notched tag across the top
  ("10 CREDITS" in ink, ±1.5°) and the name a cream strip along the foot
  (Barlow 22, ink). Selected: thick teal crop marks outside the cell, a lift,
  and BUY drops in under it (110ms, primary button). Can't afford: the price
  tag is hatched with no shadow and no text. Bought: the cell is bare board.
  REROLL GOODS is a cream button with a mustard price stamp (it was lime).
- **Map node tags:** a notched cream tag with an ink label and a 2px ink border
  on the kraft sheet. A known Enforcer is red with cream type; the current node
  is teal with ink type; a scouted node carries an ink eye glyph (it was a lime
  ring); a Headliner is a portrait sticker; a visited node is flat and
  hatched. Routes are 3px ink lines; the overpass hops stay.
- **Player HP meter:** a strip with a 2px ink border, cream track, red fill and
  ink ticks every 5 HP. "HP n/max" is Courier Prime Bold 20 in ink with a 2px
  cream outline. Block and Shield sit on a line above it (shield glyph and
  number, cream on the board, no shadow, no semantic colour).
- **Enemy HP meter:** larger, ink fill on a cream track (red stays for the
  threat, not the enemy's health). With Shield the fill turns hatch-grey
  (`#8C8270` plus hatch), with one cream shield glyph per charge on the bar.
  ATTACK beside the bar is a small red tag with a glyph and number. Abilities
  are Barlow 18 in cream under the bar.
- **INCOMING n:** one red notched tag under the enemy row (the total): "INCOMING" in
  Barlow 22 and n in Anton 32, both cream, with a cream outer edge for
  protanopia. The 4px red halftone misregistration offset applies to the tag
  body only, and the numerals stay solid. It pops when the value changes; in
  defense the body offset doubles. When fully blocked it counts to 0 and goes
  flat.
- **PASS:** secondary style (cream stock, ink Anton 32, 4px shadow) beside
  BLOCK; never red. Unavailable: hatch, no shadow, and a click opens its info.

**Conflicts decided by the owner (2026-10-27)**
- The ux-designer's readability floors were merged over the art draft.
- Scouted nodes: ink eye glyph (over the UX pick, a cream stamp ring).
- Body and number fonts switch to Zilla Slab and Courier Prime.
- A Reduce Motion setting is added.

## 8. Asset Standards

[To be designed] — file formats, naming convention, texture resolution tiers,
LOD levels, export settings, and the engine's hard limits (poly budgets per
asset category, texture memory, material slots). Where an art preference and a
technical limit disagree, record both and the trade-off chosen.

## 9. Reference Direction

[To be designed] — 3–5 references (games, films, art styles or artists). For
each: the specific element to take from it, and what to avoid so the result
does not read as a copy.
