using System;
using System.Collections.Generic;
using System.Reflection;
using CommanderBattle.Network;
using NetworkMessages.FromServer;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;
using TaleWorlds.MountAndBlade.Objects;

namespace CommanderBattle
{
    // Client half of the game mode (also present on the server, like every stock mode).
    // Implements ICommanderInfo without flags so the stock HUD's two-sided bar shows each side's
    // remaining soldiers instead of morale.
    public class MissionMultiplayerCommanderBattleClient : MissionMultiplayerGameModeBaseClient, ICommanderInfo
    {
        private const float StrengthUpdatePeriod = 0.5f;

        private static readonly MethodInfo BotsUnderControlTotalSetter = typeof(MissionPeer).GetProperty(nameof(MissionPeer.BotsUnderControlTotal))?.GetSetMethod(nonPublic: true);

        private readonly int[] _initialSoldiers = new int[2];

        private float _strengthUpdateTimer;

        public override bool IsGameModeUsingGold => false;

        public override bool IsGameModeTactical => true;

        public override bool IsGameModeUsingRoundCountdown => true;

        public override bool IsGameModeUsingCasualGold => false;

        public override MultiplayerGameType GameType => MultiplayerGameType.Captain;

        // Who commands which formation; kept up to date by FormationSlotUpdate from the server.
        public FormationSlotTable Slots { get; } = new FormationSlotTable();

        // The formation our own peer commands, or null.
        public FormationSlot MySlot => GameNetwork.IsMyPeerReady ? Slots.FindSlotOf(GameNetwork.MyPeer) : null;

        // What our side's team AI is doing (tactic, formation behaviours and destinations), from the server; null
        // until the first SideAIStatus of the round.
        public SideAIStatus AIStatus { get; private set; }

        public event Action OnAIStatusChanged;

        // The orders our side's general drew, filed by side (by (int)BattleSideEnum), oldest first. Only our side's
        // reach us; the other side's list stays empty.
        private readonly List<GeneralOrder>[] _generalOrders = { new List<GeneralOrder>(), new List<GeneralOrder>() };

        public event Action OnGeneralOrdersChanged;

        public IEnumerable<FlagCapturePoint> AllCapturePoints { get; } = new List<FlagCapturePoint>();

        public bool AreMoralesIndependent => true;

        public event Action<BattleSideEnum, float> OnMoraleChangedEvent;

        // No flags in this mode, so these never fire.
        public event Action OnFlagNumberChangedEvent
        {
            add { }
            remove { }
        }

        public event Action<FlagCapturePoint, Team> OnCapturePointOwnerChangedEvent
        {
            add { }
            remove { }
        }

        public override void OnBehaviorInitialize()
        {
            CommanderBattleLog.Detail("Client.OnBehaviorInitialize");
            base.OnBehaviorInitialize();
            // Runs on the server too: both logs show the classes each side loaded.
            CommanderRoles.ClearClassCache();
            CommanderRoles.LogClasses();
            CreateVisualMarkersIfMissing();
            if (!GameNetwork.IsServer)
            {
                ApplyClientOptions();
            }
            MissionPeer.OnTeamChanged += OnTeamChangedForGeneralOrders;
        }

        // The stock MP order UI only ticks when NumberOfBotsPerFormation > 0, read once in its AfterStart. Without it
        // the order bar and F keys are dead and the troop placer is never suspended, so every attack click becomes a
        // drag move order. The server sets the option in code (no config line) and sends options only when a client
        // connects, and after a map change the order UI stayed off. Set it here, before any AfterStart.
        private static void ApplyClientOptions()
        {
            int botsPerFormation = MultiplayerOptions.OptionType.NumberOfBotsPerFormation.GetIntValue();
            if (botsPerFormation != CommanderBattleSettings.BotsPerFormationBaseline)
            {
                CommanderBattleLog.Write($"Order bar turned on for this map (bots per formation was {botsPerFormation}, now {CommanderBattleSettings.BotsPerFormationBaseline})");
                MultiplayerOptions.OptionType.NumberOfBotsPerFormation.SetValue(CommanderBattleSettings.BotsPerFormationBaseline);
            }
        }

        public override void EarlyStart()
        {
            CommanderBattleLog.Detail("Client.EarlyStart");
            base.EarlyStart();
        }

        public override void AfterStart()
        {
            CommanderBattleLog.Detail("Client.AfterStart");
            Mission.Current.SetMissionMode(MissionMode.Battle, atStart: true);
            // Spawn paths are known from here on.
            _markersCanBePlaced = _visualMarkers != null && CommanderBattleSpawnLayout.UsesSpawnPaths(Mission);
            UpdateVisualMarkers();
            CommanderBattleLog.Detail("Client.AfterStart done");
        }

        public override void OnRemoveBehavior()
        {
            if (_visualMarkers != null)
            {
                Slots.OnSlotChanged -= OnSlotChangedForMarkers;
                MissionPeer.OnTeamChanged -= OnTeamChangedForMarkers;
            }
            MissionPeer.OnTeamChanged -= OnTeamChangedForGeneralOrders;
            base.OnRemoveBehavior();
        }

        // --- The general's orders ---

        public IReadOnlyList<GeneralOrder> GetGeneralOrders(BattleSideEnum side)
        {
            return side == BattleSideEnum.Attacker || side == BattleSideEnum.Defender ? _generalOrders[(int)side] : (IReadOnlyList<GeneralOrder>)Array.Empty<GeneralOrder>();
        }

        internal void AddGeneralOrder(GeneralOrder order)
        {
            if (order == null || (order.Side != BattleSideEnum.Attacker && order.Side != BattleSideEnum.Defender))
            {
                return;
            }
            List<GeneralOrder> orders = _generalOrders[(int)order.Side];
            int index = orders.FindIndex(existing => existing.Id == order.Id);
            if (index >= 0)
            {
                orders[index] = order;
            }
            else
            {
                orders.Add(order);
            }
            OnGeneralOrdersChanged?.Invoke();
        }

        internal void RemoveGeneralOrder(int id)
        {
            bool removed = false;
            foreach (List<GeneralOrder> orders in _generalOrders)
            {
                removed |= orders.RemoveAll(order => order.Id == id) > 0;
            }
            if (removed)
            {
                OnGeneralOrdersChanged?.Invoke();
            }
        }

        // Removals of the side we left never reach us, so its orders go. The new side's are kept: the server may send
        // them before we learn of the change.
        private void OnTeamChangedForGeneralOrders(NetworkCommunicator peer, Team previousTeam, Team newTeam)
        {
            if (!peer.IsMine)
            {
                return;
            }
            BattleSideEnum newSide = newTeam?.Side ?? BattleSideEnum.None;
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                if (side != newSide)
                {
                    _generalOrders[(int)side].Clear();
                }
            }
            OnGeneralOrdersChanged?.Invoke();
        }

        private void ClearGeneralOrders()
        {
            foreach (List<GeneralOrder> orders in _generalOrders)
            {
                orders.Clear();
            }
            OnGeneralOrdersChanged?.Invoke();
        }

        // Ask the server to put a path order on the map (we must be our side's general).
        public void RequestGeneralOrder(Vec2[] points, string label)
        {
            Request(new RequestGeneralOrder(points, label), gameMode => gameMode.RequestGeneralOrderFrom(GameNetwork.MyPeer, points, label));
        }

        public void RequestDeleteGeneralOrder(int id)
        {
            Request(new RequestDeleteGeneralOrder(id), gameMode => gameMode.RequestDeleteGeneralOrderFrom(GameNetwork.MyPeer, id));
        }

        private void HandleServerEventGeneralOrderAdded(GameNetworkMessage baseMessage)
        {
            AddGeneralOrder(((GeneralOrderAdded)baseMessage).Order);
        }

        private void HandleServerEventGeneralOrderRemoved(GameNetworkMessage baseMessage)
        {
            RemoveGeneralOrder(((GeneralOrderRemoved)baseMessage).Id);
        }

        // --- Preparation visuals on campaign scenes ---

        private const int VisualMarkerCount = 6;

        private GameEntity[] _visualMarkers;

        private GameEntity[] _attackerVisualMarkers;

        private GameEntity[] _defenderVisualMarkers;

        private bool _markersCanBePlaced;

        // The stock visual spawner shows our commander and troops during preparation at scene entities tagged
        // sp_visual_*, which multiplayer maps have and campaign scenes don't. It looks them up once, on its first
        // tick, so create them during initialisation and move them to our formation's place once the scene's
        // spawn paths are known (it reads their position each time it places a visual).
        private void CreateVisualMarkersIfMissing()
        {
            if (GameNetwork.IsDedicatedServer || Mission.Scene.FindEntityWithTag("sp_visual_0") != null)
            {
                return;
            }
            _visualMarkers = CreateMarkers("sp_visual_");
            _attackerVisualMarkers = CreateMarkers("sp_visual_attacker_");
            _defenderVisualMarkers = CreateMarkers("sp_visual_defender_");
            Slots.OnSlotChanged += OnSlotChangedForMarkers;
            MissionPeer.OnTeamChanged += OnTeamChangedForMarkers;
            CommanderBattleLog.Detail("Created preparation visual markers (scene has none)");
        }

#if !SERVER
        // The stock spawner's tag lookup (its helper's constructor, on its first OnPreMissionTick) did not find the
        // markers created above: every CreateAgentVisuals threw in GetSpawnPointFrameForPlayer (NullReferenceException,
        // "Exception in handler of CreateAgentVisuals" in the rgl log) and no preparation visual showed. So the markers
        // go into its helper directly, once it exists. OnMissionTick runs after the OnPreMissionTick that makes it.
        private static readonly FieldInfo SpawnFrameHelperField = typeof(MultiplayerMissionAgentVisualSpawnComponent).GetField("_spawnFrameSelectionHelper", BindingFlags.Instance | BindingFlags.NonPublic);

        private object _helperGivenMarkers;

        private void GiveMarkersToVisualSpawner()
        {
            if (_visualMarkers == null)
            {
                return;
            }
            MultiplayerMissionAgentVisualSpawnComponent spawner = Mission.GetMissionBehavior<MultiplayerMissionAgentVisualSpawnComponent>();
            object helper = spawner != null ? SpawnFrameHelperField?.GetValue(spawner) : null;
            if (helper == null || helper == _helperGivenMarkers)
            {
                return;
            }
            _helperGivenMarkers = helper;
            int foundByTag = GiveMarkers(helper, "_visualSpawnPoints", _visualMarkers)
                + GiveMarkers(helper, "_visualAttackerSpawnPoints", _attackerVisualMarkers)
                + GiveMarkers(helper, "_visualDefenderSpawnPoints", _defenderVisualMarkers);
            CommanderBattleLog.Detail($"Preparation visual markers given to the stock visual spawner (it had found {foundByTag} of {3 * VisualMarkerCount} by tag)");
        }

        // Returns how many of the helper's points its own lookup had found.
        private static int GiveMarkers(object helper, string fieldName, GameEntity[] markers)
        {
            if (!(helper.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(helper) is GameEntity[] points))
            {
                CommanderBattleLog.Write($"Stock visual spawner has no {fieldName}; its preparation visuals may fail");
                return 0;
            }
            int found = 0;
            for (int i = 0; i < points.Length && i < markers.Length; i++)
            {
                if (points[i] != null)
                {
                    found++;
                }
                points[i] = markers[i];
            }
            return found;
        }
#endif

        private GameEntity[] CreateMarkers(string tagPrefix)
        {
            GameEntity[] markers = new GameEntity[VisualMarkerCount];
            for (int i = 0; i < VisualMarkerCount; i++)
            {
                markers[i] = GameEntity.CreateEmpty(Mission.Scene, isModifiableFromEditor: false);
                markers[i].AddTag(tagPrefix + i);
            }
            return markers;
        }

        private void OnSlotChangedForMarkers(FormationSlot slot)
        {
            UpdateVisualMarkers();
        }

        private void OnTeamChangedForMarkers(NetworkCommunicator peer, Team previousTeam, Team newTeam)
        {
            if (peer.IsMine)
            {
                UpdateVisualMarkers();
            }
        }

        private void UpdateVisualMarkers()
        {
            if (!_markersCanBePlaced)
            {
                return;
            }
            BattleSideEnum mySide = CommanderBattleTeams.MySide;
            BattleSideEnum shownSide = mySide == BattleSideEnum.None ? BattleSideEnum.Attacker : mySide;
            FormationSlot mySlot = MySlot;
            MatrixFrame formationFrame = mySlot != null && mySlot.Side == shownSide
                ? CommanderBattleSpawnLayout.GetFormationFrame(Mission, shownSide, mySlot.Index)
                : CommanderBattleSpawnLayout.GetSideFrame(Mission, shownSide);
            float halfWidth = mySlot != null && (mySlot.Role == CommanderRole.Cavalry || mySlot.Role == CommanderRole.HorseArcher || mySlot.Role == CommanderRole.General) ? 15f : 10f;
            // Commander in front of the troops (markers 0, side markers), troops centred on 3 between 1 and 5.
            // The stock spawner grounds the troops' frames but puts the commander (and his horse) at the marker's
            // frame as it is, so the marker itself must be on the ground, not at the formation centre's height.
            MatrixFrame heroFrame = formationFrame;
            heroFrame.origin = new WorldPosition(Mission.Scene, UIntPtr.Zero, formationFrame.origin + formationFrame.rotation.f * 5f, hasValidZ: false).GetGroundVec3();
            MatrixFrame leftFrame = formationFrame;
            leftFrame.origin = formationFrame.origin - formationFrame.rotation.s * halfWidth;
            MatrixFrame rightFrame = formationFrame;
            rightFrame.origin = formationFrame.origin + formationFrame.rotation.s * halfWidth;
            for (int i = 0; i < VisualMarkerCount; i++)
            {
                MatrixFrame frame = i == 3 ? formationFrame : (i == 1 ? leftFrame : (i == 5 ? rightFrame : heroFrame));
                _visualMarkers[i].SetGlobalFrame(in frame);
            }
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                MatrixFrame sideFrame = side == shownSide ? heroFrame : CommanderBattleSpawnLayout.GetSideFrame(Mission, side);
                foreach (GameEntity marker in side == BattleSideEnum.Attacker ? _attackerVisualMarkers : _defenderVisualMarkers)
                {
                    marker.SetGlobalFrame(in sideFrame);
                }
            }
        }

        protected override void AddRemoveMessageHandlers(GameNetwork.NetworkMessageHandlerRegistererContainer registerer)
        {
            if (GameNetwork.IsClient)
            {
                // Sent by the stock lobby component whenever a commander loses a soldier.
                registerer.RegisterBaseHandler<BotsControlledChange>(HandleServerEventBotsControlledChange);
                registerer.RegisterBaseHandler<FormationSlotUpdate>(HandleServerEventFormationSlotUpdate);
                registerer.RegisterBaseHandler<FormationSlotPendingSelection>(HandleServerEventFormationSlotPendingSelection);
                registerer.RegisterBaseHandler<SideAIStatus>(HandleServerEventSideAIStatus);
                registerer.RegisterBaseHandler<SpawnLayoutSeed>(HandleServerEventSpawnLayoutSeed);
                registerer.RegisterBaseHandler<GeneralOrderAdded>(HandleServerEventGeneralOrderAdded);
                registerer.RegisterBaseHandler<GeneralOrderRemoved>(HandleServerEventGeneralOrderRemoved);
            }
        }

        private void HandleServerEventSpawnLayoutSeed(GameNetworkMessage baseMessage)
        {
            SpawnLayoutSeed message = (SpawnLayoutSeed)baseMessage;
            // Layout first: the slot listeners (formation list, visual markers) read it.
            CommanderBattleSpawnLayout.SetRound(Mission, message.Seed, message.TroopCounts);
            Slots.SetTroopCounts(message.TroopCounts);
            UpdateVisualMarkers();
        }

        private void HandleServerEventSideAIStatus(GameNetworkMessage baseMessage)
        {
            AIStatus = (SideAIStatus)baseMessage;
            OnAIStatusChanged?.Invoke();
        }

        private void HandleServerEventFormationSlotUpdate(GameNetworkMessage baseMessage)
        {
            FormationSlotUpdate message = (FormationSlotUpdate)baseMessage;
            Slots.Set(message.Side, message.SlotIndex, message.Commander, message.ClassIndex, message.WeaponIndex);
        }

        private void HandleServerEventFormationSlotPendingSelection(GameNetworkMessage baseMessage)
        {
            FormationSlotPendingSelection message = (FormationSlotPendingSelection)baseMessage;
            Slots.SetPendingSelection(message.Side, message.SlotIndex, message.ClassIndex, message.Perks);
        }

        // A request to the server; on a listen server the game mode runs it directly.
        private void Request(GameNetworkMessage message, Action<MissionMultiplayerCommanderBattle> runOnServer)
        {
            if (GameNetwork.IsClient)
            {
                CommanderBattleNetwork.SendToServer(message);
            }
            else if (GameNetwork.IsServer)
            {
                MissionMultiplayerCommanderBattle gameMode = Mission.GetMissionBehavior<MissionMultiplayerCommanderBattle>();
                if (gameMode != null)
                {
                    runOnServer(gameMode);
                }
            }
        }

        // Ask the server to field this troop type with these perks (one index per perk list) in our formation.
        public void RequestTroopSelection(int classIndex, int[] perks)
        {
            Request(new RequestTroopSelection(classIndex, perks), gameMode => gameMode.RequestTroopSelectionFrom(GameNetwork.MyPeer, classIndex, perks));
        }

        // Whether a troop pick sent now applies this round: before the armies spawn, as on the server
        // (MissionMultiplayerCommanderBattle.RequestTroopSelectionFrom).
        public bool CanTroopSelectionApplyNow
        {
            get
            {
                MultiplayerRoundState state = RoundComponent?.CurrentRoundState ?? MultiplayerRoundState.WaitingForPlayers;
                return state == MultiplayerRoundState.WaitingForPlayers || state == MultiplayerRoundState.Preparation;
            }
        }

        // Ask the server for command of a free formation of this role on our side (the troop screen's first step).
        public void RequestRoleCommand(CommanderRole role)
        {
            Request(new RequestRoleCommand(role), gameMode => gameMode.RequestRoleCommandFrom(GameNetwork.MyPeer, role));
        }

        // Ask the server for command of one of our side's formations.
        public void RequestFormationCommand(int slotIndex)
        {
            Request(new RequestFormationCommand(slotIndex), gameMode => gameMode.RequestFormationCommandFrom(GameNetwork.MyPeer, slotIndex));
        }

        // An admin panel command (the server checks that we are an admin).
        public void RequestAdminCommand(AdminCommandType command, NetworkCommunicator target = null)
        {
            Request(new AdminCommand(command, target), gameMode => gameMode.RunAdminCommand(GameNetwork.MyPeer, command, target));
        }

        private void HandleServerEventBotsControlledChange(GameNetworkMessage baseMessage)
        {
            BotsControlledChange message = (BotsControlledChange)baseMessage;
            MissionPeer missionPeer = message.Peer.GetComponent<MissionPeer>();
            if (missionPeer == null)
            {
                return;
            }
            missionPeer.BotsUnderControlAlive = message.AliveCount;
            BotsUnderControlTotalSetter?.Invoke(missionPeer, new object[] { message.TotalCount });
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
#if !SERVER
            GiveMarkersToVisualSpawner();
#endif
            _strengthUpdateTimer += dt;
            if (_strengthUpdateTimer < StrengthUpdatePeriod)
            {
                return;
            }
            _strengthUpdateTimer = 0f;
            UpdateSideStrength(BattleSideEnum.Defender);
            UpdateSideStrength(BattleSideEnum.Attacker);
        }

        // Fraction of the side's soldiers still standing, relative to the side's troop count this round, or the most
        // seen if more (the commanders themselves stand on top of the counts).
        private void UpdateSideStrength(BattleSideEnum side)
        {
            int alive = CommanderBattleTeams.CountActiveSoldiers(Mission, side);
            int index = (int)side;
            if (alive > _initialSoldiers[index])
            {
                _initialSoldiers[index] = alive;
            }
            // Someone who joins mid-round never saw the full army. Only once soldiers stand, so the bar stays full
            // through preparation.
            if (alive > 0 && IsRoundInProgress)
            {
                _initialSoldiers[index] = Math.Max(_initialSoldiers[index], Slots.GetTotalTroopCount(side));
            }
            float strength = _initialSoldiers[index] == 0 ? 1f : (float)alive / _initialSoldiers[index];
            OnMoraleChangedEvent?.Invoke(side, strength);
        }

        public override void OnClearScene()
        {
            _initialSoldiers[0] = 0;
            _initialSoldiers[1] = 0;
            AIStatus = null;
            OnAIStatusChanged?.Invoke();
            // The server clears its own at the same point (and has already sent the removals at round end).
            ClearGeneralOrders();
        }

        public Team GetFlagOwner(FlagCapturePoint flag)
        {
            return null;
        }

        public override SpectatorCameraTypes GetMissionCameraLockMode(bool lockedToMainPlayer)
        {
            MissionPeer missionPeer = GameNetwork.IsMyPeerReady ? GameNetwork.MyPeer.GetComponent<MissionPeer>() : null;
            if (lockedToMainPlayer || missionPeer == null)
            {
                return SpectatorCameraTypes.Invalid;
            }
            if (missionPeer.Team == Mission.SpectatorTeam)
            {
                return SpectatorCameraTypes.Free;
            }
            // A dead commander watches their own soldiers, so they can pick one to take over.
            Formation formation = missionPeer.ControlledFormation;
            if (IsRoundInProgress && formation != null && formation.HasUnitsWithCondition(agent => !agent.IsPlayerControlled && agent.IsActive()))
            {
                return SpectatorCameraTypes.LockToPlayerFormation;
            }
            return SpectatorCameraTypes.Invalid;
        }

        // Compass shows every friendly commander's formation, including those in the hidden ally team.
        public override List<CompassItemUpdateParams> GetCompassTargets()
        {
            List<CompassItemUpdateParams> targets = new List<CompassItemUpdateParams>();
            if (!GameNetwork.IsMyPeerReady || !IsRoundInProgress)
            {
                return targets;
            }
            MissionPeer myPeer = GameNetwork.MyPeer.GetComponent<MissionPeer>();
            if (myPeer?.Team == null || myPeer.Team.Side == BattleSideEnum.None)
            {
                return targets;
            }
            foreach (NetworkCommunicator networkPeer in GameNetwork.NetworkPeers)
            {
                MissionPeer commander = networkPeer.GetComponent<MissionPeer>();
                Formation formation = commander?.ControlledFormation;
                if (commander?.Team == null || commander.Team.Side != myPeer.Team.Side || formation == null || formation.CountOfUnits == 0 || formation.Team == null)
                {
                    continue;
                }
                WorldPosition position = formation.CachedMedianPosition;
                Vec2 average = formation.SmoothedAverageUnitPosition;
                position.SetVec2(average.IsValid ? average : formation.CachedAveragePosition);
                if (formation.Banner == null)
                {
                    formation.Banner = new Banner(formation.BannerCode, formation.Team.Color, formation.Team.Color2);
                }
                TargetIconType icon = MultiplayerClassDivisions.GetMPHeroClassForPeer(commander)?.IconType ?? TargetIconType.None;
                targets.Add(new CompassItemUpdateParams(formation, icon, position.GetNavMeshVec3(), formation.Banner, formation.Team.IsAttacker, isAlly: true));
            }
            return targets;
        }

        public override int GetGoldAmount()
        {
            return 0;
        }

        public override void OnGoldAmountChangedForRepresentative(MissionRepresentativeBase representative, int goldAmount)
        {
        }
    }
}
