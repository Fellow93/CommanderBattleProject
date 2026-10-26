using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace RBM.Multiplayer
{
    // Applies RBM AI's and RBM Stance's Harmony patches on the dedicated server, one class at a time (as RBM's own
    // RBMAiPatcher.DoPatching does), so one failing class is logged and skipped instead of stopping the rest.
    // RBM's patchers are not used: they patch every class of their assembly, and this assembly holds both modules.
    // Each module keeps its own Harmony id, which RBM's [HarmonyAfter] ordering refers to.
    public static class RbmMpPatcher
    {
        public const string AiHarmonyId = "com.rbmai";

        public const string StanceHarmonyId = "com.rbmstance";

        public const string MultiplayerHarmonyId = "com.rbm.multiplayer";

        // RBM patch classes that do not apply to a multiplayer mission.
        private static readonly HashSet<string> Skipped = new HashSet<string>
        {
            // Singleplayer agent stat model (absent in multiplayer; compiled against a stub).
            "RBMAI.AgentAi+SetWeaponSkillEffectsOnAgentPatch",
            // RBM's entry points into a singleplayer battle: RbmMpTactics registers the tactics, and
            // PatchCommanderBattleTeamAI gives Commander Battle's formations RBM's behaviours.
            "RBMAI.Tactics+OverrideTeamAIGeneral",
            "RBMAI.Tactics+EarlyStartPatch"
        };

        // Commander Battle's team AI replaces the stock TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime, where
        // RBM adds its behaviours to each formation; RBM's own postfix goes on Commander Battle's override instead.
        private const string CommanderBattleTeamAIType = "CommanderBattle.AI.CommanderBattleTeamAI";

        private static bool _patched;

        public static void PatchAll()
        {
            if (_patched)
            {
                return;
            }
            _patched = true;
            Harmony ai = new Harmony(AiHarmonyId);
            Harmony stance = new Harmony(StanceHarmonyId);
            Harmony multiplayer = new Harmony(MultiplayerHarmonyId);
            int applied = 0;
            int failed = 0;
            int skipped = 0;
            // Patches on small Agent methods go first. Patching a method compiles its replacement at once, and the JIT
            // inlines a small callee such as SetActionChannel into it unless that callee is already patched: a stagger
            // started from such a replacement then bypasses StanceAnimationSync and never reaches the clients, and a
            // Frontline release bypasses TargetFrameClearSync, and RBM's ranged tick bypasses AmmoLimitGuard.
            IEnumerable<Type> types = AccessTools.GetTypesFromAssembly(typeof(RbmMpPatcher).Assembly)
                .Where(type => type.HasHarmonyAttribute())
                .OrderBy(type => PatchesSmallAgentMethod(type) ? 0 : 1);
            foreach (Type type in types)
            {
                string name = type.FullName;
                Harmony harmony = GetHarmony(type, ai, stance, multiplayer);
                if (harmony == null || Skipped.Contains(name))
                {
                    skipped++;
                    continue;
                }
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception e)
                {
                    failed++;
                    RbmMpLog.Write($"Patch {name} failed: {e}");
                }
            }
            RbmMpLog.Write($"Patches: {applied} classes applied, {failed} failed, {skipped} skipped");
            if (RBMConfig.RBMConfig.rbmAiEnabled)
            {
                PatchCommanderBattleTeamAI(ai);
            }
        }

        private static void PatchCommanderBattleTeamAI(Harmony ai)
        {
            try
            {
                MethodInfo target = AccessTools.Method(AccessTools.TypeByName(CommanderBattleTeamAIType), "OnUnitAddedToFormationForTheFirstTime");
                MethodInfo postfix = AccessTools.Method(AccessTools.Inner(typeof(RBMAI.Tactics), "OverrideTeamAIGeneral"), "PostfixOnUnitAddedToFormationForTheFirstTime");
                if (target == null || postfix == null)
                {
                    RbmMpLog.Write($"RBM behaviours not added to Commander Battle's formations: {(target == null ? CommanderBattleTeamAIType + " not found" : "RBM's postfix not found")}");
                    return;
                }
                ai.Patch(target, postfix: new HarmonyMethod(postfix));
                RbmMpLog.Detail("RBM behaviours added to Commander Battle's team AI");
            }
            catch (Exception e)
            {
                RbmMpLog.Write($"RBM behaviours not added to Commander Battle's formations: {e}");
            }
        }

        private static readonly HashSet<string> SmallAgentMethods = new HashSet<string>
        {
            nameof(TaleWorlds.MountAndBlade.Agent.SetActionChannel),
            nameof(TaleWorlds.MountAndBlade.Agent.ClearTargetFrame),
            nameof(TaleWorlds.MountAndBlade.Agent.SetTargetPositionSynched),
            nameof(TaleWorlds.MountAndBlade.Agent.SetTargetPositionAndDirectionSynched),
            nameof(TaleWorlds.MountAndBlade.Agent.SetScriptedCombatFlags)
        };

        private static bool PatchesSmallAgentMethod(Type type)
        {
            HarmonyMethod target = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(type));
            return target.declaringType == typeof(TaleWorlds.MountAndBlade.Agent) && SmallAgentMethods.Contains(target.methodName);
        }

        // RBM AI's classes include a few in the global namespace (RBM's own tactics); everything else is namespaced.
        private static Harmony GetHarmony(Type type, Harmony ai, Harmony stance, Harmony multiplayer)
        {
            string ns = type.Namespace ?? string.Empty;
            if (ns.StartsWith("RBM.Multiplayer", StringComparison.Ordinal))
            {
                return multiplayer;
            }
            if (ns.StartsWith("RBMStance", StringComparison.Ordinal))
            {
                return RBMConfig.RBMConfig.rbmStanceEnabled ? stance : null;
            }
            if (ns.Length == 0 || ns.StartsWith("RBMAI", StringComparison.Ordinal))
            {
                return RBMConfig.RBMConfig.rbmAiEnabled ? ai : null;
            }
            RbmMpLog.Detail($"Patch {type.FullName}: no module for namespace '{ns}', skipped");
            return null;
        }
    }
}
