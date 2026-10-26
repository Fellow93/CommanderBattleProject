using System.Collections.Generic;
using CommanderBattle.Network;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle.View
{
    // Shows the player what their side's team AI is doing, from the server's SideAIStatus, while Left Alt is held
    // (the key that shows the stock formation markers):
    //   - a panel on the left: the tactic, and per formation its behaviour, order and how far it still has to go;
    //   - a marker on the ground where each formation the AI or the general leads is heading;
    //   - a marker for the general's latest suggestion to the player's formation (docs/plans/general-orders.md): where
    //     to move, whom to charge, or above the formation for the others. The general sees the suggestions he gave,
    //     with Alt and always in general mode.
    // Leading the main formation is also told in the message log, which shows without Alt.
    public class CommanderBattleAIStatusView : CommanderBattleLayerView<AIStatusVM>
    {
        // Each suggestion marker, with the suggestion it shows (its place is worked out every frame).
        private readonly List<GeneralSuggestion> _shownSuggestions = new List<GeneralSuggestion>();

        private bool _suggestionsDirty = true;

        private const string MovieName = "CommanderBattleAIStatus";

        private const float MarkerHeight = 2f;

        public const string MainFormationNotice = "You lead the main formation: the AI's archers and cavalry take their places from yours";

        private static readonly Color NoticeColor = Color.FromUint(0xFFE8C14Au);

        private bool _dirty = true;

        // There is a status for the player's side to show.
        private bool _hasStatus;

        public CommanderBattleAIStatusView()
        {
            // Below the side banner (15), team selection (22) and formation list (24).
            ViewOrderPriority = 14;
        }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            Client.OnAIStatusChanged += OnAIStatusChanged;
            Client.OnGeneralSuggestionsChanged += OnSuggestionsChanged;
            Client.Slots.OnSlotChanged += OnSlotChanged;
            MissionPeer.OnTeamChanged += OnTeamChanged;
            OpenLayer(new AIStatusVM(), MovieName, ViewOrderPriority, captureMouse: false);
        }

        public override void OnMissionScreenFinalize()
        {
            Client.OnAIStatusChanged -= OnAIStatusChanged;
            Client.OnGeneralSuggestionsChanged -= OnSuggestionsChanged;
            Client.Slots.OnSlotChanged -= OnSlotChanged;
            MissionPeer.OnTeamChanged -= OnTeamChanged;
            base.OnMissionScreenFinalize();
        }

        private void OnAIStatusChanged()
        {
            _dirty = true;
        }

        private void OnSuggestionsChanged()
        {
            _suggestionsDirty = true;
        }

        // A new commander or general changes whose suggestions show.
        private void OnSlotChanged(FormationSlot slot)
        {
            _dirty = true;
            _suggestionsDirty = true;
        }

        private void OnTeamChanged(NetworkCommunicator peer, Team previousTeam, Team newTeam)
        {
            if (peer.IsMine)
            {
                _dirty = true;
                _suggestionsDirty = true;
            }
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (!IsOpen)
            {
                return;
            }
            if (_dirty)
            {
                _dirty = false;
                Rebuild();
            }
            if (_suggestionsDirty)
            {
                _suggestionsDirty = false;
                RebuildSuggestions();
            }
            bool isAltDown = Input.IsKeyDown(InputKey.LeftAlt);
            bool show = _hasStatus && isAltDown;
            DataSource.IsVisible = show;
            foreach (AIDestinationMarkerVM marker in DataSource.Markers)
            {
                UpdateMarker(marker, show);
            }
            // The general sees his suggestions all the time in general mode, where he gives them from.
            bool showSuggestions = isAltDown || (Client.IsGeneralHere && Mission.GetMissionBehavior<CommanderBattleGeneralModeView>()?.IsGeneralMode == true);
            for (int i = 0; i < DataSource.SuggestionMarkers.Count && i < _shownSuggestions.Count; i++)
            {
                AIDestinationMarkerVM marker = DataSource.SuggestionMarkers[i];
                bool hasPlace = TryGetSuggestionPlace(_shownSuggestions[i], out Vec3 place);
                if (hasPlace)
                {
                    marker.WorldPosition = place;
                }
                UpdateMarker(marker, showSuggestions && hasPlace);
            }
        }

        private void RebuildSuggestions()
        {
            DataSource.SuggestionMarkers.Clear();
            _shownSuggestions.Clear();
            foreach (GeneralSuggestion suggestion in Client.GeneralSuggestions)
            {
                if (!Client.IsSuggestionForMe(suggestion))
                {
                    continue;
                }
                FormationSlot slot = Client.Slots.GetSlot(suggestion.Side, suggestion.FormationIndex);
                string text = GeneralCommands.GetSuggestionLine(slot, suggestion.Kind, suggestion.HasTarget);
                _shownSuggestions.Add(suggestion);
                DataSource.SuggestionMarkers.Add(new AIDestinationMarkerVM(text, Vec3.Invalid));
            }
        }

        // Where a suggestion's marker stands: the move's point, the formation to charge or advance on, or above the
        // suggested formation itself for the others.
        private bool TryGetSuggestionPlace(GeneralSuggestion suggestion, out Vec3 place)
        {
            place = Vec3.Invalid;
            Vec2 point;
            if (suggestion.HasPosition)
            {
                point = suggestion.Position;
            }
            else
            {
                BattleSideEnum side = suggestion.HasTarget ? suggestion.TargetSide : suggestion.Side;
                int index = suggestion.HasTarget ? suggestion.TargetFormationIndex : suggestion.FormationIndex;
                Formation formation = CommanderBattleTeams.GetTeam(Mission, side)?.GetFormation((FormationClass)index);
                if (formation == null || formation.CountOfUnits == 0)
                {
                    return false;
                }
                Vec2 smoothed = formation.SmoothedAverageUnitPosition;
                point = smoothed.IsValid ? smoothed : formation.CachedAveragePosition;
            }
            if (!point.IsValid)
            {
                return false;
            }
            place = point.ToVec3(Mission.Scene.GetTerrainHeight(point) + MarkerHeight);
            return true;
        }

        private void Rebuild()
        {
            SideAIStatus status = Client.AIStatus;
            BattleSideEnum mySide = CommanderBattleTeams.MySide;
            DataSource.Formations.Clear();
            DataSource.Markers.Clear();
            if (status == null || status.Side != mySide)
            {
                // Between rounds or off a side: no "no longer" message for that.
                _hasStatus = false;
                DataSource.LeadsMainFormation = false;
                return;
            }
            DataSource.TacticText = "AI tactic: " + (string.IsNullOrEmpty(status.Tactic) ? "none" : status.Tactic);
            FormationSlot mySlot = Client.MySlot;
            int myFormationIndex = mySlot != null && mySlot.Side == mySide ? mySlot.Index : -1;
            bool leadsMain = false;
            foreach (AIFormationStatus formation in status.Formations)
            {
                string label = $"{GetLabel(formation)} ({formation.Troops})";
                if (formation.IsMain)
                {
                    label += " (main)";
                    leadsMain |= formation.IsPlayerLed && formation.FormationIndex == myFormationIndex;
                }
                DataSource.Formations.Add(new AIStatusLineVM(label + ": " + Describe(formation)));
                if (!formation.IsPlayerLed && formation.HasDestination)
                {
                    Vec3 position = formation.Destination.ToVec3(Mission.Scene.GetTerrainHeight(formation.Destination) + MarkerHeight);
                    string order = GetOrderName(formation.Order);
                    DataSource.Markers.Add(new AIDestinationMarkerVM($"{GetLabel(formation)}: {order}", position));
                }
            }
            _hasStatus = true;
            SetLeadsMainFormation(leadsMain);
        }

        // Also told in the message log when it changes, since the panel is easy to miss mid-fight.
        private void SetLeadsMainFormation(bool leadsMain)
        {
            if (leadsMain == DataSource.LeadsMainFormation)
            {
                return;
            }
            DataSource.LeadsMainFormation = leadsMain;
            InformationManager.DisplayMessage(new InformationMessage(
                leadsMain ? MainFormationNotice : "Your formation is no longer the main formation", NoticeColor));
        }

        private static string Describe(AIFormationStatus formation)
        {
            if (formation.IsPlayerLed)
            {
                return "led by a player";
            }
            string text = formation.Leader == AIFormationLeader.General ? "led by the general"
                : string.IsNullOrEmpty(formation.Behavior) ? "no behaviour"
                : formation.Behavior;
            string order = GetOrderName(formation.Order);
            if (order.Length > 0)
            {
                text += ", " + order;
            }
            if (formation.HasDestination)
            {
                text += $" {(int)formation.Position.Distance(formation.Destination)} m";
            }
            return text;
        }

        private void UpdateMarker(AIDestinationMarkerVM marker, bool show)
        {
            Camera camera = MissionScreen.CombatCamera;
            if (!show || camera == null)
            {
                marker.IsOnScreen = false;
                return;
            }
            float screenX = -5000f;
            float screenY = -5000f;
            float w = 0f;
            MBWindowManager.WorldToScreenInsideUsableArea(camera, marker.WorldPosition, ref screenX, ref screenY, ref w);
            marker.IsOnScreen = w >= 0f;
            if (marker.IsOnScreen)
            {
                // Real pixels to UI units; the prefab centres its dot on this point.
                float inverseScale = Layer.UIContext.InverseScale;
                marker.ScreenX = screenX * inverseScale;
                marker.ScreenY = screenY * inverseScale;
            }
        }

        // "3. Cavalry" by the order bar's formation number; the general, whom the order bar doesn't number, by name.
        private static string GetLabel(AIFormationStatus formation)
        {
            return formation.Kind == AIFormationKind.General ? GetKindName(formation.Kind) : $"{formation.FormationIndex + 1}. {GetKindName(formation.Kind)}";
        }

        private static string GetKindName(AIFormationKind kind)
        {
            switch (kind)
            {
                case AIFormationKind.Ranged:
                    return "Ranged";
                case AIFormationKind.Cavalry:
                    return "Cavalry";
                case AIFormationKind.HorseArchers:
                    return "Horse archers";
                case AIFormationKind.General:
                    return "General";
                default:
                    return "Infantry";
            }
        }

        private static string GetOrderName(AIFormationOrder order)
        {
            switch (order)
            {
                case AIFormationOrder.Move:
                    return "moving";
                case AIFormationOrder.Charge:
                    return "charging";
                case AIFormationOrder.Hold:
                    return "holding";
                case AIFormationOrder.Retreat:
                    return "retreating";
                default:
                    return "";
            }
        }
    }
}
