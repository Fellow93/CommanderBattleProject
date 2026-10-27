using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommanderBattle.Network;
using NetworkMessages.FromClient;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer.Admin;
using TaleWorlds.MountAndBlade.Multiplayer.GauntletUI.Mission;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ObjectSystem;

namespace CommanderBattle.View
{
    // The stock admin panel (escape menu, Admin Panel) with Commander Battle's own option provider in place of the
    // stock one. The stock provider shows options this mode doesn't use (warmup, class toggles, formation markers),
    // and its map list filters out the campaign scenes. The panel takes providers through a creator list, which it
    // fills with the stock provider in OnMissionScreenInitialize and clears in OnMissionScreenFinalize, so the list is
    // replaced once per mission, on the first tick, when every view has initialised.
    public class CommanderBattleAdminPanelView : MissionView
    {
        private static readonly FieldInfo ProviderCreatorsField = typeof(MissionGauntletAdminPanel).GetField("_optionProviderCreators", BindingFlags.Instance | BindingFlags.NonPublic);

        private bool _replaced;

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (_replaced)
            {
                return;
            }
            _replaced = true;
            MissionGauntletAdminPanel panel = Mission.GetMissionBehavior<MissionGauntletAdminPanel>();
            if (panel == null)
            {
                return;
            }
            if (ProviderCreatorsField?.GetValue(panel) is MBList<MissionGauntletAdminPanel.CreateOptionProviderDelegeate> creators)
            {
                creators.Clear();
            }
            else
            {
                CommanderBattleLog.Write("Admin panel: the stock provider list was not found, its options stay");
            }
            panel.AddOptionProviderCreator(() => new CommanderBattleAdminPanelProvider(Mission));
        }
    }

    public class CommanderBattleAdminPanelProvider : IAdminPanelOptionProvider
    {
        private readonly Mission _mission;

        private readonly MissionMultiplayerCommanderBattleClient _client;

        private readonly MultiplayerAdminComponent _admin;

        private readonly MBList<IAdminPanelOptionGroup> _groups = new MBList<IAdminPanelOptionGroup>();

        private ChoiceOption _map;

        private ChoiceOption _attackerCulture;

        private ChoiceOption _defenderCulture;

        private NumberOption _roundTotal;

        private NumberOption _roundTimeLimit;

        private NumberOption _mapTimeLimit;

        private ChoiceOption _timeOfDay;

        private ChoiceOption _weather;

        public CommanderBattleAdminPanelProvider(Mission mission)
        {
            _mission = mission;
            _client = mission.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>();
            _admin = mission.GetMissionBehavior<MultiplayerAdminComponent>();
        }

        public MBReadOnlyList<IAdminPanelOptionGroup> GetOptionGroups()
        {
            _groups.Clear();
            _groups.Add(CreateMapGroup());
            _groups.Add(CreateMatchGroup());
            _groups.Add(CreateRoundGroup());
            _groups.Add(CreatePlayerGroup());
            return _groups;
        }

        // --- Next map ---

        private OptionGroup CreateMapGroup()
        {
            _map = new ChoiceOption("cb_map", "Map", "The map to play next.", GetMaps(), MultiplayerOptions.OptionType.Map.GetStrValue());
            List<ChoiceItem> cultures = GetCultures();
            _attackerCulture = new ChoiceOption("cb_attacker_culture", "Attacker Culture", "The attackers' culture on the next map.", cultures, MultiplayerOptions.OptionType.CultureTeam1.GetStrValue());
            _defenderCulture = new ChoiceOption("cb_defender_culture", "Defender Culture", "The defenders' culture on the next map.", cultures, MultiplayerOptions.OptionType.CultureTeam2.GetStrValue());
            OptionGroup group = new OptionGroup("cb_next_map", "Next Map", requiresRestart: true);
            group.Options.Add(_map);
            group.Options.Add(_attackerCulture);
            group.Options.Add(_defenderCulture);
            group.Actions.Add(new AdminAction("cb_change_map", "Change Map Now",
                "End the match and open the map above with these cultures, with no vote. Apply Changes instead keeps the match and uses them after it.",
                () => _map.GetValue() == null ? "No map to pick." : null,
                () =>
                {
                    SendOptions(forceMapAndCultures: true);
                    ClosePanel();
                    _admin?.AdminEndMission();
                }));
            return group;
        }

        // The vote pool's maps (the server sends every player the pool) and the usable maps (sent to admins as they
        // join), by the names the vote shows, plus the map being played.
        private static List<ChoiceItem> GetMaps()
        {
            HashSet<string> ids = new HashSet<string>(MultiplayerIntermissionVotingManager.Instance.MapVoteItems.Select(i => i.Id));
            ids.UnionWith(MultiplayerIntermissionVotingManager.Instance.GetUsableMaps(CommanderBattleSettings.GameModeName));
            string current = MultiplayerOptions.OptionType.Map.GetStrValue();
            if (!string.IsNullOrEmpty(current))
            {
                ids.Add(current);
            }
            return ids.Select(id => new ChoiceItem(id, GetMapName(id)))
                .OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string GetMapName(string sceneId)
        {
            return GameTexts.TryGetText("str_multiplayer_scene_name", out TextObject name, sceneId) ? $"{name} ({sceneId})" : sceneId;
        }

        // The multiplayer cultures: the main cultures that have multiplayer classes.
        private static List<ChoiceItem> GetCultures()
        {
            return MBObjectManager.Instance.GetObjectTypeList<BasicCultureObject>()
                .Where(c => c.IsMainCulture && MultiplayerClassDivisions.GetMPHeroClasses(c).Any())
                .Select(c => new ChoiceItem(c.StringId, c.Name.ToString()))
                .OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        // --- Match settings ---

        private OptionGroup CreateMatchGroup()
        {
            _roundTotal = new NumberOption("cb_round_total", "Number of Rounds", "Rounds in a match. Applies to the match being played and the next ones.", MultiplayerOptions.OptionType.RoundTotal);
            _roundTimeLimit = new NumberOption("cb_round_time_limit", "Round Time Limit", "Seconds a round lasts. Applies from the next round.", MultiplayerOptions.OptionType.RoundTimeLimit);
            _mapTimeLimit = new NumberOption("cb_map_time_limit", "Map Time Limit", "Minutes a match lasts. Applies from the next map.", MultiplayerOptions.OptionType.MapTimeLimit);
            OptionGroup group = new OptionGroup("cb_match", "Match", requiresRestart: false);
            group.Options.Add(_roundTotal);
            group.Options.Add(_roundTimeLimit);
            group.Options.Add(_mapTimeLimit);
            return group;
        }

        // --- Rounds ---

        private OptionGroup CreateRoundGroup()
        {
            OptionGroup group = new OptionGroup("cb_rounds", "Commander Battle", requiresRestart: false);
            // The setting the server last sent; -1 is the seed's weighted draw. A new map starts random again.
            List<ChoiceItem> times = new List<ChoiceItem> { new ChoiceItem("-1", "Random (weighted)") };
            for (int i = 0; i < CommanderBattleWeather.TimeCount; i++)
            {
                times.Add(new ChoiceItem(i.ToString(), CommanderBattleWeather.GetTimeName((CommanderBattleWeather.RoundTime)i)));
            }
            List<ChoiceItem> weathers = new List<ChoiceItem> { new ChoiceItem("-1", "Random (weighted)") };
            for (int i = 0; i < CommanderBattleWeather.WeatherCount; i++)
            {
                weathers.Add(new ChoiceItem(i.ToString(), CommanderBattleWeather.GetWeatherDisplayName((CommanderBattleWeather.RoundWeather)i)));
            }
            _timeOfDay = new ChoiceOption("cb_time_of_day", "Time of Day", "The time of day for every round of this map. Random is mostly around noon (late morning to early afternoon 65%, morning, afternoon and dusk the rest). Applies at once; a new map starts random again.", times, CommanderBattleWeather.ForcedTime.ToString());
            _weather = new ChoiceOption("cb_weather", "Weather", "The weather for every round of this map. Random is mostly clear (70% clear, 15% overcast, 10% fog, 5% rain). Applies at once; a new map starts random again.", weathers, CommanderBattleWeather.ForcedWeather.ToString());
            group.Options.Add(_timeOfDay);
            group.Options.Add(_weather);
            group.Actions.Add(new AdminAction("cb_restart_round", "Restart Round",
                "End the round being fought with no winner, and play it again.",
                () => IsRoundInProgress ? null : "No round is being fought.",
                () => Send(AdminCommandType.RestartRound)));
            group.Actions.Add(new AdminAction("cb_swap_sides", "Swap Sides",
                "Move every player to the other side. During a fight the round restarts; otherwise the sides swap at once or from the next round.",
                () => null,
                () => Send(AdminCommandType.SwapSides)));
            group.Actions.Add(new AdminAction("cb_release_formation", "Remove Commander",
                "Hand a player's formation to the AI and move the player to the spectators. They stay on the server and can join a side again.",
                () => _client?.Slots.FindAnyCommander() != null ? null : "No player commands a formation.",
                ShowReleaseFormationInquiry));
            return group;
        }

        private bool IsRoundInProgress => _client?.RoundComponent?.CurrentRoundState == MultiplayerRoundState.InProgress;

        private void Send(AdminCommandType command, NetworkCommunicator target = null)
        {
            _client?.RequestAdminCommand(command, target);
            ClosePanel();
        }

        private void ShowReleaseFormationInquiry()
        {
            if (_client == null)
            {
                return;
            }
            List<InquiryElement> commanders = new List<InquiryElement>();
            foreach (BattleSideEnum side in new[] { BattleSideEnum.Attacker, BattleSideEnum.Defender })
            {
                foreach (FormationSlot slot in _client.Slots.GetSlots(side))
                {
                    if (slot.Commander != null)
                    {
                        string sideName = side == BattleSideEnum.Attacker ? "Attackers" : "Defenders";
                        string formationName = slot.IsGeneral ? CommanderRoles.GetName(slot.Role) : $"{slot.Index + 1}. {CommanderRoles.GetName(slot.Role)}";
                        commanders.Add(new InquiryElement(slot.Commander, $"{slot.Commander.UserName} ({sideName}, {formationName})", null));
                    }
                }
            }
            ShowPlayerInquiry("Remove Commander", "Select the commander whose formation goes to the AI.", "Remove", commanders,
                target => Send(AdminCommandType.ReleaseFormation, target));
        }

        // --- Players (the stock actions) ---

        private OptionGroup CreatePlayerGroup()
        {
            OptionGroup group = new OptionGroup("cb_players", "Players", requiresRestart: false);
            group.Actions.Add(new AdminAction("cb_kick", "Kick Player", "Select a player to kick.", () => null,
                () => ShowPlayerInquiry("Kick Player", "Select the player to kick.", "Kick", AllPlayers(p => true), p => _admin?.KickPlayer(p, banPlayer: false))));
            group.Actions.Add(new AdminAction("cb_ban", "Ban Player", "Select a player to ban.", () => null,
                () => ShowPlayerInquiry("Ban Player", "Select the player to ban.", "Ban", AllPlayers(p => true), p => _admin?.KickPlayer(p, banPlayer: true))));
            group.Actions.Add(new AdminAction("cb_mute", "Mute Player", "Select a player to mute.", () => null,
                () => ShowPlayerInquiry("Mute Player", "Select the player to mute.", "Mute", AllPlayers(p => !MultiplayerGlobalMutedPlayersManager.IsUserMuted(p.VirtualPlayer.Id)), p => _admin?.GlobalMuteUnmutePlayer(p, unmute: false))));
            group.Actions.Add(new AdminAction("cb_unmute", "Unmute Player", "Select a player to unmute.", () => null,
                () => ShowPlayerInquiry("Unmute Player", "Select the player to unmute.", "Unmute", AllPlayers(p => MultiplayerGlobalMutedPlayersManager.IsUserMuted(p.VirtualPlayer.Id)), p => _admin?.GlobalMuteUnmutePlayer(p, unmute: true))));
            return group;
        }

        private static List<InquiryElement> AllPlayers(Func<NetworkCommunicator, bool> filter)
        {
            List<InquiryElement> players = new List<InquiryElement>();
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers)
            {
                if (!peer.IsServerPeer && filter(peer))
                {
                    players.Add(new InquiryElement(peer, peer.UserName, null));
                }
            }
            return players;
        }

        private static void ShowPlayerInquiry(string title, string text, string affirmative, List<InquiryElement> players, Action<NetworkCommunicator> onPicked)
        {
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, text, players, isExitShown: true, 0, 1, affirmative, "Cancel", delegate (List<InquiryElement> selected)
            {
                if (selected != null && selected.Count == 1 && selected[0].Identifier is NetworkCommunicator target)
                {
                    onPicked(target);
                }
            }, null, string.Empty, isSeachAvailable: true));
        }

        // --- Applying ---

        // The panel's Apply Changes.
        public void ApplyOptions()
        {
            SendOptions(forceMapAndCultures: false);
        }

        // Through the stock AdminUpdateMultiplayerOptions, which the server applies for admins only. The server reads an
        // empty game type and an empty map as undecided, which keeps the vote after the match; a map given turns the vote
        // off for the next maps, so it is only sent when the admin picked one. Cultures go only as a pair (one alone
        // falls back to the vote too).
        private void SendOptions(bool forceMapAndCultures)
        {
            const MultiplayerOptions.MultiplayerOptionsAccessMode Next = MultiplayerOptions.MultiplayerOptionsAccessMode.NextMapOptions;
            const MultiplayerOptions.MultiplayerOptionsAccessMode Current = MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions;
            AdminUpdateMultiplayerOptions message = new AdminUpdateMultiplayerOptions();
            message.AddMultiplayerOption(MultiplayerOptions.OptionType.GameType, Next, "");
            bool sendMap = forceMapAndCultures || _map.IsDirty;
            message.AddMultiplayerOption(MultiplayerOptions.OptionType.Map, Next, sendMap ? _map.GetValue()?.Value ?? "" : "");
            bool sendCultures = forceMapAndCultures || _attackerCulture.IsDirty || _defenderCulture.IsDirty;
            message.AddMultiplayerOption(MultiplayerOptions.OptionType.CultureTeam1, Next, sendCultures ? _attackerCulture.GetValue()?.Value ?? "" : "");
            message.AddMultiplayerOption(MultiplayerOptions.OptionType.CultureTeam2, Next, sendCultures ? _defenderCulture.GetValue()?.Value ?? "" : "");
            // The round controller reads these as each round starts and ends, so they also apply to this match.
            foreach (NumberOption option in new[] { _roundTotal, _roundTimeLimit })
            {
                message.AddMultiplayerOption(option.OptionType, Current, option.GetValue());
                message.AddMultiplayerOption(option.OptionType, Next, option.GetValue());
            }
            message.AddMultiplayerOption(_mapTimeLimit.OptionType, Next, _mapTimeLimit.GetValue());
            CommanderBattleNetwork.SendToServer(message);
            if (_timeOfDay.IsDirty || _weather.IsDirty)
            {
                _client?.RequestWeatherOverride(ParseChoice(_timeOfDay), ParseChoice(_weather));
            }
            foreach (IAdminPanelOptionGroup group in _groups)
            {
                foreach (IAdminPanelOption option in group.Options)
                {
                    (option as OptionBase)?.MarkApplied();
                }
            }
        }

        private static int ParseChoice(ChoiceOption option)
        {
            return int.TryParse(option.GetValue()?.Value, out int value) ? value : -1;
        }

        private void ClosePanel()
        {
            _admin?.ChangeAdminMenuActiveState(isActive: false);
        }

        public IAdminPanelOption GetOptionWithId(string id)
        {
            return _groups.SelectMany(g => g.Options).FirstOrDefault(o => o.UniqueId == id);
        }

        public IAdminPanelAction GetActionWithId(string id)
        {
            return _groups.SelectMany(g => g.Actions).FirstOrDefault(a => a.UniqueId == id);
        }

        public void OnTick(float dt)
        {
        }

        public void OnFinalize()
        {
            _groups.Clear();
        }

        // --- The panel's interfaces (the stock helper classes are internal) ---

        private class OptionGroup : IAdminPanelOptionGroup
        {
            public OptionGroup(string uniqueId, string name, bool requiresRestart)
            {
                UniqueId = uniqueId;
                Name = new TextObject("{=!}" + name);
                RequiresRestart = requiresRestart;
            }

            public string UniqueId { get; }

            public bool RequiresRestart { get; }

            public TextObject Name { get; }

            public MBList<IAdminPanelOption> Options { get; } = new MBList<IAdminPanelOption>();

            public MBList<IAdminPanelAction> Actions { get; } = new MBList<IAdminPanelAction>();

            MBReadOnlyList<IAdminPanelOption> IAdminPanelOptionGroup.Options => Options;

            MBReadOnlyList<IAdminPanelAction> IAdminPanelOptionGroup.Actions => Actions;

            public void OnFinalize()
            {
                Options.Clear();
                Actions.Clear();
            }
        }

        private class AdminAction : IAdminPanelAction
        {
            // Returns why the action can't run now, or null.
            private readonly Func<string> _disabledReason;

            private readonly Action _onExecuted;

            public AdminAction(string uniqueId, string name, string description, Func<string> disabledReason, Action onExecuted)
            {
                UniqueId = uniqueId;
                Name = name;
                Description = description;
                _disabledReason = disabledReason;
                _onExecuted = onExecuted;
            }

            public string UniqueId { get; }

            public string Name { get; }

            public string Description { get; }

            public bool GetIsDisabled(out string reason)
            {
                reason = _disabledReason() ?? string.Empty;
                return reason.Length > 0;
            }

            public void OnActionExecuted()
            {
                _onExecuted();
            }

            public bool GetIsAvailable()
            {
                return true;
            }
        }

        // An option's value against the value it had when the panel opened or was last applied.
        private abstract class OptionBase : IAdminPanelOption
        {
            private Action _onRefresh;

            protected OptionBase(string uniqueId, string name, string description)
            {
                UniqueId = uniqueId;
                Name = name;
                Description = description;
            }

            public string UniqueId { get; }

            public string Name { get; }

            public string Description { get; }

            public bool RequiresMissionRestart => false;

            public bool IsRequired => false;

            public abstract bool IsDirty { get; }

            public bool CanRevertToDefaultValue => false;

            public virtual bool GetIsDisabled(out string reason)
            {
                reason = string.Empty;
                return false;
            }

            public bool GetIsAvailable()
            {
                return true;
            }

            public abstract void RevertChanges();

            public void RestoreDefaults()
            {
                RevertChanges();
            }

            public abstract void MarkApplied();

            public void SetOnRefreshCallback(Action callback)
            {
                _onRefresh = callback;
            }

            protected void Refresh()
            {
                _onRefresh?.Invoke();
            }
        }

        private class ChoiceItem : IAdminPanelMultiSelectionItem
        {
            public ChoiceItem(string value, string displayName)
            {
                Value = value;
                DisplayName = displayName;
            }

            public string Value { get; }

            public string DisplayName { get; }

            public bool IsFallbackValue => false;

            public bool IsDisabled => false;

            public bool CanBeApplied => true;
        }

        private class ChoiceOption : OptionBase, IAdminPanelMultiSelectionOption
        {
            private readonly MBList<IAdminPanelMultiSelectionItem> _items = new MBList<IAdminPanelMultiSelectionItem>();

            private IAdminPanelMultiSelectionItem _applied;

            private IAdminPanelMultiSelectionItem _value;

            public ChoiceOption(string uniqueId, string name, string description, List<ChoiceItem> items, string current)
                : base(uniqueId, name, description)
            {
                foreach (ChoiceItem item in items)
                {
                    _items.Add(item);
                }
                _applied = _items.FirstOrDefault(i => i.Value == current) ?? _items.FirstOrDefault();
                _value = _applied;
            }

            public override bool IsDirty => _value != _applied;

            public override bool GetIsDisabled(out string reason)
            {
                reason = _items.Count == 0 ? "Nothing to pick." : string.Empty;
                return _items.Count == 0;
            }

            public MBReadOnlyList<IAdminPanelMultiSelectionItem> GetAvailableOptions()
            {
                return _items;
            }

            public IAdminPanelMultiSelectionItem GetValue()
            {
                return _value;
            }

            public void SetValue(IAdminPanelMultiSelectionItem value)
            {
                if (value != null && value != _value)
                {
                    _value = value;
                    Refresh();
                }
            }

            public override void RevertChanges()
            {
                _value = _applied;
                Refresh();
            }

            public override void MarkApplied()
            {
                _applied = _value;
                Refresh();
            }
        }

        private class NumberOption : OptionBase, IAdminPanelNumericOption
        {
            private int _applied;

            private int _value;

            public NumberOption(string uniqueId, string name, string description, MultiplayerOptions.OptionType optionType)
                : base(uniqueId, name, description)
            {
                OptionType = optionType;
                _applied = optionType.GetIntValue();
                _value = _applied;
            }

            public MultiplayerOptions.OptionType OptionType { get; }

            public override bool IsDirty => _value != _applied;

            public int? GetMinimumValue()
            {
                MultiplayerOptionsProperty property = OptionType.GetOptionProperty();
                return property.HasBounds ? property.BoundsMin : (int?)null;
            }

            public int? GetMaximumValue()
            {
                MultiplayerOptionsProperty property = OptionType.GetOptionProperty();
                return property.HasBounds ? property.BoundsMax : (int?)null;
            }

            public int GetValue()
            {
                return _value;
            }

            public void SetValue(int value)
            {
                if (value != _value)
                {
                    _value = value;
                    Refresh();
                }
            }

            public override void RevertChanges()
            {
                _value = _applied;
                Refresh();
            }

            public override void MarkApplied()
            {
                _applied = _value;
                Refresh();
            }
        }
    }
}
