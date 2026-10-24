---
id: kd_3bf08839-a0d3-4ce0-af24-4f533deb510a
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# Dealbreak shared gameplay-effect architecture

Implemented September 25–26, 2026 for the canonical Artifact/Enhancement content migration and careful incremental expansion. This document describes the code that exists now, its deterministic contracts, and deliberate v1 limits. Add content one verified increment at a time; the draft workbook alone does not establish implementation or permission to bypass regression gates.

## Goals and boundaries

The architecture provides one typed foundation for effects sourced by owned Artifacts and card-local Enhancements. It separates immutable ScriptableObject definitions from run-owned mutable state, centralizes attack/block calculation for preview and execution, and routes reactive combat effects through deterministic queues. It intentionally avoids both ID/name dispatch and a generic scripting language.

V1 now includes canonical Tier I `rel_001`–`rel_014`, `rel_016`, `rel_019`, `rel_023`, `rel_028`, and `enh_001`–`enh_004`, `enh_007`, `enh_008`. Typed turn-grant and same-rank multi-card rule modifiers are implemented only for rel_011–rel_013. Tier upgrades, rarity distribution, other poker/rank actions, effective suits, overflow play, and remaining workbook content are not implemented.

## System responsibilities

- `Assets/Scripts/Combat/GameplayEffects.cs`
  - Defines effect kinds, triggers, categories, typed conditions and source/context types.
  - `GameplayEffectResolver` is the shared modifier/reactive foundation for both Artifacts and Enhancements.
  - `ArtifactRuntimeInstance` owns one acquired Artifact definition plus run-local `GameplayEffectState`.
  - A `CardInstance` is the runtime source for its attached Enhancement and owns its own `GameplayEffectState`.
  - `GameplayRuleModifierData` is a separate typed rule representation with inline `ExtraTurnRuleDefinition` and `SameRankMultiCardRuleDefinition` variants. CombatManager validates/executes only the rules required by Cheater, Hands and Dwarf; it is not a general scripting language.
- `Assets/Scripts/Data/RelicData.cs`
  - Immutable Artifact authoring metadata: legacy runtime ID, canonical ID, display metadata, rarity, tier, parent canonical ID, capacity category, typed effects, and optional typed rule modifiers.
  - Existing legacy prototype hook fields remain only as a compatibility adapter for definitions without typed effects. If `effects` is non-empty, typed effects are authoritative.
- `Assets/Scripts/Data/CardEnhancementData.cs`
  - Immutable Enhancement authoring metadata and typed effect/rule definitions.
  - Legacy attack/defense/heal/draw fields are likewise used only when no typed effects exist.
- `Assets/Scripts/Managers/CardManager.cs`
  - Owns Artifact definitions and the run-local `ArtifactRuntimeInstance` map, five-slot Persistent capacity, NonSlot exemption, Enhancement attachment and stable CardInstances.
  - `Reset` clears owned definitions and Artifact runtime state. A rebuilt run creates fresh CardInstances and fresh Enhancement state.
- `Assets/Scripts/Data/CardInstance.cs`
  - Keeps stable per-run identity, one Enhancement definition, and run-local Enhancement effect state.
  - Card-local attack/block flats are resolved through the shared effect resolver.
- `Assets/Scripts/Managers/CombatManager.cs`
  - Calls the shared resolver for attack/block preview and execution.
  - Creates authoritative card-commit, player-turn-start and completed-player-action contexts and uses `CombatReactionQueue` for reactive effects.
  - Owns the FIFO for distinct extra-turn grants and routes each grant through the same `BeginPlayerTurn` lifecycle as a normal turn.
  - All player-turn entry paths used by current combat call the same `BeginPlayerTurn` boundary, including encounter start, full defense, remaining-damage resolution and zero-attack retaliation.
- `Assets/Scripts/Combat/CombatReactionQueue.cs`
  - Provides bounded deterministic ordering. V1 adds a `PlayerTurnStarted` phase.
- `Assets/Editor/GameDataGenerator.cs`
  - Retained for compatibility, but updated so an accidental explicit invocation would recreate the eight canonical definitions rather than restore conflicting legacy effects. It was not run for this migration.

## Shared effect source model

`GameplayEffectSource` is either:

1. an `ArtifactRuntimeInstance`, active because its definition is owned; or
2. a stable `CardInstance`, active only for that card's attached Enhancement and only where the trigger semantics make that source relevant.

Both source types expose the same effect definitions, source category, display metadata and runtime-state extension point. Artifact and Enhancement adapters are thin; there is no separate Relic effect engine and Enhancement effect engine.

ScriptableObject definitions are not mutated during a run. Mutable counters, charges and pity state belong in `ArtifactRuntimeInstance.State`; per-card accumulated Enhancement state such as future Devouring scaling belongs in `CardInstance.EffectState`. `GameplayEffectState` currently offers indexed counters; Hammer node progress and Cheater's encounter-once state are run-owned examples. The eight initial core definitions do not need mutable state.

## Effect categories

### Numeric pipeline modifiers

Current typed kinds:

- `FlatAttack`
- `AttackMultiplier`
- `FlatBlock`
- `BlockMultiplier`
- `FlatActionDamage` and `ActionDamageCap` (action aggregate; cap is one budget across all hits)
- `IncomingCombatDamageReduction` (each player combat hit, before Shield)
- `CriticalChanceOverride` (action-scoped percentage resolved for a qualifying committed card action)
- `HandSizeBonus` (passive capacity modifier from active typed sources)
- `FlatAttack` with `InHandAttackCalculated` (source-scoped held Enhancements added through the same attack calculation used by preview and execution)

Card-local/suit attack modifiers, thresholded bonuses, action damage, and incoming damage are distinct stages. Their evaluation is side-effect-free and consumes no RNG.

### Reactive effects

Current typed kinds include:

- `Heal`
- `Draw`
- `BonusGold` at EncounterWon and action-level AttackCommitted boundaries.
- `CounterDamage` at a successful typed `AttackBlocked` boundary

The compatibility adapter also maps the four still-supported prototype families used by non-migrated definitions/tests: reduce enemy attack by card value, recycle discard by card value, and draw by card value. Canonical Heart, Diamond and Spade definitions do not use those legacy effects.

### Rule modifiers

Rule-changing mechanics do not pass through numeric/reactive `Apply` behavior. `GameplayRuleModifierData` is an inline typed definition with narrowly supported variants: `ExtraTurn` and `SameRankMultiCard`. The first is resolved only after an authoritative completed player action; the second participates in selection validation and classifies the resulting action. No name/ID branch is used. Other rule types remain unsupported until separately approved and integrated at their owning state-machine boundary.

## Implemented lifecycle contexts

V1 introduces only boundaries required by the migrated content:

- **AttackCalculated / DefenseCalculated:** side-effect-free card value calculation using the source CardInstance and owned Artifact sources. Attack calculation uses a pre-threshold qualifying value; thresholded Sword bonuses are applied only afterward.
- **ActionDamageCalculated:** sums committed cards, applies typed action-flat modifiers, and supplies the tightest action-total cap. Combat applies this cap as one pre-critical requested-damage budget across hits.
- **InHandAttackCalculated:** preview and execution share the same CardManager hand-zone membership; typed `FlatAttack` sources from eligible Enhancements in hand apply to each attack card calculation.
- **HandCapacityCalculated:** CardManager resolves base capacity plus typed owned Artifact/current-hand Enhancement modifiers; draw/deal/refill clamping and RunStatusUI use this value.
- **IncomingDamageCalculated:** player-owned Artifact reductions are evaluated for each combat `DamageRequest` before `CombatResolver` checks Shield; direct Event/Run HP costs do not enter this pipeline.
- **EncounterStart:** the existing baseline draw resolves first, then a separate bounded deterministic reaction phase processes Tome and any other authored encounter-start effects. Current Tome attempts Draw 1 after baseline, subject to hand capacity.
- `AttackCommitted` is evaluated once for a successfully committed attack action after the committed cards leave hand but before their `CardCommitted` reactions or damage hits. The captured in-hand Enhancement-source snapshot is stable through the rest of the action; drawn or otherwise newly entering cards do not join it.
- **CardCommitted:** once per committed card, not once per hit. Context carries action ID, card, target, card order, hit count, managers and a stable negative committed-card hit index. Heart, Diamond and Quickdraw use this boundary.
- **CriticalChanceCalculated:** side-effect-free action-scoped resolution over committed CardInstances; matching overrides are applied deterministically, then the existing independent `combat-critical` stream is rolled at most once for that action. Ace Emblem can enable qualifying Ace-containing actions.
- **AttackBlocked:** after defense cards are authoritatively discarded and distinct pending attacks are matched/removed, each blocked attack carries its exact snapshotted attacker, blocking CardInstance, and blocked value to deterministic typed reactions. Retaliation counter-damage resolves through the existing CombatResolver/queue; taking remaining damage or consuming player Shield is not this trigger.
- **MapReady:** RunManager supplies authoritative run/map state to typed reveal effects; Lantern reveals one eligible hidden node. The selection policy uses stable candidate identity/order, leaves topology and always-visible/Elite rules unchanged, and does not use combat RNG.
- **NodeCompleted:** RunManager first commits node completion and its completed-node count, then resolves typed Artifact effects. Hammer advances its counter in run-owned Artifact state and, at each third completed node, deterministically chooses an eligible unenhanced owned card and an authored Common Enhancement through the dedicated Artifact/content seeded streams; application goes through CardManager's authoritative API. No eligible card is a no-op.
- **CompletedPlayerAction:** after a legal attack action has fully resolved its card reactions, hits and enemy defeat checks, CombatManager snapshots committed CardInstances and completed-player-turn number and asks typed Artifact rules for turn grants. This is not a general event subscription.
- **PlayerTurnStart:** every normal or granted player turn enters the same boundary; Healing Light/Mending therefore execute naturally without grant-specific calls.
- **EncounterWon:** deterministic Artifact reward summary for existing Victory Draught and Golden Compass compatibility.

Existing damage/hit/enemy lifecycle stays with `CombatActionContext`, `DamageRequest`, `DamageResult`, `CombatResolver` and `CombatReactionQueue`. Future HitResolved or EnemyKilled content should integrate there rather than subscribing uncontrolled listeners. DefenseCommitted, DrawAttempt, HandEnter and HandLeave effect contexts remain unimplemented until approved content requires them.

## Deterministic ordering

No effect application relies on Unity object discovery, scene hierarchy, dictionary enumeration, display name, runtime ID or event subscription order.

- Committed cards execute in the authoritative selected-card order already supplied to `TryPlayCards`.
- For one committed card, owned Artifact sources are enqueued in acquisition/list order, then the card's Enhancement source.
- Effects inside one source execute in serialized array order.
- `CombatReactionQueue` orders by phase, source category (Artifact before Enhancement), source order, handler/effect index, priority, hit index and action ID, with a bounded operation limit.
- Player-turn-start Artifacts use acquisition order; Mending Enhancement sources use authoritative hand-zone order, not Transform order.
- Completed-action rule evaluation follows owned Artifact acquisition order, then each Artifact's serialized rule order. Each qualifying grant is appended separately to a FIFO; grants are not collapsed. A dequeued grant calls the ordinary PlayerTurnStart path. When Cheater and Dwarf both qualify, Cheater (first Artifact) grants first and Dwarf remains queued until that granted turn completes; terminal Victory/Defeat clears queued grants. Cheater's encounter counter lives on its `ArtifactRuntimeInstance.State` and is cleared at encounter start. Dwarf has no persistent counter: all committed ranks must be below Five, at most one grant per action, and each later qualifying action can grant again.
- `AttackCommitted` snapshots in-hand reactive sources once after card removal; Artifact sources resolve by acquisition order, then Enhancement sources by hand order, and each effect in serialized order. This action-level event does not multiply for Ace pairs or multi-hit actions and consumes no RNG.
- `InHandAttackCalculated` resolves in Artifact modifier order after card-local attack flats and before Artifact attack multipliers/flats; in-hand Enhancement flats are summed in authoritative Hand order and affect each card's calculation identically in preview and execution. The calculated source set is the hand at attack-calculation time, before selected cards are discarded.
- `HandCapacityCalculated` resolves deterministically from base capacity plus Artifact acquisition/effect order, then current-hand Enhancement order; runtime ScriptableObject definitions remain immutable.
- Attack-blocked reactions are ordered by source category (Artifact before Enhancement), source order, effect order, then stable encounter-order block index. Each blocked pending attack triggers once and targets its exact recorded `EnemyRuntime`; a defense card cannot create a second trigger for the same attack. Player Shield/aggregate TakeRemainingDamage does not emit AttackBlocked.
- Hands selection derives from owned typed rule definitions, not UI state. Without Hands the existing one-card and two-card Ace-pair rules remain; with Hands, same-rank selections can reach three cards. Invalid rank extension/fourth card is rejected. A valid two-card Ace pair retains existing seeded critical behavior; with Ace Emblem, a three-card same-rank action containing Aces also gets the Artifact's 50% action-level critical chance (it remains a Hands action, not an Ace-pair action).
- Map and node effects resolve only after authoritative RunManager lifecycle boundaries. Lantern's deterministic reveal does not mutate topology or consume combat RNG. Hammer processes a node completion after the authoritative completed-node count is incremented, and random choices use dedicated explicitly named Artifact/content streams rather than combat-critical streams.
- Encounter-start effects execute after the baseline draw in Artifact acquisition order and then hand Enhancement order. Incoming reductions iterate owned Artifact order; action flats sum deterministically and multiple caps use the minimum, independent of purchase order.


- Numeric modifiers do not consume RNG. Reactive applicability checks do not consume RNG. Future random effects must request an explicitly named run-seeded stream and document whether consumption is once per action, source, card or hit.

## Conditions and filters

`GameplayEffectDefinition.conditions` is a small composable typed list. V1 condition kinds are:

- `CardSuit`
- `CardRank`
- `EnhancedCard`
- `AttackValueGreaterThan` (integer attack threshold; qualifying value is snapshotted after ordinary card-local/suit/base modifiers, before thresholded bonuses)

All conditions must match. Enhancement source identity is enforced by the source adapter: a card-local effect applies only to its source CardInstance. Conditions are data and never compare display names or IDs. Further rank or HP-threshold conditions require approved content and defined authoritative snapshots.

## Modifier ordering and preview parity

`GameplayEffectResolver.CalculateAttack` and `CalculateBlock` are the only active calculation path used by `CombatManager`, and `CardView` obtains its preview through those manager methods.

V1 ordering is:

1. base card attack/block value;
2. card-local flat Enhancement modifier;
3. eligible in-hand Enhancement flat attack modifiers, summed in authoritative Hand order (Auxiliary's own card is included while it remains in the calculation-time hand);
4. card-local multiplier, if a future Enhancement supplies one;
5. matching Artifact multipliers;
6. matching Artifact flat bonuses;
7. test attack-threshold conditions against the resulting pre-threshold value, then add qualifying bonuses (Sword cannot self-qualify);
8. aggregate committed card damage, add action modifiers and enforce a **single action-total cap** over requested damage across all hits;
9. resolve any matching action critical-chance override in stable source order; if the action is critical-eligible, consume at most one seeded roll for the whole action;
10. multiply by the existing critical result **after** the cap (a 10-point Dagger/Hands budget becomes 20 on a critical action).

This preserves the prior attack contract `(base + Enhancement flat) × Artifact multipliers + Artifact flats`. Defense remains `(base block + Hardened +3) ×2` for a qualifying Spade. Club Emblem multiplies only its qualifying card before Ace-pair damage is summed. Sword checks each committed card independently after ordinary card/suit modifiers, before its own +10, action caps and crits. Dagger's +3 and cap operate at action scope, never once per card; on a staged multi-hit action, each requested hit consumes the same budget even when Shield absorbs it. Incoming player combat damage is reduced per actual hit (Leather −3, floor zero) **before** Shield; direct Event HP costs bypass this combat path. Recover resolves the combined surviving enemies' attack as one damage instance, applies incoming combat modifiers first, then consumes at most one Shield charge only when positive residual damage remains. If the player survives, it makes exactly one DrawToHand(1) request; it does not repeat enemy attacks or emit Retaliation. Direct Run/Event HP costs bypass Shield.

## Canonical Tier I content and legacy crosswalk

Unity GUIDs, asset paths, scene references and cached-offer runtime IDs were preserved. Canonical IDs are explicit metadata rather than inferred from names.

| Canonical | Asset | Legacy runtime ID retained | Migrated typed behavior |
| --- | --- | --- | --- |
| `rel_001` Club Emblem | `Assets/Data/Relics/ClubPower.asset` | `club_power` | Per-card Club attack ×2. |
| `rel_002` Heart Emblem | `Assets/Data/Relics/HeartPower.asset` | `heart_power` | Heal 1 per committed Heart card; no discard recycle. |
| `rel_003` Diamond Emblem | `Assets/Data/Relics/DiamondPower.asset` | `diamond_power` | Draw up to 2 per committed Diamond; no rank-based count. |
| `rel_004` Spade Emblem | `Assets/Data/Relics/SpadePower.asset` | `spade_power` | Spade block ×2 after card-local flats; no enemy-ATK reduction. |
| `enh_001` Sharpened | `Assets/Data/Enhancements/Sharpened.asset` | `sharpened` | This CardInstance has +3 attack. |
| `enh_002` Hardened | `Assets/Data/Enhancements/Reinforced.asset` | `reinforced` | This CardInstance has +3 block; display name is now Hardened. |
| `enh_003` Mending | `Assets/Data/Enhancements/Mending.asset` | `mending` | Heal 2 at each player-turn start while this CardInstance is in hand; no heal on play. |
| `enh_004` Quickdraw | `Assets/Data/Enhancements/Quickdraw.asset` | `quickdraw` | Draw 1 when this CardInstance is committed. |
| `enh_007` Golden | `Assets/Data/Enhancements/Golden.asset` | `enh_007` | Each Golden still in the post-discard attack-hand snapshot grants 2 Gold once per committed attack action. |
| `enh_008` Auxiliary | `Assets/Data/Enhancements/Auxiliary.asset` | `enh_008` | Each Auxiliary source in hand adds +3 flat attack to every card's calculation (including its own card while still in hand at calculation time). |

The original eight Artifact/Enhancement core entries are Common, Tier 1, with no parent ID; Golden (`enh_007`) and Auxiliary (`enh_008`) are additional Rare Tier I Enhancements. The listed authored Artifacts follow. Tier replacement and rarity-based offer distribution remain unimplemented; `Sword` carries Rare metadata but is offered by the unchanged Shop system, which does not use rarity weighting. Spreadsheet prices are blank for the listed content; added prices are temporary prototype values, not canonical balance.

| Canonical | Authored asset | Rarity / tier | Temporary Shop price | Typed behavior |
| --- | --- | --- | --- | --- |
| `rel_005` Leather Armor | `Assets/Data/Relics/LeatherArmor.asset` | Common / 1 | **15g** | Reduce each incoming player combat hit by 3, floor zero before Shield; no Event-cost reduction. |
| `rel_006` Dagger | `Assets/Data/Relics/Dagger.asset` | Common / 1 | **15g** | +3 after card aggregation; cap the entire action at 10 before criticals. |
| `rel_007` Tome | `Assets/Data/Relics/Tome.asset` | Common / 1 | **18g** | Encounter-start Draw 1 after baseline draw, respecting hand capacity. |
| `rel_008` Lantern | `Assets/Data/Relics/Lantern.asset` | Common / 1 | **18g** | MapReady reveals one deterministically selected eligible hidden node without changing topology. |
| `rel_009` Hammer | `Assets/Data/Relics/Hammer.asset` | Common / 1 | **20g** | Every third completed node, apply a random authored Common Enhancement to a random eligible card using independent Artifact/content streams. |
| `rel_010` Healing Light | `Assets/Data/Relics/HealingLight.asset` | Common / 1 | **18g** | Heal 2 at each authoritative PlayerTurnStart. |
| `rel_011` Cheater Emblem | `Assets/Data/Relics/CheaterEmblem.asset` | Common / 1 | **20g** | After first completed player turn per encounter, enqueue one extra turn. |
| `rel_012` Hands Emblem | `Assets/Data/Relics/HandsEmblem.asset` | Common / 1 | **20g** | Allow 2–3 same-rank cards; apply action-total cap 10; no three-card Ace crit. |
| `rel_013` Dwarf Emblem | `Assets/Data/Relics/DwarfEmblem.asset` | Common / 1 | **18g** | After an action where every committed rank is below Five, enqueue one extra turn. |
| `rel_014` Ace Emblem | `Assets/Data/Relics/AceEmblem.asset` | Common / 1 | **20g** | Any attack action containing an Ace uses 50% critical chance, including single Aces and 3-card Hands; one roll/action. |
| `rel_016` Retaliation Emblem | `Assets/Data/Relics/RetaliationEmblem.asset` | Common / 1 | **20g** | Each successfully blocked pending enemy attack triggers 5 reactive damage against that exact attacker. |
| `rel_019` Chain Armor | `Assets/Data/Relics/ChainArmor.asset` | Rare / 1 | **25g** | Reduce each incoming player combat hit by 8 before Shield, floor zero; direct non-combat HP costs bypass it. |
| `rel_023` Sword | `Assets/Data/Relics/Sword.asset` | Rare / 1 | **25g** | Each played card whose pre-Sword modified attack exceeds 8 gains +10. |
| `rel_028` Arsenal Emblem | `Assets/Data/Relics/ArsenalEmblem.asset` | Common / 1 | **20g** | Passive +1 maximum hand capacity, calculated by the shared typed hand-capacity resolver. |

Prices in the added Artifact table are temporary prototype values because spreadsheet prices are blank. The rel_011, rel_012, rel_014, and rel_016 Common prices are **20g** each; rel_013 Dwarf Emblem is **18g**, and Arsenal (`rel_028`) is **20g**. Chain Armor (`rel_019`) is **25g**. Golden (`enh_007`) and Auxiliary (`enh_008`) each have temporary **20g** prices. The unchanged Shop does not use rarity weighting. These assets use canonical IDs as runtime `id`s, occupy Persistent slots, and are registered in the existing Game scene catalog. Rule data is serialized inline in each Artifact definition, not separate subassets. Existing assets' GUIDs/references are unchanged. `Assets/Editor/GameDataGenerator.cs` was not run for the content batches.

## Adding a simple Artifact

1. Confirm the mechanic fits an existing numeric or reactive trigger. If it changes legal actions, turns, targeting, zones or map rules, use the rule-modifier path instead.
2. Create or update a `RelicData` asset without changing another asset's GUID.
3. Set a unique legacy/runtime `id`, canonical spreadsheet `canonicalId`, display metadata, rarity, tier, explicit `upgradeFromId`, capacity category, and typed `effects` in deliberate serialized order.
4. Compose typed conditions; do not add ID/name checks to managers.
5. Leave legacy hook fields neutral when typed effects are present.
6. Add the definition deliberately to `GameController.relicCatalog`; files in the folder are not auto-discovered.
7. Add focused calculation/reaction/order/reset tests and Shop/capacity regression.

## Adding an Enhancement

1. Create or update a `CardEnhancementData` asset with canonical metadata and ordered typed effects.
2. Use numeric effects for card-local attack/block values and reactive triggers only where that source CardInstance is authoritative.
3. Leave legacy fields neutral when typed effects exist.
4. Add it deliberately to `GameController.enhancementCatalog`; Shop/Upgrade offers remain deterministic and target-neutral.
5. Keep mutable per-card progress on `CardInstance.EffectState`, never on the shared asset.
6. For reactive effects that depend on other Enhancements being in hand at attack time, use the action-scoped `AttackCommitted` snapshot; for numeric in-hand modifiers, use `InHandAttackCalculated` so preview and execution share the same authoritative hand-source calculation.
7. Verify one-Enhancement-per-card, exact target identity, preview/execution parity, hand/deck/discard trigger semantics and reset.

## When a mechanic is a Rule Modifier

Use a Rule Modifier rather than a normal effect if the mechanic changes what is legal or how the state machine advances, for example:

- grants or suppresses turns;
- permits same-rank, poker or five-card actions;
- changes a card's effective suit;
- plays overflow cards automatically;
- changes target count or selection rules;
- changes ownership/zone legality rather than reacting after an authoritative action.

A rule must integrate with its owning manager's validation and execution boundary. It must not masquerade as a numeric modifier, invoke UI commands, or mutate Transform/UI state.

## Future extension points and deliberate v1 limits

- **Counters/charges:** use source runtime `GameplayEffectState`; define reset scope and stable effect-key migration before content uses it. Current indexed counters are sufficient as an extension point but have no save/version migration contract.
- **Devouring/per-card scaling:** store accumulated bonus on the stable CardInstance state, separated from the Enhancement asset so future replacement can preserve it if approved.
- **Map/node hooks:** Lantern's typed `MapReady` reveal selects one eligible hidden node deterministically without changing topology. Hammer uses the typed `NodeCompleted` boundary after RunManager commits each node and owns its counter/random choices on the acquired Artifact runtime instance, with dedicated independent Artifact/content streams.
- **Defense reactions:** `AttackBlocked` resolves once per matched defense assignment with its authoritative blocking CardInstance, blocked amount, and source EnemyRuntime. Artifact/Enhancement reactions are deterministic; reactive counter-damage is a normal queued damage request. Multi-enemy block matching is source-preserving. Shield absorbing aggregate leftover damage does not trigger this event.
- **Extra turns:** current typed queue supports Cheater's first-completed-player-turn-per-encounter and Dwarf's all-ranks-below-Five-per-action contracts. Other turn grants, suppression, stacking priorities and save/load migration require explicit design and focused state-machine handling; do not bypass PlayerTurnStart.
- **Poker/effective suit:** Hands is the only implemented non-Ace multi-card rule (same rank, 2–3 cards, action cap 10). Add other combinations only through typed validation/action semantics; never infer a generic poker evaluator from Hands.
- **Legacy compatibility:** legacy fields remain for the four non-migrated prototype Artifacts and tests that create definitions in memory. They are adapted into the shared resolver only when no typed effects exist. This is a migration bridge, not a second active path for canonical content.
- **Unsupported v1 triggers/rules:** no typed draw-attempt, hand-enter/leave, enemy-kill or further hit-resolved content; no additional random-effect families beyond Hammer's authored Common-Enhancement grant; no effective-suit, overflow or further poker/rule execution; no tier upgrade/replacement UI or save/load format.

## Verification checkpoint

- First canonical core migration: focused **8/8**, full EditMode **160/160**, PlayMode **1/1**; historical checkpoint.
- Four-Artifact batch: focused `ArtifactContentBatch1Tests` **11/11 passed**, full EditMode **171/171 passed** with zero failures/skips/inconclusive, and discovered PlayMode Shield/multi-hit **1/1 passed**.
- Game-scene accelerated smoke: Tome hand 6→8 after baseline+additional draw; Leather converted 5 incoming to 2 HP loss, and a staged Shield consumed the remaining post-reduction hit; Dagger normal five dealt 8, Ace-pair capped critical dealt 20; Sword eight dealt 8, ten dealt 20 and Club+Sharpened five dealt 26. No natural twelve-map/economy playthrough is claimed.
- Golden increment: focused `GoldenEnhancementTests` **5/5**, full EditMode **214/214**, discovered PlayMode **1/1**. The typed `AttackCommitted` snapshot pays once per in-hand source per action, excludes the played Golden and discard-zone Golden, and does not multiply per hit or Ace-pair card. The serialized Golden asset/catalog entry were already present when inspected and were preserved; the Game scene was later saved after confirming it was clean for Arsenal/Auxiliary registration. No interactive Shop/combat smoke was performed.
- Auxiliary/Arsenal increment: focused `AuxiliaryAndArsenalTests` **6/6**, full EditMode **220/220**, discovered PlayMode **1/1**. Auxiliary's in-hand `FlatAttack` sources match preview/execution, including source card, discard exclusion and Ace-pair calculations. Arsenal changes base hand capacity 8→9 and reset restores 8. The final Game scene/catalog was verified clean; no interactive Shop/combat smoke was performed. Final warning/error query: **0**; one unrelated Unity AI Toolkit account-service warning appeared earlier in the session.
