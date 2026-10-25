using TaleWorlds.Library;

namespace CommanderBattle.View
{
    // Data for the AI status panel (the side's tactic and what each formation does) and the destination markers.
    public class AIStatusVM : ViewModel
    {
        private bool _isVisible;

        private string _tacticText;

        private bool _leadsMainFormation;

        private MBBindingList<AIStatusLineVM> _formations = new MBBindingList<AIStatusLineVM>();

        private MBBindingList<AIDestinationMarkerVM> _markers = new MBBindingList<AIDestinationMarkerVM>();

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (value != _isVisible)
                {
                    _isVisible = value;
                    OnPropertyChangedWithValue(value, nameof(IsVisible));
                }
            }
        }

        [DataSourceProperty]
        public string TacticText
        {
            get => _tacticText;
            set
            {
                if (value != _tacticText)
                {
                    _tacticText = value;
                    OnPropertyChangedWithValue(value, nameof(TacticText));
                }
            }
        }

        // The player leads the tactic's main formation, so the AI's archers and cavalry move with them.
        [DataSourceProperty]
        public bool LeadsMainFormation
        {
            get => _leadsMainFormation;
            set
            {
                if (value != _leadsMainFormation)
                {
                    _leadsMainFormation = value;
                    OnPropertyChangedWithValue(value, nameof(LeadsMainFormation));
                }
            }
        }

        [DataSourceProperty]
        public string MainFormationText => CommanderBattleAIStatusView.MainFormationNotice;

        [DataSourceProperty]
        public MBBindingList<AIStatusLineVM> Formations
        {
            get => _formations;
            set
            {
                if (value != _formations)
                {
                    _formations = value;
                    OnPropertyChangedWithValue(value, nameof(Formations));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<AIDestinationMarkerVM> Markers
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

    public class AIStatusLineVM : ViewModel
    {
        private string _text;

        public AIStatusLineVM(string text)
        {
            _text = text;
        }

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
    }

    // A label on the ground where an AI formation is heading. ScreenX/ScreenY are in UI units (screen pixels times
    // the layer's inverse scale), for PositionXOffset/YOffset.
    public class AIDestinationMarkerVM : ViewModel
    {
        private string _text;

        private float _screenX = -5000f;

        private float _screenY = -5000f;

        private bool _isOnScreen;

        public AIDestinationMarkerVM(string text, Vec3 worldPosition)
        {
            _text = text;
            WorldPosition = worldPosition;
        }

        public Vec3 WorldPosition { get; }

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
