using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer.GauntletUI.Mission;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection.HUDExtensions;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace CommanderBattle.View
{
    // A dead commander who watches one of their own soldiers can take it over with the action key (the stock
    // RequestToSpawnAsBot; MissionMultiplayerCommanderBattle.CheckForCommandersTakingOverSoldiers takes the followed
    // soldier). The stock spectator card has the "Press F to take control" line for it, with the player's own key,
    // but turns it on only in the flag modes (MissionMultiplayerSpectatorHUDVM checks for their client). Turned on
    // here instead.
    public class CommanderBattleTakeOverHintView : MissionView
    {
        // The server takes the request only once the respawn period after a death is over
        // (SpawningBehaviorBase.GetMaximumReSpawnPeriodForPeer, which CommanderBattleSpawningBehavior keeps).
        private const float RespawnPeriod = 3f;

        private MissionMultiplayerCommanderBattleClient _client;

        private Agent _lastMainAgent;

        // When the player's agent died this round, or -1 while they have one (or haven't spawned).
        private float _diedAt = -1f;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            _client = Mission.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>();
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            _lastMainAgent = null;
            _diedAt = -1f;
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            Agent mainAgent = Mission.MainAgent;
            if (mainAgent != null)
            {
                _diedAt = -1f;
            }
            else if (_lastMainAgent != null)
            {
                _diedAt = Mission.CurrentTime;
            }
            _lastMainAgent = mainAgent;
            MissionMultiplayerSpectatorHUDVM spectator = Mission.GetMissionBehavior<MissionGauntletMultiplayerHUDExtension>()?.DataSource?.SpectatorControls;
            if (spectator != null)
            {
                // Set every tick: the stock card clears it whenever the camera moves to another soldier.
                spectator.CanTakeControlOfSpectatedAgent = CanTakeOverFollowedSoldier();
            }
        }

        private bool CanTakeOverFollowedSoldier()
        {
            if (_diedAt < 0f || Mission.CurrentTime - _diedAt < RespawnPeriod || _client == null || !_client.IsRoundInProgress || !GameNetwork.IsMyPeerReady)
            {
                return false;
            }
            MissionPeer me = GameNetwork.MyPeer.GetComponent<MissionPeer>();
            Agent followed = me?.FollowedAgent;
            // Not IsAIControlled: on a client it is always false (the AI runs on the server).
            return me.ControlledFormation != null && followed != null && followed.IsActive() && !followed.IsPlayerControlled && followed.Formation == me.ControlledFormation;
        }
    }
}
