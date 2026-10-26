# Taking over a front-rank soldier: release Frontline's hold

Follows [2026-10-26-1851-cheer-server-installed.md](2026-10-26-1851-cheer-server-installed.md). It fixes the "probable, not fixed" item of [2026-10-25-1650-frontline-mp-check.md](2026-10-25-1650-frontline-mp-check.md).

## State

- **Not committed**, with everything earlier handoffs list as uncommitted.
- **Compiles** (Release, both frameworks). The client DLLs are installed; they don't matter here, because the fix runs on the server only.
- **Not installed on the dedicated server.** Server pid 30072 (started by the cheer session) holds the DLLs (MSB3027). A restart with `tools\Start-Server.ps1 -Build -Restart` installs it.

## What was done

- The user reported: a dead player presses F on a soldier in the front line and then can't move.
- Cause: RBM's Frontline pins its front rank with the native `SetTargetPosition` (server only, `MovementLockedState` set). In singleplayer the release comes from `AgentPanicFix.OnAgentControllerSetToPlayer`. On a server, `Mission.ReplaceBotWithPlayer` only sets `MissionPeer`; no controller change fires. Frontline's `GetOrderPositionOfUnit` prefix then returns at `IsPlayerControlled` before its pending clear, so the hold stays for good. The client moves, and the server keeps the agent in place.
- Fix: `RbmMpServerLogic.RebuildTakenOverAgents` (already run once per newly player-controlled agent, on the main thread) now calls `agent.ClearTargetFrame()` before the stat rebuild. Through `TargetFrameClearSync`, a native-only hold is released without a message; a hold the client got through a `...Synched` setter is released with one.
- A freshly spawned player agent goes through it too; with no hold, the call does nothing.
- `README.md`: the RbmMpServerLogic row and a checklist line.

## Verified vs not

- Compiles only. Not run on a server, not played.

## Open items

- Restart the server with `-Build`, take over a front-rank soldier mid-melee, and walk away.
- If he is still stuck, the other suspects are a stock scripted hold (`SetScriptedPosition` from a formation behaviour) or a game object in use; `ClearTargetFrame` covers both target-frame holds, but not `StopUsingGameObject`.

## Traps

- None new.
