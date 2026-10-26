using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using NetworkMessages.FromServer;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer.AI
{
    // On a server, Agent.ClearTargetFrame sends every client a ClearAgentTargetFrame message whenever the agent is held in
    // place. Two problems with RBM's Frontline:
    // - It holds soldiers with the native SetTargetPosition, which clients never hear of, and releases them all the time:
    //   about 900 messages a second in melee, every one for a hold the client doesn't have. Only the ...Synched setters
    //   send a hold to clients, so only agents held through them get the message.
    // - It releases its holds on the formation-movement workers (the GetOrderPositionOfUnit prefix, the
    //   HumanAIComponent tick postfix), so several threads would write the one native broadcast at once. Singleplayer
    //   never broadcasts, which is why RBM gets away with it there. Off the main thread the hold is released on the spot,
    //   as in singleplayer, and the message waits for the main thread (Flush, each mission tick).
    // Server only.
    internal static class TargetFrameClearSync
    {
        private static int _mainThreadId = -1;

        private static AccessTools.FieldRef<Agent, bool> _checkIfTargetFrameIsChanged;

        private static Action<Agent> _clearTargetFrameAux;

        // Agents whose hold the clients were sent and not yet released. Read and written on the workers too.
        private static readonly ConcurrentDictionary<Agent, byte> HeldOnClients = new ConcurrentDictionary<Agent, byte>();

        // Agents released off the main thread whose message is not sent yet. Written on the workers.
        private static readonly ConcurrentQueue<Agent> Pending = new ConcurrentQueue<Agent>();

        private static readonly HashSet<Agent> SentThisTick = new HashSet<Agent>();

        // Main thread only.
        internal static int SentThisRound { get; private set; }

        // Releases of holds the clients never had, so not sent. Counted on the workers too.
        private static long _skippedThisRound;

        internal static long SkippedThisRound => Interlocked.Read(ref _skippedThisRound);

        // Main thread: sends the messages for the holds released since the last tick, once per agent. A dead agent's
        // hold no longer matters, and the client may have no agent left at that index.
        internal static void Flush()
        {
            while (Pending.TryDequeue(out Agent agent))
            {
                if (!agent.IsActive() || !SentThisTick.Add(agent))
                {
                    continue;
                }
                Broadcast(agent);
            }
            SentThisTick.Clear();
        }

        // At mission start and when a round's scene is cleared (RbmMpServerLogic.ResetRbmState): its agents are gone.
        internal static void Reset()
        {
            while (Pending.TryDequeue(out _))
            {
            }
            HeldOnClients.Clear();
            SentThisRound = 0;
            Interlocked.Exchange(ref _skippedThisRound, 0);
        }

        private static void Broadcast(Agent agent)
        {
            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(new ClearAgentTargetFrame(agent.Index));
            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.AddToMissionRecord);
            SentThisRound++;
        }

        // A hold the clients are sent. Marked even when the setter returns early (already held at that target): that
        // costs at most one needless release.
        [HarmonyPatch(typeof(Agent), nameof(Agent.SetTargetPositionSynched))]
        private static class HoldPositionSynched
        {
            private static void Prefix(Agent __instance)
            {
                if (GameNetwork.IsServerOrRecorder)
                {
                    HeldOnClients[__instance] = 0;
                }
            }
        }

        [HarmonyPatch(typeof(Agent), nameof(Agent.SetTargetPositionAndDirectionSynched))]
        private static class HoldPositionAndDirectionSynched
        {
            private static void Prefix(Agent __instance)
            {
                if (GameNetwork.IsServerOrRecorder)
                {
                    HeldOnClients[__instance] = 0;
                }
            }
        }

        [HarmonyPatch(typeof(Agent), nameof(Agent.ClearTargetFrame))]
        private static class ClearTargetFrame
        {
            // Run by the patcher, on the main thread. Without the private members the patch is left out, not half applied.
            private static bool Prepare()
            {
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                FieldInfo checkField = AccessTools.Field(typeof(Agent), "_checkIfTargetFrameIsChanged");
                MethodInfo clearAux = AccessTools.Method(typeof(Agent), "ClearTargetFrameAux");
                if (checkField == null || clearAux == null)
                {
                    RbmMpLog.Write("Agent.ClearTargetFrame not patched: its private members were not found; every hold release is broadcast, from worker threads too");
                    return false;
                }
                _checkIfTargetFrameIsChanged = AccessTools.FieldRefAccess<Agent, bool>(checkField);
                _clearTargetFrameAux = AccessTools.MethodDelegate<Action<Agent>>(clearAux);
                return true;
            }

            // Agent.ClearTargetFrame with its broadcast only for holds the clients have, and queued off the main thread.
            private static bool Prefix(Agent __instance)
            {
                if (!GameNetwork.IsServerOrRecorder)
                {
                    return true;
                }
                _checkIfTargetFrameIsChanged(__instance) = false;
                if (__instance.MovementLockedState == AgentMovementLockedState.None)
                {
                    return false;
                }
                _clearTargetFrameAux(__instance);
                if (!HeldOnClients.TryRemove(__instance, out _))
                {
                    Interlocked.Increment(ref _skippedThisRound);
                    return false;
                }
                if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
                {
                    Broadcast(__instance);
                }
                else
                {
                    Pending.Enqueue(__instance);
                }
                return false;
            }
        }
    }
}
