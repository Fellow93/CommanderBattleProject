using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle.View
{
    // A formation's name as the order bar's cards show it: its troops, then whose it is when a player leads it,
    // "Cataphracts (Fellow)". The same wording as the Alt labels (CommanderBattleAllyPlayerMarkersView.GetFormationLabel),
    // which keep their own copy for now; the two could share this.
    public static class FormationNames
    {
        // The troops alone for an AI formation.
        public static string GetLabel(FormationSlot slot, BasicCultureObject culture)
        {
            string troops = GetTroopName(slot, culture);
            if (slot.Commander == null)
            {
                return troops;
            }
            string name = slot.Commander.GetComponent<MissionPeer>()?.DisplayedName ?? slot.Commander.UserName;
            return $"{troops} ({name})";
        }

        // The troops' own name, as on the troop screen; the role until the class is known.
        public static string GetTroopName(FormationSlot slot, BasicCultureObject culture)
        {
            MultiplayerClassDivisions.MPHeroClass heroClass = culture != null ? CommanderRoles.GetClass(culture, slot.ClassIndex) : null;
            return heroClass?.TroopName.ToString() ?? CommanderRoles.GetName(slot.Role);
        }
    }
}
