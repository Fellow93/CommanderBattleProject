using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Server tells clients a commander's banner: the sigil and shade their formation carries this round, and a pick
    // that applies from the next round (HasPending). Sigil -1 is the side's own sigil. Every client builds the banner
    // from these (CommanderBanners.Build); the shields themselves get it from the server with each agent. Sent on each
    // change and to players who join.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class CommanderBanner : GameNetworkMessage
    {
        public CommanderBanner(NetworkCommunicator peer, int sigil, int shade, bool hasPending, int pendingSigil, int pendingShade)
        {
            Peer = peer;
            Sigil = sigil;
            Shade = shade;
            HasPending = hasPending;
            PendingSigil = pendingSigil;
            PendingShade = pendingShade;
        }

        public CommanderBanner()
        {
        }

        public NetworkCommunicator Peer { get; private set; }

        public int Sigil { get; private set; }

        public int Shade { get; private set; }

        public bool HasPending { get; private set; }

        public int PendingSigil { get; private set; }

        public int PendingShade { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Peer = ReadNetworkPeerReferenceFromPacket(ref bufferReadValid);
            Sigil = ReadIntFromPacket(CommanderBannerPackets.SigilCompressionInfo, ref bufferReadValid);
            Shade = ReadIntFromPacket(CommanderBannerPackets.ShadeCompressionInfo, ref bufferReadValid);
            HasPending = ReadBoolFromPacket(ref bufferReadValid);
            if (HasPending)
            {
                PendingSigil = ReadIntFromPacket(CommanderBannerPackets.SigilCompressionInfo, ref bufferReadValid);
                PendingShade = ReadIntFromPacket(CommanderBannerPackets.ShadeCompressionInfo, ref bufferReadValid);
            }
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteNetworkPeerReferenceToPacket(Peer);
            WriteIntToPacket(Sigil, CommanderBannerPackets.SigilCompressionInfo);
            WriteIntToPacket(Shade, CommanderBannerPackets.ShadeCompressionInfo);
            WriteBoolToPacket(HasPending);
            if (HasPending)
            {
                WriteIntToPacket(PendingSigil, CommanderBannerPackets.SigilCompressionInfo);
                WriteIntToPacket(PendingShade, CommanderBannerPackets.ShadeCompressionInfo);
            }
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Equipment;
        }

        protected override string OnGetLogFormat()
        {
            return $"Commander banner of {Peer?.UserName}: sigil {Sigil}, shade {Shade}{(HasPending ? $", next round sigil {PendingSigil}, shade {PendingShade}" : "")}";
        }
    }

    public static class CommanderBannerPackets
    {
        // Banner mesh ids fit 13 bits (the stock banner packet's width); -1 is the side's own sigil.
        public static readonly CompressionInfo.Integer SigilCompressionInfo = new CompressionInfo.Integer(-1, 8191, maximumValueGiven: true);

        public static readonly CompressionInfo.Integer ShadeCompressionInfo = new CompressionInfo.Integer(0, CommanderBanners.MaxShades - 1, maximumValueGiven: true);
    }
}
