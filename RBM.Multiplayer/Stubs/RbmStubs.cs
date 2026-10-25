using TaleWorlds.MountAndBlade;

// Stand-ins for the RBM types whose files are left out of RBM.Multiplayer (singleplayer HUD, campaign perks,
// Gauntlet markers) but which the files compiled here still reference. Same names, inert behaviour.

namespace RBMStance
{
    // RBM's singleplayer posture HUD (UI/StanceVisualLogic.cs). AgentStances.postureVisual stays null in
    // multiplayer, so RBM's HUD updates return early; RBM.Multiplayer.View draws the bars from network messages.
    public class StanceVisualLogic : MissionLogic
    {
        public int timer = 0;

        public StanceVisualVM _dataSource;

        public Agent affectedAgent = null;

        public int DisplayTime => 0;
    }

    // RBM's first-person exhaustion effect (Stance/PlayerExhaustion.cs). On the server no agent is the main agent,
    // so it never begins; RBM.Multiplayer.View plays the effect on the exhausted player's own client.
    public class PlayerExhaustionLogic : MissionLogic
    {
        internal static PlayerExhaustionLogic Instance;

        internal static bool TryBegin(Agent agent)
        {
            return false;
        }

        internal static bool IsInputLocked(Mission mission)
        {
            return false;
        }
    }
}

namespace RBMAI
{
    // RBM's formation marker smoothing (UI/FormationMarkers.cs), a Gauntlet HUD patch: nothing to clear here.
    internal static class FormationMarkerPatches
    {
        internal static void Clear()
        {
        }
    }
}

namespace RBMConfig
{
    // RBM's Arrow Catcher perk scaling (Shared/ArrowCatcher.cs) reads campaign perks; there are none in
    // multiplayer, where every factor is 1, as RBM's own GetStrength returns 0 outside a campaign.
    public static class ArrowCatcher
    {
        public static float GetStrength(Agent agent)
        {
            return 0f;
        }

        public static float GetPostureFactor(Agent agent)
        {
            return 1f;
        }

        public static float GetShieldDamageFactor(Agent agent)
        {
            return 1f;
        }

        public static float GetPilumPenetrationFactor(Agent agent)
        {
            return 1f;
        }
    }
}
