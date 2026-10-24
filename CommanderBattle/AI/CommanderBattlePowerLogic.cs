using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle.AI
{
    // The singleplayer behaviours weigh their options by the team's remaining power ratio, which the
    // query system works out as (total power - casualty power loss) per side. The stock
    // BattlePowerCalculationLogic reads the total from the singleplayer spawn logic, which multiplayer
    // missions do not have, and computes it once per mission. Here the total is the power of the soldiers
    // still standing plus everything CasualtyHandler has counted as lost, so the remaining power is simply
    // that of the soldiers still standing, in every round.
    public class CommanderBattlePowerLogic : MissionLogic, IBattlePowerCalculationLogic
    {
        private CasualtyHandler _casualtyHandler;

        public float GetTotalTeamPower(Team team)
        {
            if (_casualtyHandler == null)
            {
                _casualtyHandler = Mission.GetMissionBehavior<CasualtyHandler>();
            }
            float power = 0f;
            foreach (Formation formation in team.FormationsIncludingSpecialAndEmpty)
            {
                if (formation.CountOfUnits > 0)
                {
                    power += formation.GetFormationPower();
                }
                if (_casualtyHandler != null)
                {
                    power += _casualtyHandler.GetCasualtyPowerLossOfFormation(formation);
                }
            }
            return power;
        }
    }
}
