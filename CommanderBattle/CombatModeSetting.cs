using System;
using System.IO;
using TaleWorlds.Library;

namespace CommanderBattle
{
    public enum CombatMode
    {
        // Stock multiplayer items, RBM.Multiplayer's damage curve and Commander Battle's slower ranged fire.
        Light,

        // RBM's own items (the classes' RBM loadouts) and RBM Combat.
        Rbm
    }

    // The host's combat rules. The dedicated server reads them once, at module load, from
    // Modules\CommanderBattle\ModuleData\combat_mode.txt: a line "RBM" or "Light", '#' for comments; no file is Light.
    // Only a restart changes them: RBM Combat's patches go on at game start. The build doesn't ship the file, so a
    // host's choice survives a reinstall. Clients learn the mode from the server when they join (Network/CombatModeInfo).
    // RBM.Multiplayer reads IsRbm by name, so neither module needs the other's dll.
    public static class CombatModeSetting
    {
        public const string FileName = "combat_mode.txt";

        public static CombatMode Current { get; private set; } = CombatMode.Light;

        public static bool IsRbm => Current == CombatMode.Rbm;

        public static void LoadServerSetting()
        {
            string path = Path.Combine(BasePath.Name, "Modules", "CommanderBattle", "ModuleData", FileName);
            Current = Read(path, out string source);
            CommanderBattleLog.Write($"Combat mode: {Current} ({source})");
        }

        // A client, on the server's word.
        public static void SetFromServer(CombatMode mode)
        {
            if (mode != Current)
            {
                Current = mode;
                CommanderBattleLog.Write($"Combat mode: {mode} (server)");
            }
        }

        private static CombatMode Read(string path, out string source)
        {
            try
            {
                if (!File.Exists(path))
                {
                    source = $"no {FileName}";
                    return CombatMode.Light;
                }
                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                    {
                        continue;
                    }
                    if (line.Equals("RBM", StringComparison.OrdinalIgnoreCase))
                    {
                        source = FileName;
                        return CombatMode.Rbm;
                    }
                    if (line.Equals("Light", StringComparison.OrdinalIgnoreCase))
                    {
                        source = FileName;
                        return CombatMode.Light;
                    }
                    source = $"{FileName} says \"{line}\", not RBM or Light";
                    return CombatMode.Light;
                }
                source = $"{FileName} is empty";
                return CombatMode.Light;
            }
            catch (Exception e)
            {
                source = $"{FileName} unreadable: {e.Message}";
                return CombatMode.Light;
            }
        }
    }
}
