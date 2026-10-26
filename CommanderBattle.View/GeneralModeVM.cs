using System;
using TaleWorlds.Library;

namespace CommanderBattle.View
{
    // Data for the general's orders drawn on the screen (paths and labels) and for the general-mode menu. Screen
    // positions are in UI units (screen pixels times the layer's inverse scale), for PositionXOffset/YOffset.
    public class GeneralModeVM : ViewModel
    {
        private readonly Action _onPathTool;

        private readonly Action _onDeleteTool;

        private bool _isGeneralMode;

        private bool _isEnterHintVisible;

        private bool _isPathTool = true;

        private bool _isDeleteTool;

        private string _hintText;

        private string _countText;

        private MBBindingList<GeneralOrderSegmentVM> _segments = new MBBindingList<GeneralOrderSegmentVM>();

        private MBBindingList<GeneralOrderSegmentVM> _pendingSegments = new MBBindingList<GeneralOrderSegmentVM>();

        private MBBindingList<GeneralOrderLabelVM> _labels = new MBBindingList<GeneralOrderLabelVM>();

        public GeneralModeVM(Action onPathTool, Action onDeleteTool)
        {
            _onPathTool = onPathTool;
            _onDeleteTool = onDeleteTool;
        }

        // The general-mode menu shows.
        [DataSourceProperty]
        public bool IsGeneralMode
        {
            get => _isGeneralMode;
            set
            {
                if (value != _isGeneralMode)
                {
                    _isGeneralMode = value;
                    OnPropertyChangedWithValue(value, nameof(IsGeneralMode));
                }
            }
        }

        // The general isn't in general mode but could open it: the key hint shows.
        [DataSourceProperty]
        public bool IsEnterHintVisible
        {
            get => _isEnterHintVisible;
            set
            {
                if (value != _isEnterHintVisible)
                {
                    _isEnterHintVisible = value;
                    OnPropertyChangedWithValue(value, nameof(IsEnterHintVisible));
                }
            }
        }

        [DataSourceProperty]
        public string EnterHintText => "Ctrl+C: General mode (draw orders for your side)";

        [DataSourceProperty]
        public bool IsPathTool
        {
            get => _isPathTool;
            set
            {
                if (value != _isPathTool)
                {
                    _isPathTool = value;
                    OnPropertyChangedWithValue(value, nameof(IsPathTool));
                }
            }
        }

        [DataSourceProperty]
        public bool IsDeleteTool
        {
            get => _isDeleteTool;
            set
            {
                if (value != _isDeleteTool)
                {
                    _isDeleteTool = value;
                    OnPropertyChangedWithValue(value, nameof(IsDeleteTool));
                }
            }
        }

        [DataSourceProperty]
        public string TitleText => "General mode";

        [DataSourceProperty]
        public string PathText => "Path";

        [DataSourceProperty]
        public string DeleteText => "Delete";

        [DataSourceProperty]
        public string KeysText => "Mouse: look around    Hold Ctrl: cursor    Mouse wheel: zoom    F1-F9: order bar    Ctrl+C or Esc: leave";

        // What the selected tool does.
        [DataSourceProperty]
        public string HintText
        {
            get => _hintText;
            set
            {
                if (value != _hintText)
                {
                    _hintText = value;
                    OnPropertyChangedWithValue(value, nameof(HintText));
                }
            }
        }

        [DataSourceProperty]
        public string CountText
        {
            get => _countText;
            set
            {
                if (value != _countText)
                {
                    _countText = value;
                    OnPropertyChangedWithValue(value, nameof(CountText));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<GeneralOrderSegmentVM> Segments
        {
            get => _segments;
            set
            {
                if (value != _segments)
                {
                    _segments = value;
                    OnPropertyChangedWithValue(value, nameof(Segments));
                }
            }
        }

        // The path being drawn, before it is sent.
        [DataSourceProperty]
        public MBBindingList<GeneralOrderSegmentVM> PendingSegments
        {
            get => _pendingSegments;
            set
            {
                if (value != _pendingSegments)
                {
                    _pendingSegments = value;
                    OnPropertyChangedWithValue(value, nameof(PendingSegments));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<GeneralOrderLabelVM> Labels
        {
            get => _labels;
            set
            {
                if (value != _labels)
                {
                    _labels = value;
                    OnPropertyChangedWithValue(value, nameof(Labels));
                }
            }
        }

        public void ExecuteSelectPath()
        {
            _onPathTool?.Invoke();
        }

        public void ExecuteSelectDelete()
        {
            _onDeleteTool?.Invoke();
        }
    }

    // One straight piece of a path, from Start to End on the ground: a thin bar placed at the projected start,
    // rotated towards the projected end.
    public class GeneralOrderSegmentVM : ViewModel
    {
        private float _screenX = -5000f;

        private float _screenY = -5000f;

        private float _length;

        private float _angle;

        private bool _isOnScreen;

        public GeneralOrderSegmentVM(Vec3 start, Vec3 end)
        {
            Start = start;
            End = end;
        }

        public Vec3 Start { get; }

        public Vec3 End { get; }

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
        public float Length
        {
            get => _length;
            set
            {
                if (value != _length)
                {
                    _length = value;
                    OnPropertyChangedWithValue(value, nameof(Length));
                }
            }
        }

        // Degrees, clockwise on the screen (its y axis points down).
        [DataSourceProperty]
        public float Angle
        {
            get => _angle;
            set
            {
                if (value != _angle)
                {
                    _angle = value;
                    OnPropertyChangedWithValue(value, nameof(Angle));
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

    // A path's label at its midpoint; in the Delete tool, a button that asks the server to delete the path.
    public class GeneralOrderLabelVM : ViewModel
    {
        private readonly Action<int> _onDelete;

        private string _text;

        private float _screenX = -5000f;

        private float _screenY = -5000f;

        private bool _isOnScreen;

        private bool _canDelete;

        public GeneralOrderLabelVM(int orderId, string text, Vec3 worldPosition, Action<int> onDelete)
        {
            OrderId = orderId;
            _text = text;
            WorldPosition = worldPosition;
            _onDelete = onDelete;
        }

        public int OrderId { get; }

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

        // Disabled labels take no clicks, so a click on one outside the Delete tool reaches the ground below.
        [DataSourceProperty]
        public bool CanDelete
        {
            get => _canDelete;
            set
            {
                if (value != _canDelete)
                {
                    _canDelete = value;
                    OnPropertyChangedWithValue(value, nameof(CanDelete));
                }
            }
        }

        public void ExecuteDelete()
        {
            _onDelete?.Invoke(OrderId);
        }
    }
}
