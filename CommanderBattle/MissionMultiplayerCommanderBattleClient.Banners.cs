using System;
using System.Collections.Generic;
using CommanderBattle.Network;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.MountAndBlade.Network.Messages;
using TaleWorlds.PlayerServices;

namespace CommanderBattle
{
    // Commanders' banners on the client: what each commander's formation carries this round and next (from
    // CommanderBanner), our own pick saved between sessions (CommanderBattleClientConfig) and sent whenever we join a
    // side, and each player-led formation's Banner, which the compass and the agent labels read.
    public partial class MissionMultiplayerCommanderBattleClient
    {
        public sealed class CommanderBannerInfo
        {
            public CommanderBannerInfo(CommanderBanner message)
            {
                Sigil = message.Sigil;
                Shade = message.Shade;
                HasPending = message.HasPending;
                PendingSigil = message.PendingSigil;
                PendingShade = message.PendingShade;
            }

            public int Sigil { get; }

            public int Shade { get; }

            public bool HasPending { get; }

            public int PendingSigil { get; }

            public int PendingShade { get; }
        }

        private readonly Dictionary<NetworkCommunicator, CommanderBannerInfo> _commanderBanners = new Dictionary<NetworkCommunicator, CommanderBannerInfo>();

        // The banner built for each commander's formation this round, on the side it was built for, and its code as the
        // stock client recolours it in team colours (see OnAgentBuild).
        private readonly Dictionary<NetworkCommunicator, (BattleSideEnum Side, int Sigil, int Shade, Banner Banner, string TeamColoured)> _builtBanners = new Dictionary<NetworkCommunicator, (BattleSideEnum, int, int, Banner, string)>();

        // Each commander's own profile banner code, from before UpdateFormationBanners replaced it on this machine.
        private readonly Dictionary<NetworkCommunicator, string> _profileBannerCodes = new Dictionary<NetworkCommunicator, string>();

        public event Action OnCommanderBannersChanged;

        // Null until the server sends the commander's banner.
        public CommanderBannerInfo GetCommanderBannerInfo(NetworkCommunicator peer)
        {
            return peer != null && _commanderBanners.TryGetValue(peer, out CommanderBannerInfo info) ? info : null;
        }

        // The banner a commander's formation carries this round, or null.
        public Banner GetCommanderBanner(NetworkCommunicator peer)
        {
            CommanderBannerInfo info = GetCommanderBannerInfo(peer);
            BattleSideEnum side = peer?.GetComponent<MissionPeer>()?.Team?.Side ?? BattleSideEnum.None;
            if (info == null || (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender))
            {
                return null;
            }
            if (_builtBanners.TryGetValue(peer, out var built) && built.Side == side && built.Sigil == info.Sigil && built.Shade == info.Shade)
            {
                return built.Banner;
            }
            Banner banner = CommanderBanners.Build(side, info.Sigil, info.Shade);
            Team team = peer.GetComponent<MissionPeer>().Team;
            string teamColoured = banner != null ? new Banner(banner.BannerCode, team.Color, team.Color2).BannerCode : null;
            _builtBanners[peer] = (side, info.Sigil, info.Shade, banner, teamColoured);
            return banner;
        }

        // The commander's own profile banner code: this machine's copy of it is replaced (UpdateFormationBanners).
        public string GetProfileBannerCode(NetworkCommunicator peer)
        {
            return peer != null && _profileBannerCodes.TryGetValue(peer, out string code) ? code : peer?.VirtualPlayer.BannerCode;
        }

        // The small banner over each soldier (MissionAgentLabelView) is the banner of the agent's build data on this
        // machine. The server sends an agent before it joins its formation (Mission.SpawnAgent writes CreateAgent before
        // Agent.Build), so the stock client never finds the formation and builds a fresh banner from the owning
        // commander's peer banner code in team colours (MissionNetworkComponent.HandleServerEventCreateAgent). That code
        // is the commander's banner here (UpdateFormationBanners); the team colours lose the shade, which is restored on
        // that per-agent banner before the label view, added later, draws it.
        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            if (!GameNetwork.IsClient || banner == null || banner == agent.Team?.Banner)
            {
                return;
            }
            string code = banner.BannerCode;
            foreach (var built in _builtBanners.Values)
            {
                if (built.Banner != null && built.TeamColoured == code)
                {
                    banner.Deserialize(built.Banner.BannerCode);
                    return;
                }
            }
        }

        // Our saved pick for the side's culture: sigil -1 = our profile banner's. A shade out of the side's range (the
        // file edited, or the palette changed) is the side's own colour.
        public void GetSavedBanner(BattleSideEnum side, out int sigil, out int shade)
        {
            CommanderBattleClientConfig.TryGetBanner(MyPlayerId, CommanderBattleTeams.GetCulture(side)?.StringId, out sigil, out shade);
            if (shade >= CommanderBanners.GetShades(side).Count)
            {
                shade = 0;
            }
        }

        // Saves the pick for our side's culture and asks the server for it.
        public void RequestCommanderBanner(int sigil, int shade)
        {
            CommanderBattleClientConfig.SaveBanner(MyPlayerId, CommanderBattleTeams.GetCulture(CommanderBattleTeams.MySide)?.StringId, sigil, shade);
            Request(new RequestCommanderBanner(sigil, shade), gameMode => gameMode.RequestCommanderBannerFrom(GameNetwork.MyPeer, sigil, shade));
        }

        // The id the stock perk file is keyed by (LobbyState: MPPerkSelectionManager.InitializeForUser).
        private static PlayerId MyPlayerId => NetworkMain.GameClient?.PlayerID ?? GameNetwork.MyPeer.VirtualPlayer.Id;

        // The client is the source of the pick each time, the one saved for the new side's culture.
        private void OnTeamChangedForBanner(NetworkCommunicator peer, Team previousTeam, Team newTeam)
        {
            if (!peer.IsMine || newTeam == null || (newTeam.Side != BattleSideEnum.Attacker && newTeam.Side != BattleSideEnum.Defender))
            {
                return;
            }
            GetSavedBanner(newTeam.Side, out int sigil, out int shade);
            Request(new RequestCommanderBanner(sigil, shade), gameMode => gameMode.RequestCommanderBannerFrom(GameNetwork.MyPeer, sigil, shade));
        }

        private void HandleServerEventCommanderBanner(GameNetworkMessage baseMessage)
        {
            CommanderBanner message = (CommanderBanner)baseMessage;
            if (message.Peer == null)
            {
                return;
            }
            _commanderBanners[message.Peer] = new CommanderBannerInfo(message);
            OnCommanderBannersChanged?.Invoke();
        }

        private readonly (string Cultures, Banner Banner)[] _sideBanners = new (string, Banner)[2];

        // Each formation's Banner: its commander's, or the side's for one the AI leads. Read when an agent of the
        // formation is created (labels) and by the compass. The server's code for it comes in team colours
        // (InitializeFormation), so the shade would be lost, and the client keeps the first Banner it built from it.
        private void UpdateFormationBanners()
        {
            foreach (FormationSlot slot in Slots.AllSlots)
            {
                Formation formation = CommanderBattleTeams.GetTeam(Mission, slot.Side)?.GetFormation(slot.FormationClass);
                if (formation == null)
                {
                    continue;
                }
                Banner banner = slot.Commander != null ? GetCommanderBanner(slot.Commander) : GetSideBanner(slot.Side);
                if (banner != null && formation.Banner != banner)
                {
                    formation.Banner = banner;
                }
                // The stock client builds the soldiers' label banners from this code (see OnAgentBuild); the scoreboard
                // and the kill feed then show the commander's banner too.
                VirtualPlayer player = slot.Commander?.VirtualPlayer;
                if (player != null && banner != null && player.BannerCode != banner.BannerCode)
                {
                    if (!_profileBannerCodes.ContainsKey(slot.Commander))
                    {
                        _profileBannerCodes[slot.Commander] = player.BannerCode;
                    }
                    player.BannerCode = banner.BannerCode;
                }
            }
        }

        private Banner GetSideBanner(BattleSideEnum side)
        {
            string cultures = $"{CommanderBattleTeams.GetCulture(BattleSideEnum.Attacker)?.StringId}|{CommanderBattleTeams.GetCulture(BattleSideEnum.Defender)?.StringId}";
            if (_sideBanners[(int)side].Cultures != cultures)
            {
                _sideBanners[(int)side] = (cultures, CommanderBanners.GetSideBanner(side));
            }
            return _sideBanners[(int)side].Banner;
        }
    }
}
