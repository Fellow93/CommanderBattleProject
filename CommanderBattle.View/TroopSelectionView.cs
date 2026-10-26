using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace CommanderBattle.View
{
    // The troop selection screen, in three steps: formation class (taking a free formation of it, RequestRoleCommand),
    // weight, weapon setup; Confirm sends RequestTroopSelection. Replaces the stock class loadout. Opens:
    // - at step 1 when the player joins a side. The server has already put them in a free formation (PlaceQueuedPeers),
    //   so the screen shows it as theirs and they can keep it or switch class;
    // - at step 2 when they take a formation from the formation list, once the server confirms it;
    // - at step 2 from the list's "Troops" button on their own row.
    // Esc or Cancel closes it.
    public class CommanderBattleTroopSelectionView : MissionView
    {
        private const string MovieName = "CommanderBattleTroopSelection";

        // Drawn above the formation list (24), which stays open beneath it.
        private const int LayerOrder = 25;

        // How long a take from the formation list may wait for the server before the screen stops waiting for it
        // (a refused take changes no slot).
        private const float OpenWhenCommandingTimeout = 3f;

        private MissionMultiplayerCommanderBattleClient _client;

        private GauntletLayer _layer;

        private TroopSelectionVM _dataSource;

        // The side the screen is for; it closes if the player leaves it.
        private BattleSideEnum _side = BattleSideEnum.None;

        // Slot index the player asked to command from the formation list: the screen opens once it is theirs.
        private int _openWhenCommanding = -1;

        private float _openWhenCommandingTimer;

        private bool _openOnJoin;

        private bool _lastApplyNow;

        public CommanderBattleTroopSelectionView()
        {
            // Below the formation list (24): MissionScreen offers Esc to views in ascending priority, so this screen,
            // drawn on top, closes first.
            ViewOrderPriority = 23;
        }

        public bool IsOpen => _layer != null;

        private static BattleSideEnum MySide => CommanderBattleTeams.MySide;

        private FormationSlot MySlot => _client.MySlot;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            _client = Mission.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>();
            _client.Slots.OnSlotChanged += OnSlotChanged;
            MissionPeer.OnTeamChanged += OnTeamChanged;
        }

        public override void OnMissionScreenFinalize()
        {
            _client.Slots.OnSlotChanged -= OnSlotChanged;
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
            Close();
            _openWhenCommanding = -1;
            _openOnJoin = newTeam != null && newTeam.Side != BattleSideEnum.None;
        }

        // Called by the formation list when the player asks for a formation.
        public void OpenWhenCommanding(int slotIndex)
        {
            _openWhenCommanding = slotIndex;
            _openWhenCommandingTimer = OpenWhenCommandingTimeout;
        }

        // step: TroopSelectionVM.StepRole, or StepWeight for the formation the player commands (falls back to
        // StepRole when they lead none).
        public void Open(int step)
        {
            BattleSideEnum side = MySide;
            if (side == BattleSideEnum.None)
            {
                return;
            }
            if (_layer != null)
            {
                Close();
            }
            _side = side;
            _dataSource = new TroopSelectionVM(OnRequestRole, OnConfirm, Close);
            _lastApplyNow = _client.CanTroopSelectionApplyNow;
            _dataSource.RefreshStatus(_lastApplyNow);
            _dataSource.Initialize(_client.Slots, side, MySlot, step);
            _layer = new GauntletLayer(MovieName, LayerOrder);
            _layer.LoadMovie(MovieName, _dataSource);
            _layer.InputRestrictions.SetInputRestrictions(isMouseVisible: true, InputUsageMask.Mouse);
            MissionScreen.AddLayer(_layer);
        }

        public void Close()
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
            _side = BattleSideEnum.None;
        }

        private void OnSlotChanged(FormationSlot slot)
        {
            FormationSlot mySlot = MySlot;
            if (_openWhenCommanding >= 0 && mySlot != null && mySlot.Index == _openWhenCommanding)
            {
                _openWhenCommanding = -1;
                if (_layer == null || _dataSource.Step == TroopSelectionVM.StepRole)
                {
                    Open(TroopSelectionVM.StepWeight);
                    return;
                }
            }
            if (_layer != null && slot.Side == _side)
            {
                _dataSource.OnSlotsChanged(mySlot);
            }
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (_openWhenCommanding >= 0)
            {
                _openWhenCommandingTimer -= dt;
                if (_openWhenCommandingTimer <= 0f)
                {
                    _openWhenCommanding = -1;
                }
            }
            if (_openOnJoin && MySide != BattleSideEnum.None)
            {
                _openOnJoin = false;
                Open(TroopSelectionVM.StepRole);
            }
            if (_layer == null)
            {
                return;
            }
            if (MySide != _side)
            {
                Close();
                return;
            }
            _dataSource.Tick(dt);
            bool applyNow = _client.CanTroopSelectionApplyNow;
            if (applyNow != _lastApplyNow)
            {
                _lastApplyNow = applyNow;
                _dataSource.RefreshStatus(applyNow);
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

        private void OnRequestRole(CommanderRole role)
        {
            _client.RequestRoleCommand(role);
        }

        private void OnConfirm(int classIndex, int weaponIndex)
        {
            _client.RequestTroopSelection(classIndex, CommanderRoles.MakePerks(weaponIndex));
            Close();
        }
    }
}
