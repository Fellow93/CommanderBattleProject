using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer
{
    // RBM's tactics regroup the AI's soldiers as in singleplayer: they merge AI-led formations of a kind and split
    // flanking parties off them. A formation a player commands must keep exactly that commander's soldiers.
    internal static class FormationOwnershipGuard
    {
        // Stock splitting (TacticComponent.SplitFormationClassIntoGivenNumber, used by every tactic's
        // ManageFormationCounts) counts a sergeant player's formation as AI-owned unless the player is Agent.Main,
        // which on a server is nobody, so every commander's formation would be split. In multiplayer a formation a
        // player owns is never the AI's.
        [HarmonyPatch(typeof(Formation), nameof(Formation.IsAIOwned), MethodType.Getter)]
        private static class PlayerFormationIsNotAIOwned
        {
            private static bool Prefix(Formation __instance, ref bool __result)
            {
                if (__instance.PlayerOwner == null)
                {
                    return true;
                }
                __result = false;
                return false;
            }
        }

        // The safety net under every regrouping, RBM's included (its tactics move soldiers one by one): no soldier
        // moves into or out of a formation a player owns. Spawning (no formation yet), dying (no formation after)
        // and a commander taking over one of their own soldiers (same formation) are not moves between formations.
        // A commander who leaves hands the formation to the AI first (PlayerOwner = null), so the AI may then
        // regroup it.
        [HarmonyPatch(typeof(Agent), nameof(Agent.Formation), MethodType.Setter)]
        private static class KeepCommandersSoldiers
        {
            private static bool Prefix(Agent __instance, Formation value)
            {
                Formation current = __instance.Formation;
                if (current == null || value == null || current == value)
                {
                    return true;
                }
                return current.PlayerOwner == null && value.PlayerOwner == null;
            }
        }
    }
}
