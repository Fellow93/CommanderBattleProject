# Cheer with your men: installed on the server

Follows [2026-10-26-1848-cheer-with-your-men.md](2026-10-26-1848-cheer-with-your-men.md).

## State

- **Not committed.**
- **Server restarted** at the user's request with `tools\Start-Server.ps1 -Build -Restart`. The old server, pid 21560, belonged to another session and was stopped. The new one is **pid 30072**, on `battle_terrain_biome_106`.
- The dedicated server's `CommanderBattle.dll` is the new build: same hash as the game folder's server build, and it contains the cheer handler.
- The client is installed. It is unchanged for the cheer, which runs on the server only.

## Verified

- RBM patch line: 94 applied, 0 failed, 3 skipped.
- `CommanderBattle_30072.log`: loaded, mission opened, round 0 in preparation; no exceptions.
- `rgl_log_30072.txt` has a block of "Cannot load: Microsoft.AspNetCore...dll" errors at startup. These are the stock server's: the previous server's log (21560) has the same ones. Nothing else stands out.

## Not verified

- The cheer itself has not been played. Test it with the README checklist line ("Cheering (the cheer wheel)..."), and turn on `CommanderBattleLog.Detailed` to see the "cheered: N of ... join in" lines.
