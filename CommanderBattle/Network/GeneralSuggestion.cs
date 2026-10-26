using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // The general ordered a formation a player leads: the server tells that commander and the general, never anyone
    // else. The formation itself is not ordered. A move line's move, facing and width arrive as one suggestion.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class GeneralSuggestion : GameNetworkMessage
    {
        private static readonly CompressionInfo.Integer FormationIndexCompressionInfo = new CompressionInfo.Integer(0, 7, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer KindCompressionInfo = new CompressionInfo.Integer(0, 6, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer TargetIndexCompressionInfo = new CompressionInfo.Integer(0, 9, maximumValueGiven: true);

        private static readonly CompressionInfo.Float WidthCompressionInfo = new CompressionInfo.Float(0f, GeneralCommands.MaxWidth, 16);

        public GeneralSuggestion(BattleSideEnum side, int formationIndex, GeneralSuggestionKind kind)
        {
            Side = side;
            FormationIndex = formationIndex;
            Kind = kind;
        }

        public GeneralSuggestion()
        {
        }

        public BattleSideEnum Side { get; private set; }

        public int FormationIndex { get; private set; }

        public GeneralSuggestionKind Kind { get; private set; }

        // Where to move (a move).
        public bool HasPosition { get; private set; }

        public Vec2 Position { get; private set; }

        // Which way to face at the end of a move line.
        public bool HasDirection { get; private set; }

        public Vec2 Direction { get; private set; }

        // How wide to stand at the end of a move line; 0 when not given.
        public float Width { get; private set; }

        // The formation to charge or advance on.
        public bool HasTarget { get; private set; }

        public BattleSideEnum TargetSide { get; private set; }

        public int TargetFormationIndex { get; private set; }

        public void SetPosition(Vec2 position)
        {
            HasPosition = true;
            Position = position;
        }

        public void SetDirection(Vec2 direction)
        {
            HasDirection = true;
            Direction = direction;
        }

        public void SetWidth(float width)
        {
            Width = width < 0f ? 0f : (width > GeneralCommands.MaxWidth ? GeneralCommands.MaxWidth : width);
        }

        public void SetTarget(BattleSideEnum side, int formationIndex)
        {
            HasTarget = true;
            TargetSide = side;
            TargetFormationIndex = formationIndex;
        }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Side = (BattleSideEnum)ReadIntFromPacket(CompressionMission.TeamSideCompressionInfo, ref bufferReadValid);
            FormationIndex = ReadIntFromPacket(FormationIndexCompressionInfo, ref bufferReadValid);
            Kind = (GeneralSuggestionKind)ReadIntFromPacket(KindCompressionInfo, ref bufferReadValid);
            HasPosition = ReadBoolFromPacket(ref bufferReadValid);
            if (HasPosition)
            {
                Position = ReadVec2FromPacket(CompressionBasic.PositionCompressionInfo, ref bufferReadValid);
            }
            HasDirection = ReadBoolFromPacket(ref bufferReadValid);
            if (HasDirection)
            {
                Direction = ReadVec2FromPacket(CompressionBasic.UnitVectorCompressionInfo, ref bufferReadValid);
            }
            Width = ReadFloatFromPacket(WidthCompressionInfo, ref bufferReadValid);
            HasTarget = ReadBoolFromPacket(ref bufferReadValid);
            if (HasTarget)
            {
                TargetSide = (BattleSideEnum)ReadIntFromPacket(CompressionMission.TeamSideCompressionInfo, ref bufferReadValid);
                TargetFormationIndex = ReadIntFromPacket(TargetIndexCompressionInfo, ref bufferReadValid);
            }
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket((int)Side, CompressionMission.TeamSideCompressionInfo);
            WriteIntToPacket(FormationIndex, FormationIndexCompressionInfo);
            WriteIntToPacket((int)Kind, KindCompressionInfo);
            WriteBoolToPacket(HasPosition);
            if (HasPosition)
            {
                WriteVec2ToPacket(Position, CompressionBasic.PositionCompressionInfo);
            }
            WriteBoolToPacket(HasDirection);
            if (HasDirection)
            {
                WriteVec2ToPacket(Direction, CompressionBasic.UnitVectorCompressionInfo);
            }
            WriteFloatToPacket(Width, WidthCompressionInfo);
            WriteBoolToPacket(HasTarget);
            if (HasTarget)
            {
                WriteIntToPacket((int)TargetSide, CompressionMission.TeamSideCompressionInfo);
                WriteIntToPacket(TargetFormationIndex, TargetIndexCompressionInfo);
            }
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Orders;
        }

        protected override string OnGetLogFormat()
        {
            return $"General suggestion {Side} formation {FormationIndex}: {Kind}";
        }
    }
}
