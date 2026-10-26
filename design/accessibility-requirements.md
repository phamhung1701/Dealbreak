# Accessibility Requirements

> Authoring guidance: .claude/docs/templates/guidance/accessibility-requirements-guide.md (load per-section as you author — do not read entirely).

> **Status**: Committed
> **Author**: owner + ux-designer
> **Last Updated**: 2026-10-27
> **Accessibility Tier Target**: Basic, with named Standard and Comprehensive features elevated (below)
> **Platform(s)**: PC (Windows), mouse primary; keyboard focus opens info; no gamepad or touch
> **External Standards Targeted**:
> - WCAG 2.2 Level AA for text contrast (1.4.3) and non-colour use (1.4.1); not for keyboard operability (2.1.1), see Known Intentional Limitations
> - AbleGamers CVAA Guidelines: not applicable (no communication features)
> - Xbox Accessibility Guidelines (XAG): No (PC only)
> - PlayStation Accessibility (Sony Guidelines): No
> - Apple / Google Accessibility Guidelines: N/A
> **Accessibility Consultant**: None engaged
> **Linked Documents**: `design/ux/hud.md`, `design/art/art-bible.md`, `Docs/OWNER_DECISIONS.md`
> **Template**: Accessibility Requirements

> **Why this document exists**: per-screen accessibility notes live in the UX
> specs. This file holds the project-wide commitment, the feature matrix, the
> test plan and the audit history. If a feature conflicts with a commitment
> here, change the feature, not the commitment, unless the owner revises this
> file.

---

## Accessibility Tier Definition

### Tier Definitions

| Tier | Core Commitment | Typical Effort |
|------|----------------|----------------|
| **Basic** | Critical player-facing text is readable at standard resolution. No feature requires color discrimination alone. Volume controls exist for music, SFX, and voice independently. The game is completable without photosensitivity risk. | Low — primarily design constraints |
| **Standard** | All of Basic, plus: full input remapping on all platforms, subtitle support with speaker identification, adjustable text size, at least one colorblind mode, and no timed input that cannot be extended or toggled. | Medium — requires dedicated implementation work |
| **Comprehensive** | All of Standard, plus: screen reader support for menus, mono audio option, difficulty assist modes, HUD element repositioning, reduced motion mode, and visual indicators for all gameplay-critical audio. | High — requires platform API integration and significant UI architecture |
| **Exemplary** | All of Comprehensive, plus: full subtitle customization (font, size, color, background, position), high contrast mode, cognitive load assist tools, tactile/haptic alternatives for all audio-only cues, and external third-party accessibility audit. | Very High — requires dedicated accessibility budget and specialist consultation |

### This Project's Commitment

**Target Tier**: Basic, with elevated features (owner decision, 2026-10-27)

**Rationale**:
- Dealbreak is a single-player, turn-based card game for PC with no timed
  inputs, no voice and no audio yet, so the most common motor and auditory
  barriers don't arise.
- Its real barriers are visual and cognitive: dense rules text, small
  numbers, and a colour-coded board (red danger, teal selection, mustard
  Credits).
- Basic covers those when every colour meaning has a shape backup and text
  meets WCAG AA contrast and an 18px floor. The art bible already commits to
  both.
- Standard would add a colourblind mode, which the shape cues make redundant,
  plus remapping and broad text scaling, which the pinned 1080p layout makes
  costly.
- Elevating the cheap Comprehensive feature that matters here, reduced motion,
  covers the main remaining risk: the motion-heavy, Balatro-style feedback.

**Features explicitly in scope (beyond tier baseline)**:
- Reduce Motion (Comprehensive): shake off, instant hops and slaps, fixed
  tilt; trigger order and hit timing kept. Owner ruling, 2026-10-27.
- Separate screen-shake toggle.
- Trigger playback speed (1× / 1.5× / 2×), so slow feedback never forces a
  wait.
- Tooltip text size (100 / 115 / 130%): a partial form of Standard's
  adjustable text size, covering the densest text, the rules panel.
- WCAG 2.2 AA contrast for all text (Standard).
- Keyboard focus opens info for every interactive HUD element.

**Features explicitly out of scope** (see Known Intentional Limitations):
- Colourblind palette modes: shape and motion cues carry every colour meaning.
- Keyboard-only play: selecting, targeting and using items stay mouse-only.
- Screen reader support: UGUI exposes no desktop accessibility tree.
- Full input remapping: the only keyboard input is Escape and tooltip paging.
- Global UI or HUD scaling: it breaks the pinned 1080p header layout.

---

## Visual Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Minimum text size | Basic | All UI | Not Started (Cut Sheet unbuilt) | 18px minimum at 1080p, 20px on kraft (art bible section 7). Ability names sit at the floor. |
| Text contrast | Standard (elevated) | All UI text | Not Started | WCAG AA: 4.5:1 body, 3:1 large. Art bible section 4 pairs: ink on cream 15:1, cream on board 13:1, cream on red 4.6:1. Banned: red text on the board below 24px bold, teal text on cream, red text on kraft. |
| Color-as-only-indicator audit | Basic | All UI and gameplay | Designed | See the table below. Every colour meaning has a shape or motion backup. |
| Screen flash / strobe | Basic | All VFX | Designed | No element flashes more than 3 times a second. The defense dot screen fades in over at least 300ms and never pulses. No full-screen shake at any setting. |
| Reduce Motion | Comprehensive (elevated) | All UI motion | Not Started (ruled 2026-10-27) | Shake off; hops and slaps settle instantly; tilt fixed at rest; board jolt off; tone crossfades become cuts. Trigger order and `ImpactDelay` are kept. |
| Screen-shake toggle | elevated | Hit shake | Not Started | On by default; forced off by Reduce Motion. |
| Tooltip text size | Standard (partial) | Contextual tooltip | Not Started | 100 / 115 / 130%; the panel scrolls and its max width grows. |
| Colorblind modes | Standard | — | Out of scope | Shape cues carry every meaning; see Known Intentional Limitations. |
| UI scaling, high contrast mode, brightness/gamma | Standard / Comprehensive / Basic | — | Out of scope | The pinned 1080p layout can't scale. The fixed 2D palette meets contrast without gamma. See Known Intentional Limitations. |
| Subtitles | Basic | — | N/A | The game has no voiced content. |

### Color-as-Only-Indicator Audit

| Location | Color Signal | What It Communicates | Non-Color Backup | Status |
|----------|-------------|---------------------|-----------------|--------|
| INCOMING, ATTACK tag, HP fill, DISCARD strip | signal red | damage, danger | notched tag shape; the words INCOMING, ATTACK, DISCARD; halftone offset; numeric HP; hit shake (off under Reduce Motion, so the words carry it); cream outer edge for protanopia | Designed |
| Enemy target marks | teal | valid target / targeted | thin or thick crop marks outside the portrait; a strip under the HP meter; a hop when targeting starts | Designed |
| Selected cards | teal | selection | a lift of at least 24px and a deeper shadow; the lift alone reads | Designed |
| Credits chip, prices | mustard | Credits | round notched stamp, coin glyph, flight to the chip | Designed |
| Enemy HP fill | grey hatch | Shield active | one shield glyph per charge | Designed |
| Disabled or unavailable controls | faded fill | can't use now | hatch overlay and no shadow | Designed |
| Map: known Enforcer node | red fill | dangerous node | node label text and its glyph | Designed |
| Map: current node | teal fill | you are here | to be decided; see Open Questions | Open |
| Gear vs Tricks heading | cream vs kraft | which row | the heading words GEAR and TRICKS | Designed |

---

## Motor Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Timed inputs | Standard | All | Met by design | The game is turn-based with no timed inputs. The 0.8s auto-Recover beat is not an input. |
| Hold and rapid inputs | Standard | All | Met by design | There are none. |
| Hit areas | Basic | All interactive UI | Not Started | Hit areas stay untilted rectangles; tilt is visual only. ATTACK, BLOCK and PASS are at least 44px tall at 1080p. |
| Item use without drag | Standard | Tricks and Gear | Out of scope | Using a Trick or moving Gear needs a drag; see Known Intentional Limitations. |
| Full input remapping | Standard | Keyboard | Out of scope | The only keyboard inputs are Escape and tooltip paging. |
| One-hand play | — | All | Met by design | Mouse only; there are no simultaneous inputs. |
| HUD repositioning | Comprehensive | — | Out of scope | The layout is fixed by owner rulings. |
| Aim, movement, platforming assists | — | — | N/A | The game has none of these systems. |

---

## Cognitive Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Pause anywhere | Basic | All gameplay states | Designed | Pause works in every state, including trigger playback, which freezes and resumes in order. The tutorial pauses the same way. |
| Reading time | Standard | All info | Met by design | Info is click-pinned and never auto-dismisses. Trigger tags (0.8s) and the auto-Recover notice (0.8s) only report what the board then shows. |
| Each fact shown once | elevated | All screens | Designed | Owner ruling; `design/ux/hud.md` section B. |
| Preview equals execution | elevated | Combat | Built | Previews use the same calculation as execution and consume no RNG. |
| No how-to outside the tutorial | — | All | Built | `ActionGuidanceCopyTests` checks this. Tutorial replay is an open question. |
| Trigger playback speed | elevated | Combat feedback | Not Started | 1× / 1.5× / 2×; order is kept. |
| Difficulty options | Standard | — | Out of scope | Runs are balanced as one difficulty. Not listed as a limitation: no tier this project targets requires it. |
| Objective clarity | Standard | Contract | Designed | The Contract line shows progress, and a click opens the objective and reward. |

---

## Auditory Accessibility

The game has no audio assets today, so nothing is communicated by sound. When
audio ships, two rules apply:
- independent Music, SFX and UI volume sliders (Basic), in the main settings
  and the pause menu;
- no gameplay information is carried by sound alone (every cue already has a
  visual).

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Independent volume controls | Basic | Music / SFX / UI buses | Not Started (no audio yet) | Required before any audio ships. |
| Subtitles, captions, mono audio | Basic–Comprehensive | — | N/A | No voice, and no audio-only cues. |

### Gameplay-Critical SFX Audit

| Sound Effect | What It Communicates | Visual Backup | Caption Required | Status |
|-------------|---------------------|--------------|-----------------|--------|
| (none yet) | — | — | — | Re-audit when audio is added |

---

## Platform Accessibility API Integration

| Platform | API / Standard | Features Planned | Status | Notes |
|----------|---------------|-----------------|--------|-------|
| PC (Screen Reader) | Windows Narrator / NVDA | none | Out of scope | Unity UGUI exposes no desktop accessibility tree; see Known Intentional Limitations. |
| Steam (PC) | Steam Input | none | N/A | No gamepad support. |
| Consoles, mobile | — | — | N/A | PC only. |

---

## Per-Feature Accessibility Matrix

| System | Visual Concerns | Motor Concerns | Cognitive Concerns | Auditory Concerns | Addressed | Notes |
|--------|----------------|---------------|-------------------|------------------|-----------|-------|
| Combat HUD | colour coding, dense numbers | none (turn-based) | many simultaneous facts, trigger chains | none | Yes (in design) | `design/ux/hud.md` |
| Hand and selection | 8-card fan overlap | precise card clicks | multi-card selection rules | none | Yes | rank and suit index always visible; selection preview |
| Items (drag) | drop-target clarity | drag required | drop outcomes | none | Partial | marks on legal targets and a drop preview; drag-only is a limitation |
| Black Market | prices, affordability | none | stock choices | none | Yes | mustard stamp; hatch for unaffordable; no text for lack of Credits |
| Route map | node types, scouting | none | route planning | none | Partial | the current node's non-colour cue is open |
| Incidents | choice costs | none | reading load | none | Yes | each cost shown once with its shape |
| Menus and settings | text size | none | none | none | Yes | Reduce Motion, shake, playback speed and tooltip size live here |

---

## Accessibility Test Plan

| Feature | Test Method | Test Cases | Pass Criteria | Responsible | Status |
|---------|------------|------------|--------------|-------------|--------|
| Text contrast | contrast analyser on 1080p screenshots | every text/background pair in combat, map, Black Market, Incident and menus | body ≥ 4.5:1, large ≥ 3:1, no banned pairs | ux-designer | Not Started |
| Colour-only cues | protanopia and deuteranopia simulator on screenshots | combat (targeting, defense, item held), Black Market, map | every row of the audit table is readable without hue | ux-designer | Not Started |
| Text floor | measure at 1920×1080 and 1280×720 | all screens | ≥ 18px at 1080p (20px on kraft); 720p result recorded | qa-tester | Not Started |
| Reduce Motion | enable it and play one full fight with Gear triggers, plus menus | shake, hops, slaps, tilt, crossfades | none move; trigger order and hit timing match the default | ux-designer | Not Started |
| Pause anywhere | pause in every combat state, including mid-playback and in the tutorial | all HUD states in `design/ux/hud.md` section E | pause opens; playback resumes in order | qa-tester | Not Started |
| Flash safety | review VFX and the defense dot screen | defense entry, hits, rewards | nothing above 3 flashes per second; no pulsing | ux-designer | Not Started |

---

## Known Intentional Limitations

| Feature | Tier Required | Why Not Included | Risk / Impact | Mitigation |
|---------|--------------|-----------------|--------------|------------|
| Colourblind palette modes | Standard | Shape and motion cues already carry every colour meaning (owner, 2026-10-27) | Low: protanopia weakens red on the board | Cream outer edge on INCOMING; words on every red element; simulator test |
| Keyboard-only play | Comprehensive / WCAG 2.1.1 | Selecting, targeting and items are mouse-only (owner, 2026-10-27) | Medium: players who can't use a mouse can't play | Focus opens info; revisit if requested |
| Screen reader | Comprehensive | UGUI has no desktop accessibility tree | High for blind players: the game is unplayable | None today; revisit if Unity adds desktop support |
| Input remapping | Standard | Almost no keyboard input exists | Low | — |
| UI and HUD scaling | Standard | The pinned 1080p header and stage anchors break | Medium for low-vision players | Tooltip text size; the 18px floor |
| Drag-only item use | Standard | Items are dragged like cards (owner ruling) | Medium for players with motor limits | Large drop targets with marks; no time pressure |
| 1280×720 below the text floor | Basic | 18px renders at 12 physical px | Medium at 720p | Open question: raise the minimum resolution to 1600×900 |
| Brightness / gamma | Basic | A fixed 2D palette meets contrast floors | Low | Contrast test plan |

---

## Audit History

| Date | Auditor | Type | Scope | Findings Summary | Status |
|------|---------|------|-------|-----------------|--------|
| 2026-10-27 | ux-designer (agent) | Design review | Cut Sheet art bible, sections 1–4 | Found 10 issues (depth cue, lime and red reuse, targeting states, mustard and kraft contrast, motion); all were merged into art bible section 7 | Closed |
| 2026-10-27 | /ux-review | Spec review | `design/ux/hud.md` | Accessibility NOT ASSESSED (no tier set); this document sets it | Re-review pending |

---

## External Resources

| Resource | URL | Relevance |
|----------|-----|-----------|
| WCAG 2.2 | https://www.w3.org/TR/WCAG22/ | Contrast (1.4.3) and use of colour (1.4.1) |
| Game Accessibility Guidelines | https://gameaccessibilityguidelines.com | Checklist by cost |
| Colour Blindness Simulator (Coblis) | https://www.color-blindness.com/coblis-color-blindness-simulator/ | Colour-only cue tests |
| Harding / photosensitivity guidance | https://gameaccessibilityguidelines.com/avoid-flickering-images-and-repetitive-patterns/ | Flash safety |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| The map's current node is marked only by a teal fill. Add a non-colour cue, such as a pin glyph or "YOU ARE HERE"? | owner | before the map spec | — |
| Minimum supported resolution: raise it to 1600×900 to keep the 18px floor, or accept 720p below it? | owner | before the UI rework | — |
| Can the tutorial be replayed from a menu (Standard: tutorial persistence)? | owner | — | — |
| Re-audit the auditory section when the first audio ships. | ux-designer | first audio milestone | — |
