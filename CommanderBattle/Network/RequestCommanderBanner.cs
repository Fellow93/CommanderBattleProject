using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Client picks its banner: a sigil (a BannerManager icon id, or -1 for its profile banner's sigil) and a shade of
    // its side's colour (an index into CommanderBanners.GetShades). Sent when the player picks one and each time they
    // join a side, from their saved file. Only ids travel, never a banner code; the server checks them.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestCommanderBanner : GameNetworkMessage
    {
        public RequestCommanderBanner(int sigil, int shade)
        {
            Sigil = sigil;
            Shade = shade;
        }

        public RequestCommanderBanner()
        {
        }

        public int Sigil { get; private set; }

        public int Shade { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Sigil = ReadIntFromPacket(CommanderBannerPackets.SigilCompressionInfo, ref bufferReadValid);
            Shade = ReadIntFromPacket(CommanderBannerPackets.ShadeCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(Sigil, CommanderBannerPackets.SigilCompressionInfo);
            WriteIntToPacket(Shade, CommanderBannerPackets.ShadeCompressionInfo);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Equipment;
        }

        protected override string OnGetLogFormat()
        {
            return $"Request commander banner: sigil {Sigil}, shade {Shade}";
        }
    }
}
