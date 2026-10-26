# Cheer with your men

**Status:** in progress (v1 built, not played) · **Size:** S

## Goal

When a commander cheers, their formation cheers with them, in a wave that rolls through the ranks. It's a way to show off before the armies close, and it makes the AI soldiers feel like your men.

## The user's decisions (2026-10-26)

- **Cheers only.** Barks ("Charge!", "Follow me!") get no echo for now; battle cries can be their own plan.
- **Any time in the round,** but only soldiers who are free to: not near an enemy, not attacking, defending, drawing or reloading.
- **Only the commander's own formation.** The general is no exception: his cheer reaches his bodyguard, not every ally around him. (The 50 m wave of the first draft is dropped.)
- **10 s cooldown** per commander.

## What exists today

- The stock cheer wheel is in the mission views (`ViewCreator.CreateMissionMainAgentCheerBarkControllerView`, `CommanderBattle.View/CommanderBattleMissionViews.cs`). The real view is `MissionGauntletMainAgentCheerControllerView` (GauntletUI).
- **How a multiplayer cheer works** (read in the decompiled client and the net6.0 server, same bodies):
  - The client sends `NetworkMessages.FromClient.TauntSelected(IndexOfTaunt)` and plays nothing itself. Its 4 s cooldown is client-side only.
  - The server's `MissionNetworkComponent` handles it (`HandleClientEventCheerSelected`): `ControlledAgent.HandleTaunt(index, isDefaultTaunt: false)`, which sets an action on channel 1 (`CosmeticsManagerHelper.GetSuitableTauntAction`). It returns false when the peer has no agent.
  - No managed message carries the action: the engine replicates action channels and server-side `MakeVoice(..., NoPrediction)` itself. Stock sets AI cheers on the server the same way (`AgentVictoryLogic`).
  - `GameNetwork` keeps a list of handlers per client message and runs them in order until one returns false. A handler of ours registered after the stock one runs after it.
- **Stock AI cheer** (`AgentVictoryLogic.ChooseWeaponToCheerWithCheerAndUpdateTimer`): skips an agent switching weapons, doesn't cheer with a wielded `DropOnAnyAction` item, plays one of `act_cheer_1..4` on channel 1 (`ignorePriority: false`, so attacks and blocks aren't cut) for mounted agents, and `MakeVoice(Victory, NoPrediction)`.

## Design (as built)

`CommanderBattle/MissionMultiplayerCommanderBattle.Cheer.cs`, server only:
- A second handler for `TauntSelected`, registered in `AddRemoveMessageHandlers`. It always returns true: false would stop the handlers after it and mark the packet invalid.
- The sender must have a living agent in the formation they command (`MissionPeer.ControlledFormation`), the general's included, and be off cooldown (10 s, server-side).
- Every other AI agent of that formation gets a cheer at a random 0.1 to 1.5 s from now, queued and played from the mission tick.
- When its time comes, an agent cheers only if it is still active, no enemy is within 20 m (`Mission.HasAnyAgentsOfSideInRange`), its channel-1 action is idle (not attacking, defending, aiming, reloading or switching weapons), it isn't using a game object or in water, and it doesn't hold a `DropOnAnyAction` item. It plays one of `act_cheer_1..4` (foot and mounted alike), and one in three also shouts (`Victory`).
- Any taunt from the wheel starts it, not only the four free cheers.
- The queue is cleared each round (`OnClearScene`). The detailed log records each cheer ("<name> cheered: N of <formation> join in").

## Things still to check in game

- The cheer wheel has entries in Commander Battle at all (its taunt list comes from the player's lobby cosmetics).
- Other players see and hear the AI soldiers cheer (engine replication of server-side actions and voices).
- Mounted soldiers' cheers look right.
- RBM: Frontline holds and RBM's animations don't fight the cheer.

## Files it touches

- New `CommanderBattle/MissionMultiplayerCommanderBattle.Cheer.cs`.
- `CommanderBattle/MissionMultiplayerCommanderBattle.cs`: handler registration, tick, round reset.
- `README.md`: a line in the rules list and the test checklist.

## Done when

- In game: a commander's cheer makes their formation cheer in a rolling wave, seen and heard by other players. The general's cheer reaches only his riders. Soldiers fighting, aiming or reloading don't cheer. A second cheer within 10 s gets no echo.
- No exceptions in the CommanderBattle log; the detailed log shows the cheer lines.
