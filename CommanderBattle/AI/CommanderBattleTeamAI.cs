using TaleWorlds.MountAndBlade;

namespace CommanderBattle.AI
{
    // The singleplayer field-battle team AI. On a server the stock TeamAIGeneral only gives formations the
    // multiplayer bot behaviours; this one gives them the singleplayer line, skirmish and flanking
    // behaviours that CommanderBattleTactic switches between.
    public class CommanderBattleTeamAI : TeamAIGeneral
    {
        public CommanderBattleTeamAI(Mission mission, Team team)
            : base(mission, team)
        {
        }

        public override void OnUnitAddedToFormationForTheFirstTime(Formation formation)
        {
            formation.ForceCalculateCaches();
            FormationAI ai = formation.AI;
            if (ai.GetBehavior<BehaviorAdvanceInLine>() != null)
            {
                return;
            }
            ai.AddAiBehavior(new BehaviorCharge(formation));
            ai.AddAiBehavior(new BehaviorPullBack(formation));
            ai.AddAiBehavior(new BehaviorRegroup(formation));
            ai.AddAiBehavior(new BehaviorReserve(formation));
            ai.AddAiBehavior(new BehaviorRetreat(formation));
            ai.AddAiBehavior(new BehaviorStop(formation));
            ai.AddAiBehavior(new BehaviorTacticalCharge(formation));
            ai.AddAiBehavior(new BehaviorAdvanceInLine(formation));
            ai.AddAiBehavior(new BehaviorCavalryScreen(formation));
            ai.AddAiBehavior(new BehaviorFlank(formation));
            ai.AddAiBehavior(new BehaviorHorseArcherSkirmish(formation));
            ai.AddAiBehavior(new BehaviorMountedSkirmish(formation));
            ai.AddAiBehavior(new BehaviorProtectFlank(formation));
            ai.AddAiBehavior(new BehaviorScreenedSkirmish(formation));
            ai.AddAiBehavior(new BehaviorSkirmish(formation));
            ai.AddAiBehavior(new BehaviorSkirmishLine(formation));
        }
    }
}
