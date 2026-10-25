using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace RBM.Multiplayer.Network
{
    public enum StanceEventType
    {
        // The player's posture broke and the blow crushed through to them.
        OwnCrushThrough,
        // The player broke an enemy's posture and crushed through.
        EnemyCrushThrough,
        // The player ran out of posture while tired: rooted for a moment, view darkening (RBM's player exhaustion).
        Exhausted,
        // The player's posture broke on a chamber block. The blow deals no damage, so it is not a crush-through.
        ChamberBlockPostureBreak
    }

    // Server tells one player about a posture event: the messages RBM shows in singleplayer, and the cue for the
    // first-person exhaustion effect. Reliable: each is shown once.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class StanceEvent : GameNetworkMessage
    {
        private static readonly CompressionInfo.Integer EventTypeCompressionInfo = new CompressionInfo.Integer(0, 3, maximumValueGiven: true);

        public StanceEvent(StanceEventType eventType, int damage, BoneBodyPartType bodyPart)
        {
            EventType = eventType;
            Damage = damage;
            BodyPart = bodyPart;
        }

        public StanceEvent()
        {
        }

        public StanceEventType EventType { get; private set; }

        public int Damage { get; private set; }

        public BoneBodyPartType BodyPart { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            EventType = (StanceEventType)ReadIntFromPacket(EventTypeCompressionInfo, ref bufferReadValid);
            Damage = ReadIntFromPacket(CompressionBasic.AgentHitDamageCompressionInfo, ref bufferReadValid);
            BodyPart = (BoneBodyPartType)ReadIntFromPacket(CompressionBasic.AgentHitBodyPartCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket((int)EventType, EventTypeCompressionInfo);
            WriteIntToPacket(Damage < 0 ? 0 : (Damage > 2000 ? 2000 : Damage), CompressionBasic.AgentHitDamageCompressionInfo);
            WriteIntToPacket((int)BodyPart, CompressionBasic.AgentHitBodyPartCompressionInfo);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Agents;
        }

        protected override string OnGetLogFormat()
        {
            return $"Stance event {EventType}: {Damage} damage, {BodyPart}";
        }
    }
}
