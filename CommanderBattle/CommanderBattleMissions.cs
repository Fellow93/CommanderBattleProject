using CommanderBattle.AI;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer;
using TaleWorlds.MountAndBlade.Multiplayer.Missions;
using TaleWorlds.MountAndBlade.Source.Missions;

namespace CommanderBattle
{
    public static class CommanderBattleMissions
    {
        // Everything except the game mode, its client counterpart and the spawning behaviour is a stock
        // multiplayer component, so lobby, rounds, polls, admin panel and scoreboard behave as usual.
        // No warmup: the stock warmup ends the match unless both sides have a human player, while here the
        // AI leads every formation nobody commands. Each round's preparation phase is the time to pick.
        [MissionMethod]
        public static void OpenCommanderBattleMission(string scene)
        {
            CommanderBattleLog.Write("Opening mission on " + scene);
            MissionState.OpenNew(CommanderBattleSettings.MissionName, new MissionInitializerRecord(scene), (Mission missionController) => GameNetwork.IsServer
                ? new MissionBehavior[]
                {
                    MissionLobbyComponent.CreateBehavior(),
                    new MissionMultiplayerCommanderBattle(),
                    new MultiplayerRoundController(),
                    new MissionMultiplayerCommanderBattleClient(),
                    new MultiplayerTimerComponent(),
                    new MultiplayerBattleMissionAgentInteractionLogic(),
#if !SERVER
                    // Listen server (hosting from the game client); the dedicated server has no visuals.
                    new MultiplayerMissionAgentVisualSpawnComponent(),
                    new ConsoleMatchStartEndHandler(),
#endif
                    new SpawnComponent(new CommanderBattleSpawnFrameBehavior(), new CommanderBattleSpawningBehavior()),
                    // Read by the singleplayer formation behaviours the team AI uses.
                    new CasualtyHandler(),
                    new CommanderBattlePowerLogic(),
                    new MissionLobbyEquipmentNetworkComponent(),
                    new MultiplayerTeamSelectComponent(),
                    new MissionHardBorderPlacer(),
                    new MissionBoundaryPlacer(),
                    new AgentVictoryLogic(),
                    new AgentHumanAILogic(),
                    new MissionAgentPanicHandler(),
                    new CommanderBattleFleeingLogic(),
                    new CommanderBattleAIStatusSync(),
                    new CommanderBattleActivityLogic(),
                    new MissionBoundaryCrossingHandler(),
                    new MultiplayerPollComponent(),
                    new MultiplayerAdminComponent(),
                    new MultiplayerGameNotificationsComponent(),
                    new MissionOptionsComponent(),
                    new MissionScoreboardComponent(new CommanderBattleScoreboardData()),
                    new EquipmentControllerLeaveLogic(),
                    new MultiplayerPreloadHelper()
                }
                : new MissionBehavior[]
                {
                    MissionLobbyComponent.CreateBehavior(),
                    new MissionMultiplayerCommanderBattleClient(),
                    new MultiplayerRoundComponent(),
                    new MultiplayerTimerComponent(),
                    new MultiplayerBattleMissionAgentInteractionLogic(),
#if !SERVER
                    new MultiplayerMissionAgentVisualSpawnComponent(),
                    new ConsoleMatchStartEndHandler(),
#endif
                    new MissionLobbyEquipmentNetworkComponent(),
                    new MultiplayerTeamSelectComponent(),
                    new MissionHardBorderPlacer(),
                    new MissionBoundaryPlacer(),
                    new MissionBoundaryCrossingHandler(),
                    new MultiplayerPollComponent(),
                    new MultiplayerAdminComponent(),
                    new MultiplayerGameNotificationsComponent(),
                    new MissionOptionsComponent(),
                    new MissionScoreboardComponent(new CommanderBattleScoreboardData()),
                    new EquipmentControllerLeaveLogic(),
                    new MissionRecentPlayersComponent(),
                    new MultiplayerPreloadHelper()
                });
        }
    }
}
