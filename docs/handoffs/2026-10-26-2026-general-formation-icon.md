# Handoff: an icon for a player general's formation

Session of 2026-10-26, written 20:26. It follows `2026-10-26-1832-spectator-player-names.md` (same session).
- Not committed.
- The client is installed: the `CommanderBattle.View` build, 0 errors; the DLL and the prefab in the module are fresh.
- Client-only, so the server doesn't need restarting.

## What was done

At the user's request, a player general's formation (him and his bodyguard) now gets a formation icon and label.

**Why it had none.** `MissionFormationMarkerVM.RefreshFormationMarkers` lists `Team.FormationsIncludingEmpty`, which holds only the 8 regular formations. The general's (index 8) is only in `FormationsIncludingSpecialAndEmpty`. `MissionFormationTargetSelectionHandler` skips it the same way when it checks out-of-sight, so it returns `NotEvaluated` for him.

**`CommanderBattleAllyPlayerMarkersView.cs`:**
- `UpdateGeneralIcons` keeps one stock `MissionFormationMarkerTargetVM` per side whose general slot has a player and whose formation has units.
  - **Icon:** `CommanderRoles.GetIcon(culture, General)`, as on the role card.
  - **Fade:** the stock MP constants (0.35 / 350 / 5 / 1 / 25). The server's `FormationMarker*` options would override them for the stock icons only; our config sets none.
- `UpdateIconPosition` copies `MissionGauntletFormationMarker.UpdateMarkerPositions` and `GetVisibilityState`.
  - It updates every tick and shows on game key 5 or with the order bar, not in deployment.
  - It shows the distance text when the option is on.
- `GetMarkerVisibility` hands regular formations to the handler.
  - For the general's formation it runs the handler's private `IsFormationVisibleEnough` through reflection, with the same rules: non-allied teams only, off when the mode is Disabled or targeting is off, Hidden beyond 1000 m.
  - The label uses it too.
  - If a game update renames those members, it falls back to `NotEvaluated`, which means always shown.
- `RefreshLabelTexts` no longer skips the general, and it clears the general icons so they are rebuilt with the player's new team colours.
- `UpdatePlayerMarkers` skips a general: he rides inside his small bodyguard, under the icon, whose label already has his name. **This is my call; the user didn't ask for it.**

**Other files:**
- `AllyPlayerMarkersVM.cs`: `GeneralIcons` (`MBBindingList<MissionFormationMarkerTargetVM>`).
- `CommanderBattleAllyPlayerMarkers.xml`: Native's `FormationMarker.xml` item template bound to `{GeneralIcons}`, without the focus indicator.
- `README.md`: the Alt paragraph and the checklist item.

## Verified vs not

- **Verified:** the build and the client install.
- **Not played.** Check:
  - a player general's icon shows in his team colour with the general's role icon, labelled, and fades like the others;
  - an enemy player general's icon hides out of sight. This only applies if the server's `FormationTargetingVisibilityMode` isn't Disabled. Our config doesn't set it, so it is probably the default.

## Open items

- If the user prefers no name marker removed for the general, or a different icon (the default for a culture whose general's class has no `IconType` is `Special_RoyalCavalry`), both are one-line changes.
