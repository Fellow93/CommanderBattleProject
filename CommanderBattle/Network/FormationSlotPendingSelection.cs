using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Server tells clients a formation slot's pending troop selection (FormationSlot.PendingClassIndex/PendingPerks):
    // the type and perks that apply when the next round's preparation starts. ClassIndex -1 = none. Sent when a pick
    // is stored or applied, and to players who join.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class FormationSlotPendingSelection : GameNetworkMessage
    {
        public FormationSlotPendingSelection(BattleSideEnum side, int slotIndex, int classIndex, int[] perks)
        {
            Side = side;
            SlotIndex = slotIndex;
            ClassIndex = classIndex;
            Perks = perks;
        }

        public FormationSlotPendingSelection()
        {
        }

        public BattleSideEnum Side { get; private set; }

        public int SlotIndex { get; private set; }

        public int ClassIndex { get; private set; }

        public int[] Perks { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Side = (BattleSideEnum)ReadIntFromPacket(CompressionMission.TeamSideCompressionInfo, ref bufferReadValid);
            SlotIndex = ReadIntFromPacket(CompressionMission.FormationClassCompressionInfo, ref bufferReadValid);
            ClassIndex = ReadIntFromPacket(CompressionMission.SelectedTroopIndexCompressionInfo, ref bufferReadValid);
            Perks = new int[CommanderRoles.PerkListCount];
            for (int i = 0; i < Perks.Length; i++)
            {
                Perks[i] = ReadIntFromPacket(CompressionMission.PerkIndexCompressionInfo, ref bufferReadValid);
            }
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket((int)Side, CompressionMission.TeamSideCompressionInfo);
            WriteIntToPacket(SlotIndex, CompressionMission.FormationClassCompressionInfo);
            WriteIntToPacket(ClassIndex, CompressionMission.SelectedTroopIndexCompressionInfo);
            for (int i = 0; i < CommanderRoles.PerkListCount; i++)
            {
                WriteIntToPacket(Perks != null && i < Perks.Length ? Perks[i] : 0, CompressionMission.PerkIndexCompressionInfo);
            }
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Formations;
        }

        protected override string OnGetLogFormat()
        {
            return $"Formation slot {Side} {SlotIndex}: pending class {ClassIndex}, perks {(Perks != null ? string.Join(",", Perks) : "-")}";
        }
    }
}
