# Handoff: admin commands in the admin panel (first pass)

Session of 2026-10-26, written 09:20. It follows `2026-10-26-0902-aggressive-frontline.md`. That handoff's work and the 08:40 one's are still uncommitted in the same tree.

## State

- **Not committed:** everything below, plus everything the 08:40 and 09:02 handoffs list.
- **Installed:** both sides, built at 09:19 by `tools\Start-Server.ps1 -Build -Restart`. The client DLLs copied too, because no game was running.
- **Running:** server pid 9060 (it stopped pid 5996). Map `battle_terrain_biome_027`. Patch line: "94 classes applied, 0 failed, 3 skipped". The mission opened, and round 0 preparation started with no exceptions.

## What was done

The user picked the **in-game admin panel**, with these first-pass commands: change maps, change factions, restart round, swap sides, kick from formation. They had no preference on runtime settings, so none were built.

Research (decompiled into the session scratchpad, not the repo):
- **Admin flag.** The server checks `NetworkCommunicator.IsAdmin`, which the lobby sets when a player joins with the admin password.
- **Panel hook.** The stock panel (`MissionGauntletAdminPanel`, Multiplayer.GauntletUI) has a public `AddOptionProviderCreator`. Its option and action helpers are internal, so we implement `IAdminPanelOptionProvider`, `IAdminPanelOptionGroup` and `IAdminPanelAction` ourselves. `MultiplayerOptions.OptionType` is a fixed enum, so mod settings can't go through `AdminUpdateMultiplayerOptions`.
- **Map and factions.** The panel's stock **Mission Options** already cover them: map, attacker culture, defender culture, then Apply and Start Mission. That group shows only with automated battle switching, which our config turns on. Picking a map turns map voting off (`IsMapSelectedByAdmin`). Picking both cultures turns culture voting off. Nothing was added for these.
- **Round control.** `MultiplayerRoundController` has no public end-round method. Ending a round goes through our own `CheckForRoundEnd`.

Files:
- `CommanderBattle/Network/AdminCommand.cs` (new, shared): a FromClient message carrying `AdminCommandType` (RestartRound, SwapSides, ReleaseFormation) and an optional target peer.
- `CommanderBattle/MissionMultiplayerCommanderBattle.Admin.cs` (new): the class is now `partial`. It holds:
  - the `IsAdmin` check, with a log line for each command and for each refusal;
  - `Announce`, a `ServerMessage` sent to everyone;
  - **Restart Round:** `_forceRoundEnd` makes `CheckForRoundEnd` true. `OnPreRoundEnding` then sets winner None and `GameModeSpecificEnded`, and decrements `RoundCount` so `BeginNewRound` replays the same number. It only runs during InProgress.
  - **Swap Sides:** during preparation it swaps at once. Otherwise it is held as `_swapSidesPending` (and the round is restarted if a fight is on), then `ApplyPendingSwap` runs it on the first tick of the next preparation, after `ResetMission`. That avoids `ChangeTeamServer` killing live agents.
  - How the swap works: every player changes team first, and only then does anyone claim. Each player keeps their slot index if the role matches there, otherwise takes the first free slot of the role, otherwise falls to `PlaceQueuedPeers`.
  - **Remove Commander:** `ChangeTeamServer(target, SpectatorTeam)`. The existing team-change handlers release the slot and hand the formation to the AI.
- `CommanderBattle/MissionMultiplayerCommanderBattle.cs`: partial, the handler registration, `ApplyPendingSwap()` in the tick, `_forceRoundEnd` in `CheckForRoundEnd`, and the restart branch in `OnPreRoundEnding`.
- `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs`: `RequestAdminCommand` (listen-server path included).
- `CommanderBattle.View/CommanderBattleAdminPanel.cs` (new):
  - `CommanderBattleAdminPanelView` registers the provider on the stock panel in `OnMissionScreenInitialize`. The panel clears its providers on finalize, so it registers once per mission.
  - The provider adds a "Commander Battle" group with the three actions. Each closes the panel after sending.
  - Remove Commander first opens a multi-selection inquiry listing the commanders from the client's slot table.
- `CommanderBattle.View/CommanderBattleMissionViews.cs`: the view is added after the admin panel handler.
- `CommanderBattle.View/CommanderBattle.View.csproj`: references `TaleWorlds.MountAndBlade.Multiplayer.GauntletUI.dll`.
- `README.md`: a new "Admin panel" section and test checklist items.
- `docs/plans/admin-commands.md` and `docs/plans/README.md`: status set to in progress.

## Verified vs not

- **Verified:** it builds with 0 errors and 0 warnings. The server starts with 0 failed patches, opens the mission, and has clean mod logs.
- **Not verified (needs a client joined as admin):**
  - The Commander Battle group shows in the panel, under or above the stock groups.
  - Restart Round replays the round number and gives no score.
  - Swap Sides mid-fight restarts the round and moves everyone at the next preparation, at the same formation numbers where the roles match.
  - Remove Commander moves the player to the spectators, and the AI picks up the formation.
  - A non-admin's request is refused.
  - The stock Mission Options group (map and cultures, Apply and Start) works for this mode on our server. Check that the vote is skipped.

## Open items

- Test the above in game. You need the admin password: `AdminPassword` in `ds_config_commander_campaign.txt`. Check whether `Install-CampaignScenes.ps1` writes one. If it doesn't, add one locally, or add a parameter to the script.
- Not built from the plan: army size for the next round, the toggles (horse speed, spawn jitter, damage curve), and force tactic or freeze AI. Army size needs `CommanderBattleSettings` budgets to become runtime values on the server. The troop counts already reach the clients through `SpawnLayoutSeed`.
- Remove Commander doesn't stop the player from rejoining a side and taking a formation straight away. Add a cooldown or block if that turns out to matter.
- Restart Round is greyed out outside a fight. In preparation, nothing needs restarting.

## Traps

- `MissionGauntletAdminPanel.OnMissionScreenFinalize` clears `_optionProviderCreators`, and `OnMissionScreenInitialize` adds the default provider. A provider registered anywhere else is lost on the next map.
- Calling `ChangeTeamServer` on a peer whose agent is alive kills it (`KillInfo.TeamSwitch`). That is why Swap Sides waits for the tick after `ResetMission` and never runs from `OnRoundStateChanged(Preparation)`, which fires before the reset.
- On the dedicated server, `RoundController.RoundCount` has a public setter that broadcasts `RoundCountChange`. After a restart, clients briefly show the round number minus one during Ending and Ended.
