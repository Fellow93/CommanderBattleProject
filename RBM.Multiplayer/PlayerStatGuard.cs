using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer
{
    // RBM AI (AgentStats) and RBM Stance (StanceStatsPatch, stamina) adjust an agent's driven properties in postfixes
    // on SetAiRelatedProperties. Driven properties are never sent over the network: each client computes its own for
    // the agent it controls and predicts its movement and attacks from them. If the server changed a player's values,
    // the player's client would disagree with the server (rubber-banding, mistimed attacks). So the values are put
    // back for every player-controlled agent, and RBM's stat changes only apply to the AI-led soldiers.
    internal static class PlayerStatGuard
    {
        // SetAiRelatedProperties runs on the parallel stat update, so the snapshot is per thread.
        [ThreadStatic]
        private static float[] _snapshot;

        [ThreadStatic]
        private static Agent _snapshotAgent;

        private static bool IsPlayerControlled(Agent agent)
        {
            return agent != null && agent.MissionPeer != null;
        }

        [HarmonyPatch(typeof(AgentStatCalculateModel), "SetAiRelatedProperties")]
        private static class SnapshotBeforeRbm
        {
            [HarmonyPriority(Priority.First)]
            private static void Postfix(Agent agent, AgentDrivenProperties agentDrivenProperties)
            {
                if (!IsPlayerControlled(agent))
                {
                    return;
                }
                float[] snapshot = _snapshot ??= new float[(int)DrivenProperty.Count];
                for (int i = 0; i < snapshot.Length; i++)
                {
                    snapshot[i] = agentDrivenProperties.GetStat((DrivenProperty)i);
                }
                _snapshotAgent = agent;
            }
        }

        [HarmonyPatch(typeof(AgentStatCalculateModel), "SetAiRelatedProperties")]
        private static class RestoreAfterRbm
        {
            [HarmonyPriority(Priority.Last)]
            [HarmonyAfter(RbmMpPatcher.AiHarmonyId, RbmMpPatcher.StanceHarmonyId)]
            private static void Postfix(Agent agent, AgentDrivenProperties agentDrivenProperties)
            {
                if (_snapshotAgent != agent || agent == null)
                {
                    return;
                }
                _snapshotAgent = null;
                float[] snapshot = _snapshot;
                for (int i = 0; i < snapshot.Length; i++)
                {
                    agentDrivenProperties.SetStat((DrivenProperty)i, snapshot[i]);
                }
            }
        }
    }
}
