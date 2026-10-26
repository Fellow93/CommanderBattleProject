using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // The side's general asks to delete one of his orders.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestDeleteGeneralOrder : GameNetworkMessage
    {
        public RequestDeleteGeneralOrder(int id)
        {
            Id = id;
        }

        public RequestDeleteGeneralOrder()
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
            return "Request to delete general order " + Id;
        }
    }
}
