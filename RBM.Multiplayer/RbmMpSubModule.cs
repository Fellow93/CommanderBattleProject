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

        // The client's multiplayer game (TaleWorlds.MountAndBlade.Multiplayer), by name like the above.
        private const string MultiplayerGameType = "MultiplayerGame";

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            RbmMpLog.Initialize();
            RbmMpSettings.Apply();
            RbmXmlMerge.Patch();
        }

        // Patched once the game is loaded (some RBM patch classes read game data when they are first touched) and
        // before any mission runs, so no target has been compiled into a caller yet. Clients don't run RBM's patches,
        // apart from RBM's weapon stats, which every machine must compute alike: the AI, the posture simulation and the
        // blows are the server's. They also predict their own shots at RBM's speeds, in RBM mode.
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (GameNetwork.IsDedicatedServer)
            {
                RbmMpPatcher.PatchAll();
            }
            else if (game.GameType?.GetType().Name == MultiplayerGameType)
            {
                RbmMpEquipWield.Patch();
                RbmMpClientMissileSpeed.Patch();
            }
        }

        // MultiplayerGame calls this once its XML is loaded and the unresolved objects are dropped; it never calls
        // OnAfterGameInitializationFinished.
        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            RbmItemSetCheck.Log();
            if (GameNetwork.IsDedicatedServer)
            {
                RbmMpPatcher.PatchAfterItemLoad();
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
                if (RBMConfig.RBMConfig.battleHitLoggingEnabled)
                {
                    mission.AddMissionBehavior(new RBMCombat.BattleHitLogic());
                }
                if (RBMConfig.RBMConfig.rbmCombatEnabled)
                {
                    mission.AddMissionBehavior(new RbmMpStuckMissiles());
                }
                RbmMpLog.Detail($"Mission {mission.SceneName}: RBM server behaviours added");
            }
            else if (GameNetwork.IsClient)
            {
                mission.AddMissionBehavior(new StanceNetworkClient());
                mission.AddMissionBehavior(new RbmMpStuckMissiles());
            }
        }
    }
}
