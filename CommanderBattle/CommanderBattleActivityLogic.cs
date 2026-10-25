using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    // Giving orders counts as activity for the stock inactivity kick. The engine kicks a player whose soldier has not
    // moved, turned or looked around for 180 s (MissionPeer.TickInactivityStatus), and a commander leading from the
    // order bar can easily stand still that long. A player who is really away still gets kicked: neither his soldier
    // nor his formation's orders change. Server only.
    public class CommanderBattleActivityLogic : MissionLogic
    {
        private const float CheckInterval = 1f;

        // A move order this far from the last one is a new order (positions are compared to the metre).
        private const float OrderMoveDistance = 1f;

        // The engine keeps the time of the player's last activity private.
        private static readonly FieldInfo LastActiveTimeField = typeof(MissionPeer).GetField("_lastActiveTime", BindingFlags.Instance | BindingFlags.NonPublic);

        private struct OrderState
        {
            public MovementOrder.MovementOrderEnum Movement;

            public Vec2 MovePosition;

            public Formation ChargeTarget;

            public int Facing;

            public int Arrangement;

            public int Form;

            public int Firing;

            public int Riding;
        }

        private readonly Dictionary<MissionPeer, OrderState> _lastOrders = new Dictionary<MissionPeer, OrderState>();

        private float _checkTimer;

        public override void OnClearScene()
        {
            base.OnClearScene();
            _lastOrders.Clear();
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            _checkTimer += dt;
            if (_checkTimer < CheckInterval || LastActiveTimeField == null)
            {
                return;
            }
            _checkTimer = 0f;
            foreach (NetworkCommunicator networkPeer in GameNetwork.NetworkPeers)
            {
                MissionPeer peer = networkPeer.GetComponent<MissionPeer>();
                Formation formation = peer?.ControlledFormation;
                if (formation == null || networkPeer.IsServerPeer)
                {
                    continue;
                }
                OrderState orders = Read(formation);
                if (_lastOrders.TryGetValue(peer, out OrderState last) && Changed(last, orders))
                {
                    LastActiveTimeField.SetValue(peer, MissionTime.Now);
                }
                _lastOrders[peer] = orders;
            }
        }

        private static OrderState Read(Formation formation)
        {
            MovementOrder movement = formation.GetReadonlyMovementOrderReference();
            return new OrderState
            {
                Movement = movement.OrderEnum,
                // Only a plain move has a fixed position; advance, follow and the like move theirs all the time.
                MovePosition = movement.OrderEnum == MovementOrder.MovementOrderEnum.Move ? formation.OrderPosition : Vec2.Zero,
                ChargeTarget = movement.OrderEnum == MovementOrder.MovementOrderEnum.ChargeToTarget ? formation.TargetFormation : null,
                Facing = (int)formation.FacingOrder.OrderEnum,
                Arrangement = (int)formation.ArrangementOrder.OrderEnum,
                Form = (int)formation.FormOrder.OrderEnum,
                Firing = (int)formation.FiringOrder.OrderEnum,
                Riding = (int)formation.RidingOrder.OrderEnum
            };
        }

        private static bool Changed(OrderState a, OrderState b)
        {
            return a.Movement != b.Movement || a.ChargeTarget != b.ChargeTarget || a.Facing != b.Facing || a.Arrangement != b.Arrangement
                || a.Form != b.Form || a.Firing != b.Firing || a.Riding != b.Riding
                || a.MovePosition.DistanceSquared(b.MovePosition) > OrderMoveDistance * OrderMoveDistance;
        }
    }
}
