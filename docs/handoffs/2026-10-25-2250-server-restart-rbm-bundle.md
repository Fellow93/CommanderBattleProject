# Handoff: server on the zip's build; RBM bundle

Session of 2026-10-25, written 22:50. Follows `2026-10-25-2245-client-zip-scene-autolink.md` (same session).

## State

- **Not committed:** unchanged from 22:45.
- **Running:** server pid 37288, restarted with `tools\Start-Server.ps1 -Restart -Build` (it stopped pid 34388, the other session's). Map `battle_terrain_biome_144`, RBM patch line "92 classes applied, 0 failed, 3 skipped", no exception in its three logs at start.
- **Installed:** the server's `CommanderBattle.dll` is the 22:44 build (same hash as the game folder's server dll). That build rebuilt the client's `CommanderBattle.View.dll` (22:45:57).

## Zips in `artifacts\` (gitignored)

- `CommanderBattle-client-2026-10-25.zip` (1.6 MB): re-packed after the rebuild. Every file matches the installed client, except `server_address_overrides.txt` (the user's rewrite removed, on purpose).
- `CommanderBattle-RBM-bundle-2026-10-25.zip` (6.9 MB): the same plus `Modules\RBM`, the user's singleplayer RBM v4.7.1 build of 24 Oct 18:45 (newer than RBM's last source change). Its README says RBM is for singleplayer and isn't used by the MP mode. RBM.Multiplayer was already current: built 16:49 today, after the newest RBM source file (24 Oct 09:50).

## Open items

- The client-side scene auto-link hasn't been run yet (see the 22:45 handoff).
- Any later rebuild makes the zips stale: re-pack them and restart the server together.
