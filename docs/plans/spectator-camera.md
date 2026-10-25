# Spectator / free camera

**Status:** idea · **Size:** S–M

## Goal

Observers, streamers and players waiting for a slot can watch the battle from a free camera, follow formations, and see what both sides are doing.

## What exists today

Most of the base is there:
- A player on the **spectator team** gets a free camera: `MissionMultiplayerCommanderBattleClient.GetMissionCameraLockMode` returns `SpectatorCameraTypes.Free` for them. A side player without an agent is locked to their formation (`LockToPlayerFormation`).
- The client views include the stock `SpectatorCameraView` and `MultiplayerSpectatorSilhouetteView` (`CommanderBattle.View/CommanderBattleMissionViews.cs`).
- A player who joins a full side is moved to spectator (`PlaceQueuedPeers`).

Not checked in game: how the free camera moves on the big campaign maps, and what the HUD shows a spectator.

## Steps

1. **Check in game** (S). Join as spectator: does the free camera work, how fast and how high can it go, can it cycle through agents? What does the HUD show (strength bar, AI panel, formation list)?
2. **Faster, higher camera** (S). The battlefield is far larger than stock MP maps. If the stock camera is too slow, add a speed boost or a height limit for spectators, as `CommanderBattleOrderCameraView` does for commanders.
3. **Follow a formation** (S–M). Cycle through the 16 formations (both sides) and follow each one's centre, not just single agents. Name the formation and its commander on screen.
4. **Spectator overview** (M). Let spectators hold Left Alt to see the AI panel and destination markers for both sides. `CommanderBattleAIStatusSync` sends `SideAIStatus` only to the side's own players today, so it would also need to go to spectators. That reveals both sides' plans, so only to spectators.
5. **A "spectate" choice** in the team selection screen, if the stock one isn't obvious enough.

## Things to work out

- Can a spectator who sees both AI plans pass them to a side? Accept it for casual play, or make the overview an admin/server option.
- Performance: the free camera can show the whole battle at once with 1000 agents on a client.

## Files it touches

- `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs` (camera mode).
- A new `CommanderBattle.View/CommanderBattleSpectatorCameraView.cs` for speed, height and formation follow.
- `CommanderBattle/CommanderBattleAIStatusSync.cs`, `CommanderBattle.View/CommanderBattleAIStatusView.cs` for the overview.

## Open questions for the user

1. Is this for streaming/casting (needs the full overview) or just for people waiting (step 1–2 is enough)?
2. Should spectators see both sides' AI plans?

## Done when

- In game: a spectator flies across the whole map, follows any formation, and (if chosen) sees both AI panels. Side players still see only their own.
