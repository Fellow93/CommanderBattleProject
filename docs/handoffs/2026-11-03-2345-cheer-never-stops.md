# Handoff: formation cheer never stops

Session of 2026-11-03, written 23:45.

## What was done

- **Report:** soldiers that cheer along with their commander keep cheering forever.
- **Cause:** `MissionMultiplayerCommanderBattle.Cheer.cs` plays the singleplayer victory cheers (`act_cheer_1..4`) on channel 1. They loop until something else takes the channel. Stock `AgentVictoryLogic` ends them with `act_none` (decompiled `AgentVictoryLogic.cs:215`); our code never did, and an idle soldier in formation does nothing that overrides it. (The commander's own taunt uses `act_taunt_cheer_*`, which play once.) The loop is inferred from the stock code: the clip data is in binary asset packages.
- **Fix,** same file: once a soldier starts cheering, a stop is queued 3.5 s later (`CheerDuration`, `QueuedCheer.IsStop`). `StopCheer` sets `act_none` on channel 1 with the stock priority cap (73), and only if the agent is still in one of the cheer actions. The queue is cleared each round as before.
- `README.md` checklist line and `docs/plans/cheer-with-your-men.md` updated.
- **Install:** `Start-Server.ps1 -Build` fails on master: `RBM.Multiplayer` no longer compiles against the current RBM source (`AgentStats.cs`, `MeleeBlowPatch.Math.cs`: a `BattleEnvironment` argument expected where RBM passes a `CharacterObject`). Not looked into. At the user's request the `rbm-combat` checkout's server (pid 28112) was stopped, only `CommanderBattle.csproj` was built and installed (client and server), and the server was started.
- **Server start:** master's `Start-Server.ps1` still goes through `DedicatedCustomServer.Starter.exe`, which adds `/playerhosteddedicatedserver`; on v1.5.6 that run exited during startup (pid 39524, WebPanel can't load ASP.NET Core 8). Started directly instead: `TaleWorlds.Starter.DotNetCore.exe` with the usual arguments plus `/dedicatedcustomserver 7210 USER 0`.
- **State:** server pid 31224: 103 applied, 0 failed, 35 skipped; mission opened on `battle_terrain_biome_106`; no exceptions. Its `RBM.Multiplayer.dll` is the one `rbm-combat` installed at 23:47; it doesn't find `CommanderBattle.CombatModeSetting` in master's CommanderBattle and falls back to Light ("RBM Combat off"). Not played, not committed.
