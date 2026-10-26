using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.HUD.FormationMarker;
using MissionGauntletFormationMarker = TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer.MissionGauntletFormationMarker;

namespace CommanderBattle.View
{
    // While the order bar is open, a gold frame around the stock formation icon of each formation selected in it (one
    // for the general, who selects one at a time), so the formation an order goes to is easy to find on the field.
    // The stock icons have no selected state. The frame is drawn with the stock icon's own view model and widget
    // (CommanderBattleSelectedFormationMarkers.xml), so it follows the icon, fades with it and hides with it; the
    // stock view keeps it updated. A formation without a stock icon (the general's own) gets none.
    public class CommanderBattleSelectedFormationView : CommanderBattleLayerView<SelectedFormationMarkersVM>
    {
        private const string MovieName = "CommanderBattleSelectedFormationMarkers";

        // The stock view keeps its icons' view models to itself.
        private static readonly FieldInfo MarkerDataSourceField = typeof(MissionGauntletFormationMarker).GetField("_dataSource", BindingFlags.Instance | BindingFlags.NonPublic);

        private MissionGauntletFormationMarker _markers;

        public CommanderBattleSelectedFormationView()
        {
            // Over the stock icons' layer (MissionGauntletFormationMarker, order 0), under every other HUD.
            ViewOrderPriority = 1;
        }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            _markers = Mission.GetMissionBehavior<MissionGauntletFormationMarker>();
            OpenLayer(new SelectedFormationMarkersVM(), MovieName, ViewOrderPriority, captureMouse: false);
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (IsOpen)
            {
                UpdateTargets();
            }
        }

        // Keeps the list to the stock icons of the selected formations, changing it only when the selection or the
        // stock icons change. The stock view only fills its icons while it is created (not with the battle UI hidden).
        private void UpdateTargets()
        {
            MBBindingList<MissionFormationMarkerTargetVM> framed = DataSource.Targets;
            MBReadOnlyList<Formation> selected = Mission.IsOrderMenuOpen ? Mission.PlayerTeam?.PlayerOrderController?.SelectedFormations : null;
            MBBindingList<MissionFormationMarkerTargetVM> icons = selected != null && selected.Count > 0 && _markers != null
                ? (MarkerDataSourceField?.GetValue(_markers) as MissionFormationMarkerVM)?.Targets
                : null;
            for (int i = framed.Count - 1; i >= 0; i--)
            {
                MissionFormationMarkerTargetVM target = framed[i];
                if (icons == null || !icons.Contains(target) || !selected.Contains(target.Formation))
                {
                    framed.RemoveAt(i);
                }
            }
            if (icons == null)
            {
                return;
            }
            for (int i = 0; i < icons.Count; i++)
            {
                MissionFormationMarkerTargetVM target = icons[i];
                if (selected.Contains(target.Formation) && !framed.Contains(target))
                {
                    framed.Add(target);
                }
            }
        }
    }
}
