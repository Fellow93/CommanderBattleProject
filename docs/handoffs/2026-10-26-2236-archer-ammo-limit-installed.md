# Handoff: archer ammo limit installed

Session of 2026-10-26, written 22:36. Follows `2026-10-26-2230-archer-ammo-limit.md`.

## State

- **Not committed:** same files as the 22:30 handoff, plus this one and its index row.
- **Installed** on the server and the client by `tools\Start-Server.ps1 -Build -Restart`, run at the user's request. The installed server `RBM.Multiplayer.dll` (22:30:05) contains `AmmoLimitGuard`.
- **Running:** server pid 33880, map `battle_terrain_biome_044`. The restart stopped pid 12636, which the round weather session (`2026-10-26-2235-round-weather-v1.md`) had started at 22:35 after the 22:30 handoff; pid 23668 was already gone. The build installed the whole checkout, so 33880 also carries the banners and weather work.

## Verified

- `Patches: 95 classes applied, 0 failed, 3 skipped`: one class more than pid 23668's 94, which is the guard.
- The mission opens: "Opening mission on battle_terrain_biome_044", "Round 0: Preparation", weather line. No exceptions in the CommanderBattle, RBM.Multiplayer or rgl logs.
- The rgl log's 32 "Cannot load: Microsoft.AspNetCore..." errors appear in pid 23668's log as well. They come from the dedicated server itself and were there before this change.

## Not verified

- Not played. In a round, watch whether AI archers hold fire longer and still have arrows when the lines meet.

## Open items

- Unchanged from the 22:30 handoff (a half-quiver threshold if archers now hold fire too long).
