using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle.AI
{
    // The singleplayer field-battle team AI. On a server the stock TeamAIGeneral only gives formations the
    // multiplayer bot behaviours; this one gives them the singleplayer set (the stock singleplayer list, which
    // the stock tactics and RBM's expect to find), plus BehaviorAdvanceInLine for CommanderBattleTactic.
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
            ai.AddAiBehavior(new BehaviorAdvance(formation));
            ai.AddAiBehavior(new BehaviorCautiousAdvance(formation));
            ai.AddAiBehavior(new BehaviorCavalryScreen(formation));
            ai.AddAiBehavior(new BehaviorDefend(formation));
            ai.AddAiBehavior(new BehaviorDefensiveRing(formation));
            ai.AddAiBehavior(new BehaviorFireFromInfantryCover(formation));
            ai.AddAiBehavior(new BehaviorFlank(formation));
            ai.AddAiBehavior(new BehaviorHoldHighGround(formation));
            ai.AddAiBehavior(new BehaviorHorseArcherSkirmish(formation));
            ai.AddAiBehavior(new BehaviorMountedSkirmish(formation));
            ai.AddAiBehavior(new BehaviorProtectFlank(formation));
            ai.AddAiBehavior(new BehaviorScreenedSkirmish(formation));
            ai.AddAiBehavior(new BehaviorSkirmish(formation));
            ai.AddAiBehavior(new BehaviorSkirmishBehindFormation(formation));
            ai.AddAiBehavior(new BehaviorSkirmishLine(formation));
            ai.AddAiBehavior(new BehaviorVanguard(formation));
            ai.AddAiBehavior(new BehaviorShootFromCliff(formation));
            // As the stock TeamAIGeneral gives formation 8, the general's.
            if (formation.FormationIndex == FormationClass.General)
            {
                ai.AddAiBehavior(new BehaviorGeneral(formation));
            }
        }

        // An AI-led general's formation as singleplayer leads one (GeneralsAndCaptainsAssignmentLogic): the default
        // weights and BehaviorGeneral, which keeps it behind the main formation. No tactic sets weights for formation
        // 8 (they only see 0-7), and a weight left at 0 is never picked, so this runs when the AI general spawns and
        // when a player general's formation goes to the AI. The weights hold until FormationAI is recreated
        // (Formation.Reset, between rounds). Returns false when the formation lacks the behaviours.
        public static bool LeadAsGeneral(Formation formation)
        {
            if (formation?.AI == null || formation.AI.GetBehavior<BehaviorGeneral>() == null || formation.AI.GetBehavior<BehaviorCharge>() == null)
            {
                return false;
            }
            formation.AI.ResetBehaviorWeights();
            TacticComponent.SetDefaultBehaviorWeights(formation);
            formation.AI.SetBehaviorWeight<BehaviorGeneral>(1f);
            return true;
        }
    }
}
