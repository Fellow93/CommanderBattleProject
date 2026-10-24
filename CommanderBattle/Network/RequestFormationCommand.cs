using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Client asks to take command of formation slot SlotIndex of its own side.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestFormationCommand : GameNetworkMessage
    {
        public RequestFormationCommand(int slotIndex)
        {
            SlotIndex = slotIndex;
        }

        public RequestFormationCommand()
        {
        }

        public int SlotIndex { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            SlotIndex = ReadIntFromPacket(CompressionMission.FormationClassCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(SlotIndex, CompressionMission.FormationClassCompressionInfo);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Formations;
        }

        protected override string OnGetLogFormat()
        {
            return "Request to command formation slot " + SlotIndex;
        }
    }
}
