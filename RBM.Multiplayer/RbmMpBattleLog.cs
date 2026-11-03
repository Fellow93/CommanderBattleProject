using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using RBMCombat;

namespace RBM.Multiplayer
{
    // RBM's battle log (RBMCombat.BattleHitLogic): every blow of a battle with its armour, raw and dealt damage, and
    // the AI's shot traces, for tuning RBM mode. Server only, RBM mode, while RbmMpLog.BattleHits is on. One file per
    // map, in C:\ProgramData\Mount and Blade II Bannerlord\logs\RBM.Multiplayer_battles\; its tallies run across the
    // map's rounds, since rounds reuse the mission.
    internal static class RbmMpBattleLog
    {
        private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Mount and Blade II Bannerlord", "logs", "RBM.Multiplayer_battles");

        // RBM opens the file in the player's singleplayer RBM folder, Documents\...\Configs\RBM\logs\battles, and
        // deletes all but its newest ten logs there. Its StartBattle, with this folder and no pruning.
        [HarmonyPatch(typeof(BattleHitLog), nameof(BattleHitLog.StartBattle))]
        private static class StartBattlePatch
        {
            private static readonly FieldInfo FileLock = AccessTools.Field(typeof(BattleHitLog), "_fileLock");

            private static readonly FieldInfo Pending = AccessTools.Field(typeof(BattleHitLog), "_pending");

            private static readonly FieldInfo FileLogFailed = AccessTools.Field(typeof(BattleHitLog), "_fileLogFailed");

            private static readonly FieldInfo FilePath = AccessTools.Field(typeof(BattleHitLog), "_filePath");

            private static bool Prefix(string header)
            {
                lock (FileLock.GetValue(null))
                {
                    ((List<string>)Pending.GetValue(null)).Clear();
                    FileLogFailed.SetValue(null, false);
                    FilePath.SetValue(null, null);
                    if (!BattleHitLog.IsEnabled)
                    {
                        return false;
                    }
                    try
                    {
                        Directory.CreateDirectory(Folder);
                        string path = Path.Combine(Folder, $"rbm_battle_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
                        File.WriteAllText(path, header);
                        FilePath.SetValue(null, path);
                        RbmMpLog.Write($"Battle log: {path}");
                    }
                    catch (Exception e)
                    {
                        FileLogFailed.SetValue(null, true);
                        RbmMpLog.Write($"Battle log not opened: {e.Message}");
                    }
                }
                return false;
            }
        }
    }
}
