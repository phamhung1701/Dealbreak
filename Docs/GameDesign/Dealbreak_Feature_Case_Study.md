# Feature Case Study: Ace Pairing

> **Dealbreak | Combat feature design | Status snapshot: October 9, 2026**  
> Ace Pairing is implemented and covered by focused EditMode tests. Recorded Game-scene checks exercised Unity EventSystem selection and action-button flows, but not a human mouse/drag session. The critical chance and related balance remain prototype tuning, not validated final balance. The Dealbreak GameData workbook is authoritative for current player-facing terminology; this technical case study retains historical code terminology where noted and does not authorize serialized-ID migration.

## Feature overview

Ace Pairing lets the player commit exactly two hand cards as one attack action when at least one selected card is an Ace. Legal examples include Ace + Eight and Ace + Ace; Two + Eight is not a legal pair. Both cards are consumed together, their separately modified attack values are combined, and the action targets one selected enemy. It is a deliberately narrow break from Dealbreak's one-card combat baseline, rather than an open-ended poker-hand system.

The action is eligible for one critical roll. The default critical chance is configurable at 25%; a successful critical doubles the paired action's damage. On a critical Ace pair, the system attempts to return an Ace to hand if there is room. The critical roll uses a dedicated seeded combat stream, so a repeatable run seed and the same decisions reproduce the result without consuming random values used by maps, shops, or card operations.

## Design objective

Ace Pairing adds a memorable tactical conversion: a low-value Ace can become an enabler for combining two attacks, and a critical can convert that commitment into a burst turn. Its constraint is as important as its upside. The player must have an Ace and a second card, choose both as a single action, target only one enemy, and give up both cards from the current hand.

This makes the pair a meaningful alternative to preserving cards for defense. It gives Aces a distinct combat identity without changing the base value table or making every hand a multi-card puzzle. The intent above is grounded in the behavior and design scope recorded in the project; no playtest finding or balance target is claimed.

## Rules and edge cases

| Situation | Rule |
|---|---|
| Exactly two cards, at least one Ace | Legal Ace Pair action |
| Ace + Ace | Legal; still one action and one critical roll |
| Two non-Aces | Rejected; cards remain in hand and no critical RNG is consumed |
| One card only | Ordinary single-card action; not an Ace Pair |
| Third selected card | Rejected under the Ace-pair selection rule unless a distinct owned Artifact enables a same-rank action |
| Both paired cards carry effects | Each card's applicable on-play effects execute once, in selection order |
| Paired damage | Sum each card's attack after its applicable per-card modifiers; apply action-level rules/caps at their defined stage |
| Critical | One seeded action roll; doubles damage once for the action, not one roll per card |
| Critical with Ace Pair | Attempts to return an Ace, subject to hand capacity and the applicable runtime return rule |
| Target defeated before all staged hits/effects resolve | Later hits against that defeated target stop; there is no automatic retarget |
| Royal Family selection | Uses its separate Artifact-gated rule; do not conflate it with Ace Pair legality |

The action resolves effects at explicit commit boundaries. Card commitment is atomic: validation happens before consuming cards. Action-level effects are not repeated because two cards are present. Individual card effects may run once per card, maintaining selection order. If a different rule such as Hands Emblem enables same-rank groups, that rule has separate eligibility and is not simply “more Ace Pair cards.” For example, a three-card same-rank group is a Hands action, not an Ace Pair critical by default; a separate Ace Emblem can change critical eligibility for any action containing an Ace.

## Player decisions and strategic depth

The pair asks the player to compare immediate damage with card opportunity cost. A player might combine Ace + Eight to remove a dangerous target before it responds, but those two physical cards are no longer available to block separate pending attacks. An Ace alone is weak (base attack 1), yet waiting for a useful partner carries a hand-management cost: hand capacity is limited, encounters attempt only one card draw, and there is no automatic per-turn refill. Using an Ace pair also concentrates damage on a chosen enemy, while a multi-enemy encounter may reward removing the enemy whose attack or ability is most threatening.

Critical chance creates variance on top of a deterministic base sum. This can reward taking a calculated burst opportunity, but it can also make expected damage harder to read. The Ace return-on-critical rule partly offsets the two-card spend; it is not guaranteed value because the critical may fail, hand capacity may prevent the return, and exact interaction details must follow the runtime implementation. Artifacts and Enhancements further alter the comparison: a paired card's suit or card-local Enhancement can make the sum more attractive, and build effects can change critical chance, draws, healing, Gold, or action-level caps.

## Interactions with existing systems

- **Card value and modifiers:** each card uses its own rank base value and matching modifier pipeline. Current documented numeric stacking is base plus card-local flat modifiers, multipliers, then Artifact flat bonuses, with action-level modifiers resolved afterward.
- **Defense and HP:** the paired action spends two cards in one commitment; defense uses cards to cover distinct pending attacks. This creates the central offense-versus-defense trade.
- **Multi-enemy targeting:** both cards attack the selected exact enemy. A lethal result does not redirect remaining damage to another enemy.
- **On-play effects:** each paired card's effects are processed once in selected order. An Ace-pair action counts once for action-trigger effects such as Golden's in-hand Gold check; multi-hit likewise must not multiply action-level triggers.
- **Critical modifiers:** default critical eligibility belongs to Ace pairs and explicit effects such as Lethal. Ace Emblem overrides chance to 50% for an attack action containing an Ace, including a single-Ace or Ace-containing Hands action; it still uses one action-level roll.
- **Action caps and other build rules:** where Dagger or Hands rules impose a total action cap, the cap precedes critical multiplication. A critical doubles the capped result. Same-rank Hands groups remain a separate Artifact-enabled action family.
- **Seeded run:** critical randomness is isolated from map, shop, and card streams. Invalid pairs and ordinary single-card plays do not consume the critical stream.

## Implementation and verification status

**Implemented:** pair validation and selection; two-card atomic discard; summed per-card damage; action-level seeded critical; critical damage doubling; Ace return attempt after a critical pair; ordered per-card commit effects; interaction with separate Hands and Royal Family rules.

**Evidence reviewed:** `Assets/Scripts/Combat/GameplayEffects.cs`, `Assets/Scripts/Managers/CombatManager.cs`, `Assets/Scripts/Data/CardData.cs`, and `Assets/Tests/EditMode/Phase7CCombatPairTests.cs` plus selection-eligibility tests. Focused tests cover pair damage/consumption, Ace + Ace and invalid non-Ace pairs, no RNG consumption for invalid/single actions, guaranteed criticals, effect ordering, and damage feedback. The September 24 development handoff records an EventSystem-dispatched Game-scene check for Ace + Four selection, third-card rejection, action-button commit, critical damage, and subsequent ordinary play. It explicitly does not claim human mouse/drag validation.

The project was already dirty at the October 9 research snapshot, including user-owned changes to CardManager and UI/card-view files and a newly added direct-hand-targeting test. Those edits are not attributed to Ace Pairing and were not tested or represented as accepted behavior by this case study.

## Balance considerations

1. **Burst versus card opportunity cost:** if the critical upside dominates the cost of losing two cards, pairing may become the automatic best action. If the cost dominates, the mechanic may rarely be used.
2. **Critical variance and expectation:** the 25% default and ×2 critical create swing without changing non-critical damage. The 50% Ace Emblem override can sharply increase the value of builds that can access Aces; its availability and price should be evaluated alongside other critical effects.
3. **Ace return and capacity:** returning an Ace can reduce the apparent cost of a successful pair. Its conditional nature, hand-cap interaction, and timing should remain visible so players do not infer a guaranteed refund.
4. **Modifier stacking:** per-card multipliers, pair summation, action caps, and critical doubling can be difficult to predict. A consistent ordering and clear attack preview reduce surprise; tests should protect each boundary.
5. **Target selection in groups:** concentrating damage is valuable, but no retarget after lethal should be communicated clearly, particularly when effects produce more than one hit.
6. **Run pacing:** seeded repetition supports controlled balance comparisons, but automated tests do not establish enjoyable critical frequency, pair availability, or full-run pacing. Those require recorded human playthroughs across representative seeds.

No numerical adjustment is proposed here. Current work establishes the rule and regression boundaries, not final balance.

## Potential iteration opportunities

- Observe a natural run and record when pairs are available, chosen, and withheld for defense; collect qualitative reasoning rather than inventing win-rate or satisfaction claims.
- Test readable selection states for “one Ace required,” paired damage preview, critical chance source, and the conditional Ace-return outcome before adding more combination rules.
- Evaluate whether the Ace-return feedback should show the exact returned card and capacity condition at resolution.
- Compare a few controlled critical chance values only after a meaningful run-level playtest identifies a problem. Preserve the seeded critical stream to make comparisons reproducible.
- Keep pairing distinct from Hands and any future poker archetype; add broader combinations only when each has a clear role, eligibility boundary, UI explanation, and measurable playtest question.

The next iteration should prioritize comprehension and evidence, not expanding the number of combinations by default.
