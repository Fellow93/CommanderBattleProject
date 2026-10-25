# Commander Battle

Bannerlord multiplayer game mode in the style of Warband Napoleonic Wars commander battles:

- Attackers vs Defenders, stock cultures (`CultureTeam1` / `CultureTeam2`) and stock multiplayer troop classes.
- Each side is a fixed army of 500 soldiers in 8 formations: 4 infantry, 2 ranged, 2 cavalry (`CommanderBattleSettings`).
- Players take command of a formation from the formation list (N). The team AI leads every formation nobody commands, with a singleplayer-style field battle tactic.
- While the order bar is open, the third-person camera rises and pulls back from the commander (about 3 m up and 7 m back), so the formation and the ground ahead are in view. The mouse wheel brings it in or out, up to about 15 m up and 35 m back, and the choice is kept for the session. It falls back when the bar closes. This runs only on the client, and the commander keeps control of their soldier (`CommanderBattleOrderCameraView`, after RTS Camera's elevated camera by Li Zhenhuan, MIT). It does nothing in first person.
- While you hold Left Alt, a panel on the left shows what your side's AI is doing (`CommanderBattleAIStatusSync` on the server sends `SideAIStatus` to that side's players, `CommanderBattleAIStatusView` draws it). It shows the AI's tactic, and for every formation its troops, behaviour, order and the metres it still has to go. The formation list (N) shows each AI-led formation's behaviour as well. With Left Alt held, a marker also shows where each AI-led formation is heading: the move order's position, or the formation it is charging. The tactic's main formation is marked "(main)": the AI's archers and cavalry take their places from it. When every infantry formation is player-led, that can be a player's. A player who leads it gets a highlighted notice in the panel and a message, and another message when it stops being theirs.
- The round goes to the last side with soldiers standing. If time runs out with both armies still in the field, the defenders win: the attackers have to break them within the time limit.
- No flags, morale or gold. A dead commander can take over one of their own soldiers, the same way as in Captain mode.
- Giving orders counts as activity for the stock inactivity kick (`CommanderBattleActivityLogic`). The game kicks a player whose soldier hasn't moved or looked around for 3 minutes, which a commander leading from the order bar easily reaches. A change in the orders of the commander's formation (movement, facing, arrangement, form, firing, riding) resets the timer, so only a player who is really away is kicked.
- Horses run at 85% of the stock multiplayer top speed (`CommanderBattleSettings.HorseSpeedFactor`, applied by `CommanderBattleAgentStatCalculateModel` on the server and every client alike).
- Fleeing soldiers leave the battle once they come within 25 m of the map edge, or after fleeing for 2 minutes (`CommanderBattleFleeingLogic`). The stock rule only lets them go within a few metres, which on the campaign maps they often never reach, so a round could not end.

## How it works

| Piece | File |
|---|---|
| Game type registration (`CommanderBattle`) | `CommanderBattle/CommanderBattleMultiplayerGameMode.cs` |
| Mission (stock components + the ones below) | `CommanderBattle/CommanderBattleMissions.cs` |
| Server rules: teams, formation commanders, round end | `CommanderBattle/MissionMultiplayerCommanderBattle.cs` |
| Formation slots and who commands them | `CommanderBattle/FormationSlots.cs`, `CommanderBattle/Network/*.cs` |
| Spawning: one army per side, laid out in formation | `CommanderBattle/CommanderBattleSpawningBehavior.cs`, `CommanderBattleSpawnLayout.cs` |
| Team AI for the formations nobody commands | `CommanderBattle/AI/*.cs` |
| Client: HUD strength bar, compass, scoreboard bot counts | `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs` |
| Client views (HUD, order bar, lifted order camera, scoreboard, formation list, attacker/defender banner and team labels, AI status panel and destination markers) | `CommanderBattle.View/*.cs` |

The client reports itself as `Captain` to the stock UI, so you get the bot counts on the scoreboard, the troop preloading and the order bar.

The server marks the mission as a field battle (`Mission.MissionTeamAIType`), as singleplayer battles are, so the singleplayer formation behaviours the team AI uses, and mods built on them such as RBM's AI, behave as in a field battle.

## RBM (RBM.Multiplayer)

`RBM.Multiplayer` is a separate multiplayer module that brings RBM (Realistic Battle Mod) to Commander Battle. It is built from RBM's own source in the sibling `RealisticBattleProject` repo (`RbmSourceDir`), without changing that source.

What runs in Commander Battle:

- **RBM AI**: RBM's field-battle AI leads the formations nobody commands:
  - its own tactics (split infantry and archers, embolon, hold choke, reserve plug, hedgehog...) and RBM's tactic choice by culture and terrain, in place of `CommanderBattleTactic`;
  - RBM's own behaviours on every formation;
  - AI reactions;
  - the terrain read, done each round once both armies stand on the field;
  - the formation and agent AI patches (Frontline, formation movement and spacing, AI combat parameters).
- **Archers defend themselves** (`RBM.Multiplayer/AI/ArcherCloseCombatLogic.cs`):
  - RBM's weapon scores for ranged formations keep the bow out at almost any range. Its formation movement also keeps every soldier on a move order in his slot.
  - So an AI archer on foot with an enemy within 4 m switches to vanilla's sword-versus-bow scores. He takes the nearest enemy as his target when the engine sticks to a far one, and leaves his slot.
  - RBM's behaviour returns 3 s after the last enemy is gone. The log's "Archer close combat" lines count them.
- **RBM Stance**: posture, stamina and AI kick/bash, simulated on the server. Players see RBM's posture/stamina bars and its crush-through and chamber-block posture-break messages (`StanceHudUpdate` / `StanceEvent`, `RBM.Multiplayer/Network`), and the first-person exhaustion effect with RBM's exertion grunt. RBM's stagger, tired and kick/bash fall animations are replayed on every client (`StanceAnimation`).
- **Settings**: RBM's defaults, the same on the server and every client. Your singleplayer RBM config is never read. RBM Combat is off (see below).
- **Damage curve** (`RBM.Multiplayer/DamageCurve.cs`): a light stand-in for RBM Combat. Every blow's final damage is first cut by 50% (`scaled = damage × 0.5`), then becomes `scaled / (1 + scaled / 300)`, so the harder a hit, the more it loses:

  | Damage | 25 | 50 | 75 | 100 | 150 | 200 |
  |---|---|---|---|---|---|---|
  | Becomes | 12 | 23 | 33 | 43 | 60 | 75 |

  `DamageCurve.Scale` (0.5) is the flat cut. `DamageCurve.Softness` (300) sets the strength of the curve: the scaled damage at which a blow is halved.

How it fits together:

| Piece | File |
|---|---|
| Entry point: settings, patching, mission behaviours | `RBM.Multiplayer/RbmMpSubModule.cs` |
| RBM's patches, one class at a time, on the dedicated server only | `RBM.Multiplayer/RbmMpPatcher.cs` |
| Per-round reset of RBM's state, tactics and terrain read; stat rebuild when a commander takes over a soldier | `RBM.Multiplayer/RbmMpServerLogic.cs` |
| RBM's tactics on Commander Battle's teams | `RBM.Multiplayer/RbmMpTactics.cs` |
| Keeps commanders' formations out of the AI's regrouping | `RBM.Multiplayer/FormationOwnershipGuard.cs` |
| Keeps RBM's stat changes off player-controlled agents | `RBM.Multiplayer/PlayerStatGuard.cs` |
| Posture HUD values and events, server and client | `RBM.Multiplayer/Stance/*.cs` |
| Stand-ins for the singleplayer types RBM's source mentions | `RBM.Multiplayer/Stubs/*.cs` |
| Posture bars and exhaustion effect (client only) | `RBM.Multiplayer.View/*.cs` |

Notes:

- **The AI regroups its own soldiers.** As in singleplayer, RBM's tactics merge the AI-led formations of a kind and split flanking parties off them. A formation a player commands is never touched: no soldier moves into or out of it, and its commander keeps exactly their own men. A formation whose commander leaves goes back to the AI and may be regrouped. Every round starts again from Commander Battle's formations.
- **Commander Battle without RBM** still uses its own `CommanderBattleTactic`. Commander Battle only leaves it out when the `RBM.Multiplayer` module is active.
- **Bots only for stat changes.** Driven properties (speeds, AI values) are computed separately on each machine and never synced. RBM's stat changes, including stamina's effect on speed, therefore apply only to the soldiers the AI controls. A player's agent keeps the stock values their client predicts with.
- **RBM Combat is not included yet.** RBM's damage and armour model expects RBM's item stats, and multiplayer uses its own `mp_` items. On those, RBM's formulas would leave arrows, bolts and horse charges doing almost no damage. Combat needs an RBM-style multiplayer item set first.
- **Stubs.** RBM's AI and Stance files compile against small stand-ins for the singleplayer types they reference. These are mostly campaign types (`Stubs/CampaignStubs.cs`), plus a few RBM UI classes left out of this module (`Stubs/RbmStubs.cs`). If RBM starts using another one, the build names it.
- **Log.** `C:\ProgramData\Mount and Blade II Bannerlord\logs\RBM.Multiplayer_<pid>.log` records which patch classes were applied, the per-round resets, the tactic registration and the terrain read.

## Build

Requires the .NET SDK and the .NET Framework 4.7.2 targeting pack. RBM.Multiplayer also needs RBM's source at `<game>\Modules\RealisticBattleProject` (override with `-p:RbmSourceDir=X:\path\`).

```bash
dotnet build "CommanderBattle.sln" -c Release
```

The build installs:

- `<game>\Modules\CommanderBattle\`: `SubModule.xml`, `bin\Win64_Shipping_Client\CommanderBattle.dll` + `CommanderBattle.View.dll`, `bin\Win64_Shipping_Server\CommanderBattle.dll`
- `<game>\Modules\RBM.Multiplayer\`: `SubModule.xml`, `GUI\` (RBM's posture HUD), `bin\Win64_Shipping_Client\RBM.Multiplayer.dll` + `RBM.Multiplayer.View.dll` + `0Harmony.dll`, `bin\Win64_Shipping_Server\RBM.Multiplayer.dll` + `0Harmony.dll`
- the server builds of both modules into the dedicated server's `Modules` folder, when the dedicated server is installed next to the game.

The game folder is taken from the repo location. Override it with `-p:BannerlordDir=X:\path\to\game\`.

## Maps

The armies start where a singleplayer field battle of this size would place them on the same scene. `CommanderBattleSpawnLayout` runs the stock deployment planner on the scene's spawn path: the singleplayer distance between the armies for 1,000 troops, infantry in front, ranged behind, cavalry on the left and right flanks. It then shares each block out between the formations of that role. Server and clients compute the same layout.

Each round, every army starts somewhere else in its spawn zone. The server picks a seed at the start of the round's preparation and sends it to every client (`SpawnLayoutSeed`), and all of them derive the same changes from it:
- The army moves up to 40 m sideways, 40 m back or 15 m forward, and turns up to 10 degrees.
- The formations of a role swap places.
- If that would put a formation outside the map boundary, a smaller move is used, down to none.

The amounts are in `CommanderBattleSettings` (`SpawnLateralJitter`, `SpawnBackJitter`, `SpawnForwardJitter`, `SpawnTurnJitterDegrees`), and the log records each side's move.

Multiplayer only loads scenes from multiplayer modules. `tools\Install-CampaignScenes.ps1` links the campaign's field-battle scenes into `Modules\CommanderBattle\SceneObj`, for both the game and the dedicated server. It also writes `Modules\Native\ds_config_commander_campaign.txt` for the server, with 2 rounds per map. After each match the players vote for the next map from these scenes (`enable_map_voting`; the game offers at most 100), and map-change polls are allowed (`AllowPollsToChangeMaps True`). The loading screen, map voting and scoreboard show each scene by a readable name. It is built from the scene's terrain, forest and features in `sp_battle_scenes.xml` ("Wooded Plains with a lake", "Steppe 7"), and the names live in `Module/ModuleData/commander_battle_strings.xml`. A scene added to the pool later needs a name there too, or it shows its id.

```bash
powershell -ExecutionPolicy Bypass -File tools/Install-CampaignScenes.ps1
```

## Run a test server

1. Install **Mount & Blade II: Dedicated Server** (Steam tool, or SteamCMD app `1863440`) next to the game, and build. The build installs both modules into it.
2. Run `tools\Install-CampaignScenes.ps1`, or write your own config in the server's `Modules\Native` folder with `GameType CommanderBattle`.
3. Start the server with `tools\Start-Server.cmd` (double-click), or from PowerShell:

   ```bash
   powershell -ExecutionPolicy Bypass -File tools/Start-Server.ps1 -Restart
   ```

   The script waits until the server hosts, then prints its process id, the map, the RBM patch line and the log files. Options:
   - `-Restart` stops a running server first; without it the script refuses to start a second one.
   - `-Build` rebuilds and installs the solution first.
   - `-NoRbm` starts without RBM Multiplayer.
   - `-Token <token>` (or the `CB_SERVER_TOKEN` environment variable) lists the server publicly.
   - `-Port`, `-TickRate` and `-Config` change the defaults (7210, 60, `ds_config_commander_campaign.txt`).
   - `-DryRun` shows what would run.

   Listing the server needs a token: run `customserver.gettoken` in the game's console (Alt+~ in the multiplayer lobby). By hand, the command is:

   ```bash
   DedicatedCustomServer.Starter.exe _MODULES_*Native*Multiplayer*CommanderBattle*RBM.Multiplayer*_MODULES_ /dedicatedcustomserverconfigfile ds_config_commander_campaign.txt /dedicatedcustomserverauthtoken <token> /tickrate 60
   ```

   Leave out `*RBM.Multiplayer` to play without RBM.
4. Each client enables **Commander Battle** (and **RBM Multiplayer**, if the server runs it) in the launcher's Multiplayer tab and joins through the custom server list. Players and the server must run the same modules.

If the server runs on your own PC or LAN and you can't join through your public address, add a rewrite to `Modules\CommanderBattle\ModuleData\server_address_overrides.txt`.

## Test checklist

- [ ] The server starts with `GameType CommanderBattle` and appears in the custom server list.
- [ ] Both armies spawn in formation and the AI leads them into battle; the round ends when one side is wiped out.
- [ ] Taking command of a formation from the list gives you its orders; releasing it hands it back to the AI.
- [ ] A dead commander can take over a soldier in their formation.
- [ ] Opening the order bar in third person lifts the camera up and back. The mouse wheel changes the height, closing the bar brings it down, and move orders land where the screen centre points.
- [ ] Time-out with both armies still in the field: the defenders win the round.
- [ ] Routed soldiers run off the map and the round ends when the last fighting soldier of a side is gone (the log's "Fleeing" lines count them).
- [ ] Server tick rate holds with 1000 agents. CPU is the main risk, more so with RBM's AI.
- [ ] With RBM.Multiplayer, the RBM.Multiplayer log lists the patch classes, with none failed.
- [ ] With RBM.Multiplayer, AI infantry keeps Frontline spacing (stepping in and out of the line) and AI soldiers kick and bash.
- [ ] With RBM.Multiplayer, the log shows "RBM tactics registered" and "Terrain read started" each round, and the AI-led formations regroup into RBM's tactic shapes.
- [ ] With RBM.Multiplayer, the formation you command keeps exactly your soldiers while the AI regroups the others.
- [ ] With RBM.Multiplayer, your posture and stamina bars show and move when you block, and the target's bars show after an exchange.
- [ ] With RBM.Multiplayer, a soldier you take over moves at normal speed (no stamina slowdown, no rubber-banding).
- [ ] With RBM.Multiplayer, the second round starts cleanly (the log shows a reset per round).

## Known limitations

- **RBM: terrain read hitch.** RBM builds its battlefield grid when it reads the terrain, at the start of each round. That can stall the server briefly while it does.
- **Agent cap.** The engine caps a mission at about 2047 agents, and horses count. 1000 soldiers plus the commanders fit, but cavalry-heavy armies eat into the margin.
- **RBM: remote AI agents on clients.** Clients compute stock stats for the AI soldiers they see, while the server applies RBM's. Movement and outcomes come from the server, but an AI soldier's swing may look slightly faster or slower than it really is.
- **RBM: no listen server.** RBM's AI and Stance only run on the dedicated server.
