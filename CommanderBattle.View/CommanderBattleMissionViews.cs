using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer.View.MissionViews;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.MissionViews.Order;
using TaleWorlds.MountAndBlade.View.MissionViews.Singleplayer;

namespace CommanderBattle.View
{
    [ViewCreatorModule]
    public class CommanderBattleMissionViews
    {
        // Stock multiplayer views plus the formation list and the troop selection. The flag marker handler is left
        // out (no flags); the order bar, formation markers, target selection and the lifted order camera are what
        // commanders use to lead their troops. The stock class loadout (CreateLobbyEquipmentUIHandler) is left out
        // too: Commander Battle's troop selection replaces it. Spawning doesn't need it: the TeamInitialPerkInfo
        // handshake the spawner waits on is sent by MissionPeer itself on a culture change and handled by
        // MissionLobbyEquipmentNetworkComponent, a mission behaviour, and the loadout only opened once the
        // preparation visuals had spawned.
        [ViewMethod(CommanderBattleSettings.MissionName)]
        public static MissionView[] OpenCommanderBattle(Mission mission)
        {
            return new List<MissionView>
            {
                MultiplayerViewCreator.CreateMissionServerStatusUIHandler(),
                MultiplayerViewCreator.CreateMultiplayerFactionBanVoteUIHandler(),
                MultiplayerViewCreator.CreateMissionMultiplayerPreloadView(mission),
                MultiplayerViewCreator.CreateMissionKillNotificationUIHandler(),
                ViewCreator.CreateMissionAgentStatusUIHandler(mission),
                ViewCreator.CreateMissionMainAgentEquipmentController(mission),
                ViewCreator.CreateMissionMainAgentCheerBarkControllerView(mission),
                MultiplayerViewCreator.CreateMissionMultiplayerEscapeMenu("Captain"),
                // The stock handler (MissionGauntletMultiplayerOrderUIHandler), which also takes input in general mode.
                new CommanderBattleOrderUIHandler(),
                ViewCreator.CreateMissionAgentLabelUIHandler(mission),
                // The stock OrderTroopPlacer (GetMissionBehavior<OrderTroopPlacer> finds it), with a longer ray in
                // general mode.
                new CommanderBattleOrderTroopPlacer(),
                new MissionFormationTargetSelectionHandler(),
                new CommanderBattleOrderCameraView(),
                ViewCreator.CreateMissionFormationMarkerUIHandler(mission),
                // A gold frame around the stock icons of the formations selected in the order bar.
                new CommanderBattleSelectedFormationView(),
                MultiplayerViewCreator.CreateMultiplayerTeamSelectUIHandler(),
                new CommanderBattleFormationSelectionView(),
                new CommanderBattleTroopSelectionView(),
                new CommanderBattleSideBannerView(),
                new CommanderBattleAIStatusView(),
                // Allied players within 100 m on Alt, in place of the stock marker handler's unlimited range.
                new CommanderBattleAllyPlayerMarkersView(),
                // The general's Ctrl+C map: camera, drawn orders, and the orders shown to his side.
                new CommanderBattleGeneralModeView(),
                MultiplayerViewCreator.CreateMissionScoreBoardUIHandler(mission, isSingleTeam: false),
                MultiplayerViewCreator.CreateMultiplayerEndOfRoundUIHandler(),
                MultiplayerViewCreator.CreateMultiplayerEndOfBattleUIHandler(),
                MultiplayerViewCreator.CreatePollProgressUIHandler(),
                new MissionItemContourControllerView(),
                new MissionAgentContourControllerView(),
                MultiplayerViewCreator.CreateMultiplayerMissionHUDExtensionUIHandler(),
                // Turns on the spectator card's take-control line, which the HUD extension above draws.
                new CommanderBattleTakeOverHintView(),
                MultiplayerViewCreator.CreateMultiplayerMissionDeathCardUIHandler(),
                ViewCreator.CreateOptionsUIHandler(),
                ViewCreator.CreateMissionMainAgentEquipDropView(mission),
                MultiplayerViewCreator.CreateMultiplayerAdminPanelUIHandler(),
                // After the admin panel, which it adds its Commander Battle group to.
                new CommanderBattleAdminPanelView(),
                ViewCreator.CreateMissionBoundaryCrossingView(),
                new MissionBoundaryWallView(),
                new SpectatorCameraView(),
                new MultiplayerSpectatorSilhouetteView()
            }.ToArray();
        }
    }
}
