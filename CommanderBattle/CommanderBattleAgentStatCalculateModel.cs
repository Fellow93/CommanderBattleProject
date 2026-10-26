using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    // The stock multiplayer stats, with slower horses (HorseSpeedFactor) and slower-firing bows and crossbows
    // (BowFireSpeedFactor, CrossbowFireSpeedFactor) in Commander Battle missions. Agent stats are computed on every
    // machine and never synced, so this is registered on the server and every client alike; a change on one side
    // only would make riders rubber-band and shots mistimed. Other game modes keep the stock values.
    public class CommanderBattleAgentStatCalculateModel : MultiplayerAgentStatCalculateModel
    {
        private Mission _checkedMission;

        private bool _isCommanderBattle;

        public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            base.UpdateAgentStats(agent, agentDrivenProperties);
            if (!IsCommanderBattle(agent.Mission))
            {
                return;
            }
            if (agent.IsMount)
            {
                agentDrivenProperties.MountSpeed *= CommanderBattleSettings.HorseSpeedFactor;
            }
            else if (agent.IsHuman)
            {
                SlowRangedFire(agent, agentDrivenProperties);
            }
        }

        // The draw (bow) or raise (crossbow) runs at ThrustOrRangedReadySpeedMultiplier and taking the next bolt or
        // arrow at ReloadSpeed. Both follow the wielded weapon: the stats are rebuilt whenever the agent wields
        // another, so a melee weapon keeps its thrust speed.
        private static void SlowRangedFire(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            EquipmentIndex index = agent.GetPrimaryWieldedItemIndex();
            if (index == EquipmentIndex.None)
            {
                return;
            }
            WeaponComponentData weapon = agent.Equipment[index].CurrentUsageItem;
            float factor;
            if (weapon?.WeaponClass == WeaponClass.Bow)
            {
                factor = CommanderBattleSettings.BowFireSpeedFactor;
            }
            else if (weapon?.WeaponClass == WeaponClass.Crossbow)
            {
                factor = CommanderBattleSettings.CrossbowFireSpeedFactor;
            }
            else
            {
                return;
            }
            agentDrivenProperties.ThrustOrRangedReadySpeedMultiplier *= factor;
            agentDrivenProperties.ReloadSpeed *= factor;
        }

        private bool IsCommanderBattle(Mission mission)
        {
            if (mission != _checkedMission)
            {
                _checkedMission = mission;
                _isCommanderBattle = mission?.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>() != null;
            }
            return _isCommanderBattle;
        }
    }
}
