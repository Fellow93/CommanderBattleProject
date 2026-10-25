using RBM.Multiplayer.Stance;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer
{
    // RBM AI + RBM Stance in Commander Battle. The AI and the posture/stamina simulation run on the dedicated server
    // only; clients get the posture HUD from the server's messages (RBM.Multiplayer.View draws it).
    public class RbmMpSubModule : MBSubModuleBase
    {
        // Commander Battle's client half is in both its server and client mission (CommanderBattle/CommanderBattleMissions.cs).
        // Matched by name so this module does not depend on CommanderBattle.dll.
        private const string CommanderBattleBehaviorName = "MissionMultiplayerCommanderBattleClient";

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            RbmMpLog.Initialize();
            RbmMpSettings.Apply();
        }

        // Patched once the game is loaded (some RBM patch classes read game data when they are first touched) and
        // before any mission runs, so no target has been compiled into a caller yet. Pure clients never patch: the
        // AI and the posture simulation are the server's.
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (GameNetwork.IsDedicatedServer)
            {
                RbmMpPatcher.PatchAll();
            }
        }

        public static bool IsCommanderBattle(Mission mission)
        {
            foreach (MissionBehavior behavior in mission.MissionBehaviors)
            {
                if (behavior.GetType().Name == CommanderBattleBehaviorName)
                {
                    return true;
                }
            }
            return false;
        }

        public override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            base.OnBeforeMissionBehaviorInitialize(mission);
            if (GameNetwork.IsDedicatedServer)
            {
                RbmMpServerLogic.ResetRbmState("mission start");
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            if (!IsCommanderBattle(mission))
            {
                return;
            }
            if (GameNetwork.IsDedicatedServer)
            {
                mission.AddMissionBehavior(new RbmMpServerLogic());
                if (RBMConfig.RBMConfig.rbmAiEnabled)
                {
                    mission.AddMissionBehavior(new RBMAI.AgentPanicFix());
                    mission.AddMissionBehavior(new AI.ArcherCloseCombatLogic());
                }
                if (RBMConfig.RBMConfig.rbmStanceEnabled)
                {
                    // Always present with Stance on, as in RBM: the posture patches self-gate on postureEnabled.
                    mission.AddMissionBehavior(new RBMStance.StanceLogic());
                    mission.AddMissionBehavior(new StanceNetworkSync());
                }
                RbmMpLog.Write($"Mission {mission.SceneName}: RBM server behaviours added");
            }
            else if (GameNetwork.IsClient)
            {
                mission.AddMissionBehavior(new StanceNetworkClient());
            }
        }
    }
}
