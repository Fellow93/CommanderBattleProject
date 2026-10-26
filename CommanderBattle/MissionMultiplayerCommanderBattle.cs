using System.Collections.Generic;
using System.Reflection;
using CommanderBattle.AI;
using CommanderBattle.Network;
using NetworkMessages.FromServer;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.MissionRepresentatives;
using TaleWorlds.MountAndBlade.Missions.Multiplayer;
using TaleWorlds.MountAndBlade.Network.Messages;
using TaleWorlds.ObjectSystem;

namespace CommanderBattle
{
    // Server-side rules: Attackers vs Defenders, each an army of formations (CommanderBattleSettings) whose sizes
    // follow their classes (ComputeTroopCounts). Players take command of a formation; the AI leads the rest.
    // A round ends when one side has no soldiers left (or on time, won by the side with more soldiers).
    // No flags, morale or gold.
    public partial class MissionMultiplayerCommanderBattle : MissionMultiplayerGameModeBase
    {
        private static readonly MethodInfo IsMatchEndingSetter = typeof(MultiplayerRoundController).GetProperty(nameof(MultiplayerRoundController.IsMatchEnding))?.GetSetMethod(nonPublic: true);

        private readonly List<NetworkCommunicator> _peersToPlace = new List<NetworkCommunicator>();

        // Class groups currently disabled in each player's class picker, as last sent to them.
        private readonly Dictionary<NetworkCommunicator, bool[]> _sentClassRestrictions = new Dictionary<NetworkCommunicator, bool[]>();

        private readonly List<CommanderBattleTactic> _tactics = new List<CommanderBattleTactic>();

        public FormationSlotTable Slots { get; private set; }

        public override bool IsGameModeHidingAllAgentVisuals => true;

        public override bool IsGameModeUsingOpposingTeams => true;

        // Captain is the stock type closest to this mode: the client UI keys bot counts on the scoreboard,
        // troop preloading and the class loadout off it.
        public override MultiplayerGameType GetMissionType()
        {
            return MultiplayerGameType.Captain;
        }

        public override bool UseRoundController()
        {
            return true;
        }

        // Its only effect is whether the server takes a client's banner code (FromClient.CreateBanner) and passes it on
        // unchecked; the spawners paint shields either way. Commanders' banners come from ids the server checks
        // (MissionMultiplayerCommanderBattle.Banners.cs).
        public override bool AllowCustomPlayerBanners()
        {
            return false;
        }

        public override void OnBehaviorInitialize()
        {
            CommanderBattleLog.Detail("GameMode.OnBehaviorInitialize");
            base.OnBehaviorInitialize();
            ApplyRequiredOptions();
            // Shared with the client half of the mode, which the UI reads (matters on a listen server).
            Slots = Mission.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>().Slots;
            AssignDefaultClasses();
        }

        public override void AfterStart()
        {
            CommanderBattleLog.Detail("GameMode.AfterStart");
            base.AfterStart();
            RoundController.OnPreRoundEnding += OnPreRoundEnding;
            RoundController.OnPostRoundEnded += OnPostRoundEnded;
            RoundController.OnCurrentRoundStateChanged += OnRoundStateChanged;
            MissionPeer.OnTeamChanged += OnPeerTeamChanged;
            MissionPeer.OnPreTeamChanged += OnPreTeamChanged;
            // A field battle to the singleplayer formation AI the team AI uses, and to mods built on it (RBM's AI checks
            // Mission.IsFieldBattle). Set here, after the spawn paths were initialised, so they stay the same as the
            // client's, which keeps the default.
            Mission.MissionTeamAIType = Mission.MissionTeamAITypeEnum.FieldBattle;
            CreateTeams();
            AddTeamAI();
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                BasicCultureObject culture = CommanderBattleTeams.GetCulture(side);
                CommanderBattleLog.Detail($"{side} ({culture?.StringId}) formations: {CommanderBattleSettings.DescribeSplit(culture)}{(CommanderBattleSettings.IsConverted(culture) ? "" : ", fallback split (culture not converted)")}");
                if (!CommanderBattleSettings.IsSlotFielded(culture, CommanderBattleSettings.GeneralSlot))
                {
                    CommanderBattleLog.Write($"{side} ({culture?.StringId}) fields no general: the culture has no {CommanderRoles.GeneralClassPrefix}<culture> class");
                }
            }
        }

        // Rounds reuse the teams, and Team.Reset leaves the general's fields alone.
        public override void OnClearScene()
        {
            base.OnClearScene();
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                CommanderBattleTeams.ClearGeneral(CommanderBattleTeams.GetTeam(Mission, side));
            }
            ClearBodyguardTargets();
            ClearGeneralOrders(notify: false);
            // Team.Reset (Formation.Reset) has already handed every formation back to the AI, here and on the clients.
            ClearGeneralCommandState();
            ClearCheers();
        }

        private void OnRoundStateChanged()
        {
            CommanderBattleLog.Write($"Round {RoundController.RoundCount}: {RoundController.CurrentRoundState}");
            CommanderBattleLog.Detail($"Round {RoundController.RoundCount}: lobby {MissionLobbyComponent.CurrentMultiplayerState}, spawning {SpawnComponent.AreAgentsSpawning()}");
            if (RoundController.CurrentRoundState != MultiplayerRoundState.InProgress)
            {
                // The general's orders last until the round ends, and so does his command of the AI's formations.
                ClearGeneralOrders(notify: true);
                ReleaseAllGeneralCommand("the round is over");
            }
            if (RoundController.CurrentRoundState == MultiplayerRoundState.Preparation)
            {
                NewSpawnLayout();
            }
            else if (RoundController.CurrentRoundState == MultiplayerRoundState.InProgress)
            {
                // Raised as preparation ends, before anyone spawns.
                FreezeTroopCounts();
            }
        }

        // Each round the armies start somewhere else in their spawn zones; the clients get the same seed.
        private void NewSpawnLayout()
        {
            int seed = 1 + MBRandom.RandomInt(SpawnLayoutSeedMax);
            _troopCountsFrozen = false;
            // Troop picks made since the last armies spawned take effect now, so the counts below follow them.
            ApplyPendingSelections();
            ApplyPendingBanners();
            Slots.SetTroopCounts(ComputeTroopCounts(log: false));
            ApplySpawnLayout(seed);
            CommanderBattleLog.Detail($"Spawn layout seed {seed}");
        }

        // The layout is planned from the round's seed and troop counts, here and on every client.
        private void ApplySpawnLayout(int seed)
        {
            int[] counts = Slots.GetTroopCounts();
            CommanderBattleSpawnLayout.SetRound(Mission, seed, counts);
            CommanderBattleNetwork.Broadcast(new SpawnLayoutSeed(seed, counts));
        }

        private const int SpawnLayoutSeedMax = (1 << 30) - 2;

        public override void OnRemoveBehavior()
        {
            RoundController.OnPreRoundEnding -= OnPreRoundEnding;
            RoundController.OnPostRoundEnded -= OnPostRoundEnded;
            RoundController.OnCurrentRoundStateChanged -= OnRoundStateChanged;
            MissionPeer.OnTeamChanged -= OnPeerTeamChanged;
            MissionPeer.OnPreTeamChanged -= OnPreTeamChanged;
            base.OnRemoveBehavior();
        }

        protected override void AddRemoveMessageHandlers(GameNetwork.NetworkMessageHandlerRegistererContainer registerer)
        {
            if (GameNetwork.IsServer)
            {
                registerer.RegisterBaseHandler<RequestFormationCommand>(HandleClientEventRequestFormationCommand);
                registerer.RegisterBaseHandler<RequestRoleCommand>(HandleClientEventRequestRoleCommand);
                registerer.RegisterBaseHandler<RequestTroopSelection>(HandleClientEventRequestTroopSelection);
                registerer.RegisterBaseHandler<AdminCommand>(HandleClientEventAdminCommand);
                registerer.RegisterBaseHandler<RequestGeneralOrder>(HandleClientEventRequestGeneralOrder);
                registerer.RegisterBaseHandler<RequestDeleteGeneralOrder>(HandleClientEventRequestDeleteGeneralOrder);
                registerer.RegisterBaseHandler<RequestGeneralCommand>(HandleClientEventRequestGeneralCommand);
                registerer.RegisterBaseHandler<RequestCommanderBanner>(HandleClientEventRequestCommanderBanner);
                // Runs after the stock handler (MissionNetworkComponent), which plays the commander's own taunt.
                registerer.RegisterBaseHandler<NetworkMessages.FromClient.TauntSelected>(HandleClientEventTauntSelected);
            }
        }

        // The stock spawner reads these; the troop counts depend on them.
        private static void ApplyRequiredOptions()
        {
            MultiplayerOptions.OptionType.NumberOfBotsPerFormation.SetValue(CommanderBattleSettings.BotsPerFormationBaseline);
            MultiplayerOptions.OptionType.NumberOfBotsTeam1.SetValue(0);
            MultiplayerOptions.OptionType.NumberOfBotsTeam2.SetValue(0);
            MultiplayerOptions.OptionType.AutoTeamBalanceThreshold.SetValue(0);
            // Without warmup the round controller would end the match right away when the first player is
            // still choosing a side. The AI armies fight until players take over formations.
            MultiplayerOptions.OptionType.MinNumberOfPlayersForMatchStart.SetValue(0);
        }

        // AI-led formations spread over the role's weights, heaviest first (3 infantry: heavy, medium, light), and the
        // k-th one of a role fields its class's setup k (wrapping). Each culture's XML lists its setups in the order
        // its AI should field them, the culture's signature setup first, because the setups differ per culture and
        // some only exist on the medium and heavy weights. Deterministic, so every mission starts from the same army
        // and the log can be compared.
        private void AssignDefaultClasses()
        {
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                BasicCultureObject culture = CommanderBattleTeams.GetCulture(side);
                Dictionary<CommanderRole, int> usedPerRole = new Dictionary<CommanderRole, int>();
                foreach (FormationSlot slot in Slots.GetSlots(side))
                {
                    List<int> weights = CommanderRoles.GetWeightClassIndexes(culture, slot.Role);
                    usedPerRole.TryGetValue(slot.Role, out int used);
                    usedPerRole[slot.Role] = used + 1;
                    // A general the culture doesn't field has no class, rather than another role's class 0.
                    slot.ClassIndex = weights.Count > 0 ? weights[weights.Count - 1 - used % weights.Count] : (slot.IsGeneral ? -1 : 0);
                    int setups = CommanderRoles.GetWeaponSetups(CommanderRoles.GetClass(culture, slot.ClassIndex)).Count;
                    slot.WeaponIndex = setups > 0 ? used % setups : 0;
                }
            }
        }

        private void CreateTeams()
        {
            BasicCultureObject attackerCulture = CommanderBattleTeams.GetCulture(BattleSideEnum.Attacker);
            BasicCultureObject defenderCulture = CommanderBattleTeams.GetCulture(BattleSideEnum.Defender);
            MultiplayerBattleColors colors = MultiplayerBattleColors.CreateWith(attackerCulture, defenderCulture);
            Banner attackerBanner = new Banner(attackerCulture.Banner, colors.AttackerColors.BannerBackgroundColorUint, colors.AttackerColors.BannerForegroundColorUint);
            Banner defenderBanner = new Banner(defenderCulture.Banner, colors.DefenderColors.BannerBackgroundColorUint, colors.DefenderColors.BannerForegroundColorUint);
            Mission.Teams.Add(BattleSideEnum.Attacker, colors.AttackerColors.BannerBackgroundColorUint, colors.AttackerColors.BannerForegroundColorUint, attackerBanner, isPlayerGeneral: false, isPlayerSergeant: true);
            Mission.Teams.Add(BattleSideEnum.Defender, colors.DefenderColors.BannerBackgroundColorUint, colors.DefenderColors.BannerForegroundColorUint, defenderBanner, isPlayerGeneral: false, isPlayerSergeant: true);
        }

        // RBM Multiplayer registers RBM's own field-battle tactics on these teams instead of CommanderBattleTactic.
        private const string RbmMultiplayerModuleId = "RBM.Multiplayer";

        // The AI leads every formation without a commander; formations led by players are left alone.
        private void AddTeamAI()
        {
            bool rbmTactics = ModuleHelper.IsModuleActive(RbmMultiplayerModuleId);
            CommanderBattleLog.Detail(rbmTactics ? "Team AI: RBM's tactics (RBM.Multiplayer)" : "Team AI: CommanderBattleTactic");
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                Team team = CommanderBattleTeams.GetTeam(Mission, side);
                if (team == null)
                {
                    continue;
                }
                team.AddTeamAI(new CommanderBattleTeamAI(Mission, team));
                if (rbmTactics)
                {
                    continue;
                }
                CommanderBattleTactic tactic = new CommanderBattleTactic(team);
                team.AddTacticOption(tactic);
                _tactics.Add(tactic);
            }
        }

        // Called by the spawner once both armies stand on the field.
        public void OnArmiesSpawned()
        {
            foreach (CommanderBattleTactic tactic in _tactics)
            {
                tactic.OnArmiesSpawned();
            }
        }

        private float _statusLogTimer;

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            _statusLogTimer += dt;
            if (_statusLogTimer >= 30f)
            {
                _statusLogTimer = 0f;
                CommanderBattleLog.Detail($"Status: round {RoundController.RoundCount} {RoundController.CurrentRoundState}, timer {TimerComponent.GetRemainingTime(isSynched: false):0.0}s, mission time {Mission.CurrentTime:0.0}, lobby {MissionLobbyComponent.CurrentMultiplayerState}, matchEnding {RoundController.IsMatchEnding}");
            }
            ApplyPendingSwap();
            PlaceQueuedPeers();
            if (CanGameModeSystemsTickThisFrame)
            {
                ReleaseDisconnectedCommanders();
                SyncCommanderClasses();
                UpdateTroopCounts();
                UpdateClassRestrictions();
            }
            if (MissionLobbyComponent.CurrentMultiplayerState == MissionLobbyComponent.MultiplayerGameState.Playing)
            {
                CheckForCommandersTakingOverSoldiers();
                KeepBodyguardsBehindGenerals();
            }
            SendPendingSuggestions();
            PlayQueuedCheers();
        }

        // --- Formation slots ---

        private void SetSlot(FormationSlot slot, NetworkCommunicator commander, int classIndex, int weaponIndex)
        {
            NetworkCommunicator previous = slot.Commander;
            Slots.Set(slot.Side, slot.Index, commander, classIndex, weaponIndex);
            CommanderBattleNetwork.Broadcast(new FormationSlotUpdate(slot.Side, slot.Index, commander, classIndex, weaponIndex));
            // Every way the general's slot changes hands passes here: a disconnect, a team change, a claim, an admin move.
            if (slot.IsGeneral && previous != commander && previous != null)
            {
                ReleaseGeneralCommand(slot.Side, commander == null ? $"{previous.UserName} is no longer the general" : $"{commander.UserName} is the new general");
            }
        }

        private void ReleaseSlotOf(NetworkCommunicator peer)
        {
            FormationSlot slot = Slots.FindSlotOf(peer);
            if (slot != null)
            {
                // The AI takes over with the class and weapon setup the player last chose.
                SetSlot(slot, null, slot.ClassIndex, slot.WeaponIndex);
            }
        }

        private bool HandleClientEventRequestFormationCommand(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            RequestFormationCommand message = (RequestFormationCommand)baseMessage;
            RequestFormationCommandFrom(peer, message.SlotIndex);
            return true;
        }

        private bool HandleClientEventRequestRoleCommand(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            RequestRoleCommand message = (RequestRoleCommand)baseMessage;
            RequestRoleCommandFrom(peer, message.Role);
            return true;
        }

        // The troop screen's first step: command a formation of this role. A commander who already leads one keeps it;
        // otherwise the lowest free slot of the role is taken under the same rules as taking it from the list (a
        // commander of another role moves).
        public void RequestRoleCommandFrom(NetworkCommunicator peer, CommanderRole role)
        {
            MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
            if (missionPeer?.Team == null || missionPeer.Team.Side == BattleSideEnum.None)
            {
                return;
            }
            BattleSideEnum side = missionPeer.Team.Side;
            FormationSlot current = Slots.FindSlotOf(peer);
            if (current != null && current.Side == side && current.Role == role)
            {
                return;
            }
            FormationSlot free = Slots.FindFreeSlot(side, role);
            if (free == null)
            {
                Slots.CountRole(side, role, out _, out int total);
                if (role == CommanderRole.General)
                {
                    SendServerMessage(peer, total == 0
                        ? $"{CommanderBattleTeams.GetCulture(side)?.Name} fields no general."
                        : $"{Slots.GetSlot(side, CommanderBattleSettings.GeneralSlot)?.Commander?.UserName} is already the general.");
                    return;
                }
                SendServerMessage(peer, total == 0
                    ? $"{CommanderBattleTeams.GetCulture(side)?.Name} fields no {CommanderRoles.GetName(role).ToLowerInvariant()} formations."
                    : $"Every {CommanderRoles.GetName(role).ToLowerInvariant()} formation already has a commander.");
                return;
            }
            RequestFormationCommandFrom(peer, free.Index);
        }

        public void RequestFormationCommandFrom(NetworkCommunicator peer, int slotIndex)
        {
            MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
            if (missionPeer?.Team == null || missionPeer.Team.Side == BattleSideEnum.None)
            {
                return;
            }
            FormationSlot slot = Slots.GetSlot(missionPeer.Team.Side, slotIndex);
            if (slot == null || slot.Commander == peer)
            {
                return;
            }
            if (!slot.IsFielded)
            {
                SendServerMessage(peer, $"{CommanderBattleTeams.GetCulture(slot.Side)?.Name} fields no general.");
                return;
            }
            if (slot.Commander != null)
            {
                SendServerMessage(peer, slot.IsGeneral ? $"{slot.Commander.UserName} is already the general." : $"{slot.Commander.UserName} already commands that formation.");
                return;
            }
            if (RoundController.IsRoundInProgress && missionPeer.SpawnCountThisRound > 0)
            {
                SendServerMessage(peer, "You can change formation before the next round.");
                return;
            }
            Claim(peer, missionPeer, slot);
        }

        // The new commander takes the formation over as it stands, class and weapon setup, so taking it never changes
        // its size; the troop screen that opens next is where they change it.
        private void Claim(NetworkCommunicator peer, MissionPeer missionPeer, FormationSlot slot)
        {
            ReleaseSlotOf(peer);
            int classIndex = slot.ClassIndex;
            int weaponIndex = slot.WeaponIndex;
            SetCommanderTroops(peer, missionPeer, classIndex, CommanderRoles.MakePerks(weaponIndex));
            SetSlot(slot, peer, classIndex, weaponIndex);
            ResetPreparationVisuals(peer, missionPeer);
            CommanderBattleLog.Write($"{peer.UserName} commands {slot.Side} {slot.LogName}");
        }

        // A commander who switches formation during preparation gets their visuals rebuilt in the new one.
        private void ResetPreparationVisuals(NetworkCommunicator peer, MissionPeer missionPeer)
        {
            if (missionPeer.ControlledAgent != null || !missionPeer.HasSpawnedAgentVisuals)
            {
                return;
            }
#if !SERVER
            Mission.GetMissionBehavior<MultiplayerMissionAgentVisualSpawnComponent>()?.RemoveAgentVisuals(missionPeer, sync: true);
#endif
            CommanderBattleNetwork.Broadcast(new RemoveAgentVisualsForPeer(peer));
            missionPeer.HasSpawnedAgentVisuals = false;
            if (missionPeer.ControlledFormation != null)
            {
                missionPeer.ControlledFormation.ContainsAgentVisuals = false;
                missionPeer.ControlledFormation = null;
            }
        }

        private static void ForceTroopIndex(NetworkCommunicator peer, MissionPeer missionPeer, int classIndex)
        {
            missionPeer.SelectedTroopIndex = classIndex;
            CommanderBattleNetwork.Broadcast(new UpdateSelectedTroopIndex(peer, classIndex));
        }

        // The commander's troop index and perks, which the spawner and the combat perks read. Perks go through the
        // stock sync so every client agrees (they feed the stat models).
        private static void SetCommanderTroops(NetworkCommunicator commander, MissionPeer missionPeer, int classIndex, int[] perks)
        {
            ForceTroopIndex(commander, missionPeer, classIndex);
            if (classIndex < 0 || classIndex >= MissionPeer.MaxNumberOfTroopTypesPerCulture)
            {
                return;
            }
            for (int i = 0; i < CommanderRoles.PerkListCount; i++)
            {
                missionPeer.SelectPerk(i, perks[i], classIndex);
            }
            CommanderBattleNetwork.Broadcast(new SyncPerksForCurrentlySelectedTroop(commander, missionPeer.Perks[classIndex]));
        }

        // --- Troop selection (the selection screen) ---

        private bool HandleClientEventRequestTroopSelection(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            RequestTroopSelection message = (RequestTroopSelection)baseMessage;
            RequestTroopSelectionFrom(peer, message.ClassIndex, message.Perks);
            return true;
        }

        // A commander picks their formation's weight (the class) and weapon setup (its perk). Before this round's armies
        // spawn it applies at once (the preview, counts and layout follow); after that it waits for the next round's
        // preparation, because the spawner reads the commander's live perks and troop index, and combat perks would
        // change the troops already fighting.
        public void RequestTroopSelectionFrom(NetworkCommunicator peer, int classIndex, int[] perks)
        {
            FormationSlot slot = Slots.FindSlotOf(peer);
            if (slot == null || peer.GetComponent<MissionPeer>()?.Team?.Side != slot.Side)
            {
                SendServerMessage(peer, "Take command of a formation first.");
                return;
            }
            MultiplayerClassDivisions.MPHeroClass heroClass = CommanderRoles.GetClass(CommanderBattleTeams.GetCulture(slot.Side), classIndex);
            if (heroClass == null || CommanderRoles.GetRole(heroClass) != slot.Role)
            {
                SendServerMessage(peer, $"Your formation takes {CommanderRoles.GetName(slot.Role).ToLowerInvariant()} troops.");
                return;
            }
            if (!CommanderRoles.ArePerksValid(heroClass, perks))
            {
                SendServerMessage(peer, $"That weapon setup doesn't belong to the {heroClass.TroopName}.");
                return;
            }
            if (!_troopCountsFrozen)
            {
                ClearPendingSelection(slot);
                ApplySelection(slot, classIndex, perks);
                UpdateTroopCounts();
                SendServerMessage(peer, $"{heroClass.TroopName}: your troops for this round.");
                return;
            }
            if (IsCurrentSelection(slot, classIndex, perks))
            {
                // Picked back what is already fielded: nothing changes next round.
                ClearPendingSelection(slot);
                SendServerMessage(peer, $"{heroClass.TroopName}: you keep your troops next round.");
                return;
            }
            SetPendingSelection(slot, classIndex, perks);
            CommanderBattleLog.Write($"{peer.UserName} picked {heroClass.StringId} ({CommanderRoles.GetWeaponName(heroClass, perks[CommanderRoles.WeaponPerkList])}) for {slot.Side} {slot.LogName}, from next round");
            SendServerMessage(peer, $"{heroClass.TroopName}: your troops from next round.");
        }

        private static bool IsCurrentSelection(FormationSlot slot, int classIndex, int[] perks)
        {
            MissionPeer missionPeer = slot.Commander?.GetComponent<MissionPeer>();
            if (missionPeer == null || slot.ClassIndex != classIndex || missionPeer.SelectedTroopIndex != classIndex)
            {
                return false;
            }
            for (int i = 0; i < CommanderRoles.PerkListCount; i++)
            {
                if (missionPeer.GetSelectedPerkIndexWithPerkListIndex(classIndex, i) != perks[i])
                {
                    return false;
                }
            }
            return true;
        }

        // Pending picks apply as the next round's preparation starts, also for a formation whose commander has left:
        // the AI fields its type and weapon setup.
        private void ApplyPendingSelections()
        {
            foreach (FormationSlot slot in Slots.AllSlots)
            {
                if (!slot.HasPendingSelection)
                {
                    continue;
                }
                int classIndex = slot.PendingClassIndex;
                int[] perks = slot.PendingPerks;
                ClearPendingSelection(slot);
                ApplySelection(slot, classIndex, perks);
            }
        }

        // The formation's class and weapon setup, and its commander's troop index and perks. A commander's preparation
        // visuals are rebuilt with the new troops. Without a commander the AI fields them (SpawnAiFormation).
        private void ApplySelection(FormationSlot slot, int classIndex, int[] perks)
        {
            NetworkCommunicator commander = slot.Commander;
            MissionPeer missionPeer = commander?.GetComponent<MissionPeer>();
            if (missionPeer != null)
            {
                SetCommanderTroops(commander, missionPeer, classIndex, perks);
            }
            int weaponIndex = perks[CommanderRoles.WeaponPerkList];
            SetSlot(slot, commander, classIndex, weaponIndex);
            if (missionPeer != null)
            {
                ResetPreparationVisuals(commander, missionPeer);
            }
            MultiplayerClassDivisions.MPHeroClass heroClass = CommanderRoles.GetClass(CommanderBattleTeams.GetCulture(slot.Side), classIndex);
            CommanderBattleLog.Detail($"{slot.Side} formation {slot.Index} ({(commander != null ? commander.UserName : "AI")}) now fields {heroClass?.StringId ?? "?"} ({CommanderRoles.GetWeaponName(heroClass, weaponIndex)})");
        }

        private void SetPendingSelection(FormationSlot slot, int classIndex, int[] perks)
        {
            Slots.SetPendingSelection(slot.Side, slot.Index, classIndex, perks);
            CommanderBattleNetwork.Broadcast(new FormationSlotPendingSelection(slot.Side, slot.Index, slot.PendingClassIndex, slot.PendingPerks));
        }

        private void ClearPendingSelection(FormationSlot slot)
        {
            if (slot.HasPendingSelection)
            {
                SetPendingSelection(slot, -1, null);
            }
        }

        private void OnPeerTeamChanged(NetworkCommunicator peer, Team previousTeam, Team newTeam)
        {
            FormationSlot slot = Slots.FindSlotOf(peer);
            if (slot != null && (newTeam == null || newTeam.Side != slot.Side))
            {
                ReleaseSlotOf(peer);
            }
            if (newTeam != null && newTeam.Side != BattleSideEnum.None && !_peersToPlace.Contains(peer))
            {
                _peersToPlace.Add(peer);
            }
            // The client files orders by side and drops the other sides' on its own team change, so these are kept
            // even if they arrive before it learns of the change.
            SendGeneralOrdersTo(peer, newTeam?.Side ?? BattleSideEnum.None);
        }

        // Someone who joins a side takes the first free formation, preferring one matching their current class, so a
        // player always leads a formation and spawns even if they never pick; their client opens the troop screen at
        // its first step (formation class) on top, where they can switch. Deferred to the tick so we never re-enter
        // ChangeTeamServer from its own event.
        private void PlaceQueuedPeers()
        {
            if (_peersToPlace.Count == 0)
            {
                return;
            }
            List<NetworkCommunicator> peers = new List<NetworkCommunicator>(_peersToPlace);
            _peersToPlace.Clear();
            foreach (NetworkCommunicator peer in peers)
            {
                MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
                if (!peer.IsConnectionActive || missionPeer?.Team == null || missionPeer.Team.Side == BattleSideEnum.None)
                {
                    continue;
                }
                BattleSideEnum side = missionPeer.Team.Side;
                FormationSlot current = Slots.FindSlotOf(peer);
                if (current != null && current.Side == side)
                {
                    continue;
                }
                CommanderRole? preferred = CommanderRoles.GetRole(CommanderRoles.GetClass(CommanderBattleTeams.GetCulture(side), missionPeer.SelectedTroopIndex));
                FormationSlot free = null;
                foreach (FormationSlot slot in Slots.GetSlots(side))
                {
                    // Nobody becomes the general without choosing to: he leads no formation of the line.
                    if (slot.IsGeneral)
                    {
                        continue;
                    }
                    if (slot.Commander == null && (free == null || (slot.Role == preferred && free.Role != preferred)))
                    {
                        free = slot;
                    }
                }
                if (free == null)
                {
                    string sideName = side == BattleSideEnum.Attacker ? "attacker" : "defender";
                    if (Slots.FindFreeSlot(side, CommanderRole.General) != null)
                    {
                        // Left on the side without a formation, so the formation list (N) can still take the general.
                        SendServerMessage(peer, $"All {CommanderBattleSettings.FormationCount} {sideName} formations already have a commander. The general is free: take him from the formation list (N).");
                        continue;
                    }
                    bool fieldsGeneral = CommanderBattleSettings.IsSlotFielded(CommanderBattleTeams.GetCulture(side), CommanderBattleSettings.GeneralSlot);
                    MultiplayerTeamSelectComponent.ChangeTeamServer(peer, Mission.SpectatorTeam);
                    SendServerMessage(peer, $"All {CommanderBattleSettings.FormationCount} {sideName} formations{(fieldsGeneral ? " and the general" : "")} already have a commander.");
                    continue;
                }
                Claim(peer, missionPeer, free);
            }
        }

        private void ReleaseDisconnectedCommanders()
        {
            foreach (FormationSlot slot in Slots.AllSlots)
            {
                if (slot.Commander != null && (!slot.Commander.IsConnectionActive || slot.Commander.GetComponent<MissionPeer>()?.Team?.Side != slot.Side))
                {
                    _sentClassRestrictions.Remove(slot.Commander);
                    SetSlot(slot, null, slot.ClassIndex, slot.WeaponIndex);
                }
            }
        }

        // Keep each formation's class and weapon setup in step with its commander's troop index and perk, which only
        // change through a troop selection (ApplySelection sets both) or a stock path (the client sends its saved perk
        // choices when it joins a side); a class of another role is forced back.
        private void SyncCommanderClasses()
        {
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                BasicCultureObject culture = CommanderBattleTeams.GetCulture(side);
                foreach (FormationSlot slot in Slots.GetSlots(side))
                {
                    MissionPeer missionPeer = slot.Commander?.GetComponent<MissionPeer>();
                    if (missionPeer == null)
                    {
                        continue;
                    }
                    int classIndex = missionPeer.SelectedTroopIndex;
                    if (classIndex != slot.ClassIndex && CommanderRoles.GetRole(CommanderRoles.GetClass(culture, classIndex)) != slot.Role)
                    {
                        ForceTroopIndex(slot.Commander, missionPeer, slot.ClassIndex);
                        continue;
                    }
                    int weaponIndex = GetCommanderWeaponIndex(missionPeer, CommanderRoles.GetClass(culture, classIndex), classIndex);
                    if (classIndex != slot.ClassIndex || weaponIndex != slot.WeaponIndex)
                    {
                        SetSlot(slot, slot.Commander, classIndex, weaponIndex);
                    }
                }
            }
        }

        // The commander's weapon perk for the class, as RefreshSelectedPerks reads it (out of range: the first).
        private static int GetCommanderWeaponIndex(MissionPeer missionPeer, MultiplayerClassDivisions.MPHeroClass heroClass, int classIndex)
        {
            if (classIndex < 0 || classIndex >= MissionPeer.MaxNumberOfTroopTypesPerCulture)
            {
                return 0;
            }
            int index = missionPeer.GetSelectedPerkIndexWithPerkListIndex(classIndex, CommanderRoles.WeaponPerkList);
            return index >= 0 && index < CommanderRoles.GetWeaponSetups(heroClass).Count ? index : 0;
        }

        // Each player's class picker only offers classes of their formation's role. The general's class is in the
        // stock Cavalry group (CommanderRoles.GetRole), and SpawnComponent.AllowEarlyAgentVisualsDespawning refuses
        // a commander whose group is restricted, so a general keeps the Cavalry group open.
        private void UpdateClassRestrictions()
        {
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers)
            {
                if (!peer.IsSynchronized || peer.IsServerPeer)
                {
                    continue;
                }
                FormationSlot slot = Slots.FindSlotOf(peer);
                BasicCultureObject culture = slot != null ? CommanderBattleTeams.GetCulture(slot.Side) : null;
                if (!_sentClassRestrictions.TryGetValue(peer, out bool[] sent))
                {
                    sent = new bool[CommanderRoles.ClassGroups.Length];
                    _sentClassRestrictions[peer] = sent;
                }
                for (int i = 0; i < CommanderRoles.ClassGroups.Length; i++)
                {
                    FormationClass classGroup = CommanderRoles.ClassGroups[i];
                    CommanderRole? groupRole = slot == null ? (CommanderRole?)null : slot.IsGeneral ? CommanderRole.Cavalry : slot.Role;
                    bool restricted = groupRole != null && CommanderRoles.GetRole(classGroup, culture) != groupRole;
                    if (sent[i] == restricted)
                    {
                        continue;
                    }
                    sent[i] = restricted;
                    CommanderBattleNetwork.SendTo(peer, new ChangeClassRestrictions(classGroup, restricted));
                }
            }
        }

        protected override void HandleNewClientAfterSynchronized(NetworkCommunicator networkPeer)
        {
            if (networkPeer.IsServerPeer)
            {
                return;
            }
            CommanderBattleNetwork.SendTo(networkPeer, new SpawnLayoutSeed(CommanderBattleSpawnLayout.GetRoundSeed(Mission), Slots.GetTroopCounts()));
            foreach (FormationSlot slot in Slots.AllSlots)
            {
                CommanderBattleNetwork.SendTo(networkPeer, new FormationSlotUpdate(slot.Side, slot.Index, slot.Commander, slot.ClassIndex, slot.WeaponIndex));
                if (slot.HasPendingSelection)
                {
                    CommanderBattleNetwork.SendTo(networkPeer, new FormationSlotPendingSelection(slot.Side, slot.Index, slot.PendingClassIndex, slot.PendingPerks));
                }
            }
            SendGeneralOrdersTo(networkPeer, networkPeer.GetComponent<MissionPeer>()?.Team?.Side ?? BattleSideEnum.None);
            SendBannersTo(networkPeer);
        }

        private static void SendServerMessage(NetworkCommunicator peer, string message)
        {
            if (peer.IsServerPeer)
            {
                InformationManager.DisplayMessage(new InformationMessage(message));
                return;
            }
            CommanderBattleNetwork.SendTo(peer, new ServerMessage(message));
        }

        // --- Troop counts ---

        // Set once the round's armies spawn: a class or perk change after that applies next round.
        private bool _troopCountsFrozen;

        // During preparation a commander's new class or perks, or a formation changing hands, change its size; the
        // layout follows, and the clients get the new counts.
        private void UpdateTroopCounts()
        {
            if (_troopCountsFrozen || RoundController.CurrentRoundState != MultiplayerRoundState.Preparation)
            {
                return;
            }
            if (Slots.SetTroopCounts(ComputeTroopCounts(log: false)))
            {
                ApplySpawnLayout(CommanderBattleSpawnLayout.GetRoundSeed(Mission));
            }
        }

        // The counts the round's armies spawn with, from the classes they spawn as.
        private void FreezeTroopCounts()
        {
            // Catch a class picked since the last game mode tick.
            SyncCommanderClasses();
            if (Slots.SetTroopCounts(ComputeTroopCounts(log: true)))
            {
                ApplySpawnLayout(CommanderBattleSpawnLayout.GetRoundSeed(Mission));
            }
            _troopCountsFrozen = true;
        }

        // A formation's budget buys men by its class (its weight): men = budget x the class's stock Captain troop count
        // for 100 bots (MPPerkObject.GetTroopCount; the commander's perks are included, though the weapon setups add no
        // troops) / the average of the culture's classes of the formation's role (no perks). Heavy classes field fewer
        // men, light ones more, and a side's total varies with its picks. The general's single class is its own
        // reference, so he always has his bodyguard's budget; a general the culture doesn't field has none.
        private int[] ComputeTroopCounts(bool log)
        {
            int slotCount = CommanderBattleSettings.SlotCount;
            int[] counts = new int[2 * slotCount];
            if (log)
            {
                CommanderBattleLog.Detail($"Troop counts for round {RoundController.RoundCount}:");
            }
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                BasicCultureObject culture = CommanderBattleTeams.GetCulture(side);
                float[] roleReference = new float[CommanderRoles.All.Length];
                foreach (CommanderRole role in CommanderRoles.All)
                {
                    roleReference[(int)role] = CommanderRoles.GetAverageStockTroopCount(culture, role, CommanderBattleSettings.BotsPerFormationBaseline);
                }
                int total = 0;
                foreach (FormationSlot slot in Slots.GetSlots(side))
                {
                    if (!slot.IsFielded)
                    {
                        if (log)
                        {
                            CommanderBattleLog.Detail($"  {side} {slot.LogName}: not fielded");
                        }
                        continue;
                    }
                    MultiplayerClassDivisions.MPHeroClass heroClass = CommanderRoles.GetClass(culture, slot.ClassIndex);
                    MissionPeer commander = slot.Commander?.GetComponent<MissionPeer>();
                    int stockCount = CommanderRoles.GetStockTroopCount(heroClass, commander != null ? MPPerkObject.GetOnSpawnPerkHandler(commander) : null);
                    float reference = roleReference[(int)slot.Role];
                    int troops = CommanderRoles.GetTroopCount(slot.Budget, stockCount, reference);
                    counts[(side == BattleSideEnum.Attacker ? slotCount : 0) + slot.Index] = troops;
                    total += troops;
                    if (log)
                    {
                        string weight = heroClass != null ? CommanderRoles.GetWeightLabel(heroClass) : "";
                        CommanderBattleLog.Detail($"  {side} {slot.LogName} ({(slot.Commander != null ? slot.Commander.UserName : "AI")}):{heroClass?.StringId ?? "no class"}{(weight.Length > 0 ? " " + weight.ToLowerInvariant() : "")}, weapons {slot.WeaponIndex} {CommanderRoles.GetWeaponName(heroClass, slot.WeaponIndex)}, stock {stockCount} / role average {reference:0.0}, budget {slot.Budget} -> {troops} troops");
                    }
                }
                if (log)
                {
                    CommanderBattleLog.Detail($"  {side} ({culture?.StringId}) total: {total} troops");
                }
            }
            return counts;
        }

        // Called by the stock spawner as bots = (int)(stockCount * multiplier). Return the multiplier that
        // turns the stock count into the formation's troop count for this round, whatever the class and perks.
        public override float GetTroopNumberMultiplierForMissingPlayer(MissionPeer spawningPeer)
        {
            MultiplayerClassDivisions.MPHeroClass heroClass = MultiplayerClassDivisions.GetMPHeroClassForPeer(spawningPeer);
            if (heroClass == null)
            {
                return 1f;
            }
            int stockCount = MPPerkObject.GetTroopCount(heroClass, MultiplayerOptions.OptionType.NumberOfBotsPerFormation.GetIntValue(), MPPerkObject.GetOnSpawnPerkHandler(spawningPeer));
            int share = Slots.FindSlotOf(spawningPeer.GetNetworkPeer())?.TroopCount ?? 0;
            if (HasBannerBearer(spawningPeer))
            {
                // The stock spawner adds the banner bearer on top of the troops.
                share--;
            }
            share = MathF.Max(share, 0);
            // +0.5 so the spawner's truncation lands exactly on the share.
            return (share + 0.5f) / stockCount;
        }

        private static bool HasBannerBearer(MissionPeer peer)
        {
            foreach (MPPerkObject perk in peer.SelectedPerks)
            {
                if (perk.HasBannerBearer)
                {
                    return true;
                }
            }
            return false;
        }

        // --- Rounds ---

        public override bool CheckForRoundEnd()
        {
            if (!CanGameModeSystemsTickThisFrame || SpawnComponent.AreAgentsSpawning())
            {
                return false;
            }
            return _forceRoundEnd
                || CommanderBattleTeams.CountActiveSoldiers(Mission, BattleSideEnum.Attacker) == 0
                || CommanderBattleTeams.CountActiveSoldiers(Mission, BattleSideEnum.Defender) == 0;
        }

        private void OnPreRoundEnding()
        {
            int attackers = CommanderBattleTeams.CountActiveSoldiers(Mission, BattleSideEnum.Attacker);
            int defenders = CommanderBattleTeams.CountActiveSoldiers(Mission, BattleSideEnum.Defender);
            BattleSideEnum winner;
            RoundEndReason reason;
            if (_forceRoundEnd)
            {
                // An admin's restart: nobody scores, and the round number is played again (BeginNewRound adds one
                // back), so a restart never uses up one of the match's rounds.
                _forceRoundEnd = false;
                RoundController.RoundWinner = BattleSideEnum.None;
                RoundController.RoundEndReason = RoundEndReason.GameModeSpecificEnded;
                CommanderBattleLog.Write($"Round {RoundController.RoundCount} restarted by an admin, {attackers} attackers and {defenders} defenders left");
                RoundController.RoundCount--;
                return;
            }
            if (attackers > 0 && defenders > 0)
            {
                // Time ran out with both armies still in the field: the defenders held, and take the round.
                winner = BattleSideEnum.Defender;
                reason = RoundEndReason.RoundTimeEnded;
            }
            else
            {
                winner = attackers > 0 ? BattleSideEnum.Attacker : (defenders > 0 ? BattleSideEnum.Defender : BattleSideEnum.None);
                reason = RoundEndReason.SideDepleted;
            }
            RoundController.RoundWinner = winner;
            RoundController.RoundEndReason = reason;
            string result = winner == BattleSideEnum.None ? "nobody wins" : (winner == BattleSideEnum.Attacker ? "attackers win" : "defenders win");
            CommanderBattleLog.Write($"Round {RoundController.RoundCount} over: {result}{(reason == RoundEndReason.RoundTimeEnded ? " (time ran out)" : "")}, {attackers} attackers and {defenders} defenders left");
            if (winner != BattleSideEnum.None)
            {
                Mission.GetMissionBehavior<AgentVictoryLogic>()?.SetTimersOfVictoryReactionsOnBattleEnd(winner);
            }
        }

        // The stock round controller ends the match when a side has no human player. With the AI leading empty
        // formations that is a normal battle, so keep going while anyone is connected, until the round total
        // or an early win.
        private void OnPostRoundEnded()
        {
            if (!RoundController.IsMatchEnding || IsMatchEndingSetter == null)
            {
                return;
            }
            int roundTotal = MultiplayerOptions.OptionType.RoundTotal.GetIntValue();
            MissionScoreboardComponent scoreboard = Mission.GetMissionBehavior<MissionScoreboardComponent>();
            bool decided = RoundController.RoundCount >= roundTotal
                || (scoreboard != null && (scoreboard.GetRoundScore(BattleSideEnum.Attacker) > roundTotal / 2 || scoreboard.GetRoundScore(BattleSideEnum.Defender) > roundTotal / 2));
            if (!decided && AnyPlayerConnected())
            {
                IsMatchEndingSetter.Invoke(RoundController, new object[] { false });
            }
        }

        private static bool AnyPlayerConnected()
        {
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers)
            {
                if (peer.IsSynchronized && !peer.IsServerPeer)
                {
                    return true;
                }
            }
            return false;
        }

        // A dead commander who asked to respawn takes over one of their own soldiers.
        private void CheckForCommandersTakingOverSoldiers()
        {
            foreach (NetworkCommunicator networkPeer in GameNetwork.NetworkPeers)
            {
                if (!networkPeer.IsSynchronized)
                {
                    continue;
                }
#if !SERVER
                if (networkPeer.IsStreamer)
                {
                    continue;
                }
#endif
                MissionPeer missionPeer = networkPeer.GetComponent<MissionPeer>();
                if (missionPeer == null || missionPeer.ControlledAgent != null || missionPeer.Team == null || missionPeer.Team == Mission.SpectatorTeam || missionPeer.ControlledFormation == null || missionPeer.SpawnCountThisRound <= 0)
                {
                    continue;
                }
                if (!missionPeer.HasSpawnTimerExpired && missionPeer.SpawnTimer.Check(Mission.CurrentTime))
                {
                    missionPeer.HasSpawnTimerExpired = true;
                }
                if (!missionPeer.HasSpawnTimerExpired || !missionPeer.WantsToSpawnAsBot)
                {
                    continue;
                }
                Agent soldier = PickSoldierToTakeOver(missionPeer);
                if (soldier == null)
                {
                    continue;
                }
                Mission.ReplaceBotWithPlayer(soldier, missionPeer);
                missionPeer.WantsToSpawnAsBot = false;
                missionPeer.HasSpawnTimerExpired = false;
                Formation formation = missionPeer.ControlledFormation;
                if (formation.FormationIndex == FormationClass.General)
                {
                    // The soldier who takes his place is the new general; the bodyguard keeps behind him from the
                    // next tick (KeepBodyguardsBehindGenerals).
                    CommanderBattleTeams.SetGeneral(missionPeer.Team, formation, soldier);
                }
            }
        }

        // The soldier the spectator camera is following, otherwise the healthiest one.
        private static Agent PickSoldierToTakeOver(MissionPeer commander)
        {
            Formation formation = commander.ControlledFormation;
            Agent followed = commander.FollowedAgent;
            if (followed != null && followed.IsActive() && followed.IsAIControlled && followed.Formation == formation)
            {
                return followed;
            }
            Agent best = null;
            formation.ApplyActionOnEachUnit(delegate (Agent agent)
            {
                if (agent.IsActive() && agent.IsAIControlled && (best == null || agent.Health > best.Health))
                {
                    best = agent;
                }
            });
            return best;
        }

        // Soldiers of a commander who leaves the battle stop following an empty spot; the AI leads them.
        protected override void HandleEarlyPlayerDisconnect(NetworkCommunicator networkPeer)
        {
            if (RoundController.IsRoundInProgress)
            {
                HandFormationToAI(networkPeer);
            }
            _sentClassRestrictions.Remove(networkPeer);
            ReleaseSlotOf(networkPeer);
            ForgetBanner(networkPeer);
        }

        private void OnPreTeamChanged(NetworkCommunicator peer, Team currentTeam, Team newTeam)
        {
            if (peer.IsSynchronized && peer.GetComponent<MissionPeer>()?.ControlledAgent != null)
            {
                HandFormationToAI(peer);
            }
        }

        // Without an owner the formation counts as AI-controlled and the team's tactic picks it up. No tactic leads the
        // general's formation, so it gets the general's behaviour itself.
        private static void HandFormationToAI(NetworkCommunicator peer)
        {
            Formation formation = peer.GetComponent<MissionPeer>()?.ControlledFormation;
            if (formation != null)
            {
                formation.PlayerOwner = null;
                if (formation.FormationIndex == FormationClass.General)
                {
                    CommanderBattleTeamAI.LeadAsGeneral(formation);
                }
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            agent.UpdateSyncHealthToAllClients(value: true);
            if (agent.IsPlayerControlled)
            {
                agent.MissionPeer.GetComponent<FlagDominationMissionRepresentative>()?.UpdateSelectedClassServer(agent);
            }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (affectedAgent.IsPlayerControlled)
            {
                affectedAgent.MissionPeer.GetComponent<FlagDominationMissionRepresentative>()?.UpdateSelectedClassServer(null);
                // A general who takes over one of his riders starts again with everything delegated.
                ReleaseGeneralCommandOnDeath(affectedAgent);
            }
        }

        protected override void HandleEarlyNewClientAfterLoadingFinished(NetworkCommunicator networkPeer)
        {
            networkPeer.AddComponent<FlagDominationMissionRepresentative>();
        }
    }
}
