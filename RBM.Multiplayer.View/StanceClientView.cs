using RBM.Multiplayer.Network;
using RBM.Multiplayer.Stance;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace RBM.Multiplayer.View
{
    // A client view drawn from the server's stance messages (StanceNetworkClient), on a layer of its own on the
    // mission screen. The mission screen may not be on top yet when the mission starts, so the layer is made on the
    // first tick it is; DataSource stays null until then.
    public abstract class StanceClientView<TViewModel> : MissionLogic where TViewModel : ViewModel
    {
        private readonly string _layerName;

        private readonly int _layerOrder;

        private readonly string _movieName;

        private MissionScreen _missionScreen;

        private GauntletLayer _layer;

        protected StanceClientView(string layerName, int layerOrder, string movieName)
        {
            _layerName = layerName;
            _layerOrder = layerOrder;
            _movieName = movieName;
        }

        protected StanceNetworkClient Client { get; private set; }

        protected TViewModel DataSource { get; private set; }

        protected abstract TViewModel CreateDataSource();

        protected virtual void OnStanceEvent(StanceEvent stanceEvent)
        {
        }

        public override void AfterStart()
        {
            base.AfterStart();
            Client = Mission.GetMissionBehavior<StanceNetworkClient>();
            if (Client != null)
            {
                Client.EventReceived += OnStanceEvent;
            }
            TryCreateLayer();
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            TryCreateLayer();
        }

        public override void OnRemoveBehavior()
        {
            if (Client != null)
            {
                Client.EventReceived -= OnStanceEvent;
            }
            if (_missionScreen != null && _layer != null)
            {
                _missionScreen.RemoveLayer(_layer);
            }
            _layer = null;
            _missionScreen = null;
            DataSource?.OnFinalize();
            DataSource = null;
            base.OnRemoveBehavior();
        }

        private void TryCreateLayer()
        {
            if (_layer != null || !(ScreenManager.TopScreen is MissionScreen missionScreen))
            {
                return;
            }
            _missionScreen = missionScreen;
            DataSource = CreateDataSource();
            _layer = new GauntletLayer(_layerName, _layerOrder);
            _missionScreen.AddLayer(_layer);
            _layer.LoadMovie(_movieName, DataSource);
        }
    }
}
