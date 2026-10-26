# Takeover hold release: installed on the server

Follows [2026-10-26-1915-takeover-frontline-hold.md](2026-10-26-1915-takeover-frontline-hold.md).

## State

- **Not committed.**
- **Server restarted** at the user's request with `tools\Start-Server.ps1 -Build -Restart`. Old pid 30072 stopped; new **pid 30484**, on `battle_terrain_022`. It runs the cheer build too.
- The dedicated server's `RBM.Multiplayer.dll` has the same hash as the game folder's server build (written 19:14:46, after the fix).

## Verified

- RBM patch line: 94 applied, 0 failed, 3 skipped. No exceptions in `CommanderBattle_30484.log` or `RBM.Multiplayer_30484.log`.

## Not verified

- Not played: take over a front-rank soldier mid-melee (F) and walk away. The README checklist has the line.
