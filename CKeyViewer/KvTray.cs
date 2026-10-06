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
        private readonly Action _openSettings;
        private readonly Action _exit;
        private Icon _generated;

        public KvTray(KvHost host, Action openSettings, Action exit)
        {
            _host = host;
            _openSettings = openSettings;
            _exit = exit;

            _generated = LoadIcon();
            Core.Diag.Log("tray icon loaded: " + Describe(_generated));

            _icon = new NotifyIcon
            {
                Text = "CKeyViewer —— 按键可视化覆盖层",
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
            var menu = new ContextMenuStrip();

            var title = new ToolStripMenuItem("CKeyViewer") { Enabled = false };
            menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());

            var toggle = new ToolStripMenuItem("显示 / 隐藏\tCtrl+Alt+K");
            toggle.Click += (s, e) => _host.ToggleVisible();
            menu.Items.Add(toggle);

            var reset = new ToolStripMenuItem("重置计数\tCtrl+Alt+R");
            reset.Click += (s, e) =>
            {
                if (MessageBox.Show("确定要把所有按键计数归零吗？", "CKeyViewer",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                    _host.ResetCounts();
            };
            menu.Items.Add(reset);

            // ---- 档案 ----
            var profiles = new ToolStripMenuItem("切换档案");
            profiles.DropDownOpening += (s, e) => FillProfiles(profiles);
            menu.Items.Add(profiles);

            // ---- 自由布局 ----
            var layout = new ToolStripMenuItem("自由布局：拖动调整位置");
            layout.Click += (s, e) => _host.ToggleLayoutMode();
            menu.Items.Add(layout);

            menu.Opening += (s, e) =>
            {
                layout.Checked = _host.LayoutMode;
                layout.Text = _host.LayoutMode
                    ? "退出布局模式（或按 Esc）"
                    : "自由布局：拖动调整位置";
            };

            menu.Items.Add(new ToolStripSeparator());

            var settings = new ToolStripMenuItem("设置…\tCtrl+Alt+S");
            settings.Click += (s, e) => _openSettings();
            menu.Items.Add(settings);

            var openCfg = new ToolStripMenuItem("打开配置目录");
            openCfg.Click += (s, e) => OpenConfigFolder();
            menu.Items.Add(openCfg);

            menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem("退出");
            exit.Click += (s, e) => _exit();
            menu.Items.Add(exit);

            return menu;
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
