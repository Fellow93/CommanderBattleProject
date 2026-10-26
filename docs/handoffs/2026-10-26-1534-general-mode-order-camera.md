# Handoff: general mode v2, the order camera lifted 3×, cursor on Ctrl

Session of 2026-10-26, written 15:34. It follows `2026-10-26-1512-general-mode-v1.md` (same session).

## State

- **Not committed:** all of the general role and general mode work. The user asked not to commit yet.
- **Installed:** the client View DLL. This change is client-only: no network or server code changed, and the server module's DLLs still match the build.
- **Running:** server pid 29216, unchanged, and fine for this client.
- **Client:** restart the game.

## What was done

**The user's report on v1:** "Camera doesn't work properly, and map is not visible properly; I wanted basically same view as when commander is giving orders but zoomed out."
- v1 used `MissionScreen.CustomCamera`. Its branch of `CheckForUpdateCamera` never calls `Mission.SetCameraFrame`, which most likely caused the bad terrain/LOD rendering.

**The user's decisions:**
- The camera is the commander's order view, lifted about 3×.
- The cursor appears only while **Left Ctrl** is held (drawing, tool buttons, labels). Otherwise the mouse turns the view.

**Built by an Opus agent; see `docs/plans/general-mode.md`, "As built (v2)":**
- **`CommanderBattle.View/CommanderBattleOrderCameraView.cs`** is now the only writer of the camera offsets.
  - `IsGeneralMode` and `ZoomGeneralMode`.
  - General lift: default 150 (about 45 m up, 105 m back), ×0.88 per wheel notch, 50–300, kept for the session.
  - **New terrain limit:** terrain height is sampled every 4 m along the pull-back, and the camera stops 2 m short of the ground. Looking up, or a hill behind, drove the camera underground. This also applies to the normal order view.
  - Forces third person in general mode and restores first person on exit.
- **`CommanderBattle.View/CommanderBattleGeneralModeView.cs`:**
  - `CustomCamera` and WASD panning are removed.
  - Ctrl switches the input restrictions of both general layers (`SetCursor`). Without Ctrl the mouse turns the view; a visible cursor holds the view still, except stock edge turning within 1% of the screen edge.
  - Releasing Ctrl mid-drag finishes the path and goes to the label popup.
  - The wheel is summed from the scene layer and both general layers.
  - Point spacing scales with the camera's distance.
  - **`MissionMainAgentController.IsDisabled` is now set every tick,** because stock `MissionScreen.UpdateCamera` re-enables it every frame. v1 only got away with setting it once because the custom camera skipped `UpdateCamera`.
- **`GeneralModeVM.cs`:** new keys line ("Mouse: look around    Hold Ctrl: cursor    Mouse wheel: zoom    F1 or Esc: leave").
- **Docs:** README "General mode" and checklist; `docs/plans/general-mode.md` (decisions, v1 camera marked replaced, as built v2); status in `docs/plans/README.md`.

## Verified vs not

- **Verified:** the build (no compiler errors or warnings); the server module's DLLs still match.
- **Not played.** What to try first:
  1. F1: the camera rises and the map renders properly, the mouse turns the view, the wheel zooms, WASD does nothing;
  2. look straight up, and towards a hill behind: the camera never goes under the ground;
  3. Ctrl: the cursor shows and the view holds still; draw, use the tools, delete; release Ctrl mid-drag and the popup comes up;
  4. entering from first person goes to third, and leaving brings first person back;
  5. the soldier really stands still;
  6. the normal commander order camera behaves as before.

## Open items

- **Play it.** The safety exit on a hit still needs the user's confirmation, carried over from 15:12.
- **Known:** pressing R (camera toggle) in general mode may flash first person for one frame.
- Commit when the user says so.

## Traps

- **`MissionScreen.UpdateCamera` resets `MissionMainAgentController.IsDisabled` to false every frame.** Disable it every tick, or use a custom camera, which skips `UpdateCamera`.
- **The `CustomCamera` branch skips `Mission.SetCameraFrame`.** Prefer the stock follow camera with `Mission.SetCustomCamera*Offset` for gameplay views.
- **Large `SetCustomCameraLocalOffset` pull-backs** run along the view direction, so looking up puts the camera underground. Limit them against the terrain.
