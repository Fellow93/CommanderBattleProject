using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    // A player general rides at the tip of his bodyguard's wedge: the stock Skein arrangement, one V-shaped rank whose
    // middle file is furthest forward, one file per agent. The stock Follow order keeps that middle front slot free
    // for the followed agent (ReserveMiddleFrontUnitPosition), but a mounted formation on the move aims 2 s of his
    // velocity ahead of him (MovementOrder.GetPositionAuxFollow), so the riders rode across his path. The order bar
    // can't order formation 8, so the server gives the bodyguard a Move order that puts the reserved slot on the
    // general, renewed as he moves or turns. A player's own unit sits in the formation's back-most slot
    // (Formation.TryRelocatePlayerUnit), so the riders fill the V and his own slot is the empty one behind the tip.
    // An AI general moves with his formation (BehaviorGeneral) and is left alone.
    public partial class MissionMultiplayerCommanderBattle
    {
        // How far the general moves before the wedge is sent after him.
        private const float BodyguardRefreshDistance = 2f;

        // How far his heading turns (cosine, about 15 degrees) before the wedge turns with him.
        private const float BodyguardRefreshCosine = 0.966f;

        // Faster than this, the wedge points where the general rides; slower, it keeps its last heading.
        private const float BodyguardMovingSpeed = 2f;

        // Per side, by (int)BattleSideEnum: where the tip and heading were last sent, and for how many agents the
        // wedge was sized. Cleared each round.
        private readonly Vec2[] _bodyguardTips = { Vec2.Invalid, Vec2.Invalid };
        private readonly Vec2[] _bodyguardHeadings = { Vec2.Invalid, Vec2.Invalid };
        private readonly int[] _bodyguardFiles = { 0, 0 };

        private void KeepBodyguardsBehindGenerals()
        {
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                Team team = CommanderBattleTeams.GetTeam(Mission, side);
                Agent general = team?.GeneralAgent;
                Formation formation = team?.GeneralsFormation;
                if (general == null || !general.IsActive() || general.MissionPeer == null || formation == null || formation.CountOfUnits <= 1)
                {
                    continue;
                }
                int index = (int)side;
                Vec2 velocity = general.AverageVelocity.AsVec2;
                Vec2 heading = velocity.LengthSquared > BodyguardMovingSpeed * BodyguardMovingSpeed ? velocity.Normalized()
                    : _bodyguardHeadings[index].IsValid ? _bodyguardHeadings[index] : formation.Direction;
                Vec2 tip = general.Position.AsVec2;
                bool sized = _bodyguardFiles[index] == formation.CountOfUnits && formation.ArrangementOrder.OrderEnum == ArrangementOrder.ArrangementOrderEnum.Skein;
                if (sized && _bodyguardTips[index].IsValid
                    && _bodyguardTips[index].DistanceSquared(tip) < BodyguardRefreshDistance * BodyguardRefreshDistance
                    && _bodyguardHeadings[index].DotProduct(heading) > BodyguardRefreshCosine
                    && formation.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Move)
                {
                    continue;
                }
                if (!sized)
                {
                    // A new arrangement drops the reserved slot; it is reserved again below.
                    formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderSkein);
                    formation.SetFormOrder(FormOrder.FormOrderCustom(LineFormation.CalculateWidth(formation.Interval, formation.UnitDiameter, formation.CountOfUnits) + 0.1f));
                    _bodyguardFiles[index] = formation.CountOfUnits;
                }
                formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(heading));
                WorldPosition position = general.GetWorldPosition();
                position.SetVec2(tip - formation.GetMiddleFrontUnitPositionOffset());
                formation.SetMovementOrder(MovementOrder.MovementOrderMove(position));
                formation.Arrangement.ReserveMiddleFrontUnitPosition(general);
                _bodyguardTips[index] = tip;
                _bodyguardHeadings[index] = heading;
            }
        }

        private void ClearBodyguardTargets()
        {
            for (int i = 0; i < _bodyguardTips.Length; i++)
            {
                _bodyguardTips[i] = Vec2.Invalid;
                _bodyguardHeadings[i] = Vec2.Invalid;
                _bodyguardFiles[i] = 0;
            }
        }
    }
}
