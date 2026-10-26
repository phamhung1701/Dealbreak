---
paths:
  - "Assets/Scripts/Combat/**/*.cs"
  - "Assets/Scripts/Managers/**/*.cs"
  - "Assets/Scripts/Data/**/*.cs"
  - "Assets/Scripts/Randomness/**/*.cs"
  - "Assets/Scripts/Persistence/**/*.cs"
---

# Gameplay Code Rules (Dealbreak)

`CLAUDE.md` (determinism, combat invariants, architecture contracts) is the
authority here. Where a template default below disagrees, `CLAUDE.md` wins.

- Balance numbers come from `GameBalance.Current` (`Resources/GameBalance`) or
  typed ScriptableObject data, never literals.
- Runtime state lives on runtime objects, not on ScriptableObject definitions.
- Randomness uses its own `RunRandomContext.CreateStream`; previews never
  change state or consume RNG.
- No content-ID or name checks in managers; one mechanic has one execution path.
- No direct references from gameplay to UI code; use events.
- Write EditMode tests for gameplay logic, and run the gate through `unity-tester`.
- Existing singletons (`GameFlowService.Instance`, `GameBalance.Current`) stay;
  don't refactor to dependency injection unless a task asks for it.

## Examples

**Correct** (data-driven) — Godot:

```gdscript
var damage: float = config.get_value("combat", "base_damage", 10.0)
var speed: float = stats_resource.movement_speed * delta
```

Unity (values from a ScriptableObject):

```csharp
float damage = combatConfig.BaseDamage;
float speed = stats.MovementSpeed * Time.deltaTime;
```

Unreal (values from a data asset):

```cpp
const float Damage = CombatConfig->BaseDamage;
const float Speed = Stats->MovementSpeed * DeltaTime;
```

**Incorrect** (hardcoded):

```gdscript
var damage: float = 25.0   # VIOLATION: hardcoded gameplay value
var speed: float = 5.0      # VIOLATION: not from config, not using delta
```
