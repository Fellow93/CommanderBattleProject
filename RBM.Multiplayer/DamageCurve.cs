using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace RBM.Multiplayer
{
    // A light stand-in for RBM Combat until it can be ported (it needs RBM's item stats): all damage is cut by a
    // flat share, and then blows lose more the harder they hit, so one-hit kills become rarer. Applied to the final
    // damage of every blow on an agent (after armour, perks and body part), on the server, where all damage is
    // decided:
    //   scaled  = damage * Scale
    //   damage' = scaled / (1 + scaled / Softness)
    //   25 -> 12 (-52%), 50 -> 23 (-54%), 75 -> 33 (-56%), 100 -> 43 (-57%), 150 -> 60 (-60%), 200 -> 75 (-63%)
    internal static class DamageCurve
    {
        // Flat share of damage kept before the curve.
        public const float Scale = 0.5f;

        // A blow of this much (scaled) damage is halved by the curve; lower = stronger reduction of big hits.
        public const float Softness = 300f;

        public static float Apply(float damage)
        {
            if (damage <= 0f)
            {
                return damage;
            }
            float scaled = damage * Scale;
            return scaled / (1f + scaled / Softness);
        }

        [HarmonyPatch(typeof(AgentApplyDamageModel), nameof(AgentApplyDamageModel.CalculateDamage))]
        private static class CalculateDamagePatch
        {
            private static void Postfix(ref float __result)
            {
                __result = Apply(__result);
            }
        }
    }
}
