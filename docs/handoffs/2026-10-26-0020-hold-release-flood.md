# Handoff: ClearAgentTargetFrame flood cut

Session of 2026-10-25/26, written 00:20. Follows `2026-10-26-0010-short-logs.md` (same session). It closes the "hold-release flood" open item from the 22:08 and 22:36 handoffs.

## State

- **Not committed:** everything from the 23:53 and 00:10 handoffs, plus `RBM.Multiplayer/AI/TargetFrameClearSync.cs`, `RBM.Multiplayer/RbmMpPatcher.cs`, `RBM.Multiplayer/RbmMpServerLogic.cs` and `README.md`.
- **Installed:** both sides, built at about 00:16.
- **Running:** server pid 11340, started with `tools\Start-Server.ps1 -Restart`, map `battle_terrain_biome_027`. Its patch line reads "94 classes applied, 0 failed, 3 skipped": 92 before, plus the two new patch classes.
- The zips in `artifacts\` are stale.

## The flood

- The client's rgl log from the 23:07 session reached 417 MB, about 2.1 million `ClearAgentTargetFrame` lines in 35 minutes.
- Stock `Agent.ClearTargetFrame` on a server broadcasts that message whenever the agent is movement-locked.
- RBM's Frontline locks soldiers with the native `SetTargetPosition`, which sends nothing to clients, and releases them constantly. So nearly every message was for a hold the client never had: on a client, `ClearTargetFrame` on an unlocked agent does nothing.
- Holds reach clients only through `Agent.SetTargetPositionSynched` and `SetTargetPositionAndDirectionSynched`, the only senders of `SetAgentTargetPosition(AndDirection)`. In TaleWorlds.MountAndBlade their one caller is `AgentVictoryLogic`; other assemblies may call them too.

## What was done

- `TargetFrameClearSync`:
  - Prefixes on the two `...Synched` setters mark the agent in `HeldOnClients`, a `ConcurrentDictionary`, on the server.
  - The `ClearTargetFrame` prefix replaces the stock method on the server, on every thread. It does the native release as before, broadcasts only if the agent was marked (and unmarks it), and queues the message for `Flush` when off the main thread.
  - `Reset` clears the marks each round.
  - New counter `SkippedThisRound`.
- The detail log's "Frontline this round" line now reads "... N hold releases sent to clients, M not sent (clients never had the hold)".
- `RbmMpPatcher`: patches on `ClearTargetFrame` and both `...Synched` setters now go first, with the `SetActionChannel` ones. That way the JIT can't inline the stock method into a replacement compiled earlier, as CLAUDE.md warns.
- `README.md`: the Frontline paragraph was rewritten.

## Verified vs not

- Builds. On the server, the patches apply with 0 failed.
- **Not verified in play:**
  - The client's rgl log should no longer fill with `ClearAgentTargetFrame` lines during a melee. Count them with `grep -c` after a round.
  - Soldiers should behave the same on clients: no frozen or rubber-banding AI.
  - With `RbmMpLog.Detailed = true`, the Frontline line shows sent ≈ 0 and not sent in the hundreds of thousands.
- Victory cheering at the round end uses `SetTargetPositionAndDirectionSynched`. Its release still reaches clients.

## Traps

- Marking happens even when a setter returns early because the agent is already held at that target. That costs at most one needless release, never a missed one.
