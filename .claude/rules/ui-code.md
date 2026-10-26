---
paths:
  - "Assets/Scripts/UI/**/*.cs"
  - "Assets/Scripts/View/**/*.cs"
  - "Assets/Scripts/Frontend/**/*.cs"
  - "Assets/Editor/Dealbreak*Authoring*.cs"
---

# UI Code Rules (Dealbreak)

The owner's rulings in `Docs/OWNER_DECISIONS.md` and the "UI essentials" in
`CLAUDE.md` decide how UI looks and behaves. Where a template agent or skill
suggests otherwise, they win.

- UI never owns or changes game state. Attack and block go through
  `CombatCommands`; `CardView` is only a UI adapter over `CardInstance`.
- Styling goes through `DealbreakUIRuntime`, `DealbreakUIStyle`,
  `EditorialFrame`, `EditorialButton` and `EditorialSurface`, not ad-hoc colours.
- Info opens on click, next to the clicked object, through the single
  `ContextualTooltipUI`. No how-to guidance outside the tutorial.
- Player-facing names follow the terminology workbook. Text that quotes a
  balance number stays authored and is pinned by `BalanceTextDriftTests`.
- Many layouts are written by the editor authoring scripts in `Assets/Editor/`.
  Move an element in the scene and its authoring script together.
- Animations respect the motion rules in the owner decisions.
- There is no localization system and no gamepad support today. Don't add
  either unless a task asks for it.
