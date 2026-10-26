using System;
using System.Diagnostics;
using System.IO;

namespace RBM.Multiplayer
{
    // Writes straight to disk, next to Commander Battle's log:
    // C:\ProgramData\Mount and Blade II Bannerlord\logs\RBM.Multiplayer_<pid>.log
    //
    // Write is for the patch summary and failures. Detail is for diagnostics (tactics, terrain, formation status,
    // Frontline and archer counters) and is only written while Detailed is on.
    public static class RbmMpLog
    {
        // Turn on when diagnosing RBM's AI on the server.
        public static bool Detailed = false;

        private static readonly object Sync = new object();

        private static string _path;

        public static void Initialize()
        {
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Mount and Blade II Bannerlord", "logs");
                Directory.CreateDirectory(directory);
                _path = Path.Combine(directory, $"RBM.Multiplayer_{Process.GetCurrentProcess().Id}.log");
                Write("RBM.Multiplayer loaded");
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
    }
}
