# Handoff: code review and its five defect fixes

Session of 2026-10-26, written 08:40. The session started from a clean tree at `d20065f`, so the uncommitted work listed in the 00:20 handoff had been committed by then.

## State

- **Not committed:**
  - `CommanderBattle/CommanderBattleLog.cs`, `CommanderBattle/CommanderBattleSpawnLayout.cs`, `CommanderBattle/MissionMultiplayerCommanderBattle.cs`
  - `RBM.Multiplayer/RbmMpServerLogic.cs`, `RBM.Multiplayer/AI/TargetFrameClearSync.cs`, `RBM.Multiplayer/Stance/TiredReactionPatch.cs`
  - `tools/Start-Server.ps1`, `tools/Install-CampaignScenes.ps1`
  - this handoff
- **Installed:** both sides, built at 08:40. No game was running, so the client DLLs copied too.
- **Running:** server pid 8120, started with `tools\Start-Server.ps1 -Build`, map `battle_terrain_biome_093b`. Patch line: "94 classes applied, 0 failed, 3 skipped". Both mod logs are clean.

## What was done

There was a read-only review of all four projects, by three reviewers, one per area. It produced a list of refactors, and the user asked for the defects to be fixed first. Those five fixes:

1. **Diagnostics ran with logging off.** In `RbmMpServerLogic.OnMissionTick`, `LogTactics` and `LogFormations` now run only while `RbmMpLog.Detailed` is on. `LogFormations` scans every agent for each standing soldier, every 30 s.
2. **First-chance exception hook** (`CommanderBattleLog`):
   - It filters on the stack frames' types (namespace `CommanderBattle*` / `RBM*`), not on the `Environment.StackTrace` text. The text is built only on a match, and .NET Framework translates the "at" the old filter looked for.
   - Once the 300 budget is used up, it returns before doing anything, so `SeenExceptions` stops growing.
3. **The layout seed outlived a map change** (`CommanderBattleSpawnLayout`):
   - `RoundSeed` became `GetRoundSeed(Mission)`.
   - `ForgetOtherMission` clears the seed, the troop counts and the layouts when the mission changes. `GetLayout` uses it too.
   - The three callers in `MissionMultiplayerCommanderBattle` pass `Mission`. Before this, a player joining a new map before its first round got the last map's seed.
4. **Missing resets** (`RbmMpServerLogic.ResetRbmState`): it now also calls `TargetFrameClearSync.Reset()` and the new `TiredReactionPatch.Reset()`. `ResetRbmState` already runs at mission start and every round, so `TargetFrameClearSync.Reset()` now also runs at mission start; the call in `OnClearScene` was removed. The 256-entry cap and the `now >= last` check in `TiredReactionPatch.Grunt` were only workarounds for the missing reset and were removed.
5. **Scripts:**
   - In `Install-CampaignScenes.ps1`, the `-ServerDir` default now follows `-GameDir`.
   - In `Start-Server.ps1`, a server that is still alive 30 s after the stop now fails the script, instead of the script building against locked DLLs. The log folder comes from `$env:ProgramData`.

## Verified vs not

- **Verified:**
  - It builds, with 0 errors; the one warning comes from RBM source.
  - The server starts with 0 failed patches and the mod logs are clean.
  - The new `-ServerDir` default was checked in a scratch scriptblock.
- **Not verified in play:**
  - With `CommanderBattleLog.Detailed` off, an exception thrown in our code still reaches the log, with its stack. The frame filter is new. An exception the hook can't see would be a regression.
  - With `RbmMpLog.Detailed = true` (rebuild), the Tactics, Formations and Frontline lines still appear.
  - After a map change, a client joining before the first round gets seed 0. After that, the markers and spawns match the server.
  - The exhaustion grunt still plays once per exhaustion, across rounds.

## Open items

- The review's refactor list hasn't been started. It is in the chat, not in a file. Ranked by value:
  - network send helpers (`Broadcast`, `SendTo`, `SendToServer`)
  - `CommanderBattleTeams.Sides` and `FormationSlotTable.AllSlots`
  - a cached `CommanderRoles.GetClasses(culture)`
  - `MySide` and `MySlot` on the client
  - a layer-view base class for the 4 CommanderBattle views and the 2 RBM views
  - merging `TroopRoleCardVM` and `TroopTypeVM`
  - a shared log source file
  - constant VM texts as getter-only properties
  - dead stubs and namespace placeholders
  - one `CommanderLeadDistance` constant (5 m, in two places that must agree)
  - a shared csproj targets file
- `CLAUDE.md` says armies of 500, but the budgets give about 400 a side. Not changed yet.

## Traps

- The seed fix doesn't remove every difference before the first round. On the new map, the server keeps the stock random spawn path until its first `SetRound`. A client given seed 0 picks `paths[0]`. Nothing is spawned from either before the first preparation, which broadcasts the real seed. So only the markers can differ, and only before that first broadcast.
