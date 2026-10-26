using System;
using System.Collections.Generic;
using System.Linq;
using CommanderBattle.Network;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle.View
{
    // Data for the formation list: every formation of the player's side, who leads it and with what.
    public class FormationSelectionVM : ViewModel
    {
        private readonly Action<int> _onTake;

        private readonly Action _onTroops;

        private readonly Action _onClose;

        private MBBindingList<FormationSlotVM> _formations;

        private string _title;

        private string _hint;

        private string _closeText;

        private string _summary;

        // The rows' slot indexes, grouped by role (infantry, ranged, cavalry, horse archers, the general last), then by
        // formation number. Rebuilt when the side's culture changes, since the culture sets the roles and whether it
        // fields a general.
        private int[] _rowSlots;

        private string _rowCulture;

        public FormationSelectionVM(Action<int> onTake, Action onTroops, Action onClose)
        {
            _onTake = onTake;
            _onTroops = onTroops;
            _onClose = onClose;
            Formations = new MBBindingList<FormationSlotVM>();
            CloseText = "Done";
            Hint = "Click a formation to lead it; \"Troops\" picks its weight and weapons. Lighter troops field more men. The AI leads every formation without a commander. N reopens this list.";
        }

        public void Refresh(FormationSlotTable slots, BattleSideEnum side, NetworkCommunicator me, SideAIStatus aiStatus)
        {
            if (aiStatus != null && aiStatus.Side != side)
            {
                aiStatus = null;
            }
            BasicCultureObject culture = CommanderBattleTeams.GetCulture(side);
            FormationSlot[] sideSlots = slots.GetSlots(side);
            Title = $"{culture?.Name} · {CommanderBattleSideBannerView.GetSideName(side)}";
            // The classes set the formations' sizes, so the side's total changes with its picks.
            Summary = $"{DescribeSplit(sideSlots)} · {slots.GetTotalTroopCount(side)} men";
            string cultureId = culture?.StringId ?? "";
            if (_rowSlots == null || _rowCulture != cultureId)
            {
                _rowCulture = cultureId;
                _rowSlots = sideSlots.Where(slot => slot.IsFielded).OrderBy(slot => (int)slot.Role).ThenBy(slot => slot.Index).Select(slot => slot.Index).ToArray();
                Formations.Clear();
                foreach (int slotIndex in _rowSlots)
                {
                    Formations.Add(new FormationSlotVM(slotIndex, _onTake, _onTroops));
                }
            }
            for (int i = 0; i < _rowSlots.Length; i++)
            {
                FormationSlot slot = sideSlots[_rowSlots[i]];
                Formations[i].Refresh(slot, culture, me, GetBehavior(aiStatus, slot.Index));
            }
        }

        // "3 infantry · 2 ranged · 3 cavalry · a general".
        private static string DescribeSplit(FormationSlot[] slots)
        {
            List<string> parts = new List<string>();
            foreach (CommanderRole role in CommanderRoles.All)
            {
                int count = slots.Count(slot => slot.Role == role && slot.IsFielded);
                if (count > 0)
                {
                    parts.Add(role == CommanderRole.General ? "a general" : $"{count} {CommanderRoles.GetName(role).ToLowerInvariant()}");
                }
            }
            return string.Join(" · ", parts);
        }

        // Slot i leads formation (FormationClass)i, which the server reports by that index.
        private static string GetBehavior(SideAIStatus aiStatus, int formationIndex)
        {
            if (aiStatus == null)
            {
                return null;
            }
            foreach (AIFormationStatus formation in aiStatus.Formations)
            {
                if (formation.FormationIndex == formationIndex && !formation.IsPlayerLed)
                {
                    return formation.Leader == AIFormationLeader.General ? "led by the general" : formation.Behavior;
                }
            }
            return null;
        }

        public void ExecuteClose()
        {
            _onClose?.Invoke();
        }

        [DataSourceProperty]
        public MBBindingList<FormationSlotVM> Formations
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
        public string Summary
        {
            get => _summary;
            set
            {
                if (value != _summary)
                {
                    _summary = value;
                    OnPropertyChangedWithValue(value, nameof(Summary));
                }
            }
        }
    }

    public class FormationSlotVM : ViewModel
    {
        private readonly int _slotIndex;

        private readonly Action<int> _onTake;

        private readonly Action _onTroops;

        private string _name;

        private string _troopLine;

        private string _detailLine;

        private string _pendingLine;

        private string _iconType;

        private string _commander;

        private bool _isMine;

        private bool _canTake;

        private string _troopsButtonText;

        public FormationSlotVM(int slotIndex, Action<int> onTake, Action onTroops)
        {
            _slotIndex = slotIndex;
            _onTake = onTake;
            _onTroops = onTroops;
            TroopsButtonText = "Troops";
        }

        public void Refresh(FormationSlot slot, BasicCultureObject culture, NetworkCommunicator me, string aiBehavior)
        {
            // Named by its formation number, as the order bar numbers it; the general by name.
            Name = slot.DisplayName;
            MultiplayerClassDivisions.MPHeroClass heroClass = CommanderRoles.GetClass(culture, slot.ClassIndex);
            // The class's icon in mpclassdivisions.xml is a Compass.Type.Icons layer, as on the troop screen.
            IconType = heroClass?.IconType.ToString() ?? CommanderRoles.GetIcon(culture, slot.Role);
            TroopLine = heroClass?.TroopName.ToString() ?? "?";
            string weight = heroClass != null ? CommanderRoles.GetWeightLabel(heroClass) : "";
            string weapons = heroClass != null ? CommanderRoles.GetWeaponName(heroClass, slot.WeaponIndex) : "";
            // The general's riders follow him: the order bar can't reach formation 8.
            string size = slot.IsGeneral ? $"{slot.TroopCount} bodyguards follow him" : $"{slot.TroopCount} men";
            DetailLine = string.Join(" · ", new[] { weight, weapons, size }.Where(part => part.Length > 0));
            // A pick made after the armies spawned shows until the next round fields it.
            PendingLine = slot.HasPendingSelection ? $"Next round: {Describe(culture, slot.PendingClassIndex, slot.PendingPerks[CommanderRoles.WeaponPerkList])}" : "";
            IsMine = slot.Commander != null && slot.Commander == me;
            Commander = slot.Commander == null
                ? (string.IsNullOrEmpty(aiBehavior) ? "AI" : "AI · " + aiBehavior)
                : (IsMine ? "You" : slot.Commander.UserName);
            CanTake = slot.Commander == null;
        }

        // "Sergeant, Spear and shield".
        private static string Describe(BasicCultureObject culture, int classIndex, int weaponIndex)
        {
            MultiplayerClassDivisions.MPHeroClass heroClass = CommanderRoles.GetClass(culture, classIndex);
            if (heroClass == null)
            {
                return "?";
            }
            string weapons = CommanderRoles.GetWeaponName(heroClass, weaponIndex);
            return weapons.Length > 0 ? $"{heroClass.TroopName}, {weapons}" : heroClass.TroopName.ToString();
        }

        public void ExecuteTake()
        {
            if (CanTake)
            {
                _onTake?.Invoke(_slotIndex);
            }
        }

        public void ExecuteTroops()
        {
            if (IsMine)
            {
                _onTroops?.Invoke();
            }
        }

        [DataSourceProperty]
        public string TroopsButtonText
        {
            get => _troopsButtonText;
            set
            {
                if (value != _troopsButtonText)
                {
                    _troopsButtonText = value;
                    OnPropertyChangedWithValue(value, nameof(TroopsButtonText));
                }
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

        // The troop type's name.
        [DataSourceProperty]
        public string TroopLine
        {
            get => _troopLine;
            set
            {
                if (value != _troopLine)
                {
                    _troopLine = value;
                    OnPropertyChangedWithValue(value, nameof(TroopLine));
                }
            }
        }

        // "Heavy · Spear and shield · 62 men".
        [DataSourceProperty]
        public string DetailLine
        {
            get => _detailLine;
            set
            {
                if (value != _detailLine)
                {
                    _detailLine = value;
                    OnPropertyChangedWithValue(value, nameof(DetailLine));
                }
            }
        }

        // "Next round: ..." or empty.
        [DataSourceProperty]
        public string PendingLine
        {
            get => _pendingLine;
            set
            {
                if (value != _pendingLine)
                {
                    _pendingLine = value;
                    OnPropertyChangedWithValue(value, nameof(PendingLine));
                }
            }
        }

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
        public string Commander
        {
            get => _commander;
            set
            {
                if (value != _commander)
                {
                    _commander = value;
                    OnPropertyChangedWithValue(value, nameof(Commander));
                }
            }
        }

        [DataSourceProperty]
        public bool IsMine
        {
            get => _isMine;
            set
            {
                if (value != _isMine)
                {
                    _isMine = value;
                    OnPropertyChangedWithValue(value, nameof(IsMine));
                }
            }
        }

        [DataSourceProperty]
        public bool CanTake
        {
            get => _canTake;
            set
            {
                if (value != _canTake)
                {
                    _canTake = value;
                    OnPropertyChangedWithValue(value, nameof(CanTake));
                }
            }
        }
    }
}
