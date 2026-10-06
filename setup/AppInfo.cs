using System;
using System.IO;

namespace CKeyViewer.Setup
{
    /// <summary>产品常量 —— 安装位置、注册表键名、快捷方式名的唯一来源。</summary>
    internal static class AppInfo
    {
        public const string ProductName = "CKeyViewer";
        public const string DisplayName = "CKeyViewer 按键可视化覆盖层";
        public const string Version = "1.0.0";
        public const string Publisher = "DXBbyd";
        public const string Author = "DXBbyd";
        public const string AuthorQQ = "3157037483";
        public const string Url = "https://github.com/DXBbyd/CKeyViewer";

        public const string AppExeName = "CKeyViewer.exe";
        public const string UninstallerName = "uninstall.exe";
        public const string ShortcutName = "CKeyViewer.lnk";

        /// <summary>卸载信息的注册表位置（HKCU，用不到管理员权限）。</summary>
        public const string UninstallKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CKeyViewer";

        /// <summary>
        /// 「以管理员身份运行」的兼容性标记就存在这里。
        /// 值名 = exe 完整路径，值数据 = "~ RUNASADMIN"。
        /// </summary>
        public const string AppCompatLayersKey =
            @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

        public const string AppCompatRunAsAdmin = "~ RUNASADMIN";

        /// <summary>安装程序里嵌入的主程序包名。</summary>
        public const string PayloadResource = "CKeyViewerSetup.payload";

        /// <summary>
        /// 默认安装目录：<c>%LOCALAPPDATA%\Programs\CKeyViewer</c>。
        /// 刻意**不**装进 Program Files —— 主程序运行时才需要提权（为了读管理员进程的按键），
        /// 安装本身不需要管理员，放用户目录能省掉一次 UAC。
        /// </summary>
        public static string DefaultInstallDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", ProductName);
            }
        }

        public static string StartMenuDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), ProductName);
            }
        }

        public static string DesktopDir
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); }
        }

        /// <summary>setup.exe 自己所在的目录 —— 找隔壁的 config/ 用。</summary>
        public static string SetupDir
        {
            get
            {
                try { return Path.GetDirectoryName(Environment.ProcessPath) ?? "."; }
                catch { return "."; }
            }
        }
    }
}
