---
id: kd_be979d60-aa63-4b02-b708-f61734df37fc
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# Early-access game-data implementation plan

**Status: EXECUTION TRACKER.** Phase statuses reflect implementation evidence, not blanket approval of unresolved content values. No commit is requested or performed.

## Authority and baseline

- Source reviewed directly with openpyxl: `the old game-data workbook (removed 2026-10-25)` sheets Enemies, Bosses, Artifacts, Enhancements, Events, Shops, Maps, Consumables, Boss Abilities, Scaling. Re-read exact rows and notes at phase entry; workbook changes supersede this snapshot.
- User approved the eight-phase order, not every TBD content value. Phases/rows leave Planned only when individually implemented and verified; acceptance state is tracked separately.
- Effort labels S/M/L/XL are relative engineering scope only, not dates or delivery promises.
- Before edits inspect `git status --short --branch`, preserve dirty user-owned baseline, inspect callers/serialized bindings/tests, and record phase baseline. Never stage/stash/reset/clean/commit/push.
- `RunManager.BuildBossDeck` already creates 12 J/Q/K bosses in deterministic order with rank-based stats/suit abilities. `CreateEliteType` applies ×1.5 HP, +2 ATK, ×2 Gold; authored elites must avoid double application. `EnemyRuntime` copies immutable EnemyTypeData stats.
- Existing `ArtifactUpgradeResolver` and same-slot upgrades exist. Preserve one enhancement/card; do not assume Upgrade nodes are artifact upgrade UI or invent mastery requirements.
- Current backpack: three slots, per-item charges, no stacks and no general reward tray. Full reward must support replace/decline, never silent loss. Rare Pack capacity loss is withdraw-only; partial charges consumed first; rune suits remain separate.
- Boss assignment cached once per seed/map; UI re-entry/retry cannot reroll. Map-entry UI shows cached current-map boss description, never whole preview deck.
- Surveyor’s Map may duplicate already-visible Shops/Elites: decision gate requires useful distinct benefit; do not add a no-op reveal.
- Inspect/preserve dirty M4DevMode state. Prior builds had internal Unity Bee pipeline errors. Capture diagnostics, distinguish infrastructure from code failure; release gate includes actual failure triage and Windows dev/release smoke when supported.
- TBD numeric economy, rarity, prices, growth and quantities require phase-entry approval. Do not invent, and do not claim current test/build status without running it.

## Approved phase order

| Phase | Scope | Effort | Entry gate |
|---|---|---:|---|
| 1 | Scaling + Knight, Shieldbearer, Brute | M | Resolve phase-specific workbook TBDs before code. |
| 2 | Mixed groups + Captain/Royal Knight elites; elites may appear on any map as an optional visible risk with reachable non-elite alternative | L | Resolve phase-specific workbook TBDs before code. |
| 3 | 12 boss variants + cached ability assignment + map-entry UI | XL | Resolve phase-specific workbook TBDs before code. |
| 4 | Oppression, Silence, Withering, Double Strike + Duelist/War Drummer | L | Resolve phase-specific workbook TBDs before code. |
| 5 | Challenger’s Crest, Club Epic, Overflow Epic, Bounty Ledger, Gilded Blade | L | Resolve phase-specific workbook TBDs before code. |
| 6 | Purchase/use hooks, reward tray, Traveler’s Pack tiers + economy artifacts (excluding Gilded Blade) | XL | Resolve phase-specific workbook TBDs before code. |
| 7 | Other emblem upgrades, growth/map artifacts + Master Thief | XL | Resolve phase-specific workbook TBDs before code. |
| 8 | Blood Ritual, Quest, Infinite Mode | XL | Resolve phase-specific workbook TBDs before code. |

## Phase protocol

- Entry: re-read exact workbook rows/notes; inspect relevant implementation, callers, tests, scene bindings, build state and dirty overlap.
- Resolve listed behavioral/numeric decisions with owner; leave unresolved subfeatures blocked rather than guessing.
- Add characterization/focused tests before modifying shared resolution order or ownership. Work one phase/slice at a time.
- Exit: focused tests, relevant regression, full available EditMode; PlayMode/real UI smoke where presentation/input/state matters. Report exact counts, failures, skips, environment and anything not run.
- Check same-seed determinism, cached decisions, reset/re-entry, exact-once reward boundaries and no shared data asset mutation.
- Review diff against baseline. Workbook status updates require separate authorization and evidence. No auto-commit/push.

---

## Phase 1 — Scaling + Knight, Shieldbearer, Brute

**Status:** Planned. **Effort:** M. **Workbook scope:** Scaling sheet; enemy_knight, enemy_shieldbearer, enemy_brute.

### Entry decisions
- Approve tie-breaking after multiplication and any unresolved base stats; Knight approved reference is 16 HP / 4 ATK.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Calculate map-scaled HP/ATK using HP factor `1 + 0.2 × (map−1)` and ATK factor `1 + 0.07 × (map−1)`, rounded once after multiplication using the approved tie rule. Apply only to base stats. Keep Captain/War Drummer bonuses fixed and excluded. Preview and spawned runtime share the same result.
- [ ] Use exact workbook stat/behavior baselines: Knight 16 HP / 4 ATK (proposed change from current Unity asset 25/8); solo introduction around Map 3 and later may appear with one weak ally. Shieldbearer 10 HP / 3 ATK begins each encounter with one Shield; proposed pairing is Shieldbearer + Goblin. Brute 18 HP / 3 ATK, alone or Duelist + Brute, alternates normal response and telegraphed attack at 2× ATK. Gold remains TBD. Do not modify assets at runtime.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `RunManager` scaling/construction; `EnemyTypeData`; `EnemyRuntime`; `PathScreenUI`/`EnemyGroupUI` previews. Reuse pure helper if present.

### Dependencies
- Baseline inspection and approved data decisions; no prior phase dependency.

### Tests and acceptance
- Tests: map 1/2/12, fractional rounding, runtime-preview parity, repeated instantiation without compounding, source asset immutability.
- Acceptance: Exact approved stats at all maps; one rounding step; no elite bonus or source mutation layered accidentally.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Phase 2 — Mixed groups + Captain/Royal Knight elites; optional elite route on all maps

**Status:** Planned. **Effort:** L. **Workbook scope:** Enemies sheet; all map encounter definitions.

### Entry decisions
- Decide group composition budget, elite frequency/spawn count and whether current visible elite routes already meet this goal. Preserve an optional elite alternative route on each map, never make it mandatory.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Author mixed groups and elite variants. Captain: 16 HP / 3 ATK, spawns with 2 Goblins, each +1 ATK while Captain lives; group base total 22 HP / 9 ATK. Royal Knight: 32 HP / 5 ATK, alternates Shield + normal attack and telegraphed 2× heavy attack. Both may appear on any map, including Map 1, as visibly risky optional encounters with reachable non-elite alternatives. Do not force an elite spawn on every map. Prevent generic `CreateEliteType` ×1.5 HP/+2 ATK/×2 Gold from double-applying to authored elites; Gold remains TBD. Keep Captain/Drummer ally bonus fixed and unscaled.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `RunManager` encounter generation/`CreateEliteType`; `RunMapGenerator`; `EnemyTypeData`/`EnemyRuntime`; `PathScreenUI`; `Phase7DMapVisibilityTests` and run tests.

### Dependencies
- Phase 1 accepted; do not pull later-phase behavior forward.

### Tests and acceptance
- Tests: Seed/map matrix for reachability, optionality, composition, exact elite stats, deterministic signatures and unchanged unrelated route streams; UI smoke.
- Acceptance: All maps permit the authored elite encounter to be eligible without forcing it to spawn; whenever an elite route/encounter is generated it is optional and has a reachable non-elite alternative. Authored stats correct and deterministic.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Phase 3 — 12 boss variants + cached ability assignment + map-entry UI

**Status:** Planned. **Effort:** XL. **Workbook scope:** Bosses and Boss Abilities sheets, all 12 boss IDs.

### Entry decisions
- Keep existing 12-boss rank/suit roster and approved 30 HP / 6 ATK shared baseline. Boss reward Gold remains TBD except retained legacy values. Resolve ability rounding details before implementation.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Assign abilities once at map generation and cache per seed/map; never reroll on map UI open, retry or re-entry. Jacks/Queens one ability; Kings two distinct: at most one hard counter. If none, exactly one defensive + one offensive; if one hard counter, second from remaining five non-hard-counter abilities. Suit immunity intrinsic and separate. For phase 3, only enable Heavy Swing (alternate normal/150% ATK response), Desperation (+25% ATK below 50% HP), Regeneration (heal 5% maximum HP after response, max 3 activations; rounding decision required), and Guarded (combat start/after each response: first non-immune hit reduced 50%; immunity does not consume it). Temporarily constrain assignment pool to implemented/verified abilities until phase 4 adds Silence, Withering, Oppression, Double Strike; enable those only after tests pass. Show only cached current-map boss description, not whole deck preview.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `RunManager.BuildBossDeck` and map cache; `EnemyAbility`/`EnemyTypeData`; `PathScreenUI`/`RunStatusUI` map entry; boss tests.

### Dependencies
- Phase 2 accepted; do not pull later-phase behavior forward.

### Tests and acceptance
- Tests: Roster/order; valid pairing categories; same-seed stability, changed-seed behavior, no UI reroll; displayed description equals runtime boss.
- Acceptance: Deterministic valid assignment, cached and accurately displayed; no unapproved stats/rewards.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Phase 4 — Oppression, Silence, Withering, Double Strike + Duelist/War Drummer

**Status:** Planned. **Effort:** L. **Workbook scope:** Boss Abilities and Enemies sheets.

### Entry decisions
- Approve remaining rounding. Confirm combat damage/heal/effect order with phase 3 assignment.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Silence and Withering apply only while boss is above 50% HP. Silence suppresses passive and triggered held-card effects, including Auxiliary, Mending and Golden. Withering blocks healing and prevented healing cannot trigger overheal/Vitality. Oppression adds +2 damage per held card beyond 3 once to response budget before Double Strike split. Double Strike is two hits at 60% normal ATK with approved per-hit rounding. Add Duelist: announces a suit each turn; first attack with that suit deals 50% damage, not immunity (rounding TBD). Add War Drummer: after each enemy response, grant allies +1 ATK for encounter up to +3 per ally; no self-buff, death stops future growth, existing buffs remain, fixed bonus not map-scaled.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `CombatManager`; `GameplayEffects`; `EnemyAbility`; `EnemyRuntime`; `CardManager` held-effect resolution; `EnemyGroupUI`.

### Dependencies
- Phase 3 accepted; do not pull later-phase behavior forward.

### Tests and acceptance
- Tests: card counts 3/4, hit split and mitigation, Silence passive/trigger, Withering no-heal/no-overheal, thresholds, fixed ally bonus and interrupted/defeated response.
- Acceptance: One bonus budget, correct effects ordering, enemy behavior parity and correct preview/runtime.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Phase 5 — Challenger’s Crest, Club Epic, Overflow Epic, Bounty Ledger, Gilded Blade

**Status:** Implemented; pending owner phase acceptance. **Effort:** L. **Workbook scope:** Artifact rows rel_136, rel_034, rel_110, rel_126, rel_128.

### Entry decisions
- Owner approved Club Epic replacement semantics; committed cards are excluded from held-Club count and ×1.25 factors floor once. Owner approved Overflow Rare behavior retention, current unplayed-hand snapshot at each auto-play, and shared three-autoplay root budget. Owner approved Bounty base-reward-only modifier and nearest/ties-up rounding; Gilded uses whole tens and applies once to opening attack. Common artifacts priced 20 Gold; Epic upgrades priced 30 Gold.

### Executable tasks
- [x] Inspect current source, data, tests and exact workbook rows; record baseline behavior. Baseline: Overflow Rare auto-play already had a shared three-play root-action budget and target fallback; RunManager owns encounter-kind context; CombatManager settles enemy Gold once.
- [x] Implement five artifacts with existing ownership/resolution seams; Club Epic replaces Rare ×2, Overflow Epic preserves Rare capacity/target/queue semantics, Bounty modifies base enemy Gold before separate bonuses, Gilded applies once to opening attack, Crest grants one Elite/Boss starting Shield.
- [x] Use existing ownership/resolution hooks first; only narrow seams were added for encounter-kind forwarding and new special rules.
- [x] Add focused automated tests; preserve existing cancel, failure, reset and retry paths.
- [x] Add five assets to the GameController artifact catalog; no broad UI redesign.
- [x] Run focused tests, full available EditMode and PlayMode regressions, inspect logs/diff, and request phase acceptance.

### Files and reusable hooks
- `CombatManager`; `GameplayEffects`; `CardManager`; `RunManager` reward resolution; `ArtifactUpgradeResolver`; existing HUD if needed.

### Dependencies
- Phase 4 implementation handoff was present at entry; acceptance state was not verified. Do not pull later-phase behavior forward.

### Tests and acceptance
- Focused phase5 checks: 7/7 artifact mechanics/metadata tests passed; nested Overflow Rare/Epic draw-chain checks: 2/2 passed. Full EditMode: 387/387 passed; PlayMode: 5/5 passed; zero skips/failures. Unity compilation/domain reload succeeded. Windows player build and human UI/gameplay acceptance were not run. Game scene artifact catalog saved with 42 entries.
- Phase5 behavior is implemented and verified; owner acceptance remains pending.

---

## Phase 6 — Purchase/use hooks, reward tray, Traveler’s Pack tiers + economy artifacts

**Status:** Implemented; pending owner phase acceptance. **Effort:** XL. **Workbook scope:** rel_120/121/123/117/129/125/127/124/131/130/122 (Gilded Blade `rel_128` remains phase 5).

### Entry decisions
- Owner approved on October 2, 2026: Traveler’s Pack Common is 20 Gold and Rare is 30 Gold. Preparation Manual, Scavenger’s Pouch, Field Medic’s Kit, Cashback Token, and Merchant’s Badge are Common / 20 Gold. Scavenger’s Satchel, Golden Vault, Merchant’s Gift, and Alchemist’s Kit are Rare / 30 Gold.
- Preparation Manual grants +2 numeric Block per distinct consumable definition carried at encounter start. The pool lasts for that encounter, is consumed point-for-point by incoming enemy/recovery damage before Shield, and expires at encounter end; rune suits remain distinct definitions. Field Medic’s Kit heals 3 HP on the first successful consumable use each encounter.
- Merchant’s Badge discounts only paid Artifact, Enhancement, and Consumable Shop items. Effective price is 80% rounded to nearest integer with ties up; cached offer identity/content remains fixed and reprices in place without rerolling; Badge does not discount its own purchase. Cashback refunds 15% actual Gold spent with nearest/ties-up rounding, zero/capped for free/actual spend, and no own-acquisition refund.
- Merchant’s Gift counts every three eligible paid item purchases after acquisition across the run; Scavenger’s Satchel counts every three Combat/Elite/Boss wins after acquisition across the run. Both reset on a new run. Golden Vault snapshots Gold after Boss base reward, Bounty Ledger, and existing victory Gold bonuses settle, then grants 10% nearest/ties-up once without compounding itself.
- Backpack shrink is withdraw-only. Rare Pack stacks up to 3 identical consumables/slot with per-item charges, partially-used-first, separate rune suits. Full backpack reward must allow replace/decline; never silent loss. Current inventory has 3 slots, per-item charges, no stacking/general reward tray.

### Executable tasks
- [x] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [x] Implement rel_120 Common Traveler’s Pack: +2 backpack slots, from 3 to 5, still nonstacking. rel_121 Rare: retain five slots; up to 3 identical consumables per slot, separate item charges, consume partially used first, keep different rune suits separate, and on capacity loss withdraw-only. rel_123 Preparation Manual: +2 encounter Block per distinct carried definition. rel_117 Scavenger’s Pouch: first successful in-encounter consumable use draws one. rel_122 Field Medic’s Kit: the same first use heals 3 HP. rel_129 Scavenger’s Satchel: every 3 post-acquisition Combat/Elite/Boss wins offers one random non-enhancement consumable through the tray. rel_130 Alchemist’s Kit: each new map caches three seeded enhancement-consumable choices; enemy drops still exclude them.
- [x] Implement rel_124 Merchant’s Badge: paid Artifact/Enhancement/Consumable items use one displayed 80% nearest/ties-up price before cashback, including in-place cached-offer repricing. rel_125 Cashback Token refunds 15% actual Gold spent with the approved guards. rel_131 Merchant’s Gift counts every 3 post-acquisition paid eligible items across the run and uses the tray. rel_127 Golden Vault snapshots after Boss base/Bounty/existing bonus settlement and grants 10% nearest/ties-up once. Gilded Blade remains phase 5.
- [x] Add success-only hooks at authoritative completed purchase and consumable-use points; failed/cancelled actions emit nothing. Implement inspect/claim/replace/decline reward tray with idempotent grant. No generated reward disappears silently.
- [x] Use existing ownership/resolution hooks first; only narrow stack, reward-offer, and reward-UI state was introduced where no existing seam existed.
- [x] Add focused automated tests before UI wiring; preserve cancel, failure, reset and retry paths.
- [x] Wire five dynamic backpack buttons, stack/charge labels, encounter Block display, and the reward modal into the existing Game scene without replacing Phase 4/5 catalog entries.
- [x] Run regression, inspect logs/diff, update only verified workbook rows, and request phase acceptance.

### Files and reusable hooks
- `RunManager` purchase path/cached offers; `ShopUI`; `CardManager` inventory/use validation; `ConsumableInstance`; `RunStatusUI`; narrowly scoped tray only if existing UI cannot host.

### Dependencies
- Owner explicitly authorized Phase 6 implementation while Phase 5 owner acceptance remained pending. No Phase 7 behavior was pulled forward.

### Tests and acceptance
- Focused Phase 6 mechanics/metadata: 10/10 passed. Related Bread/drop/tray regression was included in the full suite. Full EditMode: 397/397 passed; PlayMode: 5/5 passed; zero skips, failures or inconclusive results. Unity compilation/domain reload succeeded.
- Automated live Game-scene smoke used seed `D227E894`: Common Pack exposed five slots; three Common-tier runes occupied three slots; Rare upgrade compacted them to one three-item stack; five distinct slots filled; a full reward showed one inspectable choice and five explicit replacement buttons; invoking the replacement button removed the selected stack, granted the reward exactly once, cleared `GameplayInputGate`, hid the tray and left the map visible. This is automated interaction evidence, not human gameplay acceptance.
- `Assets/Scenes/Game.unity` contains 53 catalog artifacts with each Phase 6 ID exactly once, five backpack bindings and a fully bound `ConsumableRewardUI`. The 11 workbook rows are marked `Implemented — awaiting phase acceptance` with the approved values/notes; `rel_128` was not changed as Phase 6 content.
- Windows x64 Development build was attempted to `D:/locus/data/temp/Dealbreak-Phase6-Builds/Development/Dealbreak.exe` and failed before output (`Failed`, 3 errors, 1 warning, size 0). Unity 6000.3.24f1 threw `InvalidCastException` in `EditorCompilation.CompleteActiveBuildWhilePumping`; Bee/Tundra then failed to rename digest/state files and reported the backend still running. Editor assemblies remained healthy and `unity_recompile` returned `up_to_date`. Release build was not run because it shares the reproduced blocked player-script compilation stage; no executable is claimed.
- Console classification before the build: no project errors, one unrelated Unity AI account/network warning. Build errors are infrastructure/pipeline diagnostics above, not C# test or Editor-compilation failures. Human UI/gameplay acceptance remains open; no commit or push was performed.
- Acceptance target remains: settlement-based exactly-once hooks; no silent drops, duplicate payouts, shared asset mutation or unsupported persistence. Implementation evidence satisfies the automated target; owner phase acceptance remains pending.

---

## Phase 7 — Other emblem upgrades, growth/map artifacts + Master Thief

**Status:** Implemented (21 non-deferred artifacts + Master Thief); pending owner phase acceptance. Surveyor’s Map (`rel_132`) is explicitly deferred as redundant and remains unimplemented. **Effort:** XL. **Workbook scope:** 21 remaining artifact records plus Master Thief; Surveyor is excluded from implementation.

### Entry decisions
- Owner approved on October 2, 2026: TBD rarity/price convention Common 20 Gold / Rare 30 Gold; +1 permanent kill growth unless specified; Crown +3 max HP on Boss kill plus +3 for zero actual HP lost; +1 action buffs; Warpath +2 encounter Block per consecutive Combat/Elite/Boss win, cap 6, reset by a noncombat; Heart Vitality 1:1, cap 10, spent on next Heart attack; Wanderer’s Boots heal +3 HP and gain +3 Gold once per map after 3 distinct node types; Hammer no-target fallback Retry/Decline; Master Thief bonus actions count as player actions; Master Thief normal Elite reward is 20 Gold.
- Owner approved: Arsenal hand capacity is `floor(1 + 1/2 + ... + 1/n)` for `n` Face cards in hand (0 at n=0), without predecessor effects or forced discards. Kingslayer counts each manually played physical card once and spends one charge per empowered next attack. Hands Epic repeats authored Enhancement on-play effects only, not attack damage, action cost, or bonus-action generation. Balancer’s Scale: ranks below Five are Low; manual Low↔Face alternation grants +1 damage to the next attack, consumed once; repeating the same group clears the stored bonus before that attack.
- Owner deferred Surveyor’s Map because Shops/Elites are already visible and no distinct benefit was approved. No mastery conditions or meta-progression were invented. Growth is run-local and does not mutate shared ScriptableObjects.
- Owner approved strict hand capacity for Diamond Epic. If its selected discard draw cannot fit after retained Rare draw effects, or no discard exists, the action continues and the reason is logged; cancel skips only that draw and does not replay the action.
### Executable tasks
- [x] Inspect current source, data, tests and exact workbook rows; record baseline behavior. The repository contained dirty Phase 4–6 integration files/assets; these were preserved. Common Hammer (`rel_009`) was restored to its original random-reward behavior; Phase 7 Rare Hammer (`rel_140`) is a separate asset.
- [x] Implement `rel_114` Ace Rare and `rel_115` Ace Epic: retain Ace critical chance, return the same physical Ace once per encounter after a critical Ace pair when hand capacity permits, and guarantee the Ace+Face pair critical.
- [x] Implement `rel_108` Diamond Epic: retain Rare draw 2; every third manually played Diamond queues a physical discard-to-hand choice, revalidates identity, honors strict hand capacity, supports cancel/empty-pile skip, and resumes the original action once. Implement `rel_030` Arsenal Epic harmonic hand-capacity formula without lower-tier retention or forced discard. Implement `rel_105` Heart Epic with Rare healing/Gold, 1:1 run-local Vitality capped at 10, next Heart attack spend, and Withering prevention. Implement `rel_116` Mimic Epic with base behavior plus exactly one matching-rank held-effect retrigger without recursion.
- [x] Implement `rel_112` Dwarf Rare retaining the Common bonus action; ranks below Five prepare +1 damage for the next higher-rank attack. `rel_113` Dwarf Epic retains Rare behavior and permanently gives the original owned low-rank source +1 attack only when the prepared higher-rank attack kills. `rel_109` Hands Epic retains Rare behavior, permits four matching ranks, and triggers each card Enhancement on-play effect twice without replaying attacks, costs, or bonus actions. rel_118 Balancer’s Scale: alternate manually played low-rank and Face cards; repeating group resets bonus; Low is owner-approved rank below Five; each manual Low↔Face alternation grants +1 damage to the next attack, consumed once; repeating the same group clears the bonus before that attack.
- [x] Implement `rel_106` Spade Rare: keep doubled block; fully block with one Spade to draw one. `rel_107` Spade Epic retains Rare behavior; each unused Spade Block grants +1 damage to the next attack, consumed once per encounter. `rel_111` Retaliation Epic: counterattack kill permanently increases subsequent counterattack damage by +1, run-local. `rel_139` Crown: Boss kill permanently increases max HP by +3, plus +3 more when actual HP loss is zero; shield/block do not count as HP loss, and max-HP growth does not heal. `rel_138` Kingslayer: against a Boss, 3 distinct manually played physical ranks grant +1 damage to the next attack; consume a charge on attack and repeat within the encounter.
- [x] Implement `rel_135` Hidden Trail: once per map reroll one reachable hidden Event/Risk node to the opposite hidden type using a named deterministic stream; no Boss change, added nodes, or connectivity alteration. Owner-deferred `rel_132` Surveyor’s Map; not implemented because Shops/Elites are already visible and no distinct benefit was approved. `rel_133` Wanderer’s Boots: after 3 distinct node types on a map, heal +3 HP and gain +3 Gold once/map, as explicitly approved. `rel_134` Warpath Banner: consecutive Combat/Elite/Boss victories add +2 encounter Block before Shield, capped at +6; any completed noncombat node resets the run-local streak. `rel_119` Hunter’s Ledger: each actual Elite kill run-locally adds +1 to the first attack of each encounter. `rel_137` Trophy Rack: each actual Elite kill run-locally adds +1 to the first attack against a Boss. `rel_140` Rare Hammer: every 3 nodes after acquisition cache 3 distinct Common Enhancements; select one and an eligible card, preserving one Enhancement/card. With no eligible card, present Retry or Decline.
- [x] Master Thief: 24 HP / 4 ATK / 20 Gold; after first enemy response steals up to 8 actual Gold, announces escape, and flees after the player’s fourth action including bonus actions unless killed. On kill, returns stolen Gold once plus normal 20g Elite reward; flee gives no kill reward; zero Gold does not stop the timer. Authored Elite receives no generic elite modifier.
- [x] Use existing ownership/resolution hooks first; introduce narrow integration seams only when needed.
- [x] Add focused automated tests before UI wiring; preserve cancel, failure, reset and retry paths.
- [x] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [x] Run regression, inspect logs/diff, request phase acceptance.

### Files and reusable hooks
- `ArtifactUpgradeResolver`; `RelicData`; `CardManager`/`GameplayEffects`; `RunManager` kill/map hooks; `RunMapGenerator`/`PathScreenUI`; `EnemyRuntime`; existing upgrade UI only if separately applicable.

### Dependencies
- Phase 6 implementation is present but owner acceptance remains pending; Phase 7 implementation was separately authorized and kept within its approved scope.

### Phase 7 sub-batches and focused acceptance
All seven implementation slices were completed and verified; Surveyor’s Map is excluded by owner decision.
1. **Growth bookkeeping:** Retaliation, Hunter’s Ledger, Trophy Rack and Crown. Focused tests cover lethal counter attribution, actual Elite kill versus flee, Boss HP growth and zero actual HP loss; runtime counters are run-local and assets immutable.
2. **Action preparation:** Dwarf tiers, Balancer and Kingslayer. Focused tests cover source-card identity/permanent growth, distinct-rank manual trigger, charge consumption, repeat-group reset, and encounter cleanup; bonus actions remain single-action transactions.
3. **Card ownership and choices:** Ace tiers, Diamond and Hammer. Focused/Edit/Play tests cover physical Ace identity/capacity/once-per-encounter, Diamond third-manual trigger, strict capacity, cancel and deferred continuation, and Hammer cached choices/target/retry/decline.
4. **Hand engines:** Arsenal, Heart and Mimic. Focused tests cover the owner-approved harmonic formula, no forced discard, Withering-safe Vitality accumulation/spend, matching-rank extra trigger, and nonrecursive effect boundary.
5. **Grouped offense and defense:** Hands and Spade tiers. Focused tests cover four-card legality, exactly two card Enhancement activations without replaying actions/costs, Rare draw-on-full-block, unused-block +1, and next-attack consumption.
6. **Route mechanics:** Hidden Trail, Wanderer and Warpath. Focused tests cover deterministic reachable reroll, no Boss/connectivity change, distinct-node map reset, +3/+3 once-map reward, consecutive Combat/Elite/Boss streak, noncombat reset and Block-before-Shield consumption. Surveyor remains deferred.
7. **Master Thief:** Focused tests cover 24/4/20 stats, up-to-8 actual Gold theft after first response, exactly-once stolen refund plus normal Elite payout, zero-Gold timer, fourth-action escape including bonus action, no kill reward on flee, and no double elite modifier.

### Tests and acceptance
- Tests: Phase 7 focused EditMode 23/23 passed. Full EditMode: 420/420 passed; zero skips, failures, or inconclusive results. Full PlayMode: 5/5 passed; zero skips, failures, or inconclusive results. Unity compilation/domain reload succeeded.
- Acceptance: All 21 non-deferred artifact IDs plus Master Thief have implemented/tested behavior; Surveyor `rel_132` remains deliberately unimplemented. Owner phase acceptance remains pending.
- Evidence: GameController relic catalog has 74 entries; each of the 21 Phase 7 artifact IDs appears exactly once, Common Hammer `rel_009` and Rare Hammer `rel_140` each appear once, Surveyor is absent, and Master Thief is listed once in authored optional elites. Game scene is saved and not dirty after Play smoke. Diamond UI smoke verified physical return, full-hand strict skip/log, cancel-without-draw, input gating, and exactly-once deferred turn continuation. Hammer smoke verified three cached Common options, target application, Retry after no eligible target, and later reward reopening/application.
- Console: 10 merged `Curl error 55: Recv failure: Connection was reset` entries; nearby Editor.log lines show Unity Personal Licensing entitlement requests returning HTTP 404. No project code stack trace was associated. A prior PlayMode test-runner scene-restore error from polling while still in Play was not reproduced after returning to Edit; the final test run completed cleanly. The Unity AI account-service warning was also observed during the session.
- No Windows player build was run for Phase 7. Human gameplay/visual acceptance and owner Phase 7 acceptance remain open. No commit or push was performed.
- Workbook rows have since been updated after confirming the spreadsheet process and lock were gone: the verified Phase 7 patch updated 106 targeted cells across 23 IDs (21 non-deferred artifacts, deferred Surveyor status/notes, and Master Thief) in the latest workbook, preserving all other worksheet cells.

---

## Phase 8 — Blood Ritual, Quest, Infinite Mode

**Status:** Implemented; pending owner phase acceptance. **Effort:** XL. **Workbook scope:** Events/Shops/Scaling and run lifecycle.

### Entry decisions
- On October 3, 2026, the owner approved the implementation baseline for all three features. This supersedes draft workbook values for shop cost, deadlines, and Infinite Mode.
- Blood Ritual (`evt_100`): **Everything** sets max HP to 5, clamps current HP without healing, and grants an eligible Legendary Artifact. **A Pool** costs 15 current HP and grants an eligible Epic Artifact. **A Drop** costs 5 current HP and grants a random owned Artifact’s immediate one-tier upgrade. Paid HP costs are nonlethal and leave at least 1 HP. Reward availability is checked before applying cost; full capacity opens explicit replace/cancel UI and cancellation costs nothing. Empty rarity/tier pools disable the option. The event is offered on a deterministic 1-in-20 eligible Risk roll; chosen rewards are cached for the event transaction.
- Quest (`shop_003`): costs 20 Gold only when the player confirms an objective. Requires a prior successful priced purchase in the same Shop; failed/free purchases do not qualify. The Contract consumes no Artifact slot and only one can be active. Combat: win 2 encounters within 12 maps for 6 Gold. Elite: kill a reachable Elite before leaving the purchase map for a Rare Artifact, or heal 5 HP if no collectible artifact/slot is available. Flawless: win an encounter with zero actual HP lost for 10 Gold. Failure settles once for up to 5 HP and never below 1; map expiry adds no second penalty. Selection can be canceled without charging.
- Infinite Mode is explicitly opt-in after Map 12. Each extra map compounds +10% HP / +4% ATK from the Map 12 baseline, flooring after each step (deterministic decimal arithmetic). The cached 12-boss sequence/abilities repeat by run seed and existing boss reward values remain unchanged; no extra reward scaling is added. Numeric stats and accumulated Gold saturate at `int.MaxValue`; boss/map indexes stop safely at their integer bound. Infinite timing retains a rolling 100-split history. Restarting the same seed resets to normal mode.

### Executable tasks
- [x] Inspect the exact workbook rows and current implementation; preserve existing Phase 4–7 dirty integrations. Baseline included reusable event/shop UI, artifact catalog/upgrade rules, player HP APIs, cached boss deck, and the existing run-result screen.
- [x] Implement and author `evt_100`, add it once to the Risk catalog, and use a named per-node random stream for the approved 1-in-20 appearance roll. Preserve seed determinism and keep map layout/content streams unchanged.
- [x] Implement Ritual HP boundaries, max-HP clamp without healing, eligible Epic/Legendary selection, random one-tier upgrade, full-capacity replace/cancel UI, and payment only after collection commits.
- [x] Add the Quest Contract Shop offer with same-Shop successful-priced-purchase gating, 20 Gold fee on objective confirmation, no-charge cancel, one-active/non-slot runtime state, reachable Elite objective gating, actual-HP-loss tracking, deadlines, exact rewards/failure settlement, and HUD objective/deadline progress. Full-capacity Elite reward resolves to the approved +5 HP fallback instead of silently dropping an Artifact.
- [x] Add explicit Infinite Mode opt-in using the existing result UI, continue the deterministic cached boss deck beyond Map 12, apply per-step scaled stats consistently to runtime and previews, resume run timing, keep map/boss numbering bounded, clamp cumulative combat/player/gold stats, cap timing-history memory, and reset Endless state on same-seed restart.
- [x] Add focused tests for Ritual costs/rewards/replacement/cancel/exactly-once, Quest purchase gate/cancel/commit/reachability/progress/failure/reward fallback, deterministic rare event selection, Infinite opt-in/scaling/overflow/timing/restart, and saturating player/enemy stats.
- [x] Run full EditMode and PlayMode regressions, live Game-scene UI smoke, inspect Console/scene state, and update only verified workbook rows.

### Files and reusable hooks
- `RunEventDefinition` / `RunEventUI`; `RunMapGenerator`; `RunManager` event/shop/result/map lifecycle; `CardManager` Artifact collection and Gold; `PlayerRuntime` / `CombatManager` HP and capped combat stats; `ShopOffer`; `RunContractState`; `InfiniteRunScaling`; `RunTimingStatistics`; `RunStatusUI`; `RunResultUI`.

### Dependencies
- Phase 7 implementation is present, but owner Phase 7 acceptance remains pending. The owner explicitly authorized Phase 8 implementation notwithstanding that pending acceptance.

### Tests and acceptance
- Focused Phase 8 EditMode suite: **20/20 passed**, zero skips/failures/inconclusive.
- Full EditMode: **440/440 passed**; full PlayMode: **5/5 passed**; zero skips/failures/inconclusive. Unity compilation/domain reload completed successfully.
- Live Game-scene smoke on seed `D1977F99`: Contract offer showed the same-Shop purchase prerequisite; a 5g Investment unlocked it; Contract selection charged no fee, cancel returned to the Shop without charge, and confirming an objective then charged 20g. HUD showed `Contract • Encounters 0/2 • due before Map 13`. Forced `evt_100` display opened the real event UI; Everything granted a Legendary Artifact and clamped max HP to 5. The result screen displayed `CONTINUE INFINITE MODE`; clicking it continued on Map 13, resumed timing, and same-seed restart returned to normal mode. This is automated live-UI interaction, not human gameplay acceptance.
- The live Game scene was returned to Edit Mode and remained clean. Console had no project errors; the known Unity account/network warning is unrelated. No Windows player build was run; the project tracker records a prior Unity Bee/Tundra internal pipeline failure, and no new build result is claimed.
- Workbook rows `evt_100`, `shop_003`, and Scaling `13+` now reflect approved values and `Implemented — awaiting phase acceptance`; 15 cells changed across those three exact rows. Workbook status is not owner acceptance.
- Owner acceptance and human gameplay/visual acceptance remain open. No commit or push was performed.

---

## Coverage ledger — 38 tracked artifact records

IDs/names below were read from the current workbook with openpyxl and are mapped exactly once. Phase assignment is sequencing only, not implementation status. Phase 8 adds Blood Ritual/Quest/Infinite Mode, not another artifact row.

| Workbook ID | Workbook name | Planned phase | Tier / rarity as authored | Status |
|---|---|---:|---|---|
| `rel_111` | Retaliation Emblem | 7 | Tier 3 / Epic (upgrade from `rel_022`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_139` | Crown of Endurance | 7 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_138` | Kingslayer's Mark | 7 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_034` | Club Emblem | 5 | Tier 3 / Epic (upgrade from `rel_025`) | Implemented; pending owner acceptance. |
| `rel_123` | Preparation Manual | 6 | Tier 1 / Common (approved) | Implemented; pending owner acceptance. |
| `rel_117` | Scavenger's Pouch | 6 | Tier 1 / Common (approved) | Implemented; pending owner acceptance. |
| `rel_129` | Scavenger's Satchel | 6 | Tier 1 / Rare (approved) | Implemented; pending owner acceptance. |
| `rel_120` | Traveler's Pack | 6 | Tier 1 / Common | Implemented; pending owner acceptance. |
| `rel_121` | Traveler's Pack | 6 | Tier 2 / Rare (upgrade from `rel_120`) | Implemented; pending owner acceptance. |
| `rel_114` | Ace Emblem | 7 | Tier 2 / Rare (upgrade from `rel_014`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_115` | Ace Emblem | 7 | Tier 3 / Epic (upgrade from `rel_114`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_108` | Diamond Emblem | 7 | Tier 3 / Epic (upgrade from `rel_027`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_110` | Overflow Emblem | 5 | Tier 3 / Epic (upgrade from `rel_021`) | Implemented; pending owner acceptance. |
| `rel_126` | Bounty Ledger | 5 | Tier 1 / Common (approved) | Implemented; pending owner acceptance. |
| `rel_125` | Cashback Token | 6 | Tier 1 / Common (approved) | Implemented; pending owner acceptance. |
| `rel_128` | Gilded Blade | 5 | Tier 1 / Common (approved) | Implemented; pending owner acceptance. |
| `rel_127` | Golden Vault | 6 | Tier 1 / Rare (approved) | Implemented; pending owner acceptance. |
| `rel_124` | Merchant's Badge | 6 | Tier 1 / Common (approved) | Implemented; pending owner acceptance. |
| `rel_131` | Merchant's Gift | 6 | Tier 1 / Rare (approved) | Implemented; pending owner acceptance. |
| `rel_136` | Challenger's Crest | 5 | Tier 1 / Common (approved) | Implemented; pending owner acceptance. |
| `rel_137` | Trophy Rack | 7 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_119` | Hunter's Ledger | 7 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_130` | Alchemist's Kit | 6 | Tier 1 / Rare (approved) | Implemented; pending owner acceptance. |
| `rel_140` | Hammer | 7 | Tier 2 / Rare (upgrade from `rel_009`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_030` | Arsenal Emblem | 7 | Tier 3 / Epic (upgrade from `rel_029`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_122` | Field Medic's Kit | 6 | Tier 1 / Common (approved) | Implemented; pending owner acceptance. |
| `rel_105` | Heart Emblem | 7 | Tier 3 / Epic (upgrade from `rel_026`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_116` | Mimic Emblem | 7 | Tier 2 / Epic (upgrade from `rel_017`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_112` | Dwarf Emblem | 7 | Tier 2 / Rare (upgrade from `rel_013`) | Planned |
| `rel_113` | Dwarf Emblem | 7 | Tier 3 / Epic (upgrade from `rel_112`) | Planned |
| `rel_135` | Hidden Trail | 7 | Tier 1 / TBD | Planned |
| `rel_132` | Surveyor's Map | 7 | Tier 1 / TBD | Planned |
| `rel_133` | Wanderer's Boots | 7 | Tier 1 / TBD | Planned |
| `rel_134` | Warpath Banner | 7 | Tier 1 / TBD | Planned |
| `rel_109` | Hands Emblem | 7 | Tier 3 / Epic (upgrade from `rel_020`) | Planned |
| `rel_118` | Balancer's Scale | 7 | Tier 1 / TBD | Planned |
| `rel_106` | Spade Emblem | 7 | Tier 2 / Rare (upgrade from `rel_004`) | Planned |
| `rel_107` | Spade Emblem | 7 | Tier 3 / Epic (upgrade from `rel_106`) | Planned |

**Ledger integrity:** 38 distinct workbook records; 38 phase assignments; 38 unique IDs. Five Phase 5 and eleven Phase 6 rows have verified implementation; owner acceptance remains pending for both phases. The remaining 22 rows retain their planned status. Tier and rarity shown are workbook values or explicit owner approvals.

## Cross-phase invariants

1. Opening a UI is read-only; cached decisions do not consume RNG again.
2. Cancel/failure never charges, consumes, grants, or completes unless workbook explicitly requires it.
3. Every gameplay effect has one authoritative owner and resolution point; UI callbacks/retries cannot duplicate it.
4. Separate authored base stats, map scaling, elite modifiers, ally aura, temporary ability and runtime current stats; test each once and in order.
5. Tier upgrades preserve prior-tier effects only where the workbook says “Keep” (or its upgrade relation explicitly requires it); Arsenal/Club Epic behavior must follow the row definition, not a blanket retention assumption. Authoritative acquisition checks enforce duplicate/capacity rules.
6. Keep stack count, slot count, individual charges and charge-order distinct.
7. Permanent-looking growth is run-local unless separate meta-progression approval exists; never mutate shared ScriptableObjects.
8. Test defeat, victory, restart, node completion, map transition and scene re-entry cleanup.
9. UI description derives from same cached configuration the runtime executes.
10. Make numeric rounding explicit and test boundaries; no repeated-rounding drift.
11. Use deterministic random context/named streams and cache first-generated rewards/options.
12. Infrastructure-blocked tests remain unverified; do not weaken criteria or claim success.

## Final release/handoff gate

- Run all available EditMode/PlayMode tests and report discovered counts, skips and infrastructure failures.
- Build Windows development/release configurations where available; investigate actual Bee errors instead of citing historical builds.
- Smoke seeded run, combat, boss entry description, shop purchase, consumable/reward tray, map route, boss completion, retry/reset and Infinite opt-in if approved. Human interaction is required where interaction acceptance matters; automated callbacks are not human acceptance.
- Compare same-seed boss assignment/retry/re-entry and different-seed stream independence.
- Review final diff/status against original session baseline; update workbook only for verified rows when authorized.
- Handoff implemented scope, open decisions, deferred items, exact evidence/logs/manual checks. Do not auto-commit/push.

## Exclusions

- God content is deferred/unknown; do not invent scope or sequence it into these phases without a separately approved design.
- Do not revive rejected Fire Lantern/Cheater healing/light-to-tier suggestions or superseded boss abilities.
- Do not add speculative save persistence, broad generic frameworks, extra map depths, or unapproved UI redesign. Any staging/commit/push requires separate authorization; never do so automatically.
- Do not treat the 38-row ledger as proof all other workbook content is approved or implemented.
- No row/phase leaves Planned without runtime behavior, UI where applicable, tests, evidence and acceptance.
