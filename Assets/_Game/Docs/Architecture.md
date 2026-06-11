# Architecture Notes

## Goals

- Keep gameplay systems data-driven through ScriptableObject definitions.
- Keep runtime save data serializable and independent from Unity scene objects.
- Make the early-game loop easy to tune for a 30-minute evaluation window.
- Prefer small services with explicit responsibilities over scene-wide hidden dependencies.

## Initial Runtime Layers

- Core: boot flow, game state ownership, time, service registration.
- Data: ScriptableObject definitions and registries.
- Persistence: save/load, offline timestamps, schema migration hooks.
- Characters: player runtime state, stats, level, equipment-facing model.
- Combat: enemies, auto-attacks, rewards, respawn loop.
- Economy: inventory, currencies, item stacks, equipment.
- Skills: gathering loops, skill XP, resource rewards.
- Quests: guided objectives and rewards.
- UI: panels and bindings that reflect runtime state.

## Save Boundary

Save files should contain plain serializable data only. ScriptableObject references should be stored by stable IDs, then resolved through registries at runtime.

