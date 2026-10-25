using TaleWorlds.Library;

namespace CommanderBattle.View
{
    // Data for the side banner: which side the player fights on, announced large at the start of a round and
    // then kept small at the top of the screen.
    public class SideBannerVM : ViewModel
    {
        private string _sideText;

        private string _taskText;

        private bool _isVisible;

        private bool _isAnnouncing;

        [DataSourceProperty]
        public string SideText
        {
            get => _sideText;
            set
            {
                if (value != _sideText)
                {
                    _sideText = value;
                    OnPropertyChangedWithValue(value, nameof(SideText));
                }
            }
        }

        [DataSourceProperty]
        public string TaskText
        {
            get => _taskText;
            set
            {
                if (value != _taskText)
                {
                    _taskText = value;
                    OnPropertyChangedWithValue(value, nameof(TaskText));
                }
            }
        }

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
                    OnPropertyChangedWithValue(IsSmallVisible, nameof(IsSmallVisible));
                }
            }
        }

        [DataSourceProperty]
        public bool IsAnnouncing
        {
            get => _isAnnouncing;
            set
            {
                if (value != _isAnnouncing)
                {
                    _isAnnouncing = value;
                    OnPropertyChangedWithValue(value, nameof(IsAnnouncing));
                    OnPropertyChangedWithValue(IsSmallVisible, nameof(IsSmallVisible));
                }
            }
        }

        [DataSourceProperty]
        public bool IsSmallVisible => _isVisible && !_isAnnouncing;
    }
}
