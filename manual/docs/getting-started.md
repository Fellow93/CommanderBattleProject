# Getting Started

This page covers installing Commander Battle as a player, joining a server, and hosting your own dedicated server.

## Requirements

| | |
|---|---|
| Game version | Mount & Blade II: Bannerlord **v1.5.5** |
| Commander Battle version | v0.3.0 |
| Required modules | Native, Multiplayer |
| RBM Multiplayer | Included in the Commander Battle module |
| The original RBM mod | Not needed: RBM Multiplayer is self-contained |

## Installing the client

Commander Battle comes as one module folder. Put it in the game's `Modules` folder so the result looks like this:

```
Mount & Blade II Bannerlord\
  Modules\
    CommanderBattle\
      SubModule.xml
      bin\
      GUI\
      ModuleData\
```

Close the launcher and the game before you copy it.

!!! warning "Updating from v0.2.1 or older"
    RBM Multiplayer used to be a second folder, `Modules\RBM.Multiplayer`. Delete it: it is now part of
    `CommanderBattle`, and with both installed the game loads it twice.

## Enabling in the launcher

1. Start the Bannerlord launcher and open the **Multiplayer** tab.
2. Tick **Commander Battle**.
3. Load order: **Native**, **Multiplayer**, **Commander Battle**.

!!! warning "Same version"
    You and the server must run the same version of the module. Commander Battle's troops take the place of the stock multiplayer classes, so different versions disagree about who is who. If joining fails with a module mismatch, update to the server's version.

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

On the PC that hosts, you don't have to do this by hand: every time the server starts (`Start-Server`, or the server manager's **Start / Restart**), it looks up your public address at `api.ipify.org` and adds the `= 127.0.0.1` line if it is missing. Internet providers change that address now and then, so restart the server when joining your own server stops working. A second PC in your home network still needs its own line, written by hand.

## Hosting a dedicated server

Commander Battle runs on a dedicated server. RBM Multiplayer's AI and posture system only run there, never on a listen server, and a listen-server host can't be the general.

### The short way

Once you have what is listed below, double-click `tools\Start-Server.cmd`. On a first run it builds and installs the module, links the maps and writes the server config, then starts the server. Later it just restarts the server. It warns when you have no server token yet (see the note under Step 3). The steps below are what it does, for when you want to do them yourself or set an admin password.

### The server manager

Double-click `tools\Server-Manager.cmd` for a window that does all of this:

- **State:** whether the server runs, its map, RBM Multiplayer's patch line, and the server's name with whether it needs a password to join.
- **Settings:** the server name, the join password and the admin password, as they are in the config. Change them and click **Save settings**. An empty password means none.
- **Setup checks:** the dedicated server, the mod in the game and on the server, whether both have the same build, the maps, the config, the admin password, the server token, and what building needs. A failed check says what to do.
- **Buttons:** Start / Restart, Stop, Build, Install maps, Open logs. It asks before disconnecting players.

Changed settings need a server restart. The window can't check your router: forward UDP port 7210 yourself.

Each step is a double-click file too:

| File | What it does |
|---|---|
| `tools\Build.cmd` | Stops a running server, then builds and installs the module. Close the game first. |
| `tools\Install-CampaignScenes.cmd` | Links the maps and rewrites the server config. It keeps the admin password already there. |
| `tools\Start-Server.cmd` | Starts the server, replacing one that is running. |

### What you need

- **Mount & Blade II: Dedicated Server**, from Steam's tools or SteamCMD (app `1863440`). Install it next to the game, as `Mount & Blade II Dedicated Server`.
- The Commander Battle source, checked out as `Modules\CommanderBattleProject` inside the game folder, with the .NET SDK and the .NET Framework 4.7.2 targeting pack. RBM Multiplayer also needs RBM's source next to it, as `Modules\RealisticBattleProject`.

### Step 1: build and install

From the repository folder:

```
dotnet build "CommanderBattle.sln" -c Release
```

The build installs the module into the game and into the dedicated server. Close the game first: it locks the client files.

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
| `-ServerName <name>` | The name in the custom server list. Without it, a rerun keeps the name already there. |
| `-GamePassword <password>` | The password players need to join. Without it, a rerun keeps the password already there. `-GamePassword ''` removes it. |
| `-AdminPassword <password>` | Writes the admin password into the config. Without it, a rerun keeps the password already there. `-AdminPassword ''` removes it. |

The script rewrites the config from scratch each time it runs, so make your own changes to a copy (see [Server Settings](settings.md#your-own-config)).

### Step 3: start the server

Double-click `tools\Start-Server.cmd`, or run from PowerShell:

```
powershell -ExecutionPolicy Bypass -File tools\Start-Server.ps1 -Restart
```

The script starts the server, waits until it hosts a map, then prints the server's process id, the map, RBM Multiplayer's patch line and where the logs are. The patch line should read **0 failed**.

| Parameter | What it does |
|---|---|
| `-Restart` | Stops a running server first. Without it, the script refuses to start a second one. |
| `-Build` | Builds and installs the solution first (stops a running server, as `-Restart`). |
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

Run the server from the dedicated server's `bin\Win64_Shipping_Server` folder, **from PowerShell, not Git Bash**. Git Bash turns the `/` options into file paths, and the server starts without its config.

```
TaleWorlds.Starter.DotNetCore.exe _MODULES_*Native*Multiplayer*CommanderBattle*_MODULES_ /dedicatedcustomserverconfigfile ds_config_commander_campaign.txt /dedicatedcustomserverauthtoken <token> /tickrate 60 /port 7210 /dedicatedcustomserver 7210 USER 0
```

On v1.5.6, don't use `DedicatedCustomServer.Starter.exe`: it loads TaleWorlds' web panel, which fails on the server's bundled .NET, and the server quits without a message.

Leave out the token for a server that isn't listed.

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
