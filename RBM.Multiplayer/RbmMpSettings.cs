namespace RBM.Multiplayer
{
    // Multiplayer runs on RBM's default settings (the initial values of the RBMConfig.RBMConfig statics), the same on
    // the server and every client. RBM's LoadConfig is never called: it would read, and rewrite, the singleplayer
    // config in Documents, and each machine would end up with different values.
    // RBM Combat follows the host's combat mode (RbmMpCombatMode): on in RBM mode, on the server, where every blow is
    // decided. Frontline's front rank is made more aggressive.
    public static class RbmMpSettings
    {
        public static void Apply()
        {
            // The table LoadConfig would fill: RBM Combat's per-weapon-type armour factors, also read by RBM Stance's
            // crush-through estimate. Without it every type falls back to the base rule.
            RBMConfig.RBMConfig.weaponTypesFactors.Clear();
            RBMConfig.Utilities.createWeaponTypesFactors(ref RBMConfig.RBMConfig.weaponTypesFactors);
            // RBM Combat runs on RBM's own items (RBM mode's loadouts); on the stock mp_ items its numbers are far off.
            // The flag also switches RBM AI to RBM's missile speeds. Commander Battle reads the mode before this.
#if SERVER
            RBMConfig.RBMConfig.rbmCombatEnabled = RbmMpCombatMode.IsRbm;
            RBMConfig.RBMConfig.battleHitLoggingEnabled = RbmMpLog.BattleHits && RBMConfig.RBMConfig.rbmCombatEnabled;
#else
            RBMConfig.RBMConfig.rbmCombatEnabled = false;
#endif
            // Commander battles want the front rank to press in rather than step back and trade places. Frontline's
            // decisions are taken on the server only, so these weights don't need to match the clients.
            RBMConfig.RBMConfig.frontlineAttackWeight = 1.3f;
            RBMConfig.RBMConfig.frontlineBackStepWeight = 0.75f;
            RbmMpLog.Detail($"Settings: RBM defaults; AI {RBMConfig.RBMConfig.rbmAiEnabled}, Stance {RBMConfig.RBMConfig.rbmStanceEnabled} (posture {RBMConfig.RBMConfig.postureEnabled}, stamina {RBMConfig.RBMConfig.staminaEnabled}, kick/bash {RBMConfig.RBMConfig.aiKickBashEnabled}), Combat {RBMConfig.RBMConfig.rbmCombatEnabled} ({RBMConfig.RBMConfig.weaponTypesFactors.Count} weapon types), Frontline {RBMConfig.RBMConfig.frontlineEnabled} (attack {RBMConfig.RBMConfig.frontlineAttackWeight}, back step {RBMConfig.RBMConfig.frontlineBackStepWeight}), reactions {RBMConfig.RBMConfig.aiReactionsEnabled}, terrain tactics {RBMConfig.RBMConfig.terrainTacticsEnabled}");
        }
    }
}
