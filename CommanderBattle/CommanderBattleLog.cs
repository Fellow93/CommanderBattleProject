using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace CommanderBattle
{
    // Writes straight to disk so the last lines survive a hard crash (the engine's rgl log is buffered).
    // C:\ProgramData\Mount and Blade II Bannerlord\logs\CommanderBattle_<pid>.log
    public static class CommanderBattleLog
    {
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

        // Lobby/HTTP traffic throws and swallows these routinely; they are not ours.
        private static bool IsNetworkNoise(Exception exception)
        {
            return exception is IOException || exception is OperationCanceledException || exception is System.Net.Sockets.SocketException;
        }

        // Each distinct exception (type + call stack) is logged once, so a stock error repeating every frame
        // cannot use up the budget before something new happens.
        private static readonly System.Collections.Generic.HashSet<string> SeenExceptions = new System.Collections.Generic.HashSet<string>();

        private static void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs args)
        {
            if (_inFirstChance || IsNetworkNoise(args.Exception))
            {
                return;
            }
            _inFirstChance = true;
            try
            {
                string stackTrace = Environment.StackTrace;
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
                Write($"first-chance {args.Exception.GetType().FullName}: {args.Exception.Message}{Environment.NewLine}{stackTrace}");
            }
            finally
            {
                _inFirstChance = false;
            }
        }
    }
}
