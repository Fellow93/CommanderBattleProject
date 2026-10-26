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
                MultiplayerViewCreator.CreateMultiplayerMissionOrderUIHandler(mission),
                ViewCreator.CreateMissionAgentLabelUIHandler(mission),
                ViewCreator.CreateOrderTroopPlacerView((OrderController)null),
                new MissionFormationTargetSelectionHandler(),
                new CommanderBattleOrderCameraView(),
                ViewCreator.CreateMissionFormationMarkerUIHandler(mission),
                MultiplayerViewCreator.CreateMultiplayerTeamSelectUIHandler(),
                new CommanderBattleFormationSelectionView(),
                new CommanderBattleTroopSelectionView(),
                new CommanderBattleSideBannerView(),
                new CommanderBattleAIStatusView(),
                MultiplayerViewCreator.CreateMissionScoreBoardUIHandler(mission, isSingleTeam: false),
                MultiplayerViewCreator.CreateMultiplayerEndOfRoundUIHandler(),
                MultiplayerViewCreator.CreateMultiplayerEndOfBattleUIHandler(),
                MultiplayerViewCreator.CreatePollProgressUIHandler(),
                new MissionItemContourControllerView(),
                new MissionAgentContourControllerView(),
                MultiplayerViewCreator.CreateMultiplayerMissionHUDExtensionUIHandler(),
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
