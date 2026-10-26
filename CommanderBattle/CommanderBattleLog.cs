using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace CommanderBattle
{
    // Writes straight to disk so the last lines survive a hard crash (the engine's rgl log is buffered).
    // C:\ProgramData\Mount and Blade II Bannerlord\logs\CommanderBattle_<pid>.log
    //
    // Write is for what matters when reading a session back: map and round changes, players taking formations and
    // picking troops, failures, exceptions from our code. Detail is for diagnostics (spawn layout, troop counts, team
    // AI, periodic status) and is only written while Detailed is on.
    public static class CommanderBattleLog
    {
        // Turn on when diagnosing spawning, the layout sync or the team AI.
        public static bool Detailed = false;

        private const int MaxFirstChanceExceptions = 300;

        private static readonly object Sync = new object();

        private static string _path;

        private static int _firstChanceCount;

        [ThreadStatic]
        private static bool _inFirstChance;

        public static void Initialize()
        {
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Mount and Blade II Bannerlord", "logs");
                Directory.CreateDirectory(directory);
                _path = Path.Combine(directory, $"CommanderBattle_{Process.GetCurrentProcess().Id}.log");
                AppDomain.CurrentDomain.UnhandledException += (sender, args) => Write("UNHANDLED: " + args.ExceptionObject);
                AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
                Write("CommanderBattle loaded");
            }
            catch
            {
                _path = null;
            }
        }

        public static void Write(string message)
        {
            if (_path == null)
            {
                return;
            }
            lock (Sync)
            {
                try
                {
                    File.AppendAllText(_path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                }
                catch
                {
                }
            }
        }

        public static void Detail(string message)
        {
            if (Detailed)
            {
                Write(message);
            }
        }

        // The game and the lobby throw and catch exceptions all the time; only those passing through our code
        // (CommanderBattle, RBM.Multiplayer, RBM's linked source) are worth reading. The logger's own frame is skipped.
        private static bool IsFromOurCode(string stackTrace)
        {
            foreach (string line in stackTrace.Split('\n'))
            {
                string frame = line.TrimStart();
                if (frame.Contains("CommanderBattleLog."))
                {
                    continue;
                }
                if (frame.StartsWith("at CommanderBattle.") || frame.StartsWith("at RBM"))
                {
                    return true;
                }
            }
            return false;
        }

        // Each distinct exception (type + call stack) is logged once, so a stock error repeating every frame
        // cannot use up the budget before something new happens.
        private static readonly System.Collections.Generic.HashSet<string> SeenExceptions = new System.Collections.Generic.HashSet<string>();

        private static void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs args)
        {
            if (_inFirstChance)
            {
                return;
            }
            _inFirstChance = true;
            try
            {
                string stackTrace = Environment.StackTrace;
                if (!IsFromOurCode(stackTrace))
                {
                    return;
                }
                lock (Sync)
                {
                    if (!SeenExceptions.Add(args.Exception.GetType().FullName + stackTrace))
                    {
                        return;
                    }
                }
                if (Interlocked.Increment(ref _firstChanceCount) > MaxFirstChanceExceptions)
                {
                    return;
                }
                Write($"Exception {args.Exception.GetType().FullName}: {args.Exception.Message}{Environment.NewLine}{stackTrace}");
            }
            finally
            {
                _inFirstChance = false;
            }
        }
    }
}
