using CommanderBattle.Network;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle
{
    // The general's horn calls (docs/plans/horn-signals.md). The stock bark wheel is the input: his Attack, Defend and
    // Regroup barks sound Advance, Hold and Fall back for the whole field. The stock handler (MissionNetworkComponent)
    // has already made his voice; this one runs after it.
    public partial class MissionMultiplayerCommanderBattle
    {
        private readonly float[] _lastHornTimes = { float.MinValue, float.MinValue };

        // Always true: a false return stops the handlers after this one and marks the packet as invalid.
        private bool HandleClientEventBarkSelected(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            NetworkMessages.FromClient.BarkSelected bark = (NetworkMessages.FromClient.BarkSelected)baseMessage;
            if (!HornSignals.TryGetKindOfBark(bark.IndexOfBark, out HornSignalKind kind) || !RoundController.IsRoundInProgress)
            {
                return true;
            }
            FormationSlot slot = Slots.FindSlotOf(peer);
            Agent general = peer.ControlledAgent;
            if (slot == null || !slot.IsGeneral || general == null || !general.IsActive() || general.Team == null || general.Team.Side != slot.Side)
            {
                return true;
            }
            int sideIndex = slot.Side == BattleSideEnum.Attacker ? 0 : 1;
            float now = Mission.CurrentTime;
            if (now - _lastHornTimes[sideIndex] < HornSignals.Cooldown)
            {
                return true;
            }
            _lastHornTimes[sideIndex] = now;
            CommanderBattleNetwork.Broadcast(new HornSignal(slot.Side, kind, general.Position));
            CommanderBattleLog.Write($"{slot.Side} general ({peer.UserName}) sounded {kind}");
            return true;
        }

        private void ClearHorns()
        {
            _lastHornTimes[0] = float.MinValue;
            _lastHornTimes[1] = float.MinValue;
        }
    }
}
