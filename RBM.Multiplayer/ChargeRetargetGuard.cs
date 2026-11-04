using System;
using HarmonyLib;

namespace RBM.Multiplayer
{
    // A player's "charge that formation" ends in RBM's SetTargetFormation postfix, which re-issues the charge at the pick
    // and sets the target again; the nested call is meant to return at once because the order now aims at the pick.
    // When the formation is already charging and the pick is farther than ChargeGuard's start distance, ChargeGuard's
    // "keep" swaps the new order for the charge already running, so the nested call re-issues it again, endlessly: a
    // stack overflow killed the server with no log or dump. Only the outermost call runs; the formation keeps its
    // running charge (RBM's rule), the server stays up.
    [HarmonyPatch(typeof(RBMAI.OverrideSetMovementOrder), "PostfixSetTargetFormation")]
    internal static class ChargeRetargetGuard
    {
        [ThreadStatic]
        private static bool _inside;

        // The nested call happens on every re-target, so it isn't logged.
        private static bool Prefix(out bool __state)
        {
            __state = !_inside;
            _inside = true;
            return __state;
        }

        private static void Finalizer(bool __state)
        {
            if (__state)
            {
                _inside = false;
            }
        }
    }
}
