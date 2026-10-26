#if !SERVER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using Path = System.IO.Path;

namespace CommanderBattle
{
    // Multiplayer only loads scenes from multiplayer modules, so the campaign field-battle scenes the server's map pool
    // uses are linked from the player's own SandBoxCore into this module's SceneObj, as tools\Install-CampaignScenes.ps1
    // does. Doing it here means players who get the mod as a zip never run a script. Junctions need no admin rights
    // and no disk space; the scenes are never shipped with the mod.
    internal static class CampaignSceneLinker
    {
        private const string ModuleId = "CommanderBattle";

        public static void LinkMissingScenes()
        {
            try
            {
                string gameModules = Path.GetFullPath(Path.Combine(BasePath.Name, "Modules"));
                string sceneList = Path.Combine(gameModules, "SandBox", "ModuleData", "sp_battle_scenes.xml");
                string sceneSource = Path.Combine(gameModules, "SandBoxCore", "SceneObj");
                // <module>\bin\Win64_Shipping_Client\CommanderBattle.dll: works for a Workshop install outside the game folder too.
                string moduleDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(CampaignSceneLinker).Assembly.Location), "..", ".."));
                string sceneObj = Path.Combine(moduleDir, "SceneObj");

                if (!File.Exists(sceneList) || !Directory.Exists(sceneSource))
                {
                    CommanderBattleLog.Write("Campaign scenes: not linked, " + sceneList + " or " + sceneSource + " is missing");
                    return;
                }

                List<string> scenes = ReadSceneIds(sceneList).Where(id => Directory.Exists(Path.Combine(sceneSource, id))).ToList();
                Directory.CreateDirectory(sceneObj);
                List<string> missing = scenes.Where(id => !Directory.Exists(Path.Combine(sceneObj, id))).ToList();
                if (missing.Count > 0)
                {
                    CreateJunctions(missing, sceneObj, sceneSource);
                }

                // The engine may have listed the module's scenes before this ran, so tell it where the new ones are.
                int linked = 0;
                foreach (string id in scenes)
                {
                    if (Directory.Exists(Path.Combine(sceneObj, id)))
                    {
                        Utilities.PairSceneNameToModuleName(id, ModuleId);
                        linked++;
                    }
                }
                int linkedNow = missing.Count(id => Directory.Exists(Path.Combine(sceneObj, id)));
                string summary = $"Campaign maps: {linked} of {scenes.Count} ready ({linkedNow} linked now)";
                if (missing.Count > 0)
                {
                    CommanderBattleLog.Write(summary);
                }
                else
                {
                    CommanderBattleLog.Detail(summary);
                }
            }
            catch (Exception e)
            {
                CommanderBattleLog.Write("Campaign scenes: linking failed: " + e);
            }
        }

        private static IEnumerable<string> ReadSceneIds(string sceneList)
        {
            var xml = new XmlDocument();
            xml.Load(sceneList);
            return xml.SelectNodes("/SPBattleScenes/Scene").Cast<XmlNode>()
                .Select(node => node.Attributes?["id"]?.Value)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        // .NET Framework has no junction API, so mklink /J runs once from a batch file (one cmd call per scene is slow,
        // and the full list is longer than a command line allows).
        private static void CreateJunctions(List<string> scenes, string sceneObj, string sceneSource)
        {
            string batch = Path.Combine(Path.GetTempPath(), "CommanderBattle_link_scenes.cmd");
            var lines = new StringBuilder("@echo off\r\nchcp 65001 >nul\r\n");
            foreach (string id in scenes)
            {
                lines.Append("mklink /J \"").Append(Path.Combine(sceneObj, id)).Append("\" \"").Append(Path.Combine(sceneSource, id)).Append("\" >nul\r\n");
            }
            File.WriteAllText(batch, lines.ToString(), new UTF8Encoding(false));
            try
            {
                var start = new ProcessStartInfo("cmd.exe", "/c \"" + batch + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process process = Process.Start(start))
                {
                    if (!process.WaitForExit(60000))
                    {
                        CommanderBattleLog.Write("Campaign scenes: mklink did not finish within 60 s");
                    }
                }
            }
            finally
            {
                File.Delete(batch);
            }
        }
    }
}
#endif
