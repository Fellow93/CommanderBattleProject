using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;

namespace CommanderBattle.View
{
    // The formation list. Opens when the player joins a side and on N; clicking a free formation asks the
    // server for command of it.
    public class CommanderBattleFormationSelectionView : MissionView
    {
        private const string MovieName = "CommanderBattleFormationSelection";

        private MissionMultiplayerCommanderBattleClient _client;

        private GauntletLayer _layer;

        private FormationSelectionVM _dataSource;

        private bool _openWhenReady;

        public CommanderBattleFormationSelectionView()
        {
            // Above the stock team selection (22) and class loadout.
            ViewOrderPriority = 24;
        }

        private BattleSideEnum MySide
        {
            get
            {
                MissionPeer myPeer = GameNetwork.IsMyPeerReady ? GameNetwork.MyPeer.GetComponent<MissionPeer>() : null;
                return myPeer?.Team?.Side ?? BattleSideEnum.None;
            }
        }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            _client = Mission.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>();
            _client.Slots.OnSlotChanged += OnSlotChanged;
            _client.OnAIStatusChanged += Refresh;
            MissionPeer.OnTeamChanged += OnTeamChanged;
        }

        public override void OnMissionScreenFinalize()
        {
            _client.Slots.OnSlotChanged -= OnSlotChanged;
            _client.OnAIStatusChanged -= Refresh;
            MissionPeer.OnTeamChanged -= OnTeamChanged;
            Close();
            base.OnMissionScreenFinalize();
        }

        private void OnTeamChanged(NetworkCommunicator peer, Team previousTeam, Team newTeam)
        {
            if (!peer.IsMine)
            {
                return;
            }
            if (newTeam != null && newTeam.Side != BattleSideEnum.None)
            {
                _openWhenReady = true;
            }
            else
            {
                Close();
            }
        }

        private void OnSlotChanged(FormationSlot slot)
        {
            Refresh();
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (MySide == BattleSideEnum.None)
            {
                return;
            }
            if (_openWhenReady)
            {
                _openWhenReady = false;
                Open();
            }
            else if (_layer == null && Input.IsKeyReleased(InputKey.N))
            {
                Open();
            }
        }

        public override bool OnEscape()
        {
            if (_layer != null)
            {
                Close();
                return true;
            }
            return base.OnEscape();
        }

        private void Open()
        {
            if (_layer != null)
            {
                Refresh();
                return;
            }
            _dataSource = new FormationSelectionVM(OnTake, Close);
            Refresh();
            _layer = new GauntletLayer(MovieName, ViewOrderPriority);
            _layer.LoadMovie(MovieName, _dataSource);
            _layer.InputRestrictions.SetInputRestrictions(isMouseVisible: true, InputUsageMask.Mouse);
            MissionScreen.AddLayer(_layer);
        }

        private void Close()
        {
            if (_layer == null)
            {
                return;
            }
            _layer.InputRestrictions.ResetInputRestrictions();
            MissionScreen.RemoveLayer(_layer);
            _layer = null;
            _dataSource.OnFinalize();
            _dataSource = null;
        }

        private void Refresh()
        {
            BattleSideEnum side = MySide;
            if (_dataSource != null && side != BattleSideEnum.None)
            {
                _dataSource.Refresh(_client.Slots, side, GameNetwork.MyPeer, _client.AIStatus);
            }
        }

        private void OnTake(int slotIndex)
        {
            _client.RequestFormationCommand(slotIndex);
        }
    }
}
