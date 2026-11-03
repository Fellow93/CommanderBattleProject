using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace CommanderBattle
{
    // RBM mode's gear (CombatModeSetting): every class's loadout of singleplayer items, which RBM's item files retune
    // (RBM.Multiplayer loads them in multiplayer). Light mode keeps the classes' own mp_ gear and never reads this.
    //
    // Read from Module/ModuleData/cb_rbm_loadouts_<culture>.xml, the same files on the server and every client, once
    // the game's items are loaded:
    //   <Class id="<MPHeroClass id>">
    //     <Hero> <equipment slot="Item0" id="Item.<item>" /> ... </Hero>        the commander's gear, as given
    //     <Troop> <EquipmentRoster> ... </EquipmentRoster> ... </Troop>          each bot draws every slot from a random
    //                                                                            roster, as stock bots do
    //     <Setup index="1" name="..."> <equipment slot="Item0" id="..." target="Troops" /> ... </Setup>
    //   </Class>
    // A setup replaces the class's weapon setup perk of that index (CommanderRoles.GetWeaponSetups); its slots work as
    // the perk's AlternativeEquipmentOnSpawn effects: an item, or no id to empty the slot; target Player, Troops or Any
    // (the default). Setup 0 is the class's own loadout and has no entry.
    public static class RbmLoadouts
    {
        private const string FilePattern = "cb_rbm_loadouts_*.xml";

        private sealed class SetupSlot
        {
            public EquipmentIndex Index;

            public ItemObject Item;

            public bool ForPlayer;

            public bool ForTroops;
        }

        private sealed class ClassLoadout
        {
            public Equipment Hero;

            public readonly List<Equipment> TroopRosters = new List<Equipment>();

            public readonly Dictionary<int, List<SetupSlot>> Setups = new Dictionary<int, List<SetupSlot>>();
        }

        private static Dictionary<string, ClassLoadout> _loadouts;

        private static readonly HashSet<string> MissingLogged = new HashSet<string>();

        // RBM mode's gear of a commander (or the AI general): the class's hero loadout with the setup's player slots.
        // Null when the class has none: the caller keeps the class's own gear.
        public static Equipment BuildHero(MultiplayerClassDivisions.MPHeroClass heroClass, int weaponIndex)
        {
            ClassLoadout loadout = Find(heroClass);
            if (loadout?.Hero == null)
            {
                return null;
            }
            Equipment equipment = loadout.Hero.Clone();
            ApplySetup(equipment, loadout, weaponIndex, forPlayer: true);
            return equipment;
        }

        // RBM mode's gear of one bot: each slot from a roster drawn with the seed, then the setup's troop slots.
        public static Equipment BuildTroop(MultiplayerClassDivisions.MPHeroClass heroClass, int weaponIndex, int seed)
        {
            ClassLoadout loadout = Find(heroClass);
            if (loadout == null || loadout.TroopRosters.Count == 0)
            {
                return null;
            }
            Random random = new Random(seed);
            Equipment equipment = new Equipment();
            for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot; index < EquipmentIndex.NumEquipmentSetSlots; index++)
            {
                equipment[index] = loadout.TroopRosters[random.Next(loadout.TroopRosters.Count)][index];
            }
            ApplySetup(equipment, loadout, weaponIndex, forPlayer: false);
            return equipment;
        }

        // The first roster, for a preview: the gear every bot of the class carries, in one of its looks.
        public static Equipment GetTroopPreview(MultiplayerClassDivisions.MPHeroClass heroClass, int weaponIndex)
        {
            ClassLoadout loadout = Find(heroClass);
            if (loadout == null || loadout.TroopRosters.Count == 0)
            {
                return null;
            }
            Equipment equipment = loadout.TroopRosters[0].Clone();
            ApplySetup(equipment, loadout, weaponIndex, forPlayer: false);
            return equipment;
        }

        // For the class selection screen in RBM mode, where the class XML's armour value describes the mp_ gear: the
        // troops' body and head armour. Null without a loadout.
        public static string DescribeArmour(MultiplayerClassDivisions.MPHeroClass heroClass)
        {
            Equipment equipment = GetTroopPreview(heroClass, 0);
            return equipment == null ? null : $"armour {equipment.GetHumanBodyArmorSum():0} · head {equipment.GetHeadArmorSum():0}";
        }

        // The troops' weapons with the setup, by name ("Spear, Kite Shield, Gladius"). Null without a loadout.
        public static string DescribeWeapons(MultiplayerClassDivisions.MPHeroClass heroClass, int weaponIndex)
        {
            Equipment equipment = GetTroopPreview(heroClass, weaponIndex);
            if (equipment == null)
            {
                return null;
            }
            List<string> names = new List<string>();
            for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot; index < EquipmentIndex.NumAllWeaponSlots; index++)
            {
                if (!equipment[index].IsEmpty)
                {
                    names.Add(equipment[index].Item.Name.ToString());
                }
            }
            return string.Join(", ", names);
        }

        private static void ApplySetup(Equipment equipment, ClassLoadout loadout, int weaponIndex, bool forPlayer)
        {
            if (!loadout.Setups.TryGetValue(weaponIndex, out List<SetupSlot> slots))
            {
                return;
            }
            foreach (SetupSlot slot in slots)
            {
                if (forPlayer ? slot.ForPlayer : slot.ForTroops)
                {
                    equipment[slot.Index] = slot.Item != null ? new EquipmentElement(slot.Item) : EquipmentElement.Invalid;
                }
            }
        }

        private static ClassLoadout Find(MultiplayerClassDivisions.MPHeroClass heroClass)
        {
            if (heroClass == null)
            {
                return null;
            }
            EnsureLoaded();
            if (_loadouts.TryGetValue(heroClass.StringId, out ClassLoadout loadout))
            {
                return loadout;
            }
            if (MissingLogged.Add(heroClass.StringId))
            {
                CommanderBattleLog.Write($"RBM loadouts: none for {heroClass.StringId}, it keeps its own gear");
            }
            return null;
        }

        // Every class of the converted cultures, checked once against the files: the server calls this when an RBM
        // mode mission starts, so a gap shows in its log before anyone spawns.
        public static void LogCoverage()
        {
            EnsureLoaded();
            List<string> missing = MultiplayerClassDivisions.GetMPHeroClasses()
                .Where(heroClass => CommanderBattleSettings.IsConverted(heroClass.Culture) && !_loadouts.ContainsKey(heroClass.StringId))
                .Select(heroClass => heroClass.StringId).ToList();
            CommanderBattleLog.Write($"RBM loadouts: {_loadouts.Count} classes, {_loadouts.Values.Sum(l => l.Setups.Count)} setups" +
                (missing.Count > 0 ? $"; no loadout for {missing.Count}: {string.Join(", ", missing)}" : ""));
        }

        private static void EnsureLoaded()
        {
            if (_loadouts != null)
            {
                return;
            }
            _loadouts = new Dictionary<string, ClassLoadout>();
            List<string> problems = new List<string>();
            string directory = Path.Combine(BasePath.Name, "Modules", "CommanderBattle", "ModuleData");
            string[] files = Directory.Exists(directory) ? Directory.GetFiles(directory, FilePattern).OrderBy(path => path, StringComparer.Ordinal).ToArray() : new string[0];
            foreach (string path in files)
            {
                try
                {
                    XmlDocument document = new XmlDocument();
                    document.Load(path);
                    foreach (XmlElement classNode in document.DocumentElement.ChildNodes.OfType<XmlElement>().Where(node => node.Name == "Class"))
                    {
                        string id = classNode.GetAttribute("id");
                        _loadouts[id] = ReadClass(classNode, $"{Path.GetFileName(path)} {id}", problems);
                    }
                }
                catch (Exception e)
                {
                    problems.Add($"{Path.GetFileName(path)}: {e.Message}");
                }
            }
            CommanderBattleLog.Detail($"RBM loadouts read: {_loadouts.Count} classes from {files.Length} files");
            if (problems.Count > 0)
            {
                CommanderBattleLog.Write($"RBM loadouts: {problems.Count} problems: {string.Join("; ", problems.Take(20))}{(problems.Count > 20 ? "; ..." : "")}");
            }
        }

        private static ClassLoadout ReadClass(XmlElement classNode, string where, List<string> problems)
        {
            ClassLoadout loadout = new ClassLoadout();
            foreach (XmlElement node in classNode.ChildNodes.OfType<XmlElement>())
            {
                switch (node.Name)
                {
                    case "Hero":
                        loadout.Hero = ReadEquipment(node, $"{where} hero", problems);
                        break;
                    case "Troop":
                        foreach (XmlElement roster in node.ChildNodes.OfType<XmlElement>().Where(child => child.Name == "EquipmentRoster"))
                        {
                            loadout.TroopRosters.Add(ReadEquipment(roster, $"{where} troop roster {loadout.TroopRosters.Count}", problems));
                        }
                        break;
                    case "Setup":
                        if (int.TryParse(node.GetAttribute("index"), out int setupIndex) && setupIndex > 0)
                        {
                            loadout.Setups[setupIndex] = ReadSetup(node, $"{where} setup {setupIndex}", problems);
                        }
                        else
                        {
                            problems.Add($"{where}: setup index \"{node.GetAttribute("index")}\" is not 1 or more");
                        }
                        break;
                }
            }
            return loadout;
        }

        private static Equipment ReadEquipment(XmlElement node, string where, List<string> problems)
        {
            Equipment equipment = new Equipment();
            foreach (XmlElement slotNode in node.ChildNodes.OfType<XmlElement>().Where(child => child.Name == "equipment"))
            {
                if (TryReadSlot(slotNode, where, problems, out EquipmentIndex index, out ItemObject item) && item != null)
                {
                    equipment[index] = new EquipmentElement(item);
                }
            }
            return equipment;
        }

        private static List<SetupSlot> ReadSetup(XmlElement node, string where, List<string> problems)
        {
            List<SetupSlot> slots = new List<SetupSlot>();
            foreach (XmlElement slotNode in node.ChildNodes.OfType<XmlElement>().Where(child => child.Name == "equipment"))
            {
                if (!TryReadSlot(slotNode, where, problems, out EquipmentIndex index, out ItemObject item))
                {
                    continue;
                }
                string target = slotNode.GetAttribute("target");
                slots.Add(new SetupSlot
                {
                    Index = index,
                    Item = item,
                    ForPlayer = target != "Troops",
                    ForTroops = target != "Player"
                });
            }
            return slots;
        }

        // False for a slot to skip; item null for one to empty.
        private static bool TryReadSlot(XmlElement slotNode, string where, List<string> problems, out EquipmentIndex index, out ItemObject item)
        {
            item = null;
            index = EquipmentIndex.None;
            try
            {
                index = Equipment.GetEquipmentIndexFromOldEquipmentIndexName(slotNode.GetAttribute("slot"));
            }
            catch (ArgumentException)
            {
                problems.Add($"{where}: unknown slot \"{slotNode.GetAttribute("slot")}\"");
                return false;
            }
            string id = slotNode.GetAttribute("id");
            if (id.Length == 0)
            {
                return true;
            }
            string itemId = id.StartsWith("Item.") ? id.Substring("Item.".Length) : id;
            item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            if (item == null)
            {
                problems.Add($"{where}: no item {itemId}");
                return false;
            }
            if (!Equipment.IsItemFitsToSlot(index, item))
            {
                problems.Add($"{where}: {itemId} does not fit {slotNode.GetAttribute("slot")}");
                item = null;
                return false;
            }
            return true;
        }
    }
}
