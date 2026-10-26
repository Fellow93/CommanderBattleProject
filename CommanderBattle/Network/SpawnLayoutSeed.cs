using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Server tells clients the round's spawn layout inputs: the seed and every formation's troop count
    // (CommanderBattleSpawnLayout), so they place the preparation-phase visuals where the server will spawn the
    // armies, and the formation list shows the real counts. The counts come from the classes and the commanders'
    // perks, which clients don't all know, so they are sent rather than recomputed. Sent when preparation starts,
    // again whenever a count changes before the armies spawn, and to players who join.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class SpawnLayoutSeed : GameNetworkMessage
    {
        private static readonly CompressionInfo.Integer SeedCompressionInfo = new CompressionInfo.Integer(0, 30);

        private static readonly CompressionInfo.Integer TroopCountCompressionInfo = new CompressionInfo.Integer(0, CommanderBattleSettings.MaxTroopsPerFormation, maximumValueGiven: true);

        public SpawnLayoutSeed(int seed, int[] troopCounts)
        {
            Seed = seed;
            TroopCounts = troopCounts;
        }

        public SpawnLayoutSeed()
        {
        }

        // 0..2^30-1; 0 is the plain layout.
        public int Seed { get; private set; }

        // FormationSlotTable.GetTroopCounts' order: defender slots, then attacker slots.
        public int[] TroopCounts { get; private set; }

        private static int CountLength => 2 * CommanderBattleSettings.SlotCount;

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Seed = ReadIntFromPacket(SeedCompressionInfo, ref bufferReadValid);
            TroopCounts = new int[CountLength];
            for (int i = 0; i < TroopCounts.Length; i++)
            {
                TroopCounts[i] = ReadIntFromPacket(TroopCountCompressionInfo, ref bufferReadValid);
            }
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(Seed, SeedCompressionInfo);
            for (int i = 0; i < CountLength; i++)
            {
                int count = TroopCounts != null && i < TroopCounts.Length ? TroopCounts[i] : 0;
                WriteIntToPacket(count < 0 ? 0 : (count > CommanderBattleSettings.MaxTroopsPerFormation ? CommanderBattleSettings.MaxTroopsPerFormation : count), TroopCountCompressionInfo);
            }
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Mission;
        }

        protected override string OnGetLogFormat()
        {
            return $"Spawn layout seed {Seed}, troop counts {(TroopCounts != null ? string.Join(",", TroopCounts) : "-")}";
        }
    }
}
