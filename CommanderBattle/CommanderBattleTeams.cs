using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace CommanderBattle
{
    // One team per side: Mission.AttackerTeam / Mission.DefenderTeam, each holding the side's formations.
    public static class CommanderBattleTeams
    {
        public static Team GetTeam(Mission mission, BattleSideEnum side)
        {
            return side == BattleSideEnum.Attacker ? mission.AttackerTeam : mission.DefenderTeam;
        }

        public static BasicCultureObject GetCulture(BattleSideEnum side)
        {
            MultiplayerOptions.OptionType option = side == BattleSideEnum.Attacker ? MultiplayerOptions.OptionType.CultureTeam1 : MultiplayerOptions.OptionType.CultureTeam2;
            return MBObjectManager.Instance.GetObject<BasicCultureObject>(option.GetStrValue());
        }

        public static int CountActiveSoldiers(Mission mission, BattleSideEnum side)
        {
            int count = 0;
            foreach (Team team in mission.Teams)
            {
                if (team.Side != side)
                {
                    continue;
                }
                foreach (Agent agent in team.ActiveAgents)
                {
                    if (agent.IsHuman)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
