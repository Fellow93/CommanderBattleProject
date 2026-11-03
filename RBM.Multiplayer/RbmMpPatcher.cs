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

        // RBM Stance orders some of its patches after RBM Combat's ([HarmonyAfter("com.rbmcombat")]).
        public const string CombatHarmonyId = "com.rbmcombat";

        public const string MultiplayerHarmonyId = "com.rbm.multiplayer";

        // RBM patch classes that do not apply to a multiplayer mission.
        private static readonly HashSet<string> Skipped = new HashSet<string>
        {
            // Singleplayer agent stat model (absent in multiplayer; compiled against a stub).
            "RBMAI.AgentAi+SetWeaponSkillEffectsOnAgentPatch",
            // RBM's entry points into a singleplayer battle: RbmMpTactics registers the tactics, and
            // PatchCommanderBattleTeamAI gives Commander Battle's formations RBM's behaviours.
            "RBMAI.Tactics+OverrideTeamAIGeneral",
            "RBMAI.Tactics+EarlyStartPatch",
            // RBM Combat (RBM mode). Client-side visuals, which a server patch never shows: the thicker flying arrows
            // and the repeating crossbow's crosshair.
            "RBMCombat.RangedRework+FlyingArrowThicknessPatch",
            "RBMCombat.RangedRework+RepeatingCrossbowCrosshair",
            // Weapon swing, thrust and draw speeds: every machine simulates these, so RbmMpEquipWield runs RBM's prefix
            // on the server and the clients alike.
            "RBMCombat.RangedRework+OverrideWeaponEquipped",
            // A sheathed bow's arrow put back in the quiver by hand: ammo changed on each machine on its own, outside the
            // server's ammo messages. The engine's own unload-when-sheathed (set at equip) applies instead.
            "RBMCombat.RangedRework+OnWieldedItemIndexChangePatch",
            "RBMCombat.RangedRework+ProcessPendingBowUnloadsPatch",
            // BipedalRadius 0.48 and the stick threshold, set when a mission finishes loading: a client finishes
            // before it learns the mode, and the radius must match on every machine. The radius is loaded from
            // Module\ModuleData\rbm_mp_core_parameters.xml instead, everywhere and in both modes.
            "RBMCombat.RangedRework+MissionLoadChangeParameters",
            // RBM's reload speed is in CommanderBattleAgentStatCalculateModel (RBM mode), which every machine runs; a
            // server-only one desyncs a player's reload.
            "RBMCombat.RangedRework+OverrideSetAiRelatedProperties"
        };

        // The game also builds each crafted weapon's thrust damage with these when it loads the items. Applied before
        // that, the server built RBM's numbers while clients built the stock ones (304 weapons apart, mp_ ones too), so
        // they go on once the items are built (PatchAfterItemLoad). RBM's blows don't read the items' thrust damage.
        private static readonly HashSet<string> AfterItemLoad = new HashSet<string>
        {
            "RBMCombat.MagnitudeChanges+ChangeLanceDamage",
            "RBMCombat.MagnitudeChanges+CalculateStrikeMagnitudeForThrustPatch"
        };

        private static readonly List<Type> Deferred = new List<Type>();

        // Light mode's damage curve, and the kick/bash minimum it needs, give way to RBM Combat's own damage model.
        private static readonly HashSet<string> SkippedWithRbmCombat = new HashSet<string>
        {
            "RBM.Multiplayer.DamageCurve+CalculateDamagePatch",
            "RBM.Multiplayer.DamageCurve+AlternativeAttackMinimumPatch"
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
            Harmony combat = new Harmony(CombatHarmonyId);
            Harmony multiplayer = new Harmony(MultiplayerHarmonyId);
            bool rbmCombat = RBMConfig.RBMConfig.rbmCombatEnabled;
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
                Harmony harmony = GetHarmony(type, ai, stance, combat, multiplayer);
                if (harmony == null || Skipped.Contains(name) || (rbmCombat && SkippedWithRbmCombat.Contains(name)))
                {
                    skipped++;
                    continue;
                }
                if (AfterItemLoad.Contains(name))
                {
                    Deferred.Add(type);
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
            // Both sides, for RBM's items only (so in either mode); clients patch it in RbmMpSubModule.
            RbmMpEquipWield.Patch();
            RbmMpLog.Write($"Patches: {applied} classes applied, {failed} failed, {skipped} skipped, {Deferred.Count} after item load; RBM Combat {(rbmCombat ? "on" : "off")}");
            if (RBMConfig.RBMConfig.rbmAiEnabled)
            {
                PatchCommanderBattleTeamAI(ai);
            }
        }

        public static void PatchAfterItemLoad()
        {
            int failed = 0;
            foreach (Type type in Deferred)
            {
                try
                {
                    new Harmony(CombatHarmonyId).CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    failed++;
                    RbmMpLog.Write($"Patch {type.FullName} failed: {e}");
                }
            }
            if (Deferred.Count > 0)
            {
                RbmMpLog.Write($"Patches after item load: {Deferred.Count - failed} applied, {failed} failed");
            }
            Deferred.Clear();
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

        // NetworkRangeGuard's packet writers are as small, and every message calls them.
        private static bool PatchesSmallAgentMethod(Type type)
        {
            HarmonyMethod target = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(type));
            return (target.declaringType == typeof(TaleWorlds.MountAndBlade.Agent) && SmallAgentMethods.Contains(target.methodName))
                || target.declaringType == typeof(TaleWorlds.MountAndBlade.Network.Messages.GameNetworkMessage);
        }

        // RBM AI's classes include a few in the global namespace (RBM's own tactics); everything else is namespaced.
        private static Harmony GetHarmony(Type type, Harmony ai, Harmony stance, Harmony combat, Harmony multiplayer)
        {
            string ns = type.Namespace ?? string.Empty;
            if (ns.StartsWith("RBM.Multiplayer", StringComparison.Ordinal))
            {
                return multiplayer;
            }
            // RBM Combat only in RBM mode (RbmMpSettings): its numbers assume RBM's items.
            if (ns.StartsWith("RBMCombat", StringComparison.Ordinal))
            {
                return RBMConfig.RBMConfig.rbmCombatEnabled ? combat : null;
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
