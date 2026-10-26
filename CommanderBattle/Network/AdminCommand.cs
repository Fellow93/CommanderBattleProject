using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    public enum AdminCommandType
    {
        RestartRound,
        SwapSides,
        // Target leaves their formation to the AI.
        ReleaseFormation
    }

    // Client asks for a Commander Battle admin command (the admin panel's Commander Battle group). The server only
    // runs it for a peer the stock admin system marks as admin (NetworkCommunicator.IsAdmin).
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class AdminCommand : GameNetworkMessage
    {
        private static readonly CompressionInfo.Integer CommandCompressionInfo = new CompressionInfo.Integer(0, 2, maximumValueGiven: true);

        public AdminCommand(AdminCommandType command, NetworkCommunicator target = null)
        {
            Command = command;
            Target = target;
        }

        public AdminCommand()
        {
        }

        public AdminCommandType Command { get; private set; }

        public NetworkCommunicator Target { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Command = (AdminCommandType)ReadIntFromPacket(CommandCompressionInfo, ref bufferReadValid);
            if (ReadBoolFromPacket(ref bufferReadValid))
            {
                Target = ReadNetworkPeerReferenceFromPacket(ref bufferReadValid);
            }
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket((int)Command, CommandCompressionInfo);
            WriteBoolToPacket(Target != null);
            if (Target != null)
            {
                WriteNetworkPeerReferenceToPacket(Target);
            }
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Administration;
        }

        protected override string OnGetLogFormat()
        {
            return $"Admin command {Command}{(Target != null ? " on " + Target.UserName : "")}";
        }
    }
}
