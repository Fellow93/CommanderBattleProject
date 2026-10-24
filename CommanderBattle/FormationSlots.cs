using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    public class FormationSlot
    {
        public FormationSlot(BattleSideEnum side, int index)
        {
            Side = side;
            Index = index;
            Role = CommanderBattleSettings.FormationRoles[index];
            TroopCount = CommanderBattleSettings.GetTroopCount(index);
            ClassIndex = -1;
        }

        public BattleSideEnum Side { get; }

        public int Index { get; }

        public CommanderRole Role { get; }

        public int TroopCount { get; }

        // Player commanding this formation, or null when the AI leads it.
        public NetworkCommunicator Commander { get; set; }

        // Index into MultiplayerClassDivisions.GetMPHeroClasses(side culture): the troops' class.
        public int ClassIndex { get; set; }

        public FormationClass FormationClass => (FormationClass)Index;
    }

    // The two armies' formation slots. Authoritative on the server; clients keep a copy updated by
    // FormationSlotUpdate messages.
    public class FormationSlotTable
    {
        private readonly FormationSlot[][] _slots;

        public FormationSlotTable()
        {
            _slots = new FormationSlot[2][];
            foreach (BattleSideEnum side in new[] { BattleSideEnum.Defender, BattleSideEnum.Attacker })
            {
                _slots[(int)side] = new FormationSlot[CommanderBattleSettings.FormationCount];
                for (int i = 0; i < CommanderBattleSettings.FormationCount; i++)
                {
                    _slots[(int)side][i] = new FormationSlot(side, i);
                }
            }
        }

        public event Action<FormationSlot> OnSlotChanged;

        public FormationSlot[] GetSlots(BattleSideEnum side)
        {
            return side == BattleSideEnum.Attacker || side == BattleSideEnum.Defender ? _slots[(int)side] : Array.Empty<FormationSlot>();
        }

        public FormationSlot GetSlot(BattleSideEnum side, int index)
        {
            FormationSlot[] slots = GetSlots(side);
            return index >= 0 && index < slots.Length ? slots[index] : null;
        }

        public FormationSlot FindSlotOf(NetworkCommunicator commander)
        {
            if (commander == null)
            {
                return null;
            }
            foreach (FormationSlot[] sideSlots in _slots)
            {
                foreach (FormationSlot slot in sideSlots)
                {
                    if (slot.Commander == commander)
                    {
                        return slot;
                    }
                }
            }
            return null;
        }

        public NetworkCommunicator FindAnyCommander()
        {
            foreach (FormationSlot[] sideSlots in _slots)
            {
                foreach (FormationSlot slot in sideSlots)
                {
                    if (slot.Commander != null)
                    {
                        return slot.Commander;
                    }
                }
            }
            return null;
        }

        public void Set(BattleSideEnum side, int index, NetworkCommunicator commander, int classIndex)
        {
            FormationSlot slot = GetSlot(side, index);
            if (slot == null)
            {
                return;
            }
            slot.Commander = commander;
            slot.ClassIndex = classIndex;
            OnSlotChanged?.Invoke(slot);
        }
    }
}
