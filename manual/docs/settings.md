# Server Settings

Commander Battle has no settings screen. A server is set up with its **server config file**, and an admin changes the match while it runs from the [Admin Panel](features/admin.md). The battle rules themselves are built into the mod and are the same on every server.

## The server config

`tools\Install-CampaignScenes.ps1` writes a ready config to the dedicated server's folder:

```
Mount & Blade II Dedicated Server\Modules\Native\ds_config_commander_campaign.txt
```

`tools\Start-Server.ps1` uses it by default. Each line is one setting or command:

| Line | Value | What it does |
|---|---|---|
| `ServerName` | Commander Battle | The name in the custom server list. |
| `GameType` | CommanderBattle | The game mode. Every Commander Battle config needs this line. |
| `CultureTeam1` | vlandia | The first side's culture. |
| `CultureTeam2` | battania | The second side's culture. |
| `MaxNumberOfPlayers` | 18 | Room for both sides' 8 commanders and their general. |
| `RoundPreparationTimeLimit` | 45 | Preparation time at the start of each round, in seconds. |
| `RoundTimeLimit` | 900 | Length of a round, in seconds (15 minutes). When it runs out, the defenders win. |
| `RoundTotal` | 2 | Rounds per match, so each map is played twice. |
| `MapTimeLimit` | 60 | Time limit for a map, in minutes. |
| `AllowPollsToChangeMaps` | True | Players can call a vote to change the map. |
| `AdminPassword` | yours | The password for **Join as Admin**. Only written when you give one; see below. |
| `add_map_to_automated_battle_pool` | one line per map | The maps offered in the vote after each match: every campaign field-battle map found. The game offers at most 100. |
| `add_map_to_usable_maps` | one line per map | The maps the admin panel's map list offers. |
| `set_automated_battle_count` | -1 | Keep playing matches, with no end. |
| `enable_map_voting` | | Players vote for the next map after each match. |
| `start_game` | | Starts the server. |
| `enable_automated_battle_switching` | | Moves on to the next map after each match. The admin panel's **Change Map Now** needs it. |

The cultures are `vlandia`, `empire`, `khuzait`, `aserai`, `sturgia` and `battania`.

### The admin password

`Install-CampaignScenes.ps1 -AdminPassword <password>` writes the `AdminPassword` line. Run it again without the parameter and the password already in the config is kept; `-AdminPassword ''` removes it. The line must come before `start_game`, because the server hands the password to the lobby when it registers. Restart the server after changing it.

### Your own config

`Install-CampaignScenes.ps1` rewrites its config from scratch every time it runs. To keep changes of your own, save a copy under another name in the same `Modules\Native` folder and start the server with it:

```
powershell -ExecutionPolicy Bypass -File tools\Start-Server.ps1 -Config my_commander_config.txt -Restart
```

Your config needs `GameType CommanderBattle`, and its maps must be ones the server has: the campaign maps linked by `Install-CampaignScenes.ps1`.

## Starting the server

`tools\Start-Server.ps1` takes the port (default 7210), the tick rate (default 60), the config, a token to list the server publicly, and whether to run RBM Multiplayer. Every parameter is listed in [Getting Started](getting-started.md#step-3-start-the-server).

## Changing a running match

An admin can change these while the server runs, from the [Admin Panel](features/admin.md):

- the next map and both cultures, or a change of map right away;
- the number of rounds and the round time limit, for the current match too;
- the map time limit, from the next map;
- restarting a round, swapping the sides and removing a commander.

## Built-in rules

These are part of the mod and can't be changed in the config:

| Rule | Value |
|---|---|
| Formations per side | 8, plus the general |
| General's bodyguard | 6 mounted riders |
| Formation budget | 54 men for infantry and ranged, 44 for cavalry and horse archers; heavier troops field fewer, lighter ones more |
| Horse speed | 85% of the stock multiplayer top speed |
| Bow and crossbow draw and reload | 75% of the stock speed |
| Fleeing soldiers leave the battle | within 25 m of the map edge, or after 2 minutes of fleeing |
| Inactivity kick | 3 minutes without moving or looking around; giving orders counts as activity |
| Taking over a soldier after you fall | after about 3 seconds |
| Start position changes per round | up to 40 m to the side, 40 m back or 15 m forward, and a small turn |
| Allied player names on Left Alt | within 100 m |
| Cheer with your men | soldiers within 20 m of an enemy don't join; 10 seconds before a cheer echoes again |
| General's drawn orders | up to 10 per side, labels up to 40 characters, gone at the end of the round |
| Time-out | the defenders win |

### RBM Multiplayer

RBM Multiplayer always runs with **RBM's default settings**, the same on the server and every client. Players' singleplayer RBM configs are never read. It differs from RBM's defaults in two ways:

| Setting | RBM Multiplayer |
|---|---|
| RBM Combat | Off. A light damage curve takes its place: every blow is halved, and hard blows lose more. See [RBM Multiplayer](features/rbm.md#the-damage-curve). |
| Frontline attack and back-step weights | A more aggressive front rank than in singleplayer. |

Frontline runs for formations of more than 25 men, and AI archers on foot draw their sidearm when an enemy comes within 4 m.

To run Commander Battle without RBM Multiplayer, start the server with `-NoRbm`. Commander Battle then uses its own team AI.
