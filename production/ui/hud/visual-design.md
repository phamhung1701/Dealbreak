# Combat HUD: Visual Design (Phase 2)

> **Status**: Draft for owner review (team-ui Phase 2; the ux-designer also covers the art-director's part, team size "individual")
> **Date**: 2026-10-27
> **Inputs**: `design/ux/hud.md` (sections C, D, Data Sources), `design/art/art-bible.md` (Cut Sheet, sections 3, 4, 7), `design/accessibility-requirements.md` (Basic with elevated features)
> **Scope**: combat HUD only. Nothing here changes layout, behaviour or copy. Owner rulings in `Docs/OWNER_DECISIONS.md` win on any conflict.
> **Contrast figures** below are computed from the art-bible hexes (WCAG relative luminance). They are provisional until the eyedropper pre-step passes; rerun them if any hex moves.

Contents: 0 Pre-step, 1 Token map, 2 Asset manifest, 3 Per-element treatment, 4 Consistency and sequencing, 5 Implementation notes for ui-programmer, 6 Concerns.

---

## 0. Pre-step: eyedropper check (before any hex enters `DealbreakUITheme`)

The seven hexes were sampled by eye. Sample the art with a median (not a mean: the art carries grain speckle and halftone dots) over a small patch, in the file's own pixel space, snapping each patch to a flat fill. Coordinates are read from a downscaled view, so treat them as +/-10px and nudge onto flat fill.

| Token | File | Where to sample | Notes |
|---|---|---|---|
| Cream | `Assets/Art/Cards/Clubs/1c.png` (about 535x765) | four 16x16 patches of clear paper: (270,150), (120,600), (400,650), (150,300). Avoid the pips and speckle | take the median of the four |
| Ink | `1c.png` | the club pip body at (270,330), and the card border (3px) at top centre about (270,8) | the pip and the border should agree; if not, use the border |
| Red | `1c.png` | printed halftone dots right of the pip, centres at about (322,335), (355,395), (330,470) | single-pixel dot centres, median of 10 dots. The dot colour is the token; printed on cream it reads lighter, which is expected |
| Teal | `Assets/Art/Characters/Rook.png` (about 384x384) | flat bandana fill at about (205,95), (235,105), (160,80), away from the dots | Rook's bandana leans blue; accept if hue is within 10 degrees of 188 |
| Teal (second) | `Assets/Art/Items/Gear/Scanner.png` (about 256) | the mid-teal shaded facet on the right and bottom, about (170,145) and (150,205). The pale body (about (75,90)) is a tint, not the token | |
| Mustard | `Assets/Art/Items/Gear/GoldPistol.png` (about 256) | flat gold on the slide at about (140,85) and on the magazine base at about (110,215) | |
| Board, kraft | none: UI-only (board chosen by the owner over felt; kraft has no source) | not sampled | Kraft is checked for contrast only |

**Procedure.** Add `Tools/PrintArt/sample_palette.py` (Pillow, takes file plus box list, prints median RGB and hex). Accept a hex if the sample is within CIE76 delta-E 6 of the bible value; otherwise adopt the sample (or the nearest value that keeps the ratios below) and **rerun section 3's contrast table** with `Tools/PrintArt/contrast_check.py` (the same relative-luminance maths as this document). Gates that must still hold after any change: ink on teal at least 4.5, ink on mustard at least 4.5, ink on kraft at least 4.5, paper on red at least 4.5 (or 3.0 where the text is large, see section 3), teal and red on board at least 3.0 as non-text.

Computed values (effective hexes):

| Pair | Ratio |
|---|---|
| Ink `#121212` on cream `#EFE4CC` | 14.8 |
| Cream on board `#1C1814` | 14.0 (bible says about 13; this is higher, so safe) |
| Cream on defense board `#15110E` | 14.9 |
| Cream on red `#B8382E` | 4.57 |
| Ink on red | 3.25 |
| Red on board | 3.06 (non-text only) |
| Ink on teal `#2E9AA8` | 5.6 |
| Cream on teal | 2.6 (banned) |
| Teal on board | 5.3 |
| Ink on mustard `#E0A82E` | 8.8 |
| Mustard on board | 8.2 |
| Ink on kraft `#B8956A` | 6.7 |
| Cream on kraft | 2.2 (banned) |
| Kraft on board | 6.3 |
| Ink on muted `#8C8270` | 4.9 |
| Cream on muted | 3.0 (glyphs need the ink outline, see Enemy Shield) |
| Cream on disabled red `#722A22` | 8.1 |

**Large text.** WCAG treats text as large at 24px regular or 18.66px bold. Anton (all sizes used, 32) and Barlow Condensed Bold at 22 count as large (3:1 needed). Zilla Slab Medium 20 and Barlow 18 do not (4.5:1 needed); every use of those sits on ink-on-cream or cream-on-board at 14 or better.

---

## 1. Token map

The theme asset lives at `Resources/DealbreakUITheme` (`DealbreakUIStyle.Theme`). **The serialized asset overrides the code defaults in `DealbreakUITheme.cs`, so change values in the asset** (and update the code defaults to match so a fresh asset isn't stale). The code defaults today are blue-slate (`ink` 0.055/0.065/0.075, `charcoal`, `slate`) and `paper` is already within a few units of cream.

### 1a. Legacy fields: value changes

| Field (legacy meaning) | Cut Sheet token | New value | Meaning change? |
|---|---|---|---|
| `ink` (stage) | Board | `#1C1814` | **No.** Still the stage. But `ink` is also read as dark text and border colour on paper (for example `DealbreakUIStyle.Button`: `role==Paper ? Theme.ink : Theme.paper`). Those reads move to the new `inkPrint`. Contrast is fine either way (ink-on-cream 14.8 vs board-on-cream 14.0) but shadows and baked borders need `#121212` |
| `charcoal` (surface) | none (see below) | `#241E18` (warm, one step above the board) | **Deliberately not changed to cream.** Legacy screens put `paper` text on `charcoal` fills. Making `charcoal` cream would turn cream text on cream. It stays a dark raised surface, warmed. Cut Sheet cream stock arrives through sprites and `paper` |
| `slate` (hairline, disabled grey) | none | `#5C5247` (warm hairline) | **Split.** The disabled meaning moves to the new `muted`. Cream on `#8C8270` is 3.0, so pouring the disabled value into `slate` would break every legacy label that sits on it |
| `paper` (off-white) | Cream stock | `#EFE4CC` | No (current value is about `#F0E6CE`; near no-op) |
| `brick` (signal red) | Red | `#B8382E` | No. Danger only; audit in 1c |
| `ochre` (acid lime) | Teal | `#2E9AA8` | **Value only, as decided.** Selection and valid target only. Audit usages (1c): any `ochre` read that means Credits must move to `mustard` first |

### 1b. New fields

| Field | Value or asset | Used for |
|---|---|---|
| `inkPrint` | `#121212` | type on stock, baked-border colour, every shadow tint |
| `kraft` | `#B8956A` | Tricks heading strip, secondary surfaces |
| `mustard` | `#E0A82E` | Credits only |
| `muted` | `#8C8270` | disabled fill (cream toward board) and the enemy Shield fill. One value, two uses by the bible; one field |
| `boardDefense` | `#15110E` | defense board tone. Other states' board tones (map `#221C16`, market `#241B10`, incident, rewards, defeat) are added when those screens are specced |
| `shadowPanel/Button/Slot/Meter` | 6 / 4 / 3 / 0 px (and 12 / 8 for cards) | constants, not theme fields: put them in one static `CutSheetMetrics` class |
| `panelSprite`, `tagSprite`, `chipSprite`, `stickerSprite`, `markSprite` | sprites 1-5 (section 2) | `EditorialFrame` sprite mode |
| `grainSprite`, `halftoneSprite`, `hatchSprite` | sprites 6-8 | overlays |
| `slotDashedSprite` | proposed 9th sprite | empty slots (see 6, item 3) |

### 1c. Existing fields that change role

| Field | Today | Cut Sheet |
|---|---|---|
| `headingFont` | Anton | Anton, unchanged |
| `labelFont` | Barlow Condensed Bold | unchanged. Tracking: the code uses `characterSpacing = 6`; the bible's "+40" is thousandths of an em (CSS or Figma units). TMP counts hundredths, so +40 is about **4**. Keep the existing 6 unless a mock shows otherwise; do not type 40 |
| `bodyFont` | Space Grotesk | **Zilla Slab** (asset swap) |
| `numericFont` | IBM Plex Mono | **Courier Prime Bold** (asset swap). Courier Prime's advance is 0.6 em like Plex Mono, so counter widths hold |
| `symbolFont` | `Dealbreak Symbols`, a dynamic asset built on `LiberationSans.ttf` (`DealbreakUIOverhaulAuthoring.cs:20`) | rebuilt from a custom TTF (section 2c) |
| `halftoneMaterial` | `UI/Dealbreak Halftone` prints every flat fill with a dot screen (`DealbreakUIStyle.PrintFill`) | **Role narrows.** Cut Sheet bans dot screens behind body copy. Combat stops calling `PrintFill`; the material stays for legacy screens |
| `paperTexture`, `slateTexture`, `darkTexture`, `dryAccents`, `cleanAccents`, `inkAccentMaterial` | legacy flat-panel overlays | untouched; unused by combat |
| `creditsIcon` | current coin sprite | replaced by the notched-rim coin (2c) |

Usage audit before the value swap (149 reads across 30 files): every `Theme.ochre` read gets classified selection vs Credits; every `Theme.ink` read as stage vs type-on-stock/border/shadow; every `Theme.charcoal` read paired with its text colour. The heaviest files: `EditorialButton` (20), `DealbreakUIStyle` (21), `ItemDragUI` (12), `TutorialCoachUI` (9), `ShopOfferUI` (8), `EffectTriggerPresenterUI` (8), `EnemyDisplayUI` (7).

---

## 2. Asset manifest

**Conventions.** Reference resolution 1920x1080, Expand scaler. All sprites are authored at 1 sprite pixel = 1 reference pixel and imported at **PPU 100** (equal to the canvas reference pixels per unit of 100, so one sprite pixel is one reference pixel). Shared import settings unless stated:

- Texture Type: Sprite (2D and UI); Sprite Mode: Single; Pivot: Center; sRGB: **on** (colour art); Alpha Source: Input Texture Alpha; Alpha Is Transparency: on.
- Mesh Type: **Full Rect** (required for 9-slice and tiling).
- Generate Mip Maps: off; Filter Mode: Bilinear (the Expand scaler resamples at 1440p and 2160p; Point would shimmer); Wrap Mode: Clamp (Repeat for overlays); Aniso 0.
- Compression: None (RGBA32). These are tiny; keep ink edges clean.
- Read/Write: off. Max size 256 (the largest sprite is 96).
- Straight-alpha PNG, 8-bit RGBA, sRGB. Author with transparent pixels carrying the neighbour's colour (no black fringes): the generator dilates colour into alpha-0 pixels.
- Tinted sprites are authored in **near-white** (mean about `#FAFAFA`) with the ink border baked in. Effective stock colour is tint x 0.98; the contrast table already tolerates that. Ink-coloured shadows are the same sprite with `Image.color = inkPrint` (the entire sprite, border included, goes ink).
- Sprites 1-5 (+ dashed) pack into one SpriteAtlas `CutSheetUI` with **Tight Packing off** (rectangle packing; tight packing breaks 9-slice), padding 4. Overlays 6-8 stay out of the atlas (own textures, Wrap Repeat).
- Generator: one deterministic script, `Tools/PrintArt/make_ui_sprites.py` (Pillow plus numpy, same dependencies as the other `Tools/PrintArt` scripts, `requirements.txt` updated). Draw at 4x and downsample with LANCZOS for anti-aliasing. A fixed seed produces every noise tile, so reruns are byte-identical. Output folder `Assets/Art/UI/CutSheet/`.

### 2a. The eight sprites

| # | File | Size (1080p px) | 9-slice border (L,B,R,T) | Procedural? | Notes |
|---|---|---|---|---|---|
| 1 | `ui_panel_stock_9s.png` | 64x64 | 4,4,4,4 | **Yes** | Square, 3px ink border (plus 1px AA), near-white fill with grain at +/-2% luminance. **Use Image Type Tiled, not Sliced**: the 56x56 centre must tile seamlessly (the generator uses wrap-around noise), so grain stays 1:1 instead of stretching blurry on large panels. Border strips are flat ink so tiling the edges leaves no seam |
| 2 | `ui_tag_notch_9s.png` | 64x40 | 14,12,14,12 | **Yes** | Ticket stub, 2px ink border, near-white flat fill. **Notches are scallops cut into the four corners (radius 6), not a half-circle at mid-height**: a mid-height notch lives in the stretching band and would distort at the 40 to 64px tag heights used (INCOMING is 64 tall). Image Type Sliced. See 6, item 2 |
| 3 | `ui_chip_stamp.png` | 96x96 | none | **Yes** | Round disc, 16 scallops on the rim, 2px ink border, near-white fill. Image Type Simple, used from 32 to 56px. Not sliced (the circle would distort) |
| 4 | `ui_sticker_cutline_9s.png` | 64x64 | 10,10,10,10 | **Yes** | A 2px near-white ring with outer corner radius 6 and a transparent centre. No ink: it is the item art's own off-white cut line. Image Type Sliced, Fill Center off. Tint paper |
| 5 | `ui_mark_crop.png` | 32x32 | none | **Yes** | One top-left corner bracket: arm length 22, stroke 3px near-white, 2px ink outline each side (7px total weight), bracket corner at (4,4). Used four times rotated 0/90/180/270. Thick marks are the same sprite at scale 1.6 (stroke 4.8, outline 3.2); the snap from thin to thick is a scale tween |
| 6 | `ui_overlay_grain.png` | 256x256 | none | **Yes** | Seamless wrap-around noise, grey, alpha at most 5% (about 0.05 at the extremes, mean 0.02). Wrap Repeat, Image Type Tiled |
| 7 | `ui_overlay_halftone.png` | 16x16 | none | **Yes** | White dots on transparent, pitch 11.3px on a 45 degree lattice: dot centres at (4,4) and (12,12) in the tile (lattice vectors (8,8) and (-8,8)), radius 3.2px (about 25% coverage), 4x supersampled. Tint red for the misregistration, board-dark for the defense edge. Wrap Repeat, Image Type Tiled |
| 8 | `ui_overlay_hatch.png` | 12x12 | none | **Yes** | 45 degree stripes, 2px wide (3px measured along x), pitch 12 (about 8.5px between lines), **ink at 50% alpha**. Between the lines the ground stays at 4.9:1 ink-on-muted; on the stripes it drops locally (inactive components are exempt from WCAG 1.4.3, so this is acceptable; the label stays on top). Wrap Repeat, Image Type Tiled |

**Overlay scale rule (no moire).** The Expand scaler stretches the canvas, so a tiled sprite's pitch would scale with it. A small component `ScreenPixelTile` sets `Image.pixelsPerUnitMultiplier = canvas.scaleFactor` (rechecked on resolution change) so one overlay texel is exactly one screen pixel at 1440p and 2160p. Image Type Tiled on a Repeat texture; no mipmaps. This is how the hud.md "halftone is set in screen pixels" is met.

### 2b. Proposed additions (not in the approved eight)

| File | Size | Border | Why |
|---|---|---|---|
| `ui_slot_dashed_9s.png` (9th) | 70x70 | 10,10,10,10 | The empty Gear/Trick slot is a "flat dashed outline" (hud.md). Neither the ring (solid) nor the crop mark (a valid-target signal) can stand in. 2px near-white dashes, 6 on 4 off, period 10 so the 50px edge strip holds five whole dashes. Image Type Tiled, tint paper. **Needs owner approval** (6, item 3) |
| `Credits_Coin.png` | 64x64 | none | Flying coins and the coin disc need a sprite, not the glyph. Rasterised from the same master SVG as the coin glyph: mustard fill, 2px ink outline, notched rim. Replaces `theme.creditsIcon` at `Assets/Art/UI/Sprites/` |

### 2c. Glyphs (the `Dealbreak Symbols` font)

One TTF carries all seven, so they work inline in TMP. Today the asset is built from `LiberationSans.ttf`; replace it.

| Item | Spec |
|---|---|
| Source | `Assets/Art/Fonts/DealbreakSymbols.ttf`, built by `Tools/PrintArt/make_symbols_font.py` from seven hand-drawn SVGs in `Tools/PrintArt/symbols/` (fontTools plus an SVG path parser). Then regenerate `Dealbreak Symbols.asset` |
| Codepoints (private use) | U+E000 shield, E001 coin, E002 hand, E003 deck, E004 map, E005 close, E006 eye |
| Grid | design on a 48x48 unit grid, one unit = 1px at 48px. 4px snap. Use at 24, 32 and 48px. Stroke at least 6 units (3px at 24px). Corners slightly irregular (cut, not vector-perfect) |
| Shapes | shield: heater; coin: round stamp with a notched rim; hand: three fanned cards; deck: three stacked offset cards; map: folded sheet with a pin; close: two crossed tape strips; eye: marks scouted nodes |
| Production | **Hand art** (seven drawings, vector; about half a day). The font build is scripted. Glyphs are monochrome, so the bible's two tones come from the material, not the font: **on the board** = paper face with a 2px ink outline (TMP material preset `Symbols Board`); **on stock** = ink face, no outline (`Symbols Stock`). Tune the outline width visually, since TMP thickness is relative to the SDF |
| Halftone on danger glyphs | A font cannot carry it, and the bible allows it only at 32px+. Not built; the red offset lives on the tag body. See 6 |
| Hand and deck glyphs | Not used by any hud.md row. They are for other screens (deck view). Author them with the set so the font is built once |
| `ShieldGlyph` (drawn in code) | export the shield outline polygon from the master SVG into a generated points array, so the in-code glyph (enemy Shield bar) matches the font |
| Atlas | static SDF, 1024x1024, sampling point size 90, padding 9, Render Mode SDFAA. Character set: seven private-use codepoints only |

### 2d. Fonts

| Font | Files | Source and licence | Target | Notes |
|---|---|---|---|---|
| Anton (kept) | `Anton-Regular.ttf` | unchanged | `Assets/Art/Fonts/Anton UI.asset` | Check against the card numerals; fallback Rokkitt ExtraBold only if it reads too smooth |
| Barlow Condensed (kept) | `BarlowCondensed-Bold.ttf` | unchanged | `Barlow Condensed UI.asset` | Only Bold exists in the repo. The bible allows SemiBold; keep Bold only (it counts as large text at 22) |
| Zilla Slab (new) | `ZillaSlab-Medium.ttf`, `ZillaSlab-SemiBold.ttf` | Google Fonts, OFL | `Zilla Slab UI.asset` (Medium) and `Zilla Slab SemiBold UI.asset` | Replaces Space Grotesk. Include the OFL text next to the font |
| Courier Prime (new) | `CourierPrime-Bold.ttf` | Google Fonts, OFL | `Courier Prime UI.asset` | Replaces IBM Plex Mono. Check the glyphs for the bullet and the multiplication sign |

Generation for all new TMP assets: static SDF, atlas 1024x1024 (Latin only, so one page), sampling point size 90, padding 9, Render Mode SDFAA, multi-atlas off, Atlas Population static. Character set: ASCII 32-126 plus the bullet, multiplication sign, apostrophe and quotes, en and em dashes. **Fallback chain** for the three text fonts: the font, then `Dealbreak Symbols`, then the LiberationSans fallback. Keep `Space Grotesk UI.asset` and `IBM Plex Mono UI.asset` in the project until the last legacy screen has moved (section 4).

---

## 3. Per-element visual treatment

### 3a. State recipes (shared; rows below list only exceptions)

Colour is never the only indicator: every state change below has a shape, position or shadow change.

| State | Recipe | Non-colour cue |
|---|---|---|
| Normal | per row | the shadow |
| Hover (mouse, interactive only) | translate 2px up-left, shadow +2px (a button goes 4 to 6). **No tint change**, so contrast never moves | the lift |
| Pressed (buttons) | 60ms down-right 4px, shadow 0; release 90ms ease-out-back | the movement |
| Focus (keyboard; focus opens info) | a 2px paper ring (`ui_sticker_cutline_9s`, 4px outset) around the element. It sits on the board, where paper is 14.0:1. Visible on every element; the item slots, cards and meters use it as their only focus cue | the ring |
| Selected | cards: lift at least 24px, thick teal crop marks, shadow = rest + lift/2 (8 + 12 = 20). Toggled buttons: latched down (see Sort) | the lift alone reads |
| Disabled | fill 45% toward the board (paper to `muted` `#8C8270`), hatch overlay beneath the label, **shadow 0**, label ink | the hatch and the lost shadow |
| Board busy (trigger playback) | buttons keep their look; no hatch | (hatch means unavailable) |

### 3b. Elements

Tilts follow the bible (slots at most +/-1 degree here, tags settle to +/-1.5, status tag no tilt). Shadow = the element's own sprite tinted `inkPrint`, offset down-right at 45 degrees. A row's contrast is the text or glyph pair; the board tone behind is `#1C1814` (defense `#15110E`).

| Element | Sprite and tint | Type | Shadow | State exceptions | Contrast and non-colour |
|---|---|---|---|---|---|
| Gear row | heading strip: tag sprite, `paper`. Slots: item art with its own cut line; slot ring `ui_sticker_cutline_9s` paper shown on hover and focus. Tier mark and stack: `ui_chip_stamp` 32px disc, `paper` | heading "GEAR n/cap": Barlow Condensed Bold 22 ink, tracked, uppercase. Tier and stack: Courier Prime Bold 20 ink | strip 0 (label, not pressed); slots 3 (the icon's own alpha tinted ink); disc 3 | Slots: hover lift 2px; focus ring; selected = ring stays while its tooltip is open. Empty: `ui_slot_dashed_9s` paper at full strength, shadow 0. Trigger hop 220ms | Ink on paper 14.8. Dashes and ring: paper on board 14.0. Slots are at least 56px |
| Trick row | strip: tag sprite, `kraft`, 2px ink border (baked in the tag). Slots as Gear | "TRICKS n/cap": Barlow Bold 22 ink | as Gear | as Gear | Ink on kraft 6.7 (text is above the 20px kraft floor). No red text on kraft. Charge and stack discs sit on an untilted parent |
| MAP | panel sprite, `paper` | "MAP" / "CLOSE MAP": Barlow Bold 22 ink, with the map or close glyph at 24 (ink, Symbols Stock) | 4 | recipes | Ink on paper 14.8. The word is the cue. Button is 152x42: Anton 32 does not fit, so it uses Barlow |
| Portrait sticker | the portrait art with its own cut line; no extra frame | none | 3 | hover lift 2px; focus ring | decoration: no colour carries meaning |
| Player HP meter | frame: panel sprite tinted `inkPrint` (solid ink), with the track inset 2px, so the border reads as 2px ink with no extra sprite. Track `paper`; fill `brick`; ticks: 2px `inkPrint` every 5 HP | "HP n/max": Courier Prime Bold 20 ink with a 2px paper outline (the bible's explicit 20, not 28) | 0 | no pressed state. Hover/focus: paper ring only | Ink on red fill 3.25 with the cream outline making a 14:1 contour; text is 20px bold, which counts as large (3:1). Number and fill length carry HP. If the tick spacing falls below 6px (for example with a very high max HP), tick every 10 |
| Block / Shield line | none; glyph and text on the board | shield glyph 24 (paper face, 2px ink outline) plus n in Courier Prime Bold 28 paper; or "BLOCK n": Barlow Bold 22 paper | 0 | hidden at 0. Pop 100ms on change | Paper on board 14.0. No semantic colour; glyph plus number |
| Credits chip | coin disc: `ui_chip_stamp` `mustard`, with the coin glyph 32 ink. Number plate: tag sprite `mustard`, behind the disc | Courier Prime Bold 28 ink | 3 on each | stamp 120ms (scale 1.12) per coin landing. Hover: paper ring only | Ink on mustard 8.8. Round stamp, coin glyph and the flight to the chip carry Credits. Chip is 210x46 in code, so the disc is 46px and the plate 38px tall (see 6, item 1) |
| Enemy | portrait sticker (art cut line); name on the board; no frame until targeted | name: Barlow Bold 22 paper, uppercase. "HP n/max": Courier Prime Bold 20 paper, **beside the meter on the board** (it would not survive a split ink/paper background). Abilities: Barlow Bold 18 paper | 6 under the portrait | Hover lift 2px; focus ring. Targeted: see Target marks. Hit shake 120ms, 6px. Defeat fade | Paper on board 14.0 (names, abilities and HP). Barlow 18 is not large text, so the 14.0 matters |
| Enemy HP meter | frame: ink panel as the player's; track `paper`; fill `inkPrint` with a **2px `paper` gap** between frame and fill (otherwise a full meter melts into its own border) | none (the number is beside it) | 0 | recipes | Ink fill on paper track 14.8. The fill length carries HP |
| ATTACK tag | tag sprite `brick` | "ATTACK n": Barlow Bold 22 paper | 3 | none | Paper on red 4.57, large text (22 bold) so 3:1 applies. The word is the backup. **No attack glyph exists in the set of seven**, so the tag carries the word only (see 6) |
| Enemy Shield | fill `muted` plus hatch over the HP meter; one shield glyph per charge, ×n after 4 | glyph 24, paper face with a 2px ink outline | 0 | crossfade on gain; glyph pop | Paper on muted is 3.0, so the **2px ink outline is required** (it gives a 14.8 contour). Glyph count is the non-colour cue |
| Intent | none, on the board | Barlow Bold 18 paper | 0 | hidden when it is an ordinary attack | Paper on board 14.0 |
| Target marks | `ui_mark_crop` x4, `ink` outline baked, tint `ochre` (teal). Thin = scale 1; thick = scale 1.6. Strip under the HP meter: ink panel with a teal inset | none | 0 | valid: thin; targeted: thick plus the strip; snap 90ms. Marks sit outside the portrait bounds (Rook has teal) | Teal on board 5.3 (non-text 3:1 met). Shape and position carry it, not hue. A one-frame hop when targeting begins |
| INCOMING n | tag sprite `brick`; a second tag sprite behind it, `paper`, 2px larger on each side, is the cream outer edge. The red halftone offset: `ui_overlay_halftone` tinted `brick`, masked by the tag shape offset 4px down-right (8px in defense), visible only outside and over the body edge; numerals sit above on solid fill | "INCOMING": Barlow Bold 22 paper. n: Anton 32 paper. "ALL BLOCKED" replaces both when n is 0 | 3 | pop 100ms; tilt +/-3 settling to +/-1.5; when fully blocked it counts to 0, goes flat (offset 0, tilt 0). Defense board `#15110E` | Paper on red 4.57, large. Red against the board is 3.06, so the cream edge is mandatory (also protanopia). The words, the notched shape, the offset and the shake carry danger |
| Status tag | tag sprite `paper`, no tilt | Barlow Bold 22 ink uppercase; numbers in Courier Prime Bold 22 ink | 3 | text swaps with a 250ms board-tone crossfade between states | Ink on paper 14.8 |
| Selection preview | inside the status tag (same sprite) | as the status tag | 3 | fade swap (instant with Reduce Motion) | 14.8. One builder |
| ATTACK button | panel sprite `brick` (the only red fill) | Anton 32 `paper` | 4 | Disabled: red at 45% toward the board (`#722A22`), hatch, shadow 0, label paper. "Cancel" while targeting uses the same style | Paper on red 4.57, large. Disabled: 8.1. The label, the lift and the hatch carry the state |
| BLOCK / CONFIRM BLOCK | panel sprite `paper` | Anton 32 `inkPrint` | 4 | disabled as the recipe (ink on `muted` 4.9) | 14.8. Label change confirms the mode |
| PASS | panel sprite `paper`, never red | Anton 32 `inkPrint` | 4 | disabled: hatch, no shadow, still clickable for its info | 14.8; disabled 4.9 |
| Sort buttons | panel sprite `paper`, 29px tall | Barlow Bold 22 ink | **3** (a deliberate step below the 4px button depth: a small secondary control, and 4px on 29px is heavy) | **Active sort is latched down**: shadow 0, offset 4px down-right, ink face, paper label. Disabled: hatch, shadow 0, paper face dimmed | Ink on paper 14.8; active paper on ink 14.8. Latched position carries the state; hatch means disabled, never confused with latched because latched keeps a dark face |
| Auto-Recover notice | status tag | as the status tag | 3 | none | 14.8 |
| Hand cards | existing card art; shadow = the card sprite tinted ink | the art's own | rest 8; selected 8 + lift/2 (20 at the 24px lift); hover +4 | Hover: lift 8px. Selected: lift at least 24px, four thick teal crop marks. **Cannot block (defense): 70% value plus a 20% hatch overlay**, never lifting | Index ink on cream at 70% value is about 7:1. The bible's "70% value" alone is a value cue only; the hatch makes it consistent with "disabled is never colour alone" (see 6). Rank and suit index stay visible with eight cards |
| HAND n/8 | none, on the board | Barlow Bold 22 paper; numerals Courier Prime Bold 22 | 0 | follows the last card; never tilts | 14.0 |
| Draw pile | existing card backs; shadows 2/4/6px (each back's sprite tinted ink). Count disc: `ui_chip_stamp` `paper` at the pile's lower right | count: Courier Prime Bold 28 ink | 2/4/6 on the stack; disc 3 | Hover: top card lifts 4px. Pressed: the stack drops 2px, shadows collapse to 2. Focus ring | Ink on paper 14.8 |
| Contract line | none, on the board | Zilla Slab Medium 20 paper; numerals Courier Prime Bold 20 | 0 | It is clickable but has no shadow (a line), so it gets a **2px paper underline** as its affordance (hover: 3px; focus: ring) | Paper on board 14.0, 20px |
| Drop-target marks | `ui_mark_crop` x4 teal as Target marks; illegal targets get nothing | none | 0 | thin; hovered: thick plus a 4px lift | as Target marks. DISCARD keeps its red |
| DISCARD strip | panel sprite `brick` | "DISCARD": Anton 32 `paper`, **rotated 90 degrees** (the strip is 90px wide and the word about 125px wide) | 6 | slides in 110ms from the right edge. Hovered: thick teal crop marks and a 4px lift | Paper on red 4.57, large. The word, the position and the marks carry it. Rotated single-line text is not tilt (a quarter turn; see 6) |
| Pending drop + LEAVE IT | item sticker 150px with `ui_sticker_cutline_9s` ring, `paper`. LEAVE IT: panel sprite `paper` | LEAVE IT: Barlow Bold 22 ink | sticker 6 (14 during the slap fall); button 4 | slap: fall 120ms from scale 1.25, squash 80ms to 0.96 | Ink on paper 14.8 |
| Contextual tooltip | panel sprite `paper` (Tiled, grain baked in). Notch: a 14px panel sprite rotated 45 degrees, drawn **under** the panel, so a half-diamond peeks out toward the source | Zilla Slab Medium 20 ink; numbers Courier Prime Bold; heading (if any) Barlow Bold 22 ink | 6 (notch has its own) | opens 110ms (slides 6px, shadow 0 to 6); closes 70ms; no tilt; 360px max, 16px padding; text scales 100/115/130% | Ink on paper 14.8. Zilla 20 is not large text; 14.8 clears 4.5 by a wide margin |

### 3c. Colour is never the only indicator (verification)

| Colour | Meaning | Backup cue present in section 3b |
|---|---|---|
| red | danger, damage | notched tag, the words INCOMING and ATTACK and DISCARD, halftone offset, shake, numeric HP |
| teal | valid target, targeted | crop marks (shape) outside the portrait, thin vs thick, the strip, the hop |
| teal | selection | the 24px lift, which reads on its own |
| mustard | Credits | round stamp, coin glyph, flight to the chip |
| `muted` + hatch | enemy Shield | one glyph per charge |
| hatch + no shadow | disabled | never colour alone |
| 70% value (cards that can't block) | can't be used for blocking | **added** hatch overlay, plus no lift on click |
| ink face | active sort | latched-down position |
| paper ring | keyboard focus | the ring itself (shape) |

Banned and avoided: red or teal text on cream, red text on kraft, teal text on cream, mustard text on cream or kraft, cream text on kraft. None appears above. Anything semantic that sits on kraft (the Trick strip) carries its 2px ink border.

---

## 4. Consistency with the unspecced screens, and sequencing

The Black Market, map, Incident and menus still render neo-punk (flat panels with 1px hairlines) and read the same `DealbreakUITheme`. Three ways to sequence:

### Option A: theme-wide token swap first
Change the six legacy values and the four fonts in the theme asset, and let every screen pick them up; add sprites for combat afterwards.
- **Pros:** one coherent palette and type family at once; no transitional theme code; teal replaces lime on every screen in one step.
- **Risks:** (1) any `ochre` read that means Credits (prices, chip) turns teal, which breaks "teal is selection only" and the Credits rule; this needs the 149-read audit first. (2) `paper` text on `charcoal` fills keeps working only because `charcoal` is not set to cream (1a). (3) Font swap changes metrics on screens not yet reviewed: Zilla Slab and Courier Prime widths differ from Space Grotesk and Plex Mono, so labels may overflow on the Black Market and Incident layouts that nobody has captured since. (4) Other screens look flat-and-warm, not Cut Sheet: a palette match with a shape mismatch. (5) Tests or authoring scripts that pin colours break without warning.

### Option B: combat-only
Give combat its own theme asset and switch with `DealbreakUIStyle.SetTheme`.
- **Pros:** other screens cannot regress.
- **Risks:** colours and fonts are **baked into frames when `Frame()` and `Button()` run**, so a runtime theme switch leaves stale colours on pooled and persistent UI. Combat and the map, shop and Incident share one scene (`Game.unity`), so the switch would fire on every screen transition, at the moment the player is looking. A flash of the wrong skin on entry is likely, and two skins to maintain until the last screen moves. Not recommended.

### Option C (recommended): additive first, then one bounded swap
1. **No-visual-change commit:** add the new theme fields (1b), the eight sprites, the fonts and the `EditorialFrame` sprite mode. Nothing reads them yet.
2. **Audit commit:** classify the `ochre`, `ink` and `charcoal` reads (1c). Retarget Credits reads to `mustard` and type-on-stock reads to `inkPrint`. Still no visible change (values unchanged).
3. **Swap commit:** change the legacy values and fonts in the asset (1a, 1c). Capture all four non-combat screens before and after at 1920x1080 and 2560x1440 and check for overflow. Legacy screens now share palette and type, in flat frames.
4. **Combat reskin:** combat elements opt into sprite frames, shadows and overlays one zone at a time (header, hand, action bar, enemy row, tag band, tooltip).
5. Each later screen spec (Black Market, map, Incident, menus) follows with the same opt-in step; when the last one is done, delete the legacy frame path and the two old font assets.

- **Risk of C:** a period (the length of steps 4 and 5) where combat reads Cut Sheet and the rest reads "Cut Sheet palette, flat frames". Mitigations: the palette and type already match, so the seam is the shape language, not a clash. Keep that period short by specifying the Black Market and map next, the two screens a player meets most between fights.
- The tooltip is shared by every screen (`ContextualTooltipUI`, one instance). Reskin it in step 4 together with combat; it then looks Cut Sheet on every screen, which is the one place the seam would otherwise show. Verify its look on the legacy screens' backgrounds.

---

## 5. Implementation notes for the ui-programmer

Interaction and visual spec only. No code is written here.

- **`EditorialFrame` sprite mode.** Today it draws a flat fill with a 1px rim (`frame.border=1; frame.corner=0`). Add a sprite mode: sprite, tint, shadow depth, tilt. Shadows are a duplicate `Image` with the same sprite (or the built-in `UnityEngine.UI.Shadow` with `effectColor = inkPrint`, `useGraphicAlpha` off), offset `(d, -d)`, with `d` animatable for lift and press.
- **`EditorialButton`** roles map to the recipes in 3a. Pressed: move the face and shadow together. Disabled: the face lerp to `muted`, the hatch child and shadow 0. Keep `RoleFor`.
- **Hatch and halftone** children use `ScreenPixelTile` (section 2a). The hatch layer sits under the label, above the face.
- **Tilt** is a visual child transform from a fixed per-slot table; hit areas stay the untilted rects.
- **Number text on meters** uses `DealbreakUIStyle.Outline`; set the outline to paper (it is black today) for the HP number.
- **Authoring scripts.** Any element that moves or changes size also changes in `DealbreakNeoPunkAuthoring`, `DealbreakCentralStageAuthoring` and `DealbreakUIArtCorrectionAuthoring` together with the scene.
- Reduce Motion, per hud.md: hops and slaps settle instantly; tilt fixed at rest values; shadow animation on lift and press stays (it is not motion of position, but make it instant under Reduce Motion for consistency).

---

## 6. Concerns and decisions needing the owner

1. **Credits chip shape.** The bible says "round notched mustard stamp", but the chip carries a number and is 210x46 in code, and a round stamp cannot hold it. This spec builds it as a round stamp disc (coin) overlapping a mustard tag plate (the number). Both are existing sprites. Alternative: a stadium-shaped chip (a 9th sprite).
2. **Tag notches.** The bible's "notched ends" is built here as corner scallops, so one sprite works from 40 to 64px tall. A mid-height notch would work only at fixed heights per tag. Confirm the look in a mock.
3. **Dashed empty slots** need a 9th sprite (`ui_slot_dashed_9s`) or a rule change. The solid ring is a cut line; crop marks mean "valid target".
4. **No attack glyph** exists among the seven, so the ATTACK tag carries the word and number only. Add an eighth glyph or accept.
5. **Selected-card shadow.** The bible's section 2 says a selected card's shadow is 12px; its animation rule (shadow = rest + lift/2) gives 20px at a 24px lift. This spec uses the rule. Confirm.
6. **Cards that can't block** are dimmed to 70% value only in the bible. This spec adds a light hatch so "disabled is never colour alone" holds.
7. **Sort button shadow** is 3px (the bible's button is 4). Chosen for a 29px-tall control.
8. **Clickable elements without a shadow** (Contract line, meters, HP panel): they get an underline or a focus ring as their affordance, because the "no shadow means not interactive" rule would otherwise hide them.
9. **DISCARD text is rotated 90 degrees** because the strip is 90px wide. Rotation is orientation, not tilt; confirm it reads well, or widen the strip (hud.md fixes it at x .895-.985).
10. **Tracking.** The bible's "+40" is a thousandths-of-an-em figure; in TMP use about 4 (the code's current 6 is acceptable). Do not type 40.
11. **Grain baked into the panel sprite stretches** if the sprite is Sliced. Mitigated by Image Type Tiled with a seamless centre; the bible's "baked grain" is kept.
12. **Eyedropper:** the colour values are provisional (section 0). The contrast table must be rerun if any hex moves.
13. **Hand and deck glyphs** are not used by any HUD row. They are authored with the set for other screens.
14. Symbols font is monochrome, so the bible's red halftone on danger glyphs (32px+) is not built in v1; the danger cue stays on the tag body.
