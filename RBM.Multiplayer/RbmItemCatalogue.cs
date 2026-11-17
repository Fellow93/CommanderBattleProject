using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RBM.Multiplayer
{
    // The unit editor (tools\UnitEditor) picks troop gear from the items the game actually built, RBM's merge and
    // crafted weapons included. Written only on a machine that has the repo next to the game, in the item shape of
    // RBM's troop loadout editor so its picker can be reused.
    internal static class RbmItemCatalogue
    {
        public static void Write(MBReadOnlyList<ItemObject> items, string statsFingerprint)
        {
            try
            {
                // The dedicated server sits next to the game, as Directory.Build.props assumes.
                string folder = new[]
                {
                    Path.Combine(BasePath.Name, "Modules", "CommanderBattleProject", "tools", "UnitEditor"),
                    Path.Combine(BasePath.Name, "..", "Mount & Blade II Bannerlord", "Modules", "CommanderBattleProject", "tools", "UnitEditor")
                }.FirstOrDefault(Directory.Exists);
                if (folder == null)
                {
                    return;
                }
                StringBuilder sb = new StringBuilder();
                sb.Append("window.UNIT_EDITOR_DATA = { written: ").Append(Str(DateTime.Now.ToString("s", CultureInfo.InvariantCulture)))
                    .Append(", fingerprint: ").Append(Str(statsFingerprint)).Append(", items: [\n");
                int n = 0;
                foreach (ItemObject item in items)
                {
                    if (item.StringId.StartsWith("mp_"))
                    {
                        continue;
                    }
                    sb.Append(n++ == 0 ? "" : ",\n");
                    AppendItem(sb, item);
                }
                sb.Append("\n] };\n");
                File.WriteAllText(Path.Combine(folder, "unit-editor-data.js"), sb.ToString());
                RbmMpLog.Write($"Item catalogue: {n} items written");
            }
            catch (Exception e)
            {
                RbmMpLog.Write($"Item catalogue failed: {e.Message}");
            }
        }

        private static void AppendItem(StringBuilder sb, ItemObject item)
        {
            List<string> f = new List<string>
            {
                "id: " + Str(item.StringId), "name: " + Str(item.Name?.ToString() ?? ""), "type: " + Str(item.ItemType.ToString())
            };
            Add(f, "culture", item.Culture?.StringId);
            // ItemObject.Tier throws in multiplayer (no ItemValueModel), and Value is a 1 placeholder there. The tier comes
            // from the id instead, as RBM's troop loadout editor reads it; its picker marks such a tier with "?".
            Match tier = Regex.Match(item.StringId, @"_t(\d)(?:_|$)");
            if (tier.Success)
            {
                f.Add("tier: " + tier.Groups[1].Value);
                f.Add("tierFromId: true");
            }
            Add(f, "weight", item.Weight);
            Add(f, "flags", FlagNames(item.ItemFlags));
            ArmorComponent armor = item.ArmorComponent;
            if (armor != null)
            {
                List<string> a = new List<string>();
                Add(a, "head", armor.HeadArmor);
                Add(a, "body", armor.BodyArmor);
                Add(a, "arm", armor.ArmArmor);
                Add(a, "leg", armor.LegArmor);
                Add(a, "mat", armor.MaterialType.ToString());
                AddObject(f, "armor", a);
            }
            HorseComponent horse = item.HorseComponent;
            if (horse != null)
            {
                List<string> h = new List<string>();
                Add(h, "spd", horse.Speed);
                Add(h, "man", horse.Maneuver);
                Add(h, "chg", horse.ChargeDamage);
                // As EquipmentElement and ItemObject count a horse's health: the monster's plus the item's extra_health.
                Add(h, "hp", horse.HitPoints + horse.HitPointBonus);
                AddObject(f, "horse", h);
            }
            if (item.Weapons != null && item.Weapons.Count > 0)
            {
                f.Add("weapons: [" + string.Join(", ", item.Weapons.Select(WeaponFields)) + "]");
            }
            sb.Append("{ ").Append(string.Join(", ", f)).Append(" }");
        }

        private static string WeaponFields(WeaponComponentData w)
        {
            List<string> f = new List<string>();
            Add(f, "cls", w.WeaponClass.ToString());
            Add(f, "sw", w.SwingDamage);
            Add(f, "swt", DamageType(w.SwingDamageType, w.SwingDamage));
            Add(f, "th", w.ThrustDamage);
            Add(f, "tht", DamageType(w.ThrustDamageType, w.ThrustDamage));
            Add(f, "spd", w.SwingSpeed);
            Add(f, "tspd", w.ThrustSpeed);
            Add(f, "len", w.WeaponLength);
            Add(f, "ms", w.MissileSpeed);
            Add(f, w.IsShield ? "hp" : "stack", w.MaxDataValue);
            Add(f, "acc", w.Accuracy);
            Add(f, "hand", w.Handling);
            Add(f, "arm", w.BodyArmor);
            Add(f, "bal", w.WeaponBalance);
            Add(f, "flags", FlagNames(w.WeaponFlags));
            return "{ " + string.Join(", ", f) + " }";
        }

        private static string DamageType(DamageTypes type, int damage)
        {
            return type == DamageTypes.Invalid || damage == 0 ? null : type.ToString();
        }

        // Single-bit names only: the enums also name masks (WeaponMask) that would read as flags of their own.
        private static List<string> FlagNames<T>(T flags) where T : Enum
        {
            ulong set = Convert.ToUInt64(flags);
            return Enum.GetValues(typeof(T)).Cast<T>().Select(v => Convert.ToUInt64(v))
                .Where(v => v != 0 && (v & (v - 1)) == 0 && (set & v) == v).Distinct()
                .Select(v => Enum.GetName(typeof(T), v)).ToList();
        }

        private static void Add(List<string> fields, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                fields.Add($"{name}: {Str(value)}");
            }
        }

        private static void Add(List<string> fields, string name, float value)
        {
            if (value != 0)
            {
                fields.Add($"{name}: {value.ToString("0.####", CultureInfo.InvariantCulture)}");
            }
        }

        private static void Add(List<string> fields, string name, List<string> values)
        {
            if (values.Count > 0)
            {
                fields.Add($"{name}: [{string.Join(", ", values.Select(Str))}]");
            }
        }

        private static void AddObject(List<string> fields, string name, List<string> objectFields)
        {
            if (objectFields.Count > 0)
            {
                fields.Add($"{name}: {{ {string.Join(", ", objectFields)} }}");
            }
        }

        private static string Str(string s)
        {
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\')
                {
                    sb.Append('\\').Append(c);
                }
                else if (c < ' ')
                {
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
