# Handoff: whose formation it is, by the stock icons

Session of 2026-10-26, written 18:24. It follows `2026-10-26-1821-ally-player-markers.md` (same session).

## State

- **Not committed.** This adds to the 18:21 ally markers, on top of the uncommitted general work.
- **Installed:** client only (`CommanderBattle.View.dll`, the prefab). Nothing goes to the server.
- **Running:** server pid 21560 (another session's), left alone. The note from 18:21 about that session's `MissionMultiplayerCommanderBattleClient.cs` edit still holds.

## What was done

The user asked for a label on the formation icons in Alt mode: "Fellow's heavy infantry".
- `CommanderBattle.View/CommanderBattleAllyPlayerMarkersView.cs`:
  - for each formation 0-7 of the player's side that a player leads (the player's own too), a label right of the stock icon;
  - projected from `CachedMedianPosition` + 3 m, the point the stock `MissionGauntletFormationMarker` uses;
  - shown when the icons are: Left Alt, or the order bar open;
  - texts rebuilt on `Slots.OnSlotChanged` and on the player's team change;
  - the weight comes from `CommanderRoles.GetWeightLabel` and the role from `CommanderRoles.GetName`, both lower-cased; there's no weight for a role with a single class;
  - no label for AI formations, the enemy or the general (formation 8 has no stock icon).
- `CommanderBattle.View/AllyPlayerMarkersVM.cs`: the `FormationLabels` list.
- `Module/GUI/Prefabs/CommanderBattleAllyPlayerMarkers.xml`: a second list, with the label 30 px right of the point.
- `README.md`: the Alt paragraph and the checklist item.
- **Changed from 18:21:** the player markers show the name only, at the user's request.

## Verified vs not

- **Verified:** the build (0 errors) and the client install.
- **Not played:**
  - check that the label clears the stock icon (`MarginLeft="30"` is a guess at the icon's width);
  - check the text on each culture.

## Open items

- Play it, and adjust the label offset if it overlaps the icon.
- Commit when the user says so.
