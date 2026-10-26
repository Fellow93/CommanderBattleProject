using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle.View
{
    // The formation list. Opens when the player joins a side and on N; clicking a free formation asks the
    // server for command of it.
    public class CommanderBattleFormationSelectionView : CommanderBattleLayerView<FormationSelectionVM>
    {
        private const string MovieName = "CommanderBattleFormationSelection";

        private bool _openWhenReady;

        public CommanderBattleFormationSelectionView()
        {
            // Above the stock team selection (22); the troop selection (23) draws above it.
            ViewOrderPriority = 24;
        }

        private static BattleSideEnum MySide => CommanderBattleTeams.MySide;

        private CommanderBattleTroopSelectionView TroopSelection => Mission.GetMissionBehavior<CommanderBattleTroopSelectionView>();

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            Client.Slots.OnSlotChanged += OnSlotChanged;
            Client.OnAIStatusChanged += Refresh;
            MissionPeer.OnTeamChanged += OnTeamChanged;
        }

        public override void OnMissionScreenFinalize()
        {
            Client.Slots.OnSlotChanged -= OnSlotChanged;
            Client.OnAIStatusChanged -= Refresh;
            MissionPeer.OnTeamChanged -= OnTeamChanged;
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
                CloseLayer();
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
            else if (!IsOpen && Input.IsKeyReleased(InputKey.N))
            {
                Open();
            }
        }

        public override bool OnEscape()
        {
            // The troop selection, drawn on top, closes first.
            if (IsOpen && TroopSelection?.IsOpen != true)
            {
                CloseLayer();
                return true;
            }
            return base.OnEscape();
        }

        private void Open()
        {
            if (!IsOpen)
            {
                OpenLayer(new FormationSelectionVM(OnTake, OnTroops, CloseLayer), MovieName, ViewOrderPriority, captureMouse: true);
            }
            Refresh();
        }

        private void Refresh()
        {
            BattleSideEnum side = MySide;
            if (IsOpen && side != BattleSideEnum.None)
            {
                DataSource.Refresh(Client.Slots, side, GameNetwork.MyPeer, Client.AIStatus);
            }
        }

        // Taking a formation opens the troop selection once the server confirms it.
        private void OnTake(int slotIndex)
        {
            TroopSelection?.OpenWhenCommanding(slotIndex);
            Client.RequestFormationCommand(slotIndex);
        }

        // The troop selection opens on top of the list, which stays open beneath it, at the weight step for the
        // player's formation.
        private void OnTroops()
        {
            TroopSelection?.Open(TroopSelectionVM.StepWeight);
        }
    }
}
