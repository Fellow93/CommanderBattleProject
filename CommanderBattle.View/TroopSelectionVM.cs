using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle.View
{
    // Data for the troop selection screen, in three steps: 1 the formation class (role), which takes a free formation
    // of it; 2 the weight (one class of the role each); 3 the weapon setup (the class's weapon perks), then Confirm. A
    // role with a single class, the general, goes from 1 straight to 3.
    public class TroopSelectionVM : ViewModel
    {
        public const int StepRole = 1;

        public const int StepWeight = 2;

        public const int StepWeapons = 3;

        // How long step 1 waits for the server to hand over a formation of the picked role before it gives up (the
        // server explains a refusal in a chat message).
        private const float RoleRequestTimeout = 3f;

        private readonly Action<CommanderRole> _onRequestRole;

        private readonly Action<int, int> _onConfirm;

        private readonly Action _onClose;

        private FormationSlotTable _slots;

        private BattleSideEnum _side;

        private BasicCultureObject _culture;

        private FormationSlot _slot;

        private IReadOnlyList<MultiplayerClassDivisions.MPHeroClass> _classes = new List<MultiplayerClassDivisions.MPHeroClass>();

        private int _step = StepRole;

        private int _selectedClass = -1;

        private int _selectedWeapon;

        private CommanderRole? _requestedRole;

        private float _requestTimer;

        private bool _applyNow;

        private MBBindingList<TroopStepVM> _steps;

        private MBBindingList<TroopRoleCardVM> _roles;

        private MBBindingList<TroopTypeVM> _types;

        private MBBindingList<TroopPerkOptionVM> _weapons;

        private string _title;

        private string _hint;

        private string _status;

        private string _columnTitle;

        private string _detailTitle;

        private string _selectedName;

        private string _selectedInfo;

        private string _confirmText;

        private string _backText;

        private string _closeText;

        private bool _showRoles;

        private bool _showTypes;

        private bool _showWeapons;

        private bool _canGoBack;

        private bool _canConfirm;

        public TroopSelectionVM(Action<CommanderRole> onRequestRole, Action<int, int> onConfirm, Action onClose)
        {
            _onRequestRole = onRequestRole;
            _onConfirm = onConfirm;
            _onClose = onClose;
            Steps = new MBBindingList<TroopStepVM>
            {
                new TroopStepVM("1  Formation class"),
                new TroopStepVM("2  Weight"),
                new TroopStepVM("3  Weapons")
            };
            Roles = new MBBindingList<TroopRoleCardVM>();
            Types = new MBBindingList<TroopTypeVM>();
            Weapons = new MBBindingList<TroopPerkOptionVM>();
            ConfirmText = "Confirm";
            BackText = "Back";
            CloseText = "Cancel";
        }

        public int Step => _step;

        // step: StepRole, or StepWeight for the formation the player commands (StepRole without one).
        public void Initialize(FormationSlotTable slots, BattleSideEnum side, FormationSlot mySlot, int step)
        {
            _slots = slots;
            _side = side;
            _culture = CommanderBattleTeams.GetCulture(side);
            _classes = CommanderRoles.GetClasses(_culture);
            _slot = mySlot != null && mySlot.Side == side ? mySlot : null;
            if (step >= StepWeight && _slot != null)
            {
                EnterWeightStep();
            }
            else
            {
                GoToStep(StepRole);
            }
        }

        // A slot of the side changed (who commands it, its class, count or pending pick).
        public void OnSlotsChanged(FormationSlot mySlot)
        {
            FormationSlot previous = _slot;
            _slot = mySlot != null && mySlot.Side == _side ? mySlot : null;
            if (_requestedRole.HasValue && _slot != null && _slot.Role == _requestedRole.Value)
            {
                // The server handed us a formation of the role picked in step 1.
                _requestedRole = null;
                EnterWeightStep();
                return;
            }
            if (_step != StepRole && (_slot == null || previous == null || _slot.Role != previous.Role))
            {
                // Lost the formation, or moved to one of another role: steps 2 and 3 no longer apply.
                GoToStep(StepRole);
                return;
            }
            if (_step != StepRole && _slot != previous)
            {
                // Moved to another formation of the same role: start from what it fields.
                EnterWeightStep();
                return;
            }
            Refresh();
        }

        public void Tick(float dt)
        {
            if (!_requestedRole.HasValue)
            {
                return;
            }
            _requestTimer -= dt;
            if (_requestTimer <= 0f)
            {
                _requestedRole = null;
                Refresh();
            }
        }

        // applyNow: a pick confirmed now changes this round's troops (preparation, before the armies spawn).
        public void RefreshStatus(bool applyNow)
        {
            _applyNow = applyNow;
            if (_requestedRole.HasValue)
            {
                Status = $"Taking a {CommanderRoles.GetName(_requestedRole.Value).ToLowerInvariant()} formation...";
                return;
            }
            if (_slot == null)
            {
                Status = "You don't lead a formation. Pick a class to take one.";
                return;
            }
            string current = $"{_slot.DisplayName} now: {_slot.TroopCount} x {Describe(_slot.ClassIndex, _slot.WeaponIndex)}.";
            string pending = _slot.HasPendingSelection ? $" Next round: {Describe(_slot.PendingClassIndex, _slot.PendingPerks[CommanderRoles.WeaponPerkList])}." : "";
            string when = _step == StepWeapons ? (applyNow ? " Confirm applies to this round." : " Confirm applies next round.") : "";
            Status = $"{current}{pending}{when}";
        }

        private string Describe(int classIndex, int weaponIndex)
        {
            MultiplayerClassDivisions.MPHeroClass heroClass = GetClass(classIndex);
            if (heroClass == null)
            {
                return "?";
            }
            string weapons = CommanderRoles.GetWeaponName(heroClass, weaponIndex);
            return weapons.Length > 0 ? $"{heroClass.TroopName} ({weapons})" : heroClass.TroopName.ToString();
        }

        private MultiplayerClassDivisions.MPHeroClass GetClass(int classIndex)
        {
            return classIndex >= 0 && classIndex < _classes.Count ? _classes[classIndex] : null;
        }

        // Step 2 starts from the formation's pending pick if it has one, otherwise from what it fields now.
        private void EnterWeightStep()
        {
            if (_slot == null)
            {
                GoToStep(StepRole);
                return;
            }
            if (_slot.HasPendingSelection)
            {
                _selectedClass = _slot.PendingClassIndex;
                _selectedWeapon = _slot.PendingPerks[CommanderRoles.WeaponPerkList];
            }
            else
            {
                _selectedClass = _slot.ClassIndex;
                _selectedWeapon = _slot.WeaponIndex;
            }
            if (CommanderRoles.GetRole(GetClass(_selectedClass)) != _slot.Role)
            {
                List<int> weights = CommanderRoles.GetWeightClassIndexes(_culture, _slot.Role);
                _selectedClass = weights.Count > 0 ? weights[0] : -1;
                _selectedWeapon = 0;
            }
            ClampWeapon();
            // A role with one class (the general) has no weight to pick.
            GoToStep(HasSingleWeight ? StepWeapons : StepWeight);
        }

        private bool HasSingleWeight => _slot != null && CommanderRoles.GetClassIndexes(_culture, _slot.Role).Count == 1;

        private void ClampWeapon()
        {
            int count = CommanderRoles.GetWeaponSetups(GetClass(_selectedClass)).Count;
            if (_selectedWeapon < 0 || _selectedWeapon >= count)
            {
                _selectedWeapon = 0;
            }
        }

        private void GoToStep(int step)
        {
            _step = step;
            if (step == StepRole)
            {
                _selectedClass = -1;
            }
            Refresh();
        }

        public void Refresh()
        {
            if (_slots == null)
            {
                return;
            }
            for (int i = 0; i < Steps.Count; i++)
            {
                Steps[i].IsSelected = i + 1 == _step;
            }
            ShowRoles = _step == StepRole;
            ShowTypes = _step != StepRole;
            ShowWeapons = _step == StepWeapons;
            CanGoBack = _step != StepRole;
            CanConfirm = _step == StepWeapons && _slot != null && CommanderRoles.GetRole(GetClass(_selectedClass)) == _slot.Role;
            switch (_step)
            {
                case StepRole:
                    RefreshRoleStep();
                    break;
                case StepWeight:
                    RefreshWeightStep();
                    break;
                default:
                    RefreshWeaponStep();
                    break;
            }
            RefreshStatus(_applyNow);
        }

        private void RefreshRoleStep()
        {
            Title = $"{_culture?.Name} formations";
            Hint = "Pick the class of formation you want to lead: you take a free formation of it. Then its weight and weapons.";
            ColumnTitle = "Formation class";
            Types.Clear();
            Weapons.Clear();
            Roles.Clear();
            Color cultureColor = Color.FromUint(_culture?.Color ?? 0xFFFFFFFFu);
            List<string> split = new List<string>();
            foreach (CommanderRole role in CommanderRoles.All)
            {
                _slots.CountRole(_side, role, out int free, out int total);
                if (total == 0)
                {
                    // The culture fields none (horse archers for most).
                    continue;
                }
                split.Add($"{total} {CommanderRoles.GetName(role).ToLowerInvariant()}");
                bool mine = _slot != null && _slot.Role == role;
                bool waiting = _requestedRole == role;
                string tag = $"{free} of {total} free{(mine ? " · yours" : "")}{(waiting ? " · taking one..." : "")}";
                bool canPick = !_requestedRole.HasValue && (mine || free > 0);
                Roles.Add(new TroopRoleCardVM(role, CommanderRoles.GetName(role), tag, $"{free}/{total}", CommanderRoles.GetIcon(_culture, role), cultureColor, mine, canPick, OnRoleSelected));
            }
            DetailTitle = "";
            SelectedName = $"{_culture?.Name}";
            SelectedInfo = $"Formations: {string.Join(", ", split)}.{(_slot != null ? $" You lead {_slot.DisplayName}." : "")} A class with no free formation can't be picked.";
        }

        private void RefreshWeightStep()
        {
            Title = $"Troops for {_slot.DisplayName}";
            Hint = "Pick the weight: lighter troops field more men, heavier ones fewer in better armour.";
            ColumnTitle = $"{CommanderRoles.GetName(_slot.Role)}: weight";
            Roles.Clear();
            Weapons.Clear();
            RefreshTypes(onlySelected: false);
            DetailTitle = "";
            RefreshSelectedInfo();
        }

        private void RefreshWeaponStep()
        {
            Title = $"Troops for {_slot.DisplayName}";
            Hint = "Pick the weapon setup. It doesn't change how many men you field.";
            ColumnTitle = HasSingleWeight ? CommanderRoles.GetName(_slot.Role) : $"{CommanderRoles.GetName(_slot.Role)}: weight";
            Roles.Clear();
            RefreshTypes(onlySelected: true);
            RefreshWeapons();
            DetailTitle = "Weapon setup";
            RefreshSelectedInfo();
        }

        private void RefreshTypes(bool onlySelected)
        {
            Types.Clear();
            Color cultureColor = Color.FromUint(_culture?.Color ?? 0xFFFFFFFFu);
            foreach (int classIndex in CommanderRoles.GetWeightClassIndexes(_culture, _slot.Role))
            {
                if (onlySelected && classIndex != _selectedClass)
                {
                    continue;
                }
                MultiplayerClassDivisions.MPHeroClass heroClass = _classes[classIndex];
                int weaponIndex = classIndex == _selectedClass ? _selectedWeapon : 0;
                int count = CommanderRoles.PreviewTroopCount(_culture, _slot.Budget, heroClass, CommanderRoles.MakePerks(weaponIndex));
                List<string> tags = new List<string>();
                string weight = CommanderRoles.GetWeightLabel(heroClass);
                if (weight.Length > 0)
                {
                    tags.Add(weight);
                }
                tags.Add($"armour {heroClass.ArmorValue}");
                tags.Add($"speed {heroClass.TroopMovementSpeedMultiplier * 100f:0}%");
                if (classIndex == _slot.ClassIndex)
                {
                    tags.Add("fielded now");
                }
                if (_slot.HasPendingSelection && classIndex == _slot.PendingClassIndex)
                {
                    tags.Add("next round");
                }
                // The icon names in the class XML are the compass icon layers (Compass.Type.Icons).
                Types.Add(new TroopTypeVM(classIndex, heroClass.TroopName.ToString(), count.ToString(), string.Join(" · ", tags), heroClass.IconType.ToString(), cultureColor, classIndex == _selectedClass, canPick: true, OnTypeSelected));
            }
        }

        private void RefreshWeapons()
        {
            Weapons.Clear();
            List<IReadOnlyPerkObject> setups = CommanderRoles.GetWeaponSetups(GetClass(_selectedClass));
            for (int i = 0; i < setups.Count; i++)
            {
                Weapons.Add(new TroopPerkOptionVM(i, setups[i].Name.ToString(), setups[i].Description.ToString(), setups[i].IconId, i == _selectedWeapon, OnWeaponSelected));
            }
        }

        private void RefreshSelectedInfo()
        {
            MultiplayerClassDivisions.MPHeroClass heroClass = GetClass(_selectedClass);
            if (heroClass == null)
            {
                SelectedName = "";
                SelectedInfo = "";
                return;
            }
            int count = CommanderRoles.PreviewTroopCount(_culture, _slot.Budget, heroClass, CommanderRoles.MakePerks(_selectedWeapon));
            string weight = CommanderRoles.GetWeightLabel(heroClass);
            string role = CommanderRoles.GetName(_slot.Role).ToLowerInvariant();
            string weapons = CommanderRoles.GetWeaponName(heroClass, _selectedWeapon);
            SelectedName = heroClass.TroopName.ToString();
            SelectedInfo = $"{(weight.Length > 0 ? weight + " " + role : CommanderRoles.GetName(_slot.Role))} · {count} men{(_step == StepWeapons && weapons.Length > 0 ? " · " + weapons : "")}";
        }

        private void OnRoleSelected(CommanderRole role)
        {
            if (_requestedRole.HasValue)
            {
                return;
            }
            if (_slot != null && _slot.Role == role)
            {
                EnterWeightStep();
                return;
            }
            _requestedRole = role;
            _requestTimer = RoleRequestTimeout;
            _onRequestRole?.Invoke(role);
            Refresh();
        }

        private void OnTypeSelected(int classIndex)
        {
            if (classIndex != _selectedClass)
            {
                // The weights of a role offer the same setups in the same order, so the setup carries over.
                _selectedClass = classIndex;
                ClampWeapon();
            }
            GoToStep(StepWeapons);
        }

        private void OnWeaponSelected(int index)
        {
            _selectedWeapon = index;
            foreach (TroopPerkOptionVM option in Weapons)
            {
                option.IsSelected = option.Index == index;
            }
            RefreshSelectedInfo();
        }

        public void ExecuteBack()
        {
            if (_step == StepWeapons)
            {
                GoToStep(HasSingleWeight ? StepRole : StepWeight);
            }
            else if (_step == StepWeight)
            {
                GoToStep(StepRole);
            }
        }

        public void ExecuteConfirm()
        {
            if (CanConfirm)
            {
                _onConfirm?.Invoke(_selectedClass, _selectedWeapon);
            }
        }

        public void ExecuteClose()
        {
            _onClose?.Invoke();
        }

        [DataSourceProperty]
        public MBBindingList<TroopStepVM> Steps
        {
            get => _steps;
            set
            {
                if (value != _steps)
                {
                    _steps = value;
                    OnPropertyChangedWithValue(value, nameof(Steps));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<TroopRoleCardVM> Roles
        {
            get => _roles;
            set
            {
                if (value != _roles)
                {
                    _roles = value;
                    OnPropertyChangedWithValue(value, nameof(Roles));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<TroopTypeVM> Types
        {
            get => _types;
            set
            {
                if (value != _types)
                {
                    _types = value;
                    OnPropertyChangedWithValue(value, nameof(Types));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<TroopPerkOptionVM> Weapons
        {
            get => _weapons;
            set
            {
                if (value != _weapons)
                {
                    _weapons = value;
                    OnPropertyChangedWithValue(value, nameof(Weapons));
                }
            }
        }

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set
            {
                if (value != _title)
                {
                    _title = value;
                    OnPropertyChangedWithValue(value, nameof(Title));
                }
            }
        }

        [DataSourceProperty]
        public string Hint
        {
            get => _hint;
            set
            {
                if (value != _hint)
                {
                    _hint = value;
                    OnPropertyChangedWithValue(value, nameof(Hint));
                }
            }
        }

        [DataSourceProperty]
        public string Status
        {
            get => _status;
            set
            {
                if (value != _status)
                {
                    _status = value;
                    OnPropertyChangedWithValue(value, nameof(Status));
                }
            }
        }

        // Above the left column: the role cards (step 1) or the weight rows (steps 2 and 3).
        [DataSourceProperty]
        public string ColumnTitle
        {
            get => _columnTitle;
            set
            {
                if (value != _columnTitle)
                {
                    _columnTitle = value;
                    OnPropertyChangedWithValue(value, nameof(ColumnTitle));
                }
            }
        }

        // Above the weapon setups (step 3).
        [DataSourceProperty]
        public string DetailTitle
        {
            get => _detailTitle;
            set
            {
                if (value != _detailTitle)
                {
                    _detailTitle = value;
                    OnPropertyChangedWithValue(value, nameof(DetailTitle));
                }
            }
        }

        [DataSourceProperty]
        public string SelectedName
        {
            get => _selectedName;
            set
            {
                if (value != _selectedName)
                {
                    _selectedName = value;
                    OnPropertyChangedWithValue(value, nameof(SelectedName));
                }
            }
        }

        [DataSourceProperty]
        public string SelectedInfo
        {
            get => _selectedInfo;
            set
            {
                if (value != _selectedInfo)
                {
                    _selectedInfo = value;
                    OnPropertyChangedWithValue(value, nameof(SelectedInfo));
                }
            }
        }

        [DataSourceProperty]
        public string ConfirmText
        {
            get => _confirmText;
            set
            {
                if (value != _confirmText)
                {
                    _confirmText = value;
                    OnPropertyChangedWithValue(value, nameof(ConfirmText));
                }
            }
        }

        [DataSourceProperty]
        public string BackText
        {
            get => _backText;
            set
            {
                if (value != _backText)
                {
                    _backText = value;
                    OnPropertyChangedWithValue(value, nameof(BackText));
                }
            }
        }

        [DataSourceProperty]
        public string CloseText
        {
            get => _closeText;
            set
            {
                if (value != _closeText)
                {
                    _closeText = value;
                    OnPropertyChangedWithValue(value, nameof(CloseText));
                }
            }
        }

        [DataSourceProperty]
        public bool ShowRoles
        {
            get => _showRoles;
            set
            {
                if (value != _showRoles)
                {
                    _showRoles = value;
                    OnPropertyChangedWithValue(value, nameof(ShowRoles));
                }
            }
        }

        [DataSourceProperty]
        public bool ShowTypes
        {
            get => _showTypes;
            set
            {
                if (value != _showTypes)
                {
                    _showTypes = value;
                    OnPropertyChangedWithValue(value, nameof(ShowTypes));
                }
            }
        }

        [DataSourceProperty]
        public bool ShowWeapons
        {
            get => _showWeapons;
            set
            {
                if (value != _showWeapons)
                {
                    _showWeapons = value;
                    OnPropertyChangedWithValue(value, nameof(ShowWeapons));
                }
            }
        }

        [DataSourceProperty]
        public bool CanGoBack
        {
            get => _canGoBack;
            set
            {
                if (value != _canGoBack)
                {
                    _canGoBack = value;
                    OnPropertyChangedWithValue(value, nameof(CanGoBack));
                }
            }
        }

        [DataSourceProperty]
        public bool CanConfirm
        {
            get => _canConfirm;
            set
            {
                if (value != _canConfirm)
                {
                    _canConfirm = value;
                    OnPropertyChangedWithValue(value, nameof(CanConfirm));
                }
            }
        }
    }

    // One step label in the header; the current one is highlighted.
    public class TroopStepVM : ViewModel
    {
        private string _name;

        private bool _isSelected;

        public TroopStepVM(string name)
        {
            Name = name;
        }

        [DataSourceProperty]
        public string Name
        {
            get => _name;
            set
            {
                if (value != _name)
                {
                    _name = value;
                    OnPropertyChangedWithValue(value, nameof(Name));
                }
            }
        }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    OnPropertyChangedWithValue(value, nameof(IsSelected));
                }
            }
        }
    }

    // A formation class card (step 1): the role, its icon and how many of its formations are free.
    public class TroopRoleCardVM : ViewModel
    {
        private readonly Action<CommanderRole> _onSelect;

        private string _name;

        private string _tag;

        private string _countText;

        private string _iconType;

        private Color _cultureColor;

        private bool _isSelected;

        private bool _canPick;

        public TroopRoleCardVM(CommanderRole role, string name, string tag, string countText, string iconType, Color cultureColor, bool isSelected, bool canPick, Action<CommanderRole> onSelect)
        {
            Role = role;
            _onSelect = onSelect;
            Name = name;
            Tag = tag;
            CountText = countText;
            IconType = iconType;
            CultureColor = cultureColor;
            IsSelected = isSelected;
            CanPick = canPick;
        }

        public CommanderRole Role { get; }

        public void ExecuteSelect()
        {
            if (CanPick)
            {
                _onSelect?.Invoke(Role);
            }
        }

        [DataSourceProperty]
        public string Name
        {
            get => _name;
            set
            {
                if (value != _name)
                {
                    _name = value;
                    OnPropertyChangedWithValue(value, nameof(Name));
                }
            }
        }

        // "2 of 3 free · yours".
        [DataSourceProperty]
        public string Tag
        {
            get => _tag;
            set
            {
                if (value != _tag)
                {
                    _tag = value;
                    OnPropertyChangedWithValue(value, nameof(Tag));
                }
            }
        }

        // "2/3": free formations of the role.
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

        // A layer name of the Compass.Type.Icons brush.
        [DataSourceProperty]
        public string IconType
        {
            get => _iconType;
            set
            {
                if (value != _iconType)
                {
                    _iconType = value;
                    OnPropertyChangedWithValue(value, nameof(IconType));
                }
            }
        }

        [DataSourceProperty]
        public Color CultureColor
        {
            get => _cultureColor;
            set
            {
                if (value != _cultureColor)
                {
                    _cultureColor = value;
                    OnPropertyChangedWithValue(value, nameof(CultureColor));
                }
            }
        }

        // The player's own formation class.
        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    OnPropertyChangedWithValue(value, nameof(IsSelected));
                }
            }
        }

        [DataSourceProperty]
        public bool CanPick
        {
            get => _canPick;
            set
            {
                if (value != _canPick)
                {
                    _canPick = value;
                    OnPropertyChangedWithValue(value, nameof(CanPick));
                }
            }
        }
    }

    // A weight (step 2): one class of the role, with the men it would field.
    public class TroopTypeVM : ViewModel
    {
        private readonly int _classIndex;

        private readonly Action<int> _onSelect;

        private string _name;

        private string _countText;

        private string _tag;

        private string _iconType;

        private Color _cultureColor;

        private bool _isSelected;

        private bool _canPick;

        public TroopTypeVM(int classIndex, string name, string countText, string tag, string iconType, Color cultureColor, bool isSelected, bool canPick, Action<int> onSelect)
        {
            _classIndex = classIndex;
            _onSelect = onSelect;
            Name = name;
            CountText = countText;
            Tag = tag;
            IconType = iconType;
            CultureColor = cultureColor;
            IsSelected = isSelected;
            CanPick = canPick;
        }

        public void ExecuteSelect()
        {
            if (CanPick)
            {
                _onSelect?.Invoke(_classIndex);
            }
        }

        [DataSourceProperty]
        public string Name
        {
            get => _name;
            set
            {
                if (value != _name)
                {
                    _name = value;
                    OnPropertyChangedWithValue(value, nameof(Name));
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

        // "Heavy · armour 40 · speed 80% · fielded now".
        [DataSourceProperty]
        public string Tag
        {
            get => _tag;
            set
            {
                if (value != _tag)
                {
                    _tag = value;
                    OnPropertyChangedWithValue(value, nameof(Tag));
                }
            }
        }

        // A layer name of the Compass.Type.Icons brush (the class's icon in the class XML).
        [DataSourceProperty]
        public string IconType
        {
            get => _iconType;
            set
            {
                if (value != _iconType)
                {
                    _iconType = value;
                    OnPropertyChangedWithValue(value, nameof(IconType));
                }
            }
        }

        [DataSourceProperty]
        public Color CultureColor
        {
            get => _cultureColor;
            set
            {
                if (value != _cultureColor)
                {
                    _cultureColor = value;
                    OnPropertyChangedWithValue(value, nameof(CultureColor));
                }
            }
        }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    OnPropertyChangedWithValue(value, nameof(IsSelected));
                }
            }
        }

        [DataSourceProperty]
        public bool CanPick
        {
            get => _canPick;
            set
            {
                if (value != _canPick)
                {
                    _canPick = value;
                    OnPropertyChangedWithValue(value, nameof(CanPick));
                }
            }
        }
    }

    // A weapon setup (step 3): one weapon perk of the class.
    public class TroopPerkOptionVM : ViewModel
    {
        private readonly Action<int> _onSelect;

        private string _name;

        private string _description;

        private string _iconPath;

        private bool _isSelected;

        public TroopPerkOptionVM(int index, string name, string description, string iconId, bool isSelected, Action<int> onSelect)
        {
            Index = index;
            _onSelect = onSelect;
            Name = name;
            Description = description;
            // Perk icons are General\Perks\<icon> sprites (ui_group1, always loaded), as the stock loadout shows them.
            IconPath = string.IsNullOrEmpty(iconId) ? "General\\Perks\\PerkHoldTheLine" : "General\\Perks\\" + iconId;
            IsSelected = isSelected;
        }

        public int Index { get; }

        public void ExecuteSelect()
        {
            _onSelect?.Invoke(Index);
        }

        [DataSourceProperty]
        public string Name
        {
            get => _name;
            set
            {
                if (value != _name)
                {
                    _name = value;
                    OnPropertyChangedWithValue(value, nameof(Name));
                }
            }
        }

        [DataSourceProperty]
        public string Description
        {
            get => _description;
            set
            {
                if (value != _description)
                {
                    _description = value;
                    OnPropertyChangedWithValue(value, nameof(Description));
                }
            }
        }

        [DataSourceProperty]
        public string IconPath
        {
            get => _iconPath;
            set
            {
                if (value != _iconPath)
                {
                    _iconPath = value;
                    OnPropertyChangedWithValue(value, nameof(IconPath));
                }
            }
        }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    OnPropertyChangedWithValue(value, nameof(IsSelected));
                }
            }
        }
    }
}
