using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer.AI
{
    // RBM AI gives every ranged agent IgnoreAmmoLimitForRangeCalculation each tick (the HumanAIComponent.OnTick
    // postfix in TickPatches). The engine then lets an archer shoot at full range however few arrows are left, as
    // vanilla does only for siege defenders resupplied by AmmoSupplyLogic. Commander Battle has no resupply, so the
    // quivers would empty at long range before the lines meet. Without the flag the engine shortens the range as the ammo
    // runs low. RBM re-sets the flag every tick, so it is stripped here, at the setter, rather than cleared after.
    // Server only (patched on the dedicated server).
    [HarmonyPatch(typeof(Agent), nameof(Agent.SetScriptedCombatFlags))]
    internal static class AmmoLimitGuard
    {
        private static void Prefix(ref Agent.AISpecialCombatModeFlags flags)
        {
            if (GameNetwork.IsServerOrRecorder)
            {
                flags &= ~Agent.AISpecialCombatModeFlags.IgnoreAmmoLimitForRangeCalculation;
            }
        }
    }
}
