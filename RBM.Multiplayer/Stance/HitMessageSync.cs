using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RBM.Multiplayer.Network;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer.Stance
{
    // RBM Combat's "Face hit!" and "Under shoulder hit!" (DamageRework.OverrideDamageCalc, on the server, where the
    // blows are decided) are InformationManager lines, which never reach a client. Each such call is swapped for a
    // StanceEvent to the player RBM meant it for: its message text says which (RBM_COM_005/007 to the attacker,
    // 006/008 to the victim).
    internal static class HitMessageSync
    {
        private static readonly MethodInfo DisplayMessage = AccessTools.Method(typeof(InformationManager), nameof(InformationManager.DisplayMessage), new[] { typeof(InformationMessage) });

        // The blow RBM's prefix is working on. Main thread only.
        private static Agent _attacker;

        private static Agent _victim;

        [HarmonyPatch(typeof(RBMCombat.DamageRework.OverrideDamageCalc), "Prefix")]
        private static class OverrideDamageCalcPatch
        {
            private static void Prefix(ref AttackInformation attackInformation)
            {
                _attacker = attackInformation.AttackerAgent;
                _victim = attackInformation.VictimAgent;
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                string text = null;
                int replaced = 0;
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.opcode == OpCodes.Ldstr)
                    {
                        text = (string)instruction.operand;
                    }
                    else if (instruction.Calls(DisplayMessage) && Replacement(text) is string replacement)
                    {
                        instruction.operand = AccessTools.Method(typeof(HitMessageSync), replacement);
                        replaced++;
                    }
                    yield return instruction;
                }
                if (replaced != 4)
                {
                    RbmMpLog.Write($"Face and under-shoulder hit messages: {replaced} of RBM's 4 found, the rest stay on the server");
                }
            }
        }

        private static string Replacement(string text)
        {
            if (text == null)
            {
                return null;
            }
            if (text.StartsWith("{=RBM_COM_005}"))
            {
                return nameof(FaceHitToAttacker);
            }
            if (text.StartsWith("{=RBM_COM_006}"))
            {
                return nameof(FaceHitToVictim);
            }
            if (text.StartsWith("{=RBM_COM_007}"))
            {
                return nameof(UnderShoulderHitToAttacker);
            }
            return text.StartsWith("{=RBM_COM_008}") ? nameof(UnderShoulderHitToVictim) : null;
        }

        private static void FaceHitToAttacker(InformationMessage message) => Send(_attacker, StanceEventType.FaceHit);

        private static void FaceHitToVictim(InformationMessage message) => Send(_victim, StanceEventType.FaceHit);

        private static void UnderShoulderHitToAttacker(InformationMessage message) => Send(_attacker, StanceEventType.UnderShoulderHit);

        private static void UnderShoulderHitToVictim(InformationMessage message) => Send(_victim, StanceEventType.UnderShoulderHit);

        private static void Send(Agent agent, StanceEventType eventType)
        {
            StanceNetworkSync.Send(StanceNetworkSync.GetPlayer(agent), new StanceEvent(eventType, 0, BoneBodyPartType.None));
        }
    }
}
