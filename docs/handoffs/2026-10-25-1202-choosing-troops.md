# Handoff: choosing troops (the class sets the formation's size)

Session of 2026-10-25, written 12:02. Plan: `docs/plans/choosing-troops.md` (options 1–3, status in progress). `README.md` describes the new rule in full.

## State

- **Nothing is committed.** The working tree on `master` sits on top of `755390d`. The earlier handoff's work was committed before this session (`a61eee9`, `2a702b1`).
- **Installed, not running.** A full `dotnet build "CommanderBattle.sln" -c Release` (0 errors) installed matching client and server DLLs at 11:59. No game or server was running at handoff, and none was started.

## What was done

**Rule.** Each formation keeps a budget of 62–63 men (500 / 8). men = budget × stock count of its class / average stock count of the culture's classes of that role, rounded half up, clamped to 1..1023.
- The stock count is `MPPerkObject.GetTroopCount(class, 100, perks)`. A player commander's perks count; AI-led formations have none.
- A side's total varies. For Vlandia it is 517 with the default classes, 410 all heavy and 618 all light.

**Files**
- `CommanderBattle/CommanderBattleSettings.cs`: `TroopsPerSide` → `TroopBudgetPerSide`, `GetTroopCount` → `GetTroopBudget`, new `MaxTroopsPerFormation` (the network cap).
- `CommanderBattle/FormationSlots.cs`: `FormationSlot.Budget` is fixed, and `TroopCount` is now set per round. `FormationSlotTable` gains `GetTroopCounts`, `SetTroopCounts` and `GetTotalTroopCount`.
- `CommanderBattle/CommanderRole.cs`: `GetAverageStockTroopCount` (the role reference).
- `CommanderBattle/MissionMultiplayerCommanderBattle.cs`:
  - `ComputeTroopCounts` applies the rule.
  - `NewSpawnLayout` computes the counts when preparation starts.
  - `UpdateTroopCounts` re-plans and rebroadcasts during preparation when a count changes.
  - `FreezeTroopCounts` runs on the switch to InProgress (inside `MultiplayerRoundController.StartSpawning`, before the spawner runs). It writes the "Troop counts for round N" log.
  - Late joiners get the counts in `HandleNewClientAfterSynchronized`.
- `CommanderBattle/Network/SpawnLayoutSeed.cs`: now carries the 16 counts (10 bits each).
- `CommanderBattle/CommanderBattleSpawnLayout.cs`: `SetRound(mission, seed, counts)`. Block widths and the distance between the armies use the real counts.
- `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs`:
  - It receives the counts.
  - The strength bar measures against the side's count total, which fixes late joiners who used to see 100%.
- `CommanderBattle.View/FormationSelectionVM.cs`: the side total is in the title, and the hint explains the rule.
- Docs: `README.md`, `docs/plans/choosing-troops.md` (the decisions), `docs/plans/admin-commands.md` (the rename).

## Verified vs not

| Change | Status |
|---|---|
| Builds for net472 and net6.0, installed to both | Verified |
| Counts freeze before spawning | Checked in the decompiled `MultiplayerRoundController`; not seen in a log yet |
| Log counts match spawned formations | Not run |
| Formation list updates for all players during preparation | Not seen in game |
| Preparation visuals and spawn layout agree between client and server | Not seen in game |
| Perk extras change the count | Not seen in game |

## Open items

1. **In-game test** (README checklist item "Picking another class..."):
   - start the server with `tools\Start-Server.ps1 -Restart`;
   - pick light vs heavy classes during preparation and watch the formation list;
   - compare the log's "Troop counts" lines with the spawned formations.
2. **Known edges (minor):**
   - After a mid-round class change, the list shows the new class next to this round's frozen count until the next round.
   - A commander who leaves the slot after the freeze but before spawning leaves AI troops at a count that includes their perks.
   - A commander's preparation visuals may lag one game-mode tick behind a count change.
3. **Agent cap.** An all-light side is about 620 soldiers. Two of those plus horses stay under ~2047, but check tick rate with large armies.
4. **Option 4 (role split)** is deferred by the user.

## Traps

- None new. The compile-only trap from the 10:13 handoff (item 8) still applies.
