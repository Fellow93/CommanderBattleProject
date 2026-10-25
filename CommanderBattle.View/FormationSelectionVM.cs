using System;
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

        private readonly Action _onClose;

        private MBBindingList<FormationSlotVM> _formations;

        private string _title;

        private string _hint;

        private string _closeText;

        public FormationSelectionVM(Action<int> onTake, Action onClose)
        {
            _onTake = onTake;
            _onClose = onClose;
            Formations = new MBBindingList<FormationSlotVM>();
            CloseText = "Done";
            Hint = "Pick a formation to lead. Formations without a commander are led by the AI. Press N to reopen this list.";
        }

        public void Refresh(FormationSlotTable slots, BattleSideEnum side, NetworkCommunicator me, SideAIStatus aiStatus)
        {
            if (aiStatus != null && aiStatus.Side != side)
            {
                aiStatus = null;
            }
            BasicCultureObject culture = CommanderBattleTeams.GetCulture(side);
            Title = $"{CommanderBattleSideBannerView.GetSideName(side)}: {culture?.Name} formations";
            FormationSlot[] sideSlots = slots.GetSlots(side);
            if (Formations.Count != sideSlots.Length)
            {
                Formations.Clear();
                foreach (FormationSlot slot in sideSlots)
                {
                    Formations.Add(new FormationSlotVM(slot.Index, _onTake));
                }
            }
            for (int i = 0; i < sideSlots.Length; i++)
            {
                Formations[i].Refresh(sideSlots[i], culture, me, GetBehavior(aiStatus, sideSlots[i].Index));
            }
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
                    return formation.Behavior;
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
    }

    public class FormationSlotVM : ViewModel
    {
        private readonly int _slotIndex;

        private readonly Action<int> _onTake;

        private string _name;

        private string _troops;

        private string _commander;

        private bool _isMine;

        private bool _canTake;

        public FormationSlotVM(int slotIndex, Action<int> onTake)
        {
            _slotIndex = slotIndex;
            _onTake = onTake;
        }

        public void Refresh(FormationSlot slot, BasicCultureObject culture, NetworkCommunicator me, string aiBehavior)
        {
            Name = $"{CommanderRoles.GetName(slot.Role)} {slot.Index + 1}";
            string troopName = CommanderRoles.GetClass(culture, slot.ClassIndex)?.TroopName?.ToString() ?? "?";
            Troops = $"{slot.TroopCount} x {troopName}";
            IsMine = slot.Commander != null && slot.Commander == me;
            Commander = slot.Commander == null
                ? (string.IsNullOrEmpty(aiBehavior) ? "AI" : "AI: " + aiBehavior)
                : (IsMine ? "You" : slot.Commander.UserName);
            CanTake = slot.Commander == null;
        }

        public void ExecuteTake()
        {
            if (CanTake)
            {
                _onTake?.Invoke(_slotIndex);
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
        public string Troops
        {
            get => _troops;
            set
            {
                if (value != _troops)
                {
                    _troops = value;
                    OnPropertyChangedWithValue(value, nameof(Troops));
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
