# Handoff: spawn path picked from the round seed; first Frontline round in the logs

Session of 2026-10-25, written 22:08. Follows `2026-10-25-1650-frontline-mp-check.md` (same session).

## State

- **Not committed:**
  - from 16:50: `RBM.Multiplayer/AI/TargetFrameClearSync.cs` (new), `RBM.Multiplayer/RbmMpServerLogic.cs`, the 16:50 handoff;
  - from this pass: `CommanderBattle/CommanderBattleSpawnLayout.cs`;
  - both passes: `README.md`, `docs/handoffs/README.md`.
- **Installed:** both changes are on the game client and the dedicated server. Release builds; the installed DLLs match `obj\Release\<tfm>\`.
- **Running:** nothing. The game closed at 17:00. Server pid 32440, restarted with `-Build` at 16:51 at the user's request, shut down cleanly at 22:04.

## Logs of the user's round (pid 32440, 16:55–17:00)

- **Frontline works.** Charging infantry showed, for example, "Frontline 79 deciding (attack 39, back step 22, find ally 5, flank 3/10), 35 closing up, 91 held". Formations of 25 men or fewer showed "too small for Frontline".
- **Hold releases sent:** 91,701 in about 90 s of melee, roughly 900 reliable messages a second. Clients never get Frontline's holds (plain `SetTargetPosition` isn't synced), so these messages are wasted. Not fixed yet; see open items.
- **Server FPS in melee:** 57 dips below 60, averaging 53, with only one below 30 (17 FPS). The bad stalls were at round-start spawning (0.9 FPS, 1,079 agents) and when the client joined (3.6 FPS).
- **The red icon** the user sees on the left is one of the stock MP status icons (`MultiplayerMissionServerStatusVM`). Server performance turns red when the server's average FPS is below 30 (orange below 50), ping at 110 ms or more, packet loss at 2% or more. On a local server it's the server performance one, set off by those stalls.

## Spawn layout mismatch: fixed

- **Seen:** same seed 866963449, but the server planned attackers at (437, 768) and defenders at (811, 536), and the client the reverse, (794, 778) and (409, 644). Path offsets differed too (-243 m vs -174 m).
- **Cause:** `BattleSpawnPathSelector.FindBestInitialPath`. On a scene without a map patch (every MP mission), it picks `allSpawnPaths.GetRandomElement()` and a pivot at `0.37 + MBRandom.RandomFloat * 0.26` of the path length, separately on each machine in `Mission.AfterStart`.
- **Fix:** in `CommanderBattleSpawnLayout.SetRound`, on both server and client, whenever the seed or mission changes, `SelectSpawnPath`:
  - picks the path and pivot from the seed (`LayoutRandom(seed, 2)`; seed 0 means `spawn_path_00` at 50%);
  - writes them into the mission's selector by reflection (`Mission._battleSpawnPathSelector`, its `_initialPath` and `_battleSideSelectors`), building both sides as `Initialize` does;
  - sets `MissionTeamAIType` to `NoTeamAI` for a moment while building them. The sides' ground snapping reads `IsFieldBattle`, which only the server sets.
- It does nothing on a scene with a map patch, where the stock pick is already deterministic.
- It logs "Spawn path spawn_path_NN (N on the scene), pivot X m of Y m (seed S)". If the reflection fails, it logs and the stock path stays.
- The private field names were checked in the client decompile and in the server's net6 `TaleWorlds.MountAndBlade.dll`.

## Verified

- Both frameworks compile, and the build is installed on both sides.

## Not verified

- Not run yet. Check that the server's and a client's `CommanderBattle_<pid>.log` show the same "Spawn path" line and the same front lines.
- The client's first-chance NRE at 16:54:47 in `VisualSpawnFrameSelectionHelper.GetSpawnPointFrameForPlayer`, raised when the user picked a formation, may have come from the mismatch. Watch whether it recurs.

## Open items

- **Cut the hold-release flood:** send `ClearAgentTargetFrame` only for agents whose hold clients actually got (`SetTargetPositionSynched` / `SetTargetPositionAndDirectionSynched`). Track those agents and broadcast in `TargetFrameClearSync.Flush` only for them. The user was offered this and chose the spawn mismatch first.
- **Desync audit:** the subagent stalled after 600 s with no result. Rerun it in narrower pieces: player-agent and mount stat guards; combat and stance actions on player agents; server-only item changes.
- From 16:50: the takeover hold-release question, and the `ArcherCloseCombatLogic.cs:15` comment.
- Stalls at round-start spawning and at client join: spread the AI spawn over several ticks if they bother players.

## Traps

- The game's own stock MP code can be nondeterministic between server and client (`MBRandom` in mission start-up code). Anything the layout reads from the mission has to be checked for that, not just our own inputs.
