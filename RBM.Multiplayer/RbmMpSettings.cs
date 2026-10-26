namespace RBM.Multiplayer
{
    // Multiplayer runs on RBM's default settings (the initial values of the RBMConfig.RBMConfig statics), the same on
    // the server and every client. RBM's LoadConfig is never called: it would read, and rewrite, the singleplayer
    // config in Documents, and each machine would end up with different values.
    // RBM Combat, which cannot run in multiplayer yet, is switched off, and Frontline's front rank is made more aggressive.
    public static class RbmMpSettings
    {
        public static void Apply()
        {
            // RBM Combat needs RBM's item stats, which the multiplayer items don't have; it isn't part of this module.
            RBMConfig.RBMConfig.rbmCombatEnabled = false;
            // Commander battles want the front rank to press in rather than step back and trade places. Frontline's
            // decisions are taken on the server only, so these weights don't need to match the clients.
            RBMConfig.RBMConfig.frontlineAttackWeight = 1.3f;
            RBMConfig.RBMConfig.frontlineBackStepWeight = 0.75f;
            RbmMpLog.Detail($"Settings: RBM defaults; AI {RBMConfig.RBMConfig.rbmAiEnabled}, Stance {RBMConfig.RBMConfig.rbmStanceEnabled} (posture {RBMConfig.RBMConfig.postureEnabled}, stamina {RBMConfig.RBMConfig.staminaEnabled}, kick/bash {RBMConfig.RBMConfig.aiKickBashEnabled}), Frontline {RBMConfig.RBMConfig.frontlineEnabled} (attack {RBMConfig.RBMConfig.frontlineAttackWeight}, back step {RBMConfig.RBMConfig.frontlineBackStepWeight}), reactions {RBMConfig.RBMConfig.aiReactionsEnabled}, terrain tactics {RBMConfig.RBMConfig.terrainTacticsEnabled}");
        }
    }
}
