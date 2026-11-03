# Handoff: RBM's ranged rework (no shared missile speed) adopted

Session of 2026-11-03, written 19:40, on branch `rbm-combat` (worktree `Modules\CommanderBattleProject-rbm-combat`), uncommitted.

## What was done

- **RBM's uncommitted ranged rework reviewed** (`..\RealisticBattleProject`, `RangedRework.MissileSpeed.cs` / `.EquipWield.cs` / `.State.cs`, `MissileBallistics.cs`): a bow, crossbow or sling's item `MissileSpeed` stays its XML draw weight and is never written. The launch speed goes only into the per-agent weapon stats at equip and into the shot (prefix velocity, transpiler for `baseSpeed`). The spawn patch, the `EndMission` restore and the `rangedWeaponStats` / `rangedWeaponMW` caches are gone; `GetLauncherSpeed` / `GetSlingSpeed` lost their draw weight argument. Shots fly the same. For MP it removes the server-only shared item writes and the per-mission caches.
- **Adapted to it:**
  - `RBM.Multiplayer/RbmMpEquipWield.cs`: `CacheDrawWeight` and its comment removed; RBM's prefix reads the draw weight from the item.
  - `RBM.Multiplayer/RbmMpClientMissileSpeed.cs`: `GetSlingSpeed` called with three arguments; comments updated.
  - `docs/plans/rbm-combat-mp-items.md`: the `RbmMpEquipWield` note.
- **Built and installed** both sides (0 errors, DLLs 19:40). No server running; not played.
