using System.Collections.Generic;
using HarmonyLib;
using RBMStance;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer.Stance
{
    // RBM's tired reaction (StanceLogic.ForceTiredAnimation) for players. AI soldiers keep RBM's own clip
    // (act_scared_to_normal_2, added to the warrior action set by RBM_action_sets.xml, which this module ships).
    // For a player RBM picks by camera, which a server can't see, and its first-person exhaustion (view darkening,
    // input lock) runs on the main agent of the machine it happens on, never a player's on a server. So a player gets
    // RBM's camera-safe fallback, the backward stagger, with the same uncancellable priority and, on foot, the same
    // rooted lower body; and on foot a StanceEvent, from which their client plays the exhaustion effect, plus RBM's
    // exertion grunt (PlayerExhaustionLogic.TryBegin), made on the server so everyone around hears it.
    [HarmonyPatch(typeof(StanceLogic), nameof(StanceLogic.ForceTiredAnimation))]
    internal static class TiredReactionPatch
    {
        // RBM grunts once per exhaustion, not on every tired blow; its lock lasts about this long.
        private const float GruntInterval = 3f;

        private static readonly Dictionary<Agent, float> LastGrunt = new Dictionary<Agent, float>();

        private static bool Prefix(Agent agent, float actionSpeed)
        {
            if (agent?.MissionPeer == null)
            {
                return true;
            }
            if (!StanceLogic.CanForceReaction(agent))
            {
                return false;
            }
            AnimFlags flags = AnimFlags.amf_priority_striked;
            ActionIndexCache action = ActionIndexCache.act_stagger_backward;
            if (!agent.HasMount)
            {
                agent.SetActionChannel(0, action, additionalFlags: flags | AnimFlags.anf_lock_movement, actionSpeed: actionSpeed);
                StanceNetworkSync.SendExhausted(agent);
                Grunt(agent);
            }
            agent.SetActionChannel(1, action, additionalFlags: flags, actionSpeed: actionSpeed);
            return false;
        }

        private static void Grunt(Agent agent)
        {
            float now = agent.Mission?.CurrentTime ?? 0f;
            if (LastGrunt.TryGetValue(agent, out float last) && now >= last && now - last < GruntInterval)
            {
                return;
            }
            LastGrunt[agent] = now;
            if (LastGrunt.Count > 256)
            {
                LastGrunt.Clear();
                LastGrunt[agent] = now;
            }
            agent.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
        }
    }
}
