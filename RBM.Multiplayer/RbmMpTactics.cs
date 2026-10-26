using System;
using RBMAI;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace RBM.Multiplayer
{
    // RBM's field-battle tactics on Commander Battle's teams. RBM registers them from a MissionCombatantsLogic.EarlyStart
    // postfix (RBMAI.Tactics.EarlyStartPatch), which a multiplayer mission does not have; this does the same, with the
    // same option lists, once per round (a round reuses the teams, and a tactic object remembers its battle).
    // Commander Battle leaves its own tactic out when RBM.Multiplayer is loaded.
    internal static class RbmMpTactics
    {
        public static void Register(Mission mission)
        {
            foreach (Team team in mission.Teams)
            {
                if (!team.HasTeamAi || (team.Side != BattleSideEnum.Attacker && team.Side != BattleSideEnum.Defender))
                {
                    continue;
                }
                IBattleCombatant leader = new SideCombatant(team);
                team.ClearTacticOptions();
                if (team.Side == BattleSideEnum.Attacker)
                {
                    Add(team, new RBMTacticEmbolon(team), leader);
                    Add(team, new RBMTacticAttackSplitSkirmishers(team), leader);
                    Add(team, new RBMTacticAttackSplitInfantry(team), leader);
                    Add(team, new RBMTacticAttackSplitArchers(team), leader);
                    Add(team, new TacticRangedHarrassmentOffensive(team), leader);
                    Add(team, new TacticFullScaleAttack(team), leader);
                }
                else
                {
                    Add(team, new RBMTacticDefendSplitArchers(team), leader);
                    Add(team, new TacticDefensiveEngagement(team), leader);
                    Add(team, new TacticDefensiveLine(team), leader);
                    Add(team, new RBMTacticDefendSplitInfantry(team), leader);
                    Add(team, new RBMTacticHoldChoke(team), leader);
                    Add(team, new RBMTacticReservePlug(team), leader);
                    Add(team, new RBMTacticHedgehog(team), leader);
                    Add(team, new TacticFullScaleAttack(team), leader);
                }
                // Drops last round's tactic; the new ones are chosen from once the armies are on the field.
                team.TeamAI.ResetTactic(keepCurrentTactic: false);
            }
            RbmMpLog.Detail("RBM tactics registered");
        }

        // As RBM's Tactics.EarlyStartPatch.AddTacticOption.
        private static void Add(Team team, TacticComponent tactic, IBattleCombatant leader)
        {
            ArmyScale.NoteLeader(team.Side, leader);
            team.AddTacticOption(tactic);
            TacticSelection.Register(team, tactic, leader);
        }

        // RBM weighs tactics by the side's culture and its general (TacticSelection). A Commander Battle side has a
        // culture (CultureTeam1 = attackers, CultureTeam2 = defenders, as Commander Battle reads them) and no general.
        private sealed class SideCombatant : IBattleCombatant
        {
            private readonly Team _team;

            public SideCombatant(Team team)
            {
                _team = team;
                MultiplayerOptions.OptionType option = team.Side == BattleSideEnum.Attacker ? MultiplayerOptions.OptionType.CultureTeam1 : MultiplayerOptions.OptionType.CultureTeam2;
                BasicCulture = MBObjectManager.Instance.GetObject<BasicCultureObject>(option.GetStrValue());
            }

            public TextObject Name => new TextObject(_team.Side.ToString());

            public BattleSideEnum Side => _team.Side;

            public BasicCultureObject BasicCulture { get; }

            public BasicCharacterObject General => null;

            public Tuple<uint, uint> PrimaryColorPair => new Tuple<uint, uint>(_team.Color, _team.Color2);

            public Banner Banner => _team.Banner;

            public BattleEnvironment CurrentBattleEnvironment => BattleEnvironment.Land;

            public int GetTacticsSkillAmount()
            {
                return 0;
            }

            public int GetNumberOfMissionReadyTroops()
            {
                return _team.ActiveAgents.Count;
            }

            public bool IsUnderPlayersCommand(BattleSideEnum playerSide)
            {
                return false;
            }
        }
    }
}
