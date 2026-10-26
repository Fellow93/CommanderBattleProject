namespace RBM.Multiplayer
{
    // Multiplayer runs on RBM's default settings (the initial values of the RBMConfig.RBMConfig statics), the same on
    // the server and every client. RBM's LoadConfig is never called: it would read, and rewrite, the singleplayer
    // config in Documents, and each machine would end up with different values.
    // Only RBM Combat, which cannot run in multiplayer yet, is switched off.
    public static class RbmMpSettings
    {
        public static void Apply()
        {
            // RBM Combat needs RBM's item stats, which the multiplayer items don't have; it isn't part of this module.
            RBMConfig.RBMConfig.rbmCombatEnabled = false;
            RbmMpLog.Detail($"Settings: RBM defaults; AI {RBMConfig.RBMConfig.rbmAiEnabled}, Stance {RBMConfig.RBMConfig.rbmStanceEnabled} (posture {RBMConfig.RBMConfig.postureEnabled}, stamina {RBMConfig.RBMConfig.staminaEnabled}, kick/bash {RBMConfig.RBMConfig.aiKickBashEnabled}), Frontline {RBMConfig.RBMConfig.frontlineEnabled}, reactions {RBMConfig.RBMConfig.aiReactionsEnabled}, terrain tactics {RBMConfig.RBMConfig.terrainTacticsEnabled}");
        }
    }
}
