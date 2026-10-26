# Handoff: allied player markers on Alt

Session of 2026-10-26, written 18:21. It follows `2026-10-26-1802-general-orders-v1.md` (another session).

## State

- **Not committed.** This sits on top of the uncommitted general mode and general orders work (see the 18:02 handoff).
- **Installed:** client only (`CommanderBattle.View.dll`, the new prefab). The change is client-only, so the server needs nothing.
- **Running:** a server, pid 21560, started 18:05 by another session. I left it alone. Its server DLLs are locked, so the full solution build's server copies failed (MSB3027). Nothing in this change goes to the server.
- **Another session's edit:** `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs` changed at 18:13, after that server started. My client build includes it. If it changed a network message, the client and the running server won't match. Ask that session.

## What was done

While Left Alt is held, each allied player within 100 m gets a green dot and a label with **their name only** over the head. The user first asked for the name plus the formation type, then cut it to the name.
- `CommanderBattle.View/CommanderBattleAllyPlayerMarkersView.cs` (new):
  - finds players through the side's formation slots (`Slots.GetSlots(MySide)`, `Commander`, `ControlledAgent`), one marker per slot, allocated once;
  - measures distance from `Agent.Main`, or from the camera while spectating;
  - anchors the marker at `GetEyeGlobalPosition()` + 0.6 m;
  - projects it to the screen like `CommanderBattleAIStatusView`.
- `CommanderBattle.View/AllyPlayerMarkersVM.cs` (new).
- `Module/GUI/Prefabs/CommanderBattleAllyPlayerMarkers.xml` (new): the AI status markers' layout, in green.
- `CommanderBattle.View/CommanderBattleMissionViews.cs`: the view is registered after the AI status view.
- `README.md`: a rule line in the Alt paragraph, and a checklist item.

**Why not the stock view:** the stock `MissionGauntletMultiplayerMarkerUIHandler` / `MultiplayerMissionMarkerVM` (decompiled into the scratchpad) shows every teammate on game key 5 (Alt) at any range. Its view model is all private, so no range limit can be added to it.

## Verified vs not

- **Verified:** the build (0 errors) and the client install.
- **Not played.** Test with two players on a side:
  - the marker shows within 100 m and is gone beyond;
  - enemies never get one;
  - a mounted player's marker sits over his head;
  - a dead spectating player sees allies near the camera.

## Open items

- Play the checklist item.
- Commit when the user says so.

## Traps

- The stock MP assemblies aren't in `decompiled/`. `ilspycmd -p -o <dir> <dll>` on `Modules\Multiplayer\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection.dll` / `...GauntletUI.dll` gets them in seconds.
