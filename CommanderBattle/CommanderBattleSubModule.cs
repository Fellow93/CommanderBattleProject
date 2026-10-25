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
            Module.CurrentModule.AddMultiplayerGameMode(new CommanderBattleMultiplayerGameMode());
        }

        // Added after MultiplayerGame's own models, so it is the AgentStatCalculateModel the game uses (the last one
        // added wins). Server and clients both run this.
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (game.GameType is MultiplayerGame)
            {
                gameStarterObject.AddModel(new CommanderBattleAgentStatCalculateModel());
                CommanderBattleLog.Write("Agent stats: Commander Battle model (horse speed x" + CommanderBattleSettings.HorseSpeedFactor + ")");
            }
        }
    }
}
