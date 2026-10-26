# The general orders formations

**Status:** in progress (v1 built 2026-10-26, not played) · **Size:** L

Follows `general-role.md` (phase 0, played) and `general-mode.md` (Ctrl+C map with drawn paths, played).

## Goal

The side's general (a player in slot 8) gives orders with the **stock order bar**, like any commander.
- **AI-led formations obey.** They start every round under the team AI ("delegate command"). An order from the general takes one over, and the stock Delegate order hands it back.
- **Player-led formations get a suggestion,** not an order: a pop-up and a marker on the map, shown to that commander and to the general only.

## Decisions (the user, 2026-10-26)

- **Input: the stock order bar:** F1–F9, number keys to select, the order flag. It works in the normal view and in general mode's lifted camera.
- **Only one formation can be ordered at a time.** There is no select-all and no multi-select (Ctrl+number) for the general.
- **The pop-up and Alt marker** are shown to that commander and the general only.
- **When the general dies, leaves, or stops being the general,** every formation he took over goes back to the AI at once.
- **Each round starts with every AI formation delegated.**
- **Defaults taken, since the user didn't choose; change them if he does:**
  - **Suggestion lifetime:** until the general replaces it, or the round ends.
  - **RBM regrouping:** a delegated formation is a normal AI formation, which RBM may merge. One the general controls is never merged.
  - **The pop-up:** the stock centre quick message with a short sound, also written to the message log.
  - **v1 suggestion kinds:** Move (flag or line, with facing), Charge (with or without a target), Hold (stand your ground), Follow (the general), Advance, Fall back, Retreat. Other orders to a player formation are not suggested, and the general is told once per bar opening.
  - **A listen-server host as general** is not supported. Commander Battle runs on a dedicated server.

## Design (from the code: the Opus planning report of 2026-10-26)

**Why not just give the general ownership on the server.**
- Server-side `PlayerOwner` = general for AI formations breaks bot scoring. `MissionLobbyComponent.OnBotKills`/`OnBotDies` look for a peer whose `ControlledFormation` is that formation.
- It also breaks the spectator card's peer (`GetCardPeer`) and the RBM guard, and it must follow every new agent of his.
- `Team.IsPlayerGeneral` isn't viable: every per-agent `OrderController` would take ownership of every formation.
- A Harmony patch on `IsFormationSelectable` risks inlining and needs Harmony in CommanderBattle.

**Chosen: our own command path.**
- **On the server, AI formations keep `PlayerOwner == null`.**
- **On the general's client only,** a local mirror sets `PlayerOwner = Agent.Main` on his team's formations 0–7, so the stock bar opens, lists them and lets him select (`MissionOrderVM.CheckCanBeOpened`, `OrderController.IsFormationSelectable`). It is restored to null when he stops being the general. Only when `GameNetwork.IsClient`.
- **His local `IsAIControlled`** follows the latest `SideAIStatus` leader, so the Delegate toggle shows the right state.
- **Single selection:** the client keeps at most one formation selected, the latest. On an automatic select-all at bar opening, it keeps one: the previously selected formation if still there, else the first in index order.
- **The client hooks his `PlayerOrderController.OnOrderIssued`.** Orders are applied locally first, which is cosmetic. For each order it sends `RequestGeneralCommand(orderType, formation index, payload)`.
  - Payload: position, direction, width + unit spacing, target formation (side + index), or agent.
  - The top-level `MoveToLineSegment*` events are skipped, because their per-formation sub-events (`Move`, `LookAtDirection`, `FormCustom`) carry the data.
  - The stock messages he still sends are ignored by the server: the formations aren't selectable there.
- **The server checks:**
  - the sender is the side's general slot commander;
  - his `ControlledAgent` is alive;
  - the round is in progress;
  - a rate limit;
  - positions inside the boundaries.
- **For an AI-led formation** (`PlayerOwner == null`):
  - unless the order is `AIControlOn`/`Off`, `SetControlledByAI(false)` first;
  - then apply through a per-side server `OrderController(Mission, team, null)`, created once per mission and cleared after each use: `SetOrder` / `WithPosition` / `WithFormation` (the target on the enemy team) / `WithAgent(FollowMe)`;
  - facing via `SetFacingOrder`, width via `SetFormOrder(FormOrderCustom)` plus spacing.
  - Logs: "took over" and "delegated".
- **For a player-led formation** (`PlayerOwner != null`): collect the suggestion within the tick (Move + facing + width become one), then `SendTo` the formation's commander and the general. Never a broadcast.
- **Delegate:** the stock `AIControlOn` toggle is hidden in MP (`DefaultVisualOrderProvider`). A `CommanderBattleVisualOrderProvider`, copying the stock layout, adds the `order_toggle_ai` toggle for the general only. It is registered in the View submodule.
- **Releasing:** `ReleaseGeneralCommand(side, reason)` calls `SetControlledByAI(true)` on every formation 0–7 with `PlayerOwner == null && !IsAIControlled`. It is hooked on:
  - his death (`OnAgentRemoved`);
  - `SetSlot` for the general slot (covers disconnects, team changes, claims and admin moves);
  - the round leaving InProgress.
  - State is cleared in `OnClearScene`. `Team.Reset` → `Formation.Reset` already makes every formation delegated each round, on server and clients.
- **RBM guard** (`RBM.Multiplayer/FormationOwnershipGuard.KeepCommandersSoldiers`): soldiers move only between formations that are both AI-led (`PlayerOwner == null && IsAIControlled`). So a formation the general controls keeps its men. `PlayerFormationIsNotAIOwned` is unchanged.
- **AI status:** `SideAIStatus.IsPlayerLed` becomes `Leader {AI, General, Player}`. The Alt panel shows "led by the general".
- **Suggestions on the client:**
  - the latest suggestion per (side, formation) is stored, with an `OnSuggestionsChanged` event;
  - a quick-message pop-up with a sound plus a log line ("General: Infantry 1, move here"); the general's echo reads "Suggested to <name> (Infantry 3): move here";
  - markers on Left Alt (and in general mode for the general);
  - cleared at round end.
- **The order UI for the general** (`CommanderBattleOrderUIHandler`):
  - no longer blocks F1–F9 for him;
  - in general mode, `SetDisplayDialog(true)` would stop the bar's `TickInput`, so the handler lifts the flag around `base.TickInput` when general mode owns it;
  - Esc closes the bar before general mode.
  - If the order flag's centre ray can't reach the ground at high lifts (`ScreenPointToWorldRay` is capped at the boundary radius), subclass `OrderTroopPlacer` with a longer ray.

## Files it touches

- **Shared:**
  - new `CommanderBattle/Network/RequestGeneralCommand.cs` and `GeneralSuggestion.cs`;
  - `SideAIStatus.cs`;
  - new `CommanderBattle/GeneralCommands.cs` (allow-list, suggestion kinds and texts).
- **Server:** new `MissionMultiplayerCommanderBattle.GeneralCommand.cs`; `MissionMultiplayerCommanderBattle.cs` (handler, release hooks, clearing); `CommanderBattleAIStatusSync.cs`.
- **Client logic:** `MissionMultiplayerCommanderBattleClient` made partial, plus new `...Client.GeneralCommand.cs` (mirror, single selection, order hook, suggestions).
- **View:** `CommanderBattleOrderUIHandler.cs`; new `CommanderBattleVisualOrderProvider.cs` (+ registration in `CommanderBattleViewSubModule.cs`); `CommanderBattleGeneralModeView.cs`; `CommanderBattleAIStatusView.cs` / `AIStatusVM.cs` / prefab (leader, suggestion markers).
- **RBM:** `RBM.Multiplayer/FormationOwnershipGuard.cs`.
- **Docs:** `README.md` and the handoffs.

## Done when

1. **As the general, F1 in the normal view:** the bar opens with formations 0–7. Exactly one is selected at a time. Ordering an AI formation moves it and it keeps the order. The server log shows "took over", and the Alt panel shows it led by the general.
2. **Delegate** appears only for the general and hands the formation back to the AI at once. Other commanders' bars are unchanged.
3. **In general mode,** F1 opens the bar, the flag follows the screen centre at high lifts, Esc closes the bar first, and Ctrl drawing still works.
4. **Releasing:** death, disconnect, a slot change and a team change put his formations back to the AI. Taking over a rider starts again with everything delegated.
5. **RBM:** a formation the general controls is never regrouped; a delegated one may be. The patch line reads "0 failed".
6. **Suggestions:** selecting a player formation and ordering it gives that commander (and the general) the pop-up and the Alt marker. Nobody else sees it, and the formation itself isn't ordered.
7. **Next round:** everything is delegated and the suggestions are gone. The scoreboard's bot row still counts kills and deaths. The server rgl log has no flood of "not selectable" asserts.

## As built (v1, 2026-10-26)

Built as designed above, with these details where the design left room or the code differed:

- **Facing and width orders on their own** (F-key "face this direction", the form-width cursor) carry a point, not a direction or width. The client sends the point for a facing order and the server turns it into a direction through its owner-less `OrderController` (`SetOrderWithPosition(LookAtDirection)`), because only the server knows an AI formation's order position. A form-width order is measured on the client, from the general to the flag as the stock does (`GetOrderFormCustomWidth` reads `Agent.Main`, which a server doesn't have), and sent as a width.
- **Unit spacing** travels as a change, not a value: what the move line did to the formation's spacing on the general's client (the client captures each formation's spacing in the mission tick, before the views give orders). A client's copy of an AI formation never sees the AI's arrangement changes, so an absolute value would reset them.
- **Move lines** keep the design: the top-level `MoveToLineSegment*` events are skipped, their per-formation sub-events are sent. The client's copy of the formation does the line simulation, so the width may differ a little from the server's (README "Known limitations").
- **The local leader** shown by Delegate follows the server's `SideAIStatus`, plus a 3 s local override after he orders a formation, so the toggle doesn't flicker back before the next status (sent within about a second).
- **Single selection** keeps the newest formation when exactly one was added; otherwise (a select-all) the one kept before if still selected, else the lowest index.
- **Not suggested:** the client decides (a player-led formation by the slot table) and doesn't send the order; the notice shows once per opening of the bar (reset while the bar is closed).
- **Pop-up:** `MBInformationManager.AddQuickInformation` (the global `GauntletGameNotification` layer, which multiplayer missions keep) plus a coloured message-log line, for the commander and the general; the sound (`event:/ui/notification/alert`, the chat's admin-message sound) plays for the commander only, so the general isn't buzzed by his own orders.
- **Markers** for every suggestion kind: the move's point, the charge or advance target (it follows the target), or above the suggested formation for hold, follow, fall back, retreat and a charge without target. The commander's label reads "General: move here", the general's "Infantry 3: move here".
- **Order flag at high lifts:** the flag itself finds the ground natively (`SceneView.ProjectedMousePositionOnGround`), but the troop placer's click and cursor use `TryGetScreenMiddleToWorldPosition`, whose ray `ScreenPointToWorldRay` cuts at the boundary radius. `CommanderBattleOrderTroopPlacer` (created in `CommanderBattleMissionViews` in place of `ViewCreator.CreateOrderTroopPlacerView`) casts 3,000 m while general mode is on.
- **Esc:** general mode's `OnEscape` passes Esc on while `Mission.IsOrderMenuOpen`, so the order handler (priority 19) closes the bar first.
- **Activity:** an accepted order marks the general active for the inactivity kick (`CommanderBattleActivityLogic.MarkActive`).
- **Rate limit:** 40 commands per general per one-second window.
- **The general's mode menu** lists "F1-F9: order bar" with its keys.
- **Releasing** needs no tracking: on the server a formation without `PlayerOwner` that the AI doesn't control is one the general took over.

## As built: naming and highlighting the selection (2026-10-26, not played)

The user asked for named cards and a clearer selected formation. It applies to every commander's bar, client only.
- **Card names:** "Cataphracts (Fellow)" above each card, the troops alone for an AI formation (the Alt labels' wording, `FormationNames`, a copy of `CommanderBattleAllyPlayerMarkersView.GetFormationLabel` that the two could share). `CommanderBattleOrderUIHandler` sets the stock `OrderTroopItemVM.FormationName` (the number until now, shown nowhere) when it differs; the number key stays beside the card.
- **Prefab:** `Module/GUI/Prefabs/Order/OrderTroopItem.xml`, Native's card plus the name above it and a gold glow behind the selected card. The same relative path makes the resource depot take it over Native's. The bar ("OrderBar" for `MissionOrderVM`) is compiled into Native's `AutoGenerated` dlls, card included, so the handler reloads it with `UIConfig.DoNotUseGeneratedPrefabs` on for that one load (RTS Camera turns it on for good); it checks every tick, since the stock handler reloads its movie compiled on a team change and an order type change.
- **Map frame:** the stock icons have no selected state. `CommanderBattleSelectedFormationView` lists the stock icons' own view models (`MissionGauntletFormationMarker._dataSource.Targets`, by reflection) of the `PlayerOrderController.SelectedFormations` while the bar is open, and draws a gold ring (the stock targeting ring sprite) with the stock marker widget and bindings, so it sits, scales and fades with the icon.
