#if SERVER
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TwMathF = TaleWorlds.Library.MathF;

// RBM is written for .NET Framework. On .NET 6 `using System;` also brings System.MathF into scope, so RBM's plain
// `MathF` (meaning TaleWorlds.Library.MathF) is ambiguous on the server build. Name lookup finds a type in an
// enclosing namespace before anything a using directive imports, so these forwarders settle it without touching
// RBM's files:
// - the global-namespace one serves every RBM file (including RBM's global-namespace tactics);
// - RBMAI needs its own, because two RBMAI files alias MathF themselves, and an alias next to a global-namespace
//   MathF is an error (CS0576) unless the lookup stops in RBMAI first.
// They cover the members RBM uses; a new one shows up as a server build error.

internal static class MathF
{
    public const float PI = TwMathF.PI;

    public static float Sqrt(float x) => TwMathF.Sqrt(x);

    public static float Sin(float x) => TwMathF.Sin(x);

    public static float Cos(float x) => TwMathF.Cos(x);

    public static float Acos(float x) => TwMathF.Acos(x);

    public static bool IsValidValue(float f) => TwMathF.IsValidValue(f);

    public static float Clamp(float value, float minValue, float maxValue) => TwMathF.Clamp(value, minValue, maxValue);

    public static float Lerp(float valueFrom, float valueTo, float amount, float minimumDifference = 1E-05f) => TwMathF.Lerp(valueFrom, valueTo, amount, minimumDifference);

    public static int Round(double f) => TwMathF.Round(f);

    public static int Round(float f) => TwMathF.Round(f);

    public static float Round(float f, int digits) => TwMathF.Round(f, digits);

    public static int Ceiling(double f) => TwMathF.Ceiling(f);

    public static int Ceiling(float f) => TwMathF.Ceiling(f);

    public static double Abs(double f) => TwMathF.Abs(f);

    public static float Abs(float f) => TwMathF.Abs(f);

    public static int Abs(int f) => TwMathF.Abs(f);

    public static double Max(double a, double b) => TwMathF.Max(a, b);

    public static float Max(float a, float b) => TwMathF.Max(a, b);

    public static int Max(int a, int b) => TwMathF.Max(a, b);

    public static long Max(long a, long b) => TwMathF.Max(a, b);

    public static float Max(float a, float b, float c) => TwMathF.Max(a, b, c);

    public static double Min(double a, double b) => TwMathF.Min(a, b);

    public static float Min(float a, float b) => TwMathF.Min(a, b);

    public static int Min(int a, int b) => TwMathF.Min(a, b);

    public static long Min(long a, long b) => TwMathF.Min(a, b);

    public static float Min(float a, float b, float c) => TwMathF.Min(a, b, c);
}

namespace RBMAI
{
    internal static class MathF
    {
        public const float PI = TwMathF.PI;

        public static float Sqrt(float x) => TwMathF.Sqrt(x);

        public static float Sin(float x) => TwMathF.Sin(x);

        public static float Cos(float x) => TwMathF.Cos(x);

        public static float Acos(float x) => TwMathF.Acos(x);

        public static bool IsValidValue(float f) => TwMathF.IsValidValue(f);

        public static float Clamp(float value, float minValue, float maxValue) => TwMathF.Clamp(value, minValue, maxValue);

        public static float Lerp(float valueFrom, float valueTo, float amount, float minimumDifference = 1E-05f) => TwMathF.Lerp(valueFrom, valueTo, amount, minimumDifference);

        public static int Round(double f) => TwMathF.Round(f);

        public static int Round(float f) => TwMathF.Round(f);

        public static float Round(float f, int digits) => TwMathF.Round(f, digits);

        public static int Ceiling(double f) => TwMathF.Ceiling(f);

        public static int Ceiling(float f) => TwMathF.Ceiling(f);

        public static double Abs(double f) => TwMathF.Abs(f);

        public static float Abs(float f) => TwMathF.Abs(f);

        public static int Abs(int f) => TwMathF.Abs(f);

        public static double Max(double a, double b) => TwMathF.Max(a, b);

        public static float Max(float a, float b) => TwMathF.Max(a, b);

        public static int Max(int a, int b) => TwMathF.Max(a, b);

        public static long Max(long a, long b) => TwMathF.Max(a, b);

        public static float Max(float a, float b, float c) => TwMathF.Max(a, b, c);

        public static double Min(double a, double b) => TwMathF.Min(a, b);

        public static float Min(float a, float b) => TwMathF.Min(a, b);

        public static int Min(int a, int b) => TwMathF.Min(a, b);

        public static long Min(long a, long b) => TwMathF.Min(a, b);

        public static float Min(float a, float b, float c) => TwMathF.Min(a, b, c);
    }

    internal static class RbmMpServerEngineCompat
    {
        // Missing from the server's TaleWorlds.Engine. Only DeepWaterNavBlocker calls it, and RBM.Multiplayer never
        // adds that logic (its physics walls would exist on the server only).
        internal static void SetManualLocalBoundingBox(this GameEntity entity, in BoundingBox boundingBox)
        {
        }
    }
}
#endif
