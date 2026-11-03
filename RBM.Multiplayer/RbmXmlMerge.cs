using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using TaleWorlds.ObjectSystem;

namespace RBM.Multiplayer
{
    // RBM's item files replace the singleplayer items of the same id whole, as RBM's own loader does in singleplayer
    // (RBM\XmlLoadingPatches.cs, MergeTwoXmlsPatch). The stock merge folds them in field by field instead, so an item
    // RBM rebuilt on another template keeps the old version's extra pieces: eastern_throwing_spear_1_t3 becomes a
    // Javelin with a TwoHandedPolearm guard, fails to build, and LoadXML silently drops every item after it.
    // Patched at module load on the server and on every client, before any XML is read: both must build the same
    // items, in the same order (MP sends items by index). Patched by hand, not by attribute, so RbmMpPatcher doesn't
    // apply it a second time on the server.
    //
    // The patch goes on CreateMergedXmlFile, which merges a list of files one by one, not on the small MergeTwoXmls
    // it calls: the client compiles CreateMergedXmlFile before any module loads, with MergeTwoXmls inlined into it, so
    // a patch on MergeTwoXmls never ran there (the .NET 6 server called it, and the two built different items).
    internal static class RbmXmlMerge
    {
        // The comment RBM puts at the top of each of its XML files.
        private const string RbmXmlTag = "RBM_XML_TAG";

        private static readonly Func<string, string, bool, XmlDocument> CreateDocumentFromXmlFile =
            AccessTools.MethodDelegate<Func<string, string, bool, XmlDocument>>(AccessTools.Method(typeof(MBObjectManager), "CreateDocumentFromXmlFile"));

        private static bool _patched;

        // Files merged, and RBM files among them by document root (Items, CraftingPieces...), for RbmItemSetCheck.
        private static int _merges;

        private static readonly Dictionary<string, int> RbmMerges = new Dictionary<string, int>();

        public static void Patch()
        {
            if (_patched)
            {
                return;
            }
            _patched = true;
            try
            {
                new Harmony(RbmMpPatcher.MultiplayerHarmonyId).Patch(Target(), prefix: new HarmonyMethod(typeof(RbmXmlMerge), nameof(Prefix)));
            }
            catch (Exception e)
            {
                RbmMpLog.Write($"RBM item merge not patched: {e}");
            }
        }

        public static string Describe()
        {
            Patches info = Harmony.GetPatchInfo(Target());
            string owners = info == null ? "none" : string.Join(", ", info.Prefixes.Select(p => p.owner));
            return $"merge patch prefixes [{owners}], {_merges} files merged, RBM files: " +
                (RbmMerges.Count == 0 ? "none" : string.Join(", ", RbmMerges.Select(pair => $"{pair.Key} {pair.Value}")));
        }

        private static MethodInfo Target()
        {
            return AccessTools.Method(typeof(MBObjectManager), nameof(MBObjectManager.CreateMergedXmlFile));
        }

        // CreateMergedXmlFile as the game has it, but RBM's files are merged by MergeRbm.
        private static bool Prefix(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation, ref XmlDocument __result)
        {
            XmlDocument document = CreateDocumentFromXmlFile(toBeMerged[0].Item1, toBeMerged[0].Item2, skipValidation);
            for (int i = 1; i < toBeMerged.Count; i++)
            {
                if (xsltList[i] != "")
                {
                    document = MBObjectManager.ApplyXslt(xsltList[i], document);
                }
                if (toBeMerged[i].Item1 != "")
                {
                    _merges++;
                    XmlDocument added = CreateDocumentFromXmlFile(toBeMerged[i].Item1, toBeMerged[i].Item2, skipValidation);
                    document = MergeRbm(document, added) ?? MBObjectManager.MergeTwoXmls(document, added, toBeMerged[i].Item2, keepDuplicates: false);
                }
            }
            __result = document;
            return false;
        }

        // Null for a file that isn't RBM's.
        private static XmlDocument MergeRbm(XmlDocument document, XmlDocument addedDocument)
        {
            XDocument added = MBObjectManager.ToXDocument(addedDocument);
            if (added.Root == null || !added.DescendantNodes().OfType<XComment>().Any(comment => comment.Value.Contains(RbmXmlTag)))
            {
                return null;
            }
            // Core parameters merge per id in the stock merge, as in RBM: appended, they would form a second
            // <managed_core_parameters> block, which ManagedParameters never reads (it takes the first).
            if (added.Root.Element("managed_core_parameters") != null)
            {
                return null;
            }
            string root = added.Root.Name.LocalName;
            RbmMerges[root] = RbmMerges.TryGetValue(root, out int count) ? count + 1 : 1;
            XDocument original = MBObjectManager.ToXDocument(document);
            HashSet<string> replaced = new HashSet<string>(added.Root.Elements().Where(IsReplacedWhole).Select(Key));
            original.Root.Elements().Where(element => IsReplacedWhole(element) && replaced.Contains(Key(element))).Remove();
            if (RBMConfig.RBMConfig.betterArrowVisuals)
            {
                UseRealArrowMeshes(added);
            }
            // Everything else is appended, duplicates included, as in RBM: a crafting piece's later node is
            // deserialized over the earlier one.
            original.Root.Add(added.Root.Elements());
            return MBObjectManager.ToXmlDocument(original);
        }

        // RBM's betterArrowVisuals (XmlLoadingPatches): its arrows and bolts fly as their own mesh, not the stock
        // flying stand-in. Here on every machine, so all build the same items.
        private static void UseRealArrowMeshes(XDocument added)
        {
            foreach (XElement item in added.Root.Elements("Item"))
            {
                string type = item.Attribute("Type")?.Value;
                XAttribute flyingMesh = item.Attribute("flying_mesh");
                XAttribute mesh = item.Attribute("mesh");
                if ((type == "Arrows" || type == "Bolts") && flyingMesh != null && mesh != null)
                {
                    flyingMesh.Value = mesh.Value;
                }
            }
        }

        private static bool IsReplacedWhole(XElement element)
        {
            return (element.Name == "Item" || element.Name == "CraftedItem") && element.Attribute("id") != null;
        }

        private static string Key(XElement element)
        {
            return $"{element.Name}|{element.Attribute("id").Value}";
        }
    }
}
