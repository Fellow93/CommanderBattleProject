using System.Collections.Generic;
using CommanderBattle.Network;
using NetworkMessages.FromServer;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle
{
    // Admin commands from the admin panel's Commander Battle group (AdminCommand). Map and faction changes are the
    // stock panel's own Mission Options; these are the ones only this mode knows.
    public partial class MissionMultiplayerCommanderBattle
    {
        // Set by Restart round: the next CheckForRoundEnd ends the round with no winner.
        private bool _forceRoundEnd;

        // Swap sides asked for outside preparation: done as the next preparation starts.
        private bool _swapSidesPending;

        private bool HandleClientEventAdminCommand(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            AdminCommand message = (AdminCommand)baseMessage;
            RunAdminCommand(peer, message.Command, message.Target);
            return true;
        }

        private bool CheckAdmin(NetworkCommunicator admin, string command)
        {
            if (admin.IsAdmin || admin.IsServerPeer)
            {
                return true;
            }
            CommanderBattleLog.Write($"Refused admin command {command} from {admin.UserName}: not an admin");
            SendServerMessage(admin, "Only an admin can do that.");
            return false;
        }

        public void RunAdminCommand(NetworkCommunicator admin, AdminCommandType command, NetworkCommunicator target)
        {
            if (!CheckAdmin(admin, command.ToString()))
            {
                return;
            }
            CommanderBattleLog.Write($"Admin {admin.UserName}: {command}{(target != null ? " " + target.UserName : "")} (round {RoundController.RoundCount} {RoundController.CurrentRoundState})");
            switch (command)
            {
                case AdminCommandType.RestartRound:
                    RestartRound(admin);
                    break;
                case AdminCommandType.SwapSides:
                    RequestSwapSides(admin);
                    break;
                case AdminCommandType.ReleaseFormation:
                    ReleaseFormation(admin, target);
                    break;
            }
        }

        // CheckForRoundEnd only runs while a round is in progress; a round in preparation is restarted by playing it.
        private void RestartRound(NetworkCommunicator admin)
        {
            if (RoundController.CurrentRoundState != MultiplayerRoundState.InProgress)
            {
                SendServerMessage(admin, "No round is being fought.");
                return;
            }
            _forceRoundEnd = true;
            Announce($"{admin.UserName} restarted the round.");
        }

        // Swapping mid-fight would kill every player's agent (ChangeTeamServer's team switch), so a round being
        // fought is restarted and the sides swap as the next one's preparation starts.
        private void RequestSwapSides(NetworkCommunicator admin)
        {
            if (RoundController.CurrentRoundState == MultiplayerRoundState.Preparation)
            {
                Announce($"{admin.UserName} swapped the sides.");
                SwapSides();
                return;
            }
            _swapSidesPending = true;
            if (RoundController.CurrentRoundState == MultiplayerRoundState.InProgress)
            {
                _forceRoundEnd = true;
                Announce($"{admin.UserName} swapped the sides: the round restarts.");
            }
            else
            {
                Announce($"{admin.UserName} swapped the sides from the next round.");
            }
        }

        // Called from the tick, so ResetMission has already cleared last round's agents.
        private void ApplyPendingSwap()
        {
            if (_swapSidesPending && RoundController.CurrentRoundState == MultiplayerRoundState.Preparation)
            {
                _swapSidesPending = false;
                SwapSides();
            }
        }

        // Every player goes to the other side. Each keeps their formation's number when the other side's formation of
        // that number has the same role (the cultures split their formations differently), else takes the first free
        // formation of the role; anyone left over is placed as on joining (PlaceQueuedPeers). Every player changes
        // side before anyone claims, so nobody finds their new formation still held by the player leaving it.
        private void SwapSides()
        {
            List<(NetworkCommunicator Peer, int SlotIndex, CommanderRole? Role)> moves = new List<(NetworkCommunicator, int, CommanderRole?)>();
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers)
            {
                BattleSideEnum side = peer.GetComponent<MissionPeer>()?.Team?.Side ?? BattleSideEnum.None;
                if (!peer.IsSynchronized || (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender))
                {
                    continue;
                }
                FormationSlot slot = Slots.FindSlotOf(peer);
                bool onSide = slot != null && slot.Side == side;
                moves.Add((peer, onSide ? slot.Index : -1, onSide ? slot.Role : (CommanderRole?)null));
            }
            foreach ((NetworkCommunicator peer, _, _) in moves)
            {
                BattleSideEnum side = peer.GetComponent<MissionPeer>().Team.Side;
                MultiplayerTeamSelectComponent.ChangeTeamServer(peer, side == BattleSideEnum.Attacker ? Mission.DefenderTeam : Mission.AttackerTeam);
            }
            foreach ((NetworkCommunicator peer, int slotIndex, CommanderRole? role) in moves)
            {
                MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
                if (role == null || missionPeer?.Team == null)
                {
                    continue;
                }
                BattleSideEnum side = missionPeer.Team.Side;
                FormationSlot slot = Slots.GetSlot(side, slotIndex);
                // The general stays the general (slot 8 on both sides), if the other culture fields one.
                if (slot == null || slot.Commander != null || slot.Role != role.Value || !slot.IsFielded)
                {
                    slot = Slots.FindFreeSlot(side, role.Value);
                }
                if (slot != null)
                {
                    Claim(peer, missionPeer, slot);
                }
            }
            CommanderBattleLog.Write($"Sides swapped: {moves.Count} players");
        }

        // The player goes to the spectators, which hands their formation to the AI (OnPreTeamChanged, OnPeerTeamChanged);
        // they can join a side again and take a free formation.
        private void ReleaseFormation(NetworkCommunicator admin, NetworkCommunicator target)
        {
            FormationSlot slot = Slots.FindSlotOf(target);
            if (target == null || slot == null)
            {
                SendServerMessage(admin, "That player commands no formation.");
                return;
            }
            MultiplayerTeamSelectComponent.ChangeTeamServer(target, Mission.SpectatorTeam);
            Announce($"{admin.UserName} handed {target.UserName}'s formation to the AI.");
        }

        private bool HandleClientEventRequestWeatherOverride(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            RequestWeatherOverride message = (RequestWeatherOverride)baseMessage;
            SetWeatherOverrideFrom(peer, message.Time, message.Weather);
            return true;
        }

        // The admin panel's Time of Day and Weather. Applies at once, on the round being played too, and stays for the
        // next rounds of this map; a new map starts random again.
        public void SetWeatherOverrideFrom(NetworkCommunicator admin, int time, int weather)
        {
            if (!CheckAdmin(admin, "SetWeather"))
            {
                return;
            }
            CommanderBattleWeather.SetForced(time, weather);
            int seed = CommanderBattleSpawnLayout.GetRoundSeed(Mission);
            CommanderBattleLog.Write($"Admin {admin.UserName}: weather set to {CommanderBattleWeather.DescribeForced()}; round {RoundController.RoundCount} weather: {CommanderBattleWeather.Describe(CommanderBattleWeather.GetPick(seed))}");
            CommanderBattleNetwork.Broadcast(new WeatherOverride(CommanderBattleWeather.ForcedTime, CommanderBattleWeather.ForcedWeather));
            Announce($"{admin.UserName} set the weather: {CommanderBattleWeather.DescribeForced()}.");
            CommanderBattleWeather.Apply(Mission, seed);
        }

        // To every player; a listen server's host isn't reached by the broadcast.
        private static void Announce(string message)
        {
            if (!GameNetwork.IsDedicatedServer)
            {
                InformationManager.DisplayMessage(new InformationMessage(message));
            }
            CommanderBattleNetwork.Broadcast(new ServerMessage(message));
        }
    }
}
