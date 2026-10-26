# Handoff: layer-view base classes

Session of 2026-10-26, written 11:13. It follows `2026-10-26-1040-review-refactors-1-4.md` (same session). That handoff's work is committed as `8d62bca`. The user played it: joining, formation and role requests, and a map change all show up in the logs, with no exceptions.

## State

- **Not committed:**
  - the new `CommanderBattle.View/CommanderBattleLayerView.cs` and `RBM.Multiplayer.View/StanceClientView.cs`;
  - the six views that now use them: `TroopSelectionView.cs`, `CommanderBattleFormationSelectionView.cs`, `CommanderBattleAIStatusView.cs`, `CommanderBattleSideBannerView.cs`, `StanceHudView.cs`, `ExhaustionView.cs`;
  - this handoff.
- **Installed:** both sides, built at 11:12. The only warning is RBM's usual SYSLIB0032.
- **Running:** server pid 29532, map `battle_terrain_biome_088`. Patch line: "94 classes applied, 0 failed, 3 skipped". Both mod logs are clean.

## What was done

Review item "layer-view base classes". No behaviour change was intended. The six views lost 180 lines net, and the two base classes add about 150.

- **`CommanderBattleLayerView<TViewModel>`**, a `MissionView`:
  - `Client`, `Layer`, `DataSource` and `IsOpen`;
  - `OpenLayer(dataSource, movieName, layerOrder, captureMouse)` and `CloseLayer()`, which `OnMissionScreenFinalize` calls.
  - Users:
    - the troop selection, with `captureMouse`, at layer 25 and view priority 23 as before;
    - the formation list, with `captureMouse`;
    - the AI status and side banner, opened once as HUDs.
  - The formation list now looks up the troop view through one `TroopSelection` property, instead of three `GetMissionBehavior` calls.
- **`StanceClientView<TViewModel>`**, a `MissionLogic`:
  - It makes its layer once the mission screen is on top, and subscribes to `StanceNetworkClient.EventReceived`. It tears both down in `OnRemoveBehavior`.
  - Subclasses give the layer name, order and movie to its constructor, and override `CreateDataSource` and `OnStanceEvent`. `StanceHudView` and `ExhaustionView` use it.

### Small differences, believed harmless

- **Formation list:** it refreshes its view model just after the movie loads, instead of just before. Both happen in the same frame.
- **HUD layers** (AI status, side banner): `CloseLayer` resets their input restrictions before removing them. They never had any set. The reset only marks the restrictions empty and the mouse hidden on a layer that is being removed.
- **`ExhaustionView`:** it now finalizes its `BindingListFloatItem` (`ViewModel.OnFinalize`, a no-op). It reads `Agent.Main` in the tick instead of `Mission.MainAgent`; they return the same agent.

## Verified vs not

- **Verified:** it builds, the server starts with 0 failed patches, and the logs are clean.
- **Not verified in play:**
  - the troop screen and the formation list open and close: N, Esc (the troop screen first), Cancel, Confirm and the Troops button;
  - Left Alt shows the AI status panel and its markers;
  - the side banner shows on joining and at round start;
  - the posture bars and the crush-through messages appear;
  - the exhaustion darkening plays.

## Open items

The rest of the review list:
1. Merge `TroopRoleCardVM` and `TroopTypeVM`.
2. Make the constant VM texts getter-only, and merge the two troop step refreshes.
3. Helpers for the spawning `AgentBuildData` and the team colours.
4. A shared log source file.
5. Text helpers for the slot name, the pick description and the side name; `GetSideName` still lives in the banner view.
6. Clamp the network messages to their compression limits, and share the perk read/write.
7. Remove the dead stubs and the namespace placeholders.
8. One `CommanderLeadDistance` constant.
9. A shared csproj targets file.

`CLAUDE.md` still says armies of 500.
