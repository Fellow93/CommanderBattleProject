using System.Collections.Generic;
using CommanderBattle.Network;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle
{
    // Commanders' banners. Each commander's formation carries their sigil on a shade of their side's colour
    // (CommanderBanners.Build). The client sends its pick (RequestCommanderBanner) when it joins a side and when the
    // player picks one; with no pick saved, the sigil of their profile banner is used, and with no usable profile
    // banner the side's own sigil.
    //
    // A shield is painted when its agent is built, and Agent has no call to repaint one, so a pick applies before the
    // commander's formation spawns and otherwise from the next round, like a troop pick. Only ids reach the other
    // players (CommanderBanner), never a client's banner code; the server checks them first.
    public partial class MissionMultiplayerCommanderBattle
    {
        private sealed class BannerChoice
        {
            public int Sigil = CommanderBanners.SideSigil;

            public int Shade;

            public bool HasPending;

            public int PendingSigil = CommanderBanners.SideSigil;

            public int PendingShade;
        }

        private readonly Dictionary<NetworkCommunicator, BannerChoice> _banners = new Dictionary<NetworkCommunicator, BannerChoice>();

        private bool HandleClientEventRequestCommanderBanner(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            RequestCommanderBanner message = (RequestCommanderBanner)baseMessage;
            RequestCommanderBannerFrom(peer, message.Sigil, message.Shade);
            return true;
        }

        // sigil: an icon id, or -1 for the profile banner's sigil. shade: an index into the side's shades.
        public void RequestCommanderBannerFrom(NetworkCommunicator peer, int sigil, int shade)
        {
            MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
            BattleSideEnum side = missionPeer?.Team?.Side ?? BattleSideEnum.None;
            if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender)
            {
                return;
            }
            if ((sigil != CommanderBanners.SideSigil && !CommanderBanners.IsValidSigil(sigil)) || shade < 0 || shade >= CommanderBanners.GetShades(side).Count)
            {
                CommanderBattleLog.Write($"Refused {peer.UserName}'s banner: sigil {sigil}, shade {shade} ({CommanderBanners.GetShades(side).Count} shades on their side)");
                SendServerMessage(peer, "That banner can't be used here.");
                return;
            }
            if (sigil == CommanderBanners.SideSigil)
            {
                sigil = CommanderBanners.GetProfileSigil(missionPeer.Peer.BannerCode);
            }
            bool isNew = !_banners.TryGetValue(peer, out BannerChoice choice);
            if (isNew)
            {
                choice = new BannerChoice();
                _banners[peer] = choice;
            }
            // Before the commander's formation spawns this round, the pick is what it spawns with.
            bool applyNow = isNew || !RoundController.IsRoundInProgress || missionPeer.SpawnCountThisRound == 0;
            bool keeps = choice.Sigil == sigil && choice.Shade == shade;
            if (applyNow || keeps)
            {
                if (!isNew && keeps && !choice.HasPending)
                {
                    return;
                }
                choice.Sigil = sigil;
                choice.Shade = shade;
                choice.HasPending = false;
            }
            else
            {
                if (choice.HasPending && choice.PendingSigil == sigil && choice.PendingShade == shade)
                {
                    return;
                }
                choice.HasPending = true;
                choice.PendingSigil = sigil;
                choice.PendingShade = shade;
            }
            BroadcastBanner(peer, choice);
            if (isNew)
            {
                CommanderBattleLog.Detail($"{peer.UserName}'s banner: sigil {sigil}, shade {shade}");
                return;
            }
            CommanderBattleLog.Write($"{peer.UserName} picked banner sigil {sigil}, shade {shade}{(choice.HasPending ? ", from next round" : "")}");
            SendServerMessage(peer, choice.HasPending ? "Your banner: from next round." : "Your banner: this round.");
        }

        // Picks made since the commander's formation spawned take effect as the next round's preparation starts.
        private void ApplyPendingBanners()
        {
            foreach (KeyValuePair<NetworkCommunicator, BannerChoice> entry in _banners)
            {
                BannerChoice choice = entry.Value;
                if (!choice.HasPending)
                {
                    continue;
                }
                choice.Sigil = choice.PendingSigil;
                choice.Shade = choice.PendingShade;
                choice.HasPending = false;
                BroadcastBanner(entry.Key, choice);
            }
        }

        // The banner a commander's formation spawns with this round, on the commander's current side. A commander whose
        // client sent nothing (yet) gets their profile banner's sigil.
        public Banner GetCommanderBanner(MissionPeer commander)
        {
            NetworkCommunicator peer = commander?.GetNetworkPeer();
            BattleSideEnum side = commander?.Team?.Side ?? BattleSideEnum.None;
            if (peer == null || (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender))
            {
                return null;
            }
            if (!_banners.TryGetValue(peer, out BannerChoice choice))
            {
                choice = new BannerChoice { Sigil = CommanderBanners.GetProfileSigil(commander.Peer.BannerCode) };
                _banners[peer] = choice;
                BroadcastBanner(peer, choice);
            }
            return CommanderBanners.Build(side, choice.Sigil, choice.Shade);
        }

        private static void BroadcastBanner(NetworkCommunicator peer, BannerChoice choice)
        {
            CommanderBattleNetwork.Broadcast(ToMessage(peer, choice));
        }

        private static CommanderBanner ToMessage(NetworkCommunicator peer, BannerChoice choice)
        {
            return new CommanderBanner(peer, choice.Sigil, choice.Shade, choice.HasPending, choice.PendingSigil, choice.PendingShade);
        }

        private void SendBannersTo(NetworkCommunicator networkPeer)
        {
            foreach (KeyValuePair<NetworkCommunicator, BannerChoice> entry in _banners)
            {
                if (entry.Key.IsConnectionActive)
                {
                    CommanderBattleNetwork.SendTo(networkPeer, ToMessage(entry.Key, entry.Value));
                }
            }
        }

        private void ForgetBanner(NetworkCommunicator peer)
        {
            _banners.Remove(peer);
        }

        // Called while the stock spawner turns preparation visuals into agents: the agents' build data are all queued
        // by then, and each agent's weapons take its build data's banner only after this event.
        public override void OnAgentCreated(Agent agent)
        {
            base.OnAgentCreated(agent);
            (SpawnComponent?.SpawningBehavior as CommanderBattleSpawningBehavior)?.ApplyCommanderBannersToQueuedAgents();
        }
    }
}
