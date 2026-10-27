using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // An admin asks to fix the time of day and weather (as in WeatherOverride; -1 for drawn from the seed). The server
    // only takes it from a peer the stock admin system marks as admin.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestWeatherOverride : GameNetworkMessage
    {
        public RequestWeatherOverride(int time, int weather)
        {
            Time = time;
            Weather = weather;
        }

        public RequestWeatherOverride()
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
            return MultiplayerMessageFilter.Administration;
        }

        protected override string OnGetLogFormat()
        {
            return $"Request weather override: time {Time}, weather {Weather}";
        }
    }
}
