using System;
using System.Windows;

namespace CKeyViewer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // 无界面自检：验证按键捕获的扫描逻辑（能绑 Shift / 鼠标键），跑完就退
            if (args != null && args.Length > 0 &&
                string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
            {
                Native.Win32.AttachParentConsole();
                string outPath = args.Length > 1
                    ? args[1]
                    : System.IO.Path.Combine(AppContext.BaseDirectory, "ckv_selftest.txt");
                Environment.ExitCode = SelfTest.Run(outPath);
                return;
            }

            Core.Diag.Reset();
            Core.Diag.Log("=== start ===");

            // 必须管理员运行：非管理员时读不到管理员进程（例如管理员启动的游戏）的按键。
            // 这里**主动检查 + 弹窗 + 结束进程**，而不是靠清单里的 requireAdministrator ——
            // 清单方式只会弹系统 UAC，看不到我们的说明，被拒也就直接退了。
            if (!Admin.EnsureElevated())
            {
                Core.Diag.Log("not elevated -> " + Admin.RequireMessage + " -> exit");
                Environment.Exit(1);
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Core.Diag.Log("AppDomain: " + e.ExceptionObject);

            try
            {
                var app = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                app.DispatcherUnhandledException += (s, e) =>
                {
                    Core.Diag.Log("Dispatcher: " + e.Exception);
                    e.Handled = true;
                };

                var store = Core.KvProfileStore.CreateDefault();
                Core.Diag.Log("config root = " + store.Root);

                var window = new OverlayWindow();
                var host = new KvHost(window, store);

                // 设置窗口按需创建，关闭时只隐藏不销毁。
                // 传 -1 保持当前标签页，传具体下标则跳过去（托盘的「关于」用 11）。
                KvSettingsWindow settings = null;
                Action<int> openSettings = tab =>
                {
                    try
                    {
                        if (settings == null)
                        {
                            settings = new KvSettingsWindow(host);
                            settings.Closed += (s, e) => settings = null;
                            settings.Show();
                            Core.Diag.Log("settings window shown " + settings.ActualWidth + "x" + settings.ActualHeight
                                          + " @ " + settings.Left + "," + settings.Top);
                        }
                        else
                        {
                            if (!settings.IsVisible) settings.Show();
                            settings.Activate();
                            settings.Focus();
                        }

                        if (tab >= 0) settings.SelectTab(tab);
                    }
                    catch (Exception ex)
                    {
                        Core.Diag.Log("open settings: " + ex);
                    }
                };

                var hotkeys = new KvHotkeys(window);
                hotkeys.Toggle += () => host.ToggleVisible();
                hotkeys.Reset += () => host.ResetCounts();
                hotkeys.Settings += () => openSettings(-1);
                hotkeys.NextProfile += () => host.SwitchNextProfile();
                hotkeys.ToggleLayout += () => host.ToggleLayoutMode();

                var tray = new KvTray(host, openSettings, () => app.Shutdown());

                app.Exit += (s, e) =>
                {
                    try { hotkeys.Dispose(); } catch (Exception ex) { Core.Diag.Log("hotkeys: " + ex); }
                    try { tray.Dispose(); } catch (Exception ex) { Core.Diag.Log("tray: " + ex); }
                    try { host.Stop(); } catch (Exception ex) { Core.Diag.Log("stop: " + ex); }
                };

                // 必须先把窗口显示出来，HWND 才会真正创建 —— 全局热键要注册在它上面。
                window.Show();
                host.Start();
                hotkeys.Attach();

                Core.Diag.Log("config = " + store.ProfilePath(store.CurrentProfile));

#if DEBUG
                // 截图 / 调试用：直接打开设置面板，省得发模拟按键去抢用户的键盘。
                // 值 = 标签页下标，-1 表示沿用上次记住的那一页。Release 里整段被编译掉。
                string autoSettings = Environment.GetEnvironmentVariable("CKV_OPEN_SETTINGS");
                if (!string.IsNullOrEmpty(autoSettings))
                {
                    int tab;
                    if (!int.TryParse(autoSettings, out tab)) tab = -1;
                    var t = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(4)
                    };
                    t.Tick += (s, e) =>
                    {
                        t.Stop();
                        Core.Diag.Log("auto-open settings tab=" + tab);
                        openSettings(tab);
                    };
                    t.Start();
                }

                // 截图 / 调试用：直接进布局模式（覆盖层 + 信息层都画出来）。
                // 不用发 Ctrl+Alt+L —— 用户自己那份实例会先抢到这个热键。
                string autoLayout = Environment.GetEnvironmentVariable("CKV_LAYOUT");
                if (!string.IsNullOrEmpty(autoLayout))
                {
                    var t2 = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(3)
                    };
                    t2.Tick += (s, e) =>
                    {
                        t2.Stop();
                        Core.Diag.Log("auto layout mode on");
                        host.SetLayoutMode(true, "CKV_LAYOUT");
                    };
                    t2.Start();
                }
#endif

                app.Run();
                Core.Diag.Log("app.Run returned");
            }
            catch (Exception ex)
            {
                Core.Diag.Log("FATAL: " + ex);
            }
        }
    }
}
