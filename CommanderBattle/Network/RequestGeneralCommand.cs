using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // The side's general gave one of his formations an order with the order bar (docs/plans/general-orders.md). The
    // server applies it to a formation the AI leads, or suggests it to the commander of a player-led one
    // (MissionMultiplayerCommanderBattle.GeneralCommand). One message per order and formation.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestGeneralCommand : GameNetworkMessage
    {
        private static readonly CompressionInfo.Integer FormationIndexCompressionInfo = new CompressionInfo.Integer(0, 7, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer PayloadCompressionInfo = new CompressionInfo.Integer(0, 5, maximumValueGiven: true);

        private static readonly CompressionInfo.Float WidthCompressionInfo = new CompressionInfo.Float(0f, GeneralCommands.MaxWidth, 16);

        private static readonly CompressionInfo.Integer SpacingChangeCompressionInfo = new CompressionInfo.Integer(-GeneralCommands.MaxSpacingChange, GeneralCommands.MaxSpacingChange, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer TargetIndexCompressionInfo = new CompressionInfo.Integer(0, 9, maximumValueGiven: true);

        public RequestGeneralCommand(OrderType orderType, int formationIndex)
        {
            OrderType = orderType;
            FormationIndex = formationIndex;
            Payload = GeneralCommandPayload.None;
        }

        public RequestGeneralCommand()
        {
        }

        public OrderType OrderType { get; private set; }

        // The general's team's formation, 0-7 (the order bar's).
        public int FormationIndex { get; private set; }

        public GeneralCommandPayload Payload { get; private set; }

        public Vec3 Position { get; private set; }

        public Vec2 Direction { get; private set; }

        public float Width { get; private set; }

        // How much the move line changed the formation's unit spacing on the general's client (0: kept).
        public int SpacingChange { get; private set; }

        public BattleSideEnum TargetSide { get; private set; }

        public int TargetFormationIndex { get; private set; }

        public int AgentIndex { get; private set; }

        public RequestGeneralCommand WithPosition(Vec3 position)
        {
            Payload = GeneralCommandPayload.Position;
            Position = position;
            return this;
        }

        public RequestGeneralCommand WithDirection(Vec2 direction)
        {
            Payload = GeneralCommandPayload.Direction;
            Direction = direction;
            return this;
        }

        public RequestGeneralCommand WithWidth(float width, int spacingChange)
        {
            Payload = GeneralCommandPayload.Width;
            Width = width < 0f ? 0f : (width > GeneralCommands.MaxWidth ? GeneralCommands.MaxWidth : width);
            SpacingChange = spacingChange < -GeneralCommands.MaxSpacingChange ? -GeneralCommands.MaxSpacingChange : (spacingChange > GeneralCommands.MaxSpacingChange ? GeneralCommands.MaxSpacingChange : spacingChange);
            return this;
        }

        public RequestGeneralCommand WithFormation(BattleSideEnum side, int formationIndex)
        {
            Payload = GeneralCommandPayload.Formation;
            TargetSide = side;
            TargetFormationIndex = formationIndex;
            return this;
        }

        public RequestGeneralCommand WithAgent(int agentIndex)
        {
            Payload = GeneralCommandPayload.Agent;
            AgentIndex = agentIndex;
            return this;
        }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            OrderType = (OrderType)ReadIntFromPacket(CompressionMission.OrderTypeCompressionInfo, ref bufferReadValid);
            FormationIndex = ReadIntFromPacket(FormationIndexCompressionInfo, ref bufferReadValid);
            Payload = (GeneralCommandPayload)ReadIntFromPacket(PayloadCompressionInfo, ref bufferReadValid);
            switch (Payload)
            {
                case GeneralCommandPayload.Position:
                    Position = ReadVec3FromPacket(CompressionMission.OrderPositionCompressionInfo, ref bufferReadValid);
                    break;
                case GeneralCommandPayload.Direction:
                    Direction = ReadVec2FromPacket(CompressionBasic.UnitVectorCompressionInfo, ref bufferReadValid);
                    break;
                case GeneralCommandPayload.Width:
                    Width = ReadFloatFromPacket(WidthCompressionInfo, ref bufferReadValid);
                    SpacingChange = ReadIntFromPacket(SpacingChangeCompressionInfo, ref bufferReadValid);
                    break;
                case GeneralCommandPayload.Formation:
                    TargetSide = (BattleSideEnum)ReadIntFromPacket(CompressionMission.TeamSideCompressionInfo, ref bufferReadValid);
                    TargetFormationIndex = ReadIntFromPacket(TargetIndexCompressionInfo, ref bufferReadValid);
                    break;
                case GeneralCommandPayload.Agent:
                    AgentIndex = ReadAgentIndexFromPacket(ref bufferReadValid);
                    break;
            }
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket((int)OrderType, CompressionMission.OrderTypeCompressionInfo);
            WriteIntToPacket(FormationIndex, FormationIndexCompressionInfo);
            WriteIntToPacket((int)Payload, PayloadCompressionInfo);
            switch (Payload)
            {
                case GeneralCommandPayload.Position:
                    WriteVec3ToPacket(Position, CompressionMission.OrderPositionCompressionInfo);
                    break;
                case GeneralCommandPayload.Direction:
                    WriteVec2ToPacket(Direction, CompressionBasic.UnitVectorCompressionInfo);
                    break;
                case GeneralCommandPayload.Width:
                    WriteFloatToPacket(Width, WidthCompressionInfo);
                    WriteIntToPacket(SpacingChange, SpacingChangeCompressionInfo);
                    break;
                case GeneralCommandPayload.Formation:
                    WriteIntToPacket((int)TargetSide, CompressionMission.TeamSideCompressionInfo);
                    WriteIntToPacket(TargetFormationIndex, TargetIndexCompressionInfo);
                    break;
                case GeneralCommandPayload.Agent:
                    WriteAgentIndexToPacket(AgentIndex);
                    break;
            }
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Orders;
        }

        protected override string OnGetLogFormat()
        {
            return $"Request general command {OrderType} ({Payload}) for formation {FormationIndex}";
        }
    }
}
