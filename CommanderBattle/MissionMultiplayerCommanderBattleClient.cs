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

        // What our side's team AI is doing (tactic, formation behaviours and destinations), from the server; null
        // until the first SideAIStatus of the round.
        public SideAIStatus AIStatus { get; private set; }

        public event Action OnAIStatusChanged;

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
            CommanderBattleLog.Write("Client.OnBehaviorInitialize");
            base.OnBehaviorInitialize();
            CreateVisualMarkersIfMissing();
        }

        public override void EarlyStart()
        {
            CommanderBattleLog.Write("Client.EarlyStart");
            base.EarlyStart();
        }

        public override void AfterStart()
        {
            CommanderBattleLog.Write("Client.AfterStart");
            Mission.Current.SetMissionMode(MissionMode.Battle, atStart: true);
            // Spawn paths are known from here on.
            _markersCanBePlaced = _visualMarkers != null && CommanderBattleSpawnLayout.UsesSpawnPaths(Mission);
            UpdateVisualMarkers();
            CommanderBattleLog.Write("Client.AfterStart done");
        }

        public override void OnRemoveBehavior()
        {
            if (_visualMarkers != null)
            {
                Slots.OnSlotChanged -= OnSlotChangedForMarkers;
                MissionPeer.OnTeamChanged -= OnTeamChangedForMarkers;
            }
            base.OnRemoveBehavior();
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
            CommanderBattleLog.Write("Created preparation visual markers (scene has none)");
        }

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
            MissionPeer myPeer = GameNetwork.IsMyPeerReady ? GameNetwork.MyPeer.GetComponent<MissionPeer>() : null;
            BattleSideEnum mySide = myPeer?.Team?.Side ?? BattleSideEnum.None;
            BattleSideEnum shownSide = mySide == BattleSideEnum.None ? BattleSideEnum.Attacker : mySide;
            FormationSlot mySlot = GameNetwork.IsMyPeerReady ? Slots.FindSlotOf(GameNetwork.MyPeer) : null;
            MatrixFrame formationFrame = mySlot != null && mySlot.Side == shownSide
                ? CommanderBattleSpawnLayout.GetFormationFrame(Mission, shownSide, mySlot.Index)
                : CommanderBattleSpawnLayout.GetSideFrame(Mission, shownSide);
            float halfWidth = mySlot?.Role == CommanderRole.Cavalry ? 15f : 10f;
            // Commander in front of the troops (markers 0, side markers), troops centred on 3 between 1 and 5.
            MatrixFrame heroFrame = formationFrame;
            heroFrame.origin = formationFrame.origin + formationFrame.rotation.f * 5f;
            MatrixFrame leftFrame = formationFrame;
            leftFrame.origin = formationFrame.origin - formationFrame.rotation.s * halfWidth;
            MatrixFrame rightFrame = formationFrame;
            rightFrame.origin = formationFrame.origin + formationFrame.rotation.s * halfWidth;
            for (int i = 0; i < VisualMarkerCount; i++)
            {
                MatrixFrame frame = i == 3 ? formationFrame : (i == 1 ? leftFrame : (i == 5 ? rightFrame : heroFrame));
                _visualMarkers[i].SetGlobalFrame(in frame);
            }
            foreach (BattleSideEnum side in new[] { BattleSideEnum.Attacker, BattleSideEnum.Defender })
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
                registerer.RegisterBaseHandler<SideAIStatus>(HandleServerEventSideAIStatus);
                registerer.RegisterBaseHandler<SpawnLayoutSeed>(HandleServerEventSpawnLayoutSeed);
            }
        }

        private void HandleServerEventSpawnLayoutSeed(GameNetworkMessage baseMessage)
        {
            CommanderBattleSpawnLayout.SetRoundSeed(Mission, ((SpawnLayoutSeed)baseMessage).Seed);
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
            Slots.Set(message.Side, message.SlotIndex, message.Commander, message.ClassIndex);
        }

        // Ask the server for command of one of our side's formations.
        public void RequestFormationCommand(int slotIndex)
        {
            if (GameNetwork.IsClient)
            {
                GameNetwork.BeginModuleEventAsClient();
                GameNetwork.WriteMessage(new RequestFormationCommand(slotIndex));
                GameNetwork.EndModuleEventAsClient();
            }
            else if (GameNetwork.IsServer)
            {
                Mission.GetMissionBehavior<MissionMultiplayerCommanderBattle>()?.RequestFormationCommandFrom(GameNetwork.MyPeer, slotIndex);
            }
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
            _strengthUpdateTimer += dt;
            if (_strengthUpdateTimer < StrengthUpdatePeriod)
            {
                return;
            }
            _strengthUpdateTimer = 0f;
            UpdateSideStrength(BattleSideEnum.Defender);
            UpdateSideStrength(BattleSideEnum.Attacker);
        }

        // Fraction of the side's soldiers still standing, relative to the most seen this round.
        private void UpdateSideStrength(BattleSideEnum side)
        {
            int alive = CommanderBattleTeams.CountActiveSoldiers(Mission, side);
            int index = (int)side;
            if (alive > _initialSoldiers[index])
            {
                _initialSoldiers[index] = alive;
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
