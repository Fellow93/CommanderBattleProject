# Handoff: Commander Battle's own admin panel groups

Session of 2026-10-26, written 10:09. It follows `2026-10-26-0946-admin-join-usable-maps.md`.

## State

- **Not committed:** `CommanderBattle.View/CommanderBattleAdminPanel.cs` (rewritten), `CommanderBattle.View/CommanderBattle.View.csproj` (references `TaleWorlds.PlayerServices.dll`), `README.md`, this handoff, and everything the earlier handoffs of the day list.
- **Installed:** both sides, built at 10:08. The client DLL copied, because no game was running.
- **Running:** server pid 30000, map `battle_terrain_t`. Patch line: "94 classes applied, 0 failed, 3 skipped".

## What was done

The user played as admin. Remove Commander worked: the 09:59 log line is "Admin Fellow93: ReleaseFormation". They reported two problems:
- the stock panel shows many settings they don't want;
- the stock map list still had no maps to pick, even though the client log shows all 84 usable maps arrived when they joined as admin (`MultiplayerIntermissionUsableMapAdded`).

The user chose what the panel keeps: map and factions, the round commands, kick/ban/mute, and rounds and time limits. Maps come from the pool.

`CommanderBattleAdminPanelView` now clears the stock panel's provider creators (the private field `_optionProviderCreators`, found by reflection) on its first tick, and adds only our provider. The provider has four groups:
- **Next Map:**
  - The map list is the vote pool (`MapVoteItems`) plus the usable maps for CommanderBattle plus the current map. Each shows by its `str_multiplayer_scene_name` text, followed by the scene id.
  - The culture lists show the main cultures that have multiplayer classes.
  - The **Change Map Now** action applies them and then calls `AdminEndMission`.
- **Match:** RoundTotal and RoundTimeLimit are sent for both the current and the next map. MapTimeLimit is sent for the next map only.
- **Commander Battle:** Restart Round, Swap Sides, Remove Commander, unchanged.
- **Players:** the stock kick, ban, mute and unmute, through the public `MultiplayerAdminComponent` methods.

Options are applied through the stock `AdminUpdateMultiplayerOptions`. The stock server handler has side effects that `SendOptions` works around:
- It always sends GameType as "". When the current game type matches `InitialGameType`, that counts as the default game type.
- It sends Map as "" unless the admin changed the map. A message without a map entry, or with one, makes the handler turn map voting off (`IsMapSelectedByAdmin`). With "", voting stays on.
- It sends cultures as a pair only when one of them changed, and as "" otherwise. That leaves the culture vote as it was.

## Verified vs not

- **Verified:** it builds with 0 errors, and the server starts with 0 failed patches.
- **Not verified in game:**
  - Only our four groups show.
  - The map list fills, and Change Map Now opens the chosen map and cultures.
  - Apply Changes with only the number of rounds changed leaves map voting on.
  - The number fields show valid bounds.

## Traps

- The stock map option keeps the usable maps only after `FilterAvailableOptions` and the Game Type selection. Why it showed nothing for CommanderBattle was not traced. The client game mode reports itself as Captain (`MissionMultiplayerCommanderBattleClient.GameType`), which may be involved. Our own list avoids the question.
- `AdminUpdateMultiplayerOptions` reads int values with each option's `BoundsMin` and `BoundsMax`. A value outside them would break the packet. The number fields take their bounds from the same property.
