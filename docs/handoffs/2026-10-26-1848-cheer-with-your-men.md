# Cheer with your men v1; five flavor plans

## State

- **Not committed.** This session added the files below on top of the other sessions' uncommitted work (general mode, general orders, labels).
- **Built:** `CommanderBattle` compiles for net472 and net6.0. The client DLL is installed in the game folder.
- **Server not installed:** the dedicated server's `CommanderBattle.dll` is locked by server pid 21560, which belongs to another session. The cheer runs on the server only, so it needs that server stopped and the build re-run (or `tools\Start-Server.ps1 -Build -Restart`) before it can be tried.

## What was done

- **Five flavor plans** in `docs/plans/`, indexed in its README: `general-fallen.md`, `cheer-with-your-men.md`, `named-formations.md`, `commander-banners.md`, `round-weather.md` (no night battles, at the user's request).
- **Cheer with your men, v1** (`docs/plans/cheer-with-your-men.md`, now in progress):
  - `CommanderBattle/MissionMultiplayerCommanderBattle.Cheer.cs` (new): a second server handler for the stock `NetworkMessages.FromClient.TauntSelected`. It queues a cheer for every AI soldier of the cheering commander's own formation, at 0.1 to 1.5 s, and plays it from the mission tick: `act_cheer_1..4` on channel 1, `MakeVoice(Victory)` for one in three. Soldiers near an enemy (20 m) or busy on the upper body don't join in. 10 s cooldown per commander.
  - `CommanderBattle/MissionMultiplayerCommanderBattle.cs`: handler registration, `PlayQueuedCheers` in `OnMissionTick`, `ClearCheers` in `OnClearScene`.
  - `README.md`: a rules bullet and a test checklist line.

## Verified vs not

- Compiles on both targets. Nothing played or run on a server.
- The stock cheer flow (client sends `TauntSelected`; the server's `MissionNetworkComponent` plays the taunt; the engine replicates action channels and server-side voices; several handlers per client message run in order) was read in the decompiled client and the net6.0 server. The plan file has the details.

## Open items

- Install on the server and play it: the checklist line in the README. Things to watch:
  - whether the cheer wheel has entries in this mode (its taunts come from the player's lobby cosmetics);
  - whether other players see and hear the soldiers;
  - how mounted soldiers look;
  - RBM formations.
- **User decisions:**
  - cheers only, no bark echo;
  - any time in the round, for soldiers who are free;
  - only the commander's own formation, the general included (his riders, no 50 m wave);
  - 10 s cooldown.
- The other four plans are `idea`; go through their open questions before starting one.

## Traps found

- A handler for a stock client message must return true: `GameNetwork` stops at the first handler that returns false and treats the packet as invalid.
- `GameNetworkMessage` is in `TaleWorlds.MountAndBlade.Network.Messages`; `HasAnyFlag` needs `TaleWorlds.Library`.
