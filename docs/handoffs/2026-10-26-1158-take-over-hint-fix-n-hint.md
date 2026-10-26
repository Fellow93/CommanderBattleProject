# Handoff: take-over hint fixed; N hint in the Alt panel

Session of 2026-10-26, written 11:58. It follows `2026-10-26-1127-take-over-hint.md` (same session).

## State

- **Not committed:**
  - everything from the 11:13 and 11:27 handoffs;
  - `CommanderBattle.View/CommanderBattleTakeOverHintView.cs` (the fix);
  - `Module/GUI/Prefabs/CommanderBattleAIStatus.xml` (the N line);
  - `README.md`, `CLAUDE.md` (a new MP convention);
  - this handoff.
- **Installed:** both sides, built at 11:57 with 0 warnings. The prefab with the N line is in the game's module folder.
- **Running:** server pid 2564, map `battle_terrain_028`. Patch line: "94 classes applied, 0 failed, 3 skipped".

## What was done

### 1. The take-control line never showed

The user played the 11:27 build. Everything else looked good, but the F hint did not appear.

- **Cause:** the hint required `followed.IsAIControlled`. On a client that is always false: `Agent.IsAIControlled` returns `!GameNetwork.IsClientOrReplay` for AI agents.
- **Fix:** the check is now `!followed.IsPlayerControlled`, as `GetMissionCameraLockMode` already does.
- **CLAUDE.md:** this went into "Multiplayer conventions".

The stock card does show for a player:
- It is visible when `IsSpectatingAgent` is true.
- For a non-spectator it shows the short card (`ShowReducedInfo = IsRetracted || !IsSpectating`).
- Both card variants carry the `@CanTakeControlOfSpectatedAgent` line.

### 2. N hint in the Alt panel

The user asked for a small hint in the Left Alt panel that N opens troop selection.

- **Placement:** a static line, "Press N to change your formation or troops", under "Yellow markers: …" in `CommanderBattleAIStatus.xml`.
- **Why that wording:** N opens the formation list, and its Troops button opens the troop selection, so the text names both.
- **README:** the Alt panel paragraph says so.

## Verified vs not

- **Verified:** it builds, the server starts with 0 failed patches, and the installed prefab has the line.
- **Not verified in play:**
  - "Press F to take control" shows about 3 s after dying, on your own soldier's card;
  - F takes over that soldier;
  - the N line shows at the bottom of the Alt panel.
- **Already played by the user before this fix** (the 11:13 base classes and the rest of the 11:27 build): "all looked good".

## Open items

- Commit after the user's next round.
- The rest of the review list is in the 11:13 handoff.

## Traps

- `Agent.IsAIControlled` is always false on a client (now in `CLAUDE.md`).
