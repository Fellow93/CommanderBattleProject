# Named formations

**Status:** idea · **Size:** S–M

## Goal

A commander gives their formation a name ("The Iron Wall"). The name shows wherever the formation is named for players: the formation labels, the formation list, the scoreboard, the general's suggestions, and later the announcements and the battle report. It gives commanders an identity that lasts from round to round.

## What exists today

- A formation is named by its role and number: `FormationSlot.DisplayName` ("Infantry 3", "General", `CommanderBattle/FormationSlots.cs`). It is used in the formation list (`FormationSelectionVM`), the scoreboard's Formation column (`CommanderBattleScoreboardData`), the troop screen's title (`TroopSelectionVM`), the suggestion pop-ups (`MissionMultiplayerCommanderBattleClient.GeneralCommand.cs`), the Alt panel (`CommanderBattleAIStatusView`) and the server's messages and log.
- The label beside each player-led formation's icon reads "<troop name> (<player>)" (`CommanderBattleAllyPlayerMarkersView`, around the `TroopName` line).
- The general's path labels already go through a server check: 1-40 characters, `<`, `>` and control characters taken out (`MissionMultiplayerCommanderBattle.GeneralOrders.cs`).
- Nothing of Commander Battle's own is kept on the client between sessions. The order camera's zoom is kept for the session only.

## Design

**The name belongs to the commander, not the formation slot.** It shows on whatever formation they lead, and follows them if they take another one or swap sides. A formation the AI leads has no name.

**Setting it:** a "Name" button on your own row of the formation list (next to "Troops") opens a text popup (the same kind as the path label popup). 1-24 characters. An empty name clears it.

**Server:** `RequestFormationName` from the client. The server cleans the name as it does path labels, keeps it per peer, broadcasts `CommanderFormationName` (peer, name) and sends every known name to a player who joins. Log: "Fellow93 named their formation 'The Iron Wall'".

**Shown as:**
- Formation label: "The Iron Wall · Knights (Fellow)".
- Formation list: the name on the row, with "Cavalry 3" kept beside it, so the order bar's number still matches.
- Scoreboard: "The Iron Wall · Heavy Cavalry 3 · Lance and shield".
- Suggestions and Alt panel: "General: The Iron Wall, move here".
- Server messages and the log keep `DisplayName`; the log adds the name where it helps.

A new `FormationSlot.Title` (the name if the commander has one, else `DisplayName`) keeps `DisplayName` stable for logs and order-bar references.

**Kept between sessions, on each player's client** (the user's decision). Setting a name writes it to `Configs\CommanderBattle_<playerId>.xml` in the player's own game folder (`Documents\Mount and Blade II Bannerlord\Configs` on Windows), so a regular keeps "The Iron Wall" without retyping it. The path comes from `EngineFilePaths.ConfigsPath` (`PlatformFileType.User, "Configs"`), as the stock `MPPerkSelectionManager` does for each player's default perks (`MPDefaultPerks_<playerId>.xml`); that folder is the game's own per-user one on every platform. Keying the file by player id keeps two accounts on one machine apart.
- On joining a side, the client reads the file and sends the name with `RequestFormationName`. The server cleans it on every join, not only when it's set: the client is the source each time.
- The server keeps names for its session only and stores nothing, so a player's name follows them to any server.
- This is Commander Battle's first file on the client.

## Things to work out

- The text popup: reuse the general mode's label popup (`CommanderBattleGeneralModeView`) or the stock text inquiry (`InformationManager.ShowTextInquiry`). Read `..\RealisticBattleProject\docs\BannerlordGauntletUI.md` first.
- How long the scoreboard column can get before it scrolls badly.

## Files it touches

- New `CommanderBattle/Network/RequestFormationName.cs`, `CommanderFormationName.cs` (shared).
- `CommanderBattle/FormationSlots.cs` (`Title`), `MissionMultiplayerCommanderBattle.cs` (handler, join sync), `MissionMultiplayerCommanderBattleClient.cs` (names by peer).
- `CommanderBattle.View/FormationSelectionVM.cs` + prefab (Name button), `CommanderBattleAllyPlayerMarkersView.cs`, `CommanderBattleScoreboardData.cs`, `MissionMultiplayerCommanderBattleClient.GeneralCommand.cs`, `CommanderBattleAIStatusView.cs`.
- `README.md`.

## Open questions for the user

1. Admin control: should the admin panel get "Clear formation name" (and the server log every name) for names that cross the line? The client sends its saved name again on every join, so clearing it once doesn't stick: the admin's command would have to block that player's name for the rest of the server's session.
2. Name per commander (follows the player), or per formation for the round?
3. Should the general be able to name his bodyguard, or does "General" stay?

Answered: the name is kept between sessions on each player's client (see "Kept between sessions" above).

## Done when

- In game: naming your formation shows the name for every player of both sides in the labels, the formation list and the scoreboard, and for the general in his suggestions. A player who joins later sees it. Taking another formation takes the name with you; leaving clears it from the formation.
- Restarting the game and joining again, on any server, restores the name.
- Names with `<`, `>` or braces show as plain text and break no UI.
- Log: one line per name set.
