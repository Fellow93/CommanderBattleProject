using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual;

namespace CommanderBattle.View
{
    // Loads this assembly on clients; the views are found through [ViewCreatorModule] in CommanderBattleMissionViews.
    // Also registers the general's order bar layout, after Native's (the factory asks the last registered first).
    public class CommanderBattleViewSubModule : MBSubModuleBase
    {
        private CommanderBattleVisualOrderProvider _visualOrderProvider;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            _visualOrderProvider = new CommanderBattleVisualOrderProvider();
            VisualOrderFactory.RegisterProvider(_visualOrderProvider);
        }

        protected override void OnSubModuleUnloaded()
        {
            if (_visualOrderProvider != null)
            {
                VisualOrderFactory.UnregisterProvider(_visualOrderProvider);
                _visualOrderProvider = null;
            }
            base.OnSubModuleUnloaded();
        }
    }
}
