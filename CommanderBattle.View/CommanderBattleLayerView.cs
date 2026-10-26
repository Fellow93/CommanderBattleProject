using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace CommanderBattle.View
{
    // A mission view with one Gauntlet layer of its own, on the mission screen: a menu opened and closed on demand,
    // or a HUD opened once and kept. The layer is removed when the mission screen goes.
    public abstract class CommanderBattleLayerView<TViewModel> : MissionView where TViewModel : ViewModel
    {
        protected MissionMultiplayerCommanderBattleClient Client { get; private set; }

        protected GauntletLayer Layer { get; private set; }

        protected TViewModel DataSource { get; private set; }

        public bool IsOpen => Layer != null;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            Client = Mission.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>();
        }

        public override void OnMissionScreenFinalize()
        {
            CloseLayer();
            base.OnMissionScreenFinalize();
        }

        // captureMouse: a menu, which shows the cursor and takes the mouse; a HUD leaves both to the game.
        protected void OpenLayer(TViewModel dataSource, string movieName, int layerOrder, bool captureMouse)
        {
            CloseLayer();
            DataSource = dataSource;
            Layer = new GauntletLayer(movieName, layerOrder);
            Layer.LoadMovie(movieName, dataSource);
            if (captureMouse)
            {
                Layer.InputRestrictions.SetInputRestrictions(isMouseVisible: true, InputUsageMask.Mouse);
            }
            MissionScreen.AddLayer(Layer);
        }

        protected void CloseLayer()
        {
            if (Layer == null)
            {
                return;
            }
            Layer.InputRestrictions.ResetInputRestrictions();
            MissionScreen.RemoveLayer(Layer);
            Layer = null;
            DataSource.OnFinalize();
            DataSource = null;
        }
    }
}
