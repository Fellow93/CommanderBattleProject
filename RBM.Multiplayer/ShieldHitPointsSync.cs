using HarmonyLib;
using NetworkMessages.FromServer;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace RBM.Multiplayer
{
    // RBM gives a few shields more than 1023 hit points (steel_round_shield 2000, noyans_shield 1500,
    // steppe_guardian_shield 1100), but the engine sends a shield's hit points after each hit in 10 bits
    // (SetWeaponNetworkData, CompressionMission.ItemDataCompressionInfo). Every hit on one tripped an engine assertion
    // that froze the server about 5 s (2026-11-03, a Khuzait round). The value is capped as it is written: the server
    // keeps the real hit points and decides when the shield breaks; the bearer's client shows at most 1023 until then.
    // Server only (RbmMpPatcher), both modes.
    [HarmonyPatch(typeof(SetWeaponNetworkData), "OnWrite")]
    internal static class ShieldHitPointsSync
    {
        private const int MaxDataValue = 1023;

        private static bool Prefix(SetWeaponNetworkData __instance)
        {
            if (__instance.DataValue <= MaxDataValue)
            {
                return true;
            }
            GameNetworkMessage.WriteAgentIndexToPacket(__instance.AgentIndex);
            GameNetworkMessage.WriteIntToPacket((int)__instance.WeaponEquipmentIndex, CompressionMission.ItemSlotCompressionInfo);
            GameNetworkMessage.WriteIntToPacket(MaxDataValue, CompressionMission.ItemDataCompressionInfo);
            return false;
        }
    }
}
