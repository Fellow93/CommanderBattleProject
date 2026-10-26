using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Server tells the players of one side about an order their general drew (or, to a player who joins the side,
    // one already standing). An id the client already has is replaced.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class GeneralOrderAdded : GameNetworkMessage
    {
        public GeneralOrderAdded(GeneralOrder order)
        {
            Order = order;
        }

        public GeneralOrderAdded()
        {
        }

        public GeneralOrder Order { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            int id = ReadIntFromPacket(GeneralOrderPackets.IdCompressionInfo, ref bufferReadValid);
            BattleSideEnum side = (BattleSideEnum)ReadIntFromPacket(CompressionMission.TeamSideCompressionInfo, ref bufferReadValid);
            Vec2[] points = GeneralOrderPackets.ReadPoints(ref bufferReadValid);
            string label = ReadStringFromPacket(ref bufferReadValid);
            Order = new GeneralOrder(id, side, points, label);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(Order.Id, GeneralOrderPackets.IdCompressionInfo);
            WriteIntToPacket((int)Order.Side, CompressionMission.TeamSideCompressionInfo);
            GeneralOrderPackets.WritePoints(Order.Points);
            WriteStringToPacket(Order.Label ?? "");
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Orders;
        }

        protected override string OnGetLogFormat()
        {
            return $"General order {Order?.Id} added for {Order?.Side}: '{Order?.Label}', {Order?.Points?.Length} points";
        }
    }

    // The order messages' shared fields.
    internal static class GeneralOrderPackets
    {
        public static readonly CompressionInfo.Integer IdCompressionInfo = new CompressionInfo.Integer(0, GeneralOrders.MaxId, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer PointCountCompressionInfo = new CompressionInfo.Integer(0, GeneralOrders.MaxPoints, maximumValueGiven: true);

        public static Vec2[] ReadPoints(ref bool bufferReadValid)
        {
            int count = GameNetworkMessage.ReadIntFromPacket(PointCountCompressionInfo, ref bufferReadValid);
            if (!bufferReadValid || count < 0)
            {
                return new Vec2[0];
            }
            Vec2[] points = new Vec2[count];
            for (int i = 0; i < count && bufferReadValid; i++)
            {
                points[i] = GameNetworkMessage.ReadVec2FromPacket(CompressionBasic.PositionCompressionInfo, ref bufferReadValid);
            }
            return points;
        }

        public static void WritePoints(Vec2[] points)
        {
            int count = points == null ? 0 : (points.Length > GeneralOrders.MaxPoints ? GeneralOrders.MaxPoints : points.Length);
            GameNetworkMessage.WriteIntToPacket(count, PointCountCompressionInfo);
            for (int i = 0; i < count; i++)
            {
                GameNetworkMessage.WriteVec2ToPacket(points[i], CompressionBasic.PositionCompressionInfo);
            }
        }
    }
}
