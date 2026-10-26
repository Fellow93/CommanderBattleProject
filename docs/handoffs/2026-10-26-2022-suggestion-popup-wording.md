# Handoff: the suggestion pop-up worded like the marker

Session of 2026-10-26, written 20:22. It follows `2026-10-26-1926-suggestion-marker-addressee.md` (same session).

## What changed

The user: word the pop-up the same way as the marker, e.g. "Fellow, move here".

- **`CommanderBattle/GeneralCommands.cs`:** new `GetSuggestionLine(slot, kind, hasPlace)`, the one wording for both. It reads "<commander's name>, <what>", with the formation's name if the commander has left.
- **`MissionMultiplayerCommanderBattleClient.GeneralCommand.cs` `AnnounceSuggestion`:**
  - the pop-up and log line use it for the commander (with the sound) and for the general's echo;
  - "General: Infantry 1, ..." and "Suggested to ..." are gone.
- **`CommanderBattle.View/CommanderBattleAIStatusView.cs`:** the marker uses it too; the view's own `GetAddresseeName` is gone.

## State

- **Built and installed:** client and server folders; the server's `CommanderBattle.dll` hash matches. No network message changed.
- **No server is running** at 20:22. The last one (pid 30484, from the other session) has stopped. Start one with `tools\Start-Server.ps1`.
- **Not played. Not committed.**
