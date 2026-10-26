using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.HUD.FormationMarker;

namespace CommanderBattle.View
{
    // Data for the allied player markers (one per allied player close enough to show), the labels by the stock
    // formation icons of the formations players lead, and the icons of the player generals' formations, which the
    // stock icons leave out.
    public class AllyPlayerMarkersVM : ViewModel
    {
        private MBBindingList<AllyPlayerMarkerVM> _markers = new MBBindingList<AllyPlayerMarkerVM>();

        private MBBindingList<AllyPlayerMarkerVM> _formationLabels = new MBBindingList<AllyPlayerMarkerVM>();

        private MBBindingList<MissionFormationMarkerTargetVM> _generalIcons = new MBBindingList<MissionFormationMarkerTargetVM>();

        [DataSourceProperty]
        public MBBindingList<MissionFormationMarkerTargetVM> GeneralIcons
        {
            get => _generalIcons;
            set
            {
                if (value != _generalIcons)
                {
                    _generalIcons = value;
                    OnPropertyChangedWithValue(value, nameof(GeneralIcons));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<AllyPlayerMarkerVM> FormationLabels
        {
            get => _formationLabels;
            set
            {
                if (value != _formationLabels)
                {
                    _formationLabels = value;
                    OnPropertyChangedWithValue(value, nameof(FormationLabels));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<AllyPlayerMarkerVM> Markers
        {
            get => _markers;
            set
            {
                if (value != _markers)
                {
                    _markers = value;
                    OnPropertyChangedWithValue(value, nameof(Markers));
                }
            }
        }
    }

    // A label with an allied player's name over their head, or with whose formation it is by its icon. ScreenX/
    // ScreenY are in UI units, as for AIDestinationMarkerVM.
    public class AllyPlayerMarkerVM : ViewModel
    {
        private string _text;

        private float _screenX = -5000f;

        private float _screenY = -5000f;

        private bool _isOnScreen;

        [DataSourceProperty]
        public string Text
        {
            get => _text;
            set
            {
                if (value != _text)
                {
                    _text = value;
                    OnPropertyChangedWithValue(value, nameof(Text));
                }
            }
        }

        [DataSourceProperty]
        public float ScreenX
        {
            get => _screenX;
            set
            {
                if (value != _screenX)
                {
                    _screenX = value;
                    OnPropertyChangedWithValue(value, nameof(ScreenX));
                }
            }
        }

        [DataSourceProperty]
        public float ScreenY
        {
            get => _screenY;
            set
            {
                if (value != _screenY)
                {
                    _screenY = value;
                    OnPropertyChangedWithValue(value, nameof(ScreenY));
                }
            }
        }

        [DataSourceProperty]
        public bool IsOnScreen
        {
            get => _isOnScreen;
            set
            {
                if (value != _isOnScreen)
                {
                    _isOnScreen = value;
                    OnPropertyChangedWithValue(value, nameof(IsOnScreen));
                }
            }
        }
    }
}
