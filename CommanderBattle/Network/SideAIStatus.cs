using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    public enum AIFormationKind
    {
        Infantry,
        Ranged,
        Cavalry,
        HorseArchers,
        // The general and his bodyguard (formation 8), by its index rather than its troops.
        General
    }

    public enum AIFormationOrder
    {
        Other,
        Move,
        Charge,
        Hold,
        Retreat
    }

    public enum AIFormationLeader
    {
        // The team AI (delegated).
        AI,
        // The side's general took it over with the order bar: it keeps his orders, the AI leaves it alone.
        General,
        // Its own commander.
        Player
    }

    public struct AIFormationStatus
    {
        public int FormationIndex;

        public AIFormationKind Kind;

        public int Troops;

        // Who leads it; a behaviour is only shown for the AI.
        public AIFormationLeader Leader;

        public bool IsPlayerLed => Leader == AIFormationLeader.Player;

        // The tactic's main formation (FormationAI.IsMainFormation): the AI's archers and cavalry take their places
        // from it, even when a player leads it.
        public bool IsMain;

        // The AI's active behaviour, readable ("Skirmish Line"); empty for a player-led formation.
        public string Behavior;

        public AIFormationOrder Order;

        // Where the formation stands (its average position) and, when it is moving or charging, where it is going:
        // the move order's position, or the charged formation's position.
        public Vec2 Position;

        public bool HasDestination;

        public Vec2 Destination;
    }

    // Server tells the players of one side what their team AI is doing: its tactic and, per formation with men in
    // it, the behaviour it runs. Tactics and behaviours only exist on the server. Names are made readable there.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class SideAIStatus : GameNetworkMessage
    {
        private static readonly CompressionInfo.Integer FormationCountCompressionInfo = new CompressionInfo.Integer(0, 16, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer KindCompressionInfo = new CompressionInfo.Integer(0, 4, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer TroopsCompressionInfo = new CompressionInfo.Integer(0, 2047, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer OrderCompressionInfo = new CompressionInfo.Integer(0, 4, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer LeaderCompressionInfo = new CompressionInfo.Integer(0, 2, maximumValueGiven: true);

        public SideAIStatus(BattleSideEnum side, string tactic, List<AIFormationStatus> formations)
        {
            Side = side;
            Tactic = tactic ?? "";
            Formations = formations;
        }

        public SideAIStatus()
        {
        }

        public BattleSideEnum Side { get; private set; }

        // Readable ("Defensive Line"); empty when the team AI has no tactic.
        public string Tactic { get; private set; }

        public List<AIFormationStatus> Formations { get; private set; } = new List<AIFormationStatus>();

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Side = (BattleSideEnum)ReadIntFromPacket(CompressionMission.TeamSideCompressionInfo, ref bufferReadValid);
            Tactic = ReadStringFromPacket(ref bufferReadValid);
            int count = ReadIntFromPacket(FormationCountCompressionInfo, ref bufferReadValid);
            Formations = new List<AIFormationStatus>(count);
            for (int i = 0; i < count && bufferReadValid; i++)
            {
                AIFormationStatus formation = new AIFormationStatus
                {
                    FormationIndex = ReadIntFromPacket(CompressionMission.FormationClassCompressionInfo, ref bufferReadValid),
                    Kind = (AIFormationKind)ReadIntFromPacket(KindCompressionInfo, ref bufferReadValid),
                    Troops = ReadIntFromPacket(TroopsCompressionInfo, ref bufferReadValid),
                    Leader = (AIFormationLeader)ReadIntFromPacket(LeaderCompressionInfo, ref bufferReadValid),
                    IsMain = ReadBoolFromPacket(ref bufferReadValid),
                    Behavior = ReadStringFromPacket(ref bufferReadValid),
                    Order = (AIFormationOrder)ReadIntFromPacket(OrderCompressionInfo, ref bufferReadValid),
                    Position = ReadVec2FromPacket(CompressionBasic.PositionCompressionInfo, ref bufferReadValid),
                    HasDestination = ReadBoolFromPacket(ref bufferReadValid)
                };
                if (formation.HasDestination)
                {
                    formation.Destination = ReadVec2FromPacket(CompressionBasic.PositionCompressionInfo, ref bufferReadValid);
                }
                Formations.Add(formation);
            }
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket((int)Side, CompressionMission.TeamSideCompressionInfo);
            WriteStringToPacket(Tactic);
            int count = Formations.Count > 16 ? 16 : Formations.Count;
            WriteIntToPacket(count, FormationCountCompressionInfo);
            for (int i = 0; i < count; i++)
            {
                AIFormationStatus formation = Formations[i];
                WriteIntToPacket(formation.FormationIndex, CompressionMission.FormationClassCompressionInfo);
                WriteIntToPacket((int)formation.Kind, KindCompressionInfo);
                WriteIntToPacket(formation.Troops < 0 ? 0 : (formation.Troops > 2047 ? 2047 : formation.Troops), TroopsCompressionInfo);
                WriteIntToPacket((int)formation.Leader, LeaderCompressionInfo);
                WriteBoolToPacket(formation.IsMain);
                WriteStringToPacket(formation.Behavior ?? "");
                WriteIntToPacket((int)formation.Order, OrderCompressionInfo);
                WriteVec2ToPacket(formation.Position, CompressionBasic.PositionCompressionInfo);
                WriteBoolToPacket(formation.HasDestination);
                if (formation.HasDestination)
                {
                    WriteVec2ToPacket(formation.Destination, CompressionBasic.PositionCompressionInfo);
                }
            }
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Formations;
        }

        protected override string OnGetLogFormat()
        {
            return $"AI status {Side}: {Tactic}, {Formations.Count} formations";
        }
    }
}
