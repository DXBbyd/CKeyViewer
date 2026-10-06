using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using CKeyViewer.Setup.Ui;

namespace CKeyViewer.Setup
{
    /// <summary>安装 / 卸载的单一窗口。控件全部手工布局，省掉设计器文件。</summary>
    internal sealed class MainForm : Form
    {
        private const int W = 480;
        private const int Pad = 18;

        private readonly SetupOptions _opts;
        private readonly bool _uninstallMode;

        private Panel _header;
        private TextBox _dir;
        private IosButton _browse, _themeBtn;
        private IosToggle _desktop, _startMenu, _admin, _migrate, _keepConfig, _runNow;
        private IosCard _card;
        private Label _lblDir, _hint, _author;
        private IosBar _bar;
        private Label _status;
        private IosButton _primary, _cancel;
        private PictureBox _avatar;

        /// <summary>卡片里参与纵向排布的开关（顺序 = 显示顺序）。</summary>
        private readonly System.Collections.Generic.List<IosToggle> _rows =
            new System.Collections.Generic.List<IosToggle>();
        private readonly System.Collections.Generic.HashSet<IosToggle> _hiddenRows =
            new System.Collections.Generic.HashSet<IosToggle>();

        /// <summary>顶部「安装位置」那两行是否还占位（装完之后就没必要了）。</summary>
        private bool _topRowsOn = true;

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

            // 主题：优先跟随上次在程序里选过的那个，其次跟随 Windows 的「应用」主题。
            Skin.Use(Skin.ReadAppTheme(AppInfo.SetupDir) ?? Skin.DetectSystemDark());

            // Label 用 BackColor=Transparent 时，父容器必须支持透明背景
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);

            Text = _uninstallMode ? "卸载 CKeyViewer" : "安装 CKeyViewer";
            UiFont = ResolveFont();
            Font = UiFont;
            BackColor = Skin.Bg;
            ForeColor = Skin.Ink;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = true;
            StartPosition = FormStartPosition.CenterScreen;
            if (AppIcon != null) Icon = AppIcon;

            BuildHeader();
            BuildBody();

            if (_uninstallMode) ConfigureForUninstall();
            else ConfigureForInstall();

            ApplyTheme();
            Relayout();
        }

        private void ApplyTheme()
        {
            BackColor = Skin.Bg;
            ForeColor = Skin.Ink;
            Skin.Apply(this);
            _themeBtn.Text = Skin.Dark ? "浅色" : "深色";
            _themeBtn.Invalidate();
        }

        private void OnToggleTheme(object sender, EventArgs e)
        {
            Skin.Use(!Skin.Dark);
            ApplyTheme();
        }

        // ── 界面搭建 ──────────────────────────────────────────────

        private void BuildHeader()
        {
            _header = new IosPanel { Left = 0, Top = 0, Width = W, Height = 68, Tag = "header" };

            var icon = new PictureBox
            {
                Left = Pad, Top = 18, Width = 32, Height = 32,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try
            {
                if (AppIcon != null) icon.Image = AppIcon.ToBitmap();
            }
            catch { }
            _header.Controls.Add(icon);

            _header.Controls.Add(new Label
            {
                Text = AppInfo.ProductName + "  " + AppInfo.Version,
                Left = Pad + 44, Top = 14, Width = 300, Height = 24,
                Font = new Font(UiFont.FontFamily, 12f, FontStyle.Bold),
                BackColor = Color.Transparent
            });

            _header.Controls.Add(new Label
            {
                Text = _uninstallMode
                    ? "从这台电脑上移除 CKeyViewer"
                    : "按键可视化覆盖层 —— 置顶显示按键状态、KPS 与计数",
                Left = Pad + 45, Top = 38, Width = 310, Height = 18,
                Font = new Font(UiFont.FontFamily, 8.5f),
                Tag = "muted",
                BackColor = Color.Transparent
            });

            _themeBtn = new IosButton(Skin.Dark ? "浅色" : "深色", BtnKind.Ghost)
            {
                Left = W - Pad - 68, Top = 20, Width = 68, Height = 28
            };
            _themeBtn.Click += OnToggleTheme;
            _header.Controls.Add(_themeBtn);

            Controls.Add(_header);
        }

        private void BuildBody()
        {
            _lblDir = new Label
            {
                Left = Pad, Top = 0, Width = W - Pad * 2, Height = 20,
                Tag = "muted", BackColor = Color.Transparent, Text = "安装位置"
            };
            Controls.Add(_lblDir);

            _dir = new TextBox
            {
                Left = Pad, Top = 0, Width = W - Pad * 2 - 104, Height = 26,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Skin.Field, ForeColor = Skin.Ink
            };
            Controls.Add(_dir);

            _browse = new IosButton("浏览…", BtnKind.Plain)
            {
                Left = W - Pad - 96, Top = 0, Width = 96, Height = 28
            };
            _browse.Click += OnBrowse;
            Controls.Add(_browse);

            // ── 选项卡片 ──

            _card = new IosCard { Left = Pad, Top = 0, Width = W - Pad * 2 };
            Controls.Add(_card);

            _desktop = MakeToggle("创建桌面快捷方式", true);
            _startMenu = MakeToggle("添加到开始菜单", true);
            _admin = MakeToggle("以管理员权限运行（启动时自动弹出 UAC 提权）", true);
            _migrate = MakeToggle("迁移 setup.exe 旁边的 config /（如果存在）", false);
            _keepConfig = MakeToggle("保留 config /（按键计数、配色与自定义布局）", true);

            _hint = new Label
            {
                Left = Pad, Top = 0, Width = W - Pad * 2, Height = 40,
                Tag = "muted", BackColor = Color.Transparent,
                Font = new Font(UiFont.FontFamily, 8.5f),
                Text = "主程序通过 GetAsyncKeyState 读取按键，权限不能低于你要观察的程序，"
                     + "因此默认以管理员身份运行。安装本身不需要管理员，会装进你的用户目录。"
            };
            Controls.Add(_hint);

            _bar = new IosBar { Left = Pad, Top = 0, Width = W - Pad * 2, Height = 8 };
            Controls.Add(_bar);

            _status = new Label
            {
                Left = Pad, Top = 0, Width = W - Pad * 2, Height = 20,
                BackColor = Color.Transparent, Text = "准备就绪"
            };
            Controls.Add(_status);

            _avatar = new PictureBox
            {
                Left = Pad, Top = 0, Width = 22, Height = 22,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent
            };
            Bitmap av = LoadAvatar(22);
            if (av != null) { _avatar.Image = av; Controls.Add(_avatar); }
            else _avatar = null;

            _author = new Label
            {
                Left = _avatar != null ? Pad + 34 : Pad, Top = 0, Width = 200, Height = 22,
                Tag = "muted", BackColor = Color.Transparent, AutoSize = false,
                AutoEllipsis = true,
                Font = new Font(UiFont.FontFamily, 8.5f),
                Text = AppInfo.Author + "  ·  QQ " + AppInfo.AuthorQQ
            };
            Controls.Add(_author);

            // 底部左侧是「装之前署名 / 装完换成开关」的同一块地方。
            // 别忘了**一开始就藏起来** —— 它的轨道会从作者文字右边露出来，
            // 而且它比署名晚 Add，Z 序在上面，正好压住按钮的左边角。
            _runNow = MakeToggle("完成后立即运行", true, inCard: false);
            _runNow.Left = Pad;
            _runNow.Visible = false;
            _runNow.Parent = this;

            _primary = new IosButton("安装", BtnKind.Accent)
            {
                Left = W - Pad - 96, Top = 0, Width = 96, Height = 32
            };
            _primary.Click += OnPrimary;
            Controls.Add(_primary);

            _cancel = new IosButton("取消", BtnKind.Plain)
            {
                Left = W - Pad - 96 - 104, Top = 0, Width = 96, Height = 32,
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(_cancel);

            AcceptButton = _primary;
            CancelButton = _cancel;
        }

        private IosToggle MakeToggle(string text, bool @checked, bool inCard = true)
        {
            var t = new IosToggle(text, @checked) { Left = 12 };
            t.CheckedChanged += (s, e) => Relayout();
            if (inCard)
            {
                t.Width = _card.Width - 24;
                _card.Controls.Add(t);
                _rows.Add(t);
            }
            return t;
        }

        /// <summary>
        /// 显式记账某一行的显隐。
        /// 不能直接读 <c>Visible</c> 来判断 —— 窗体还没 Show 时，子控件的
        /// <c>Visible</c> 一律返回 false（它返回的是「有效可见性」），
        /// 布局里就会把整页行都跳过去。第一版就栽在这里：卡片被压成 12px 高。
        /// </summary>
        private void ShowRow(IosToggle t, bool on)
        {
            if (on) _hiddenRows.Remove(t); else _hiddenRows.Add(t);
            t.Visible = on;
        }

        /// <summary>把可见的开关自上而下码进卡片，再按剩下的高度把整页重新排一遍。</summary>
        private void Relayout()
        {
            int y = 80;
            const int rowH = 37;

            if (_topRowsOn)
            {
                _lblDir.Top = y; y += 22;
                _dir.Top = y; _browse.Top = y + 1; y += 40;
            }

            int cy = 0;
            foreach (var t in _rows)
            {
                if (_hiddenRows.Contains(t)) continue;
                t.Top = 6 + cy;
                t.Width = _card.Width - 24;
                cy += rowH;
            }
            _card.Height = cy + 12;
            _card.Top = y;
            y += _card.Height + 14;

            if (_topRowsOn)
            {
                _hint.Top = y;
                _hint.Height = Math.Max(32, TextRenderer.MeasureText(
                    _hint.Text, _hint.Font, new Size(_hint.Width, 0),
                    TextFormatFlags.WordBreak).Height + 4);
                _hint.Visible = true;
                y += _hint.Height + 12;
            }

            _bar.Top = y; y += _bar.Height + 12;
            _status.Top = y; y += _status.Height + 12;

            const int bottomH = 32;
            _primary.Top = y;
            _cancel.Top = y;

            // 左侧那块地方只能是「署名」或「立即运行」二选一，宽度按取消按钮的左边缘现算，
            // 免得写死数字之后一改窗体宽度就压到按钮上。
            int leftRoom = _cancel.Left - Pad - 8;
            if (_avatar != null) _avatar.Top = y + 5;
            _author.Top = y + 6;
            _author.Width = Math.Max(60, _cancel.Left - _author.Left - 10);
            _runNow.Top = y - 2;
            _runNow.Width = Math.Max(120, leftRoom);

            ClientSize = new Size(W, y + bottomH + Pad);
            _header.Width = W;
        }

        /// <summary>
        /// 默认焦点别落在输入框上 —— 否则窗口一出现，路径就是「全选」状态，很难看。
        /// 把插入点收到末尾，焦点给主按钮。
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Relayout();
            try
            {
                _dir.SelectionStart = _dir.TextLength;
                _dir.SelectionLength = 0;
                _primary.Focus();
            }
            catch { }
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
            _dir.BackColor = Skin.HoverFill;
            _browse.Visible = false;
            ShowRow(_desktop, false);
            ShowRow(_startMenu, false);
            ShowRow(_admin, false);
            ShowRow(_migrate, false);
            ShowRow(_keepConfig, true);

            _hint.Text = "卸载会删除程序文件、快捷方式与「以管理员身份运行」标记。"
                       + "是否保留 config / 由上面的开关决定（推荐保留，里面有你的按键计数与配色）。";

            _primary.Text = "卸载";
            _primary.Kind = BtnKind.Danger;
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
            _primary.Invalidate();
            _bar.Value = 0;
            _dir.Enabled = false;
            _browse.Enabled = false;
            foreach (Control c in _card.Controls) c.Enabled = false;
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
            bool dark = Skin.Dark;
            string installDir = _opts.Dir;

            var th = new Thread(delegate ()
            {
                int rc = uninstall
                    ? Uninstaller.Begin(opts.KeepConfig, Report)
                    : Installer.Run(opts, Report);

                // 装完把主题也写进新装的 config，免得「安装包里选了浅色，打开程序还是深色」
                if (rc == 0 && !uninstall) Installer.WriteInitialTheme(installDir, dark);

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
                _status.Tag = "err";
                _status.ForeColor = Skin.Danger;
                _primary.Text = "关闭";
                _primary.Kind = BtnKind.Plain;
                _primary.Enabled = true;
                _primary.Invalidate();
                _finished = true;
                _cancel.Visible = false;
                _bar.Value = 0;
                return;
            }

            _finished = true;
            _status.Tag = "ok";
            _status.ForeColor = Skin.Ok;

            if (_uninstallMode)
            {
                _bar.Value = 100;
                _primary.Text = "完成";
                _primary.Enabled = true;
                _primary.Invalidate();
                _cancel.Visible = false;
                return;
            }

            _appExe = Path.Combine(_opts.Dir, AppInfo.AppExeName);
            _bar.Value = 100;
            _primary.Text = "完成";
            _primary.Enabled = true;
            _primary.Invalidate();
            _author.Visible = false;
            if (_avatar != null) _avatar.Visible = false;
            _runNow.Visible = true;

            // 装完这里只剩「立即运行」和两个按钮，安装位置那两行就没必要占地方了
            _topRowsOn = false;
            _lblDir.Visible = false;
            _dir.Visible = false;
            _browse.Visible = false;
            _hint.Visible = false;
            foreach (Control c in _card.Controls) c.Enabled = true;
            Relayout();
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

        private static Font UiFont;

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
