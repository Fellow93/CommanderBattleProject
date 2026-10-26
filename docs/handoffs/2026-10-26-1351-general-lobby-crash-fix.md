# Handoff: client crash at the multiplayer lobby (the general's class group)

Session of 2026-10-26, written 13:51. It follows `2026-10-26-1340-general-role-phase-0.md` (same session) and corrects its class-group design.

## State

- **Not committed:** all of the general role work (see the 13:40 handoff) plus this fix.
- **Installed:** both sides. No installed `cb_mpcharacters_*.xml`, game or server, still has `default_group="General"`.
- **Running:** server pid 5436, map `battle_terrain_biome_107`. Patch line: "94 classes applied, 0 failed, 3 skipped". Round 0 opened, and there is no "fields no general" line, so the general class is still found by its id.

## What happened

The client (pid 29640) crashed as `MultiplayerLobbyGauntletScreen` opened, before joining any server. The rgl log had no managed stack, and the dump was cancelled.

**Cause.** I decompiled `TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection` into the scratchpad. `MPLobbyClassFilterFactionItemVM` (around line 149–157) builds `_classGroupDictionary` from the 4 stock `MultiplayerHeroClassGroups`, then does `_classGroupDictionary[heroClass.ClassGroup.StringId]` for every class. The general's group "General" isn't a key, so the lookup throws `KeyNotFoundException` while the lobby builds its class filter.

## What was done

- **XML:** in all six `Module/ModuleData/cb_mpcharacters_<culture>.xml`, the general's hero and bodyguard troop now have `default_group="Cavalry"`.
- **`CommanderBattle/CommanderRole.cs`:** `GetRole(MPHeroClass)` recognizes the general by its class id, using `IsGeneralClass` and `GeneralClassPrefix = "cb_general_"`, before switching on the group. Cavalry role lists, weights and averages therefore never include the general.
- **`CommanderBattle/MissionMultiplayerCommanderBattleClient.cs`:** `WidenClassRestrictions` (the reflection on `MissionLobbyComponent._classRestrictions`) is removed. With no group past the stock 4, nothing overflows that array.
- **`CommanderBattle/MissionMultiplayerCommanderBattle.cs` `UpdateClassRestrictions`:** a general's picker keeps the Cavalry group open.
  - `SpawnComponent.AllowEarlyAgentVisualsDespawning` returns false when the commander's class group is restricted (`MissionMultiplayerGameModeBase.IsClassAvailable`).
  - Before this fix, all 4 groups would have been restricted for a general.
- **Docs:** the README "The general" section ("Stock class groups"), and the class line in `docs/plans/general-role.md`.

## Verified vs not

- **Verified:** the build (0 warnings, 0 errors); the installed XML; the server start and round open with 0 failed patches; the server still finds a general for both sides.
- **Not verified:** that the client now reaches the lobby. Everything in the 13:40 handoff's "not played" list is also still open.

## Open items

- The user restarts the game, reaches the lobby, joins, and plays the general's checklist (README).
- Commit after that.

## Traps

- **Stock MP UI only knows the 4 class groups** (Infantry, Ranged, Cavalry, HorseArcher). A class whose hero has another `default_group` crashes the client at the multiplayer lobby, before any mission, without a managed stack in the rgl log. Tell special classes apart by id, not by class group.
- **The multiplayer UI assemblies** (`Modules\Multiplayer\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.Multiplayer*.dll`) aren't in the decompiled folder. `ilspycmd -p -o <scratchpad> -r <game bin> -r <that folder> <dll>` decompiles them in a few seconds.
