# Handoff: Join as Admin, 16 players, usable maps for the admin panel

Session of 2026-10-26, written 09:46. It follows `2026-10-26-0922-admin-password-param.md`.

## State

- **Not committed:** `tools/Install-CampaignScenes.ps1`, `README.md`, this handoff, and everything the earlier handoffs of the day list.
- **Server config rewritten.** It has an `AdminPassword` (the user's choice; it lives only in the server's config file, never in the repo), `MaxNumberOfPlayers 16`, and 84 `add_map_to_usable_maps <scene> CommanderBattle` lines.
- **Running:** server pid 30744, map `battle_terrain_003`. Patch line: "94 classes applied, 0 failed, 3 skipped".

## What was done

- **Admin login.** The user's normal join didn't ask for the admin password. The stock way is to right-click the server in the custom server list and choose **Join as Admin** (`MPCustomGameVM`, `CustomGameJoinType.Admin`). I started an in-game login (an always-visible escape menu item, a password check on the server, setting `IsAdmin` by reflection). The user stopped it and chose the stock route, and the unused `AdminLogin.cs` was deleted. The README says to use Join as Admin.
- **Max players.** `MaxNumberOfPlayers` in the script went from 32 to 16, at the user's request.
- **Empty map list.** The admin panel's map list (Mission Options) was empty. It shows `MultiplayerIntermissionVotingManager.UsableMaps`, which only `add_map_to_usable_maps` fills; the vote pool (`add_map_to_automated_battle_pool`) is a separate list. The script now adds every scene as usable for CommanderBattle. The panel also filters the list by its Game Type, so pick CommanderBattle there first.

## Verified vs not

- **Verified:** the config holds the new lines, the server restarts with 0 failed patches, and the command exists on the dedicated server (`ListedServerCommandManager`).
- **Not verified:**
  - The map list now fills, after Join as Admin and Game Type CommanderBattle.
  - Apply and Start Mission opens the picked map with the picked cultures.
  - The Commander Battle group's commands in play.

## Traps

- Usable maps reach a client only when it joins as an admin (`BaseNetworkComponent`). An admin who joined before the server had them must reconnect.
