using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.Library;

namespace CommanderBattle
{
    // Client-side address rewrites read from Modules\CommanderBattle\ModuleData\server_address_overrides.txt,
    // one "advertised = actual" pair per line, '#' for comments. Re-read on every join so edits apply without
    // restarting the game.
    public static class ServerAddressOverrides
    {
        private const string FileName = "server_address_overrides.txt";

        public static bool TryGetOverride(string advertisedAddress, out string actualAddress)
        {
            actualAddress = null;
            if (string.IsNullOrEmpty(advertisedAddress))
            {
                return false;
            }
            return Load().TryGetValue(advertisedAddress.Trim(), out actualAddress);
        }

        private static Dictionary<string, string> Load()
        {
            Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string path = Path.Combine(BasePath.Name, "Modules", "CommanderBattle", "ModuleData", FileName);
                if (!File.Exists(path))
                {
                    return overrides;
                }
                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    int separator = line.IndexOf('=');
                    if (line.Length == 0 || line.StartsWith("#") || separator <= 0)
                    {
                        continue;
                    }
                    string advertised = line.Substring(0, separator).Trim();
                    string actual = line.Substring(separator + 1).Trim();
                    if (advertised.Length > 0 && actual.Length > 0)
                    {
                        overrides[advertised] = actual;
                    }
                }
            }
            catch (Exception exception)
            {
                CommanderBattleLog.Write("Could not read " + FileName + ": " + exception.Message);
            }
            return overrides;
        }
    }
}
