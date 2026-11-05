# Server shut itself down after 10 minutes

The dedicated server quit about 10 minutes after starting ("Shutting down in 20 seconds with reason 'Disconnected from custom battle server manager'"). The backend didn't drop it. This is the stock idle timeout in `CustomBattleServer` (`TaleWorlds.MountAndBlade.Diamond`):
- `IsIdle` is true once the server has been registered with no players for 600 s.
- `DedicatedCustomServerIntermissionManagerHandler.OnTick` then calls `FinishAsIdle`, which disconnects and shuts down.
- `Connect` turns the timer on unless the server runs with `/playerhosteddedicatedserver`, and v1.5.6 can't start with that flag (2026-11-03-1711 handoff).
- The timer restarts on a join request or a disconnect, but not when a player quits through the lobby. A server that empties more than 10 minutes after the last join therefore shuts down at once.

- `CommanderBattle/CommanderBattleSubModule.cs`: `DisableIdleShutdown()`, server only, in `OnSubModuleLoad`. Multiplayer's submodule has already called `Connect` by then; this sets `_useTimeoutTimer` to false on `NetworkMain.CustomBattleServer` by reflection. It logs "Idle shutdown disabled", or why it couldn't.
- Server built and started (pid 24652), and the log shows "Idle shutdown disabled". The client DLL copies were locked by the launcher; nothing changed on the client side. Committed.

## Crash on "charge that formation"

The server then died silently mid-battle, three times: no log line, no dump, no Windows error. The process exit code, caught with a watcher, was 0x800703E9, a stack overflow. Each time the last engine line was `SetOrderWithFormation Charge`, a player ordering their formation to charge a specific enemy formation. The user confirmed it reproduces.

The cycle is in RBM AI:
- `OverrideSetMovementOrder.PostfixSetTargetFormation` (`MovementOrderPatches.cs:248-266`) re-issues the charge at the player's pick, then calls `SetTargetFormation` again.
- When the formation is already charging and the pick is beyond ChargeGuard's start distance, `ChargeGuard`'s "keep" branch (`ChargeGuard.cs:413-429`, which doesn't honour `playerPick`) swaps the order back to the running charge.
- The nested call therefore sees the target unchanged and re-issues it again, endlessly.

- `RBM.Multiplayer/ChargeRetargetGuard.cs`: a prefix and finalizer on RBM's postfix method. Only the outermost call runs. In the loop case the formation keeps its running charge (RBM's rule); otherwise nothing changes. Server build only (client DLLs locked by the launcher; the patch is server-only). Server pid 41264, 125 applied / 0 failed. Not yet re-tested in game, not committed.
- RBM, at the user's request: `ChargeGuard.cs` keep branch skipped for `playerPick`, so a re-aimed player charge falls through to the normal release and charges the pick (`..\RealisticBattleProject`). The guard stays as a second line. Server pid 22992, 125 applied / 0 failed. The user re-targeted a running charge in game, and it works. Committed in both repos.

## v0.2.1

- Version v0.2.1 in `Module/SubModule.xml`, `RBM.Multiplayer/Module/SubModule.xml`, `manual/docs/index.md` and `manual/docs/getting-started.md`. `CHANGELOG.md` has a v0.2.1 entry (the charge crash fix).
- Everything built and installed with the game closed (client and server DLLs at 23:01). Server pid 39492 on battle_terrain_biome_088, 125 applied / 0 failed. Committed (`b60985e`); the release was rebuilt after `02137cb` (swing-speed log, another session), tagged `v0.2.1` and pushed. The user publishes the GitHub release.

## A player's "charge that formation" charges it (RBM)

Player-led infantry ordered to charge horse archers turned onto the nearest foot instead. In `..\RealisticBattleProject`:
- `ChargeGuard.cs`, safety net: re-applying the player's charge every 0.5 s cleared `Formation.TargetFormation` (stock `SetMovementOrder` ends with `SetTargetFormation(null)`), which is how the engine tells every man which formation to go for. The pick is set again after the re-apply.
- `ChargeGuard.RememberPlayerPick`, called from the `SetTargetFormation` postfix (`MovementOrderPatches.cs`): a charge that went at once at the pick, because the pick was the nearest formation, was stored as the plain Charge and lost the pick when re-applied.
- `FrontlinePositioning.cs` + `Utilities.NearestAgentFocused(..., playerPick)`: a player's pick gives way only to an enemy within 4 m (self-defence). Before, the pick was taken with no exception at all; AI formations keep the 8 m slack. When nobody in the pick can be targeted (all routing), the formation's normal choice applies.
- The formation follows its pick until the player orders something else (user). Built both sides, server pid 21584, 125 applied / 0 failed. The user tested it in game, and it works. Committed in RBM.
