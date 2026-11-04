using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    public class CommanderBattleSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            CommanderBattleLog.Initialize();
#if SERVER
            // Before any module's game start: RBM.Multiplayer patches RBM Combat there only in RBM mode.
            CombatModeSetting.LoadServerSetting();
            DisableIdleShutdown();
#else
            CampaignSceneLinker.LinkMissingScenes();
#endif
            Module.CurrentModule.AddMultiplayerGameMode(new CommanderBattleMultiplayerGameMode());
        }

#if SERVER
        // Without /playerhosteddedicatedserver (which v1.5.6 can't load), CustomBattleServer shuts the server down once
        // it has had no players for 10 minutes. Multiplayer's submodule has already called Connect, which sets the flag.
        private static void DisableIdleShutdown()
        {
            var server = NetworkMain.CustomBattleServer;
            var field = typeof(TaleWorlds.MountAndBlade.Diamond.CustomBattleServer).GetField("_useTimeoutTimer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (server == null || field == null)
            {
                CommanderBattleLog.Write("Idle shutdown not disabled: " + (server == null ? "no custom battle server" : "field _useTimeoutTimer not found"));
                return;
            }
            field.SetValue(server, false);
            CommanderBattleLog.Write("Idle shutdown disabled");
        }
#endif

        // Added after MultiplayerGame's own models, so it is the AgentStatCalculateModel the game uses (the last one
        // added wins). Server and clients both run this.
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (game.GameType is MultiplayerGame)
            {
                gameStarterObject.AddModel(new CommanderBattleAgentStatCalculateModel());
                CommanderBattleLog.Detail("Agent stats: Commander Battle model (horse speed x" + CommanderBattleSettings.HorseSpeedFactor
                    + ", bow fire speed x" + CommanderBattleSettings.BowFireSpeedFactor
                    + ", crossbow fire speed x" + CommanderBattleSettings.CrossbowFireSpeedFactor
                    + ", bow reload x" + CommanderBattleSettings.BowReloadSpeedFactor
                    + ", crossbow reload x" + CommanderBattleSettings.CrossbowReloadSpeedFactor + ")");
            }
        }
    }
}
