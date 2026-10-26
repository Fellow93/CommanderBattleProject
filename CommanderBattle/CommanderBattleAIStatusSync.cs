using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using CommanderBattle.Network;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    // Server: tells each player what their side's team AI is doing (SideAIStatus): the tactic, and per formation the
    // behaviour, order, position and destination. Sent when any of the names or orders change, and every
    // ResendInterval for the positions. Works with Commander Battle's own tactic and with RBM's.
    public class CommanderBattleAIStatusSync : MissionLogic
    {
        private const float CheckInterval = 1f;

        private const float ResendInterval = 2f;

        // A move order closer than this to where the formation stands is holding position, not going anywhere.
        private const float MinDestinationDistance = 8f;

        // The team AI keeps its tactic private.
        private static readonly FieldInfo CurrentTacticField = typeof(TeamAIComponent).GetField("_currentTactic", BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class SentState
        {
            public string Key;

            public float Time;
        }

        private readonly Dictionary<NetworkCommunicator, SentState> _sent = new Dictionary<NetworkCommunicator, SentState>();

        private readonly SideAIStatus[] _status = new SideAIStatus[2];

        private readonly string[] _keys = new string[2];

        private float _checkTimer;

        public override void OnClearScene()
        {
            base.OnClearScene();
            _sent.Clear();
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            _checkTimer += dt;
            if (_checkTimer < CheckInterval)
            {
                return;
            }
            _checkTimer = 0f;
            float now = Mission.CurrentTime;
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                _status[(int)side] = Build(side, out _keys[(int)side]);
            }
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers)
            {
                if (!peer.IsSynchronized || peer.IsServerPeer)
                {
                    continue;
                }
                BattleSideEnum side = peer.GetComponent<MissionPeer>()?.Team?.Side ?? BattleSideEnum.None;
                if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender)
                {
                    continue;
                }
                SideAIStatus status = _status[(int)side];
                string key = _keys[(int)side];
                if (status == null)
                {
                    continue;
                }
                if (!_sent.TryGetValue(peer, out SentState sent))
                {
                    sent = new SentState();
                    _sent[peer] = sent;
                }
                if (sent.Key == key && now - sent.Time < ResendInterval)
                {
                    continue;
                }
                sent.Key = key;
                sent.Time = now;
                CommanderBattleNetwork.SendTo(peer, status);
            }
        }

        // The side's status, and a key of everything but positions and troop counts, which change all the time.
        private SideAIStatus Build(BattleSideEnum side, out string key)
        {
            key = null;
            Team team = CommanderBattleTeams.GetTeam(Mission, side);
            if (team == null || !team.HasTeamAi)
            {
                return null;
            }
            string tactic = Readable(CurrentTacticField?.GetValue(team.TeamAI)?.GetType());
            StringBuilder keyBuilder = new StringBuilder();
            keyBuilder.Append(side).Append('|').Append(tactic);
            List<AIFormationStatus> formations = new List<AIFormationStatus>();
            foreach (Formation formation in team.FormationsIncludingEmpty)
            {
                if (formation.CountOfUnits == 0)
                {
                    continue;
                }
                AIFormationStatus status = Describe(formation);
                formations.Add(status);
                keyBuilder.Append('|').Append(status.FormationIndex).Append(status.IsPlayerLed ? 'P' : 'A').Append(status.IsMain ? 'M' : '-')
                    .Append(status.Behavior).Append((int)status.Order).Append(status.HasDestination ? 'D' : '-');
            }
            key = keyBuilder.ToString();
            return new SideAIStatus(side, tactic, formations);
        }

        private static AIFormationStatus Describe(Formation formation)
        {
            FormationQuerySystem query = formation.QuerySystem;
            MovementOrder order = formation.GetReadonlyMovementOrderReference();
            AIFormationStatus status = new AIFormationStatus
            {
                FormationIndex = (int)formation.FormationIndex,
                Kind = query.IsRangedCavalryFormation ? AIFormationKind.HorseArchers
                    : query.IsCavalryFormation ? AIFormationKind.Cavalry
                    : query.IsRangedFormation ? AIFormationKind.Ranged
                    : AIFormationKind.Infantry,
                Troops = formation.CountOfUnits,
                IsPlayerLed = !formation.IsAIControlled,
                IsMain = formation.AI != null && formation.AI.IsMainFormation,
                Behavior = formation.IsAIControlled ? Readable(formation.AI?.ActiveBehavior?.GetType()) : "",
                Order = GetOrder(order.OrderEnum),
                Position = formation.CachedAveragePosition
            };
            Vec2 destination = Vec2.Invalid;
            switch (status.Order)
            {
                case AIFormationOrder.Move:
                case AIFormationOrder.Retreat:
                    destination = order.GetPosition(formation);
                    break;
                case AIFormationOrder.Charge:
                    if (formation.TargetFormation != null && formation.TargetFormation.CountOfUnits > 0)
                    {
                        destination = formation.TargetFormation.CachedAveragePosition;
                    }
                    break;
            }
            if (destination.IsValid && destination.DistanceSquared(status.Position) > MinDestinationDistance * MinDestinationDistance)
            {
                status.HasDestination = true;
                status.Destination = destination;
            }
            return status;
        }

        private static AIFormationOrder GetOrder(MovementOrder.MovementOrderEnum order)
        {
            switch (order)
            {
                case MovementOrder.MovementOrderEnum.Move:
                case MovementOrder.MovementOrderEnum.Follow:
                case MovementOrder.MovementOrderEnum.FollowEntity:
                case MovementOrder.MovementOrderEnum.Advance:
                case MovementOrder.MovementOrderEnum.FallBack:
                    return AIFormationOrder.Move;
                case MovementOrder.MovementOrderEnum.Charge:
                case MovementOrder.MovementOrderEnum.ChargeToTarget:
                    return AIFormationOrder.Charge;
                case MovementOrder.MovementOrderEnum.Stop:
                    return AIFormationOrder.Hold;
                case MovementOrder.MovementOrderEnum.Retreat:
                    return AIFormationOrder.Retreat;
                default:
                    return AIFormationOrder.Other;
            }
        }

        // "RBMBehaviorArcherSkirmish" -> "Archer Skirmish", "TacticDefensiveLine" -> "Defensive Line".
        public static string Readable(Type type)
        {
            if (type == null)
            {
                return "";
            }
            string name = type.Name;
            int generic = name.IndexOf('`');
            if (generic >= 0)
            {
                name = name.Substring(0, generic);
            }
            foreach (string prefix in new[] { "RBMBehavior", "RBMTactic", "Behavior", "Tactic", "RBM" })
            {
                if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    name = name.Substring(prefix.Length);
                    break;
                }
            }
            if (name.Length > "Tactic".Length && name.EndsWith("Tactic", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - "Tactic".Length);
            }
            StringBuilder readable = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                {
                    readable.Append(' ');
                }
                readable.Append(c);
            }
            return readable.ToString();
        }
    }
}
