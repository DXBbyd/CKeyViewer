using System;
using System.Drawing;
using System.Windows.Forms;

namespace CKeyViewer
{
    /// <summary>
    /// 系统托盘图标 —— 无窗口界面时的唯一常驻入口。
    /// 提供显示/隐藏、重置计数、切换档案、打开设置与配置目录、退出。
    /// </summary>
    public sealed class KvTray : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly KvHost _host;
        private readonly Action<int> _openSettings;
        private readonly Action _exit;
        private Icon _generated;

        public KvTray(KvHost host, Action<int> openSettings, Action exit)
        {
            _host = host;
            _openSettings = openSettings;
            _exit = exit;

            _generated = LoadIcon();
            Core.Diag.Log("tray icon loaded: " + Describe(_generated));

            _icon = new NotifyIcon
            {
                Text = About.ProductName + " " + About.Version + " —— 按键可视化覆盖层",
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };
            if (_generated != null) _icon.Icon = _generated;

            _icon.DoubleClick += (s, e) => _host.ToggleVisible();
            Core.Diag.Log("tray notifyicon visible=" + _icon.Visible);

            ShowStartBalloon();
        }

        private static string Describe(Icon ico)
        {
            if (ico == null) return "null";
            try { return ico.Size.Width + "x" + ico.Size.Height; }
            catch { return "?"; }
        }

        /// <summary>
        /// 启动气泡。新注册的托盘图标在 Win11 上默认会被收进「隐藏的图标」折叠区，
        /// 用户很容易以为程序没起来 —— 用气泡把位置和快捷键交代清楚。
        /// </summary>
        private void ShowStartBalloon()
        {
            try
            {
                _icon.BalloonTipTitle = "CKeyViewer 已启动";
                _icon.BalloonTipText =
                    "覆盖层已就位。双击托盘图标可显示 / 隐藏，Ctrl+Alt+S 打开设置。\r\n" +
                    "如果任务栏上看不到本图标，点右下角的 ^ 展开「隐藏的图标」即可。";
                _icon.BalloonTipIcon = ToolTipIcon.Info;
                _icon.ShowBalloonTip(8000);
            }
            catch (Exception ex)
            {
                Core.Diag.Log("tray balloon: " + ex.Message);
            }
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip
            {
                // 圆角卡片 + 我们自己的 1px 描边，原生投影会和圆角对不上，关掉更干净
                DropShadowEnabled = false,
                ShowImageMargin = false,
                ShowCheckMargin = true,
                Padding = new Padding(0, 5, 0, 5)
            };

            var title = new ToolStripMenuItem(About.TitleWithVersion + "  ·  " + About.Author)
            {
                Enabled = false,
                Padding = new Padding(0, 4, 0, 4),
                Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold)
            };
            menu.Items.Add(title);

            Add(menu.Items, "显示 / 隐藏", () => _host.ToggleVisible(), "Ctrl+Alt+K");

            var reset = Add(menu.Items, "重置计数", () =>
            {
                if (MessageBox.Show("确定要把所有按键计数归零吗？", "CKeyViewer",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                    _host.ResetCounts();
            }, "Ctrl+Alt+R");
            reset.Tag = "danger";

            // ---- 吸附（和设置面板「吸附」页同一套取值） ----
            var anchor = Sub(menu.Items, "吸附位置");
            for (int i = Core.KvSnap.Min; i <= Core.KvSnap.Max; i++)
            {
                int v = i;
                var it = Add(anchor.DropDownItems, Core.KvSnap.NameOf(v), () => SetAnchor(v));
                it.Tag = "anchor" + v;
            }

            var profiles = Sub(menu.Items, "切换档案");
            // 先填一次：ToolStripMenuItem 只在「有子项」时才画右侧的 › 箭头，
            // 而档案列表是每次展开才刷新的 —— 不预填的话这一行看起来不像能展开。
            FillProfiles(profiles);
            profiles.DropDownOpening += (s, e) =>
            {
                FillProfiles(profiles);
                Skin(profiles.DropDown);
            };

            // ---- 自由布局 ----
            var layout = Add(menu.Items, "自由布局：拖动调整位置", () => _host.ToggleLayoutMode());

            // ---- ADOFAI（冰与火之舞）覆盖层 ----
            var adofai = Add(menu.Items, "ADOFAI 信息覆盖层", () =>
            {
                bool on = !_host.AdofaiSettings.Enabled;
                _host.SetAdofaiEnabled(on);
                Balloon("ADOFAI 覆盖层" + (on ? "已启用" : "已关闭"),
                        on ? "已进入监听。请确认冰与火之舞已启动并进入关卡。\r\n当前：" + _host.AdofaiStatus
                           : "已断开与游戏进程的连接。");
            });

            var adofaiLayout = Add(menu.Items, "拖动摆放信息层", () =>
                _host.SetLayoutMode(true, "tray adofai"));

            anchor.DropDownOpening += (s, e) =>
            {
                int cur = Core.KvSnap.Clamp(_host.Store.Settings.Anchor);
                anchor.Text = "吸附位置：" + Core.KvSnap.NameOf(cur);
                foreach (ToolStripItem it in anchor.DropDownItems)
                    if (it is ToolStripMenuItem mi && mi.Tag is string t && t.StartsWith("anchor"))
                        mi.Checked = t == "anchor" + cur;
                anchor.Checked = false;
                Skin(anchor.DropDown);
            };

            menu.Opening += (s, e) =>
            {
                // 主题可能刚在设置面板里改过，每次弹出前重刷一遍配色
                Skin(menu);

                layout.Checked = _host.LayoutMode;
                layout.Text = _host.LayoutMode
                    ? "退出布局模式（或按 Esc）"
                    : "自由布局：拖动调整位置";

                adofaiLayout.Enabled = _host.AdofaiSettings.Enabled;

                adofai.Checked = _host.AdofaiSettings.Enabled;
                adofai.Text = "ADOFAI 信息覆盖层（" + _host.AdofaiStatus + "）";

                anchor.Text = "吸附位置：" + Core.KvSnap.NameOf(
                    Core.KvSnap.Clamp(_host.Store.Settings.Anchor));
            };

            menu.Items.Add(Sep());

            Add(menu.Items, "设置…", () => _openSettings(-1), "Ctrl+Alt+S");
            Add(menu.Items, "ADOFAI 覆盖层设置…", () => _openSettings(KvSettingsWindow.AdofaiTab));
            Add(menu.Items, "关于 CKeyViewer…", () => _openSettings(KvSettingsWindow.AboutTab));
            Add(menu.Items, "打开配置目录", OpenConfigFolder);

            menu.Items.Add(Sep());

            var exit = Add(menu.Items, "退出", () => _exit());
            exit.Tag = "danger";

            Skin(menu);

#if DEBUG
            // 截图 / 调试用：直接把托盘菜单弹出来（这台机器发不了鼠标右键，
            // SendInput / SetCursorPos 都是空操作，只能由程序自己 Show）。
            // 值 = 额外要展开的子菜单标题，如 CKV_TRAYMENU=吸附位置
            string autoMenu = Environment.GetEnvironmentVariable("CKV_TRAYMENU");
            if (autoMenu != null)
            {
                var t = new Timer { Interval = 6000 };
                t.Tick += (s, e) =>
                {
                    t.Stop();
                    Core.Diag.Log("auto tray menu: " + autoMenu);
                    var screen = Screen.PrimaryScreen.WorkingArea;
                    menu.Show(new Point(screen.Left + 120, screen.Top + 120));
                    if (autoMenu.Length > 0)
                    {
                        foreach (ToolStripItem it in menu.Items)
                            if (it is ToolStripMenuItem mi && mi.Text.StartsWith(autoMenu))
                            { mi.ShowDropDown(); break; }
                    }
                };
                t.Start();
            }
#endif

            return menu;
        }

        /// <summary>统一样式地往菜单里塞一项 —— 行高、内边距都在这里定，省得每项都写一遍。
        /// 收 <see cref="ToolStripItemCollection"/> 而不是 <see cref="ToolStrip"/>：
        /// 子菜单那一层给的是 <c>DropDownItems</c>，不是 ToolStrip。</summary>
        private static ToolStripMenuItem Add(ToolStripItemCollection into, string text,
                                             Action onClick, string shortcut = null)
        {
            var item = new ToolStripMenuItem(text);
            Style(item);
            if (shortcut != null)
            {
                // 快捷键用独立的一列（ShortcutKeyDisplayString），不要塞进 Text 的 \t 里 ——
                // 那样 GDI 会把制表符当普通字符画出来，位置也不对。
                item.ShortcutKeyDisplayString = shortcut;
                item.ShowShortcutKeys = true;
            }
            item.Click += (s, e) => onClick();
            into.Add(item);
            return item;
        }

        /// <summary>带子菜单的父项。</summary>
        private static ToolStripMenuItem Sub(ToolStripItemCollection into, string text)
        {
            var item = new ToolStripMenuItem(text);
            Style(item);
            into.Add(item);
            return item;
        }

        private static void Style(ToolStripMenuItem item)
        {
            // 行高只能靠 Padding 撑 —— 千万**别**用 AutoSize=false + Height：
            // 那样宽度会停在控件默认的 32px，而文字矩形是从 33px 起的，
            // 整段文字正好落在条目自己的裁剪区之外，屏幕上一片空白（踩过）。
            item.Padding = new Padding(0, 7, 0, 7);
            item.Font = new Font("Microsoft YaHei UI", 9f);
        }

        private static ToolStripSeparator Sep() =>
            new ToolStripSeparator { Margin = new Padding(0, 3, 0, 3) };

        /// <summary>
        /// 把当前主题刷到某个下拉菜单上。必须**每个子菜单单独刷** ——
        /// 渲染器是挂在 <see cref="ToolStrip"/> 上的，父菜单设了不会往下传，
        /// 子菜单会退回 ToolStripManager 的默认渲染器（又是 Office 那种渐变蓝）。
        ///
        /// 不用设 <c>BackColor</c>：卡片底色由渲染器在 OnRenderToolStripBackground 里画，
        /// 直接赋值反而要拿 <c>KvPalette</c> 的字符串去转 Color，多一次没必要的解析。
        /// </summary>
        private static void Skin(ToolStrip ts)
        {
            ts.Renderer = new Ui.KvMenuRenderer(Ui.Kit.Palette);
        }

        /// <summary>改吸附位置。与设置面板走同一条路径：落盘 + 立刻重排窗口。</summary>
        private void SetAnchor(int v)
        {
            try
            {
                _host.Store.Settings.Anchor = Core.KvSnap.Clamp(v);
                _host.QueueSave();
                _host.Rebuild();
                Core.Diag.Log("tray anchor -> " + Core.KvSnap.NameOf(_host.Store.Settings.Anchor));
            }
            catch (Exception ex)
            {
                Core.Diag.Log("tray anchor: " + ex.Message);
            }
        }

        private void Balloon(string title, string text)
        {
            try
            {
                _icon.BalloonTipTitle = title;
                _icon.BalloonTipText = text;
                _icon.BalloonTipIcon = ToolTipIcon.Info;
                _icon.ShowBalloonTip(5000);
            }
            catch { }
        }

        private void FillProfiles(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();

            string[] names;
            try { names = _host.Store.ScanProfiles().ToArray(); }
            catch { names = new[] { _host.Store.CurrentProfile }; }

            if (names == null || names.Length == 0) names = new[] { _host.Store.CurrentProfile };

            string current = _host.Store.CurrentProfile;
            foreach (var n in names)
            {
                var item = new ToolStripMenuItem(n)
                {
                    Checked = string.Equals(n, current, StringComparison.OrdinalIgnoreCase)
                };
                Style(item);
                string captured = n;
                item.Click += (s, e) => _host.SwitchProfile(captured);
                parent.DropDownItems.Add(item);
            }
        }

        private void OpenConfigFolder()
        {
            try
            {
                string dir = _host.Store.Root;
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Core.Diag.Log("open config folder: " + ex.Message);
            }
        }

        // ---------------------------------------------------------------
        // 图标 —— 直接用嵌入的 assets/app.ico（源图 icon/icon.png，
        // 由 tools/makeicon.py 生成 9 档尺寸）
        // ---------------------------------------------------------------

        private const string IconResourceName = "CKeyViewer.app.ico";

        /// <summary>
        /// 按托盘实际槽位尺寸从 ICO 里挑最合适的一档。
        /// 原先固定拿 32×32 给 16×16 的槽位，被系统缩掉一半会糊成一团；
        /// ICO 里本来就有 16/20/24 各档，直接取对应的那档最清晰。
        /// </summary>
        private Icon LoadIcon()
        {
            int size = 16;
            try
            {
                Size s = SystemInformation.SmallIconSize;
                if (s.Width > 0) size = s.Width;
            }
            catch { }

            using (var stream = typeof(KvTray).Assembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                {
                    // Icon(Stream, Size) 会从多尺寸 ICO 里挑最接近的那一档，
                    // 比 Icon(Stream) 固定拿第一档要准
                    return new Icon(stream, new Size(size, size));
                }
            }

            // 资源没嵌进来（比如有人删了 assets/app.ico）时退回 exe 自身的图标，
            // 托盘至少不会是个空白
            Core.Diag.Log("tray icon resource missing, fallback to exe icon");
            try
            {
                string exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                    return Icon.ExtractAssociatedIcon(exe);
            }
            catch (Exception ex)
            {
                Core.Diag.Log("tray icon fallback failed: " + ex.Message);
            }
            return null;
        }

        public void Dispose()
        {
            try
            {
                _icon.Visible = false;
                _icon.Dispose();
            }
            catch { }
            _generated?.Dispose();
            _generated = null;
        }
    }
}
