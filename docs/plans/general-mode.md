# General mode and drawn orders

**Status:** in progress (v1 built and played 2026-10-26; v2 camera and Ctrl cursor built, not played yet) · **Size:** L

Follows `general-role.md` (phase 0: the general as a pickable role, played 2026-10-26).

## Goal

The side's general (a player in slot 8) presses **F1** to enter **general mode**: a high camera over the battlefield. There he draws orders on the map for his side.
- **v1** has two tools in a small general menu:
  - **Path:** hold the left mouse button and drag along the ground, release, then type a label.
  - **Delete:** click an order's label to remove it.
- His side's players see the orders while they hold **Left Alt**.
- Later order types, such as a path with an arrowhead, come as more tools.

## Decisions (the user, 2026-10-26)

- **F1** enters and leaves general mode.
  - The general has no order bar use: his formation 8 only follows him, and the stock bar never opens for him (`MissionOrderVM.CheckCanBeOpened` finds no formation 0–7 he owns).
  - For everyone else, F1 stays the stock order key.
- **Camera:** zoomed out much more than the normal view.
- **Path:** hold and drag. **A plain line,** with the label in the middle of the path; the arrowed path is a separate future order type.
- **Lifetime:** until the round ends, or until the general deletes it.
- **His soldier stands still** in general mode; the bodyguard wedge stays with him.

**After playing v1 (the user, 2026-10-26):** "Camera doesn't work properly, and map is not visible properly. I wanted basically same view as when commander is giving orders but zoomed out." Decided:
- **Camera = the commander's order camera lifted about 3×.** Default lift 150 (about 45 m up, 105 m back), wheel 50–300, kept for the session, same easing. The mouse turns the view; no WASD panning, since the camera stays on the general. One view owns the offsets.
- **The cursor only while Left Ctrl is held.** Ctrl held: the view stops turning, a cursor shows, and drawing, the menu buttons and Delete clicks work. Released: the cursor hides and the view turns again.

## Design (from the research, 2026-10-26)

**Key and stock order UI.** F1 is the stock `SelectOrder1` game key (`MissionOrderHotkeyCategory`). For the general it only pops "There isn't any unit under command."
- The client's order handler becomes a subclass of `MissionGauntletMultiplayerOrderUIHandler` whose `TickInput` returns early for the general (`Client.MySlot?.IsGeneral`).
- It is created directly in `CommanderBattleMissionViews` rather than through `MultiplayerViewCreator`.
- It needs the `TaleWorlds.MountAndBlade.GauntletUI` and `.ViewModelCollection` references in `CommanderBattle.View.csproj`.

**Camera (v1, replaced in v2: see "As built (v2)").** `MissionScreen.CustomCamera` with our own `Camera` (stock precedent: `MissionGauntletTournamentView`).
- Start high above the general, tilted, not straight down.
- WASD pans (game keys, speed by height) and the wheel zooms (clamped height). XY is clamped to the mission boundaries.
- On exit: `CustomCamera = null`, release the camera. The follow camera resumes where it was.
- The stock free camera is capped at 80 m and needs Deployment mode, so it isn't used.

**Freezing the agent.** `MissionMainAgentController.IsDisabled = true`, plus zeroed `MovementFlags` / `MovementInputVector` each tick. `MissionScreen.SetDisplayDialog(true)` silences the cheer, equip and drop views. ESC leaves general mode first, through `OnEscape` at a view priority below the escape menu's 50.
- **Safety: general mode closes when the general is hit,** because he stands still in the open.

**Mouse.** A general-mode Gauntlet layer with a visible mouse and a full-screen, event-accepting root, so clicks on empty ground reach it.
- Ground point: `MissionScreen.ScreenPointToWorldRay` + `Scene.RayCastForClosestEntityOrTerrain`, or `GetProjectedMousePositionOnGround`.
- Drawing: press starts, held samples a point every few metres (the line is thinned), release ends.

**Label.** The stock `InformationManager.ShowTextInquiry` popup (a global, focus-taking layer, so typed keys never reach the agent). 40 characters at most, checked by its `TextCondition`. Cancel discards the path. While the inquiry is active, the general layer ignores clicks.

**Showing the orders.** Screen-space Gauntlet widgets, as `CommanderBattleAIStatusView` already does for destination markers:
- world points projected each frame with `MBWindowManager.WorldToScreenInsideUsableArea`;
- one rotated thin widget per segment;
- a text label at the path's midpoint, which is a button in Delete mode;
- points behind the camera skipped.
They show while Left Alt is held, and always in general mode. They are drawn over terrain and units (no depth test); a world mesh is a possible v2.

**Network** (`CommanderBattle/Network`, shared dll). Points travel as `Vec2`; clients take the height from the terrain.
- Client → server: `RequestGeneralOrder` (up to 64 points, label ≤ 40) and `RequestDeleteGeneralOrder(id)`.
- Server → the general's side only (per-peer `SendTo`, never `Broadcast`): `GeneralOrderAdded(id, points, label)` and `GeneralOrderRemoved(id)`.
- Late joiners and team changers get the side's orders again.
- **Server checks:**
  - the sender is the side's general;
  - point and label caps;
  - `<`, `>` and control characters stripped;
  - points inside the boundaries;
  - at most **10 orders per side** (the oldest goes).
- Orders are cleared at round end and in `OnClearScene`.

## Files it touches

- `CommanderBattle/Network/`: the four messages.
- `CommanderBattle/MissionMultiplayerCommanderBattle*.cs`: the server's order list per side, its checks, sending, and clearing.
- `CommanderBattle/MissionMultiplayerCommanderBattleClient.cs`: the client's order list per side, with change events.
- `CommanderBattle.View/`:
  - the general-mode view (camera, freeze, input, tools, label popup);
  - the drawing of orders (with the AI status view or its own layer);
  - the order UI subclass;
  - `CommanderBattleMissionViews.cs`.
- `Module/GUI/Prefabs/`: the general menu, the drawing overlay, brushes.
- `README.md`.

## As built (v1, 2026-10-26)

Built as designed above; `README.md` ("General mode") has the rules. Where the code settled what the design left open or got wrong:

- **Two layers, not one.** This view's layer draws the paths and labels and holds the menu; it takes the mouse only where a button is. Below it, in general mode only, an input layer (`CommanderBattleGeneralModeInput.xml`, an empty full-screen widget that accepts events) takes the mouse everywhere else. So a press on the input layer is always a press on the ground, never on the menu.
- **Labels are not in zero-sized roots.** Gauntlet hit-tests children of a zero-sized widget but only dispatches clicks into widgets whose area holds the mouse (`EventManager.CollectEnableWidgetsAt`). The label and segment items sit in plain `Widget` containers (no stacking) and move their own root with `PositionXOffset/YOffset`. A label is a `ButtonWidget` that is disabled outside the Delete tool, so it takes no clicks then.
- **Projection uses `CombatCamera`**, in `OnMissionTick`: with a custom camera on, `MissionScreen` copies it into `CombatCamera` each frame, so `CombatCamera` is what is rendered.
- **The ground ray is lengthened.** `ScreenPointToWorldRay` cuts the ray at the boundary radius (30 m without boundaries). The view casts 3000 m along it instead.
- **F1** is read as `MissionOrderHotkeyCategory.SelectOrder1` on the scene layer, keyboard only (the stock bar ignores its gamepad button outside the bar too).
- **Hits.** Leaving on a hit uses `OnAgentHit` and, because a client may not run the blow, a drop in the general's (synced) health. Losing or hurting his horse counts too.
- **Entering needs the general alive with his soldier**, during the fight. The server only accepts orders while the round is in progress, so nothing is drawn during preparation.
- **Lifetime.** The server clears and sends `GeneralOrderRemoved` for every order when the round leaves `InProgress`; both sides also clear silently in `OnClearScene`. Clients file orders by side and drop the other sides' on their own team change, so the orders the server sends a team changer are kept whichever message arrives first.
- **No first-person change** was needed: the custom camera replaces the follow camera either way. (v2 forces third person.)

## As built (v2, 2026-10-26): order-camera view and Ctrl for the cursor

v1's custom camera is gone. Likely causes of the bad map rendering: the custom-camera branch of `MissionScreen.CheckForUpdateCamera` never calls `Mission.SetCameraFrame` (the stock follow camera calls it at the end of `UpdateCamera`), and v1 set no near/far planes of its own. v2 uses the stock follow camera, so both come from the stock path.

- **One view owns the offsets.** `CommanderBattleOrderCameraView` gains `IsGeneralMode` (set by the general mode view on entering and leaving) and `ZoomGeneralMode(notches)`. In general mode its lift target is `_generalTargetLift` (static, default 150, ×0.88 per wheel notch, 50–300) instead of the order bar's; it rises with no delay, with the same rise (0.7 s), fall (1 s) and lift easing. The general mode view only reads the wheel (from the scene layer, or from its two layers while the cursor shows; Gauntlet gives the wheel to one layer only) and passes it on.
- **How the stock camera takes the offsets** (`MissionScreen.UpdateCamera`, around lines 1480-1620): `CustomCameraTargetLocalOffset` is added as a world vector to the follow target; `CustomCameraLocalOffset` is in the camera frame, so the pull back runs along the view. The stock box cast (`BoxCastOnlyForCamera`) starts from the already shifted target and covers only the follow camera's own few metres. So it never pulls the camera in at these distances, and it doesn't stop the pull back going into terrain either. `SetCustomCameraIgnoreCollision` is therefore left alone.
- **Terrain.** Looking up turns the pull back downward (64° up at lift 150 would put the camera about 50 m under the general), and a hill behind him can be in the way. The camera view samples the terrain height every 4 m along the pull back (plus the follow distance) and stops 2 m short of the ground; coming in is immediate, going out is eased. It applies to the order bar's camera too, which had the same problem looking up at large lifts. It checks terrain only, not buildings or trees, so the order bar's camera is unchanged elsewhere.
- **Vertical limits** are the stock ones: elevation from about 78° down to 64° up (`MissionScreen.HandleUserInput`). At 78° down and lift 150 the camera is about 148 m up, nearly over the general. On entering, the view keeps the player's elevation (the setter is private), so he looks down with the mouse to see the field.
- **Third person is forced** while in general mode (`Mission.CameraIsFirstPerson = false`, every tick too, because the camera toggle key still works); on leaving, first person comes back if it was on.
- **The agent controller.** `MissionScreen.UpdateCamera` sets `MissionMainAgentController.IsDisabled` back to false every frame for a camera-attachable main agent. The general mode view sets it to true on every screen tick, which runs between that and the controller's next `OnPreMissionTick`. Camera bearing and elevation come from `MissionScreen.HandleUserInput` (raw mouse movement, not the controller), so the frozen agent doesn't stop the view turning.
- **Ctrl and the cursor.** The input layer is added on entering and kept for the whole of general mode. While Left Ctrl is held (read on the scene layer, which has the keys), both layers get `SetInputRestrictions(isMouseVisible: true, Mouse)`; on release, `ResetInputRestrictions`. A visible mouse is all it takes to stop the view turning: `HandleUserInput` then only turns at the screen's edge (within 1% of it), or with the right button held on the scene layer, as in the stock order view. While the cursor shows the layers have the mouse buttons, so the right-button turning doesn't happen.
- **Ctrl released mid-drag finishes the path** (the label popup comes up; Cancel discards it), so a slip of the finger doesn't lose a long path.
- **While the label popup, the formation list or the troop screen is open**, the cursor state isn't touched: those layers show their own cursor. It follows Ctrl again once they close.
- **Left Ctrl bindings:** none in the stock combat or generic keys; the only stock binding is the multiplayer `StoreCameraPositionModifier`, read by `SpectatorCameraView` for spectators only. The general's controller is off, and the order UI's `TickInput` returns early for him.
- **Point spacing** while drawing is now max(3 m, 0.02 × the camera's distance to the point).

## Open questions

None open for v1.

## Done when

- **As the general:**
  - F1 opens general mode with the order camera lifted about 3×; the mouse turns it, the wheel zooms, the map renders properly, and F1 or ESC leaves.
  - Holding Left Ctrl stops the view and shows the cursor; releasing it turns the view again.
  - His soldier stands still, and no "no unit under command" message appears.
  - Path: drag a line, type a label, and it shows.
  - Delete: click a label and it's gone.
  - Being hit leaves general mode.
- **As another player on his side:** holding Left Alt shows the paths and labels. The other side never sees them.
- **Lifetime:** orders are gone after the round ends. A player joining mid-round sees the existing orders.
- **Logs:** each order added or deleted, with the general's name.
