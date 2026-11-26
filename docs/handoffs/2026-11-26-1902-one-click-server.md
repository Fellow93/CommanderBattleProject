# One-click server start (2026-11-26 19:02)

So a friend with the repo can host by double-clicking one file.

## Done

- `tools\Start-Server.ps1`: sets itself up on a first run.
  - Module not on the server: it builds (as `-Build`). Without a .NET SDK it stops and says to unzip the release into both `Modules` folders.
  - Default config missing: it runs `Install-CampaignScenes.ps1` after the build. A missing custom `-Config` still stops it.
  - No `-Token` and no `Documents\Mount and Blade II Bannerlord\Tokens\DedicatedCustomServerAuthToken.txt`: a warning with the `customserver.gettoken` instructions.
- `tools\Start-Server.cmd`: a double-click (no arguments) passes `-Restart`, so it replaces a running server instead of refusing.
- `tools\Build.cmd`: double-click build. It stops a running server (`Start-Server.ps1 -Stop`), builds the solution and says whether it worked.
- `tools\Install-CampaignScenes.cmd`: double-click map install and config; arguments go to the `.ps1`.
- `tools\Server-Manager.ps1` + `.cmd`: a WinForms window for the host. It shows the server's state (pid, map, RBM patch line, refreshed every 4 s) and ten setup checks with a fix for each, and has buttons for Start / Restart, Stop, Build, Install maps, Set password and Open logs. It only calls the other tools. Start and Build open their own console window; Stop, the maps and the password run in the window. `-Check` prints the state as text, `-Snapshot <png>` draws the window to a file.
- `README.md` ("Run a test server") and `manual\docs\getting-started.md` ("The short way") describe it.

## After the first commit (f605027)

- `tools\Install-CampaignScenes.ps1`: `-ServerName` and `-GamePassword`, carried over on a rerun like `-AdminPassword` (an empty name goes back to "Commander Battle", an empty password removes the line). The name used to be reset on every run.
- `tools\Server-Manager.ps1`: shows the server name and whether a join password is set; three fields (name, join password, admin password) filled from the config, saved together with **Save settings**. This replaces the "Set password" button.
- Manual (`settings.md`, `getting-started.md`) and README describe them.
- Checked on a throwaway server folder: set all three, rerun keeps them, empty name and join password reset them. Not checked: the name and the join password in the game's server list.

## Joining your own server

- The host could not join: the lobby handed out the new public address (95.103.152.89), the installed `server_address_overrides.txt` only had two older ones, and the server dropped the peer after 20 s. Line added to the installed file by hand.
- `tools\Start-Server.ps1` now does it on every start: it asks `api.ipify.org` for the public address and appends `<address> = 127.0.0.1` to the game's `server_address_overrides.txt` when it is missing. A failure is a warning.
- Checked by running that block against a throwaway game folder (new file, second run, file without a final line break). Not checked: a real server start with it, and the join in game.

## Combat mode in the manager (after ced491c)

- `tools\Server-Manager.ps1`: an **RBM Combat** checkbox, read from the server's `combat_mode.txt` and written by **Save settings** (only on a change, so hand-written comments stay). The state line shows "RBM Combat on/off".
- Checked: the readout on the real file (RBM), the parser on no file, a comment plus `rbm`, and `Light`; the window drawn. Not checked: the checkbox clicked by hand and the file written through the window.

## Match settings (after ced491c)

- `tools\Install-CampaignScenes.ps1`: `-CultureTeam1`, `-CultureTeam2`, `-RoundTimeLimit` (60-3600), `-RoundPreparationTimeLimit` (2-60), `-RoundTotal` (1-99), `-AllowPollsToChangeMaps` and `-MapVoting` (`True`/`False`), all kept on a rerun. The limits are from `MultiplayerOptions`. `-MapVoting False` leaves out `enable_map_voting`.
- `tools\Server-Manager.ps1`: two culture dropdowns, round minutes, rounds per map, preparation seconds, and checkboxes for the map vote and the map-change polls, saved by **Save settings**. The window is 88 px taller.
- Manual (`settings.md` "The match settings", `getting-started.md`) and README.
- Checked on a throwaway server folder: defaults in a new config, all set, kept on a rerun, voting off and on and kept, out-of-range values refused; the window drawn. Not checked: Save clicked by hand, and a server run with voting off (what the server picks as the next map then).

## Checked

- `-DryRun` against the real install (unchanged command) and against an empty fake server folder (shows the build and the scene install).

- `Build.cmd` and `Install-CampaignScenes.cmd` run for real with no server or game open: build 0 errors, 84 scenes already linked, config rewritten with its password kept.

- `Start-Server.ps1 -Restart` for real after the change: hosting, 126 applied / 0 failed (pid 9196, stopped again afterwards).
- Server manager: `-Check` and `-Snapshot` with the server stopped and running (state, map and patch line read correctly, all checks OK); the console command line it builds, run with `-DryRun`.

## Not checked

- A real first run on a clean machine.
- The manager's buttons clicked by hand, and how a failed check looks.
- Not committed.
