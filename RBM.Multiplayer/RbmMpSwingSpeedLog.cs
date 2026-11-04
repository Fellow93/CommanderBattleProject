using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer
{
    // What sets a player's melee speed, to compare with singleplayer: the wielded item's own speeds, RBM's per-agent
    // speeds (the same call as RangedRework.OverrideWeaponEquipped, which RbmMpEquipWield hands to the engine) and the
    // final multipliers. Logged from PlayerStatGuard on the server, after the stat rebuild, because MissionPeer is not
    // set yet when the spawn equipment is equipped. Once per agent and change: stats are rebuilt often.
    internal static class RbmMpSwingSpeedLog
    {
        private static readonly Dictionary<int, string> LastLine = new Dictionary<int, string>();

        public static void Log(Agent agent, AgentDrivenProperties properties)
        {
            if (!RbmMpLog.SwingSpeeds)
            {
                return;
            }
            EquipmentIndex index = agent.GetPrimaryWieldedItemIndex();
            if (index == EquipmentIndex.None)
            {
                return;
            }
            MissionWeapon weapon = agent.Equipment[index];
            WeaponComponentData usage = weapon.CurrentUsageItem;
            if (weapon.Item == null || usage == null || usage.IsRangedWeapon)
            {
                return;
            }
            int skill = usage.RelevantSkill != null ? MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(agent, usage.RelevantSkill) : 0;
            string rbm = "mp_ item, stock";
            if (!weapon.Item.StringId.StartsWith("mp_"))
            {
                RBMCombat.Utilities.CalculateVisualSpeeds(new EquipmentElement(weapon.Item), weapon.CurrentUsageIndex, RBMConfig.SkillDamage.MeleeSkillDR(skill),
                    out int swing, out int thrust, out int handling);
                rbm = $"rbm sp {swing}/{thrust} hd {handling}";
            }
            string line = $"{agent.Character?.StringId} {weapon.Item.StringId} {usage.WeaponClass} skill {skill}: item sp {usage.SwingSpeed}/{usage.ThrustSpeed} hd {usage.Handling}, " +
                $"{rbm}, mult swing {properties.SwingSpeedMultiplier:0.000} thrust {properties.ThrustOrRangedReadySpeedMultiplier:0.000} handling {properties.HandlingMultiplier:0.000}" +
                (agent.HasMount ? ", mounted" : "");
            // SetAiRelatedProperties runs on the parallel stat update.
            lock (LastLine)
            {
                if (LastLine.TryGetValue(agent.Index, out string last) && last == line)
                {
                    return;
                }
                LastLine[agent.Index] = line;
            }
            RbmMpLog.Write($"Swing speed: {agent.MissionPeer?.Name} {line}");
        }
    }
}
