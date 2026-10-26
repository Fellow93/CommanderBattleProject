using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // The side's general asks to put a path order on the map: up to GeneralOrders.MaxPoints ground points and a
    // label. The server checks the sender, the points and the label (MissionMultiplayerCommanderBattle.GeneralOrders).
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestGeneralOrder : GameNetworkMessage
    {
        public RequestGeneralOrder(Vec2[] points, string label)
        {
            Points = points;
            Label = label;
        }

        public RequestGeneralOrder()
        {
        }

        public Vec2[] Points { get; private set; }

        public string Label { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Points = GeneralOrderPackets.ReadPoints(ref bufferReadValid);
            Label = ReadStringFromPacket(ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            GeneralOrderPackets.WritePoints(Points);
            WriteStringToPacket(Label ?? "");
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Orders;
        }

        protected override string OnGetLogFormat()
        {
            return $"Request general order '{Label}', {Points?.Length} points";
        }
    }
}
