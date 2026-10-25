# Handoff: posture animations reach clients

Session of 2026-10-25, written 16:38. Follows `2026-10-25-1611-smaller-armies-formation-list.md`.

## State

- **Not committed:** `RBM.Multiplayer/RbmMpPatcher.cs` and `RBM.Multiplayer/Stance/StanceNetworkSync.cs`. The rest of the tree matches `d9a5905`.
- **Installed:** full Release build at 16:38, 0 errors. The temporary diagnostics are removed from both the source and the installed build.
- **Running:** dedicated server pid 26700, battle_terrain_029. The patch line reads "91 classes applied, 0 failed, 3 skipped". No game client is running.

## The problem

The user reported that posture animations (RBM's stagger, tired, kick/bash falls) never played. The sync built in the 10:13 session (`Stance/StanceAnimationSync`, `Network/StanceAnimation`) had never been seen working. A diagnostics round showed RBM asking for about 80 staggers on the server, while the client received no messages at all.

## What was fixed

1. **The broadcast was never switched on** (`Stance/StanceNetworkSync.cs`). `IsActive` was set in `OnBehaviorInitialize`. `Mission.Initialize` calls that on its behaviours *before* the submodules' `OnMissionBehaviorInitialize`, and `AddMissionBehavior` doesn't call it either, so a behaviour added from that hook never runs it. `IsActive` stayed false and `StanceAnimationSync` never sent anything. `IsActive` is now set in `EarlyStart`, which runs after. This was the main cause.
2. **Patches on `Agent.SetActionChannel` now go first** (`RbmMpPatcher.cs`, `PatchesSetActionChannel`). Patching a method makes Harmony compile its replacement at once, and the JIT inlines a small callee like `SetActionChannel` into it unless that callee is already patched. Calls from such a replacement then bypass the broadcast postfix. In the diagnostics round, every `ForceStaggerAnimation` call (patched by the diagnostics before `SetActionChannel` was) skipped the postfix. Ordering the `SetActionChannel` patch classes first rules this out for any present or future patch.

## Verified (second diagnostics round, server pid 26952, client pid 16684)

- Every RBM request reached `SetActionChannel` on the server, which returned true and broadcast it.
- The client received each one; `SetActionChannel` returned true and the agent was still playing the clip 0.25 s later. Seen for `act_stagger_backward/forward/left` (including a mounted ch1 stagger), `act_scared_to_normal_2` (the tired clip, ch0+ch1, starting at progress 0.40), and both kick/bash falls.
- Two clips were replaced within 0.25 s by a fresh hit reaction (a new blow), which is normal.

## Not verified

- **Reactions on the player's own agent.** All events in the sample were on AI soldiers. Check that a player's stagger and the tired fallback (`TiredReactionPatch`, the backward stagger) play on their own screen, and that client prediction doesn't fight them.
- **Double-starts:** the engine's own agent sync may already carry the server's action for AI agents. The 0.25 s checks showed clean progress (0.18–0.20 for staggers), with no sign of restarts, but this was not looked at in game.
- The client build that ran the test still had the diagnostics in; the clean build is installed but has not been played yet.

## Open items

- Check player-agent reactions in game (above).
- Message load from the 10:13 handoff still applies: `StanceAnimation` goes out reliable. If players drop in large fights, make it unreliable.

## Traps found (added to `CLAUDE.md`)

- A behaviour added in `OnMissionBehaviorInitialize` never gets `OnBehaviorInitialize`. Use `EarlyStart` or later.
- Harmony replacements can inline small game methods that aren't patched yet. A postfix on such a method (here `Agent.SetActionChannel`) must be applied before any patch whose target calls it.
