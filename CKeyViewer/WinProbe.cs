using System;
using System.Collections.Generic;
using System.Text;
using CKeyViewer.Adofai;
using CKeyViewer.Native;

namespace CKeyViewer
{
    /// <summary>
    /// 「为什么没吸附到游戏窗口 / 为什么连接失败」的现场取证工具。
    ///
    /// 这两件事原来都是**静默失败**：窗口认不出来就不吸附、进程挑错了就连不上，
    /// 界面上只会显示一句没头没尾的「连接失败」，日志里也什么都没写。
    /// 这里把判定过程中用到的每一条原始信息都摊开：
    /// 有几个 ADOFAI 进程、有多少可见顶层窗口、每个候选的 pid / 类名 / 标题 / 矩形，
    /// 最后是 <see cref="Win32.PickGameWindow"/> 的判定结果和原因。
    ///
    /// 用法（**开着游戏跑**，窗口化 / 全屏各跑一次）：
    /// <c>CKeyViewer.exe --winprobe [输出文件]</c>
    /// </summary>
    internal static class WinProbe
    {
        public static int Run(string outPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CKeyViewer window probe");
            sb.AppendLine("time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("elevated(admin): " + Admin.IsElevated());
            sb.AppendLine();

            int[] pids = AdofaiReader.ProcessIds();
            sb.AppendLine("---- ADOFAI processes ----");
            sb.AppendLine(pids.Length == 0
                ? "  (none)  —— 进程名匹配 \"" + AdofaiReader.ProcName + "\""
                : "  " + string.Join(", ", pids));
            // 逐个体检：游戏关掉之后经常留下一个同名的 32KB 残留进程，
            // 对它 OpenProcess 会得到 err=5，看着像权限问题，其实是它早就退出了。
            foreach (int pid in pids)
            {
                bool exited;
                string extra = "";
                try
                {
                    using (System.Diagnostics.Process pr = System.Diagnostics.Process.GetProcessById(pid))
                    {
                        exited = pr.HasExited;
                        if (!exited)
                        {
                            try { extra = " 内存=" + (pr.WorkingSet64 / 1024) + "KB"; } catch { }
                        }
                    }
                }
                catch { exited = true; }

                sb.AppendLine("  pid=" + pid + (exited ? "  已退出（残留进程，会被跳过）" : "  存活" + extra));
            }
            sb.AppendLine();

            sb.AppendLine("---- attach dry run ----");
            try
            {
                using (var r = new AdofaiReader())
                {
                    bool attached = r.Attach();
                    sb.AppendLine(attached
                        ? "  成功：pid=" + r.ProcessId
                        : "  失败：" + r.LastError);
                }
            }
            catch (Exception ex) { sb.AppendLine("  异常：" + ex.Message); }
            sb.AppendLine();

            List<Win32.WinInfo> wins = Win32.ListVisibleTopLevelWindows();
            sb.AppendLine("---- visible top-level windows (" + wins.Count + ") ----");
            foreach (Win32.WinInfo w in wins)
            {
                sb.AppendLine(string.Format(
                    "  hwnd=0x{0:X8} pid={1,-7} {2,-34} \"{3}\"  ({4},{5})-({6},{7}) {8}x{9}{10}",
                    (long)w.Hwnd & 0xFFFFFFFF, w.Pid, Trim(w.Class, 34), Trim(w.Title, 30),
                    w.Left, w.Top, w.Right, w.Bottom, w.Width, w.Height,
                    w.Iconic ? "  [最小化]" : ""));
            }
            sb.AppendLine();

            // 只看「可能是游戏」的那几个，省得在几十个窗口里翻
            sb.AppendLine("---- candidates ----");
            int self = Environment.ProcessId;
            foreach (Win32.WinInfo w in wins)
            {
                bool byPid = Array.IndexOf(pids, w.Pid) >= 0;
                bool byCls = string.Equals(w.Class, "UnityWndClass", StringComparison.Ordinal);
                bool byTitle = w.Title.IndexOf("A Dance of Fire and Ice", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!byPid && !byCls && !byTitle) continue;
                sb.AppendLine(string.Format(
                    "  {0}{1}{2} pid={3} cls={4} \"{5}\" {6}x{7} area={8}{9}",
                    byPid ? "[PID] " : "", byCls ? "[Unity] " : "", byTitle ? "[标题] " : "",
                    w.Pid, w.Class, w.Title, w.Width, w.Height, w.Area,
                    w.Area < Win32.MinGameWindowArea ? "  (小于最小面积，会被跳过)" : ""));
            }
            sb.AppendLine();

            sb.AppendLine("---- pick result ----");
            bool ok = Win32.PickGameWindow(wins, "A Dance of Fire and Ice",
                                           AdofaiReader.LiveProcessIds(), self,
                                           out Win32.WinInfo hit, out bool minimized, out string reason);
            if (ok)
            {
                sb.AppendLine(string.Format(
                    "  命中：pid={0} cls={1} \"{2}\"  ({3},{4})-({5},{6}) {7}x{8}",
                    hit.Pid, hit.Class, hit.Title, hit.Left, hit.Top, hit.Right, hit.Bottom,
                    hit.Width, hit.Height));
            }
            else
            {
                sb.AppendLine("  未命中" + (minimized ? "（游戏最小化）" : "") + "：" + reason);
            }

            string text = sb.ToString();
            Console.Write(text);
            try { System.IO.File.WriteAllText(outPath, text, Encoding.UTF8); }
            catch (Exception ex) { Console.WriteLine("write " + outPath + ": " + ex.Message); }

            return ok ? 0 : 1;
        }

        private static string Trim(string s, int n)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n - 1) + "…");
    }
}
