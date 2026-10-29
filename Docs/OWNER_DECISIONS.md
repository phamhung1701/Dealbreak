# Dealbreak — owner decisions and UI specs

The owner's rulings on how features look and behave, moved out of `CLAUDE.md` so they load only when needed. Read the section for the area you are touching before changing it. Dates are when the owner decided. Code, authored assets and passing tests remain the evidence; when this file and the code disagree, check with the owner before "fixing" either.

When the owner makes a new ruling, add it here under its area (with the date), not to `CLAUDE.md`.

## UX principles

- **No in-game guidance outside the tutorial.** Show state and numbers (damage and block previews, Recover cost, enemy intents, choice requirements), never how-to hints. `ActionGuidanceCopyTests` pins this. How-to copy lives only in the scripted tutorial run (`Assets/Scripts/Tutorial`, lessons in `TutorialScript`, content in `Resources/TutorialRun`); the first NEW RUN starts it until `TutorialProgress` is set, it never touches the run save, and its steps refuse off-script commands through `TutorialGate`.
- **No bloated UI: show each fact once.** Pending damage appears only under the enemies as "INCOMING n". (2026-10-24) The selection preview ("1 selected • Attack power 10", "Blocks 1 of 2 • 2 damage left") stands in for PLAYER TURN or DEFENSE on the one status line under the enemies (`DealbreakUIRuntime.MergeStatusLines`). A hand card's detail is only its ATK/DEF and Mod, with no heading (the card face names it). An enemy's detail adds only what the board does not show (no HP or attack); an enemy with nothing more opens none.
- **Info opens on click, next to the clicked object** (2026-10-21), never on hover and never in a fixed panel. A second click or a click anywhere else closes it, whatever opened it (a selected card or Gear included, 2026-10-23). A selected hand card or Gear shows its own; clicking an enemy targets it (a lime ring while two or more stand; Cut Sheet target 2026-10-27: thin teal crop marks on valid targets, thick marks plus a teal strip on the targeted one) and opens its info; a button whose click acts (PASS) shows its info on click only while unavailable. Keyboard focus also opens it. ATTACK and BLOCK have none; on the route map only a scouted node does.
- Item text (2026-10-22): each Gear and Trick reads as one short sentence, the workbook's "Ingame description".
- (2026-10-27, combat HUD spec `design/ux/hud.md`) Cut the facts the combat screen shows twice: one builder for the selection preview, which never repeats in card detail or the ATTACK label; no VALID/INVALID TARGET text; enemy detail drops Shield, Double Strike/charged and Headliner reward; HAND n/cap only by the hand; Trick charges only on the slot. Clicking the Contract line opens its objective and reward. INCOMING stays one total under the enemy row. The selection preview has one builder (`CombatDetailPanelUI.SelectionSummary`, using the execution calculation) and one writer (`ActionButtonsUI`). Clicking the draw pile opens its detail beside it, not the deck viewer panel.

## Combat and hand

- No Take Damage button; PASS skips the blocking phase. No Recover button (2026-10-23): on the player's turn with an empty hand, once the board settles, Recover runs on its own after a short beat (`ActionButtonsUI.TickAutoRecover` → `CombatManager.Recover`).
- Every played card (attack or Block) and every Incident discard goes straight back into the draw pile, which reshuffles. The discard pile no longer fills in play; Ace Protocol and Diamond Driver take their card from the draw pile.
- Destroying the last owned card loses the run at once (`CombatManager.ResolveDeckedOut`).
- Enemy drops go straight into the backpack when there is room. With the backpack full the drop waits on the board (`RunManager.PendingEnemyDrop`) and the run waits with it until it is dragged in or left (LEAVE IT).

## Items: Gear, Tricks, Mods

- Tricks and Gear are dragged like cards (`ItemDropRules`, `ItemDragUI`):
  - a card Trick onto a hand card (one card per drop, one charge each); damage onto an enemy; heal onto the HP bar;
  - either onto the Black Market to sell, with no confirmation;
  - a Gear onto a matching copy to merge into the next tier (the result takes the slot it was dropped on);
  - a Gear onto another Gear or an empty Gear slot to move it (`CardManager.MoveGear`; slot order is trigger order); a Trick onto another Trick slot to move it;
  - either onto the red DISCARD strip down the right edge (between MAP and the draw pile) while an item is held.
  - There is no SELL panel, Sell Items mode, Merge button or Trick USE button. The HP panel stays visible beside the compact hand.
- Card Tricks only ever target cards in hand. Mods earned outside the Black Market (Incidents, Hammer) arrive as their Mod Trick; there is no select-a-card dialog and no Training (Upgrade) node.
- (2026-10-22) A Mod Trick replaces a card's existing Mod. A Crystal also takes a card already of its suit and still spends a charge (Gear that counts Trick uses needs it).
- Clone Vial (formerly Copycat) works like Balatro's Cryptid: copies of a hand card (`ConsumableData.copies`, 2) go into the hand.
- Gear prices follow rarity: Common 10, Rare 20, Epic 30. Legendary Gear is never sold.

### Legendary Gear specifics

- **Overclock** (`rel_141`, `GearSpecialRule.SignatureMods`): while owned, every modded card plays its Mod's Signature version (`CardEnhancementData.signatureName`/`signatureDescription`/`signatureEffects`). `ModSignature` is the one switch: the effect resolver, card names, plates and info all read through it, so nothing is saved and previews match execution. Rules (2026-10-24): a Signature changes how the card plays, never just a bigger number. Cover Fire shoots the weakest other enemy (no RNG); a Bulwark card blocks without leaving the hand, once per enemy turn; Transfusion gives at most 1 Shield per fight however many cards have it; Joker counts as any rank for every rank check (groupings and Gear alike, via `CardInstance.MatchesRank`/`IsFaceCard`/`RanksBelow`/`RanksAbove`/`SharesRankWith`). A Mod Trick's text names the Signature it gives (`ModSignature.TrickDescription`, authored `signatureTrickText`), and a Signature card's look (`CardModFxUI`) runs faster with two wires racing round it. Card-side counters are `ModCounterKeys` (save data; never renumber); numbers are `gear.executionerHealthPercent`, `tutor*`, `chainReactionMaxBlasts`.
- **Dealbreaker** (`rel_036`): once per combat, an enemy hit that would be lethal and that Shield will not absorb destroys one random hand card (picked by card id from a node-keyed `dealbreaker` stream) and is negated. It runs only in `PlayerRuntime.ModifyIncomingCombatDamage` via `CombatManager.TryNegateLethalHit`, never in previews; an empty hand keeps the charge.
- **Nuclear Option** (`rel_100`): one attack may commit up to 5 cards of any kind (`gear.nuclearOptionMaxCards`) on one enemy, each card hitting separately. Ace pairs and Hands same-rank sets keep their rules; a one-suit Royal Flush (10-J-Q-K-A) defeats instantly.
- **Ghost Lens** (`rel_035`, `RevealAllMapNodes`): scouts every node ahead, on purchase and on each new District.

## Black Market

- Goods: each slot is Gear 75% / Trick 25%; Gear rarity 6:3:1 Common/Rare/Epic, never Legendary. Shop Gear is standalone (`BuyStandaloneGear`): no tier-upgrade preference or replacement, and owned copies are sold. Supply is repeatable within a Black Market. A Contract needs no earlier purchase. In dev mode the Black Market stocks every Gear, Trick and service.
- Layout: two narrow columns, Goods and Services, centred over the hand (`ShopUI.MarketLeft`; the title lines up with Goods), with CONTINUE TO MAP pinned at the foot of the services (outside the services scroll, so it is always visible; the tile geometry in `ShopOfferUI` is tight so the four services fit).
- (2026-10-24) Goods are a grid of squares, three across (`SquareGoodsGrid`; a normal Black Market's five sit in two rows): big art, the price ("10 Credits") across the top, the name along the foot. Clicking a square selects it (lime border; Cut Sheet target: thick teal crop marks and a lift), opens its rules text beside it and shows BUY under it; BUY confirms; a second click puts it down. A bought square leaves its cell empty.
- Services stay rows whose price button ("5 Credits") buys at once; a bought service row leaves a gap that closes, so the rows below slide up. Supply and Stake tiles always show MIN − n + MAX (all four boxed buttons; clicking the tile adds no other controls). REROLL GOODS is a solid acid-lime button (`RerollButtonStyle`), not a tile (Cut Sheet target 2026-10-27: a cream button with a mustard price stamp).
- (2026-10-23) A plain lack of Credits shows no text, it only disables the price button (`RunManager.GetShopOfferBlockedReason` gives the reasons that do show).
- A bought item flies from its tile into its slot, and the slot shows it only once the flight lands (`ItemDragUI.FlyIntoSlot` `revealOnLanding`; enemy drops and Trick rewards too).

## Map, scouting, Incidents

- Choosing a route has no map button. Inside a fight, Incident or Black Market a MAP button (top right) opens the route map view-only above the screen; CLOSE MAP returns.
- Scouting (Black Market Intel, the Late-Night Diner tip, Lantern) reveals one seeded-random reachable node whose contents are unknown, of any kind. Fights show their enemies only once scouted (`PathNode.scouted`); Headliners are always known. `RunManager.GetNodeDetail`, `IsScoutedNode` and `NodeDetailsVisible` define what a node reveals.
- Map drawing: where two routes cross, the overpass breaks and hops over the other in the route colour (`PathScreenUI.DrawHop`). `MapNodePresentation` draws node glyphs as wide tags with the label inside; a lime outer ring marks scouted nodes (Cut Sheet target 2026-10-27: an ink eye glyph on the tag), a known Enforcer prints in signal red, and the node the run is in (seen from the MAP view) is lime-filled with a lime edge (Cut Sheet target: teal fill with ink type). A scouted node opens its info on the first click and is entered on the second; other nodes are entered on the first click. The District title has a second line naming the Headliner and its abilities (click for the rules).
- An authored Incident whose every choice is unavailable offers LEAVE (`RunManager.CanLeaveBlockedEvent`/`LeaveBlockedEvent`): nothing gained or lost, so a run never stalls.

## Characters (2026-10-24)

Balatro-deck style: the run setup screen (NEW RUN) picks one of six (`CharacterPickerUI`, all available, last pick remembered in PlayerPrefs), each with one simple perk: Rook +10 max HP, Dex +1 Gear slot, Nova +1 Trick slot (stacks on Cargo Case, so up to six Trick slots), Scarlet +1 hand size, Glitch a seeded-random Common standalone Gear (own `character-gear` stream), Velvet 15 Credits.

- `CharacterData` assets (`Assets/Data/Characters`, registered in `RunSaveContent.characters`, written by `DealbreakCharacterAuthoring`) hold the perk numbers; each perk's text quotes them (`CharacterTests`).
- Start-of-run perks apply once in `RunManager.StartRunWithSeed(seed, character)`; capacity perks are read live through `CardManager.Character`.
- Saved as `CardManagerSaveData.character` (save v6); a v5 save, the tutorial and dev runs without one play with no perk and the first character's look.
- The portrait sits above the Credits chip (`PlayerAvatarUI`); a click opens its name and perk. Balance stats break runs down by character.

## Art direction

**(2026-10-27) Cut Sheet replaces the neo-punk editorial look.** The full spec is [`design/art/art-bible.md`](../design/art/art-bible.md) (sections 1–4 and 7). This ruling is decided and **partly built**: since 2026-10-28 (slice S6b) the theme draws the Cut Sheet palette and fonts, but shapes, shadows, tilt and the board texture arrive zone by zone (`production/ui/hud/engine-notes.md`). Read "today" from the code and "target" from here and the art bible.

- Scope is UI chrome only. Card faces, item icons and character portraits keep their halftone-print art and pipeline (below).
- The rule: everything on screen is a printed paper object (edge, hard offset shadow, ink on stock). Warm ink-brown board `#1C1814` (not green felt), cream stock `#EFE4CC`, kraft `#B8956A`, ink `#121212`. (2026-10-28) Eyedropper check done (`Tools/PrintArt/sample_palette.py`): cream and ink match the art; red, teal and mustard take the colours sampled from the card dots, Scanner and Gold Pistol, and every contrast gate still holds (`contrast_check.py`).
- Semantic colours, one meaning each: signal red `#A84538` for damage and danger only; teal `#4AA6B8` for selection and valid targets only (replaces acid lime); mustard `#E6AB55` for Credits only. Each also has a shape or motion backup for colourblind players.
- Square chrome, 3px ink borders on panels and buttons, hard shadows down-right whose depth shows importance and pressability; slots are die-cut stickers. Tilt is visual-only from a fixed per-slot table (never RNG), and never on panels, the tooltip, buttons, meters, numbers or multi-line text.
- Readability floors: a selected or block-picked card moves up at least 24px with crop marks (shadow depth is never the only cue); valid targets get thin crop marks and the targeted enemy thick marks plus a teal strip; Credits always show as a mustard stamp with ink type; minimum text 18px (20px on kraft).
- Fonts: Anton and Barlow Condensed stay; Zilla Slab replaces Space Grotesk for body text and Courier Prime replaces IBM Plex Mono for numbers.
- Add a Reduce Motion setting (no shake, instant settles, fixed tilt; trigger order and `ImpactDelay` timing kept). No full-screen shake at any setting.
- (2026-10-28, header inventory) Slot tilts are the `CutSheetMetrics.SlotTilt` table (-0.6, +0.8, -0.3, +1.0, -0.9, +0.4, -0.5, +0.7 degrees by slot index; the Trick row starts five entries along). Gear shows no tier number: better Gear has a better aura (`ItemTierFxUI`, header and Black Market alike), told apart by structure and colour. Common: none; Rare: one frame, cobalt `#5B7FD6`; Epic: a breathing double frame and a racing wire, violet `#9B6AD8`; Legendary: the double frame with a halo, a fast pulse, two wires and eight sparks, orange `#EE8A3A` (2026-10-29: classic rarity colours, an owner exception to the palette; they stay clear of teal, red and mustard). Reduce Motion keeps each tier's static frames. The named rarity tag shows on Black Market tiles only.
- (2026-10-27, combat HUD spec) Also add a screen-shake toggle, trigger playback speed (1x/1.5x/2x) and tooltip text size (100/115/130%). No global HUD or text scale. Keyboard focus opens info only; selecting, targeting and items stay mouse-only for now. On ultrawide screens the play area stays 16:9 and centred.
- Superseded by this: the 2026-10-21 neo-punk ruling (near-black stage, off-white type, acid lime, 1px hairlines, "no off-white cut-out frames", buttons by weight through `EditorialButton.Role`), the 2026-10-24 halftone print on every UI fill (halftone stays only as a semantic red offset and the defense-state edge), and the single ink-and-grain stage for every menu (each state now has its own board tone; see art bible section 2).
- Item icons (unchanged): painted item art reprinted as halftone cut-outs (own colours, thin off-white cut line, no shadow) by `Tools/PrintArt/make_item_icons.py` (sources `Tools/PrintArt/item_sources`, output `Assets/Art/Items`), assigned as `iconSprite` and drawn through `ItemIconUI`. One icon per Gear family; every tier shares it. Character portraits get the same print from `Tools/PrintArt/make_portraits.py` (neutral faces from the pack's two character folders, output `Assets/Art/Characters`).

## HUD layout (2026-10-23, aspect ratios 2026-10-24)

- The header carries no District or Headliner name (the route map shows them), and no "Headliner reward added" banner (the reward card is seen flying into the draw pile).
- Gear and Tricks run as two rows of slots across the header, Balatro style, each with its heading and count on its left (`DealbreakCentralStageAuthoring.InventoryLayout`, menu Dealbreak/UI/Apply Header Inventory Layout). Each row's heading and count sit on a ticket-stub strip (`InventoryPanelUI`, Cut Sheet since 2026-10-28): Gear cream, Tricks kraft, ink type (red is danger only). Slots are tilted stickers with ink shadows; empty slots are dashed outlines (`CutSheetSlotUI`).
- The hand size ("HAND 6/8") rides at the hand's right end and follows the last card, not the hand frame's edge.
- The shared stage (fight, map, Black Market, Incident) spans x .06–.88, y .18–.86 and centres over the hand (`StageLayout`).
- Every CanvasScaler is Expand, so the canvas is never under 1920×1080 and the 16:9 layout only gains room; the header rows (Gear bar, Trick heading, count and six slots) keep their 1080p pixel boxes from the canvas's top centre (`CanvasPin`), and the MAP button sits under them.
- An active Contract's progress is the only run-progress text, a small line above the draw pile.
- (2026-10-27, combat HUD spec `design/ux/hud.md`, not built yet) ATTACK, BLOCK and PASS stay below the hand. INCOMING sits directly under the enemy row with the status line below it (the enemy row rises about 20px to make room). The status line becomes a cream notched tag. A waiting enemy drop and LEAVE IT move to the top of the stage, under the header. The DISCARD strip stops below MAP and above the Contract line. The HP panel moves down beside the action bar (it was raised 114px over the hand frame), with the Credits chip and portrait stacked above it; the action bar centres under the hand, carrying the sort buttons in its left end.

## Detail panel

`ContextualTooltipUI` is the single detail panel (click-pinned). Sources are `CombatInspectUI` (cards, enemies, Gear) and `ContextualInfoTrigger` (everything else, including map nodes).

- Placement: hand cards open above the card; Gear and Trick slots open below the slot; everything else opens right, then left, above, below.
- It draws at sorting order 90: above gameplay, below modal menus (100).
- Panels that open beside slots must call `ContextualTooltipUI.RegisterObstacle(rect)`.

## Timing and motion

- `AttackCardPresentationUI.ImpactDelay(hitIndex)` is when a hit visibly lands; enemy HP drain, shake and the defeat fade wait for it.
- `DisplayedNumber` eases HP and Credits. `BoardPanelTransition` fades panels (`FadeIn` for modal overlays).
- Enemy HP is a meter (bigger, ATTACK beside it). While an enemy has Shield its fill is grey and one shield glyph per charge (`ShieldGlyph`, drawn in code) sits on the bar; its abilities are named under it. (2026-10-29, Cut Sheet) Each ability is a tag beside the ATTACK tag (paper, ink type); clicking it opens that ability's description next to it, and the enemy's own detail no longer repeats ability text. The enemy HP meter fills cream on an ink track, so a full bar reads bright and lost HP goes dark. The turn state, the selection or block preview and the auto-Recover notice share one cream status tag below INCOMING (2026-10-29). In defense the board darkens and a soft red halftone edge fades down from the top of the screen to the enemies' HP line (no hard border, no wash over the enemies).
- Player HP is a meter (`HealthBarBacking.Look.Meter`: hairline track, inset red fill, "HP n/max" on it; Cut Sheet target: 2px ink border, cream track, red fill, ink ticks every 5 HP); Block and Shield sit on a small line above it, not on the bar.
- Credits show as a chip and a number just above the HP panel (`CreditsHudUI`, sprite `DealbreakUITheme.creditsIcon`, printed from `Artifacts/24`). A defeated enemy's Credits fly into the coin as its killing hit lands (`EnemyDisplayUI.DefeatHitLanded`); the count waits for the board and the coins (`CreditsHudUI.HoldsCredits`). Kill Credits are `CombatManager.SecuredCredits`.
- Mod and Gear triggers (2026-10-23, after Balatro): gameplay reports each activation to `EffectTriggerFeed` as it executes (an `EffectTriggerTrace` records attack and block values from the committing calculation; previews never report). `EffectTriggerPresenterUI` plays them in order: the card or Gear hops, a tag says what it did, Credits fly into the chip and healing onto the HP bar, and an HP cost (Overclocked) drains the bar and flies into the card. Activations before the hit play first while the played cards stand lifted out of the hand (`AttackCardPresentationUI` lead-in); the board stays busy until combat triggers finish, and Credits and HP count up as their chips land.
