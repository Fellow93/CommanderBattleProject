using System;
using System.Collections.Generic;
using CommanderBattle.AI;
using CommanderBattle.Network;
using NetworkMessages.FromServer;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.Multiplayer;
using MathF = TaleWorlds.Library.MathF;

namespace CommanderBattle
{
    // One spawn per round. During preparation every commander gets agent visuals for themselves plus their
    // formation's troops; the base class turns those visuals into agents when preparation ends (and asks the
    // game mode for the troop count via GetTroopNumberMultiplierForMissingPlayer). Right after that, every
    // formation still empty is filled with AI-led troops, so each side always fields its full army.
    public class CommanderBattleSpawningBehavior : SpawningBehaviorBase
    {
        private MultiplayerRoundController _roundController;

        private MissionMultiplayerCommanderBattle _commanderBattle;

        private float _spawningTimer;

        private bool _spawningTimerTicking;

        private bool _roundInitialSpawnOver;

        private bool _aiFormationsSpawned;

        public override void Initialize(SpawnComponent spawnComponent)
        {
            CommanderBattleLog.Detail("Spawning.Initialize");
            base.Initialize(spawnComponent);
            _roundController = Mission.GetMissionBehavior<MultiplayerRoundController>();
            _commanderBattle = Mission.GetMissionBehavior<MissionMultiplayerCommanderBattle>();
            _roundController.OnRoundStarted += RequestStartSpawnSession;
            _roundController.OnRoundEnding += RequestStopSpawnSession;
            _roundController.OnRoundEnding += SetRemainingAgentsInvulnerable;
            OnPeerSpawnedFromVisuals += HandlePeerSpawnedFromVisuals;
            OnAllAgentsFromPeerSpawnedFromVisuals += HandleAllAgentsFromPeerSpawned;
        }

        public override void Clear()
        {
            base.Clear();
            _roundController.OnRoundStarted -= RequestStartSpawnSession;
            _roundController.OnRoundEnding -= RequestStopSpawnSession;
            _roundController.OnRoundEnding -= SetRemainingAgentsInvulnerable;
            OnPeerSpawnedFromVisuals -= HandlePeerSpawnedFromVisuals;
            OnAllAgentsFromPeerSpawnedFromVisuals -= HandleAllAgentsFromPeerSpawned;
        }

        public override void OnTick(float dt)
        {
            if (_spawningTimerTicking)
            {
                _spawningTimer += dt;
            }
            if (IsSpawningEnabled)
            {
                if (!_roundInitialSpawnOver && IsRoundInProgress())
                {
                    CommanderBattleLog.Detail("Round in progress: spawning players and AI formations");
                    _roundInitialSpawnOver = true;
                    Mission.AllowAiTicking = true;
                }
                SpawnAgents();
                if (_roundInitialSpawnOver && _spawningTimer > MultiplayerOptions.OptionType.RoundPreparationTimeLimit.GetIntValue())
                {
                    IsSpawningEnabled = false;
                    _spawningTimer = 0f;
                    _spawningTimerTicking = false;
                }
            }
            base.OnTick(dt);
            // After base.OnTick: players' formations have spawned from their visuals by now.
            if (_roundInitialSpawnOver && !_aiFormationsSpawned)
            {
                _aiFormationsSpawned = true;
                SpawnAiFormations();
                _commanderBattle.OnArmiesSpawned();
            }
        }

        public override void RequestStartSpawnSession()
        {
            CommanderBattleLog.Detail($"Spawn session requested (already enabled: {IsSpawningEnabled})");
            if (!IsSpawningEnabled)
            {
                Mission.Current.SetBattleAgentCount(-1);
                IsSpawningEnabled = true;
                _spawningTimerTicking = true;
                _aiFormationsSpawned = false;
                ResetSpawnCounts();
                ResetSpawnTimers();
            }
        }

        protected override void SpawnAgents()
        {
            BasicCultureObject attackerCulture = CommanderBattleTeams.GetCulture(BattleSideEnum.Attacker);
            BasicCultureObject defenderCulture = CommanderBattleTeams.GetCulture(BattleSideEnum.Defender);
            MultiplayerBattleColors battleColors = MultiplayerBattleColors.CreateWith(attackerCulture, defenderCulture);
            foreach (NetworkCommunicator networkPeer in GameNetwork.NetworkPeers)
            {
                MissionPeer commander = networkPeer.GetComponent<MissionPeer>();
                if (!networkPeer.IsSynchronized || commander?.Team == null || commander.Team.Side == BattleSideEnum.None || commander.Team == Mission.SpectatorTeam)
                {
                    continue;
                }
                if (commander.ControlledAgent != null || commander.HasSpawnedAgentVisuals || !commander.TeamInitialPerkInfoReady || !commander.SpawnTimer.Check(Mission.CurrentTime))
                {
                    continue;
                }
                FormationSlot slot = _commanderBattle.Slots.FindSlotOf(networkPeer);
                if (slot == null || slot.Side != commander.Team.Side)
                {
                    continue;
                }
                MultiplayerClassDivisions.MPHeroClass heroClass = MultiplayerClassDivisions.GetMPHeroClassForPeer(commander);
                if (heroClass == null)
                {
                    continue;
                }
                Formation formation = commander.Team.GetFormation(slot.FormationClass);
                formation.ContainsAgentVisuals = true;
                if (string.IsNullOrEmpty(formation.BannerCode))
                {
                    formation.BannerCode = commander.Peer.BannerCode;
                }
                bool isAttacker = commander.Team.IsAttacker;
                BasicCultureObject culture = isAttacker ? attackerCulture : defenderCulture;
                MultiplayerBattleColors.MultiplayerCultureColorInfo commanderColors = battleColors.GetPeerColors(commander);
                BasicCharacterObject heroCharacter = heroClass.HeroCharacter;
                AgentBuildData commanderData = new AgentBuildData(heroCharacter).MissionPeer(commander).Team(commander.Team).VisualsIndex(0)
                    .Formation(formation)
                    .MakeUnitStandOutOfFormationDistance(7f)
                    .IsFemale(commander.Peer.IsFemale)
                    .BodyProperties(GetBodyProperties(commander, culture))
                    .ClothingColor1(commanderColors.ClothingColor1Uint)
                    .ClothingColor2(commanderColors.ClothingColor2Uint);
                MPPerkObject.MPOnSpawnPerkHandler perkHandler = MPPerkObject.GetOnSpawnPerkHandler(commander);
                Equipment commanderEquipment = heroCharacter.Equipment.Clone();
                ApplyAlternativeEquipment(commanderEquipment, perkHandler?.GetAlternativeEquipments(isPlayer: true));
                commanderData.Equipment(commanderEquipment);
#if !SERVER
                if (GameMode.ShouldSpawnVisualsForServer(networkPeer))
                {
                    AgentVisualSpawnComponent.SpawnAgentVisualsForPeer(commander, commanderData, commander.SelectedTroopIndex);
                }
#endif
                GameMode.HandleAgentVisualSpawning(networkPeer, commanderData);
                commander.ControlledFormation = formation;

                IEnumerable<(EquipmentIndex, EquipmentElement)> troopEquipment = perkHandler?.GetAlternativeEquipments(isPlayer: false);
                MultiplayerBattleColors.MultiplayerCultureColorInfo sideColors = isAttacker ? battleColors.AttackerColors : battleColors.DefenderColors;
                for (int i = 0; i < slot.TroopCount; i++)
                {
                    SpawnSoldierVisuals(commander, i + 1, heroClass.TroopCharacter, formation, slot.TroopCount, troopEquipment, sideColors);
                }
            }
        }

        private void SpawnSoldierVisuals(MissionPeer commander, int visualsIndex, BasicCharacterObject troop, Formation formation, int totalCount, IEnumerable<(EquipmentIndex, EquipmentElement)> alternativeEquipment, MultiplayerBattleColors.MultiplayerCultureColorInfo colors)
        {
            AgentBuildData data = new AgentBuildData(troop).Team(commander.Team).OwningMissionPeer(commander).VisualsIndex(visualsIndex)
                .TroopOrigin(new BasicBattleAgentOrigin(troop))
                .EquipmentSeed(MissionLobbyComponent.GetRandomFaceSeedForCharacter(troop, visualsIndex))
                .Formation(formation)
                .IsFemale(troop.IsFemale)
                .ClothingColor1(colors.ClothingColor1Uint)
                .ClothingColor2(colors.ClothingColor2Uint);
            Equipment equipment = Equipment.GetRandomEquipmentElements(troop, randomEquipmentModifier: false, Equipment.EquipmentType.Battle, MBRandom.RandomInt());
            ApplyAlternativeEquipment(equipment, alternativeEquipment);
            data.Equipment(equipment);
            SetRandomBodyProperties(data, troop);
            NetworkCommunicator networkPeer = commander.GetNetworkPeer();
#if !SERVER
            if (GameMode.ShouldSpawnVisualsForServer(networkPeer))
            {
                AgentVisualSpawnComponent.SpawnAgentVisualsForPeer(commander, data, -1, isBot: true, totalCount);
            }
#endif
            GameMode.HandleAgentVisualSpawning(networkPeer, data, totalCount, useCosmetics: false);
        }

        // Formations nobody took (or whose commander did not spawn this round) are led by the team AI.
        private void SpawnAiFormations()
        {
            BasicCultureObject attackerCulture = CommanderBattleTeams.GetCulture(BattleSideEnum.Attacker);
            BasicCultureObject defenderCulture = CommanderBattleTeams.GetCulture(BattleSideEnum.Defender);
            MultiplayerBattleColors battleColors = MultiplayerBattleColors.CreateWith(attackerCulture, defenderCulture);
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
            {
                Team team = CommanderBattleTeams.GetTeam(Mission, side);
                if (team == null)
                {
                    continue;
                }
                BasicCultureObject culture = side == BattleSideEnum.Attacker ? attackerCulture : defenderCulture;
                MultiplayerBattleColors.MultiplayerCultureColorInfo colors = side == BattleSideEnum.Attacker ? battleColors.AttackerColors : battleColors.DefenderColors;
                int spawned = 0;
                foreach (FormationSlot slot in _commanderBattle.Slots.GetSlots(side))
                {
                    if (slot.IsFielded && team.GetFormation(slot.FormationClass).CountOfUnits == 0)
                    {
                        SpawnAiFormation(team, slot, culture, colors);
                        spawned++;
                    }
                }
                CommanderBattleLog.Detail($"{side}: {spawned} AI-led formations");
            }
        }

        private void SpawnAiFormation(Team team, FormationSlot slot, BasicCultureObject culture, MultiplayerBattleColors.MultiplayerCultureColorInfo colors)
        {
            MultiplayerClassDivisions.MPHeroClass heroClass = CommanderRoles.GetClass(culture, slot.ClassIndex);
            if (heroClass == null)
            {
                return;
            }
            BasicCharacterObject troop = heroClass.TroopCharacter;
            bool mounted = troop.HasMount();
            Formation formation = team.GetFormation(slot.FormationClass);
            formation.Banner = team.Banner;
            float width = 0f;
            MatrixFrame spawnFrame = UsesSpawnPaths
                ? CommanderBattleSpawnLayout.GetFormationFrame(Mission, team.Side, slot.Index, out width)
                : SpawnComponent.GetSpawnFrame(team, mounted, isInitialSpawn: true);
            List<WorldFrame> frames = GetUnitFrames(spawnFrame, slot.TroopCount, mounted, width);
            // The formation's weapon setup is a perk of its class. The AI has no peer to hold perks, so only the perk's
            // spawn equipment is applied, as the stock spawner applies a commander's to their troops; no perk handler,
            // so no combat perks.
            List<IReadOnlyPerkObject> setups = CommanderRoles.GetWeaponSetups(heroClass);
            IReadOnlyPerkObject setup = slot.WeaponIndex >= 0 && slot.WeaponIndex < setups.Count ? setups[slot.WeaponIndex] : null;
            List<(EquipmentIndex, EquipmentElement)> setupEquipment = setup?.GetAlternativeEquipments(isWarmup: false, isPlayer: false, null);
            CommanderBattleLog.Detail($"{team.Side} formation {slot.Index} ({slot.Role}, {heroClass.StringId}, {setup?.Name.ToString() ?? "own loadout"}) at {FormatPosition(spawnFrame.origin)}, {frames.Count} unit frames, first {(frames.Count > 0 ? FormatPosition(frames[0].ToGroundMatrixFrame().origin) : "-")}, last {(frames.Count > 0 ? FormatPosition(frames[frames.Count - 1].ToGroundMatrixFrame().origin) : "-")}");
            for (int i = 0; i < slot.TroopCount; i++)
            {
                MatrixFrame frame = i < frames.Count ? frames[i].ToGroundMatrixFrame() : spawnFrame;
                AgentBuildData data = new AgentBuildData(troop).Team(team)
                    .TroopOrigin(new BasicBattleAgentOrigin(troop))
                    .EquipmentSeed(MissionLobbyComponent.GetRandomFaceSeedForCharacter(troop, i))
                    .Formation(formation)
                    .IsFemale(troop.IsFemale)
                    .ClothingColor1(colors.ClothingColor1Uint)
                    .ClothingColor2(colors.ClothingColor2Uint)
                    .InitialPosition(in frame.origin)
                    .InitialDirection(frame.rotation.f.AsVec2.Normalized());
                Equipment equipment = Equipment.GetRandomEquipmentElements(troop, randomEquipmentModifier: false, Equipment.EquipmentType.Battle, data.AgentEquipmentSeed);
                ApplyAlternativeEquipment(equipment, setupEquipment);
                data.Equipment(equipment);
                SetRandomBodyProperties(data, troop);
                Agent agent = Mission.SpawnAgent(data);
                agent.AddComponent(new MPPerksAgentComponent(agent));
                agent.SetWatchState(Agent.WatchState.Alarmed);
                agent.WieldInitialWeapons();
            }
            if (slot.IsGeneral)
            {
                SpawnAiGeneral(team, slot, formation, heroClass, setup, spawnFrame, colors);
                return;
            }
            // The team AI (CommanderBattleTactic) gives the orders from here.
        }

        // The AI general: the class's hero, once, in front of his bodyguard as a commander stands in front of his
        // troops (PlaceOnSpawnPath), in the hero's own gear as a player general gets it. No tactic leads formation 8,
        // so it gets the stock general's behaviour weights here (CommanderBattleTeamAI.LeadAsGeneral).
        private void SpawnAiGeneral(Team team, FormationSlot slot, Formation formation, MultiplayerClassDivisions.MPHeroClass heroClass, IReadOnlyPerkObject setup, MatrixFrame spawnFrame, MultiplayerBattleColors.MultiplayerCultureColorInfo colors)
        {
            BasicCharacterObject hero = heroClass.HeroCharacter;
            Vec3 position = new WorldPosition(Mission.Scene, UIntPtr.Zero, spawnFrame.origin + spawnFrame.rotation.f * 5f, hasValidZ: false).GetGroundVec3();
            AgentBuildData data = new AgentBuildData(hero).Team(team)
                .TroopOrigin(new BasicBattleAgentOrigin(hero))
                .EquipmentSeed(MissionLobbyComponent.GetRandomFaceSeedForCharacter(hero, 0))
                .Formation(formation)
                .IsFemale(hero.IsFemale)
                .ClothingColor1(colors.ClothingColor1Uint)
                .ClothingColor2(colors.ClothingColor2Uint)
                .InitialPosition(in position)
                .InitialDirection(spawnFrame.rotation.f.AsVec2.Normalized());
            Equipment equipment = hero.Equipment.Clone();
            ApplyAlternativeEquipment(equipment, setup?.GetAlternativeEquipments(isWarmup: false, isPlayer: true, null));
            data.Equipment(equipment);
            SetRandomBodyProperties(data, hero);
            Agent general = Mission.SpawnAgent(data);
            general.AddComponent(new MPPerksAgentComponent(general));
            general.SetWatchState(Agent.WatchState.Alarmed);
            general.WieldInitialWeapons();
            CommanderBattleTeams.SetGeneral(team, formation, general);
            bool led = CommanderBattleTeamAI.LeadAsGeneral(formation);
            CommanderBattleLog.Detail($"{team.Side} AI general {hero.StringId} ({(general.HasMount ? "mounted" : "on foot")}) at {FormatPosition(position)} with {slot.TroopCount} bodyguards{(led ? "" : ", no general's behaviour")}");
        }

        private static string FormatPosition(Vec3 position)
        {
            return $"({position.x:0.0}, {position.y:0.0}, {position.z:0.0})";
        }

        private bool UsesSpawnPaths =>(SpawnComponent.SpawnFrameBehavior as CommanderBattleSpawnFrameBehavior)?.UsesSpawnPaths == true;

        // Ranks behind the frame's origin, facing the frame's forward direction. plannedWidth: the width the
        // deployment layout gives the formation (0 = a default block of 15 files, 10 mounted).
        private List<WorldFrame> GetUnitFrames(MatrixFrame frame, int count, bool mounted, float plannedWidth = 0f)
        {
            Mat3 rotation = frame.rotation;
            rotation.MakeUnit();
            float diameter = Formation.GetDefaultUnitDiameter(mounted);
            float interval = Formation.GetDefaultMinimumUnitInterval(mounted);
            int files = plannedWidth > 0f ? (int)((plannedWidth + interval) / (diameter + interval)) : (mounted ? 10 : 15);
            int columns = MathF.Max(1, MathF.Min(count, files));
            float width = columns * diameter + (columns - 1) * interval;
            return Formation.GetFormationFramesForBeforeFormationCreation(width, count, mounted, new WorldPosition(Mission.Scene, frame.origin), rotation);
        }

        // On campaign scenes players' troops spawn around the side's spawn path; put the formation in its place.
        private void PlaceOnSpawnPath(MissionPeer commander)
        {
            FormationSlot slot = _commanderBattle.Slots.FindSlotOf(commander.GetNetworkPeer());
            Formation formation = commander.ControlledFormation;
            if (slot == null || formation == null || commander.ControlledAgent == null)
            {
                return;
            }
            List<Agent> soldiers = new List<Agent>();
            formation.ApplyActionOnEachUnit(delegate (Agent agent)
            {
                if (agent != commander.ControlledAgent)
                {
                    soldiers.Add(agent);
                }
            });
            MatrixFrame frame = CommanderBattleSpawnLayout.GetFormationFrame(Mission, slot.Side, slot.Index, out float width);
            bool mounted = soldiers.Count > 0 && soldiers[0].HasMount;
            List<WorldFrame> frames = GetUnitFrames(frame, soldiers.Count, mounted, width);
            CommanderBattleLog.Detail($"Placing {commander.GetNetworkPeer().UserName}'s {soldiers.Count} soldiers at {FormatPosition(frame.origin)}, {frames.Count} unit frames");
            for (int i = 0; i < soldiers.Count && i < frames.Count; i++)
            {
                soldiers[i].TeleportToPosition(frames[i].ToGroundMatrixFrame().origin);
            }
            Vec3 commanderPosition = new WorldPosition(Mission.Scene, UIntPtr.Zero, frame.origin + frame.rotation.f * 5f, hasValidZ: false).GetGroundVec3();
            commander.ControlledAgent.TeleportToPosition(commanderPosition);
        }

        private static void SetRandomBodyProperties(AgentBuildData data, BasicCharacterObject troop)
        {
            data.BodyProperties(BodyProperties.GetRandomBodyProperties(data.AgentRace, data.AgentIsFemale, troop.GetBodyPropertiesMin(), troop.GetBodyPropertiesMax(), (int)data.AgentOverridenSpawnEquipment.HairCoverType, data.AgentEquipmentSeed, troop.BodyPropertyRange.HairTags, troop.BodyPropertyRange.BeardTags, troop.BodyPropertyRange.TattooTags));
        }

        private static void ApplyAlternativeEquipment(Equipment equipment, IEnumerable<(EquipmentIndex, EquipmentElement)> alternatives)
        {
            if (alternatives == null)
            {
                return;
            }
            foreach ((EquipmentIndex index, EquipmentElement element) in alternatives)
            {
                equipment[index] = element;
            }
        }

        private void HandlePeerSpawnedFromVisuals(MissionPeer commander)
        {
            if (commander.ControlledFormation != null)
            {
                commander.ControlledAgent.Team.AssignPlayerAsSergeantOfFormation(commander, commander.ControlledFormation.FormationIndex);
                if (commander.ControlledFormation.FormationIndex == FormationClass.General)
                {
                    CommanderBattleTeams.SetGeneral(commander.ControlledAgent.Team, commander.ControlledFormation, commander.ControlledAgent);
                }
            }
        }

        private void HandleAllAgentsFromPeerSpawned(MissionPeer commander)
        {
            if (commander.ControlledFormation == null)
            {
                return;
            }
            if (UsesSpawnPaths)
            {
                PlaceOnSpawnPath(commander);
            }
            commander.ControlledFormation.OnFormationDispersed();
            commander.ControlledFormation.SetMovementOrder(MovementOrder.MovementOrderFollow(commander.ControlledAgent));
            // The server counted the soldiers as they spawned; give clients the starting numbers for the
            // scoreboard (later changes are sent by the stock lobby component).
            CommanderBattleNetwork.Broadcast(new BotsControlledChange(commander.GetNetworkPeer(), commander.BotsUnderControlAlive, commander.BotsUnderControlTotal));
        }

        public override bool AllowEarlyAgentVisualsDespawning(MissionPeer missionPeer)
        {
            return false;
        }

        protected override bool IsRoundInProgress()
        {
            return _roundController.IsRoundInProgress;
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            _roundInitialSpawnOver = false;
            _aiFormationsSpawned = false;
        }
    }
}
