using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CommanderBattle
{
    // An order the side's general drew on the map in general mode (v1: a path with a label). Only the side's players
    // get it. Points are 2D; each client takes the height from its terrain.
    public sealed class GeneralOrder
    {
        public GeneralOrder(int id, BattleSideEnum side, Vec2[] points, string label)
        {
            Id = id;
            Side = side;
            Points = points;
            Label = label;
        }

        public int Id { get; }

        public BattleSideEnum Side { get; }

        public Vec2[] Points { get; }

        public string Label { get; }
    }

    public static class GeneralOrders
    {
        public const int MaxPoints = 64;

        public const int MaxLabelLength = 40;

        // A side's oldest order goes when the general draws one more.
        public const int MaxOrdersPerSide = 10;

        // Ids wrap; at most 2 x MaxOrdersPerSide are alive, so a wrapped id never meets a live one.
        public const int MaxId = 65535;

        // Labels are shown in Gauntlet text: '<' and '>' would start markup, and control characters break the line.
        public static string SanitizeLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return "";
            }
            StringBuilder clean = new StringBuilder(label.Length);
            foreach (char c in label)
            {
                if (c == '<' || c == '>' || char.IsControl(c))
                {
                    continue;
                }
                clean.Append(c);
            }
            string result = clean.ToString().Trim();
            return result.Length > MaxLabelLength ? result.Substring(0, MaxLabelLength).TrimEnd() : result;
        }
    }
}
