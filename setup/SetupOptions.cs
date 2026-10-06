using System;
using System.IO;

namespace CKeyViewer.Setup
{
    /// <summary>命令行选项。GUI 模式下由窗体补充，静默模式全靠它。</summary>
    internal sealed class SetupOptions
    {
        public bool Uninstall;          // --uninstall
        public bool Silent;             // --silent            全程无界面
        public bool NoDesktop;          // --no-desktop        不建桌面快捷方式
        public bool NoStartMenu;        // --no-start-menu     不建开始菜单项
        public bool NoRunAsAdmin;       // --no-elevate        不写 RUNASADMIN 标记
        public bool KeepConfig;         // --keep-config       卸载时保留 config/
        public bool Launch;             // --launch            装完直接启动
        public string Dir;              // --dir=<路径>

        public static SetupOptions Parse(string[] args)
        {
            var o = new SetupOptions();
            if (args == null) return o;

            foreach (string raw in args)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string a = raw.Trim();
                string lower = a.ToLowerInvariant();

                if (lower == "--uninstall" || lower == "-u" || lower == "/uninstall") { o.Uninstall = true; continue; }
                if (lower == "--silent" || lower == "-s" || lower == "/silent") { o.Silent = true; continue; }
                if (lower == "--quiet" || lower == "--verysilent") { o.Silent = true; continue; }
                if (lower == "--no-desktop") { o.NoDesktop = true; continue; }
                if (lower == "--no-start-menu") { o.NoStartMenu = true; continue; }
                if (lower == "--no-elevate" || lower == "--no-run-as-admin") { o.NoRunAsAdmin = true; continue; }
                if (lower == "--keep-config") { o.KeepConfig = true; continue; }
                if (lower == "--launch" || lower == "--run") { o.Launch = true; continue; }

                if (lower.StartsWith("--dir=") || lower.StartsWith("/dir="))
                {
                    o.Dir = a.Substring(a.IndexOf('=') + 1).Trim().Trim('"');
                    continue;
                }
                // 裸路径参数当成安装目录，方便 `setup.exe D:\Apps\CKeyViewer`
                if (!a.StartsWith("-") && !a.StartsWith("/") && a.IndexOf('\\') >= 0)
                    o.Dir = a.Trim('"');
            }

            if (string.IsNullOrWhiteSpace(o.Dir)) o.Dir = AppInfo.DefaultInstallDir;
            else o.Dir = Path.GetFullPath(o.Dir);

            return o;
        }
    }
}
