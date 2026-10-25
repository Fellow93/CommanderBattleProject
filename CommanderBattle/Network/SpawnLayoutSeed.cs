using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Server tells clients the round's spawn layout seed (CommanderBattleSpawnLayout), so they place the
    // preparation-phase visuals where the server will spawn the armies.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class SpawnLayoutSeed : GameNetworkMessage
    {
        private static readonly CompressionInfo.Integer SeedCompressionInfo = new CompressionInfo.Integer(0, 30);

        public SpawnLayoutSeed(int seed)
        {
            Seed = seed;
        }

        public SpawnLayoutSeed()
        {
        }

        // 0..2^30-1; 0 is the plain layout.
        public int Seed { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Seed = ReadIntFromPacket(SeedCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(Seed, SeedCompressionInfo);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Mission;
        }

        protected override string OnGetLogFormat()
        {
            return $"Spawn layout seed {Seed}";
        }
    }
}
