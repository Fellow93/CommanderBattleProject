using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    // The stock multiplayer stats, with slower horses in Commander Battle missions (HorseSpeedFactor). Agent stats
    // are computed on every machine and never synced, so this is registered on the server and every client alike;
    // a change on one side only would make riders rubber-band. Other game modes keep the stock values.
    public class CommanderBattleAgentStatCalculateModel : MultiplayerAgentStatCalculateModel
    {
        private Mission _checkedMission;

        private bool _isCommanderBattle;

        public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            base.UpdateAgentStats(agent, agentDrivenProperties);
            if (agent.IsMount && IsCommanderBattle(agent.Mission))
            {
                agentDrivenProperties.MountSpeed *= CommanderBattleSettings.HorseSpeedFactor;
            }
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
