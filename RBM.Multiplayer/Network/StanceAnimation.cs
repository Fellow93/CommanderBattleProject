using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace RBM.Multiplayer.Network
{
    // Server tells every client to play one of RBM's forced reactions (stagger, tired, kick/bash fall) on an agent.
    // The engine has no message for a scripted action, so RBM's server-side SetActionChannel never reached clients:
    // a player's own client, which drives its agent's animation, never played it, and other agents showed it late or
    // not at all. The action is sent as its place in StanceAnimationSync.Actions, which server and client share.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class StanceAnimation : GameNetworkMessage
    {
        private static readonly CompressionInfo.Integer ActionCompressionInfo = new CompressionInfo.Integer(0, 15, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer ChannelCompressionInfo = new CompressionInfo.Integer(0, 1, maximumValueGiven: true);

        private static readonly CompressionInfo.Float SpeedCompressionInfo = new CompressionInfo.Float(0f, 4f, 10);

        private static readonly CompressionInfo.Float ProgressCompressionInfo = new CompressionInfo.Float(0f, 1f, 8);

        public StanceAnimation(int agentIndex, int action, int channel, bool ignorePriority, bool priorityStruck, bool lockMovement, float actionSpeed, float startProgress)
        {
            AgentIndex = agentIndex;
            Action = action;
            Channel = channel;
            IgnorePriority = ignorePriority;
            PriorityStruck = priorityStruck;
            LockMovement = lockMovement;
            ActionSpeed = actionSpeed;
            StartProgress = startProgress;
        }

        public StanceAnimation()
        {
        }

        public int AgentIndex { get; private set; }

        public int Action { get; private set; }

        public int Channel { get; private set; }

        public bool IgnorePriority { get; private set; }

        // AnimFlags.amf_priority_striked: nothing short of another hit or fall interrupts it.
        public bool PriorityStruck { get; private set; }

        // AnimFlags.anf_lock_movement: rooted while it plays.
        public bool LockMovement { get; private set; }

        public float ActionSpeed { get; private set; }

        public float StartProgress { get; private set; }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            AgentIndex = ReadAgentIndexFromPacket(ref bufferReadValid);
            Action = ReadIntFromPacket(ActionCompressionInfo, ref bufferReadValid);
            Channel = ReadIntFromPacket(ChannelCompressionInfo, ref bufferReadValid);
            IgnorePriority = ReadBoolFromPacket(ref bufferReadValid);
            PriorityStruck = ReadBoolFromPacket(ref bufferReadValid);
            LockMovement = ReadBoolFromPacket(ref bufferReadValid);
            ActionSpeed = ReadFloatFromPacket(SpeedCompressionInfo, ref bufferReadValid);
            StartProgress = ReadFloatFromPacket(ProgressCompressionInfo, ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteAgentIndexToPacket(AgentIndex);
            WriteIntToPacket(Action, ActionCompressionInfo);
            WriteIntToPacket(Channel, ChannelCompressionInfo);
            WriteBoolToPacket(IgnorePriority);
            WriteBoolToPacket(PriorityStruck);
            WriteBoolToPacket(LockMovement);
            WriteFloatToPacket(ActionSpeed < 0f ? 0f : (ActionSpeed > 4f ? 4f : ActionSpeed), SpeedCompressionInfo);
            WriteFloatToPacket(StartProgress < 0f ? 0f : (StartProgress > 1f ? 1f : StartProgress), ProgressCompressionInfo);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.AgentAnimations;
        }

        protected override string OnGetLogFormat()
        {
            return $"Stance animation {Action} on channel {Channel} of agent {AgentIndex}";
        }
    }
}
