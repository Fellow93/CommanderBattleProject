using System;
using System.Collections.Generic;
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
            ClassIndex = -1;
        }

        public BattleSideEnum Side { get; }

        public int Index { get; }

        // From the side's culture (CommanderBattleSettings.GetFormationRoles), read when asked: the slot table is
        // built before the server and clients are sure to have the culture options, and both derive the same roles.
        public CommanderRole Role => CommanderBattleSettings.GetFormationRoles(CommanderBattleTeams.GetCulture(Side))[Index];

        // Men at a class of the role's average size (CommanderBattleSettings.GetTroopBudget). Read from the role, like
        // Role itself, for the same reason.
        public int Budget => CommanderBattleSettings.GetTroopBudget(Role);

        private int _troopCount = -1;

        // Men the formation fields this round. Set by the server from the slot's class when the round's armies
        // are set up, and frozen once they spawn; clients get it with the round's layout (SpawnLayoutSeed). The
        // budget stands in until the first counts are set.
        public int TroopCount
        {
            get => _troopCount >= 0 ? _troopCount : Budget;
            internal set => _troopCount = value;
        }

        // Player commanding this formation, or null when the AI leads it.
        public NetworkCommunicator Commander { get; set; }

        // Index into MultiplayerClassDivisions.GetMPHeroClasses(side culture): the troops' class.
        public int ClassIndex { get; set; }

        // The weapon setup the formation fields: an index into the class's weapon perks (CommanderRoles.WeaponPerkList).
        // The commander's perk while one leads it; the AI's own pick otherwise, which the AI spawner applies as the
        // perk's alternative equipment (AI formations have no perks of their own).
        public int WeaponIndex { get; set; }

        // A troop type and perks picked on the selection screen that apply when the next round's preparation starts
        // (-1: none). Kept when the commander leaves, so the AI fields the type and weapon setup next round. Clients
        // get it from FormationSlotPendingSelection.
        public int PendingClassIndex { get; internal set; } = -1;

        public int[] PendingPerks { get; internal set; } = new int[CommanderRoles.PerkListCount];

        public bool HasPendingSelection => PendingClassIndex >= 0;

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
            foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
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

        // Every slot, defender slots first (CommanderBattleTeams.Sides).
        public IEnumerable<FormationSlot> AllSlots
        {
            get
            {
                foreach (BattleSideEnum side in CommanderBattleTeams.Sides)
                {
                    foreach (FormationSlot slot in _slots[(int)side])
                    {
                        yield return slot;
                    }
                }
            }
        }

        // Called per peer every tick: walks the arrays rather than AllSlots, which allocates an enumerator.
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
            foreach (FormationSlot slot in AllSlots)
            {
                if (slot.Commander != null)
                {
                    return slot.Commander;
                }
            }
            return null;
        }

        public void Set(BattleSideEnum side, int index, NetworkCommunicator commander, int classIndex, int weaponIndex)
        {
            FormationSlot slot = GetSlot(side, index);
            if (slot == null)
            {
                return;
            }
            slot.Commander = commander;
            slot.ClassIndex = classIndex;
            slot.WeaponIndex = weaponIndex;
            OnSlotChanged?.Invoke(slot);
        }

        // The lowest free slot of the role on the side, or null.
        public FormationSlot FindFreeSlot(BattleSideEnum side, CommanderRole role)
        {
            foreach (FormationSlot slot in GetSlots(side))
            {
                if (slot.Commander == null && slot.Role == role)
                {
                    return slot;
                }
            }
            return null;
        }

        public void CountRole(BattleSideEnum side, CommanderRole role, out int free, out int total)
        {
            free = 0;
            total = 0;
            foreach (FormationSlot slot in GetSlots(side))
            {
                if (slot.Role == role)
                {
                    total++;
                    if (slot.Commander == null)
                    {
                        free++;
                    }
                }
            }
        }

        // classIndex -1 clears the slot's pending selection.
        public void SetPendingSelection(BattleSideEnum side, int index, int classIndex, int[] perks)
        {
            FormationSlot slot = GetSlot(side, index);
            if (slot == null)
            {
                return;
            }
            slot.PendingClassIndex = classIndex;
            slot.PendingPerks = new int[CommanderRoles.PerkListCount];
            if (classIndex >= 0 && perks != null)
            {
                Array.Copy(perks, slot.PendingPerks, Math.Min(perks.Length, slot.PendingPerks.Length));
            }
            OnSlotChanged?.Invoke(slot);
        }

        // Every slot's troop count, defender slots first, then attacker slots (the order SpawnLayoutSeed sends).
        public int[] GetTroopCounts()
        {
            int count = CommanderBattleSettings.FormationCount;
            int[] counts = new int[2 * count];
            for (int i = 0; i < count; i++)
            {
                counts[i] = _slots[(int)BattleSideEnum.Defender][i].TroopCount;
                counts[count + i] = _slots[(int)BattleSideEnum.Attacker][i].TroopCount;
            }
            return counts;
        }

        // Takes counts in GetTroopCounts' order. Returns whether any slot changed.
        public bool SetTroopCounts(int[] counts)
        {
            int count = CommanderBattleSettings.FormationCount;
            if (counts == null || counts.Length != 2 * count)
            {
                return false;
            }
            List<FormationSlot> changed = new List<FormationSlot>();
            for (int i = 0; i < counts.Length; i++)
            {
                FormationSlot slot = _slots[i < count ? (int)BattleSideEnum.Defender : (int)BattleSideEnum.Attacker][i % count];
                if (slot.TroopCount != counts[i])
                {
                    slot.TroopCount = counts[i];
                    changed.Add(slot);
                }
            }
            // Listeners see the whole new set, never half of it.
            foreach (FormationSlot slot in changed)
            {
                OnSlotChanged?.Invoke(slot);
            }
            return changed.Count > 0;
        }

        public int GetTotalTroopCount(BattleSideEnum side)
        {
            int total = 0;
            foreach (FormationSlot slot in GetSlots(side))
            {
                total += slot.TroopCount;
            }
            return total;
        }
    }
}
