using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Server tells the players of one side that an order of their general is gone: deleted, pushed out by a newer
    // one, or the round ended.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class GeneralOrderRemoved : GameNetworkMessage
    {
        public GeneralOrderRemoved(int id)
        {
            Id = id;
        }

        public GeneralOrderRemoved()
        {
        }

        public int Id { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Id = ReadIntFromPacket(GeneralOrderPackets.IdCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(Id, GeneralOrderPackets.IdCompressionInfo);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Orders;
        }

        protected override string OnGetLogFormat()
        {
            return "General order " + Id + " removed";
        }
    }
}
