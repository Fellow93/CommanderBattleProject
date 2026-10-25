using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle.Network
{
    // Client asks to command a free formation of this role on its own side (the troop screen's first step). The
    // server picks the lowest free slot of the role and applies the same rules as RequestFormationCommand.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestRoleCommand : GameNetworkMessage
    {
        private static readonly CompressionInfo.Integer RoleCompressionInfo = new CompressionInfo.Integer(0, 3, maximumValueGiven: true);

        public RequestRoleCommand(CommanderRole role)
        {
            Role = role;
        }

        public RequestRoleCommand()
        {
        }

        public CommanderRole Role { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Role = (CommanderRole)ReadIntFromPacket(RoleCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket((int)Role, RoleCompressionInfo);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Formations;
        }

        protected override string OnGetLogFormat()
        {
            return "Request to command a formation of role " + Role;
        }
    }
}
