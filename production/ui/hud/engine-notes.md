# Combat HUD: Engine Notes and Implementation Plan (Phase 3)

> **Status**: Plan for owner approval. No code or assets written. The owner approves the file list per slice before any code.
> **Date**: 2026-10-27
> **Inputs**: `design/ux/hud.md` (approved), `production/ui/hud/visual-design.md` (approved; owner rulings: add the 9th sprite `ui_slot_dashed_9s`; Credits chip = coin disc plus mustard plate, no stadium sprite; sequencing option C), `CLAUDE.md`, `docs/engine-reference/unity/` (pinned 6.3 LTS; project editor `6000.3.24f1`; `com.unity.ugui` 2.0.0).
> **Author**: ui-programmer (also covering the Unity UI specialist; team size "individual").
> Items marked **verify** were not confirmed against code during planning; confirm them before the slice that depends on them.

Contents: 1 Engine notes, 2 Implementation slices, 3 External downloads, 4 Slices that need a human, 5 Concerns found in code.

---

## 1. Engine notes

### 1.1 UGUI, not UI Toolkit

The HUD stays on **UGUI (Canvas) with TextMeshPro**. The engine reference (`modules/ui.md`, `deprecated-apis.md`) recommends UI Toolkit for *new* projects; this is a reskin of a shipped UGUI screen, so that advice does not apply:

- The whole combat HUD is UGUI: `EditorialFrame` is a `MaskableGraphic` with a custom `OnPopulateMesh`; `CanvasPin`, `CreditsHudUI`, `ContextualTooltipUI`, `ItemDragUI`, `EffectTriggerPresenterUI`, `AttackCardPresentationUI` build nested canvases and RectTransforms at runtime.
- The layout is written by the `Assets/Editor` authoring scripts into `Game.unity` as RectTransform anchors, and EditMode tests find objects by scene path (`GameSceneReadabilityTests`, `UiQaLayoutTests`, `DealbreakUIOverhaulTests`). A UI Toolkit port would rewrite every one of them and both scenes.
- Card views, drag, drop targets and the item rules use `IPointer*Handler` / `EventSystem` raycasts; mixing UIDocument panels into the same screen would split input and sorting between two systems.
- Nothing in the Cut Sheet look needs UI Toolkit: 9-slice, tiling, sprite tints, stencil masks and SDF outlines are all native UGUI/TMP features.
- In Unity 6, TextMeshPro ships inside `com.unity.ugui` 2.0 (namespace still `TMPro`). **Verify** in the Package Manager that no separate `com.unity.textmeshpro` is installed alongside it.
- The 6.3 "desktop screen reader support" in `VERSION.md` is not documented for UGUI in our reference files; treat it as **unverified** and keep hud.md's "screen reader out of scope".

### 1.2 Sprites: 9-slice, tiled, shadows, overlays

**Import (one authoring path, not hand-clicked).** A new editor script `Assets/Editor/CutSheetAssetAuthoring.cs` applies the visual-design §2 import settings through `TextureImporter` (`spriteImportMode = Single`, `spriteMeshType = FullRect`, `spriteBorder` per sprite, `spritePixelsPerUnit = 100`, `mipmapEnabled = false`, `filterMode = Bilinear`, `wrapMode` Clamp or Repeat, `textureCompression = Uncompressed`, `maxTextureSize = 256`, `alphaIsTransparency = true`) and builds the `CutSheetUI` atlas with Tight Packing off and padding 4. Running it twice is a no-op. Doing it in code keeps the `.meta` files reproducible and testable.

- **Atlas**: Unity 6 defaults to Sprite Atlas V2 (`.spriteatlasv2`); the `SpriteAtlasAsset`/`SpriteAtlasImporter` editor API differs from V1's `SpriteAtlas.SetPackingSettings`. **Verify** which mode `EditorSettings.spritePackerMode` is in before writing the authoring code. Overlays (grain, halftone, hatch) stay out of the atlas (a packed sprite cannot use texture wrap).
- **Sliced** (`Image.type = Sliced`): tag, sticker cut line (`fillCenter = false`), mark crop (Simple), chip stamp (Simple).
- **Tiled** (`Image.type = Tiled`): the panel stock (grain centre tiles 1:1) and the dashed slot. Edges must be flat so a tiled edge has no seam (the generator guarantees it).
- **`Image.pixelsPerUnitMultiplier`** controls tile and border scale. With sprite PPU 100 and `CanvasScaler.referencePixelsPerUnit` 100, one sprite pixel = one reference pixel at multiplier 1. **Verify** the scaler's `referencePixelsPerUnit` is 100 on the `Game.unity` canvas.

**`ScreenPixelTile` (overlays at one texel per screen pixel).**
- Image's effective pixels-per-unit is `sprite PPU / reference PPU × multiplier`, and one canvas unit is `scaleFactor` screen pixels, so the multiplier that gives one texel per screen pixel is `canvas.scaleFactor` (visual-design §2a agrees).
- **Do not use `Image.Tiled` for large overlays.** UGUI's tiled generator caps the tile count per Image (it enlarges tiles past a vertex limit; **verify** the exact cap in the ugui 2.0 source). A 16px halftone over a full 3840x2160 defense board would exceed it and silently rescale the dots (moire). Build overlays as **`RawImage`** with the Repeat-wrapped texture and set `uvRect = (0, 0, rect.width × scaleFactor / tex.width, rect.height × scaleFactor / tex.height)`: four vertices at any size.
- `ScreenPixelTile` therefore supports both: `Image` (sets `pixelsPerUnitMultiplier`, used for the small hatch inside buttons) and `RawImage` (sets `uvRect`, used for grain, halftone and the defense dot screen).
- **No per-frame work.** Recompute only when the inputs change: register in a static set and recheck from one `Canvas.willRenderCanvases` handler that compares the cached screen size and `scaleFactor` (the pattern `EditorialFrame.RedrawOnResize` already uses), plus `OnRectTransformDimensionsChange` for the element's own size. Uniform resolution changes (1080p to 1440p) keep the same reference rect but change `scaleFactor`, so the rect callback alone is not enough. Write `uvRect`/the multiplier only when the value differs (each write dirties the mesh).

**Hard offset shadow (a tinted copy of the sprite).**
- Build it as a **sibling `Image`** placed before the face (`CutSheetShadow`, created by the frame's sprite mode): same sprite, same `type`, `fillCenter`, `pixelsPerUnitMultiplier`, colour `Theme.inkPrint`, `raycastTarget = false`, offset `(d, -d)`.
- The shadow stays put and the **face moves**: hover moves the face (-2, +2) so the shadow reads +2; pressed moves the face (+4, -4) so the shadow reads 0. This gives the press recipe without rebuilding any mesh; only the face's `anchoredPosition` changes.
- **Don't use `UnityEngine.UI.Shadow`** for these. It is a `BaseMeshEffect` that moves with the graphic, so the press needs `effectDistance` changes and a mesh rebuild per tween frame, and it doubles a tiled Image's vertex count.
- Item stickers and cards: the shadow is the icon or card sprite tinted ink (alpha shape), as a sibling under the untilted root.
- Disabled = shadow `enabled = false` (not alpha 0), so no draw cost.

**`EditorialFrame` sprite mode.** Add an opt-in mode (sprite, `Image.Type`, tint, shadow depth, tilt) that makes the frame drive a child `Image` (face) plus a sibling shadow instead of drawing its own mesh. The flat mode stays the default, so legacy screens and existing tests (`FrameHairlinesStayOneScreenPixelBelow1080p`) are untouched. The halftone material (`defaultMaterial` override) applies only in flat mode; sprite-mode Images use the default UI material so they batch with the atlas.

**Allocation rules for all new HUD code.**
- Tweens run in `LateUpdate` on cached fields with `Time.unscaledDeltaTime` (pause sets `timeScale = 0`; trigger playback freezes because the presenter is gated, not because tweens use scaled time). No coroutines per hover, no LINQ, no `GetComponentsInChildren` per frame, no closures per frame.
- Strings: set `TMP_Text.text` only when the value changes. Cache the last int and use `TMP_Text.SetText("{0}/{1}", a, b)` (the non-allocating overload) for counters.
- Colour or sprite writes only on change (`Graphic.color` setters dirty the vertices even when equal in some paths; compare first).

### 1.3 TextMeshPro font assets

**Zilla Slab (Medium, SemiBold) and Courier Prime (Bold).** Generate them from a deterministic editor menu (`Assets/Editor/CutSheetFontAuthoring.cs`), not the Font Asset Creator by hand:
- `TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic)`, then `TryAddCharacters(set)` with ASCII 32-126 plus `• × ' ' " " – —`, then switch `atlasPopulationMode` to Static.
- Save the atlas texture and material as sub-assets (`AssetDatabase.AddObjectToAsset`) and clear `sourceFontFile` only if the test allows it. `DealbreakUIOverhaulTests.ThemeHasLicensedFontSourcesPrintAssetsAndSuitFallbacks` asserts `sourceFontFile != null`, so **keep the source file reference**.
- **Verify** the exact `CreateFontAsset` overload and `GlyphRenderMode` enum location in ugui 2.0 before writing (TMP's API moved into ugui in Unity 6; our reference docs don't list it).
- **Static, not dynamic.** `git status` already shows all five existing font assets dirty: dynamic atlases rewrite themselves when the Editor renders a new glyph. Static assets for the new fonts avoid that churn. The existing dirty font assets are unrelated tool churn; don't commit them with these slices.
- **Fallback chain** (`fallbackFontAssetTable`): text font, then `Dealbreak Symbols`, then `LiberationSans SDF - Fallback`. The overhaul test requires ♥♦♣♠ through fallbacks on heading, body and numeric fonts; Zilla Slab and Courier Prime don't carry the suits, so the LiberationSans fallback must stay in their chain.
- **Check** that Courier Prime Bold has `•` (U+2022) and `×` (U+00D7). If not, they resolve through the fallback in a different face; then add them to the symbols font.
- **Outline materials.** `DealbreakUIStyle.Outline` writes `text.fontMaterial`, which instantiates a material per label. For the HP number (paper outline) and the Symbols Board/Stock looks, author **material presets** (`Courier Prime UI - Paper Outline.mat`, `Dealbreak Symbols - Board.mat`, `Dealbreak Symbols - Stock.mat`) and assign `fontSharedMaterial`. One material per look batches, and nothing allocates at runtime.

**The glyph TTF (`DealbreakSymbols.ttf`).**
- `Tools/PrintArt/make_symbols_font.py` builds it with `fontTools.fontBuilder.FontBuilder`: a `glyf` (TrueType) outline font, converting the SVG cubic paths to quadratics (`fontTools.cu2qu`), units-per-em scaled from the 48-unit grid, glyphs at the private-use codepoints U+E000 to U+E006, plus `.notdef` and space. It must also emit `ShieldGlyph` points (a generated C# array) from the same shield path.
- Then regenerate `Dealbreak Symbols.asset` as a **static** SDF (1024, point size 90, padding 9, SDFAA) holding only those seven codepoints, keeping the asset's GUID (rebuild in place through the authoring script; don't delete and recreate, which would break every scene reference).
- Today the asset is built from `LiberationSans.ttf` by `DealbreakUIOverhaulAuthoring.cs:20`, and `DealbreakUIStyle` writes `▼` with `Theme.symbolFont` on dropdowns. **Check every glyph the current symbols font serves** (`▼`, suits, any others found by grepping `symbolFont`) and map them before swapping the source, or they fall to tofu.

### 1.4 Gotchas

- **Canvas Expand scaler.** Every HUD canvas uses `ScaleWithScreenSize` + `Expand` at 1920x1080 (`UiQaLayoutTests.EveryCanvasOnlyGrowsPast1080p`). The reference rect only grows: on 16:10 it is 1920x1200, on 21:9 about 2520x1080. Fractional anchors stretch with it; `CanvasPin` boxes don't. New elements that must keep pixel size (tags, chip, slots) use fixed `sizeDelta` under an anchor point, not anchor ranges. Below 1080p `scaleFactor < 1`: `EditorialFrame` already thickens hairlines; sprite borders drawn at 2-3px stay visible, but the 18px text floor renders at 12 physical px at 720p (hud.md OQ10).
- **21:9 cap.** hud.md says the play area is capped at 16:9 and centred. **Verify** whether this exists in code; no capping component was found during planning. If it doesn't exist, it is new work and needs its own slice (not in this plan's list; flagged in Concerns).
- **`CanvasPin`** rewrites anchors in `LateUpdate` whenever the root or parent rect changes. Never animate a pinned element's own RectTransform (the pin would snap it back on the next resize, and its anchors are recomputed). Animate a **child** (face) instead. The header slots and the Gear rail are pinned (`HeaderRowsArePinnedAndSixTrickSlotsExist`).
- **Sorting orders.** Known override canvases: attack presentation 60/65/66, enemy drop 70, trigger presenter 70, item drag 80, vignette 85, **tooltip 90** (`ContextualTooltipUI.SortingOrder`), **`FrontendOverlayCanvas` 100** in `Game.unity` (holds `PausePanel`; **verify** that pause draws from that canvas), loading and save prompt 1000. Rules: don't add a nested canvas per element (it breaks batching and creates new sort orders); shadows, overlays and faces live in the element's own canvas. The drag ghost (80) must stay under the tooltip (90), and the tooltip under pause (100) (acceptance criterion).
- **Raycast targets.** Every shadow copy, face, overlay (`RawImage` grain, halftone, hatch), crop mark, cut-line ring, focus ring and tilted child sets `raycastTarget = false`. Only the untilted root keeps a transparent `Image` as `Button.targetGraphic` (`EditorialButton` already forces its colour clear). Otherwise a shadow offset 6px down-right steals clicks from the neighbour, and a tilted face's corners poke outside the hit rect. A test should assert it (slice 4).
- **Tilt is a visual-only child.** Root = hit area, `Button`, `CanvasPin`, `ContextualInfoTrigger`, tooltip anchor, drop target rect; child `Face` carries `localRotation` from a **fixed per-slot table** (no RNG: cosmetic angles from `Random` would break no gameplay stream but would make captures non-reproducible; a table also satisfies "never derive order from instance IDs"). The tooltip notch aims at the untilted root rect. `RectMask2D` doesn't clip rotated children correctly; don't put tilted faces under one.
- **`GetComponent ?? AddComponent`** fails in the Editor (fake null), and so does `GetComponent<T>()?.field`: the fake-null object is a real managed instance, so `?.` doesn't short-circuit and the field reads its default. Use `TryGetComponent`. Live instances exist today (see Concerns).
- **Stencil mask for INCOMING's halftone offset.** The offset halftone is clipped to the tag's shape: a `Mask` (stencil) on a tag-shaped `Image` offset 4px (8px in defense) with `showMaskGraphic = false`, holding the `RawImage` halftone. One instance on screen; the stencil costs two extra draw calls, which is acceptable. Don't use `RectMask2D` (rectangles only; the notches would show).
- **Scene edits.** All `Game.unity` changes go through the authoring scripts run via `EditorSceneManager` (open `Game.unity` in the Editor, run the menu, save) or verified YAML edits; **never enter Play mode with `Game.unity` open**. Captures use `MainMenu.unity` and `GameFlowService.Instance.StartDevRun(seed, false, false, false)`.

---

## 2. Implementation slices

**Gate after every slice:** `unity_recompile`, then full EditMode (`Dealbreak.EditModeTests`, started via `UnityTestApi.Start` from `unity_execute`, then polled), then PlayMode `groups: ["!TwelveDistricts"]`, then the 12-District soak separately (read `TestResults.xml` after `Application.isPlaying` is false). Baseline: EditMode 1882 + 1 skipped, PlayMode 10/10 + soak. Use the `unity-tester` agent for the runs and `invariant-reviewer` before each commit. One commit per slice.

Authoring-script rule: any element whose anchors or size change in `Game.unity` changes in the script that owns it, in the same commit. Owners found: `DealbreakCentralStageAuthoring` (stage, enemies, status band, INCOMING, action bar), `DealbreakUIOverhaulAuthoring` (PlayerArea, HandFrame, fonts), `DealbreakNeoPunkAuthoring` (runs `DealbreakUiQaAuthoring` last: header pins, Trick slots), `DealbreakUIArtCorrectionAuthoring`. Runtime placers also own positions: `DealbreakUIRuntime` (HP panel, sort buttons, hand count), `CombatDetailPanelUI` (HP panel top), `CreditsHudUI`, `PlayerAvatarUI`, `EnemyDropUI`, `ItemDragUI` (DISCARD).

### Step 1 of option C: additive, no visual change

**S1. Palette sampling tools.** Add the eyedropper and contrast scripts from visual-design §0, and run them; record the result in `visual-design.md` §0 (accepted hexes or adopted samples).
- Files: `Tools/PrintArt/sample_palette.py` (new), `Tools/PrintArt/contrast_check.py` (new), `Tools/PrintArt/requirements.txt`, `production/ui/hud/visual-design.md` (results table). **4 files.**
- Scene/prefab: none. Tests: none (Python only; the gate still runs to keep one rule).
- Risk: low. A moved hex changes every later contrast figure.

**S2. Cut Sheet sprites and the coin.** Generator plus the 9 sprites (8 approved + `ui_slot_dashed_9s`) and `Credits_Coin.png`, imported by script, packed into `CutSheetUI`.
- Files: `Tools/PrintArt/make_ui_sprites.py` (new); `Assets/Art/UI/CutSheet/` 9 PNG + 9 `.meta` (new); `Assets/Art/UI/Sprites/Credits_Coin.png` + `.meta` (new); `Assets/Art/UI/CutSheet/CutSheetUI.spriteatlasv2` + `.meta` (new; extension **verify**); `Assets/Editor/CutSheetAssetAuthoring.cs` (new: import settings, borders, atlas); `Assets/Tests/EditMode/CutSheetAssetsTests.cs` (new). **About 25 files** (5 authored by hand, the rest generated).
- Scene/prefab: none. Nothing references the sprites yet.
- Tests (new `CutSheetAssetsTests`): every sprite exists with the §2a border, `FullRect`, PPU 100, no mipmaps, uncompressed; overlays Repeat; atlas packs 1-5 + dashed with tight packing off; generator output is byte-stable (hash pinned).
- Risk: low. Atlas V1/V2 API mismatch (compile-time only).

**S3a. Text fonts (Zilla Slab, Courier Prime).** Needs the downloads in section 3.
- Files: `Assets/Art/Fonts/ZillaSlab-Medium.ttf`, `ZillaSlab-SemiBold.ttf`, `CourierPrime-Bold.ttf` (+ `.meta`), `ZillaSlab-OFL.txt`, `CourierPrime-OFL.txt` (+ `.meta`); `Zilla Slab UI.asset`, `Zilla Slab SemiBold UI.asset`, `Courier Prime UI.asset` (+ `.meta`); material preset `Courier Prime UI - Paper Outline.mat` (+ `.meta`); `Assets/Editor/CutSheetFontAuthoring.cs` (new). **About 23 files.**
- Scene/prefab: none (the theme doesn't point at them yet).
- Tests: extend `CutSheetAssetsTests`: each new asset is static, has its source file and an atlas, carries the character set, and reaches ♥♦♣♠, `•` and `×` through its fallback chain.
- Risk: low; `CreateFontAsset` signature drift in ugui 2.0.

**S3b. Symbols font from the hand-drawn glyphs.** Blocked on the seven SVGs (hand art, about half a day; visual-design §2c).
- Files: `Tools/PrintArt/symbols/*.svg` (7, new), `Tools/PrintArt/make_symbols_font.py` (new), `Tools/PrintArt/requirements.txt`, `Assets/Art/Fonts/DealbreakSymbols.ttf` + `.meta` (new), `Assets/Art/Fonts/Dealbreak Symbols.asset` (rebuilt in place, GUID kept), `Dealbreak Symbols - Board.mat`, `Dealbreak Symbols - Stock.mat` (+ `.meta`), `Assets/Scripts/UI/ShieldGlyph.cs` (generated points), `Assets/Editor/DealbreakUIOverhaulAuthoring.cs` (stop building symbols from LiberationSans), `Assets/Editor/CutSheetFontAuthoring.cs`. **About 21 files.**
- Scene/prefab: none directly; the font asset's GUID is unchanged.
- Tests: `CutSheetAssetsTests`: U+E000-E006 present; every glyph previously served by the symbols font (`▼` and any others found) still resolves. `DealbreakUIOverhaulTests` must stay green.
- Risk: **medium**: anything that relied on LiberationSans glyphs inside the symbols asset turns to tofu on legacy screens. Visible change is limited to the enemy Shield glyph shape (`ShieldGlyph`), so this is the one step-1 slice with a small visual change; call it out in the commit.
- **Needs a human:** the glyph art, and a look at the glyphs at 24/32/48px.

**S4. Theme fields, metrics and sprite-mode plumbing.** Nothing reads them yet in combat.
- Files: `Assets/Scripts/UI/DealbreakUITheme.cs` (new fields `inkPrint`, `kraft`, `mustard`, `muted`, `boardDefense`, sprite and overlay refs, `slotDashedSprite`, new font refs `bodyFontCutSheet`/`numericFontCutSheet` are **not** added: the swap in S6 changes the existing fields); `Assets/Resources/DealbreakUITheme.asset` (values and sprite refs); `Assets/Scripts/UI/CutSheetMetrics.cs` (new: shadow depths 6/4/3/0, card 12/8, lift 24, tag/tooltip timings from hud.md); `Assets/Scripts/UI/CutSheetMotion.cs` (new: a static read-only `ReduceMotion`/`ShakeEnabled`/`PlaybackSpeed` facade defaulting to off/on/1, so zone slices can honour Reduce Motion before S17 wires the setting); `Assets/Scripts/UI/EditorialFrame.cs` (sprite mode, off by default); `Assets/Scripts/UI/CutSheetShadow.cs` (new); `Assets/Scripts/UI/ScreenPixelTile.cs` (new); `Assets/Tests/EditMode/CutSheetAssetsTests.cs`; `Assets/Tests/EditMode/CutSheetFrameTests.cs` (new). **9 files.**
- Scene/prefab: none.
- Tests (new `CutSheetFrameTests`): sprite mode builds a face and one shadow; shadow and overlays are not raycast targets; pressing moves the face, not the shadow; `ScreenPixelTile` yields one texel per screen pixel at scale factors 1, 1.333, 2; toggling sprite mode twice leaves one shadow. Existing `UiQaLayoutTests.FrameHairlinesStayOneScreenPixelBelow1080p` stays green (flat mode unchanged).
- Risk: low. Watch the serialized field order on the theme (append fields; don't reorder).

### Step 2: audit (still no visible change)

**S5. Fake-null fixes in the UI layer.** A separate problem, so a separate commit, done before the audit touches the same files.
- Files: `Assets/Scripts/UI/DealbreakUIRuntime.cs` (`BindTricks`, `BindControls`: `GetComponent<ContextualInfoTrigger>() ?? AddComponent`), `Assets/Scripts/UI/EditorialButton.cs` (`Refresh` reads `GetComponent<EditorialSurface>()?.treatment` every frame; cache with `TryGetComponent`), `Assets/Scripts/UI/DealbreakUIStyle.cs` (`Button`: same `?.treatment` pattern three times). Grep the rest of `Assets/Scripts/UI` for `?? .*AddComponent` and `GetComponent<.*>()?\.` and fix what it finds (**verify** count). Test: `Assets/Tests/EditMode/EditorialButtonSurfaceTests.cs` (new): a button with no `EditorialSurface` is not treated as Open in the Editor. **4+ files.**
- Scene/prefab: none.
- Risk: **medium**. In the Editor, buttons without a surface have been styled as Open (enum value 0); fixing it can change their Editor look. Builds were already correct, so the fix makes the Editor match a build. Capture a quick before/after of the action bar.

**S6a. Theme usage audit.** Classify every `Theme.ochre`, `Theme.ink`, `Theme.charcoal` read (149 reads in 30 files per visual-design §1c). Retarget Credits reads to `mustard`, type-on-stock, border and shadow reads to `inkPrint`. Values are unchanged (the new fields hold the old values until S6b), so nothing moves on screen.
- Files: up to 30 under `Assets/Scripts/UI` and `Assets/Scripts/Frontend` (heaviest: `EditorialButton`, `DealbreakUIStyle`, `ItemDragUI`, `TutorialCoachUI`, `ShopOfferUI`, `EffectTriggerPresenterUI`, `EnemyDisplayUI`); `ActionButtonsUI.cs` (remove the dead green/blue `targetGraphic` colours); `Assets/Tests/EditMode/ThemeUsageAuditTests.cs` (new). Plus the classification table appended to this doc. **About 30 files** (exact list produced by the audit; the owner approves it before the commit).
- Rule for S6a only: `inkPrint` and `mustard` are temporarily set equal to today's `ink` and `ochre` in the asset, so the commit is provably invisible.
- Tests (`ThemeUsageAuditTests`): a source scan that fails if `Theme.ochre` appears in Credits code paths (`CreditsHudUI`, `ShopOfferUI` price, `RunEventUI` costs) or if `Theme.ink` is used as a text colour on `paper` fills in `DealbreakUIStyle`/`EditorialButton`.
- Risk: medium (wide diff, low behaviour). Misclassified reads surface only in S6b.

### Step 3: the bounded swap

**S6b. Value and font swap.** Change the six legacy values (§1a) and the new fields to their real values, point `bodyFont` at Zilla Slab and `numericFont` at Courier Prime, update the code defaults in `DealbreakUITheme.cs`, and stop combat calling `PrintFill`.
- Files: `Assets/Resources/DealbreakUITheme.asset`, `Assets/Scripts/UI/DealbreakUITheme.cs`, `Assets/Scripts/UI/DealbreakUIStyle.cs` (`Outline` colour parameter, default black kept), `Assets/Editor/DealbreakUIOverhaulAuthoring.cs` (it assigns the fonts: **verify** and update so a rerun doesn't revert the swap), `Assets/Tests/EditMode/GameSceneReadabilityTests.cs` (only if a fit fails and the fix is a rect change), `Assets/Tests/EditMode/DealbreakUIOverhaulTests.cs` (theme font expectations). **4-6 files.**
- Scene/prefab: none expected. If a label overflows, its fix is in the owning authoring script plus the scene.
- Tests: `GameSceneReadabilityTests` (`ImportantLabelsAndPrimaryButtonsFitTheir1920ReferenceRects` will catch wider Courier/Zilla metrics), `DealbreakUIOverhaulTests` (suits through fallbacks), `RunEventUILayoutTests`, `Phase7EShopSelectionTests` (**verify** they don't pin colours).
- Risk: **high (visual)**: every screen changes palette and type at once. Overflow on the Black Market, map, Incident and menus.
- **Needs a human:** before/after captures of the four non-combat screens and combat at 1920x1080 and 2560x1440.

### Step 4: combat reskin, one zone per slice

All of S7-S12 use the S4 sprite mode, `CutSheetMetrics`, and `CutSheetMotion.ReduceMotion` for tilt, slap and hop. Each **needs a human**: a dev-run capture of its zone at 1920x1080 (and 16:10 for S8-S11), compared against hud.md section C and visual-design §3b.

**S7. Header inventory (Gear and Trick rows).** Heading strips (tag sprite paper / kraft), sticker slots with cut-line ring on hover and focus, dashed empty slots, tier and stack discs, fixed tilt table (at most ±1°), slap and 220ms hop.
- Files: `Assets/Scripts/UI/InventoryPanelUI.cs`, `GearRailUI.cs`, `GearIconSlotUI.cs`, `ItemIconUI.cs`, `ItemTierFxUI.cs` (**verify** it owns the tier mark), `DealbreakUIRuntime.cs` (`InstallInventoryPanels`, `trickCapacity` text: stop allocating each frame), `ActionButtonsUI.cs` (`RefreshBackpackSlots`: stack and charges on an untilted parent), `Assets/Scripts/UI/CutSheetTiltTable.cs` (new), tests `Phase7EGearHudTests.cs`, `GearRailUITests.cs`, `UiQaLayoutTests.cs` (slots still 100x100 and pinned), new `HeaderInventoryCutSheetTests.cs`. **About 12 files.**
- Scene/prefab: none if the pinned boxes are kept (they are). If the heading strip needs a scene object, add it in `DealbreakUiQaAuthoring` and rerun `DealbreakNeoPunkAuthoring`.
- Tests: empty slots show the dashed sprite for `BackpackCapacity` slots only; tilt lives on a child and the root rect is unchanged; Trick charges are on the slot only (duplicate cut 6 is S14).
- Risk: medium: `ItemDragUI` reads slot rects as drop targets; they must stay the untilted roots.

**S8. Enemies and INCOMING.** Raise the enemy row about 20px (to y about .49-.846); portrait sticker shadow; enemy HP meter (ink fill, paper track, 2px gap); ATTACK red tag; enemy Shield muted + hatch with outlined glyphs; intent line; crop marks (thin/thick, teal strip), shown only while two or more enemies stand; INCOMING as one red notched tag with cream edge and masked halftone offset (doubled in defense), Anton/Barlow type, counts to 0 and goes flat on ALL BLOCKED; 120ms 6px shake respects the shake setting.
- Files: `Assets/Scripts/UI/EnemyDisplayUI.cs`, `EnemyGroupUI.cs`, `HealthBarBacking.cs`, `ShieldGlyph.cs`, `ScreenShakeUI.cs` (read `CutSheetMotion`), `ActionButtonsUI.cs` (the `pendingDamageLabel` block: becomes the INCOMING tag view), new `Assets/Scripts/UI/IncomingTagUI.cs`, `Assets/Scripts/UI/CropMarksUI.cs` (new; reused by drop targets in S11), `DealbreakUIStyle.cs` (`ApplyHierarchy` enemy text rules), `Assets/Editor/DealbreakCentralStageAuthoring.cs` (enemy row and INCOMING anchors), `Assets/Scenes/Game.unity`; tests: `DirectHandTargetingUITests.cs`, new `EnemyRowCutSheetTests.cs`, new `HudLayoutTests.cs` (section C anchors ±0.01, starting with the enemy row and INCOMING). **About 14 files.**
- Scene: yes, via `DealbreakCentralStageAuthoring` run through `EditorSceneManager` on `Game.unity`, then saved.
- Tests: one INCOMING tag with three enemies and two retaliating, equal to `IncomingDamageAfterModifiers`; ALL BLOCKED goes flat; marks hidden with one enemy; no VALID/INVALID text.
- Risk: **medium-high**: `AttackCardPresentationUI` and the drop and Credits flights aim at enemy rects; a row move shifts their targets (check in capture). The enemy container's right edge must stay at .871 (draw pile clearance).

**S9. Status band.** Status line as a paper notched tag (no tilt), Barlow 22 ink, Courier numbers; the selection preview and auto-Recover notice render inside it; 250ms board-tone crossfade and the defense dot screen (RawImage, `ScreenPixelTile`), instant under Reduce Motion; status band anchors below INCOMING.
- Files: `Assets/Scripts/UI/DealbreakUIRuntime.cs` (`MergeStatusLines`, `ShowOneStatusLine`), `BoardPanelTransition.cs`, `ActionButtonsUI.cs` (turn state text), new `Assets/Scripts/UI/StatusTagUI.cs`, `Assets/Editor/DealbreakCentralStageAuthoring.cs`, `Assets/Scenes/Game.unity`, tests `GameSceneReadabilityTests.cs` (`Canvas/PlayerArea/SelectionLabel` path and size floor 16 becomes 22; **verify** the label's new path), `HudLayoutTests.cs`. **About 8 files.**
- Scene: yes, via `DealbreakCentralStageAuthoring`.
- Risk: medium. The selection label is registered as a tooltip obstacle; keep the registration on the new tag rect.

**S10. Hand and actions.** HP panel moves down to x .025-.235, y .014-.082 with the Block/Shield line above it; Credits chip (mustard coin disc + mustard plate, 210x46) and portrait restack above it; action bar re-centred under the hand (x .25-.69, centre .47) with sort buttons inside its left 18% (latched-down active sort, 3px shadow); ATTACK red fill, BLOCK and PASS paper, Anton 32, press recipe, hatch when unavailable; HAND n/8 in Barlow/Courier, no shadow; hand card shadows (8/20px), 24px selection lift with crop marks, "can't block" 70% + hatch.
- Files: `Assets/Scripts/UI/DealbreakUIRuntime.cs` (`Place(playerState, ...)` in `LateUpdate`, `PositionSortControls`, `AttachHandCount`, per-frame `handCount.text` allocation), `CombatDetailPanelUI.cs` (remove its per-frame `playerStatePanel.anchorMax.y = .21` write: **one owner** for the HP panel's anchors), `CreditsHudUI.cs`, `PlayerAvatarUI.cs`, `ActionButtonsUI.cs` (`RefreshPlayerHealthLabel`, Block/Shield line), `EditorialButton.cs` (sprite-mode press, hover, disabled recipes; roles kept), `DealbreakUIStyle.cs` (`Button`/`RoleFor`, HP number paper outline via the preset material), `HealthBarBacking.cs` (ticks every 5 HP, 10 below 6px spacing), `Assets/Scripts/Cards/CardView.cs` and the hand layout (**verify** file: lift and shadow), `Assets/Editor/DealbreakCentralStageAuthoring.cs` (action bar), `Assets/Editor/DealbreakUIOverhaulAuthoring.cs` (PlayerArea, HealthPanel, HandFrame), `Assets/Scenes/Game.unity`; tests `CreditsHudTests.cs` (chip structure: disc + plate, coin = `Credits_Coin`, mustard), `GameSceneReadabilityTests.cs` (hit targets: PASS 60x56, ATTACK 200x56, BLOCK 180x56 must still hold in the narrower bar; **check the arithmetic: .44 of 1920 = 845px for sort + three buttons**), `PassPlayerTurnUITests.cs`, `CombatUISliceTests.cs`, `HudLayoutTests.cs` (HP, chip, portrait, bar centre .47, 29px clearance, 24px edge margin). **About 17 files.**
- Scene: yes, via `DealbreakCentralStageAuthoring` and `DealbreakUIOverhaulAuthoring` through `EditorSceneManager`.
- Risk: **high**: the widest slice; the compact-hand layout (`CompactHandMin/Max`, `_compactHealthLayout`) reuses the same HP panel on map, market and Incident screens, so the move must keep the compact branch. Consider splitting into S10a (HP, chip, portrait), S10b (action bar and sort), S10c (cards) if the diff exceeds one review.

**S11. Right column.** MAP as a paper button (Barlow + glyph), draw pile (2/4/6 stacked shadows, Courier count disc), Contract line (Zilla 20 with a 2px underline affordance, right-aligned above the pile), DISCARD strip (red, Anton 32 rotated 90°, x .895-.985, y .55-.83, slides in 110ms) and drop-target crop marks with thick + lift on hover.
- Files: `Assets/Scripts/UI/DrawDeckStackUI.cs`, `RunStatusUI.cs` (Contract line text and style), `ItemDragUI.cs` (DISCARD rect and marks), `ItemDropRules.cs` (**read-only**: marks read it; no change expected), `CropMarksUI.cs`, `PathScreenUI.cs` or the MAP button owner (**verify**), `Assets/Editor/DealbreakCentralStageAuthoring.cs` / `DealbreakUiQaAuthoring.cs` (MAP, pile, Contract line; **verify** owner), `Assets/Scenes/Game.unity`; tests `DrawDeckStackUITests.cs`, `HudLayoutTests.cs` (DISCARD clears MAP and Contract line), `UiQaLayoutTests.cs`. **About 10 files.**
- Scene: yes, via the owning authoring script.
- Risk: medium. MAP's -134px offset on 16:10 (hud.md platform table) must still clear the header.

**S12. Contextual tooltip.** Paper panel (Tiled, grain), 3px ink border, 6px shadow, notch half-diamond under the panel aimed at the source, Zilla 20 ink, no heading when the source names itself, 360px max, 16px padding, open 110ms (6px slide, shadow 0 to 6) and close 70ms, instant under Reduce Motion. A text-scale hook (`ContextualTooltipUI.TextScale`, default 1) is added here for S20. Sorting order stays 90.
- Files: `Assets/Scripts/UI/ContextualTooltipUI.cs`, `ContextualInfoTrigger.cs` (**verify** whether it carries the heading), `DealbreakUIStyle.cs` (`ApplyHierarchy` tooltip block: Anton 27 title and body 19 become Barlow 22 / Zilla 20), the tooltip's scene object via its authoring script (**verify**: `DealbreakUiQaAuthoring` or runtime-built), `Assets/Scenes/Game.unity` and `MainMenu.unity` if authored there; tests `UiQaLayoutTests.DetailScrollbarOnlyShowsWhenTheDetailOverflows`, new `ContextualTooltipCutSheetTests.cs`. **About 6-7 files.**
- Risk: medium: the tooltip is shared by every screen, so it is the first Cut Sheet element on legacy screens.
- **Needs a human:** captures of the tooltip on combat and on the Black Market, map and Incident backgrounds.

### Step 5: behaviour changes from the spec

**S13. One selection-preview builder.** `CombatDetailPanelUI.SelectionSummary` becomes the only builder and takes over all forms from `ActionButtonsUI` (Ace pair, same rank ×n, Royal Flush m/5 and instant defeat, Nuclear Option group, multi-hit and capped totals, "Blocks b of n • r damage left", "Can't block with this selection", "Recover: take n, draw 1"). Its attack numbers come from `PreviewActionDamage`/`PreviewHitCount`/`PreviewActionTotalDamage` (today's `SelectionSummary` sums `CalculateCardAttackDamage` instead, which disagrees: see Concerns). One writer sets `selectionLabel.text` (see Concerns for which); the two other writers are removed. ATTACK keeps a plain "ATTACK" / "CANCEL" label from one writer (today three places write it, with "Cancel" and "CANCEL"). Done in S13: `ActionButtonsUI.RefreshAll` is the one writer of both; Concerns 1-3 resolved.
- Files: `Assets/Scripts/UI/CombatDetailPanelUI.cs`, `ActionButtonsUI.cs`, `DealbreakUIRuntime.cs`; tests `ActionGuidanceCopyTests.cs` (it drives `ActionButtonsUI.RefreshAll`; update to the chosen writer), `CombatUISliceTests.cs` (`SelectionSummary` signature, no RNG or zone change), `ActionDamagePreviewTests.cs` (preview text equals execution), new `SelectionPreviewBuilderTests.cs` (every form; the label is identical whichever component refreshes first). **7 files.**
- Scene: none. Risk: medium: the label text and the ATTACK label are what players read; previews must never consume RNG (`CombatUISliceTests` already pins it).

**S14. The seven duplicate cuts.** (1) the preview is not copied into card detail; (2) card detail has no heading; (3) enemy detail drops Shield, Double Strike/charged and "Headliner reward"; (4) no VALID/INVALID TARGET text anywhere; (5) HAND n/cap not in the draw-pile detail; (6) Trick stack and charges not in Trick detail (`BindTricks`); (7) `ownedGearLabel` never shows.
- Files: `Assets/Scripts/UI/CombatDetailPanelUI.cs` (`RenderSource`), `EnemyDisplayUI.cs` (`DetailGuidance`), `DealbreakUIRuntime.cs` (`BindTricks`), `GearRailUI.cs` or `RunStatusUI.cs` (`ownedGearLabel` owner, **verify**), any VALID/INVALID TARGET writer (**verify** by grep); tests `CombatUISliceTests.cs` (it asserts "ATTACK POWER"/"BLOCK VALUE" stay), `UiQaLayoutTests.cs` ("no repeated facts" header comment suggests existing checks; **verify**), new `HudDuplicateFactsTests.cs`. **About 8 files.**
- Scene: none (unless `ownedGearLabel` is a scene object to disable; then via its authoring script). Risk: low-medium. Cut 5 depends on hud.md OQ7 (draw-pile detail vs `DeckViewerUI`).

**S15. Contract detail and its reward accessor.** Add a read-only `RunManager` accessor for the active Contract's reward text (from `Resources/QuestContract` and `GameBalance.Current` contracts, no state change, no RNG); a Contract-line click opens objective plus reward in the tooltip and no longer repeats the line.
- Files: `Assets/Scripts/Run/RunManager.Contracts.cs` (accessor; **verify** path), possibly `RunContractState.cs` (read-only formatter), `Assets/Scripts/UI/DealbreakUIRuntime.cs` (`BindControls` progress trigger), `RunStatusUI.cs`; tests new `ContractDetailTests.cs` (accessor is pure: calling it twice changes no state or RNG; text quotes the balance numbers), `BalanceTextDriftTests.cs` (if the reward text quotes a number). **About 5-6 files.**
- Scene: none. Risk: low; the accessor must not duplicate the reward calculation used at payout (one source).

**S16. Pending-drop position.** The drop token and LEAVE IT move to the top of the stage (y about .74-.82, centred); slap 120ms + 80ms, instant under Reduce Motion; LEAVE IT as a paper button.
- Files: `Assets/Scripts/UI/EnemyDropUI.cs` (position, slap, sticker ring), tests new `EnemyDropLayoutTests.cs` (anchors; shown only with `PendingEnemyDrop`; claim and leave still call `RunManager` actions). **2-3 files.**
- Scene: none (runtime-built on its own canvas, order 70). Risk: low. **Needs a human:** a capture with a full backpack after a fight.

**S17. Settings service and menu rows.** One persisted settings store under `display.*` keys for Reduce Motion (off), Screen shake (on), Trigger playback speed (1x/1.5x/2x) and Tooltip text size (100/115/130%); rows added to the settings menu; `CutSheetMotion` (S4) reads the store.
- Files: `Assets/Scripts/Frontend/DisplaySettingsService.cs` (or a new `MotionSettingsService.cs` beside it, **owner choice**), `SettingsMenuUI.cs`, `CutSheetMotion.cs`, the settings panel scene objects in `Game.unity` and `MainMenu.unity` (via the owning authoring script, **verify** which), new `MotionSettingsTests.cs` (defaults, persistence, Reduce Motion forces shake off). **About 6-7 files.**
- Risk: low-medium; menu copy must stay state-only (`ActionGuidanceCopyTests` style).

**S18. Reduce Motion and shake consumers.** Shake off; hops, slaps and tilt settle instantly; board jolt off; board-tone crossfade and dot-screen fade become cuts; tag order and `ImpactDelay` unchanged.
- Files: `ScreenShakeUI.cs`, `EffectTriggerPresenterUI.cs`, `BoardPanelTransition.cs`, `AttackCardPresentationUI.cs` (jolt only; `ImpactDelay` untouched), `EnemyDropUI.cs`, `CutSheetTiltTable.cs`; tests new `ReduceMotionTests.cs` (same tag order and same `ImpactDelay(hitIndex)` with the flag on and off). **About 7 files.**
- Risk: medium: timing code; any change to `ImpactDelay` would desync hits from HP.

**S19. Trigger playback speed.** Scales step, tag and chip timings (`StepTime`, `HealthCostStepTime`, `TagTime`, `ChipTime`, `ChipStagger`) by 1/speed, keeping order and the backlog speed-up (`MinStepScale`).
- Files: `EffectTriggerPresenterUI.cs`, `CreditsHudUI.cs` (coin timing, **verify** owner of `ChipTime`), tests extend `ReduceMotionTests.cs` or new `PlaybackSpeedTests.cs`. **3 files.**
- Risk: low-medium; the presenter gates commands, so faster playback must still release the gate exactly once (input-gate ownership rule).

**S20. Tooltip text size.** 100/115/130% scales the tooltip text and grows its max width; scroll on overflow.
- Files: `ContextualTooltipUI.cs`, tests `ContextualTooltipCutSheetTests.cs`. **2 files.**
- Risk: low. **Needs a human:** a capture at 130% on a long Gear description.

### Slice order and dependencies

S1 → S2 → S3a → (S3b whenever the art lands) → S4 → S5 → S6a → S6b → S7 … S12 → S13 … S20. S13 may be done before S9 if the owner prefers the status tag to be built against the final builder; S14 cut 5 waits on OQ7. S15-S16 are independent of the reskin and can move earlier.

---

## 3. External downloads (need owner permission before downloading)

| File | Source | Licence |
|---|---|---|
| `ZillaSlab-Medium.ttf` | https://raw.githubusercontent.com/google/fonts/main/ofl/zillaslab/ZillaSlab-Medium.ttf (specimen: https://fonts.google.com/specimen/Zilla+Slab) | SIL Open Font License 1.1 (Mozilla Foundation / Typotheque) |
| `ZillaSlab-SemiBold.ttf` | https://raw.githubusercontent.com/google/fonts/main/ofl/zillaslab/ZillaSlab-SemiBold.ttf | SIL OFL 1.1 |
| Zilla Slab licence text | https://raw.githubusercontent.com/google/fonts/main/ofl/zillaslab/OFL.txt → `Assets/Art/Fonts/ZillaSlab-OFL.txt` | — |
| `CourierPrime-Bold.ttf` | https://raw.githubusercontent.com/google/fonts/main/ofl/courierprime/CourierPrime-Bold.ttf (specimen: https://fonts.google.com/specimen/Courier+Prime) | SIL OFL 1.1 (Alan Dague-Greene / Quote-Unquote Apps) |
| Courier Prime licence text | https://raw.githubusercontent.com/google/fonts/main/ofl/courierprime/OFL.txt → `Assets/Art/Fonts/CourierPrime-OFL.txt` | — |

Notes:
- **Verify the URLs resolve** before downloading; the google/fonts repo paths were not fetched during planning. Download each into its own empty scratch folder, check it opens as a TrueType font, then copy it in.
- OFL 1.1 permits embedding and bundling with a commercial game; the licence text ships beside the font (the project already does this for Anton, Barlow, IBM Plex Mono and Space Grotesk). Reserved Font Names: don't ship a modified font under the original name (we don't modify them).
- **Python packages** for the generators (also downloads): `fontTools` (MIT) for the glyph font; `Pillow` and `numpy` are already in `Tools/PrintArt/requirements.txt` (**verify**). An SVG path parser: prefer `fontTools.svgLib` (part of fontTools) over adding `svgpathtools`.
- The glyph TTF itself needs no download: it is built from in-house SVGs.

---

## 4. Slices that need a human

| Slice | What needs eyes |
|---|---|
| S1 | Accept or adopt the sampled hexes (a judgement call if a hex is off by more than delta-E 6) |
| S3b | The seven glyph drawings (hand art), and a look at them at 24/32/48px |
| S5 | Before/after of the action bar in the Editor (the Editor styling changes to match builds) |
| S6b | Before/after of combat and the four non-combat screens at 1920x1080 and 2560x1440 (overflow, palette) |
| S7-S11 | A dev-run capture of each zone at 1920x1080 (S8-S11 also 16:10), checked against hud.md section C and visual-design §3b; the visual budget (≤30% chrome) is measured in S11's capture |
| S12 | Tooltip on combat and on the legacy screens' backgrounds |
| S16 | Pending drop with a full backpack |
| S18 | Reduce Motion on and off side by side |
| S20 | Tooltip at 130% on a long description |

Captures always come from `MainMenu.unity` → Play → `GameFlowService.Instance.StartDevRun(seed, false, false, false)`, never from `Game.unity` in Play mode.

---

## 5. Concerns found in code during planning

1. **Three writers of the selection preview, not two.** `ActionButtonsUI.RefreshAll`, `DealbreakUIRuntime.LateUpdate` and `CombatDetailPanelUI.LateUpdate` all write `selectionLabel.text`, and all three write the ATTACK label ("Cancel" in one, "CANCEL" in two; `ActionButtonsUI` also writes "ATTACK ACE PAIR" etc.). The visible text depends on script execution order.
2. **The two builders compute different numbers.** `CombatDetailPanelUI.SelectionSummary` sums `CalculateCardAttackDamage` per card; `ActionButtonsUI.DamagePreview` uses `PreviewActionDamage`/`PreviewHitCount`/`PreviewActionTotalDamage`. hud.md names `SelectionSummary` as the builder, but its body must adopt the `PreviewAction*` path, which is the one that shares execution's calculation (the combat invariant).
3. **Who writes the label after S13?** hud.md says `ActionButtonsUI` "stops writing preview text", but `ActionGuidanceCopyTests` drives `ActionButtonsUI.RefreshAll` to fill it. Recommendation: `ActionButtonsUI.RefreshAll` stays the single *writer* and calls `CombatDetailPanelUI.SelectionSummary` (the single *builder*); the other two writers are deleted. Owner or ux-designer to confirm.
4. **Fake-null traps in live UI code**: `GetComponent<ContextualInfoTrigger>() ?? AddComponent` in `DealbreakUIRuntime.BindTricks` and `BindControls`; `GetComponent<EditorialSurface>()?.treatment` in `EditorialButton.Refresh` (every frame) and `DealbreakUIStyle.Button`. In the Editor, a missing surface reads as `Open`. Fixed in S5.
5. **The HP panel has two runtime anchor writers**: `DealbreakUIRuntime.LateUpdate` (`Place(playerState, .025, .035, .235, .25)` on phase change) and `CombatDetailPanelUI.LateUpdate` (forces `anchorMax.y = .21` every frame), plus the compact layout. The HP move (S10) must leave one owner.
6. **Per-frame allocations today**: `handCount.text` and `trickCapacity.text` are formatted every `LateUpdate` in `DealbreakUIRuntime`; `CombatDetailPanelUI` calls `GetComponentsInChildren<GearIconSlotUI>()` per frame. Fixed where the zone slices touch them.
7. **Full-screen tiled overlays exceed UGUI's tile cap** at 4K; overlays must be `RawImage` with `uvRect` (§1.2), a small deviation from visual-design §2a's "Image Type Tiled".
8. **21:9 play-area cap** (hud.md, owner choice) has no implementation found and no slice here; needs its own slice or confirmation that it exists.
9. **No localization system** exists; every HUD string is a C# literal. These slices keep that (out of scope); flagging against the "all UI text through localization" rule.
10. **How-to copy in `BindControls`** ("Sort your current hand by rank.", "Pass the current action; ...") may breach the no-how-to ruling; ux-designer to confirm in S14.
11. **Open questions that block parts of slices**: OQ7 (draw-pile detail vs `DeckViewerUI`) blocks S14 cut 5; OQ9 (coin timing) affects S10/S19; OQ4 (pause button place) affects S11.
12. **Dirty font assets** in the working tree (dynamic atlas churn) are unrelated and must stay out of these commits.
13. **Action bar width**: .25-.69 is 845px at 1080p for the sort column (18%) plus ATTACK ≥200, BLOCK ≥180 and PASS ≥60 hit targets with gaps; it fits, but check the 16:10 and 1440p captures for Anton 32 labels.

---

## Appendix: S6a theme usage audit (owner-approved 2026-10-28)

The real count was 76 lines reading `ochre`, `ink` or `charcoal` (the 149 estimate counted expressions). In this slice `inkPrint` and `mustard` hold today's `ink` and `ochre` in the theme asset, so the retargets change nothing on screen; S6b sets the Cut Sheet values.

| Read | Classified as | Action |
|---|---|---|
| Credits trigger tag (`EffectTriggerPresenterUI`, new `TagStyle.Credits`); Credits coin flight (`FlyChip`); Black Market sell price while dragging (`ItemDragUI` market label) | Credits | `ochre` → `mustard` |
| Paper-button label (`EditorialButton`, `DealbreakUIStyle.Button`); text on paper surfaces (`DealbreakUIStyle` tint); enemy name text (`DealbreakUIStyle`; `DealbreakUIOverhaulAuthoring` name, HP, status); tooltip body (authoring); ability callout label; drop-preview tag label; reroll label; tier tag text (`ItemTierFxUI`); Mod FX marks (`CardModFxUI`); trigger tag labels; Shield glyph outline; `frame.ink` (×4, a field the frame never draws) | type on stock, border, outline | `ink` → `inkPrint` |
| Selected character tile; active and selected button rim and label; toggle check; enemy targeting ring and outline; selected Gear slot; drag ghost rim, drop ring and drop tag on a valid target; map current, available and scouted nodes; selected Black Market good | selection or valid target | stays `ochre` (teal in S6b) |
| Loading backdrop; tutorial and sell dimming shades; art-correction panel and portrait mount; overhaul background; HP meter track; pressed toggle; pressed-button darkening; Cost tag fill; NeoPunk authoring values | stage | stays `ink` |
| All 13 `charcoal` reads | dark raised surface with paper text | unchanged (fine at both values) |
| Loading suits and bar; tutorial lesson label and highlight rim; other gain tags (+HP, DRAW, BLOCK, SHIELD) and heal chips; ability callout fill; enemy Trick drop rim; Epic tier FX; Mod FX; reroll fill; RUN VICTORY / ENCOUNTER HALTED title | decorative accent | stays `ochre`; **owner: restyle each in its own zone slice** (they show teal between S6b and that slice) |

Also removed: the green and blue `targetGraphic` colours on ATTACK and BLOCK in `ActionButtonsUI` (both are `EditorialButton`s, which paint the target graphic clear every `LateUpdate`). Pinned by `ThemeUsageAuditTests`.
