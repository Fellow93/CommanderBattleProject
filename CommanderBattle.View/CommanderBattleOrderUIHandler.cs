using TaleWorlds.MountAndBlade;

namespace CommanderBattle.View
{
    // The stock multiplayer order UI, except for the side's general. He leads no formation of the line (his formation
    // 8 only follows him), so the stock bar never opens for him: F1 only popped "There isn't any unit under command"
    // (MissionOrderVM.CheckCanBeOpened). F1 opens general mode for him instead (CommanderBattleGeneralModeView).
    // Created directly in CommanderBattleMissionViews rather than through MultiplayerViewCreator, which would create
    // the stock handler.
    public class CommanderBattleOrderUIHandler : TaleWorlds.MountAndBlade.Multiplayer.GauntletUI.Mission.MissionGauntletMultiplayerOrderUIHandler
    {
        private MissionMultiplayerCommanderBattleClient _client;

        protected override void TickInput(float dt)
        {
            if (_client == null)
            {
                _client = Mission.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>();
            }
            if (_client?.MySlot?.IsGeneral == true)
            {
                return;
            }
            base.TickInput(dt);
        }
    }
}
