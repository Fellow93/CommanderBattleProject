using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace CommanderBattle.View
{
    // Data for the banner picker: the game's sigils in a grid, the side's shades, and a preview of the banner the
    // formation's shields will carry. Apply sends the pick (the server checks it) and saves it on this machine.
    public class BannerPickerVM : ViewModel
    {
        private readonly BattleSideEnum _side;

        private readonly int _profileSigil;

        private readonly Action<int, int> _onApply;

        private readonly Action _onClose;

        // -1: the profile banner's sigil.
        private int _sigil;

        private int _shade;

        private MBBindingList<BannerSigilVM> _sigils;

        private MBBindingList<BannerShadeVM> _shades;

        private ImageIdentifierVM _preview;

        private string _title;

        private string _status;

        private string _hint;

        private string _profileText;

        private bool _isProfileSelected;

        private string _shadeTitle;

        private string _applyText;

        private string _cancelText;

        // sigils: the icons to offer (CommanderBanners.GetAvailableSigils, those the UI can draw).
        public BannerPickerVM(BattleSideEnum side, IReadOnlyList<int> sigils, int profileSigil, int sigil, int shade, Action<int, int> onApply, Action onClose)
        {
            _side = side;
            _profileSigil = profileSigil;
            _onApply = onApply;
            _onClose = onClose;
            _sigil = sigil;
            _shade = shade;
            string cultureName = CommanderBattleTeams.GetCulture(side)?.Name?.ToString();
            Title = string.IsNullOrEmpty(cultureName) ? "Your banner" : $"Your banner · {cultureName}";
            ShadeTitle = "Shade";
            ProfileText = "Profile banner";
            ApplyText = "Apply";
            CancelText = "Cancel";
            Hint = $"Your sigil on a shade of your side's colour, on your formation's shields. Kept on this computer for your next battles as {(string.IsNullOrEmpty(cultureName) ? "this culture" : cultureName)}; each culture has its own.";
            Sigils = new MBBindingList<BannerSigilVM>();
            foreach (int id in sigils)
            {
                Sigils.Add(new BannerSigilVM(id, OnSelectSigil));
            }
            Shades = new MBBindingList<BannerShadeVM>();
            IReadOnlyList<int> shades = CommanderBanners.GetShades(side);
            for (int i = 0; i < shades.Count; i++)
            {
                Shades.Add(new BannerShadeVM(i, CommanderBanners.ToColorText(shades[i]), OnSelectShade));
            }
            if (_shade < 0 || _shade >= shades.Count)
            {
                _shade = 0;
            }
            RefreshSelection();
        }

        // Whether a pick sent now applies this round.
        public void RefreshStatus(bool appliesNow)
        {
            Status = appliesNow ? "Applies to this round." : "Your formation is already in the field: applies from the next round.";
        }

        private void OnSelectSigil(BannerSigilVM sigil)
        {
            _sigil = sigil.Id;
            RefreshSelection();
        }

        private void OnSelectShade(BannerShadeVM shade)
        {
            _shade = shade.Index;
            RefreshSelection();
        }

        public void ExecuteUseProfile()
        {
            _sigil = CommanderBanners.SideSigil;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            foreach (BannerSigilVM sigil in Sigils)
            {
                sigil.IsSelected = sigil.Id == _sigil;
            }
            foreach (BannerShadeVM shade in Shades)
            {
                shade.IsSelected = shade.Index == _shade;
            }
            IsProfileSelected = _sigil == CommanderBanners.SideSigil;
            Banner banner = CommanderBanners.Build(_side, _sigil == CommanderBanners.SideSigil ? _profileSigil : _sigil, _shade);
            Preview = banner != null ? new BannerImageIdentifierVM(banner, nineGrid: true) : null;
        }

        public void ExecuteApply()
        {
            _onApply?.Invoke(_sigil, _shade);
        }

        public void ExecuteClose()
        {
            _onClose?.Invoke();
        }

        [DataSourceProperty]
        public MBBindingList<BannerSigilVM> Sigils
        {
            get => _sigils;
            set
            {
                if (value != _sigils)
                {
                    _sigils = value;
                    OnPropertyChangedWithValue(value, nameof(Sigils));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<BannerShadeVM> Shades
        {
            get => _shades;
            set
            {
                if (value != _shades)
                {
                    _shades = value;
                    OnPropertyChangedWithValue(value, nameof(Shades));
                }
            }
        }

        [DataSourceProperty]
        public ImageIdentifierVM Preview
        {
            get => _preview;
            set
            {
                if (value != _preview)
                {
                    _preview = value;
                    OnPropertyChangedWithValue(value, nameof(Preview));
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
        public string ProfileText
        {
            get => _profileText;
            set
            {
                if (value != _profileText)
                {
                    _profileText = value;
                    OnPropertyChangedWithValue(value, nameof(ProfileText));
                }
            }
        }

        [DataSourceProperty]
        public bool IsProfileSelected
        {
            get => _isProfileSelected;
            set
            {
                if (value != _isProfileSelected)
                {
                    _isProfileSelected = value;
                    OnPropertyChangedWithValue(value, nameof(IsProfileSelected));
                }
            }
        }

        [DataSourceProperty]
        public string ShadeTitle
        {
            get => _shadeTitle;
            set
            {
                if (value != _shadeTitle)
                {
                    _shadeTitle = value;
                    OnPropertyChangedWithValue(value, nameof(ShadeTitle));
                }
            }
        }

        [DataSourceProperty]
        public string ApplyText
        {
            get => _applyText;
            set
            {
                if (value != _applyText)
                {
                    _applyText = value;
                    OnPropertyChangedWithValue(value, nameof(ApplyText));
                }
            }
        }

        [DataSourceProperty]
        public string CancelText
        {
            get => _cancelText;
            set
            {
                if (value != _cancelText)
                {
                    _cancelText = value;
                    OnPropertyChangedWithValue(value, nameof(CancelText));
                }
            }
        }
    }

    public class BannerSigilVM : ViewModel
    {
        private readonly Action<BannerSigilVM> _onSelect;

        private bool _isSelected;

        public BannerSigilVM(int id, Action<BannerSigilVM> onSelect)
        {
            Id = id;
            _onSelect = onSelect;
            // The banner icons' UI sprites are named by icon id (ui_bannericons), as in the stock banner editors.
            IconPath = id.ToString();
        }

        public int Id { get; }

        [DataSourceProperty]
        public string IconPath { get; }

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

        public void ExecuteSelect()
        {
            _onSelect?.Invoke(this);
        }
    }

    public class BannerShadeVM : ViewModel
    {
        private readonly Action<BannerShadeVM> _onSelect;

        private bool _isSelected;

        public BannerShadeVM(int index, string colorText, Action<BannerShadeVM> onSelect)
        {
            Index = index;
            ColorText = colorText;
            _onSelect = onSelect;
        }

        public int Index { get; }

        // "#RRGGBBFF".
        [DataSourceProperty]
        public string ColorText { get; }

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

        public void ExecuteSelect()
        {
            _onSelect?.Invoke(this);
        }
    }
}
