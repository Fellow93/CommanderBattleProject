using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.ViewModelCollection.HUD.FormationMarker;
using MarkerVisibility = TaleWorlds.MountAndBlade.View.MissionViews.MissionFormationTargetSelectionHandler.FormationMarkerVisibility;

namespace CommanderBattle.View
{
    // While Left Alt is held (with the AI status and the formation markers):
    //   - a label over each allied player within MaxDistance, with their name (every player's for a spectator off
    //     both sides);
    //   - by the stock icon of each formation a player leads, on either side, its troops and whose it is: "Khan's
    //     Guard (Fellow)". The icons also show while the order bar is open, and so do the labels. A spectator sees both
    //     sides' labels. An enemy formation's label hides with its icon when the formation is out of sight (the stock
    //     targeting visibility check).
    //   - an icon for a player general's formation, which the stock icons leave out: they list
    //     Team.FormationsIncludingEmpty, the 8 regular formations. It is drawn with the stock widget and rules, and
    //     gets its label too. An AI general gets neither.
    // Players are found through the sides' formation slots, so each marker and label belongs to one slot. The stock
    // multiplayer marker view shows every teammate at any range, and Commander Battle leaves it out
    // (CommanderBattleMissionViews).
    public class CommanderBattleAllyPlayerMarkersView : CommanderBattleLayerView<AllyPlayerMarkersVM>
    {
        private const string MovieName = "CommanderBattleAllyPlayerMarkers";

        private const float MaxDistance = 100f;

        // Above the eyes, clear of the helmet.
        private const float HeadClearance = 0.6f;

        // The stock formation icon's height over the formation's median position (MissionGauntletFormationMarker).
        private const float FormationIconHeight = 3f;

        // The stock multiplayer icon fade (MissionFormationMarkerVM). The server's FormationMarker* options override
        // it for the stock icons; Commander Battle's config sets none of them.
        private const float IconFarAlphaTarget = 0.35f;

        private const float IconFarDistanceCutoff = 350f;

        private const float IconCloseDistanceCutoff = 5f;

        private const float IconClosestFadeoutRange = 1f;

        private const float IconAlwaysOnDistance = 25f;

        // The targeting handler checks whether a formation is in sight only for the 8 regular formations. The
        // general's goes through the same private check, so an enemy general hides out of sight like the others.
        private static readonly FieldInfo VisibilityConfigField = typeof(MissionFormationTargetSelectionHandler).GetField("_visibilityConfig", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo TargetingDisabledField = typeof(MissionFormationTargetSelectionHandler).GetField("_isTargetingDisabled", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo IsFormationVisibleEnoughMethod = typeof(MissionFormationTargetSelectionHandler).GetMethod("IsFormationVisibleEnough", BindingFlags.Instance | BindingFlags.NonPublic);

        // The handler's range for that check (MaxDistanceForFocusCheck).
        private const float MaxVisibilityCheckDistance = 1000f;

        private bool _labelsDirty = true;

        private MissionFormationTargetSelectionHandler _targetSelection;

        // The general's icon of each block of labels (GetLabelSide), null while that general isn't a player.
        private readonly MissionFormationMarkerTargetVM[] _generalIcons = new MissionFormationMarkerTargetVM[2];

        public CommanderBattleAllyPlayerMarkersView()
        {
            // With the AI status markers (14), below the side banner (15).
            ViewOrderPriority = 14;
        }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            Client.Slots.OnSlotChanged += OnSlotChanged;
            MissionPeer.OnTeamChanged += OnTeamChanged;
            _targetSelection = Mission.GetMissionBehavior<MissionFormationTargetSelectionHandler>();
            AllyPlayerMarkersVM dataSource = new AllyPlayerMarkersVM();
            // The player's side's slots, then the enemy's (GetLabelSide).
            for (int i = 0; i < 2 * CommanderBattleSettings.SlotCount; i++)
            {
                dataSource.Markers.Add(new AllyPlayerMarkerVM());
                dataSource.FormationLabels.Add(new AllyPlayerMarkerVM());
            }
            OpenLayer(dataSource, MovieName, ViewOrderPriority, captureMouse: false);
        }

        public override void OnMissionScreenFinalize()
        {
            Client.Slots.OnSlotChanged -= OnSlotChanged;
            MissionPeer.OnTeamChanged -= OnTeamChanged;
            base.OnMissionScreenFinalize();
        }

        // A new commander or troop class changes a label.
        private void OnSlotChanged(FormationSlot slot)
        {
            _labelsDirty = true;
        }

        private void OnTeamChanged(NetworkCommunicator peer, Team previousTeam, Team newTeam)
        {
            if (peer.IsMine)
            {
                _labelsDirty = true;
            }
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (!IsOpen)
            {
                return;
            }
            BattleSideEnum mySide = CommanderBattleTeams.MySide;
            if (_labelsDirty)
            {
                _labelsDirty = false;
                RefreshLabelTexts(mySide);
            }
            Camera camera = MissionScreen.CombatCamera;
            bool isAltDown = camera != null && Input.IsKeyDown(InputKey.LeftAlt);
            UpdatePlayerMarkers(mySide, camera, isAltDown);
            UpdateFormationLabels(mySide, camera, isAltDown || (camera != null && Mission.IsOrderMenuOpen));
            if (camera != null)
            {
                UpdateGeneralIcons(mySide, camera);
            }
        }

        // The first SlotCount markers and labels are the player's side's, the rest the enemy's. A spectator off both
        // sides gets both sides', defenders first (CommanderBattleTeams.Sides).
        private static BattleSideEnum GetLabelSide(BattleSideEnum mySide, int labelIndex)
        {
            bool first = labelIndex < CommanderBattleSettings.SlotCount;
            if (mySide != BattleSideEnum.Attacker && mySide != BattleSideEnum.Defender)
            {
                return first ? BattleSideEnum.Defender : BattleSideEnum.Attacker;
            }
            return first ? mySide : mySide.GetOppositeSide();
        }

        private void UpdatePlayerMarkers(BattleSideEnum mySide, Camera camera, bool show)
        {
            // From the player's own agent; from the camera while spectating.
            Vec3 from = show ? (Agent.Main?.Position ?? camera.Position) : Vec3.Invalid;
            bool isOnSide = mySide == BattleSideEnum.Attacker || mySide == BattleSideEnum.Defender;
            for (int i = 0; i < DataSource.Markers.Count; i++)
            {
                AllyPlayerMarkerVM marker = DataSource.Markers[i];
                BattleSideEnum side = GetLabelSide(mySide, i);
                // Names are for allies only; a spectator off both sides sees everyone's.
                FormationSlot slot = show && (!isOnSide || side == mySide) ? Client.Slots.GetSlot(side, i % CommanderBattleSettings.SlotCount) : null;
                // None for a general: he rides inside his small bodyguard, right under its icon, whose label has his
                // name.
                if (!TryGetPlayer(slot, out MissionPeer peer, out Agent agent) || slot.IsGeneral || agent.Position.Distance(from) > MaxDistance)
                {
                    marker.IsOnScreen = false;
                    continue;
                }
                Vec3 position = agent.GetEyeGlobalPosition();
                position.z += HeadClearance;
                if (Project(marker, camera, position))
                {
                    marker.Text = peer.DisplayedName;
                }
            }
        }

        private void UpdateFormationLabels(BattleSideEnum mySide, Camera camera, bool show)
        {
            for (int i = 0; i < DataSource.FormationLabels.Count; i++)
            {
                AllyPlayerMarkerVM label = DataSource.FormationLabels[i];
                BattleSideEnum side = GetLabelSide(mySide, i);
                Team team = show && !string.IsNullOrEmpty(label.Text) ? CommanderBattleTeams.GetTeam(Mission, side) : null;
                Formation formation = team?.GetFormation((FormationClass)(i % CommanderBattleSettings.SlotCount));
                WorldPosition median = formation != null && formation.CountOfUnits > 0 ? formation.CachedMedianPosition : WorldPosition.Invalid;
                // The stock icon fades out a formation the player can't see (the handler checks only teams that aren't
                // the player's allies: the enemy, or both sides for a spectator); the label must not give it away.
                // (Asked only for a formation: the handler's cache lookup throws on null.)
                if (!median.IsValid || GetMarkerVisibility(formation) == MarkerVisibility.Hidden)
                {
                    label.IsOnScreen = false;
                    continue;
                }
                Project(label, camera, median.GetGroundVec3() + new Vec3(0f, 0f, FormationIconHeight));
            }
        }

        // The icon of each player general's formation, kept as MissionGauntletFormationMarker keeps its own: shown on
        // the same key and with the order bar, and moved every tick so it can fade out.
        private void UpdateGeneralIcons(BattleSideEnum mySide, Camera camera)
        {
            bool show = Mission.Mode != MissionMode.Deployment && (Input.IsGameKeyDown(5) || Mission.IsOrderMenuOpen);
            bool showDistance = ManagedOptions.GetConfig(ManagedOptions.ManagedOptionsType.ShowFormationDistances) > 1E-05f;
            for (int block = 0; block < _generalIcons.Length; block++)
            {
                int labelIndex = block * CommanderBattleSettings.SlotCount + CommanderBattleSettings.GeneralSlot;
                BattleSideEnum side = GetLabelSide(mySide, labelIndex);
                // A player general's slot has a label text (RefreshLabelTexts).
                Formation formation = !string.IsNullOrEmpty(DataSource.FormationLabels[labelIndex].Text)
                    ? CommanderBattleTeams.GetTeam(Mission, side)?.GetFormation(FormationClass.General)
                    : null;
                if (formation != null && formation.CountOfUnits == 0)
                {
                    formation = null;
                }
                MissionFormationMarkerTargetVM icon = _generalIcons[block];
                if (icon?.Formation != formation)
                {
                    if (icon != null)
                    {
                        DataSource.GeneralIcons.Remove(icon);
                    }
                    icon = formation != null ? CreateGeneralIcon(formation, side) : null;
                    _generalIcons[block] = icon;
                    if (icon != null)
                    {
                        DataSource.GeneralIcons.Add(icon);
                    }
                }
                if (icon != null)
                {
                    icon.IsEnabled = show;
                    icon.ShowDistanceTexts = showDistance;
                    icon.Size = formation.CountOfUnits;
                    UpdateIconPosition(icon, camera);
                }
            }
        }

        private static MissionFormationMarkerTargetVM CreateGeneralIcon(Formation formation, BattleSideEnum side)
        {
            // The constructor sets the team colour from the player's team. The icon is the general's own, as on the
            // role card.
            return new MissionFormationMarkerTargetVM(formation)
            {
                FormationType = CommanderRoles.GetIcon(CommanderBattleTeams.GetCulture(side), CommanderRole.General),
                FarAlphaTarget = IconFarAlphaTarget,
                FarDistanceCutoff = IconFarDistanceCutoff,
                CloseDistanceCutoff = IconCloseDistanceCutoff,
                ClosestFadeoutRange = IconClosestFadeoutRange,
                AlwaysOnDistance = IconAlwaysOnDistance
            };
        }

        // MissionGauntletFormationMarker.UpdateMarkerPositions and GetVisibilityState, for one icon.
        private void UpdateIconPosition(MissionFormationMarkerTargetVM icon, Camera camera)
        {
            WorldPosition median = icon.Formation.CachedMedianPosition;
            if (!median.IsValid)
            {
                icon.WSign = -1;
                icon.Distance = 10000f;
                icon.DistanceText = string.Empty;
                icon.ScreenPosition = new Vec2(-10000f, -10000f);
                icon.VisibilityRatio = 1f;
                icon.VisibilityState = -1;
                return;
            }
            Vec3 ground = median.GetGroundVec3();
            float screenX = 0f;
            float screenY = 0f;
            float w = 0f;
            MBWindowManager.WorldToScreen(camera, ground + new Vec3(0f, 0f, FormationIconHeight), ref screenX, ref screenY, ref w);
            if (!TaleWorlds.Library.MathF.IsValidValue(w) || !TaleWorlds.Library.MathF.IsValidValue(screenX) || !TaleWorlds.Library.MathF.IsValidValue(screenY))
            {
                screenX = -10000f;
                screenY = -10000f;
                w = -1f;
            }
            icon.WSign = w < 0f ? -1 : 1;
            icon.Distance = camera.Position.Distance(ground);
            icon.ScreenPosition = new Vec2(screenX, screenY);
            MarkerVisibility visibility = GetMarkerVisibility(icon.Formation);
            icon.VisibilityRatio = _targetSelection?.GetFormationVisibilityRatio(icon.Formation) ?? 1f;
            icon.VisibilityState = visibility == MarkerVisibility.NotEvaluated ? -1
                : visibility == MarkerVisibility.Hidden ? 0
                : icon.Distance <= icon.AlwaysOnDistance ? 2 : 1;
            if (icon.ShowDistanceTexts)
            {
                Agent main = Agent.Main;
                icon.DistanceText = ((int)(main != null && main.IsActive() ? main.Position.Distance(ground) : icon.Distance)).ToString();
            }
            else
            {
                icon.DistanceText = string.Empty;
            }
        }

        // The targeting handler's out-of-sight verdict, worked out here for the general's formation, which the
        // handler skips (see VisibilityConfigField). Like the handler, it checks only teams that aren't the player's
        // allies, and none when targeting visibility is off.
        private MarkerVisibility GetMarkerVisibility(Formation formation)
        {
            if (_targetSelection == null)
            {
                return MarkerVisibility.NotEvaluated;
            }
            if (formation.FormationIndex != FormationClass.General)
            {
                return _targetSelection.GetFormationMarkerVisibility(formation);
            }
            if (formation.Team.IsPlayerAlly || VisibilityConfigField == null || TargetingDisabledField == null || IsFormationVisibleEnoughMethod == null
                || (bool)TargetingDisabledField.GetValue(_targetSelection)
                || ((MissionFormationTargetSelectionHandler.VisibilityConfig)VisibilityConfigField.GetValue(_targetSelection)).Mode
                    == MultiplayerOptions.FormationTargetingVisibilityModes.Disabled)
            {
                return MarkerVisibility.NotEvaluated;
            }
            // The handler's camera.
            Camera camera = MissionScreen.CustomCamera ?? MissionScreen.CombatCamera;
            if (formation.CachedMedianPosition.AsVec2.Distance(camera.Position.AsVec2) >= MaxVisibilityCheckDistance)
            {
                return MarkerVisibility.Hidden;
            }
            MatrixFrame viewProjection = MatrixFrame.Identity;
            camera.GetViewProjMatrix(ref viewProjection);
            bool isVisible = (bool)IsFormationVisibleEnoughMethod.Invoke(_targetSelection, new object[] { formation, viewProjection, camera.Position });
            return isVisible ? MarkerVisibility.Visible : MarkerVisibility.Hidden;
        }

        // "Khan's Guard (Fellow)" for each formation a player leads (the player's own too), the general's included;
        // none for AI formations. The general's icons are rebuilt too, for the player's new team colours.
        private void RefreshLabelTexts(BattleSideEnum mySide)
        {
            for (int i = 0; i < DataSource.FormationLabels.Count; i++)
            {
                BattleSideEnum side = GetLabelSide(mySide, i);
                FormationSlot slot = Client.Slots.GetSlot(side, i % CommanderBattleSettings.SlotCount);
                DataSource.FormationLabels[i].Text = slot == null || slot.Commander == null
                    ? ""
                    : GetFormationLabel(slot, CommanderBattleTeams.GetCulture(side));
            }
            DataSource.GeneralIcons.Clear();
            for (int block = 0; block < _generalIcons.Length; block++)
            {
                _generalIcons[block] = null;
            }
        }

        private static string GetFormationLabel(FormationSlot slot, BasicCultureObject culture)
        {
            string name = slot.Commander.GetComponent<MissionPeer>()?.DisplayedName ?? slot.Commander.UserName;
            // The troops' own name, as on the troop screen; the role until the class is known.
            MultiplayerClassDivisions.MPHeroClass heroClass = culture != null ? CommanderRoles.GetClass(culture, slot.ClassIndex) : null;
            string troops = heroClass?.TroopName.ToString() ?? CommanderRoles.GetName(slot.Role);
            return $"{troops} ({name})";
        }

        // Moves the marker to the point's place on screen; false while the point is behind the camera.
        private bool Project(AllyPlayerMarkerVM marker, Camera camera, Vec3 position)
        {
            float screenX = -5000f;
            float screenY = -5000f;
            float w = 0f;
            MBWindowManager.WorldToScreenInsideUsableArea(camera, position, ref screenX, ref screenY, ref w);
            marker.IsOnScreen = w > 0f;
            if (marker.IsOnScreen)
            {
                float inverseScale = Layer.UIContext.InverseScale;
                marker.ScreenX = screenX * inverseScale;
                marker.ScreenY = screenY * inverseScale;
            }
            return marker.IsOnScreen;
        }

        // Another player commanding the slot, with a living agent.
        private static bool TryGetPlayer(FormationSlot slot, out MissionPeer peer, out Agent agent)
        {
            peer = slot?.Commander?.GetComponent<MissionPeer>();
            agent = peer?.ControlledAgent;
            return peer != null && !slot.Commander.IsMine && agent != null && agent.IsActive();
        }
    }
}
