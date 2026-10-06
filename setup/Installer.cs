using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using Microsoft.Win32;

namespace CKeyViewer.Setup
{
    /// <summary>安装流程。全部同步执行，进度通过回调抛给界面。</summary>
    internal static class Installer
    {
        private const int BufferSize = 1 << 20;   // 1 MiB

        public static int Run(SetupOptions o, Progress report)
        {
            if (report == null) report = delegate { };
            try
            {
                string dir = o.Dir;
                if (string.IsNullOrWhiteSpace(dir))
                    dir = AppInfo.DefaultInstallDir;
                dir = Path.GetFullPath(dir);

                if (!IsSaneDir(dir))
                {
                    report(0, "拒绝安装到「" + dir + "」——这个位置太靠近盘根目录了");
                    return 1;
                }

                string appExe = Path.Combine(dir, AppInfo.AppExeName);

                report(2, "准备安装目录…");
                Directory.CreateDirectory(dir);
                EnsureWritable(dir);

                report(8, "结束正在运行的 CKeyViewer…");
                int killed = Processes.StopInstalledApp(dir);
                if (killed > 0) report(12, "已结束 " + killed + " 个正在运行的实例");

                report(15, "解包主程序（约 150 MB，首次需要几秒）…");
                ExtractPayload(appExe);

                report(72, "写入卸载程序…");
                string uninstaller = Path.Combine(dir, AppInfo.UninstallerName);
                CopySelf(uninstaller);

                if (!o.NoDesktop || !o.NoStartMenu)
                {
                    report(80, "创建快捷方式…");
                    CreateShortcuts(o, dir, appExe, uninstaller);
                }

                if (!o.NoRunAsAdmin)
                {
                    report(88, "设置「以管理员身份运行」…");
                    SetRunAsAdmin(appExe, true);
                }

                report(93, "写入卸载信息…");
                WriteUninstallEntry(dir, uninstaller, appExe);

                TryMigrateConfig(o, dir, report);

                report(100, "安装完成");
                return 0;
            }
            catch (UnauthorizedAccessException ex)
            {
                report(0, "没有写入权限：" + ex.Message);
                return 2;
            }
            catch (SecurityException ex)
            {
                report(0, "被安全策略拦住了：" + ex.Message);
                return 2;
            }
            catch (Exception ex)
            {
                report(0, "安装失败：" + ex.GetType().Name + ": " + ex.Message);
                return 3;
            }
        }

        // ── 目录校验 ───────────────────────────────────────────────

        /// <summary>挡掉「C:\」「D:\」以及 Windows 目录这种会把系统搞坏的目标。</summary>
        private static bool IsSaneDir(string dir)
        {
            string full = Path.GetFullPath(dir).TrimEnd('\\');
            if (full.Length <= 3) return false;                         // C:\
            string root = Path.GetPathRoot(full);
            if (string.Equals(full, root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return false;

            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(win) &&
                full.StartsWith(Path.GetFullPath(win).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static void EnsureWritable(string dir)
        {
            string probe = Path.Combine(dir, ".ckv-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var fs = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                fs.WriteByte(0);
            File.Delete(probe);
        }

        // ── 解包 ──────────────────────────────────────────────────

        private static void ExtractPayload(string destExe)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream src = asm.GetManifestResourceStream(AppInfo.PayloadResource))
            {
                if (src == null)
                    throw new InvalidOperationException(
                        "安装包里没有找到主程序（资源 " + AppInfo.PayloadResource + "）。" +
                        "请确认这是用 tools/build_setup.sh 打出来的完整安装包。");

                string part = destExe + ".part";
                using (var dst = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                {
                    byte[] buf = new byte[BufferSize];
                    int n;
                    while ((n = src.Read(buf, 0, buf.Length)) > 0)
                        dst.Write(buf, 0, n);
                }

                if (File.Exists(destExe))
                {
                    try { File.Delete(destExe); }
                    catch (IOException) { Native.DeleteOnReboot(destExe); throw; }
                }
                File.Move(part, destExe);
            }
        }

        private static void CopySelf(string dest)
        {
            string self = Environment.ProcessPath;
            if (string.IsNullOrEmpty(self) || !File.Exists(self))
                throw new InvalidOperationException("取不到安装程序自身的路径。");

            if (string.Equals(Path.GetFullPath(self), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                return;

            string part = dest + ".part";
            File.Copy(self, part, true);
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(part, dest);
        }

        // ── 快捷方式 ──────────────────────────────────────────────

        private static void CreateShortcuts(SetupOptions o, string dir, string appExe, string uninstaller)
        {
            if (!o.NoDesktop)
            {
                Shortcut.Create(Path.Combine(AppInfo.DesktopDir, AppInfo.ShortcutName),
                    appExe, dir, appExe, AppInfo.DisplayName);
            }

            if (!o.NoStartMenu)
            {
                Directory.CreateDirectory(AppInfo.StartMenuDir);
                Shortcut.Create(Path.Combine(AppInfo.StartMenuDir, AppInfo.ShortcutName),
                    appExe, dir, appExe, AppInfo.DisplayName);
                Shortcut.Create(Path.Combine(AppInfo.StartMenuDir, "卸载 " + AppInfo.ProductName + ".lnk"),
                    uninstaller, dir, uninstaller, "卸载 " + AppInfo.DisplayName);
            }
        }

        // ── 以管理员身份运行 ──────────────────────────────────────

        /// <summary>
        /// 把「以管理员身份运行此程序」写进当前用户的兼容性标记。
        /// 这是 .lnk 表达不了的 —— 它是**按 exe 路径**绑的机器级 / 用户级设置，
        /// 所以快捷方式与直接双击 exe 都会自动弹 UAC 提权。
        /// </summary>
        public static void SetRunAsAdmin(string appExe, bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AppInfo.AppCompatLayersKey, true))
                {
                    if (key == null) return;
                    if (enable) key.SetValue(appExe, AppInfo.AppCompatRunAsAdmin, RegistryValueKind.String);
                    else if (key.GetValue(appExe) != null) key.DeleteValue(appExe, false);
                }
                Native.RefreshShell();
            }
            catch { /* 标记失败不致命：程序启动时自己还有一道管理员检查 */ }
        }

        // ── 卸载信息 ──────────────────────────────────────────────

        private static void WriteUninstallEntry(string dir, string uninstaller, string appExe)
        {
            long sizeKb = 0;
            try { sizeKb = new FileInfo(appExe).Length / 1024; } catch { }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AppInfo.UninstallKey, true))
            {
                if (key == null) return;
                key.SetValue("DisplayName", AppInfo.DisplayName, RegistryValueKind.String);
                key.SetValue("DisplayVersion", AppInfo.Version, RegistryValueKind.String);
                key.SetValue("Publisher", AppInfo.Publisher, RegistryValueKind.String);
                key.SetValue("InstallLocation", dir, RegistryValueKind.String);
                key.SetValue("DisplayIcon", appExe + ",0", RegistryValueKind.String);
                key.SetValue("URLInfoAbout", AppInfo.Url, RegistryValueKind.String);
                key.SetValue("UninstallString", "\"" + uninstaller + "\" --uninstall", RegistryValueKind.String);
                key.SetValue("QuietUninstallString", "\"" + uninstaller + "\" --uninstall --silent", RegistryValueKind.String);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                if (sizeKb > 0) key.SetValue("EstimatedSize", (int)sizeKb, RegistryValueKind.DWord);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
            }
        }

        // ── config 迁移 ───────────────────────────────────────────

        /// <summary>
        /// 如果 setup.exe 隔壁放着一份 <c>config/</c>（例如从旧目录拷过来），就顺手带进安装目录。
        /// 同名的旧文件先备份成 <c>*.before-install</c>，一个字节都不覆盖掉。
        /// </summary>
        private static void TryMigrateConfig(SetupOptions o, string dir, Progress report)
        {
            try
            {
                string srcConfig = Path.Combine(AppInfo.SetupDir, "config");
                if (!Directory.Exists(srcConfig)) return;

                report(96, "迁移 setup.exe 旁边的 config/…");
                string dstConfig = Path.Combine(dir, "config");
                Directory.CreateDirectory(dstConfig);
                CopyTree(srcConfig, dstConfig, ".before-install");
            }
            catch (Exception ex)
            {
                report(98, "配置迁移跳过（" + ex.Message + "）");
            }
        }

        private static void CopyTree(string src, string dst, string backupSuffix)
        {
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src))
            {
                string target = Path.Combine(dst, Path.GetFileName(f));
                if (File.Exists(target))
                    File.Move(target, target + backupSuffix);
                File.Copy(f, target, true);
            }
            foreach (string d in Directory.GetDirectories(src))
                CopyTree(d, Path.Combine(dst, Path.GetFileName(d)), backupSuffix);
        }
    }
}
