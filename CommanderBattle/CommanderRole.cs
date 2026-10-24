using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    public enum CommanderRole
    {
        Infantry,
        Ranged,
        Cavalry
    }

    // Maps the stock multiplayer class groups (mpclassdivisions.xml) onto formation roles.
    public static class CommanderRoles
    {
        // Class groups shown in the stock class picker, by their FormationClass.
        public static readonly FormationClass[] ClassGroups =
        {
            FormationClass.Infantry,
            FormationClass.Ranged,
            FormationClass.Cavalry,
            FormationClass.HorseArcher
        };

        public static CommanderRole? GetRole(MultiplayerClassDivisions.MPHeroClass heroClass)
        {
            switch (heroClass?.ClassGroup?.StringId)
            {
                case "Infantry":
                    return CommanderRole.Infantry;
                case "Ranged":
                    return CommanderRole.Ranged;
                case "Cavalry":
                case "HorseArcher":
                    return CommanderRole.Cavalry;
                default:
                    return null;
            }
        }

        public static CommanderRole GetRole(FormationClass classGroup)
        {
            switch (classGroup)
            {
                case FormationClass.Ranged:
                    return CommanderRole.Ranged;
                case FormationClass.Cavalry:
                case FormationClass.HorseArcher:
                    return CommanderRole.Cavalry;
                default:
                    return CommanderRole.Infantry;
            }
        }

        public static string GetName(CommanderRole role)
        {
            switch (role)
            {
                case CommanderRole.Ranged:
                    return "Ranged";
                case CommanderRole.Cavalry:
                    return "Cavalry";
                default:
                    return "Infantry";
            }
        }

        // Indexes into MultiplayerClassDivisions.GetMPHeroClasses(culture) (the same index as
        // MissionPeer.SelectedTroopIndex) of the culture's classes for this role.
        public static List<int> GetClassIndexes(BasicCultureObject culture, CommanderRole role)
        {
            List<MultiplayerClassDivisions.MPHeroClass> classes = MultiplayerClassDivisions.GetMPHeroClasses(culture).ToList();
            List<int> indexes = new List<int>();
            for (int i = 0; i < classes.Count; i++)
            {
                if (GetRole(classes[i]) == role)
                {
                    indexes.Add(i);
                }
            }
            return indexes;
        }

        public static MultiplayerClassDivisions.MPHeroClass GetClass(BasicCultureObject culture, int classIndex)
        {
            List<MultiplayerClassDivisions.MPHeroClass> classes = MultiplayerClassDivisions.GetMPHeroClasses(culture).ToList();
            return classIndex >= 0 && classIndex < classes.Count ? classes[classIndex] : null;
        }
    }
}
