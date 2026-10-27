using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // A side's general sounded a horn call. Sent to everyone: his side and spectators see the call, the enemy only
    // hears the horn from where he stands.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class HornSignal : GameNetworkMessage
    {
        private static readonly CompressionInfo.Integer KindCompressionInfo = new CompressionInfo.Integer(0, 2, maximumValueGiven: true);

        public HornSignal(BattleSideEnum side, HornSignalKind kind, Vec3 position)
        {
            Side = side;
            Kind = kind;
            Position = position;
        }

        public HornSignal()
        {
        }

        public BattleSideEnum Side { get; private set; }

        public HornSignalKind Kind { get; private set; }

        public Vec3 Position { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Side = (BattleSideEnum)ReadIntFromPacket(CompressionMission.TeamSideCompressionInfo, ref bufferReadValid);
            Kind = (HornSignalKind)ReadIntFromPacket(KindCompressionInfo, ref bufferReadValid);
            Position = ReadVec3FromPacket(CompressionBasic.PositionCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket((int)Side, CompressionMission.TeamSideCompressionInfo);
            WriteIntToPacket((int)Kind, KindCompressionInfo);
            WriteVec3ToPacket(Position, CompressionBasic.PositionCompressionInfo);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Orders;
        }

        protected override string OnGetLogFormat()
        {
            return $"Horn signal {Side}: {Kind}";
        }
    }
}
