using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // The host's combat mode (CombatModeSetting), sent to each player who joins, first of all: the class selection
    // screen previews the mode's loadouts, and RBM mode's client-side parts follow it.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class CombatModeInfo : GameNetworkMessage
    {
        public CombatModeInfo(CombatMode mode)
        {
            Mode = mode;
        }

        public CombatModeInfo()
        {
        }

        public CombatMode Mode { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Mode = ReadBoolFromPacket(ref bufferReadValid) ? CombatMode.Rbm : CombatMode.Light;
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteBoolToPacket(Mode == CombatMode.Rbm);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Mission;
        }

        protected override string OnGetLogFormat()
        {
            return $"Combat mode: {Mode}";
        }
    }
}
