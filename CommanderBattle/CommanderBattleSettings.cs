using System.Collections.Generic;
using TaleWorlds.Core;

namespace CommanderBattle
{
    public static class CommanderBattleSettings
    {
        public const string GameModeName = "CommanderBattle";

        public const string MissionName = "MultiplayerCommanderBattle";

        // A formation's troop budget: the men it fields with a class of its role's average size. Every formation is
        // led by a player who took command of it, or by the AI when nobody did. Its class sets how many men the budget
        // buys (MissionMultiplayerCommanderBattle.ComputeTroopCounts). Mounted formations get less: every rider brings
        // a horse, a second agent for the server to simulate, and a full army of ~1,300 agents dropped the server
        // well below its tick rate in a fight. A side lands around 400 men.
        public const int FootFormationBudget = 54;

        public const int MountedFormationBudget = 44;

        // Slot i is formation (FormationClass)i of the side's team; a Team has 8 regular formations.
        public const int FormationCount = 8;

        // Each culture's split of the 8 formations: infantry, ranged, cavalry, horse archers (CommanderRoles.All
        // order). Only a converted culture uses its split: its XML has Commander Battle's classes, one per weight,
        // horse archers included. The others keep FallbackSplit, which their stock classes fill (their horse archer
        // class counts as cavalry, CommanderRoles.GetRole).
        private static readonly Dictionary<string, int[]> CultureSplits = new Dictionary<string, int[]>
        {
            { "vlandia", new[] { 3, 2, 3, 0 } },
            { "empire", new[] { 3, 2, 2, 1 } },
            { "khuzait", new[] { 2, 2, 2, 2 } },
            { "aserai", new[] { 3, 3, 1, 1 } },
            { "sturgia", new[] { 5, 1, 2, 0 } },
            { "battania", new[] { 5, 2, 1, 0 } }
        };

        // Cultures whose cb_mpclassdivisions_<culture>.xml is in and registered in SubModule.xml.
        private static readonly HashSet<string> ConvertedCultures = new HashSet<string> { "vlandia", "empire", "khuzait", "aserai", "sturgia", "battania" };

        private static readonly int[] FallbackSplit = { 4, 2, 2, 0 };

        // The formation indexes each role prefers, so the order bar's formation names mostly match their troops
        // (FormationClass: 0 Infantry, 1 Ranged, 2 Cavalry, 3 HorseArcher, 4 Skirmisher, 5 HeavyInfantry,
        // 6 LightCavalry, 7 HeavyCavalry). A role with more formations than preferred indexes gets the lowest free
        // ones left, roles taken in CommanderRoles.All order.
        private static readonly int[][] PreferredIndexes =
        {
            new[] { 0, 5 },
            new[] { 1, 4 },
            new[] { 2, 6, 7 },
            new[] { 3 }
        };

        private static readonly Dictionary<string, CommanderRole[]> RolesByCulture = new Dictionary<string, CommanderRole[]>();

        public static bool IsConverted(BasicCultureObject culture)
        {
            return culture != null && ConvertedCultures.Contains(culture.StringId);
        }

        // How many formations of each role (CommanderRoles.All order) the culture fields.
        public static int[] GetSplit(BasicCultureObject culture)
        {
            return IsConverted(culture) && CultureSplits.TryGetValue(culture.StringId, out int[] split) ? split : FallbackSplit;
        }

        // The role of each of the side's 8 formation slots, from its culture alone, so the server and every client
        // build the same slots and layout (both read CultureTeam1/2). Cached per culture: it never changes.
        public static CommanderRole[] GetFormationRoles(BasicCultureObject culture)
        {
            string key = culture?.StringId ?? "";
            if (!RolesByCulture.TryGetValue(key, out CommanderRole[] roles))
            {
                roles = BuildFormationRoles(GetSplit(culture));
                RolesByCulture[key] = roles;
            }
            return roles;
        }

        private static CommanderRole[] BuildFormationRoles(int[] split)
        {
            CommanderRole?[] slots = new CommanderRole?[FormationCount];
            int[] placed = new int[split.Length];
            for (int r = 0; r < split.Length; r++)
            {
                foreach (int index in PreferredIndexes[r])
                {
                    if (placed[r] < split[r] && slots[index] == null)
                    {
                        slots[index] = CommanderRoles.All[r];
                        placed[r]++;
                    }
                }
            }
            for (int r = 0; r < split.Length; r++)
            {
                for (int index = 0; index < FormationCount && placed[r] < split[r]; index++)
                {
                    if (slots[index] == null)
                    {
                        slots[index] = CommanderRoles.All[r];
                        placed[r]++;
                    }
                }
            }
            CommanderRole[] roles = new CommanderRole[FormationCount];
            for (int i = 0; i < FormationCount; i++)
            {
                // A split short of 8 would leave a slot empty; it fights as infantry.
                roles[i] = slots[i] ?? CommanderRole.Infantry;
            }
            return roles;
        }

        // "3 infantry, 2 ranged, 3 cavalry (slots: 0 Infantry, ...)", for the log.
        public static string DescribeSplit(BasicCultureObject culture)
        {
            int[] split = GetSplit(culture);
            List<string> counts = new List<string>();
            for (int r = 0; r < split.Length; r++)
            {
                if (split[r] > 0)
                {
                    counts.Add($"{split[r]} {CommanderRoles.GetName(CommanderRoles.All[r]).ToLowerInvariant()}");
                }
            }
            CommanderRole[] roles = GetFormationRoles(culture);
            List<string> slots = new List<string>();
            for (int i = 0; i < roles.Length; i++)
            {
                slots.Add($"{i} {roles[i]}");
            }
            return $"{string.Join(", ", counts)} (slots: {string.Join(", ", slots)})";
        }

        public static int GetTroopBudget(CommanderRole role)
        {
            return role == CommanderRole.Cavalry || role == CommanderRole.HorseArcher ? MountedFormationBudget : FootFormationBudget;
        }

        // Baseline the stock spawner multiplies by GetTroopNumberMultiplierForMissingPlayer.
        // The game mode turns it into the formation's exact troop count. Also the bot count the class weights
        // are read at (MPPerkObject.GetTroopCount), as in a stock Captain match of 100 bots per formation.
        public const int BotsPerFormationBaseline = 100;

        // Upper bound of a formation's troop count in the network messages (SpawnLayoutSeed).
        public const int MaxTroopsPerFormation = 1023;

        // Share of the stock multiplayer top speed a horse keeps (CommanderBattleAgentStatCalculateModel).
        public const float HorseSpeedFactor = 0.85f;

        // Share of the stock draw/aim and reload speed a bow or crossbow keeps, so ranged troops fire slower
        // (CommanderBattleAgentStatCalculateModel). Applies to players and AI alike.
        public const float BowFireSpeedFactor = 0.75f;

        public const float CrossbowFireSpeedFactor = 0.75f;

        // Each round moves each army within its spawn zone by up to these amounts (CommanderBattleSpawnLayout):
        // sideways, back (away from the enemy) or forward, and turned; the formations of a role also swap places.
        public const float SpawnLateralJitter = 40f;

        public const float SpawnBackJitter = 40f;

        public const float SpawnForwardJitter = 15f;

        public const float SpawnTurnJitterDegrees = 10f;
    }
}
