using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace CKeyViewer.Setup
{
    /// <summary>
    /// 卸载流程。
    ///
    /// 绕不开的问题：删安装目录的 exe 自己就住在安装目录里，正跑着的时候删不掉自己。
    /// 常见解法是「把自己拷到 %TEMP% 再删」—— 但我们的 exe 是 150 MB 的自包含单文件，
    /// 拷一份又慢又占地方。
    ///
    /// 这里改用**延迟批处理**：写一个小 .bat 到 %TEMP%，分离启动它，
    /// 然后本进程立刻退出释放锁。bat 先睡两秒（等我们退干净），
    /// 再反复尝试删除，直到自己被删掉为止。
    /// </summary>
    internal static class Uninstaller
    {
        /// <summary>从注册表读安装目录；读不到就退回「本 exe 所在目录」。</summary>
        public static string DetectInstallDir()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppInfo.UninstallKey))
                {
                    string dir = key != null ? key.GetValue("InstallLocation") as string : null;
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                        return Path.GetFullPath(dir);
                }
            }
            catch { }

            try
            {
                string self = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(self)) return Path.GetDirectoryName(Path.GetFullPath(self));
            }
            catch { }
            return null;
        }

        /// <summary>执行卸载。返回 0 表示「善后脚本已启动」。</summary>
        public static int Begin(bool keepConfig, Progress report)
        {
            if (report == null) report = delegate { };

            try
            {
                string dir = DetectInstallDir();
                if (string.IsNullOrWhiteSpace(dir))
                {
                    report(0, "找不到 CKeyViewer 的安装目录，可能已经被卸载了。");
                    return 1;
                }
                dir = Path.GetFullPath(dir).TrimEnd('\\');
                if (dir.Length <= 3)
                {
                    report(0, "安装目录异常（" + dir + "），拒绝继续。");
                    return 1;
                }

                string appExe = Path.Combine(dir, AppInfo.AppExeName);

                report(10, "结束正在运行的 CKeyViewer…");
                Processes.StopInstalledApp(dir);

                report(30, "删除快捷方式…");
                TryDelete(Path.Combine(AppInfo.DesktopDir, AppInfo.ShortcutName));
                TryDelete(Path.Combine(AppInfo.StartMenuDir, AppInfo.ShortcutName));
                TryDelete(Path.Combine(AppInfo.StartMenuDir, "卸载 " + AppInfo.ProductName + ".lnk"));
                TryDeleteEmptyDir(AppInfo.StartMenuDir);

                report(50, "清除「以管理员身份运行」标记…");
                Installer.SetRunAsAdmin(appExe, false);

                report(70, "清除卸载信息…");
                try { Registry.CurrentUser.DeleteSubKeyTree(AppInfo.UninstallKey, false); }
                catch { }

                report(88, "安排文件清理…");
                ScheduleCleanup(dir, keepConfig);

                report(100, keepConfig
                    ? "卸载完成 —— 程序文件会自动删除，config/ 已保留"
                    : "卸载完成 —— 程序文件会在本窗口关闭后自动删除");
                return 0;
            }
            catch (Exception ex)
            {
                report(0, "卸载失败：" + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        // ── 延迟清理 ──────────────────────────────────────────────

        private static void ScheduleCleanup(string dir, bool keepConfig)
        {
            string bat = Path.Combine(Path.GetTempPath(),
                "ckv-uninstall-" + Guid.NewGuid().ToString("N") + ".bat");
            File.WriteAllText(bat, BuildScript(dir, keepConfig), new UTF8Encoding(false));

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                // 隐藏窗口跑；批处理自己负责等我们退出后再动手
                Arguments = "/c \"\"" + bat + "\"\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
        }

        private static string BuildScript(string dir, bool keepConfig)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("chcp 65001 >nul 2>nul");
            sb.AppendLine("rem 等 2 秒，让卸载程序退干净、把文件锁放掉");
            sb.AppendLine("ping -n 3 127.0.0.1 >nul");
            sb.AppendLine("set /a n=0");
            sb.AppendLine(":retry");

            if (keepConfig)
            {
                // 按绝对路径枚举，绝不依赖工作目录 —— 免得 cd 失败把别处删了
                sb.AppendLine("for /d %%i in (\"" + dir + "\\*\") do "
                            + "if /i not \"%%~nxi\"==\"config\" rd /s /q \"%%i\" 2>nul");
                sb.AppendLine("for %%i in (\"" + dir + "\\*\") do del /f /q \"%%i\" 2>nul");
            }
            else
            {
                sb.AppendLine("rd /s /q \"" + dir + "\" 2>nul");
            }

            sb.AppendLine("rem 自己还没被删掉 → 说明锁还没放，继续等（最多 60 轮 ≈ 2 分钟）");
            sb.AppendLine("if not exist \"" + Path.Combine(dir, AppInfo.UninstallerName) + "\" goto end");
            sb.AppendLine("set /a n+=1");
            sb.AppendLine("if %n% GEQ 60 goto end");
            sb.AppendLine("ping -n 2 127.0.0.1 >nul");
            sb.AppendLine("goto retry");

            if (keepConfig)
            {
                sb.AppendLine(":end");
                sb.AppendLine("rem 保留 config 时把空壳目录也留着，方便用户自己看");
            }
            else
            {
                sb.AppendLine(":end");
                sb.AppendLine("if not exist \"" + dir + "\" goto selfdel");
                sb.AppendLine("rd /s /q \"" + dir + "\" 2>nul");
            }

            sb.AppendLine(":selfdel");
            sb.AppendLine("del /f /q \"%~f0\" 2>nul");
            return sb.ToString();
        }

        // ── 小工具 ────────────────────────────────────────────────

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void TryDeleteEmptyDir(string path)
        {
            try
            {
                if (Directory.Exists(path) &&
                    Directory.GetFiles(path).Length == 0 &&
                    Directory.GetDirectories(path).Length == 0)
                    Directory.Delete(path);
            }
            catch { }
        }
    }
}
