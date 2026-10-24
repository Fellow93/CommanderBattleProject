using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Server tells clients who commands a formation slot (null = AI) and with which troop class.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class FormationSlotUpdate : GameNetworkMessage
    {
        public FormationSlotUpdate(BattleSideEnum side, int slotIndex, NetworkCommunicator commander, int classIndex)
        {
            Side = side;
            SlotIndex = slotIndex;
            Commander = commander;
            ClassIndex = classIndex;
        }

        public FormationSlotUpdate()
        {
        }

        public BattleSideEnum Side { get; private set; }

        public int SlotIndex { get; private set; }

        public NetworkCommunicator Commander { get; private set; }

        public int ClassIndex { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Side = (BattleSideEnum)ReadIntFromPacket(CompressionMission.TeamSideCompressionInfo, ref bufferReadValid);
            SlotIndex = ReadIntFromPacket(CompressionMission.FormationClassCompressionInfo, ref bufferReadValid);
            if (ReadBoolFromPacket(ref bufferReadValid))
            {
                Commander = ReadNetworkPeerReferenceFromPacket(ref bufferReadValid);
            }
            ClassIndex = ReadIntFromPacket(CompressionMission.SelectedTroopIndexCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket((int)Side, CompressionMission.TeamSideCompressionInfo);
            WriteIntToPacket(SlotIndex, CompressionMission.FormationClassCompressionInfo);
            WriteBoolToPacket(Commander != null);
            if (Commander != null)
            {
                WriteNetworkPeerReferenceToPacket(Commander);
            }
            WriteIntToPacket(ClassIndex, CompressionMission.SelectedTroopIndexCompressionInfo);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Formations;
        }

        protected override string OnGetLogFormat()
        {
            return $"Formation slot {Side} {SlotIndex}: {(Commander != null ? Commander.UserName : "AI")}, class {ClassIndex}";
        }
    }
}
