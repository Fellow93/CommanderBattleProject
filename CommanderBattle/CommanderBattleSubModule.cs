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
    }
}
