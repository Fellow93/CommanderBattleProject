# Battle cries

**Status:** in progress (v1 built, not played) · **Size:** S

## Goal

When a commander orders a charge or an advance, the AI soldiers of that formation shout in a staggered wave. Men who break and run cry out in fear.

## What exists today

- **Server voices reach the clients.** `Agent.MakeVoice(type, NoPrediction)` is native; the engine replicates it. Evidence in the decompiled client and server:
  - The stock multiplayer bark (`Agent.HandleBark`) makes the voice on the server only (`!GameNetwork.IsClientOrReplay`).
  - The `BarkAgent` message it sends is handled on the client by `HandleBark` again, which does nothing there, plus a chat line. So the sound other players hear comes from the engine.
  - `AgentVictoryLogic` and `AgentMoraleInteractionLogic` make AI voices on the server the same way.
- **Stock charge yell.** `OrderController.PlayOrderGestures` calls `Agent.YellingBehaviour` on a charge (a morale × 0.33 % chance per agent, once in 30 s per controller), only when order gestures are enabled. Not relied on.
- **Stock rout cry.** `AgentMoraleInteractionLogic.OnAgentFleeing` plays `Debacle` for 70 % of fleeing agents, but this mode doesn't run that logic. `MissionAgentPanicHandler` (which it does run) raises `Mission.OnAgentFleeing`.
- **Order hooks on the server.** Every player's order arrives through `Team.GetOrderControllerOf(peer agent)`; `Team.OnOrderIssued` covers all of the team's controllers. The general's orders to the AI's formations go through a controller of our own (`GetGeneralOrderController`), which is not one of the team's.
- `Native/ModuleData/voice_definitions.xml` has `Yell`, `Charge`, `HorseRally`, `Fear` and `Debacle` for every voice.

## Design (as built)

`CommanderBattle/MissionMultiplayerCommanderBattle.BattleCries.cs`, server only:
- Hooks `Team.OnOrderIssued` for both teams (in `AfterStart`) and the general's controller (when made).
- Only commanders' orders: the controller's `Owner` has a `MissionPeer`, or it is the general's controller.
- `Charge` and `ChargeWithTarget`: one in two AI soldiers of each formation ordered shouts. `Advance`: one in four. Each at a random 0.2 to 2 s. One cry per formation in 10 s.
- On foot: `Yell`, or the order's word (`Charge`, `Advance`) for one in four of those shouting. Mounted: `HorseRally`, as the stock yell does.
- **Order echo** (asked by the user after v1: "other orders should also be repeated"): every other order with a stock voice (`GetOrderWord`, the mapping of `OrderController.PlayOrderGestures`, minus delegate/undelegate and the naval ones) is repeated by 3 random AI soldiers of the formation at 0.4 to 1.8 s, with the commander's word. One echo per formation in 3 s, since dragging move lines gives many orders. A charge or advance within its 10 s cry cooldown is echoed instead.
- A soldier who started fleeing before his turn doesn't shout.
- `OnAgentFleeing`: one in two AI humans cry `Fear` within 1.2 s.
- Queue cleared in `OnClearScene`. The detailed log records each order ("<name> ordered Charge: N of <formation> shout").

## Things still to check in game

- Other players hear the soldiers (the engine replication; the cheer's voices were never confirmed in game either).
- The loudness of half a formation yelling at once.
- Whether `Fear` or `Debacle` sounds better for routing men.

## Files it touches

- New `CommanderBattle/MissionMultiplayerCommanderBattle.BattleCries.cs`.
- `CommanderBattle/MissionMultiplayerCommanderBattle.cs`: hook, unhook, tick, round reset.
- `CommanderBattle/MissionMultiplayerCommanderBattle.GeneralCommand.cs`: hooks the general's controller.
- `README.md`: a rules bullet and a test checklist line.

## Done when

- In game: a charge order makes the formation shout in a wave, heard by another player; an advance, fewer. A routing formation cries out.
- No exceptions in the CommanderBattle log; the detailed log shows the "ordered Charge" lines.
