using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace RBM.Multiplayer
{
    // The engine sends each number in a fixed range (CompressionInfo). A value outside it trips an engine assertion
    // that freezes the dedicated server about 5 s, and the client reads garbage. RBM's numbers outgrew two ranges so
    // far (shield hit points, ShieldHitPointsSync; the missile index past 1023 live missiles). Every int and float the
    // server writes is clamped into its range here, and the first of each kind is logged with the message that wrote
    // it, so the next one shows up in RBM.Multiplayer_<pid>.log instead of as lag. Server only (RbmMpPatcher), both
    // modes. Agent indices and bools go to the engine directly and are not covered.
    internal static class NetworkRangeGuard
    {
        private static readonly AccessTools.StructFieldRef<CompressionInfo.Integer, int> IntegerMinimum =
            AccessTools.StructFieldRefAccess<CompressionInfo.Integer, int>("minimumValue");

        // What was logged: one line per writing message and range.
        private static readonly HashSet<string> Logged = new HashSet<string>();

        [HarmonyPatch(typeof(GameNetworkMessage), nameof(GameNetworkMessage.WriteIntToPacket))]
        private static class WriteIntPatch
        {
            private static void Prefix(ref int value, CompressionInfo.Integer compressionInfo)
            {
                int min = IntegerMinimum(ref compressionInfo);
                int max = compressionInfo.GetMaximumValue();
                if (value < min || value > max)
                {
                    int clamped = value < min ? min : max;
                    Report($"{min}..{max}", value.ToString(), clamped.ToString());
                    value = clamped;
                }
            }
        }

        [HarmonyPatch(typeof(GameNetworkMessage), nameof(GameNetworkMessage.WriteFloatToPacket))]
        private static class WriteFloatPatch
        {
            private static void Prefix(ref float value, CompressionInfo.Float compressionInfo)
            {
                float min = compressionInfo.GetMinimumValue();
                float max = compressionInfo.GetMaximumValue();
                if (value < min || value > max)
                {
                    float clamped = value < min ? min : max;
                    Report($"{min:0.###}..{max:0.###}", value.ToString("0.###"), clamped.ToString("0.###"));
                    value = clamped;
                }
            }
        }

        private static void Report(string range, string value, string clamped)
        {
            // The frames above the write name the message; only taken on a bad value.
            string writer = string.Join(" < ", new StackTrace(2, false).GetFrames()?.Take(3)
                .Select(frame => frame.GetMethod())
                .Where(method => method != null)
                .Select(method => $"{method.DeclaringType?.Name}.{method.Name}") ?? Enumerable.Empty<string>());
            if (Logged.Add($"{writer}|{range}"))
            {
                RbmMpLog.Write($"Network value out of range: {value} for {range}, sent as {clamped}, from {writer} (later ones from there not logged)");
            }
        }
    }
}
