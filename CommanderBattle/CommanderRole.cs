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
        Cavalry,
        HorseArcher,
        // The side's general and his mounted bodyguard: one per side, on top of the 8 formations
        // (CommanderBattleSettings.GeneralSlot).
        General
    }

    // Maps the stock multiplayer class groups (mpclassdivisions.xml) onto formation roles.
    public static class CommanderRoles
    {
        // Every role, in the order the screen and the split table list them.
        public static readonly CommanderRole[] All =
        {
            CommanderRole.Infantry,
            CommanderRole.Ranged,
            CommanderRole.Cavalry,
            CommanderRole.HorseArcher,
            CommanderRole.General
        };

        // Class groups shown in the stock class picker, by their FormationClass.
        public static readonly FormationClass[] ClassGroups =
        {
            FormationClass.Infantry,
            FormationClass.Ranged,
            FormationClass.Cavalry,
            FormationClass.HorseArcher
        };

        // A culture whose classes aren't converted yet has no horse archer formations (4/2/2/0), so its stock
        // horse archer class still serves as cavalry. The general is known by his class id, not a class group: his
        // characters are in the stock Cavalry group, because the stock lobby's class filter
        // (MPLobbyClassFilterFactionItemVM) and lobby class restrictions only know the 4 stock groups, and a client
        // with a class in any other group crashes when the multiplayer lobby opens.
        public static CommanderRole? GetRole(MultiplayerClassDivisions.MPHeroClass heroClass)
        {
            if (IsGeneralClass(heroClass))
            {
                return CommanderRole.General;
            }
            switch (heroClass?.ClassGroup?.StringId)
            {
                case "Infantry":
                    return CommanderRole.Infantry;
                case "Ranged":
                    return CommanderRole.Ranged;
                case "Cavalry":
                    return CommanderRole.Cavalry;
                case "HorseArcher":
                    return CommanderBattleSettings.IsConverted(heroClass.Culture) ? CommanderRole.HorseArcher : CommanderRole.Cavalry;
                default:
                    return null;
            }
        }

        // The culture XMLs name each culture's general class cb_general_<culture>.
        public const string GeneralClassPrefix = "cb_general_";

        public static bool IsGeneralClass(MultiplayerClassDivisions.MPHeroClass heroClass)
        {
            return heroClass?.StringId != null && heroClass.StringId.StartsWith(GeneralClassPrefix, System.StringComparison.Ordinal);
        }

        public static CommanderRole GetRole(FormationClass classGroup, BasicCultureObject culture)
        {
            switch (classGroup)
            {
                case FormationClass.Ranged:
                    return CommanderRole.Ranged;
                case FormationClass.Cavalry:
                    return CommanderRole.Cavalry;
                case FormationClass.HorseArcher:
                    return CommanderBattleSettings.IsConverted(culture) ? CommanderRole.HorseArcher : CommanderRole.Cavalry;
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
                case CommanderRole.HorseArcher:
                    return "Horse Archers";
                case CommanderRole.General:
                    return "General";
                default:
                    return "Infantry";
            }
        }

        // Whether the culture has a general's class. A culture without one (another module's, or XML that failed to
        // load) fields no general: its general slot stays empty. Asked per slot every tick (troop counts, slot lists),
        // so it is cached with the classes.
        public static bool HasGeneral(BasicCultureObject culture)
        {
            if (culture == null)
            {
                return false;
            }
            if (!GeneralCache.TryGetValue(culture, out bool hasGeneral))
            {
                hasGeneral = GetClassIndexes(culture, CommanderRole.General).Count > 0;
                GeneralCache[culture] = hasGeneral;
            }
            return hasGeneral;
        }

        // A Compass.Type.Icons layer for the role: the icon of the culture's heaviest class of it, so the role card
        // shows the culture's own troops (crossbows for Vlandia, bows elsewhere), or a stock icon if it has none.
        public static string GetIcon(BasicCultureObject culture, CommanderRole role)
        {
            List<int> weights = GetWeightClassIndexes(culture, role);
            if (weights.Count > 0)
            {
                MultiplayerClassDivisions.MPHeroClass heaviest = GetClass(culture, weights[weights.Count - 1]);
                if (heaviest != null && heaviest.IconType != TargetIconType.None)
                {
                    return heaviest.IconType.ToString();
                }
            }
            switch (role)
            {
                case CommanderRole.Ranged:
                    return nameof(TargetIconType.Archer_Heavy);
                case CommanderRole.Cavalry:
                    return nameof(TargetIconType.Cavalry_Heavy);
                case CommanderRole.HorseArcher:
                    return nameof(TargetIconType.HorseArcher_Heavy);
                case CommanderRole.General:
                    return nameof(TargetIconType.Special_RoyalCavalry);
                default:
                    return nameof(TargetIconType.Infantry_Heavy);
            }
        }

        // GetMPHeroClasses filters every culture's classes through LINQ on each call, and the troop counts, the
        // commander sync and the scoreboard ask every tick. The classes are loaded with the game; the cache is
        // dropped at each mission start (ClearClassCache) in case they were loaded again.
        private static readonly Dictionary<BasicCultureObject, List<MultiplayerClassDivisions.MPHeroClass>> ClassCache = new Dictionary<BasicCultureObject, List<MultiplayerClassDivisions.MPHeroClass>>();

        private static readonly Dictionary<BasicCultureObject, bool> GeneralCache = new Dictionary<BasicCultureObject, bool>();

        // The culture's classes, in GetMPHeroClasses' order: index i is MissionPeer.SelectedTroopIndex i.
        public static IReadOnlyList<MultiplayerClassDivisions.MPHeroClass> GetClasses(BasicCultureObject culture)
        {
            if (culture == null)
            {
                return MultiplayerClassDivisions.GetMPHeroClasses(culture).ToList();
            }
            if (!ClassCache.TryGetValue(culture, out List<MultiplayerClassDivisions.MPHeroClass> classes))
            {
                classes = MultiplayerClassDivisions.GetMPHeroClasses(culture).ToList();
                ClassCache[culture] = classes;
            }
            return classes;
        }

        public static void ClearClassCache()
        {
            ClassCache.Clear();
            GeneralCache.Clear();
        }

        // Indexes into GetClasses(culture) (the same index as MissionPeer.SelectedTroopIndex) of the culture's
        // classes for this role.
        public static List<int> GetClassIndexes(BasicCultureObject culture, CommanderRole role)
        {
            IReadOnlyList<MultiplayerClassDivisions.MPHeroClass> classes = GetClasses(culture);
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

        // --- Weights: each class of a role is one weight ---

        // The role's classes from light to heavy: the one fielding the most men (highest multiplier) first, ties by
        // class index so every machine sorts alike.
        public static List<int> GetWeightClassIndexes(BasicCultureObject culture, CommanderRole role)
        {
            IReadOnlyList<MultiplayerClassDivisions.MPHeroClass> classes = GetClasses(culture);
            List<int> indexes = GetClassIndexes(culture, role);
            indexes.Sort((a, b) =>
            {
                int byMultiplier = classes[b].TroopMultiplier.CompareTo(classes[a].TroopMultiplier);
                return byMultiplier != 0 ? byMultiplier : a.CompareTo(b);
            });
            return indexes;
        }

        // Multiplier bands per role (CommanderRole order): at or above the first a class is light, at or above the
        // second medium, below it heavy. Every culture's XML sets its weights on one scale (infantry 0.85 / 0.65 /
        // 0.50, ranged 0.68 / 0.46, cavalry 0.46 / 0.38 / 0.30, horse archers 0.43 / 0.32), so the label follows the
        // gear tier even when a role has only two weights: Khuzait's light and medium infantry, Battania's light and
        // medium cavalry. Ranking by place alone called a role's second class heavy. The general has a single class,
        // so no weight step; its row only keeps the table one per role.
        private static readonly float[][] WeightBands =
        {
            new[] { 0.75f, 0.58f },
            new[] { 0.60f, 0.52f },
            new[] { 0.42f, 0.34f },
            new[] { 0.40f, 0.36f },
            new[] { 0f, 0f }
        };

        // "Light", "Medium" or "Heavy" from the class's multiplier band; a role with a single class has no weight
        // step ("").
        public static string GetWeightLabel(MultiplayerClassDivisions.MPHeroClass heroClass)
        {
            CommanderRole? role = GetRole(heroClass);
            if (!role.HasValue || GetClassIndexes(heroClass.Culture, role.Value).Count < 2)
            {
                return "";
            }
            float[] bands = WeightBands[(int)role.Value];
            if (heroClass.TroopMultiplier >= bands[0])
            {
                return "Light";
            }
            return heroClass.TroopMultiplier >= bands[1] ? "Medium" : "Heavy";
        }

        // The average stock Captain troop count (MPPerkObject.GetTroopCount, no perks) of the culture's classes of
        // this role: the class size a formation's budget is measured in. 0 if the culture has none.
        public static float GetAverageStockTroopCount(BasicCultureObject culture, CommanderRole role, int botsPerFormation)
        {
            int sum = 0;
            int count = 0;
            foreach (MultiplayerClassDivisions.MPHeroClass heroClass in GetClasses(culture))
            {
                if (GetRole(heroClass) == role)
                {
                    sum += MPPerkObject.GetTroopCount(heroClass, botsPerFormation, null);
                    count++;
                }
            }
            return count > 0 ? (float)sum / count : 0f;
        }

        public static MultiplayerClassDivisions.MPHeroClass GetClass(BasicCultureObject culture, int classIndex)
        {
            IReadOnlyList<MultiplayerClassDivisions.MPHeroClass> classes = GetClasses(culture);
            return classIndex >= 0 && classIndex < classes.Count ? classes[classIndex] : null;
        }

        // --- Troop counts and perks: one formula for the server's counts and the selection screen's preview ---

        // A peer has 3 perk lists per class (MissionPeer.Perks). Commander Battle's classes use only the first.
        public const int PerkListCount = 3;

        // The weapon setups: perk list 0 in code, perk_list="1" in the XML. Each is an AlternativeEquipmentOnSpawn
        // set on the class's own armour; index 0 is the class's own loadout.
        public const int WeaponPerkList = 0;

        // The weapon setups a class offers (index = the perk index a peer selects in WeaponPerkList).
        public static List<IReadOnlyPerkObject> GetWeaponSetups(MultiplayerClassDivisions.MPHeroClass heroClass)
        {
            return GetPerkOptions(heroClass, WeaponPerkList);
        }

        // The perk indexes of one weapon setup, as RequestTroopSelection and MissionPeer take them.
        public static int[] MakePerks(int weaponIndex)
        {
            int[] perks = new int[PerkListCount];
            perks[WeaponPerkList] = weaponIndex;
            return perks;
        }

        public static string GetWeaponName(MultiplayerClassDivisions.MPHeroClass heroClass, int weaponIndex)
        {
            List<IReadOnlyPerkObject> setups = GetWeaponSetups(heroClass);
            return weaponIndex >= 0 && weaponIndex < setups.Count ? setups[weaponIndex].Name.ToString() : "";
        }

        // The class's stock Captain troop count for the baseline bot count, its perks' extra troops included
        // (MPPerkObject.GetTroopCount: ceil(100 x multiplier) + any TroopCountOnSpawn). perks may be null.
        public static int GetStockTroopCount(MultiplayerClassDivisions.MPHeroClass heroClass, MPPerkObject.MPOnSpawnPerkHandler perks)
        {
            return heroClass != null ? MPPerkObject.GetTroopCount(heroClass, CommanderBattleSettings.BotsPerFormationBaseline, perks) : 0;
        }

        // A formation's budget buys men by its class: men = budget x stock count / the role's average stock count,
        // rounded, 1..MaxTroopsPerFormation. Without a class or reference the formation fields its budget.
        public static int GetTroopCount(int budget, int stockCount, float roleReference)
        {
            int troops = stockCount > 0 && roleReference > 0f ? (int)(budget * stockCount / roleReference + 0.5f) : budget;
            return troops < 1 ? 1 : (troops > CommanderBattleSettings.MaxTroopsPerFormation ? CommanderBattleSettings.MaxTroopsPerFormation : troops);
        }

        // The perks a class offers in each list for this game mode (index = the perk index a peer selects).
        public static List<IReadOnlyPerkObject> GetPerkOptions(MultiplayerClassDivisions.MPHeroClass heroClass, int listIndex)
        {
            return heroClass != null ? heroClass.GetAllAvailablePerksForListIndex(listIndex) : new List<IReadOnlyPerkObject>();
        }

        // Whether perkIndexes (one per list) are valid picks for the class: within each list's options, or 0 for a
        // list without options (MissionPeer.RefreshSelectedPerks skips those).
        public static bool ArePerksValid(MultiplayerClassDivisions.MPHeroClass heroClass, int[] perkIndexes)
        {
            if (heroClass == null || perkIndexes == null || perkIndexes.Length != PerkListCount)
            {
                return false;
            }
            for (int i = 0; i < PerkListCount; i++)
            {
                int count = GetPerkOptions(heroClass, i).Count;
                if (perkIndexes[i] < 0 || (count == 0 ? perkIndexes[i] != 0 : perkIndexes[i] >= count))
                {
                    return false;
                }
            }
            return true;
        }

        // The perk objects perkIndexes pick, in list order, as MissionPeer.RefreshSelectedPerks would (an index out of
        // range falls back to the list's first option).
        public static List<IReadOnlyPerkObject> GetSelectedPerks(MultiplayerClassDivisions.MPHeroClass heroClass, int[] perkIndexes)
        {
            List<IReadOnlyPerkObject> selected = new List<IReadOnlyPerkObject>();
            for (int i = 0; i < PerkListCount; i++)
            {
                List<IReadOnlyPerkObject> options = GetPerkOptions(heroClass, i);
                if (options.Count == 0)
                {
                    continue;
                }
                int index = perkIndexes != null && i < perkIndexes.Length ? perkIndexes[i] : 0;
                selected.Add(options[index >= 0 && index < options.Count ? index : 0]);
            }
            return selected;
        }

        // Men a formation of this budget would field with this class and these perks (the selection screen's
        // preview), by the same rule as the server's ComputeTroopCounts, measured within the class's own role.
        public static int PreviewTroopCount(BasicCultureObject culture, int budget, MultiplayerClassDivisions.MPHeroClass heroClass, int[] perkIndexes)
        {
            CommanderRole? role = GetRole(heroClass);
            if (!role.HasValue)
            {
                return budget;
            }
            int stockCount = GetStockTroopCount(heroClass, MPPerkObject.GetOnSpawnPerkHandler(GetSelectedPerks(heroClass, perkIndexes)));
            return GetTroopCount(budget, stockCount, GetAverageStockTroopCount(culture, role.Value, CommanderBattleSettings.BotsPerFormationBaseline));
        }

        // Logs every culture's classes once a mission opens: a bad node in a class XML silently drops the rest of
        // that file (MBObjectManager.LoadXML swallows the exception), and the count shows it.
        public static void LogClasses()
        {
            if (MultiplayerClassDivisions.AvailableCultures == null)
            {
                return;
            }
            foreach (BasicCultureObject culture in MultiplayerClassDivisions.AvailableCultures)
            {
                IReadOnlyList<MultiplayerClassDivisions.MPHeroClass> classes = GetClasses(culture);
                List<string> entries = new List<string>();
                for (int i = 0; i < classes.Count; i++)
                {
                    CommanderRole? role = GetRole(classes[i]);
                    string weight = GetWeightLabel(classes[i]);
                    entries.Add($"{i} {classes[i].StringId} ({(role.HasValue ? GetName(role.Value) : "no role")}{(weight.Length > 0 ? " " + weight.ToLowerInvariant() : "")}, x{classes[i].TroopMultiplier:0.00}, {GetWeaponSetups(classes[i]).Count} weapon setups)");
                }
                string warning = classes.Count > MissionPeer.MaxNumberOfTroopTypesPerCulture ? $" - more than {MissionPeer.MaxNumberOfTroopTypesPerCulture}, the troop index can't reach the rest" : "";
                string converted = CommanderBattleSettings.IsConverted(culture) ? "" : " (not converted: stock classes, 4/2/2/0)";
                CommanderBattleLog.Detail($"Classes for {culture.StringId}{converted}: {classes.Count}{warning}: {string.Join(", ", entries)}");
                if (warning.Length > 0)
                {
                    CommanderBattleLog.Write($"Troop classes for {culture.StringId}: {classes.Count}{warning}");
                }
            }
        }
    }
}
