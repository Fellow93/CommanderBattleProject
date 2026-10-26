# Handoff: spawn path sync verified; preparation visuals fix

Session of 2026-10-25, written 22:36. Follows `2026-10-25-2208-spawn-path-sync.md` (same session). Another session (`2026-10-25-2230-slower-ranged-fire.md`, `...-2233-...-installed.md`) works in this checkout at the same time. It owns the server it restarted at 22:33 (pid 34388), and its uncommitted changes are in `CommanderBattleAgentStatCalculateModel.cs`, `CommanderBattleSettings.cs` and `CommanderBattleSubModule.cs`.

## State

- **Not committed:**
  - from this session: `RBM.Multiplayer/AI/TargetFrameClearSync.cs` (new), `RBM.Multiplayer/RbmMpServerLogic.cs`, `CommanderBattle/CommanderBattleSpawnLayout.cs`, `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs`;
  - shared docs: `README.md`, `docs/handoffs/README.md`, and the 16:50, 22:08 and this handoff.
- **Installed:**
  - client: `CommanderBattle.dll` from this build, which also carries the other session's working-tree changes, since the checkout is shared;
  - server: the copy failed (MSB3027, locked by pid 34388). This change is client-only (`#if !SERVER`), so the server doesn't need it.
- **Running:** server pid 34388 (the other session's).

## Spawn path sync: verified

Server pid 35176 (`CommanderBattle_35176.log`) and client pid 27456 (`CommanderBattle_27456.log`) logged the same thing in both rounds:

| Round | Seed | Spawn path | Attacker front line | Defender front line |
|---|---|---|---|---|
| 1 | 281446017 | spawn_path_03, pivot 213 m of 523 m | (1087.7, 1025.8) | (735.1, 718.1) |
| 2 | 676189984 | spawn_path_03, pivot 313 m of 523 m | (1087.7, 1025.8) | (735.1, 718.1) |

Each side's shift and turn matched too. On this 523 m path the armies are about 500 m apart, so they sit at the path ends and the pivot doesn't move them; the round-to-round variety comes from the shifts and turns.

## Preparation visuals: fixed, not verified

- **Symptom:** every stock `CreateAgentVisuals` on the client threw in `MultiplayerMissionAgentVisualSpawnComponent.VisualSpawnFrameSelectionHelper.GetSpawnPointFrameForPlayer`: 335 of 335 in client 27456's `rgl_log`, 138 of 138 in 27912's ("Exception in handler of CreateAgentVisuals"). No preparation visual ever showed on a campaign scene. It was not caused by the spawn mismatch.
- **Cause:** the helper looks up `sp_visual_N`, `sp_visual_attacker_N` and `sp_visual_defender_N` once, by tag, in its constructor on its first `OnPreMissionTick`. The markers `CreateVisualMarkersIfMissing` creates at runtime (`GameEntity.CreateEmpty` + `AddTag`) weren't found, so the helper's arrays stayed null and the frame lookup hit them. The engine does the lookup natively, so why it misses runtime entities is unknown.
- **Fix:** `GiveMarkersToVisualSpawner`, called from the client's `OnMissionTick`, which runs after the tick that creates the helper. It puts the markers into the helper's three arrays by reflection (`_spawnFrameSelectionHelper`, `_visualSpawnPoints` / `_visualAttackerSpawnPoints` / `_visualDefenderSpawnPoints`), once per helper.
- It logs "Preparation visual markers given to the stock visual spawner (it had found N of 18 by tag)". N says whether the tag lookup ever works.
- It's `#if !SERVER`: the dedicated server's `TaleWorlds.MountAndBlade.dll` has no `MultiplayerMissionAgentVisualSpawnComponent`, and that build failed with CS0246 before the guard.

## Verify next

- Join, pick a formation and class during preparation, and check that your commander and troop previews stand at your formation's place.
- The client's `CommanderBattle_<pid>.log` should have the "given to the stock visual spawner" line and no `GetSpawnPointFrameForPlayer` exception.
- The client's `rgl_log` should have no "Exception in handler of CreateAgentVisuals".

## Open items (unchanged from 22:08)

- Hold-release flood: about 900 `ClearAgentTargetFrame` messages a second in melee. Send it only for holds clients actually got.
- Rerun the desync audit in smaller pieces.
- Takeover hold release; the `ArcherCloseCombatLogic.cs:15` comment; stalls at spawn and at join.
- The client's `ArgumentNullException` in `MPIntermissionVM.OnIntermissionStateUpdated`, raised on the between-rounds screen before the mission opens, is stock and caught. Probably harmless.
