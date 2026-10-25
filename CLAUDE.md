# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Commander Battle: a Bannerlord multiplayer game mode in the style of Warband Napoleonic Wars commander battles. Two AI armies of 500 soldiers in 8 formations each; players take command of a formation, and the team AI leads the rest. `README.md` describes the rules, the files and the test checklist. Keep it current when behaviour changes.

`RBM.Multiplayer` brings RBM (Realistic Battle Mod, sibling repo `..\RealisticBattleProject`) to Commander Battle. It brings RBM's AI and Stance, built from RBM's own source, plus a light damage curve in place of RBM Combat.

## Build

```
dotnet build "CommanderBattle.sln" -c Release
```

The build needs the .NET SDK and the .NET Framework 4.7.2 targeting pack. Every runtime project is SDK-style and multi-targets **net472** (client) and **net6.0** (dedicated server, `SERVER` define). Paths come from `Directory.Build.props`: `BannerlordDir` is taken from the repo location; `DedicatedServerDir` is `..\Mount & Blade II Dedicated Server\` next to the game, and `RbmSourceDir` defaults to `..\RealisticBattleProject\`.

Each csproj has a `DeployModule` target that installs the module into `<game>\Modules\<Module>\` and `<dedicated server>\Modules\<Module>\`. **Never copy files into a module output folder by hand.** Put them in the project (`Module\`, `GUI\`...) and let the build target copy them.

A client DLL copy fails while the Bannerlord launcher or game has it open. Server-only changes still deploy. Close the game and rebuild for client changes.

There is no test suite. Testing is manual: a local dedicated server, a client joining it, and the logs.

## Projects

| Project | Runs on | What |
|---|---|---|
| `CommanderBattle` | server + client | game mode, server rules, spawning and layout, team AI, network messages, client mission logic |
| `CommanderBattle.View` | client | HUD, order bar, scoreboard, formation list |
| `RBM.Multiplayer` | server + client | RBM AI + Stance (linked RBM source), patcher, MP fixes, stance network sync |
| `RBM.Multiplayer.View` | client | RBM posture bars (RBM's `CombatUI` prefab), exhaustion effect |

`tools\Install-CampaignScenes.ps1` links the campaign field-battle scenes into `Modules\CommanderBattle\SceneObj`. It also writes the server config `Modules\Native\ds_config_commander_campaign.txt` (map pool, voting).

## RBM.Multiplayer rules

- **Never edit RBM source** (`..\RealisticBattleProject`). It holds the user's own work in progress. RBM files are linked into `RBM.Multiplayer.csproj` by compile globs, minus an exclusion list. Every MP change lives in this repo, as a new patch, a stub or a mission behaviour.
- When RBM adds a file that references a type the server lacks, the build names it. Then either add a stand-in to `RBM.Multiplayer/Stubs/` (campaign types in `CampaignStubs.cs`, left-out RBM UI classes in `RbmStubs.cs`), or exclude the file in the csproj.
- **Patching.** RBM's own patchers are never called. `RbmMpPatcher` applies the patch classes one by one (`CreateClassProcessor().Patch()`), under RBM's Harmony ids (`com.rbmai`, `com.rbmstance`) so RBM's `[HarmonyAfter]` ordering holds. MP patches go under `com.rbm.multiplayer`. Patch classes to leave out go in its skip set. Patching runs once, in `OnGameStart`, **on the dedicated server only**.
- **Settings are RBM's defaults**, set in code by `RbmMpSettings`. The only change is `rbmCombatEnabled = false`. Never call RBM's `LoadConfig`: it reads and rewrites the player's singleplayer config.
- **.NET 6 `MathF`.** On net6.0 `MathF` is ambiguous between `System` and `TaleWorlds.Library`. RBM files are covered by the forwarders in `Stubs/ServerCompat.cs`, a global-namespace `MathF` and `RBMAI.MathF`. Inside RBM.Multiplayer, don't add a `using MathF = ...` alias: it clashes with the global type (CS0576). Use `System.Math` or the global `MathF`. CommanderBattle files, which don't have the forwarders, use the alias.

## Multiplayer conventions (learned the hard way)

- **Driven properties are not synced.** Each machine computes agent stats itself. A stat change on a player-controlled agent desyncs movement and attacks, so RBM's stat patches are restored for agents with a `MissionPeer` (`PlayerStatGuard`).
- **Server-side UI does nothing.** `InformationManager` on the server never reaches a client. Anything players should see needs a `GameNetworkMessage` (`CommanderBattle/Network`, `RBM.Multiplayer/Network`).
- **Network messages must be identical on server and client.** Keep them in the shared dll, never inside `#if SERVER`. Behaviours added late register their handlers through `NetworkMessageHandlerRegistererContainer` in `EarlyStart`.
- **Rounds reuse the mission.** Teams and formations survive across rounds. Reset per-round state in `OnClearScene`. RBM's statics are reset there too (`RbmMpServerLogic`).
- **No `Agent.Main` on a server.** Stock code that relies on it misbehaves; for example, `Formation.IsAIOwned` treats player formations as AI-owned (`FormationOwnershipGuard`).
- **The field battle flag.** `Mission.IsFieldBattle` comes from `Mission.MissionTeamAIType`, which Commander Battle sets to `FieldBattle` in `AfterStart`. Without it, most singleplayer and RBM formation AI switches off.
- **Server and client compute the same spawn layout** (`CommanderBattleSpawnLayout`). Anything feeding it must be deterministic and available on both sides.
- `MissionBehavior.Mission` has an internal setter. To run a behaviour that is not added through the normal list (RBM's `BattleTerrainLogic`), set it via `AccessTools.PropertySetter`.

## Decompiled game sources

Read the TaleWorlds source in `..\RealisticBattleProject\decompiled\<AssemblyName>\` (one file per type, foldered by namespace). Never guess a method body or signature. Use the Explore agent for broad sweeps.

That folder is RBM's Core scope: the multiplayer assemblies (`TaleWorlds.MountAndBlade.Multiplayer*`, `TaleWorlds.MountAndBlade.DedicatedCustomServer*`, `TaleWorlds.MountAndBlade.Diamond`) are not in it.
- Regenerate it with `..\RealisticBattleProject\tools\Decompile-Bannerlord.ps1 -Scope Full`. The folder is gitignored, so this doesn't touch RBM's tracked source.
- Or decompile a single type with `ilspycmd` into the scratchpad.

The dedicated server's net6.0 assemblies are in `<dedicated server>\bin\Win64_Shipping_Server\`. They match the client's in API but not always in body: check there when server behaviour differs.

## Running and logs

Use `tools\Start-Server.ps1`, for example `powershell -ExecutionPolicy Bypass -File tools\Start-Server.ps1 -Restart`:
- It passes the arguments correctly.
- It refuses to start a second server unless `-Restart` is given, and `-Build` builds and installs first.
- It waits until the server hosts and prints the pid, the map, the RBM patch line and the logs.
- `-DryRun` shows the command.

By hand, start the server from `<dedicated server>\bin\Win64_Shipping_Server\`, **from PowerShell, not Git Bash**. Git Bash rewrites `/dedicatedcustomserverconfigfile`, `/tickrate` and `/port` into `C:/Program Files/Git/...` paths, so the server starts without the config and never opens a mission. The watchdog log's `Arguments` line shows what the server actually got. The starter returns at once and the server keeps running on its own:

```
DedicatedCustomServer.Starter.exe _MODULES_*Native*Multiplayer*CommanderBattle*RBM.Multiplayer*_MODULES_ /dedicatedcustomserverconfigfile ds_config_commander_campaign.txt /tickrate 60 /port 7210
```

Logs are in `C:\ProgramData\Mount and Blade II Bannerlord\logs\`:
- `CommanderBattle_<pid>.log`: teams, spawning, round end, fleeing.
- `RBM.Multiplayer_<pid>.log`: patches applied/failed/skipped, settings, per-round resets, tactics, terrain read, formation status.
- The engine's `rgl_log_*.txt`.

When checking a server run, look for "0 failed" in the patch line and for no exceptions.

## Gauntlet UI work

For prefabs, brushes, widgets, ViewModels or layers, read `..\RealisticBattleProject\docs\BannerlordGauntletUI.md` first, and tell any subagent doing UI work to read it too. The RBM posture HUD reuses RBM's `CombatUI` prefab and brush, copied by `RBM.Multiplayer.View.csproj`.

## Bannerlord documentation online

- https://moddocs.bannerlord.com/
- https://docs.bannerlordmodding.com/

## Session handoffs

Handoffs live in `docs/handoffs/`; its `README.md` explains the format and keeps the index.

- **At the start of a session,** read the newest handoff or two there. Other sessions may be working in this checkout at the same time, and the handoff says what is uncommitted, installed or running.
- **Before a session ends, and whenever the user asks for one,** write a handoff: `docs/handoffs/YYYY-MM-DD-HHmm-<short-topic>.md`, with the local date and 24-hour time it was written (take them from the system clock, e.g. `Get-Date -Format "yyyy-MM-dd-HHmm"`). It covers:
  - state: committed or not, installed, running;
  - what was done, with the files;
  - what is verified and what isn't;
  - open items with suggested next steps, and the user's decisions;
  - traps found.

  Add a row to the index in `docs/handoffs/README.md`.
- Never rewrite an older handoff. Record corrections and progress in the new one.

## Rules

- Never add Claude as co-author in commits.
- Edit files only with the Edit/Write tools, never with ad-hoc scripts.
- Code style follows the surrounding code: comments explain the why (game rules, MP quirks), not the what.
- Delegate broad research (more than ~3 files, or the decompiled sources) to a subagent with an explicit model: Sonnet for exploration, Opus for implementation and debugging. Keep the main session for orchestration.
