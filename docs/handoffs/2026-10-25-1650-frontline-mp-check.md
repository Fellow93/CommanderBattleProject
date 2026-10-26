# Handoff: Frontline in multiplayer, worker-thread broadcast fix and log counters

Session of 2026-10-25, written 16:50. It runs alongside `2026-10-25-1638-posture-animation-sync-fix.md`, which owns the running server, and `2026-10-25-1640-scoreboard-formation-column.md`.

## State

- **Not committed:** `RBM.Multiplayer/AI/TargetFrameClearSync.cs` (new), `RBM.Multiplayer/RbmMpServerLogic.cs`, `README.md`, and this handoff.
- **Built:** a Release build of the solution compiles with no CS errors.
- **Client:** the client DLLs are installed.
- **Not installed on the dedicated server.** Pid 26700, from the 16:38 session, locks its DLLs, so the copy failed (MSB3027). A restart with `-Build` installs the fix. The fix is server-only and adds no network message type, so the old server works with the new client.

## Audit: is Frontline working in MP?

Read-only review of RBM's `AiModule/Frontline/*` against the MP setup, done with a subagent and spot-checked.

- **Nothing switches it off on the server:**
  - the field battle flag, `IsDeploymentFinished` and `frontlineEnabled` are all true;
  - the patch target `Formation.GetOrderPositionOfUnit` runs from `ParallelUpdateFormationMovement` on the server;
  - it doesn't use `Agent.Main` or campaign types;
  - `MissionStartReset` clears its state each round.
- **The bug (confirmed in the server's `Agent.ClearTargetFrame`):**
  - On a server, `ClearTargetFrame` broadcasts `ClearAgentTargetFrame` whenever the agent is held in place (`MovementLockedState != None`).
  - Frontline calls it from the movement workers, at `FrontlinePositioning.cs:150-153` and `TickPatches.cs:229`.
  - So several threads wrote to the single native broadcast at the same time.
- **Fix** (`TargetFrameClearSync`): a `com.rbm.multiplayer` prefix on `Agent.ClearTargetFrame`.
  - Off the main thread on a server, it does the native clear at once, as singleplayer does, and queues the agent.
  - `Flush`, called from `RbmMpServerLogic.OnMissionTick`, sends one message per still-active agent on the main thread.
  - `Reset` runs in `OnClearScene`.
  - The main thread id is taken in the patch's `Prepare`, which the patcher runs on the main thread. If the private members are missing, `Prepare` returns false and logs a line; it doesn't fail half-way.
- **Counters** in the 30 s formation log:
  - for each infantry or ranged formation: "Frontline N deciding (attack, back step, find ally, flank L/R, rest), N closing up, N held", or "too small for Frontline";
  - "Frontline this round": obstacle probes, steps rejected as unreachable, and hold releases sent from the main thread.
  - "Deciding" means a live `AIDecisionTimer`. That timer is not cleared when a charge ends, so after a change of order it lags behind; "held" is the live signal.

## Verified

- It compiles for both frameworks. Nothing has been checked in a running server.

## Not verified

- That the patch applies on the server: look for the patch count going up by one with 0 failed, and no "Agent.ClearTargetFrame not patched" line.
- The counters in a real charge, and no network errors or disconnects in melee (`rgl_log`).

## Open items

- Restart the server with `-Build` once the 16:38 session is done with it, play a round with an infantry charge, and read the formation log.
- **Probable, not fixed: a hold can survive a takeover.** On the server, `ReplaceBotWithPlayer` sets `MissionPeer`, but `AgentPanicFix.OnAgentControllerSetToPlayer` doesn't fire. Frontline's prefix then returns at `IsPlayerControlled` before the pending clear. A soldier held at the moment of takeover may stay movement-locked; test by taking over a soldier mid-charge. A fix would be to clear the agent's target frame in `RbmMpServerLogic.RebuildTakenOverAgents`.
- The comment in `ArcherCloseCombatLogic.cs:15` names Frontline for the hold-your-slot-on-a-Move behaviour. That is `OverrideParallelFormationMovement`, which runs whatever `frontlineEnabled` is set to.

## Traps

- Dedicated server DLLs are locked while any server runs, even one another session started. Check `docs/handoffs` before `-Restart`.
