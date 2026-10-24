using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle.AI
{
    // Infantry of one formation slot marching in its lane: the slot's sideways offset from the line between
    // the two armies. Several infantry formations so advance side by side, where the stock BehaviorAdvance
    // would send them all to the same enemy formation. Holding keeps the formation where it stood. Once an
    // enemy formation is close the weight drops, so the charge behaviours take over the fight.
    public sealed class BehaviorAdvanceInLine : BehaviorComponent
    {
        private const float EngageDistance = 40f;

        private const float LineWidth = 24f;

        private bool _engaged;

        private bool _hasHoldPosition;

        private WorldPosition _holdPosition;

        public float LateralOffset { get; set; }

        public bool Hold { get; set; }

        public BehaviorAdvanceInLine(Formation formation)
            : base(formation)
        {
            BehaviorCoherence = 0.8f;
            CalculateCurrentOrder();
        }

        protected override void CalculateCurrentOrder()
        {
            TeamQuerySystem team = Formation.Team.QuerySystem;
            if (team.EnemyUnitCount == 0)
            {
                CurrentOrder = MovementOrder.MovementOrderStop;
                return;
            }
            Vec2 forward = team.AverageEnemyPosition - team.AveragePosition;
            forward = forward.LengthSquared > 1f ? forward.Normalized() : Formation.Direction;
            Vec2 right = new Vec2(forward.y, -forward.x);
            CurrentFacingOrder = FacingOrder.FacingOrderLookAtDirection(forward);
            if (Hold)
            {
                if (!_hasHoldPosition)
                {
                    _holdPosition = Formation.CachedMedianPosition;
                    _hasHoldPosition = true;
                }
                CurrentOrder = MovementOrder.MovementOrderMove(_holdPosition);
                return;
            }
            _hasHoldPosition = false;
            WorldPosition target = Formation.CachedMedianPosition;
            target.SetVec2(team.AverageEnemyPosition + right * LateralOffset);
            CurrentOrder = MovementOrder.MovementOrderMove(target);
        }

        protected override void OnBehaviorActivatedAux()
        {
            CalculateCurrentOrder();
            Formation.SetMovementOrder(CurrentOrder);
            Formation.SetFacingOrder(CurrentFacingOrder);
            Formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLine);
            Formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
            Formation.SetFormOrder(FormOrder.FormOrderCustom(LineWidth));
        }

        public override void TickOccasionally()
        {
            CalculateCurrentOrder();
            Formation.SetMovementOrder(CurrentOrder);
            Formation.SetFacingOrder(CurrentFacingOrder);
        }

        public override void ResetBehavior()
        {
            base.ResetBehavior();
            _engaged = false;
            _hasHoldPosition = false;
        }

        protected override float GetAiWeight()
        {
            if (!_engaged)
            {
                FormationQuerySystem enemy = Formation.CachedClosestEnemyFormation;
                _engaged = enemy != null && Formation.CachedAveragePosition.DistanceSquared(enemy.Formation.CachedMedianPosition.AsVec2) < EngageDistance * EngageDistance;
            }
            return _engaged ? 0.2f : 1f;
        }
    }
}
