# Handoff: slower ranged fire installed, server running

Session of 2026-10-25, written 22:33. Follows `2026-10-25-2230-slower-ranged-fire.md` (same session).

## State

- **Not committed:** as in the 22:30 handoff, plus `CommanderBattle/CommanderBattleSubModule.cs` (log line). The 22:08 handoff's uncommitted files are still untouched.
- **Installed:** on the game client and the dedicated server. The installed `CommanderBattle.dll` hashes match `obj\Release\<tfm>\` for both frameworks.
- **Running:** dedicated server pid 34388, started with `tools\Start-Server.ps1 -Build -Restart` at the user's request (the user closed the game first). The server at pid 35176 was stopped by the script; an interim restart, pid 37432, was replaced too.

## What was done

- The startup log line now names the fire speed: "Agent stats: Commander Battle model (horse speed x0.85, bow fire speed x0.75, crossbow fire speed x0.75)".

## Verified

- Server hosts (battle_terrain_biome_087b), "92 classes applied, 0 failed, 3 skipped", no exceptions or errors in `CommanderBattle_34388.log` or `RBM.Multiplayer_34388.log`.

## Not verified

- No round with a client yet. Join, watch archers and crossbowmen reload, and fire a bow yourself to check the draw doesn't stutter or snap back.

## Open items

- Tune `BowFireSpeedFactor` / `CrossbowFireSpeedFactor` after a playtest.
- The 22:08 handoff's open items (hold-release flood, desync audit, spawn path check) stand.
