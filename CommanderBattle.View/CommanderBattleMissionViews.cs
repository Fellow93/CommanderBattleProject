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
        // Stock multiplayer views plus the formation list. The flag marker handler is left out (no flags); the
        // order bar, formation markers and target selection are what commanders use to lead their troops.
        [ViewMethod(CommanderBattleSettings.MissionName)]
        public static MissionView[] OpenCommanderBattle(Mission mission)
        {
            return new List<MissionView>
            {
                MultiplayerViewCreator.CreateLobbyEquipmentUIHandler(),
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
                ViewCreator.CreateMissionFormationMarkerUIHandler(mission),
                MultiplayerViewCreator.CreateMultiplayerTeamSelectUIHandler(),
                new CommanderBattleFormationSelectionView(),
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
                ViewCreator.CreateMissionBoundaryCrossingView(),
                new MissionBoundaryWallView(),
                new SpectatorCameraView(),
                new MultiplayerSpectatorSilhouetteView()
            }.ToArray();
        }
    }
}
