using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace CommanderBattle
{
    // One team per side: Mission.AttackerTeam / Mission.DefenderTeam, each holding the side's formations.
    public static class CommanderBattleTeams
    {
        // Both sides, defenders first: the order the slot table, the troop counts message and the logs use.
        public static readonly BattleSideEnum[] Sides = { BattleSideEnum.Defender, BattleSideEnum.Attacker };

        // Client: the side our own peer is on, None while choosing or spectating.
        public static BattleSideEnum MySide => (GameNetwork.IsMyPeerReady ? GameNetwork.MyPeer.GetComponent<MissionPeer>()?.Team?.Side : null) ?? BattleSideEnum.None;

        public static Team GetTeam(Mission mission, BattleSideEnum side)
        {
            return side == BattleSideEnum.Attacker ? mission.AttackerTeam : mission.DefenderTeam;
        }

        public static BasicCultureObject GetCulture(BattleSideEnum side)
        {
            MultiplayerOptions.OptionType option = side == BattleSideEnum.Attacker ? MultiplayerOptions.OptionType.CultureTeam1 : MultiplayerOptions.OptionType.CultureTeam2;
            return MBObjectManager.Instance.GetObject<BasicCultureObject>(option.GetStrValue());
        }

        // Server: the side's general, player or AI. RBM's reactions skip Team.GeneralsFormation as a responder. Team.
        // BodyGuardFormation stays null: TeamAIComponent.Tick would reset a set one to Charge every tick. Neither is
        // networked, and Team.Reset doesn't clear them (ClearGeneral, each round).
        public static void SetGeneral(Team team, Formation formation, Agent general)
        {
            if (team == null)
            {
                return;
            }
            team.GeneralsFormation = formation;
            team.GeneralAgent = general;
        }

        public static void ClearGeneral(Team team)
        {
            SetGeneral(team, null, null);
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
