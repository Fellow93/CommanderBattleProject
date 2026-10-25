using System;
using System.Diagnostics;
using System.IO;

namespace RBM.Multiplayer
{
    // Writes straight to disk, next to Commander Battle's log:
    // C:\ProgramData\Mount and Blade II Bannerlord\logs\RBM.Multiplayer_<pid>.log
    public static class RbmMpLog
    {
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
    }
}
