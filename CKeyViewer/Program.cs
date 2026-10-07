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

            // 现场取证：把「游戏窗口是哪一个 / 为什么没认出来」摊开（开着游戏跑）
            if (args != null && args.Length > 0 &&
                string.Equals(args[0], "--winprobe", StringComparison.OrdinalIgnoreCase))
            {
                Native.Win32.AttachParentConsole();
                string outPath = args.Length > 1
                    ? args[1]
                    : System.IO.Path.Combine(AppContext.BaseDirectory, "ckv_winprobe.txt");
                Environment.ExitCode = WinProbe.Run(outPath);
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
            // 记一笔提权状态与自己的 PID：读游戏进程失败时，先要能区分
            // 「我们没提权」和「游戏那边不让我们读」。
            Core.Diag.Log("elevated=True pid=" + Environment.ProcessId);

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

                // 启动时把 ADOFAI 进程现状记一笔：同名进程经常不止一个，
                // 连不上时这是第一个要看的线索。
                int[] adofaiPids = Adofai.AdofaiReader.ProcessIds();
                Core.Diag.Log("adofai processes = " +
                    (adofaiPids.Length == 0 ? "none" : string.Join(",", adofaiPids)));

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

                // 截图 / 调试用：打开设置面板后**自动弹开第 n 个下拉菜单**。
                // 这台机器上 SendInput / SetCursorPos 都是空操作，点不开任何菜单，
                // 只能靠环境变量硬弹 —— 之前托盘菜单那个「纯白卡片」就是这么定位的。
                // 值 = 下拉的序号（按创建顺序，0 是第一个）。Release 里整段被编译掉。
                string autoCombo = Environment.GetEnvironmentVariable("CKV_OPEN_COMBO");
                if (!string.IsNullOrEmpty(autoCombo))
                {
                    int ci;
                    if (!int.TryParse(autoCombo, out ci)) ci = 0;
                    int want = ci;
                    var t3 = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(7)
                    };
                    t3.Tick += (s, e) =>
                    {
                        t3.Stop();
                        bool ok = Ui.Kit.DebugOpenCombo(want);
                        Core.Diag.Log("auto-open combo #" + want + " ok=" + ok
                                      + " total=" + Ui.Kit.DebugComboCount);
                    };
                    t3.Start();
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

                // 截图用：打开设置面板第 n 页并滚到指定位置，**停在那儿**
                //（CKV_SCROLLTEST 最后会显式回顶，所以截不了页面下半截）。
                // 值 = "标签页下标,偏移量"，偏移可省略。Release 里整段被编译掉。
                string scrollTo = Environment.GetEnvironmentVariable("CKV_SCROLLTO");
                if (!string.IsNullOrEmpty(scrollTo))
                {
                    string[] parts = scrollTo.Split(',');
                    int stTab2 = 0, stOff = 0;
                    if (parts.Length > 0) int.TryParse(parts[0], out stTab2);
                    if (parts.Length > 1) int.TryParse(parts[1], out stOff);

                    var tA = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                    tA.Tick += (s, e) =>
                    {
                        tA.Stop();
                        openSettings(stTab2);

                        var tB = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                        tB.Tick += (s2, e2) =>
                        {
                            tB.Stop();
                            if (settings == null) { Core.Diag.Log("scrollto: 没有设置窗口"); return; }
                            settings.DebugScrollTo(stOff);

                            // ScrollToVerticalOffset 要等下一轮布局才生效，落定后再补一次
                            var tC = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                            tC.Tick += (s3, e3) =>
                            {
                                tC.Stop();
                                settings.DebugScrollTo(stOff);
                                Core.Diag.Log("scrollto: tab=" + stTab2 + " offset=" +
                                              settings.DebugScrollOffset.ToString("0.0"));
                            };
                            tC.Start();
                        };
                        tB.Start();
                    };
                    tA.Start();
                }

                // 调试用：验证「改一个选项不会把设置页跳回顶部」。
                // 这台机器发不了模拟点击（SendInput / SetCursorPos 是空操作），只能这样驱动：
                //   滚到中间 → 就地重建一次 → 再显式回顶重建一次（对照组）→ 前后偏移量全打进日志。
                // 值 = 标签页下标。Release 里整段被编译掉。
                string scrollTest = Environment.GetEnvironmentVariable("CKV_SCROLLTEST");
                if (!string.IsNullOrEmpty(scrollTest))
                {
                    int stTab;
                    if (!int.TryParse(scrollTest, out stTab)) stTab = 0;

                    var t5 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                    t5.Tick += (s, e) =>
                    {
                        t5.Stop();
                        openSettings(stTab);

                        var t6 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                        t6.Tick += (s2, e2) =>
                        {
                            t6.Stop();
                            if (settings == null) { Core.Diag.Log("scrolltest: 没有设置窗口"); return; }

                            // ScrollToVerticalOffset 不是同步生效的（要等下一轮布局），
                            // 所以每个读数都必须等它落定之后再取。
                            settings.DebugScrollTo(400);

                            var t7 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                            t7.Tick += (s3, e3) =>
                            {
                                t7.Stop();
                                Core.Diag.Log("scrolltest: 滚到 400 之后落定 = " + settings.DebugScrollOffset.ToString("0.0"));

                                settings.DebugRebuildInPlace();
                                var t8 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                                t8.Tick += (s4, e4) =>
                                {
                                    t8.Stop();
                                    Core.Diag.Log("scrolltest: 就地重建后 = " + settings.DebugScrollOffset.ToString("0.0")
                                                  + "   （保持 = 对，回 0 = 错）");

                                    settings.DebugRebuildFromTop();
                                    var t9 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                                    t9.Tick += (s5, e5) =>
                                    {
                                        t9.Stop();
                                        Core.Diag.Log("scrolltest: 对照组（显式回顶）后 = " + settings.DebugScrollOffset.ToString("0.0")
                                                      + "   （必须是 0）");
                                    };
                                    t9.Start();
                                };
                                t8.Start();
                            };
                            t7.Start();
                        };
                        t6.Start();
                    };
                    t5.Start();
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
