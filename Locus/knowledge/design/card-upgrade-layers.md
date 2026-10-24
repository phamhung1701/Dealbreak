---
id: kd_e1908df4-8288-4945-a23d-19658f81dea8
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# Additional card-upgrade layers: deferred

## Approved prototype scope
For GitHub issue [#3](https://github.com/phamhung1701/Dealbreak/issues/3), the user explicitly chose to defer Seals, Editions, and other additional persistent card-upgrade layers. This closes the design-decision issue; it does not mean those features are implemented.

Keep one Enhancement per CardInstance for the current prototype. Existing run-persistent Rune suit changes, permanent attack bonuses, Mirror duplication, and artifact tier upgrades are unchanged. These existing mechanics do not authorize new Enhancement slots or a Seal/Edition system.

## Architecture and migration
Preserve CardInstance identity and the existing immutable CardData/CardEnhancementData definitions. TryApplyEnhancement continues to reject a second Enhancement. No speculative runtime fields, stacking rules, acquisition UI, balance changes, serialized migrations, or save-format changes are required for this deferral.

Artifact tier replacement is separate from card-modification layering; its availability is not a card Enhancement replacement contract. No save/resume schema is introduced by this decision.

## Revisit gate
Revisit after remaining approved content and playtest work, through a new explicitly approved design task. Before implementation, specify layer purpose, stacking/replacement limits, canonical metadata, acquisition/targeting, interaction with the existing Enhancement/suit/permanent-bonus state, Mirror copy rules, run reset/persistence, and migration/save compatibility.

## Validation
Decision checked against Assets/Scripts/Data/CardInstance.cs: one Enhancement field, instance suit override, permanent attack bonus, and rejection of a second Enhancement. This task changes documentation and issue tracking only; runtime tests are not required or claimed as rerun.
