using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using MathF = TaleWorlds.Library.MathF;

namespace CommanderBattle.AI
{
    // How the AI leads the formations nobody commands, modelled on the singleplayer full-scale attack.
    // The stock tactics merge and split formations to fit their plan (one infantry block, one archer block,
    // two cavalry wings), which would mix soldiers between commanders; this one works with the fixed
    // formations instead and only steers the AI-controlled ones:
    // - infantry march on the enemy in their own lane and charge whatever comes close,
    // - archers skirmish in front of or behind the main infantry line,
    // - cavalry guard the flanks until the lines meet, then go around and charge,
    // - horse archers harass.
    // Defenders hold their line at the start of a round and go forward after DefenderHoldTime.
    public class CommanderBattleTactic : TacticComponent
    {
        private const float DefenderHoldTime = 120f;

        private const float BattleJoinedDistance = 60f;

        private readonly List<Formation> _aiFormations = new List<Formation>();

        private Formation _mainFormation;

        private float _roundStartTime;

        private bool _holding;

        private bool _battleJoined;

        private bool _assignmentNeeded = true;

        public CommanderBattleTactic(Team team)
            : base(team)
        {
        }

        // Called by the game mode once both armies stand on the field.
        public void OnArmiesSpawned()
        {
            _roundStartTime = Mission.Current.CurrentTime;
            _holding = Team.Side == BattleSideEnum.Defender;
            _battleJoined = false;
            _assignmentNeeded = true;
            TickOccasionally();
        }

        protected override void OnApply()
        {
            base.OnApply();
            _assignmentNeeded = true;
        }

        protected override float GetTacticWeight()
        {
            return 1f;
        }

        public override void TickOccasionally()
        {
            if (Team.HasAnyFormationsIncludingSpecialThatIsNotEmpty())
            {
                bool changed = _assignmentNeeded || AiFormationsChanged();
                if (_holding && Mission.Current.CurrentTime - _roundStartTime > DefenderHoldTime)
                {
                    _holding = false;
                    changed = true;
                }
                if (!_battleJoined && IsBattleJoined())
                {
                    _battleJoined = true;
                    changed = true;
                }
                if (changed)
                {
                    _assignmentNeeded = false;
                    Assign();
                }
            }
            base.TickOccasionally();
        }

        // A formation was taken over by the AI (its commander left) or one of ours was wiped out.
        private bool AiFormationsChanged()
        {
            int count = 0;
            foreach (Formation formation in FormationsIncludingEmpty)
            {
                if (formation.CountOfUnits > 0 && formation.IsAIControlled)
                {
                    if (count >= _aiFormations.Count || _aiFormations[count] != formation)
                    {
                        return true;
                    }
                    count++;
                }
            }
            return count != _aiFormations.Count || (_mainFormation != null && _mainFormation.CountOfUnits == 0);
        }

        // The lines have met once any infantry formation of this side, player-led or not, is close to an enemy.
        private bool IsBattleJoined()
        {
            foreach (Formation formation in FormationsIncludingEmpty)
            {
                if (formation.CountOfUnits == 0 || !formation.QuerySystem.IsInfantryFormation)
                {
                    continue;
                }
                FormationQuerySystem enemy = formation.CachedClosestEnemyFormation;
                if (enemy != null && formation.CachedAveragePosition.DistanceSquared(enemy.Formation.CachedMedianPosition.AsVec2) < BattleJoinedDistance * BattleJoinedDistance)
                {
                    return true;
                }
            }
            return false;
        }

        private void Assign()
        {
            _aiFormations.Clear();
            foreach (Formation formation in FormationsIncludingEmpty)
            {
                if (formation.CountOfUnits > 0 && formation.IsAIControlled)
                {
                    _aiFormations.Add(formation);
                }
            }
            // Archers and cavalry position themselves relative to the main formation, player-led or not.
            _mainFormation = ChooseMainFormation();
            foreach (Formation formation in FormationsIncludingEmpty)
            {
                formation.AI.IsMainFormation = formation == _mainFormation;
                if (formation.CountOfUnits > 0)
                {
                    formation.AI.ResetBehaviorWeights();
                }
            }
            foreach (Formation formation in _aiFormations)
            {
                ApplyBehaviors(formation);
            }
            CommanderBattleLog.Detail($"{Team.Side} AI: {_aiFormations.Count} formations, main {_mainFormation?.FormationIndex.ToString() ?? "none"}, {(_holding ? "holding" : "advancing")}, battle {(_battleJoined ? "joined" : "not joined")}");
        }

        // The infantry formation nearest the centre of the line, the bigger one on a tie.
        private Formation ChooseMainFormation()
        {
            Formation best = null;
            float bestLateral = float.MaxValue;
            foreach (Formation formation in FormationsIncludingEmpty)
            {
                if (formation.CountOfUnits == 0 || !formation.QuerySystem.IsInfantryFormation)
                {
                    continue;
                }
                float lateral = MathF.Abs(GetLateralOffset(formation));
                if (best == null || lateral < bestLateral - 1f || (lateral < bestLateral + 1f && formation.CountOfUnits > best.CountOfUnits))
                {
                    best = formation;
                    bestLateral = lateral;
                }
            }
            return best;
        }

        private void ApplyBehaviors(Formation formation)
        {
            FormationAI ai = formation.AI;
            FormationQuerySystem query = formation.QuerySystem;
            float lateral = GetLateralOffset(formation);
            FormationAI.BehaviorSide flank = lateral < 0f ? FormationAI.BehaviorSide.Left : FormationAI.BehaviorSide.Right;
            SetDefaultBehaviorWeights(formation);
            if (query.IsRangedCavalryFormation)
            {
                ai.Side = flank;
                ai.SetBehaviorWeight<BehaviorMountedSkirmish>(1f);
                ai.SetBehaviorWeight<BehaviorHorseArcherSkirmish>(1f);
            }
            else if (query.IsCavalryFormation)
            {
                ai.Side = flank;
                if (_battleJoined)
                {
                    ai.SetBehaviorWeight<BehaviorFlank>(1f);
                    ai.SetBehaviorWeight<BehaviorTacticalCharge>(1f);
                }
                else
                {
                    ai.SetBehaviorWeight<BehaviorProtectFlank>(1f).FlankSide = flank;
                    ai.SetBehaviorWeight<BehaviorCavalryScreen>(1f);
                }
            }
            else if (query.IsRangedFormation)
            {
                ai.Side = FormationAI.BehaviorSide.Middle;
                if (!_battleJoined)
                {
                    ai.SetBehaviorWeight<BehaviorSkirmishLine>(1f);
                }
                ai.SetBehaviorWeight<BehaviorScreenedSkirmish>(1f);
                ai.SetBehaviorWeight<BehaviorSkirmish>(1f);
            }
            else
            {
                ai.Side = FormationAI.BehaviorSide.Middle;
                BehaviorAdvanceInLine line = ai.SetBehaviorWeight<BehaviorAdvanceInLine>(1f);
                line.LateralOffset = lateral;
                line.Hold = _holding;
                ai.SetBehaviorWeight<BehaviorTacticalCharge>(1f);
            }
        }

        private static float GetLateralOffset(Formation formation)
        {
            int slotIndex = (int)formation.FormationIndex;
            if (slotIndex < 0 || slotIndex >= CommanderBattleSettings.FormationCount || formation.Team == null)
            {
                return 0f;
            }
            CommanderBattleSpawnLayout.GetOffset(formation.Team.Side, slotIndex, out float lateral, out _);
            return lateral;
        }
    }
}
