# Handoff: suggestion markers addressed to the commander

Session of 2026-10-26, written 19:26. It corrects `2026-10-26-1922-suggestion-marker-general-name.md` (same session).

## What changed

The user wants the marker to name **the player the order is for, worded as if the general orders them**, not the general.

- **`CommanderBattle.View/CommanderBattleAIStatusView.cs` `RebuildSuggestions`:** the marker text is "<addressee>, <what>", for example "Fellow, move here".
  - The same text shows for the commander and for the general.
  - `GetAddresseeName(slot)` takes the formation's commander's `UserName`, or the formation's name ("Infantry 3") if the commander has left.
  - The 19:22 `GetGeneralName` is gone.
- **Unchanged:** the centre pop-up still reads "General: Infantry 1, move here" (`MissionMultiplayerCommanderBattleClient.GeneralCommand.cs`).

## State

- **Built and installed:** the client View project alone, 0 errors. It is client-only, so the server doesn't need a restart.
- **Not played. Not committed.**
