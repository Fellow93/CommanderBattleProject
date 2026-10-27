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

        // A kick or bash does only a few points before the curve, and the engine rounds what is left, often to 0, or
        // armour takes it all. A 0-damage blow is not applied: no knockback, no hit reaction, no RBM knockdown. So a
        // kick or bash that lands on an enemy always does at least 1.
        [HarmonyPatch(typeof(Mission), "GetAttackCollisionResults")]
        private static class AlternativeAttackMinimumPatch
        {
            private static void Postfix(Agent attackerAgent, Agent victimAgent, bool cancelDamage, ref AttackCollisionData attackCollisionData)
            {
                if (!attackCollisionData.IsAlternativeAttack || cancelDamage || attackCollisionData.InflictedDamage >= 1 || attackerAgent == null
                    || victimAgent == null || !attackerAgent.IsEnemyOf(victimAgent) || attackCollisionData.AttackBlockedWithShield
                    || attackCollisionData.CollisionResult == CombatCollisionResult.Blocked || attackCollisionData.CollisionResult == CombatCollisionResult.Parried
                    || attackCollisionData.CollisionResult == CombatCollisionResult.ChamberBlocked)
                {
                    return;
                }
                attackCollisionData.InflictedDamage = 1;
            }
        }
    }
}
