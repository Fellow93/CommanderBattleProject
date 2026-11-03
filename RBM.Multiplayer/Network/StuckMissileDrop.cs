using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;
using TaleWorlds.ObjectSystem;

namespace RBM.Multiplayer.Network
{
    // Server tells every client that a stuck missile fell out (RbmMpStuckMissiles): which agent, the body (Slot -1) or
    // the shield in Slot, the attachment's index, the item (to check the index against), and the id the loose copy
    // got on the server. Laid out as the engine's own SpawnAttachedWeaponOnCorpse.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class StuckMissileDrop : GameNetworkMessage
    {
        private static readonly CompressionInfo.Integer SlotCompressionInfo = new CompressionInfo.Integer(-1, (int)EquipmentIndex.NumAllWeaponSlots - 1, maximumValueGiven: true);

        public StuckMissileDrop(int agentIndex, int slot, int attachmentIndex, ItemObject item, int spawnedId)
        {
            AgentIndex = agentIndex;
            Slot = slot;
            AttachmentIndex = attachmentIndex;
            Item = item;
            SpawnedId = spawnedId;
        }

        public StuckMissileDrop()
        {
        }

        public int AgentIndex { get; private set; }

        public int Slot { get; private set; }

        public int AttachmentIndex { get; private set; }

        public ItemObject Item { get; private set; }

        public int SpawnedId { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            AgentIndex = ReadAgentIndexFromPacket(ref bufferReadValid);
            Slot = ReadIntFromPacket(SlotCompressionInfo, ref bufferReadValid);
            AttachmentIndex = ReadIntFromPacket(CompressionMission.WeaponAttachmentIndexCompressionInfo, ref bufferReadValid);
            Item = ReadObjectReferenceFromPacket(MBObjectManager.Instance, CompressionBasic.GUIDCompressionInfo, ref bufferReadValid) as ItemObject;
            SpawnedId = ReadIntFromPacket(CompressionBasic.MissionObjectIDCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteAgentIndexToPacket(AgentIndex);
            WriteIntToPacket(Slot, SlotCompressionInfo);
            WriteIntToPacket(AttachmentIndex, CompressionMission.WeaponAttachmentIndexCompressionInfo);
            WriteObjectReferenceToPacket(Item, CompressionBasic.GUIDCompressionInfo);
            WriteIntToPacket(SpawnedId, CompressionBasic.MissionObjectIDCompressionInfo);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Items;
        }

        protected override string OnGetLogFormat()
        {
            return $"Stuck missile {AttachmentIndex} (slot {Slot}) of agent {AgentIndex} dropped as {SpawnedId}";
        }
    }
}
