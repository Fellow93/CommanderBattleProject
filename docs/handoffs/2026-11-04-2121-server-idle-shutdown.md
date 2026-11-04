# Server shut itself down after 10 minutes

The dedicated server quit about 10 minutes after starting ("Shutting down in 20 seconds with reason 'Disconnected from custom battle server manager'"). The backend didn't drop it. This is the stock idle timeout in `CustomBattleServer` (`TaleWorlds.MountAndBlade.Diamond`):
- `IsIdle` is true once the server has been registered with no players for 600 s.
- `DedicatedCustomServerIntermissionManagerHandler.OnTick` then calls `FinishAsIdle`, which disconnects and shuts down.
- `Connect` turns the timer on unless the server runs with `/playerhosteddedicatedserver`, and v1.5.6 can't start with that flag (2026-11-03-1711 handoff).
- The timer restarts on a join request or a disconnect, but not when a player quits through the lobby. A server that empties more than 10 minutes after the last join therefore shuts down at once.

- `CommanderBattle/CommanderBattleSubModule.cs`: `DisableIdleShutdown()`, server only, in `OnSubModuleLoad`. Multiplayer's submodule has already called `Connect` by then; this sets `_useTimeoutTimer` to false on `NetworkMain.CustomBattleServer` by reflection. It logs "Idle shutdown disabled", or why it couldn't.
- Server built and started (pid 24652), and the log shows "Idle shutdown disabled". The client DLL copies were locked by the launcher; nothing changed on the client side. Committed.
