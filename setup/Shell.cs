using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CKeyViewer.Setup
{
    /// <summary>
    /// 快捷方式创建走 COM 的 <c>IShellLinkW</c> + <c>IPersistFile</c> ——
    /// 比 `WScript.Shell` 可靠（不依赖 Windows Script Host 是否被策略关掉），也省掉一个动态 COM 依赖。
    /// </summary>
    internal static class Shortcut
    {
        public static void Create(string lnkPath, string target, string workingDir,
                                  string iconPath, string description)
        {
            string dir = Path.GetDirectoryName(lnkPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            IShellLinkW link = null;
            IPersistFile file = null;
            try
            {
                link = (IShellLinkW)new ShellLinkClass();
                link.SetPath(target);
                if (!string.IsNullOrEmpty(workingDir)) link.SetWorkingDirectory(workingDir);
                if (!string.IsNullOrEmpty(description)) link.SetDescription(description);
                if (!string.IsNullOrEmpty(iconPath)) link.SetIconLocation(iconPath, 0);

                file = (IPersistFile)link;
                file.Save(lnkPath, true);
            }
            finally
            {
                if (file != null) Marshal.ReleaseComObject(file);
                if (link != null) Marshal.ReleaseComObject(link);
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLinkClass { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch,
                         IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
                                 int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
                      [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }
    }

    /// <summary>与内核 / 外壳打交道的一小撮 P/Invoke。</summary>
    internal static class Native
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileExW(string lpExistingFileName, string lpNewFileName, uint dwFlags);

        private const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;

        /// <summary>删不掉的文件（正被占用）挂到下次重启时删。</summary>
        public static void DeleteOnReboot(string path)
        {
            try { MoveFileExW(path, null, MOVEFILE_DELAY_UNTIL_REBOOT); } catch { }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        /// <summary>通知外壳刷新图标缓存 —— 刚写完 RUNASADMIN 标记后，别让旧图标 / 旧关联留着。</summary>
        public static void RefreshShell()
        {
            const int SHCNE_ASSOCCHANGED = 0x08000000;
            const uint SHCNF_IDLIST = 0x0000;
            try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); } catch { }
        }
    }

    /// <summary>进程相关的小工具。</summary>
    internal static class Processes
    {
        /// <summary>
        /// 结束所有从 <paramref name="installDir"/> 跑起来的 CKeyViewer。
        /// 覆盖安装前必须先关掉，否则目标 exe 被占用写不进去。
        /// </summary>
        public static int StopInstalledApp(string installDir)
        {
            int killed = 0;
            string target = Path.GetFullPath(installDir).TrimEnd('\\') + "\\";

            Process[] all;
            try { all = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppInfo.AppExeName)); }
            catch { return 0; }

            foreach (Process p in all)
            {
                try
                {
                    string path = p.MainModule != null ? p.MainModule.FileName : null;
                    if (path == null) continue;
                    if (!Path.GetFullPath(path).StartsWith(target, StringComparison.OrdinalIgnoreCase)) continue;

                    p.Kill();
                    if (!p.WaitForExit(5000))
                    {
                        // 提权进程可能杀不动（我们不是管理员），交给上层提示用户手动关
                        continue;
                    }
                    killed++;
                }
                catch { }
            }
            return killed;
        }

        public static void Launch(string exePath)
        {
            try
            {
                var psi = new ProcessStartInfo(exePath) { UseShellExecute = true };
                psi.WorkingDirectory = Path.GetDirectoryName(exePath);
                Process.Start(psi);
            }
            catch { }
        }
    }

    /// <summary>安装进度回调：0~100 百分比 + 一行人类可读的状态。</summary>
    internal delegate void Progress(int percent, string status);
}
