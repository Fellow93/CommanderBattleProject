using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.HUD.FormationMarker;

namespace CommanderBattle.View
{
    // The stock formation icons' own view models (MissionGauntletFormationMarker's) of the formations selected in the
    // order bar, which CommanderBattleSelectedFormationView frames. Borrowed, not owned: the stock view updates and
    // finalizes them.
    public class SelectedFormationMarkersVM : ViewModel
    {
        private MBBindingList<MissionFormationMarkerTargetVM> _targets = new MBBindingList<MissionFormationMarkerTargetVM>();

        [DataSourceProperty]
        public MBBindingList<MissionFormationMarkerTargetVM> Targets
        {
            get => _targets;
            set
            {
                if (value != _targets)
                {
                    _targets = value;
                    OnPropertyChangedWithValue(value, nameof(Targets));
                }
            }
        }
    }
}
