using System;
using System.IO;
using System.Text;

namespace CKeyViewer.Core
{
    /// <summary>轻量诊断日志（写入 exe 同目录 ckv_error.log）。</summary>
    public static class Diag
    {
        public static bool Enabled = true;

        private static readonly object Gate = new object();

        public static string LogPath { get; } =
            Path.Combine(AppContext.BaseDirectory, "ckv_error.log");

        public static void Log(string message)
        {
            if (!Enabled) return;
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(LogPath,
                        DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // 忽略
            }
        }

        public static void Reset()
        {
            try { File.Delete(LogPath); } catch { }
        }
    }
}
