using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // An admin's fixed time of day and weather (CommanderBattleWeather.RoundTime / RoundWeather, -1 for drawn from the
    // round's seed). The server sends it to everyone when an admin changes it, and to each player who joins, before
    // their SpawnLayoutSeed, so their first look already follows it.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class WeatherOverride : GameNetworkMessage
    {
        public WeatherOverride(int time, int weather)
        {
            Time = time;
            Weather = weather;
        }

        public WeatherOverride()
        {
        }

        public int Time { get; private set; }

        public int Weather { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Time = ReadIntFromPacket(WeatherOverridePackets.TimeCompressionInfo, ref bufferReadValid);
            Weather = ReadIntFromPacket(WeatherOverridePackets.WeatherCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(Time, WeatherOverridePackets.TimeCompressionInfo);
            WriteIntToPacket(Weather, WeatherOverridePackets.WeatherCompressionInfo);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Mission;
        }

        protected override string OnGetLogFormat()
        {
            return $"Weather override: time {Time}, weather {Weather}";
        }
    }

    internal static class WeatherOverridePackets
    {
        public static readonly CompressionInfo.Integer TimeCompressionInfo = new CompressionInfo.Integer(-1, CommanderBattleWeather.TimeCount - 1, maximumValueGiven: true);

        public static readonly CompressionInfo.Integer WeatherCompressionInfo = new CompressionInfo.Integer(-1, CommanderBattleWeather.WeatherCount - 1, maximumValueGiven: true);
    }
}
