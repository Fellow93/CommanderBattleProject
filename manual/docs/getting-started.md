# Getting Started

This page covers installing Commander Battle as a player, joining a server, and hosting your own dedicated server.

## Requirements

| | |
|---|---|
| Game version | Mount & Blade II: Bannerlord **v1.5.5** |
| Commander Battle version | v0.2.0 |
| Required modules | Native, Multiplayer |
| RBM Multiplayer | Optional, but needed to join a server that runs it |
| The original RBM mod | Not needed: RBM Multiplayer is self-contained |

## Installing the client

Commander Battle comes as two module folders. Put them in the game's `Modules` folder so the result looks like this:

```
Mount & Blade II Bannerlord\
  Modules\
    CommanderBattle\
      SubModule.xml
      bin\
      GUI\
      ModuleData\
    RBM.Multiplayer\
      SubModule.xml
      bin\
      GUI\
```

Close the launcher and the game before you copy them.

## Enabling in the launcher

1. Start the Bannerlord launcher and open the **Multiplayer** tab.
2. Tick **Commander Battle**.
3. Tick **RBM Multiplayer** as well, if the server you want to join runs it.
4. Load order: **Native**, **Multiplayer**, **Commander Battle**, **RBM Multiplayer**.

!!! warning "Same modules, same version"
    You and the server must run the same modules, at the same version. Commander Battle's troops take the place of the stock multiplayer classes, so different versions disagree about who is who. If joining fails with a module mismatch, update to the server's version.

### The maps

Commander Battle is fought on the campaign's field-battle maps, taken from your own game. The first time you start the game with the mod, it links them into `Modules\CommanderBattle\SceneObj` by itself. Nothing is downloaded. If a map fails to load the first time, restart the game once.

### Other multiplayer modes

Commander Battle's troops replace the stock classes by name, so while the module is enabled you see them in every multiplayer mode, not only in Commander Battle. Disable the module to play the other modes with the stock classes.

## Joining a server

1. Open **Multiplayer**, then the **Custom Server List**.
2. Find the server and join it.
3. Pick a side. You are put in charge of a free formation at once, and the troop selection screen opens so you can change its class, weight and weapon setup. See [Commanding a Formation](features/commanding.md).

The round starts with a short preparation time. Use it to pick your troops and look at the formation list (N). Once the armies spawn, the fight is on.

### When the server runs on your own PC or network

If the server runs on your own PC or in your home network, joining through your public address can fail, because many routers don't send that traffic back inside. Add a rewrite to `Modules\CommanderBattle\ModuleData\server_address_overrides.txt`:

```
203.0.113.25 = 127.0.0.1        (server on this PC)
203.0.113.25 = 192.168.1.50     (server on another PC in your LAN)
```

The left side is the address the server list gives you, the right side the address to connect to. Updates keep your entries.

## Hosting a dedicated server

Commander Battle runs on a dedicated server. RBM Multiplayer's AI and posture system only run there, never on a listen server, and a listen-server host can't be the general.

### What you need

- **Mount & Blade II: Dedicated Server**, from Steam's tools or SteamCMD (app `1863440`). Install it next to the game, as `Mount & Blade II Dedicated Server`.
- The Commander Battle source, checked out as `Modules\CommanderBattleProject` inside the game folder, with the .NET SDK and the .NET Framework 4.7.2 targeting pack. RBM Multiplayer also needs RBM's source next to it, as `Modules\RealisticBattleProject`.

### Step 1: build and install

From the repository folder:

```
dotnet build "CommanderBattle.sln" -c Release
```

The build installs both modules into the game and into the dedicated server. Close the game first: it locks the client files.

### Step 2: the maps and the server config

```
powershell -ExecutionPolicy Bypass -File tools\Install-CampaignScenes.ps1 -AdminPassword <password>
```

`tools\Install-CampaignScenes.ps1` does two things:

- It links the campaign's field-battle maps into `Modules\CommanderBattle\SceneObj`, for the game and for the dedicated server. The server has no campaign files of its own, so it needs this step.
- It writes the server config, `Modules\Native\ds_config_commander_campaign.txt` in the dedicated server folder. See [Server Settings](settings.md) for what is in it.

| Parameter | What it does |
|---|---|
| `-GameDir <folder>` | The Bannerlord folder. By default, two folders above the repository. |
| `-ServerDir <folder>` | The dedicated server folder. By default, `Mount & Blade II Dedicated Server` next to the game. Skipped if it doesn't exist. |
| `-Copy` | Copies the map folders instead of linking them, for example for a server on another drive or machine. Links take no disk space and need no admin rights. |
| `-AdminPassword <password>` | Writes the admin password into the config. Without it, a rerun keeps the password already there. `-AdminPassword ''` removes it. |

The script rewrites the config from scratch each time it runs, so make your own changes to a copy (see [Server Settings](settings.md#your-own-config)).

### Step 3: start the server

Double-click `tools\Start-Server.cmd`, or run from PowerShell:

```
powershell -ExecutionPolicy Bypass -File tools\Start-Server.ps1 -Restart
```

The script starts the server, waits until it hosts a map, then prints the server's process id, the map, RBM Multiplayer's patch line and where the logs are. With RBM Multiplayer, the patch line should end in **0 failed**.

| Parameter | What it does |
|---|---|
| `-Restart` | Stops a running server first. Without it, the script refuses to start a second one. |
| `-Build` | Builds and installs the solution first (stops a running server, as `-Restart`). |
| `-NoRbm` | Starts without RBM Multiplayer. Commander Battle then uses its own AI. |
| `-Token <token>` | Lists the server in the public custom server list. Also read from the `CB_SERVER_TOKEN` environment variable. |
| `-Port <port>` | UDP port. Default 7210. |
| `-TickRate <rate>` | Server tick rate. Default 60. |
| `-Config <file>` | The config file in the server's `Modules\Native` folder. Default `ds_config_commander_campaign.txt`. |
| `-ServerDir <folder>` | The dedicated server folder, if it isn't next to the game. |
| `-DryRun` | Shows what would run, without stopping, building or starting anything. |

!!! note "The server token"
    Without a token, the server only takes direct connections. To get one, open the game's console in the
    multiplayer lobby (Alt+~) and run `customserver.gettoken`. Keep it secret: the script never prints it.

### Starting by hand

Run the starter from the dedicated server's `bin\Win64_Shipping_Server` folder, **from PowerShell, not Git Bash**. Git Bash turns the `/` options into file paths, and the server starts without its config.

```
DedicatedCustomServer.Starter.exe _MODULES_*Native*Multiplayer*CommanderBattle*RBM.Multiplayer*_MODULES_ /dedicatedcustomserverconfigfile ds_config_commander_campaign.txt /dedicatedcustomserverauthtoken <token> /tickrate 60 /port 7210
```

Leave out `*RBM.Multiplayer` to play without RBM Multiplayer, and the token for a server that isn't listed. Players then enable the same modules as the server.

### The admin password

The admin password lets players run the [Admin Panel](features/admin.md). Set it with `Install-CampaignScenes.ps1 -AdminPassword <password>`, or write an `AdminPassword <password>` line into the config yourself, before `start_game`. Restart the server after changing it: the server hands the password to the lobby when it registers.

### Logs

The server's and each client's logs are in `C:\ProgramData\Mount and Blade II Bannerlord\logs\`, one file per process:

| File | Contents |
|---|---|
| `CommanderBattle_<pid>.log` | Maps and rounds, each round's result, players taking formations and picking troops, the general's orders, admin commands, anything that failed. |
| `RBM.Multiplayer_<pid>.log` | RBM Multiplayer's patch summary and anything that failed. |
| `rgl_log_<pid>.txt` | The game engine's own log. |

When something goes wrong, look first for "0 failed" in the RBM patch line and for errors in the Commander Battle log.
