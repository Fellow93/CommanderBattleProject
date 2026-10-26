# Handoff: general mode on Ctrl+C, with an on-screen hint

Session of 2026-10-26, written 16:51. It follows `2026-10-26-1534-general-mode-order-camera.md` (same session). Everything up to then is committed as `74c4a38`.

## State

- **Not committed:** this change.
- **Installed:** the client View DLL and the prefab. This change is client-only: no shared or server code changed (`git diff` under `CommanderBattle/` and `RBM.Multiplayer/` is empty).
- **Running:** server pid 32432, which still matches. The build's copies into the running server failed as expected (locked DLLs) and don't matter here.
- **Client:** restart the game.

## What was done

The user asked for Ctrl+C instead of F1, and a visible hint on screen for the general.

- **`CommanderBattle.View/CommanderBattleGeneralModeView.cs`: the key.**
  - `IsTogglePressed` reads Ctrl (left or right) plus a C press, raw on the scene layer, keyboard only.
  - No multiplayer mission key uses C; the only stock C bindings are photo mode, crafting and campaign panels.
  - The general layers only take the mouse, so the scene layer keeps the keyboard while Ctrl shows the cursor, and Ctrl+C also leaves.
- **The same file and `GeneralModeVM.cs`: the hint.**
  - `IsEnterHintVisible` / `EnterHintText` ("Ctrl+C: General mode (draw orders for your side)").
  - Set every screen tick: not in general mode, he is the general on his side, his soldier is alive, and the round is in progress.
- **`Module/GUI/Prefabs/CommanderBattleGeneralMode.xml`:** the hint is a dark box at the top centre (MarginTop 90, where the menu opens), `DoNotAcceptEvents`.
- **`GeneralModeVM.KeysText`:** "Ctrl+C or Esc: leave".
- **Comments** in `CommanderBattleOrderUIHandler.cs` and `CommanderBattleMissionViews.cs`. The order UI still ignores F1–F9 for the general.
- **Docs:** the README "General mode" bullets and checklist, and `docs/plans/general-mode.md` (the decision).

## Verified vs not

- **Verified:** the build (no compiler warnings or errors); the installed client DLL and prefab.
- **Not played:**
  - the hint shows only for the general mid-round;
  - Ctrl+C enters and leaves, also while Ctrl is held for the cursor;
  - F1 does nothing for him.
- The general mode v2 camera and cursor (15:34 handoff) are still unplayed.

## Open items

- Play it.
- **The safety exit on a hit** still needs the user's word, carried over from 15:12.
- Commit when the user says so.
