using System;
using System.IO;
using System.Text;

namespace NeonHorizonWallpaper
{
    internal static class Logger
    {
        private static readonly object Sync = new object();
        private static string _logPath;

        public static string LogPath
        {
            get
            {
                if (_logPath == null)
                {
                    string localDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "NeonHorizonWallpaper");
                    _logPath = Path.Combine(localDir, "neon_horizon.log");
                }
                return _logPath;
            }
        }

        public static void Reset()
        {
            lock (Sync)
            {
                try
                {
                    string dir = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(LogPath, string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] === Neon Horizon Wallpaper Started ==={1}", DateTime.Now, Environment.NewLine), Encoding.UTF8);
                }
                catch
                {
                }
            }
        }

        public static void Info(string msg)
        {
            Write("INFO", msg);
        }

        public static void Error(string msg)
        {
            Write("ERR ", msg);
        }

        private static void Write(string level, string msg)
        {
            lock (Sync)
            {
                try
                {
                    string dir = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    string line = string.Format("[{0:HH:mm:ss.fff}] [{1}] {2}{3}", DateTime.Now, level, msg, Environment.NewLine);
                    File.AppendAllText(LogPath, line, Encoding.UTF8);
                }
                catch
                {
                }
            }
        }
    }
}
