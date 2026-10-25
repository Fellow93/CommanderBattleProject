using System.Reflection;
using HarmonyLib;
using RBMStance;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer.Stance
{
    // RBM tells a player whose posture breaks on a chamber block (StanceLogic.CreateMeleeBlowPatch
    // .HandleDefenderChamberBlock) with an InformationManager message, which on a server reaches nobody, and the
    // blow it makes deals no damage, so StanceNetworkSync's crush-through check never sees it. Sent here instead,
    // when the defender's posture break count goes up across the call.
    [HarmonyPatch]
    internal static class ChamberBlockBreakPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.Inner(typeof(StanceLogic), "CreateMeleeBlowPatch"), "HandleDefenderChamberBlock");
        }

        private static void Prefix(RBMStance.Stance defenderStance, out int __state)
        {
            __state = defenderStance?.postureBreakCount ?? 0;
        }

        private static void Postfix(RBMStance.Stance defenderStance, Agent victimAgent, int __state)
        {
            if (defenderStance != null && victimAgent != null && defenderStance.postureBreakCount != __state && victimAgent.MissionPeer != null)
            {
                StanceNetworkSync.SendChamberBlockPostureBreak(victimAgent);
            }
        }
    }
}
