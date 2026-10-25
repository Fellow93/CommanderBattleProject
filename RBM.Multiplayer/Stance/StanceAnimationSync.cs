using HarmonyLib;
using RBM.Multiplayer.Network;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer.Stance
{
    // Plays RBM's forced reactions on every client. RBM starts them on the server with Agent.SetActionChannel, which
    // the engine doesn't send to clients. Rather than patch each of RBM's call sites (stagger, tired, kick/bash fall,
    // horse-charge stagger), the server sends each SetActionChannel of one of these actions as a StanceAnimation.
    internal static class StanceAnimationSync
    {
        // Shared by server and client: the message carries the action's place in this list. Append only. Mirrors
        // RBM's AiKickBash.StaggerActions plus its two knockdown falls (StanceLogic.TiredAnimation,
        // AiKickBash.FallForward/FallBackward), named here so a client never initialises RBM's stance classes.
        internal static readonly ActionIndexCache[] Actions =
        {
            ActionIndexCache.act_stagger_left,
            ActionIndexCache.act_stagger_right,
            ActionIndexCache.act_stagger_forward,
            ActionIndexCache.act_stagger_backward,
            ActionIndexCache.act_stagger_backward_3,
            ActionIndexCache.Create("act_scared_to_normal_2"),
            ActionIndexCache.Create("act_strike_fall_front_back_rise"),
            ActionIndexCache.act_strike_fall_back_back_rise
        };

        private static int IndexOf(in ActionIndexCache action)
        {
            for (int i = 0; i < Actions.Length; i++)
            {
                if (action == Actions[i])
                {
                    return i;
                }
            }
            return -1;
        }

        // Client: play a reaction the server started.
        internal static void Play(StanceAnimation message)
        {
            if (message.Action < 0 || message.Action >= Actions.Length)
            {
                return;
            }
            Agent agent = Mission.MissionNetworkHelper.GetAgentFromIndex(message.AgentIndex, canBeNull: true);
            if (agent == null || !agent.IsActive())
            {
                return;
            }
            AnimFlags flags = 0;
            if (message.PriorityStruck)
            {
                flags |= AnimFlags.amf_priority_striked;
            }
            if (message.LockMovement)
            {
                flags |= AnimFlags.anf_lock_movement;
            }
            agent.SetActionChannel(message.Channel, Actions[message.Action], message.IgnorePriority, flags,
                actionSpeed: message.ActionSpeed, startProgress: message.StartProgress);
        }

        // Server only (patched on the dedicated server).
        [HarmonyPatch(typeof(Agent), nameof(Agent.SetActionChannel))]
        private static class SetActionChannelPatch
        {
            private static void Postfix(Agent __instance, bool __result, int channelNo, in ActionIndexCache actionIndexCache,
                bool ignorePriority, AnimFlags additionalFlags, float actionSpeed, float startProgress)
            {
                // Only in Commander Battle missions, the only ones whose clients register the handler.
                if (!__result || !GameNetwork.IsServer || !StanceNetworkSync.IsActive || channelNo < 0 || channelNo > 1)
                {
                    return;
                }
                int action = IndexOf(actionIndexCache);
                if (action < 0)
                {
                    return;
                }
                GameNetwork.BeginBroadcastModuleEvent();
                GameNetwork.WriteMessage(new StanceAnimation(__instance.Index, action, channelNo, ignorePriority,
                    (additionalFlags & AnimFlags.amf_priority_striked) != 0, (additionalFlags & AnimFlags.anf_lock_movement) != 0,
                    actionSpeed, startProgress));
                GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
            }
        }
    }
}
