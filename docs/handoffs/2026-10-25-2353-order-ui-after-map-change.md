# Handoff: order UI dead after a map change

Session of 2026-10-25, written 23:53. Follows `2026-10-25-2250-server-restart-rbm-bundle.md`.

## State

- **Not committed:** `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs` (`ApplyClientOptions`), `README.md` (one checklist line), this handoff and its index row. The checkout also holds the other sessions' uncommitted work listed in the 22:45 and 22:50 handoffs.
- **Installed:** `CommanderBattle.dll` built 23:53:04, into the game's client folder and the dedicated server's folder. It carries every working-tree change at that time.
- **Running:** nothing. The game and the server (pid 25044) were closed before the build.
- **Zips in `artifacts\` are stale** now: re-pack them before sending them to a player.

## The bug

The user's report: after 2 rounds the match ended and a new map loaded. There, every attack also gave an order, F keys did nothing, and orders couldn't be picked.

Client logs (pid 14812):
- `rgl_log`: on the first map, "InitializeInADisgustingManner is called" (the stock `MissionGauntletMultiplayerOrderUIHandler` starting up). On the second map (`battle_terrain_004`, 23:39) it never appears. Each attack click there logged `SetOrderWithTwoPositions MoveToLineSegment` with two different points: the order troop placer's drag order.
- No managed exception on the second map.

Why (decompiled `MissionGauntletMultiplayerOrderUIHandler`, `OrderTroopPlacer`):
- The handler only ticks when `_shouldTick` is true. It sets that once, in `AfterStart`: `NumberOfBotsPerFormation > 0`. Without the tick there is no order VM and no layer: no order bar, no F keys.
- `OrderTroopPlacer._suspendTroopPlacer` starts false. Only the handler's `SetLayerEnabled(false)` suspends it. So a dead handler leaves the placer live, and every left click becomes a move order to the formations selected at `AssignFormationToPlayer`.
- `NumberOfBotsPerFormation` isn't in the server config. The server sets it in code (`MissionMultiplayerCommanderBattle.ApplyRequiredOptions`). `BaseNetworkComponent` sends `MultiplayerOptionsInitial` and `MultiplayerOptionsImmediate` only in `HandleNewClientConnect`. The client log shows no options message during the intermission or the map load.
- Not pinned down: what clears the client's value between missions. The handler can't tick unless the value is 0 there.

## Fix

`MissionMultiplayerCommanderBattleClient.OnBehaviorInitialize`, on clients only (`!GameNetwork.IsServer`), sets `NumberOfBotsPerFormation` to `CommanderBattleSettings.BotsPerFormationBaseline` (100) when it differs. It logs "Order bar turned on for this map (bots per formation was X, now 100)". Every `OnBehaviorInitialize` runs before any `AfterStart`, so the handler reads the new value.

## Verified vs not

- Builds; the installed client dll contains `ApplyClientOptions`.
- **Not played yet.** To verify: play to the end of a match, let the next map load, then check that the order bar, F keys and attacking work. In the client's `CommanderBattle_<pid>.log`, the "Order bar turned on for this map (bots per formation was ...)" line confirms the cause: expect 0 on the second map, and likely none or 0 on the first.

## Open items

- **Hold-release flood is still there** (open since 22:08): the client's rgl log grew to 417 MB, about 2.1 million `ClearAgentTargetFrame` lines in 35 minutes. Next step as in the 22:08 handoff: send it only for holds clients actually got.
- `CommanderBattleLog.IsNetworkNoise` misses `ObjectDisposedException`, `WebException` and `HttpRequestException` from lobby traffic. They are harmless but use up the first-chance budget.

## Traps

- The MP order UI depends on `MultiplayerOptions` values that only reach a client when it connects. A gameplay option that the client UI reads and the server sets in code must also be set on the client.
- The multiplayer assemblies aren't in `..\RealisticBattleProject\decompiled`. `ilspycmd -p -o <scratch> <game>\Modules\Multiplayer\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.Multiplayer.dll` (with `-r` to the game's bin folders) took about a minute.
