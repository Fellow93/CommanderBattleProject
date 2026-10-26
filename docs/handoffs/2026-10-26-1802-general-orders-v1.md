# Handoff: the general orders formations (v1)

Session of 2026-10-26, written 18:02. It follows `2026-10-26-1653-general-mode-played.md` (same session).

## State

- **Committed:** everything up to `74c4a38`.
- **Not committed** (the user asked to hold commits):
  - the Ctrl+C key and hint (16:51);
  - the 16:53 notes;
  - all of this.
- **Installed:** both sides. The server module's DLLs match the build.
- **Running:** server pid 29436, map `battle_terrain_biome_075`. Patch line: "94 classes applied, 0 failed, 3 skipped".
- **Client:** restart the game. `SideAIStatus` changed format and two messages are new, so an old client doesn't match.

## What was done

The spec is `docs/plans/general-orders.md`: the user's decisions, the design from the Opus planning report, and the implementing agent's "As built" section.

**User decisions:**
- the stock order bar;
- one formation at a time;
- suggestions to player formations as a pop-up and an Alt marker, for that commander and the general only;
- everything back to the AI when the general is lost;
- every round starts delegated.

**Defaults I took when the user skipped the questions:**
- a suggestion lasts until replaced or the round ends;
- RBM may regroup delegated formations, never ones the general controls;
- centre pop-up + sound + log line.

**Design:** AI formations keep `PlayerOwner == null` on the server, so bot scoring, spectator cards and the RBM guard still work. Only the general's client mirrors `PlayerOwner = Agent.Main` on formations 0–7, so the stock bar opens and selects for him.

**Built:**
- **Shared:** `CommanderBattle/GeneralCommands.cs`; `Network/RequestGeneralCommand.cs` and `Network/GeneralSuggestion.cs` (new); `Network/SideAIStatus.cs` (`IsPlayerLed` → `Leader {AI, General, Player}`).
- **Server:** `MissionMultiplayerCommanderBattle.GeneralCommand.cs` (new).
  - Checks: the sender is the general, his agent alive, the round in progress, 40 commands/s at most, boundaries, an enemy charge target, follow only the general.
  - Takes over an AI formation with an owner-less `OrderController` per side.
  - Logs "took over" and "delegated".
  - `ReleaseGeneralCommand` on his death, a general slot change, and the round leaving InProgress.
  - Suggestions are collected per tick and sent only to the formation's commander and the general.
  - Plus `MissionMultiplayerCommanderBattle.cs` (hooks and clearing), `CommanderBattleAIStatusSync.cs`, and `CommanderBattleActivityLogic.MarkActive` (orders count as activity for the inactivity kick).
- **Client:** `MissionMultiplayerCommanderBattleClient` is partial, plus `...Client.GeneralCommand.cs`.
  - The mirror; `IsAIControlled` follows `SideAIStatus`, with a 3 s local override after an order.
  - Single selection: the newest is kept; on select-all, the previous one or the lowest index.
  - The `OnOrderIssued` hook sends the commands.
  - Suggestions are stored and announced with `AddQuickInformation` + a log line. The commander alone also hears `event:/ui/notification/alert`.
- **View:**
  - `CommanderBattleOrderUIHandler.cs`: the general's F1–F9 block is gone, and the display-dialog flag is lifted around `base.TickInput` in general mode.
  - `CommanderBattleVisualOrderProvider.cs` (new, registered in `CommanderBattleViewSubModule`): the stock layouts plus the Delegate toggle, for the general only.
  - `CommanderBattleOrderTroopPlacer.cs` (new, created in `CommanderBattleMissionViews`): a 3000 m ground ray in general mode.
  - `CommanderBattleGeneralModeView.cs`: Esc passes on while the bar is open.
  - `GeneralModeVM` keys line ("F1-F9: order bar").
  - AI status view/VM/prefab: "led by the general", markers, blue suggestion markers.
  - `FormationSelectionVM` ("AI · led by the general").
- **RBM:** `RBM.Multiplayer/FormationOwnershipGuard.cs`: soldiers move only between formations that are both AI-led (`PlayerOwner == null && IsAIControlled`).
- **Docs:** README "The general's orders", plus the General mode / Alt / RBM text, the table rows, the checklist and two known limitations; the plan index.

## Verified vs not

- **Verified:**
  - the build (0 compiler errors, no new warnings; one old SYSLIB0032 in RBM's source);
  - the server DLL hashes;
  - the restart with 0 failed patches;
  - I read the server handler's checks and that suggestions go per peer, never broadcast.
- **Not played.** Test first:
  1. **As the general, F1 in the normal view:**
     - the bar lists formations 0–7, and one stays selected (opening, number keys, Ctrl+number);
     - an AI formation obeys; the log shows "took over"; Alt shows "led by the general";
     - Delegate only for him, and it hands back.
  2. **General mode at maximum lift:** F1, flag placement (the 3000 m ray), Esc closing the bar first, Ctrl drawing.
  3. **With a second player:** suggestions (pop-up, sound, blue marker) only to that commander and the general; his formation is not moved.
  4. **Releases** on death, disconnect, slot change and team change. The next round is fully delegated. RBM regrouping, and "0 failed".
  5. **Server rgl log:** stock Select/Unselect from the general's client still hit `FailedAssert` ("not selectable") on the server, a few per click. Check the volume.
- **Known limitations:**
  - move-line widths come from the client's copy of the formation;
  - Ctrl is both the general-mode cursor key and the bar's multi-select / layout modifier, which is untested.

## Open items

- Play the checklist above.
- **Confirm the defaults with the user** (suggestion lifetime, RBM regrouping, pop-up).
- **The safety exit on a hit in general mode:** still awaiting the user's word.
- Commit when the user says so.

## Traps

- **`PlayerOwner` of another player's formation never reaches a client** (`AssignFormationToPlayer` goes to the owner only). On a client, every other formation reads `PlayerOwner == null`.
- **The `Formation.PlayerOwner` setter calls `SetControlledByAI(value == null)`.** Assign it only when it differs, or a local delegated state is undone.
- **Stock MP hides the Delegate (AI control) toggle** (`DefaultVisualOrderProvider`, `GameNetwork.IsMultiplayer ? null : ...`). A registered `VisualOrderProvider` can bring it back.
