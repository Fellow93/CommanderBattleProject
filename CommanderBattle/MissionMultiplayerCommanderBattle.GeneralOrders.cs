using System.Collections.Generic;
using CommanderBattle.Network;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle
{
    // The orders each side's general draws in general mode (docs/plans/general-mode.md). The server keeps them and
    // sends them to that side's players only: the other side must never learn the plan. They last until the round
    // ends or the general deletes them.
    public partial class MissionMultiplayerCommanderBattle
    {
        // Per side, by (int)BattleSideEnum, oldest first.
        private readonly List<GeneralOrder>[] _generalOrders = { new List<GeneralOrder>(), new List<GeneralOrder>() };

        private int _lastGeneralOrderId;

        private bool HandleClientEventRequestGeneralOrder(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            RequestGeneralOrder message = (RequestGeneralOrder)baseMessage;
            RequestGeneralOrderFrom(peer, message.Points, message.Label);
            return true;
        }

        private bool HandleClientEventRequestDeleteGeneralOrder(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            RequestDeleteGeneralOrder message = (RequestDeleteGeneralOrder)baseMessage;
            RequestDeleteGeneralOrderFrom(peer, message.Id);
            return true;
        }

        // The side the peer is the general of, or None.
        private BattleSideEnum GetGeneralSide(NetworkCommunicator peer)
        {
            BattleSideEnum side = peer.GetComponent<MissionPeer>()?.Team?.Side ?? BattleSideEnum.None;
            FormationSlot slot = Slots.GetSlot(side, CommanderBattleSettings.GeneralSlot);
            return slot != null && slot.Commander == peer ? side : BattleSideEnum.None;
        }

        public void RequestGeneralOrderFrom(NetworkCommunicator peer, Vec2[] points, string label)
        {
            BattleSideEnum side = GetGeneralSide(peer);
            if (side == BattleSideEnum.None)
            {
                SendServerMessage(peer, "Only your side's general can draw orders.");
                return;
            }
            if (!RoundController.IsRoundInProgress)
            {
                return;
            }
            List<Vec2> kept = new List<Vec2>(GeneralOrders.MaxPoints);
            if (points != null)
            {
                for (int i = 0; i < points.Length && kept.Count < GeneralOrders.MaxPoints; i++)
                {
                    if (points[i].IsValid && Mission.IsPositionInsideBoundaries(points[i]))
                    {
                        kept.Add(points[i]);
                    }
                }
            }
            if (kept.Count < 2)
            {
                SendServerMessage(peer, "Draw the path inside the battlefield.");
                return;
            }
            string clean = GeneralOrders.SanitizeLabel(label);
            if (clean.Length == 0)
            {
                clean = "Path";
            }
            _lastGeneralOrderId = _lastGeneralOrderId % GeneralOrders.MaxId + 1;
            GeneralOrder order = new GeneralOrder(_lastGeneralOrderId, side, kept.ToArray(), clean);
            List<GeneralOrder> orders = _generalOrders[(int)side];
            while (orders.Count >= GeneralOrders.MaxOrdersPerSide)
            {
                GeneralOrder oldest = orders[0];
                orders.RemoveAt(0);
                SendGeneralOrderRemoved(side, oldest.Id);
            }
            orders.Add(order);
            SendGeneralOrderAdded(order);
            CommanderBattleLog.Write($"{peer.UserName} ({side} general) drew path {order.Id} '{order.Label}' ({order.Points.Length} points)");
        }

        public void RequestDeleteGeneralOrderFrom(NetworkCommunicator peer, int id)
        {
            BattleSideEnum side = GetGeneralSide(peer);
            if (side == BattleSideEnum.None)
            {
                return;
            }
            List<GeneralOrder> orders = _generalOrders[(int)side];
            int index = orders.FindIndex(order => order.Id == id);
            if (index < 0)
            {
                return;
            }
            GeneralOrder removed = orders[index];
            orders.RemoveAt(index);
            SendGeneralOrderRemoved(side, id);
            CommanderBattleLog.Write($"{peer.UserName} ({side} general) deleted path {id} '{removed.Label}'");
        }

        // Round end: every order goes, and the side's players are told. OnClearScene clears silently, as every client
        // also clears its own then.
        private void ClearGeneralOrders(bool notify)
        {
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                List<GeneralOrder> orders = _generalOrders[(int)side];
                if (notify)
                {
                    foreach (GeneralOrder order in orders)
                    {
                        SendGeneralOrderRemoved(side, order.Id);
                    }
                }
                orders.Clear();
            }
        }

        // A player who joins a side, or is synchronised on one, gets its standing orders.
        private void SendGeneralOrdersTo(NetworkCommunicator peer, BattleSideEnum side)
        {
            if ((side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender) || !peer.IsSynchronized)
            {
                return;
            }
            foreach (GeneralOrder order in _generalOrders[(int)side])
            {
                SendGeneralOrderTo(peer, new GeneralOrderAdded(order));
            }
        }

        private void SendGeneralOrderAdded(GeneralOrder order)
        {
            foreach (NetworkCommunicator peer in GetSidePeers(order.Side))
            {
                SendGeneralOrderTo(peer, new GeneralOrderAdded(order));
            }
        }

        private void SendGeneralOrderRemoved(BattleSideEnum side, int id)
        {
            foreach (NetworkCommunicator peer in GetSidePeers(side))
            {
                SendGeneralOrderTo(peer, new GeneralOrderRemoved(id));
            }
        }

        // Never a broadcast: the other side and the spectators don't see a general's orders.
        private static List<NetworkCommunicator> GetSidePeers(BattleSideEnum side)
        {
            List<NetworkCommunicator> peers = new List<NetworkCommunicator>();
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers)
            {
                if (peer.IsSynchronized && peer.GetComponent<MissionPeer>()?.Team?.Side == side)
                {
                    peers.Add(peer);
                }
            }
            return peers;
        }

        // A listen server's own player has no connection to send to; its client half takes the order directly.
        private void SendGeneralOrderTo(NetworkCommunicator peer, GameNetworkMessage message)
        {
            if (!peer.IsServerPeer)
            {
                CommanderBattleNetwork.SendTo(peer, message);
                return;
            }
            MissionMultiplayerCommanderBattleClient client = Mission.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>();
            if (message is GeneralOrderAdded added)
            {
                client?.AddGeneralOrder(added.Order);
            }
            else if (message is GeneralOrderRemoved removed)
            {
                client?.RemoveGeneralOrder(removed.Id);
            }
        }
    }
}
