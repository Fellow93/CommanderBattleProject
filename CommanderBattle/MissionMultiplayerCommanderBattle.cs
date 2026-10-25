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
    // Server-side rules: Attackers vs Defenders, each a fixed army of formations (CommanderBattleSettings).
    // Players take command of a formation; the AI leads the rest. A round ends when one side has no soldiers
    // left (or on time, won by the side with more soldiers). No flags, morale or gold.
    public class MissionMultiplayerCommanderBattle : MissionMultiplayerGameModeBase
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

        public override bool AllowCustomPlayerBanners()
        {
            return false;
        }

        public override void OnBehaviorInitialize()
        {
            CommanderBattleLog.Write("GameMode.OnBehaviorInitialize");
            base.OnBehaviorInitialize();
            ApplyRequiredOptions();
            // Shared with the client half of the mode, which the UI reads (matters on a listen server).
            Slots = Mission.GetMissionBehavior<MissionMultiplayerCommanderBattleClient>().Slots;
            AssignDefaultClasses();
        }

        public override void AfterStart()
        {
            CommanderBattleLog.Write("GameMode.AfterStart");
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
        }

        private void OnRoundStateChanged()
        {
            CommanderBattleLog.Write($"Round {RoundController.RoundCount}: {RoundController.CurrentRoundState} (lobby {MissionLobbyComponent.CurrentMultiplayerState}, spawning {SpawnComponent.AreAgentsSpawning()})");
            if (RoundController.CurrentRoundState == MultiplayerRoundState.Preparation)
            {
                NewSpawnLayout();
            }
        }

        // Each round the armies start somewhere else in their spawn zones; the clients get the same seed.
        private void NewSpawnLayout()
        {
            int seed = 1 + MBRandom.RandomInt(SpawnLayoutSeedMax);
            CommanderBattleSpawnLayout.SetRoundSeed(Mission, seed);
            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(new SpawnLayoutSeed(seed));
            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
            CommanderBattleLog.Write($"Spawn layout seed {seed}");
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

        // AI-led formations cycle through the culture's classes of their role (e.g. light, heavy, shock infantry).
        private void AssignDefaultClasses()
        {
            foreach (BattleSideEnum side in new[] { BattleSideEnum.Defender, BattleSideEnum.Attacker })
            {
                BasicCultureObject culture = CommanderBattleTeams.GetCulture(side);
                Dictionary<CommanderRole, int> usedPerRole = new Dictionary<CommanderRole, int>();
                foreach (FormationSlot slot in Slots.GetSlots(side))
                {
                    List<int> classIndexes = CommanderRoles.GetClassIndexes(culture, slot.Role);
                    usedPerRole.TryGetValue(slot.Role, out int used);
                    usedPerRole[slot.Role] = used + 1;
                    slot.ClassIndex = classIndexes.Count > 0 ? classIndexes[used % classIndexes.Count] : 0;
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
            CommanderBattleLog.Write(rbmTactics ? "Team AI: RBM's tactics (RBM.Multiplayer)" : "Team AI: CommanderBattleTactic");
            foreach (BattleSideEnum side in new[] { BattleSideEnum.Defender, BattleSideEnum.Attacker })
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
                CommanderBattleLog.Write($"Status: round {RoundController.RoundCount} {RoundController.CurrentRoundState}, timer {TimerComponent.GetRemainingTime(isSynched: false):0.0}s, mission time {Mission.CurrentTime:0.0}, lobby {MissionLobbyComponent.CurrentMultiplayerState}, matchEnding {RoundController.IsMatchEnding}");
            }
            PlaceQueuedPeers();
            if (CanGameModeSystemsTickThisFrame)
            {
                ReleaseDisconnectedCommanders();
                SyncCommanderClasses();
                UpdateClassRestrictions();
            }
            if (MissionLobbyComponent.CurrentMultiplayerState == MissionLobbyComponent.MultiplayerGameState.Playing)
            {
                CheckForCommandersTakingOverSoldiers();
            }
        }

        // --- Formation slots ---

        private void SetSlot(FormationSlot slot, NetworkCommunicator commander, int classIndex)
        {
            Slots.Set(slot.Side, slot.Index, commander, classIndex);
            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(new FormationSlotUpdate(slot.Side, slot.Index, commander, classIndex));
            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
        }

        private void ReleaseSlotOf(NetworkCommunicator peer)
        {
            FormationSlot slot = Slots.FindSlotOf(peer);
            if (slot != null)
            {
                // The AI takes over with the class the player last chose.
                SetSlot(slot, null, slot.ClassIndex);
            }
        }

        private bool HandleClientEventRequestFormationCommand(NetworkCommunicator peer, GameNetworkMessage baseMessage)
        {
            RequestFormationCommand message = (RequestFormationCommand)baseMessage;
            RequestFormationCommandFrom(peer, message.SlotIndex);
            return true;
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
            if (slot.Commander != null)
            {
                SendServerMessage(peer, $"{slot.Commander.UserName} already commands that formation.");
                return;
            }
            if (RoundController.IsRoundInProgress && missionPeer.SpawnCountThisRound > 0)
            {
                SendServerMessage(peer, "You can change formation before the next round.");
                return;
            }
            Claim(peer, missionPeer, slot);
        }

        private void Claim(NetworkCommunicator peer, MissionPeer missionPeer, FormationSlot slot)
        {
            ReleaseSlotOf(peer);
            BasicCultureObject culture = CommanderBattleTeams.GetCulture(slot.Side);
            int classIndex = missionPeer.SelectedTroopIndex;
            if (CommanderRoles.GetRole(CommanderRoles.GetClass(culture, classIndex)) != slot.Role)
            {
                classIndex = slot.ClassIndex;
                ForceTroopIndex(peer, missionPeer, classIndex);
            }
            SetSlot(slot, peer, classIndex);
            ResetPreparationVisuals(peer, missionPeer);
            CommanderBattleLog.Write($"{peer.UserName} commands {slot.Side} formation {slot.Index} ({slot.Role})");
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
            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(new RemoveAgentVisualsForPeer(peer));
            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
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
            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(new UpdateSelectedTroopIndex(peer, classIndex));
            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
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
        }

        // Someone who joins a side takes the first free formation, preferring one matching their current class.
        // Deferred to the tick so we never re-enter ChangeTeamServer from its own event.
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
                    if (slot.Commander == null && (free == null || (slot.Role == preferred && free.Role != preferred)))
                    {
                        free = slot;
                    }
                }
                if (free == null)
                {
                    MultiplayerTeamSelectComponent.ChangeTeamServer(peer, Mission.SpectatorTeam);
                    SendServerMessage(peer, $"All {CommanderBattleSettings.FormationCount} {(side == BattleSideEnum.Attacker ? "attacker" : "defender")} formations already have a commander.");
                    continue;
                }
                Claim(peer, missionPeer, free);
            }
        }

        private void ReleaseDisconnectedCommanders()
        {
            foreach (BattleSideEnum side in new[] { BattleSideEnum.Defender, BattleSideEnum.Attacker })
            {
                foreach (FormationSlot slot in Slots.GetSlots(side))
                {
                    if (slot.Commander != null && (!slot.Commander.IsConnectionActive || slot.Commander.GetComponent<MissionPeer>()?.Team?.Side != side))
                    {
                        _sentClassRestrictions.Remove(slot.Commander);
                        SetSlot(slot, null, slot.ClassIndex);
                    }
                }
            }
        }

        // Keep each formation's class in step with its commander's pick in the class loadout, which is limited
        // to the formation's role (and forced back if a pick slips through).
        private void SyncCommanderClasses()
        {
            foreach (BattleSideEnum side in new[] { BattleSideEnum.Defender, BattleSideEnum.Attacker })
            {
                BasicCultureObject culture = CommanderBattleTeams.GetCulture(side);
                foreach (FormationSlot slot in Slots.GetSlots(side))
                {
                    MissionPeer missionPeer = slot.Commander?.GetComponent<MissionPeer>();
                    if (missionPeer == null || missionPeer.SelectedTroopIndex == slot.ClassIndex)
                    {
                        continue;
                    }
                    if (CommanderRoles.GetRole(CommanderRoles.GetClass(culture, missionPeer.SelectedTroopIndex)) == slot.Role)
                    {
                        SetSlot(slot, slot.Commander, missionPeer.SelectedTroopIndex);
                    }
                    else
                    {
                        ForceTroopIndex(slot.Commander, missionPeer, slot.ClassIndex);
                    }
                }
            }
        }

        // Each player's class picker only offers classes of their formation's role.
        private void UpdateClassRestrictions()
        {
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers)
            {
                if (!peer.IsSynchronized || peer.IsServerPeer)
                {
                    continue;
                }
                FormationSlot slot = Slots.FindSlotOf(peer);
                if (!_sentClassRestrictions.TryGetValue(peer, out bool[] sent))
                {
                    sent = new bool[CommanderRoles.ClassGroups.Length];
                    _sentClassRestrictions[peer] = sent;
                }
                for (int i = 0; i < CommanderRoles.ClassGroups.Length; i++)
                {
                    FormationClass classGroup = CommanderRoles.ClassGroups[i];
                    bool restricted = slot != null && CommanderRoles.GetRole(classGroup) != slot.Role;
                    if (sent[i] == restricted)
                    {
                        continue;
                    }
                    sent[i] = restricted;
                    GameNetwork.BeginModuleEventAsServer(peer);
                    GameNetwork.WriteMessage(new ChangeClassRestrictions(classGroup, restricted));
                    GameNetwork.EndModuleEventAsServer();
                }
            }
        }

        protected override void HandleNewClientAfterSynchronized(NetworkCommunicator networkPeer)
        {
            if (networkPeer.IsServerPeer)
            {
                return;
            }
            GameNetwork.BeginModuleEventAsServer(networkPeer);
            GameNetwork.WriteMessage(new SpawnLayoutSeed(CommanderBattleSpawnLayout.RoundSeed));
            GameNetwork.EndModuleEventAsServer();
            foreach (BattleSideEnum side in new[] { BattleSideEnum.Defender, BattleSideEnum.Attacker })
            {
                foreach (FormationSlot slot in Slots.GetSlots(side))
                {
                    GameNetwork.BeginModuleEventAsServer(networkPeer);
                    GameNetwork.WriteMessage(new FormationSlotUpdate(slot.Side, slot.Index, slot.Commander, slot.ClassIndex));
                    GameNetwork.EndModuleEventAsServer();
                }
            }
        }

        private static void SendServerMessage(NetworkCommunicator peer, string message)
        {
            if (peer.IsServerPeer)
            {
                InformationManager.DisplayMessage(new InformationMessage(message));
                return;
            }
            GameNetwork.BeginModuleEventAsServer(peer);
            GameNetwork.WriteMessage(new ServerMessage(message));
            GameNetwork.EndModuleEventAsServer();
        }

        // --- Troop counts ---

        // Called by the stock spawner as bots = (int)(stockCount * multiplier). Return the multiplier that
        // turns the stock count into the formation's troop count.
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
            return CommanderBattleTeams.CountActiveSoldiers(Mission, BattleSideEnum.Attacker) == 0
                || CommanderBattleTeams.CountActiveSoldiers(Mission, BattleSideEnum.Defender) == 0;
        }

        private void OnPreRoundEnding()
        {
            int attackers = CommanderBattleTeams.CountActiveSoldiers(Mission, BattleSideEnum.Attacker);
            int defenders = CommanderBattleTeams.CountActiveSoldiers(Mission, BattleSideEnum.Defender);
            BattleSideEnum winner;
            RoundEndReason reason;
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
        }

        private void OnPreTeamChanged(NetworkCommunicator peer, Team currentTeam, Team newTeam)
        {
            if (peer.IsSynchronized && peer.GetComponent<MissionPeer>()?.ControlledAgent != null)
            {
                HandFormationToAI(peer);
            }
        }

        // Without an owner the formation counts as AI-controlled and the team's tactic picks it up.
        private static void HandFormationToAI(NetworkCommunicator peer)
        {
            Formation formation = peer.GetComponent<MissionPeer>()?.ControlledFormation;
            if (formation != null)
            {
                formation.PlayerOwner = null;
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
            }
        }

        protected override void HandleEarlyNewClientAfterLoadingFinished(NetworkCommunicator networkPeer)
        {
            networkPeer.AddComponent<FlagDominationMissionRepresentative>();
        }
    }
}
