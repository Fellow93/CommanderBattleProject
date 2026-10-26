# Handoff: the bodyguard rides behind a player general

Session of 2026-10-26, written 13:59. It follows `2026-10-26-1351-general-lobby-crash-fix.md` (same session).

## State

- **Not committed:** all of the general role work (13:40, 13:51) plus this change.
- **Installed:** both sides; the build is clean.
- **Running:** server pid 30352, map `battle_terrain_biome_148`. Patch line: "94 classes applied, 0 failed, 3 skipped".
- **Client:** restart the game.

## What was done

The user played the general and reported that the bodyguard got in the way a lot.

**Cause:** the stock Follow order (`MovementOrder`) does two things.
- `OnApply` → `Arrangement.ReserveMiddleFrontUnitPosition(target)`: the followed agent is the formation's front middle, so the riders line up beside him.
- `GetPositionAuxFollow`: a mounted formation on the move aims `2 × target velocity` ahead of him, so they ride across his path.

**Change:** new `CommanderBattle/MissionMultiplayerCommanderBattle.Bodyguard.cs` (a partial of the game mode). `KeepBodyguardsBehindGenerals` runs every tick while the lobby is Playing, on the server.
- For a side whose `Team.GeneralAgent` is a living player (`MissionPeer != null`), it gives formation 8 a Move order to a point 10 m behind the general.
- "Behind" is against his average velocity above 2 m/s; otherwise it is against the formation's facing.
- The order is renewed when that point moves 3 m, or when the order is no longer a Move.
- The first time, the formation gets `FormOrderDeep`, a compact block rather than a 10-rider line.
- AI generals are left alone; `BehaviorGeneral` moves the whole formation.
- Targets are cleared in `OnClearScene`.

**Other edits:**
- `MissionMultiplayerCommanderBattle.cs`: the take-over no longer issues a Follow to the new general (the tick does it). The "fields no general" log line now names the `cb_general_<culture>` id, not the old `default_group`.
- `README.md`: the general section explains the bodyguard's position.

## Verified vs not

- **Verified:** the build (0 warnings, 0 errors); the server started with 0 failed patches.
- **Not verified in play:**
  - the riders keep about 10 m behind;
  - they don't oscillate when he stops and turns;
  - the player's own empty slot in the Move-ordered formation doesn't look odd;
  - the block's facing.

## Open items

- **Play it.** Tune `BodyguardDistance` (10 m), `BodyguardRefreshDistance` (3 m) and `BodyguardMovingSpeed` (2 m/s) from what the user sees.
- **Facing:** the block still faces where the formation's facing order says (stock default). If it looks wrong while riding, set a `FacingOrderLookAtDirection` along his movement.
- Commit the whole general role work after the user's look.

## Traps

- **The stock Follow order** puts the followed agent in the formation's front middle, and makes a mounted formation lead him by 2 s of his velocity. Don't use it for an escort.
- **`WorldPosition` is in `TaleWorlds.Engine`,** not `TaleWorlds.Library`. A failed `Start-Server.ps1 -Restart -Build` leaves the server stopped.
