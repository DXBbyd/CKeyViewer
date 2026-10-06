using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CKeyViewer.Setup
{
    /// <summary>安装 / 卸载的单一窗口。控件全部手工布局，省掉设计器文件。</summary>
    internal sealed class MainForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(0xF6, 0xF7, 0xFA);
        private static readonly Color Ink = Color.FromArgb(0x1F, 0x24, 0x30);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x72, 0x80);
        private static readonly Color Accent = Color.FromArgb(0x2F, 0x6F, 0xED);
        private static readonly Font UiFont = ResolveFont();

        private readonly SetupOptions _opts;
        private readonly bool _uninstallMode;

        private TextBox _dir;
        private Button _browse;
        private CheckBox _desktop, _startMenu, _admin, _migrate, _keepConfig, _runNow;
        private Label _lblDir, _hint, _author;
        private ProgressBar _bar;
        private Label _status;
        private Button _primary, _cancel;

        private bool _busy, _finished;
        private string _appExe;

        /// <summary>
        /// 窗体图标必须自己拿住 —— <c>using</c> 里的 Icon 出了作用域就被 Dispose，
        /// 而 Form 仍持有引用，等到第一次绘制就会 ObjectDisposedException 把进程带走。
        /// （这个坑踩过一次：GUI 模式起来 2 秒就退，退出码 0xE0434352。）
        /// </summary>
        private static readonly Icon AppIcon = LoadAppIcon();

        private static Icon LoadAppIcon()
        {
            try
            {
                string exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return null;
                return Icon.ExtractAssociatedIcon(exe);
            }
            catch { return null; }
        }

        public MainForm(SetupOptions opts)
        {
            _opts = opts;
            _uninstallMode = opts.Uninstall;

            Text = _uninstallMode ? "卸载 CKeyViewer" : "安装 CKeyViewer";
            Font = UiFont;
            BackColor = Bg;
            ForeColor = Ink;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(468, 352);
            if (AppIcon != null) Icon = AppIcon;

            BuildHeader();
            BuildBody();

            if (_uninstallMode) ConfigureForUninstall();
            else ConfigureForInstall();
        }

        // ── 界面搭建 ──────────────────────────────────────────────

        private void BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Ink };

            var icon = new PictureBox
            {
                Left = 18, Top = 16, Width = 32, Height = 32,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try
            {
                if (AppIcon != null) icon.Image = AppIcon.ToBitmap();
            }
            catch { }
            header.Controls.Add(icon);

            header.Controls.Add(new Label
            {
                Text = AppInfo.ProductName + "  " + AppInfo.Version,
                Left = 62, Top = 14, Width = 380, Height = 22,
                ForeColor = Color.White,
                Font = new Font(UiFont.FontFamily, 12f, FontStyle.Bold),
                BackColor = Color.Transparent
            });

            header.Controls.Add(new Label
            {
                Text = _uninstallMode
                    ? "从这台电脑上移除 CKeyViewer"
                    : "按键可视化覆盖层 —— 置顶显示按键状态、KPS 与计数",
                Left = 63, Top = 38, Width = 390, Height = 18,
                ForeColor = Color.FromArgb(0xA8, 0xB0, 0xC0),
                Font = new Font(UiFont.FontFamily, 8.5f),
                BackColor = Color.Transparent
            });

            Controls.Add(header);
        }

        private void BuildBody()
        {
            _lblDir = new Label { Left = 18, Top = 78, Width = 434, Height = 18, ForeColor = Muted, Text = "安装位置" };
            Controls.Add(_lblDir);

            _dir = new TextBox { Left = 18, Top = 98, Width = 330, Height = 24, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(_dir);

            _browse = new Button { Left = 356, Top = 97, Width = 94, Height = 26, Text = "浏览…", FlatStyle = FlatStyle.System };
            _browse.Click += OnBrowse;
            Controls.Add(_browse);

            _desktop = MakeCheck(132, "创建桌面快捷方式", true);
            _startMenu = MakeCheck(154, "添加到开始菜单", true);
            _admin = MakeCheck(176, "以管理员权限运行（启动时自动弹出 UAC 提权）", true);
            _migrate = MakeCheck(198, "迁移 setup.exe 旁边的 config /（如果存在）", false);

            _keepConfig = MakeCheck(98, "保留 config /（按键计数、配色与自定义布局）", true);
            _keepConfig.Visible = false;

            _hint = new Label
            {
                Left = 18, Top = 224, Width = 434, Height = 32,
                ForeColor = Muted, Font = new Font(UiFont.FontFamily, 8.5f),
                Text = "主程序通过 GetAsyncKeyState 读取按键，权限不能低于你要观察的程序，\n"
                     + "因此默认以管理员身份运行。安装本身不需要管理员，会装进你的用户目录。"
            };
            Controls.Add(_hint);

            _bar = new ProgressBar { Left = 18, Top = 262, Width = 434, Height = 16, Style = ProgressBarStyle.Continuous };
            Controls.Add(_bar);

            _status = new Label { Left = 18, Top = 282, Width = 434, Height = 18, ForeColor = Ink, Text = "准备就绪" };
            Controls.Add(_status);

            _runNow = new CheckBox
            {
                Left = 18, Top = 312, Width = 220, Height = 22,
                Text = "立即运行 CKeyViewer", Checked = true, Visible = false,
                FlatStyle = FlatStyle.System
            };
            Controls.Add(_runNow);

            // 作者信息占着左下角；装完之后那里要让给「立即运行」
            Bitmap avatar = LoadAvatar(20);
            if (avatar != null)
            {
                var pic = new PictureBox
                {
                    Left = 18, Top = 313, Width = 20, Height = 20,
                    Image = avatar, SizeMode = PictureBoxSizeMode.StretchImage,
                    BackColor = Color.Transparent
                };
                Controls.Add(pic);
            }

            _author = new Label
            {
                Left = avatar != null ? 44 : 18, Top = 315, Width = 230, Height = 18,
                ForeColor = Muted, Font = new Font(UiFont.FontFamily, 8.5f),
                Text = "作者 " + AppInfo.Author + "  ·  QQ " + AppInfo.AuthorQQ
                     + "  ·  v" + AppInfo.Version
            };
            Controls.Add(_author);

            _primary = new Button { Left = 254, Top = 308, Width = 92, Height = 30, Text = "安装", FlatStyle = FlatStyle.System };
            _primary.Click += OnPrimary;
            Controls.Add(_primary);

            _cancel = new Button { Left = 356, Top = 308, Width = 92, Height = 30, Text = "取消", FlatStyle = FlatStyle.System, DialogResult = DialogResult.Cancel };
            Controls.Add(_cancel);

            AcceptButton = _primary;
            CancelButton = _cancel;
        }

        /// <summary>
        /// 默认焦点别落在输入框上 —— 否则窗口一出现，路径就是「全选」状态，很难看。
        /// 把插入点收到末尾，焦点给主按钮。
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                _dir.SelectionStart = _dir.TextLength;
                _dir.SelectionLength = 0;
                _primary.Focus();
            }
            catch { }
        }

        private CheckBox MakeCheck(int top, string text, bool @checked)
        {
            var c = new CheckBox
            {
                Left = 18, Top = top, Width = 434, Height = 20,
                Text = text, Checked = @checked, FlatStyle = FlatStyle.System
            };
            Controls.Add(c);
            return c;
        }

        // ── 两种模式 ──────────────────────────────────────────────

        private void ConfigureForInstall()
        {
            _dir.Text = _opts.Dir ?? AppInfo.DefaultInstallDir;
            _lblDir.Text = "安装位置";

            bool hasConfig = Directory.Exists(Path.Combine(AppInfo.SetupDir, "config"));
            _migrate.Enabled = hasConfig;
            _migrate.Checked = hasConfig;
            if (!hasConfig) _migrate.Text = "迁移 setup.exe 旁边的 config /（未找到，跳过）";

            if (_opts.NoDesktop) _desktop.Checked = false;
            if (_opts.NoStartMenu) _startMenu.Checked = false;
            if (_opts.NoRunAsAdmin) _admin.Checked = false;

            _opts.Dir = _dir.Text;
        }

        private void ConfigureForUninstall()
        {
            string dir = Uninstaller.DetectInstallDir();

            _lblDir.Text = "即将卸载";
            _dir.Text = dir ?? "（找不到安装目录）";
            _dir.ReadOnly = true;
            _dir.BackColor = Color.FromArgb(0xEC, 0xEE, 0xF2);
            _browse.Visible = false;
            _desktop.Visible = false;
            _startMenu.Visible = false;
            _admin.Visible = false;
            _migrate.Visible = false;
            _keepConfig.Visible = true;

            _hint.Text = "卸载会删除程序文件、快捷方式与「以管理员身份运行」标记。\n"
                       + "是否保留 config / 由上面的勾选决定（推荐保留，里面有你的按键计数与配色）。";

            _primary.Text = "卸载";
            _primary.Enabled = dir != null;
            _status.Text = dir != null ? "准备就绪" : "找不到 CKeyViewer 的安装信息";
        }

        // ── 交互 ──────────────────────────────────────────────────

        private void OnBrowse(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择安装目录";
                dlg.ShowNewFolderButton = true;
                dlg.SelectedPath = _dir.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _dir.Text = Path.Combine(dlg.SelectedPath, AppInfo.ProductName);
                    _opts.Dir = _dir.Text;
                }
            }
        }

        private void OnPrimary(object sender, EventArgs e)
        {
            if (_finished)
            {
                if (_runNow.Checked && !string.IsNullOrEmpty(_appExe)) Processes.Launch(_appExe);
                Close();
                return;
            }

            if (_busy) return;

            if (!_uninstallMode)
            {
                string target = (_dir.Text ?? "").Trim();
                if (target.Length == 0)
                {
                    MessageBox.Show(this, "请先指定安装位置。", AppInfo.ProductName,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _opts.Dir = target;
            }

            _busy = true;
            _primary.Enabled = false;
            _bar.Value = 0;
            _dir.Enabled = false;
            _browse.Enabled = false;
            _desktop.Enabled = _startMenu.Enabled = _admin.Enabled = _migrate.Enabled = false;
            _cancel.Enabled = false;

            SetupOptions opts = _uninstallMode
                ? new SetupOptions { Uninstall = true, KeepConfig = _keepConfig.Checked }
                : new SetupOptions
                {
                    Dir = _opts.Dir,
                    NoDesktop = !_desktop.Checked,
                    NoStartMenu = !_startMenu.Checked,
                    NoRunAsAdmin = !_admin.Checked
                };

            bool uninstall = _uninstallMode;

            var th = new Thread(delegate ()
            {
                int rc = uninstall
                    ? Uninstaller.Begin(opts.KeepConfig, Report)
                    : Installer.Run(opts, Report);
                try { BeginInvoke(new Action(delegate () { Finish(rc); })); }
                catch (Exception) { /* 窗口已经关了 */ }
            });
            th.IsBackground = true;
            th.Start();
        }

        private void Report(int percent, string status)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke(new Action(delegate ()
                {
                    if (IsDisposed) return;
                    if (percent >= 0 && percent <= 100) _bar.Value = percent;
                    if (!string.IsNullOrEmpty(status)) _status.Text = status;
                }));
            }
            catch (Exception) { }
        }

        private void Finish(int rc)
        {
            _busy = false;
            if (rc != 0)
            {
                _status.ForeColor = Color.FromArgb(0xC0, 0x39, 0x2B);
                _primary.Text = "关闭";
                _primary.Enabled = true;
                _finished = true;
                _cancel.Visible = false;
                _bar.Value = 0;
                return;
            }

            _finished = true;
            _status.ForeColor = Color.FromArgb(0x1B, 0x7F, 0x4B);

            if (_uninstallMode)
            {
                _bar.Value = 100;
                _primary.Text = "完成";
                _primary.Enabled = true;
                _cancel.Visible = false;
                return;
            }

            _appExe = Path.Combine(_opts.Dir, AppInfo.AppExeName);
            _bar.Value = 100;
            _primary.Text = "完成";
            _primary.Enabled = true;
            _author.Visible = false;
            _runNow.Visible = true;
            _cancel.Visible = false;
        }

        /// <summary>
        /// 读嵌入的作者头像并裁成圆形。<c>PictureBox</c> 不会自己圆角，
        /// 所以先在内存里用 <see cref="GraphicsPath"/> 剪一次。
        /// </summary>
        private static Bitmap LoadAvatar(int size)
        {
            try
            {
                using (Stream s = typeof(MainForm).Assembly
                           .GetManifestResourceStream("CKeyViewerSetup.avatar.png"))
                {
                    if (s == null) return null;
                    using (Image src = Image.FromStream(s))
                    {
                        var bmp = new Bitmap(size, size);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            using (var path = new GraphicsPath())
                            {
                                path.AddEllipse(0, 0, size, size);
                                g.SetClip(path);
                                g.DrawImage(src, new Rectangle(0, 0, size, size));
                            }
                        }
                        return bmp;
                    }
                }
            }
            catch { return null; }
        }

        /// <summary>优先用「微软雅黑 UI」，没有就退回系统默认，避免中文糊成方块。</summary>
        private static Font ResolveFont()
        {
            string[] candidates = { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" };
            foreach (string name in candidates)
            {
                try
                {
                    var f = new Font(name, 9f);
                    if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return f;
                }
                catch { }
            }
            return SystemFonts.MessageBoxFont;
        }
    }
}
