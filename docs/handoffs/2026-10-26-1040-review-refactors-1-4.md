# Handoff: review refactors 1–4

Session of 2026-10-26, written 10:40. It follows `2026-10-26-0840-review-defect-fixes.md` (same session). Another session committed that handoff's defect fixes, together with its admin panel, as `52c35f2`.

## State

- **Not committed:** 14 files under `CommanderBattle/` and `CommanderBattle.View/` (`git diff --stat`: 126+, 158-), the new `CommanderBattle/Network/CommanderBattleNetwork.cs`, and this handoff.
- **Installed:** both sides, built at 10:39 with 0 warnings and 0 errors. No game was running, so the client DLLs copied.
- **Running:** server pid 38088, map `battle_terrain_biome_067`. Patch line: "94 classes applied, 0 failed, 3 skipped". Both mod logs are clean.

## What was done

No behaviour change was intended.

1. **Network send helpers.**
   - `CommanderBattleNetwork.Broadcast`, `SendTo(peer, …)` and `SendToServer`, in the shared dll and outside `#if SERVER`.
   - They are used in the game mode and its Admin partial, the spawner, `CommanderBattleAIStatusSync` and the admin panel's options message.
   - The client's four request methods go through one `Request(message, runOnServer)`. A listen server still calls the game mode directly.
2. **`CommanderBattleTeams.Sides`** (defenders first, as before) replaces the `new[] { Defender, Attacker }` loops.
   - **`FormationSlotTable.AllSlots`** is used where a loop only needed the slots: `ApplyPendingSelections`, `ReleaseDisconnectedCommanders`, `HandleNewClientAfterSynchronized`, `FindAnyCommander`.
   - The client's marker loop went from Attacker-first to `Sides`. Order doesn't matter there: it only sets frames.
3. **`CommanderRoles.GetClasses(culture)`** is cached per culture, in `GetMPHeroClasses` order.
   - It replaces every `GetMPHeroClasses(...).ToList()` in `CommanderRole.cs` and in `TroopSelectionVM`.
   - `ClearClassCache()` runs at each mission start, in the client half's `OnBehaviorInitialize`, which also runs on the server.
4. **`CommanderBattleTeams.MySide`** (static) and **`MissionMultiplayerCommanderBattleClient.MySlot`** replace the copied lookups in the client and in four views: troop selection, formation list, side banner, AI status.

## Verified vs not

- **Verified:** it builds, the server starts with 0 failed patches, and the logs are clean.
- **Not verified in play:** nothing has been played. Worth checking:
  - joining a side places you in a formation;
  - the troop screen and the formation list request work;
  - a pick applies;
  - the admin panel commands go through;
  - a player joining mid-match sees the slots.

## Left on purpose

- **The admin panel's commander list** (`CommanderBattleAdminPanel.cs:197`) keeps Attacker-first, because that is the order the list shows.
- **The admin panel's culture filter** keeps `GetMPHeroClasses(c).Any()`. It only runs when the panel is open.
- **`FindSlotOf`** still walks the arrays. It runs per peer every tick, and `AllSlots` would allocate an enumerator each call.
- **RBM.Multiplayer's two broadcast triples** are untouched. That project can't reference CommanderBattle.dll.

## Open items

The rest of the review list, in order:
1. A layer-view base class for the 4 CommanderBattle views and the 2 RBM views.
2. Merge `TroopRoleCardVM` and `TroopTypeVM`.
3. Make the constant VM texts getter-only.
4. Helpers for the spawning `AgentBuildData` and the team colours.
5. A shared log source file.
6. Text helpers for the slot display name, the pick description and the side name.
7. Clamp the network messages to their compression limits.
8. Remove the dead stubs and the namespace placeholders.
9. One `CommanderLeadDistance` constant.
10. A shared csproj targets file.

`FindAnyCommander` is no longer dead: the admin panel uses it. Also, `CLAUDE.md` still says armies of 500.
