---
id: kd_ced7b2fc-ebc9-4e23-a6a3-a531e37c5c58
injectMode: inherit
aiEditMode: inherit
---

# Dealbreak Phase 1 — completion report

## Scope completed

Phase 1 only. No player HP, optional defense flow, emergency recovery, one-card-per-encounter progression, face-card exclusions/rewards, or other Phase 2+ gameplay was implemented.

## Implementation

- Replaced UI-transform-owned card state with `CardInstance` and `CardCollection` runtime models.
- Card ownership is authoritative across owned, deck, hand, and discard collections; every card has a stable per-run ID.
- Kept the current 52-card initial deck and eight-card opening hand for Phase 1. Starting without face cards remains a later progression change.
- `CardManager` now creates card views from model state, validates selected/discarded cards against the authoritative hand, and synchronously removes views from layout before delayed destruction.
- Reset clears model zones, views (including dragged views), selection, gold, relic ownership, and generated runtime card definitions.
- Card selection persists after pointer release. Hover, selection, and drag visuals are independent.
- Dragging/reordering remains presentation-only and preserves card ownership and world scale. Cross-field transfers are rejected.
- Manager discovery no longer silently creates unconfigured managers. `GameController` creates/configures one CardManager, CombatManager, and RunManager before UI subscriptions, then starts the run once.
- Run initialization now performs exactly one reset/build/deal sequence.
- Combat consumes cards through CardManager's validated model operation and reads authoritative hand count rather than Transform children.
- Game scene wiring corrected: HandField is its own card holder; Thief, Goblin, and Knight assets are assigned to GameController and passed to RunManager.
- Added runtime/test assembly definitions and pure EditMode model tests.

## Verification

Compilation and domain reload completed successfully with no Console errors or warnings.

EditMode tests: 11/11 passed. Coverage includes standard 52-card initialization, unique IDs, exact-instance conservation, hand capacity, invalid requests, valid/duplicate/foreign discard behavior, discard recycling, Hearts return, shuffle conservation, and clear/reset.

Direct Game scene Play Mode:
- Exactly one CardManager, CombatManager, and RunManager.
- 52 owned = 44 deck + 8 hand + 0 discard.
- Eight active hand views matched eight model hand cards.
- Twelve bosses and a generated accessible path existed.
- Enemy references and hand holder were valid.
- No Console errors/warnings.

Interaction checks:
- Begin drag then restore preserved parent, scale, and all model counts.
- Pointer down/up retained selection and enabled Play.
- Playing one card preserved 52 owned cards while changing hand 8→7 and discard 0→1; the exact played ID was in discard, the view count became seven, and selection cleared.

Restart check:
- `StartRun()` restored 52 owned, 44 deck, 8 hand, 0 discard, eight views, no selection, zero gold/relics, twelve bosses, and one instance of every manager.

Main Menu entry check:
- MainMenu → Game initialized the same single manager set, 52/44/8 card state, eight views, twelve bosses, and a generated path.
- Active editor scene restored to `Assets/Scenes/Game.unity` afterward.

Final read-only review found no Phase 1 blockers. Scene is saved and not dirty. Unrelated test-run changes to ProjectSettings and the dynamic TMP fallback atlas were restored.

## Files affected

- `Assets/Scenes/Game.unity`
- `Assets/Scripts/Extensions/Singleton.cs`
- `Assets/Scripts/Managers/CardManager.cs`
- `Assets/Scripts/Managers/CombatManager.cs`
- `Assets/Scripts/Managers/RunManager.cs`
- `Assets/Scripts/UI/DeckViewerUI.cs`
- `Assets/Scripts/UI/EnemyDisplayUI.cs`
- `Assets/Scripts/UI/GameController.cs`
- `Assets/Scripts/View/CardView.cs`
- `Assets/Scripts/View/Field.cs`
- `Assets/Scripts/Data/CardInstance.cs`
- `Assets/Scripts/Data/CardCollection.cs`
- `Assets/Scripts/Dealbreak.Runtime.asmdef`
- `Assets/Tests/EditMode/CardCollectionTests.cs`
- `Assets/Tests/EditMode/Dealbreak.EditModeTests.asmdef`

## Deliberately deferred

- Player HP and residual incoming damage.
- Optional defense and Take Remaining Damage action.
- Emergency recovery when empty-handed.
- One card added at each combat encounter instead of current refill behavior.
- Initial 40-card Ace–Ten deck and exact face-card rewards.
- Moving node completion from selection to successful resolution.
- Boss identity/ability and map/shop progression refactors.

These belong to later approved phases and should not be inferred as completed from the Phase 1 foundation.
