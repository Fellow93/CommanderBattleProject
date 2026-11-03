using System.Collections.Generic;
using System.Linq;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace RBM.Multiplayer
{
    // Check of RBM's item set in multiplayer (Module\SubModule.xml), logged once per machine after the game has loaded
    // its XML, so the server's and a client's logs can be compared: the counts, the fingerprints, and the first item
    // that failed to load, if any. With RbmMpLog.Detailed on, also the probes (whether RBM's values won the merge over
    // the singleplayer ones, and the stock mp_ items kept theirs), the merge counts and the mp_ weapon dump.
    internal static class RbmItemSetCheck
    {
        // imperial_scale_armor: RBM body 135 / leg 122 / arm 67, SP 57 / 20 / 16. goggled_helmet_over_full_mail: RBM
        // head 114 / body 74 / arm 40, SP head 55. empire_sword_1_t2: RBM blade factors about 1, SP about 3.
        // nomad_bow: RBM thrust 100 / missile 170, SP 70 / 77. gladius_b: RBM-only crafted item.
        private static readonly string[] Probes =
        {
            "imperial_scale_armor", "mp_imperial_scale_armor", "goggled_helmet_over_full_mail",
            "empire_sword_1_t2", "gladius_b", "mp_empire_spatha", "nomad_bow", "mp_nomad_bow", "empire_horse"
        };

        private static bool _logged;

        public static void Log()
        {
            if (_logged)
            {
                return;
            }
            _logged = true;
            MBReadOnlyList<ItemObject> items = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            int mp = 0;
            int crafted = 0;
            List<string> brokenCrafted = new List<string>();
            List<string> unresolvedCulture = new List<string>();
            foreach (ItemObject item in items)
            {
                if (item.StringId.StartsWith("mp_"))
                {
                    mp++;
                }
                if (item.IsCraftedWeapon)
                {
                    crafted++;
                    // A slot the weapon doesn't use (an axe's guard) holds an invalid placeholder piece, never registered.
                    if (item.PrimaryWeapon == null || item.WeaponDesign.UsedPieces.Any(p => p.CraftingPiece != null && p.CraftingPiece.IsValid && !p.CraftingPiece.IsReady))
                    {
                        brokenCrafted.Add(item.StringId);
                    }
                }
                if (item.Culture != null && !item.Culture.IsReady)
                {
                    unresolvedCulture.Add(item.StringId);
                }
            }
            RbmMpLog.Write($"Item set: {items.Count} items ({mp} mp_, {items.Count - mp} other), {crafted} crafted, " +
                $"{brokenCrafted.Count} crafted broken{Sample(brokenCrafted)}, {unresolvedCulture.Count} with an unknown culture{Sample(unresolvedCulture)}, " +
                $"{MBObjectManager.Instance.GetObjectTypeList<CraftingPiece>().Count} crafting pieces");
            // MP sends items by index, so server and clients must register the same items in the same order, with the
            // same stats. Equal fingerprints on two machines mean they do.
            RbmMpLog.Write($"Item fingerprints: order {Fingerprint(items.Select(item => item.StringId)):x8}, " +
                $"stats {Fingerprint(items.Select(Stats)):x8}");
            LogUnloaded();
            if (RbmMpLog.Detailed)
            {
                RbmMpLog.Write($"Item probes: {string.Join("; ", Probes.Select(Describe))}");
                RbmMpLog.Write($"Item merge: {RbmXmlMerge.Describe()}");
                // RBM's managed core parameters (Module\SubModule.xml): 1 and 0.5 with RBM's, 0.15 and 0.15 stock.
                RbmMpLog.Write($"Core parameters: NonTipThrustHitDamageMultiplier {ManagedParameters.Instance.GetManagedParameter(ManagedParametersEnum.NonTipThrustHitDamageMultiplier)}, " +
                    $"SwingHitWithArmDamageMultiplier {ManagedParameters.Instance.GetManagedParameter(ManagedParametersEnum.SwingHitWithArmDamageMultiplier)}");
                DumpMpWeapons(items);
            }
        }

        // Every mp_ crafted weapon's usages, one line each, next to the log: a diff of two runs shows whether a change
        // to the shared templates or weapon descriptions touched the stock multiplayer weapons.
        private static void DumpMpWeapons(MBReadOnlyList<ItemObject> items)
        {
            try
            {
                string path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                    "Mount and Blade II Bannerlord", "logs", $"RBM.Multiplayer_{System.Diagnostics.Process.GetCurrentProcess().Id}_mp_weapons.txt");
                IEnumerable<string> lines = items.Where(item => item.StringId.StartsWith("mp_") && item.IsCraftedWeapon).OrderBy(item => item.StringId)
                    .Select(item => $"{item.StringId}: " + string.Join(" | ", item.Weapons.Select(w =>
                        $"{w.WeaponDescriptionId} {w.WeaponClass} sw {w.SwingDamage} th {w.ThrustDamage} sp {w.SwingSpeed}/{w.ThrustSpeed} len {w.WeaponLength} flags {w.WeaponFlags}")));
                System.IO.File.WriteAllLines(path, lines);
                // What the stats fingerprint hashes, item by item: a diff of a server's and a client's file names the
                // items they built differently.
                System.IO.File.WriteAllLines(path.Replace("_mp_weapons.txt", "_item_stats.txt"), items.Select(Stats));
            }
            catch (System.Exception e)
            {
                RbmMpLog.Write($"mp_ weapon dump failed: {e.Message}");
            }
        }

        // MBObjectManager.LoadXML reads every module's items as one merged document and swallows an exception, so
        // one bad node silently drops every item after it. Walks the same document to find the first one missing.
        private static void LogUnloaded()
        {
            try
            {
                XmlDocument doc = MBObjectManager.GetMergedXmlForManaged("Items", skipValidation: true, ignoreGameTypeInclusionCheck: false, gameType: "MultiplayerGame");
                int nodes = 0;
                int missing = 0;
                string firstMissing = null;
                XmlNode firstMissingNode = null;
                string lastLoaded = null;
                foreach (XmlNode node in doc.DocumentElement.ChildNodes)
                {
                    string id = node.Attributes?["id"]?.Value;
                    if (node.NodeType != XmlNodeType.Element || id == null)
                    {
                        continue;
                    }
                    nodes++;
                    if (MBObjectManager.Instance.GetObject<ItemObject>(id) == null)
                    {
                        missing++;
                        firstMissingNode = firstMissingNode ?? node;
                        firstMissing = firstMissing ?? $"{node.Name} {id} (node {nodes}, after {lastLoaded})";
                    }
                    else if (firstMissing == null)
                    {
                        lastLoaded = id;
                    }
                }
                if (firstMissingNode == null)
                {
                    RbmMpLog.Detail($"Item document: {nodes} nodes, all loaded");
                }
                else
                {
                    RbmMpLog.Write($"Item document: {nodes} nodes, {missing} not loaded, first {firstMissing}");
                    // Loading the node on its own again shows the exception the stock load swallowed.
                    XmlDocument single = new XmlDocument();
                    single.AppendChild(single.ImportNode(doc.DocumentElement.CloneNode(false), true));
                    single.DocumentElement.AppendChild(single.ImportNode(firstMissingNode, true));
                    try
                    {
                        MBObjectManager.Instance.LoadXml(single);
                        RbmMpLog.Write("Item document: the first missing node loads on its own");
                    }
                    catch (System.Exception e)
                    {
                        RbmMpLog.Write($"Item document: the first missing node throws {e}");
                    }
                }
            }
            catch (System.Exception e)
            {
                RbmMpLog.Write($"Item document check failed: {e}");
            }
        }

        // FNV-1a: string.GetHashCode differs between runs on .NET 6.
        private static uint Fingerprint(IEnumerable<string> values)
        {
            uint hash = 2166136261;
            foreach (string value in values)
            {
                foreach (char c in value + "\n")
                {
                    hash = (hash ^ c) * 16777619;
                }
            }
            return hash;
        }

        private static string Stats(ItemObject item)
        {
            // Invariant: .NET 6 and .NET Framework may format the same culture differently.
            string stats = $"{item.StringId} {item.Weight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}";
            if (item.ArmorComponent != null)
            {
                ArmorComponent armor = item.ArmorComponent;
                stats += $" a {armor.HeadArmor} {armor.BodyArmor} {armor.LegArmor} {armor.ArmArmor}";
            }
            if (item.HorseComponent != null)
            {
                stats += $" h {item.HorseComponent.Speed} {item.HorseComponent.Maneuver} {item.HorseComponent.ChargeDamage}";
            }
            foreach (WeaponComponentData w in item.Weapons ?? new MBList<WeaponComponentData>())
            {
                stats += $" w {w.WeaponDescriptionId} {w.SwingDamage} {w.ThrustDamage} {w.SwingSpeed} {w.ThrustSpeed} {w.MissileSpeed} {w.WeaponLength} {w.WeaponFlags}";
            }
            return stats;
        }

        private static string Sample(List<string> ids)
        {
            return ids.Count == 0 ? "" : $" ({string.Join(", ", ids.Take(8))}{(ids.Count > 8 ? ", ..." : "")})";
        }

        private static string Describe(string id)
        {
            ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(id);
            if (item == null)
            {
                return $"{id} missing";
            }
            if (item.ArmorComponent != null)
            {
                ArmorComponent armor = item.ArmorComponent;
                return $"{id} head {armor.HeadArmor} body {armor.BodyArmor} leg {armor.LegArmor} arm {armor.ArmArmor}, {item.Weight:0.#} kg";
            }
            if (item.HorseComponent != null)
            {
                HorseComponent horse = item.HorseComponent;
                return $"{id} speed {horse.Speed} maneuver {horse.Maneuver} charge {horse.ChargeDamage}, {item.Weight:0} kg";
            }
            WeaponComponentData weapon = item.PrimaryWeapon;
            if (weapon == null)
            {
                return $"{id} has no weapon data";
            }
            string blade = "";
            WeaponDesignElement bladePiece = item.WeaponDesign?.UsedPieces.FirstOrDefault(p => p.CraftingPiece?.BladeData != null);
            if (bladePiece != null)
            {
                BladeData data = bladePiece.CraftingPiece.BladeData;
                blade = $", blade {bladePiece.CraftingPiece.StringId} swing x{data.SwingDamageFactor:0.##} thrust x{data.ThrustDamageFactor:0.##}";
            }
            return $"{id} swing {weapon.SwingDamage} thrust {weapon.ThrustDamage} missile {weapon.MissileSpeed}, {item.Weight:0.##} kg{blade}";
        }
    }
}
