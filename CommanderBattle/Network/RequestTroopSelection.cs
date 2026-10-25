using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Client picks its formation's troop type (an index into its side culture's classes, as SelectedTroopIndex) and
    // one perk per list. The server takes it at any time; it applies when the next round's preparation starts, or
    // at once during preparation before the armies spawn. The stock RequestTroopIndexChange is only accepted
    // while agents spawn, so it can't serve a pick made mid-round.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestTroopSelection : GameNetworkMessage
    {
        public RequestTroopSelection(int classIndex, int[] perks)
        {
            ClassIndex = classIndex;
            Perks = perks;
        }

        public RequestTroopSelection()
        {
        }

        public int ClassIndex { get; private set; }

        // One perk index per list (CommanderRoles.PerkListCount).
        public int[] Perks { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
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
            WriteIntToPacket(ClassIndex, CompressionMission.SelectedTroopIndexCompressionInfo);
            for (int i = 0; i < CommanderRoles.PerkListCount; i++)
            {
                WriteIntToPacket(Perks != null && i < Perks.Length ? Perks[i] : 0, CompressionMission.PerkIndexCompressionInfo);
            }
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Equipment;
        }

        protected override string OnGetLogFormat()
        {
            return $"Request troop selection: class {ClassIndex}, perks {(Perks != null ? string.Join(",", Perks) : "-")}";
        }
    }
}
