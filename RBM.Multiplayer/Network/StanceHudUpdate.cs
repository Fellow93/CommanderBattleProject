using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace RBM.Multiplayer.Network
{
    // Server tells one player the posture/stamina of the agent they control and of the last agent they exchanged
    // blows with (RBM's posture HUD). Only the server simulates posture, so the client has nothing else to draw from.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class StanceHudUpdate : GameNetworkMessage
    {
        // RBM's maxima stay well inside these: posture ~30-300, stamina ~1000-3500, health ~ up to a horse's.
        private static readonly CompressionInfo.Integer PostureCompressionInfo = new CompressionInfo.Integer(0, 1023, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer StaminaCompressionInfo = new CompressionInfo.Integer(0, 16383, maximumValueGiven: true);

        private static readonly CompressionInfo.Integer HealthCompressionInfo = new CompressionInfo.Integer(0, 4095, maximumValueGiven: true);

        public StanceHudUpdate(int posture, int postureMax, int stamina, int staminaMax, int targetAgentIndex, int targetPosture, int targetPostureMax, int targetStamina, int targetStaminaMax, int targetHealth, int targetHealthMax)
        {
            Posture = posture;
            PostureMax = postureMax;
            Stamina = stamina;
            StaminaMax = staminaMax;
            TargetAgentIndex = targetAgentIndex;
            TargetPosture = targetPosture;
            TargetPostureMax = targetPostureMax;
            TargetStamina = targetStamina;
            TargetStaminaMax = targetStaminaMax;
            TargetHealth = targetHealth;
            TargetHealthMax = targetHealthMax;
        }

        public StanceHudUpdate()
        {
        }

        public int Posture { get; private set; }

        public int PostureMax { get; private set; }

        public int Stamina { get; private set; }

        public int StaminaMax { get; private set; }

        // -1 = no target to show.
        public int TargetAgentIndex { get; private set; }

        public int TargetPosture { get; private set; }

        public int TargetPostureMax { get; private set; }

        public int TargetStamina { get; private set; }

        public int TargetStaminaMax { get; private set; }

        public int TargetHealth { get; private set; }

        public int TargetHealthMax { get; private set; }

        public bool HasTarget => TargetAgentIndex >= 0;

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            Posture = ReadIntFromPacket(PostureCompressionInfo, ref bufferReadValid);
            PostureMax = ReadIntFromPacket(PostureCompressionInfo, ref bufferReadValid);
            Stamina = ReadIntFromPacket(StaminaCompressionInfo, ref bufferReadValid);
            StaminaMax = ReadIntFromPacket(StaminaCompressionInfo, ref bufferReadValid);
            TargetAgentIndex = -1;
            if (ReadBoolFromPacket(ref bufferReadValid))
            {
                TargetAgentIndex = ReadAgentIndexFromPacket(ref bufferReadValid);
                TargetPosture = ReadIntFromPacket(PostureCompressionInfo, ref bufferReadValid);
                TargetPostureMax = ReadIntFromPacket(PostureCompressionInfo, ref bufferReadValid);
                TargetStamina = ReadIntFromPacket(StaminaCompressionInfo, ref bufferReadValid);
                TargetStaminaMax = ReadIntFromPacket(StaminaCompressionInfo, ref bufferReadValid);
                TargetHealth = ReadIntFromPacket(HealthCompressionInfo, ref bufferReadValid);
                TargetHealthMax = ReadIntFromPacket(HealthCompressionInfo, ref bufferReadValid);
            }
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(Clamp(Posture, 1023), PostureCompressionInfo);
            WriteIntToPacket(Clamp(PostureMax, 1023), PostureCompressionInfo);
            WriteIntToPacket(Clamp(Stamina, 16383), StaminaCompressionInfo);
            WriteIntToPacket(Clamp(StaminaMax, 16383), StaminaCompressionInfo);
            WriteBoolToPacket(HasTarget);
            if (HasTarget)
            {
                WriteAgentIndexToPacket(TargetAgentIndex);
                WriteIntToPacket(Clamp(TargetPosture, 1023), PostureCompressionInfo);
                WriteIntToPacket(Clamp(TargetPostureMax, 1023), PostureCompressionInfo);
                WriteIntToPacket(Clamp(TargetStamina, 16383), StaminaCompressionInfo);
                WriteIntToPacket(Clamp(TargetStaminaMax, 16383), StaminaCompressionInfo);
                WriteIntToPacket(Clamp(TargetHealth, 4095), HealthCompressionInfo);
                WriteIntToPacket(Clamp(TargetHealthMax, 4095), HealthCompressionInfo);
            }
        }

        private static int Clamp(int value, int max)
        {
            return value < 0 ? 0 : (value > max ? max : value);
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Agents;
        }

        protected override string OnGetLogFormat()
        {
            return $"Stance HUD: posture {Posture}/{PostureMax}, stamina {Stamina}/{StaminaMax}, target {TargetAgentIndex}";
        }
    }
}
