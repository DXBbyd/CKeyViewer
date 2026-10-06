using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace CKeyViewer.Setup.Ui
{
    /// <summary>
    /// 安装程序的配色。与主程序 <c>CKeyViewer/Ui/KvPalette.cs</c> 用的是同一套
    /// iOS 系统色（浅色 / 深色两套），这样「程序和安装包看起来是同一个东西」。
    /// </summary>
    internal static class Skin
    {
        public static bool Dark { get; private set; }

        private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

        public static Color Bg => Dark ? C(0x00, 0x00, 0x00) : C(0xF2, 0xF2, 0xF7);
        public static Color Header => Dark ? C(0x1C, 0x1C, 0x1E) : C(0xFF, 0xFF, 0xFF);
        public static Color Card => Dark ? C(0x1C, 0x1C, 0x1E) : C(0xFF, 0xFF, 0xFF);
        public static Color Field => Dark ? C(0x2C, 0x2C, 0x2E) : C(0xFF, 0xFF, 0xFF);
        public static Color Border => Dark ? C(0x38, 0x38, 0x3A) : C(0xD8, 0xD8, 0xDC);
        public static Color Ink => Dark ? C(0xFF, 0xFF, 0xFF) : C(0x1C, 0x1C, 0x1E);
        public static Color Sub => C(0x8E, 0x8E, 0x93);
        public static Color Accent => Dark ? C(0x0A, 0x84, 0xFF) : C(0x00, 0x7A, 0xFF);
        public static Color HoverFill => Dark ? C(0x2C, 0x2C, 0x2E) : C(0xEF, 0xEF, 0xF4);
        public static Color PressFill => Dark ? C(0x3A, 0x3A, 0x3C) : C(0xE1, 0xE1, 0xE8);
        public static Color TrackOff => Dark ? C(0x39, 0x39, 0x3D) : C(0xE9, 0xE9, 0xEB);
        public static Color Danger => Dark ? C(0xFF, 0x45, 0x3A) : C(0xFF, 0x3B, 0x30);
        public static Color Ok => Dark ? C(0x30, 0xD1, 0x58) : C(0x25, 0x9A, 0x45);

        /// <summary>设置主题。返回 true 表示确实变了。</summary>
        public static bool Use(bool dark)
        {
            if (Dark == dark) return false;
            Dark = dark;
            return true;
        }

        /// <summary>Windows 的「应用」主题（浅色 / 深色）。</summary>
        public static bool DetectSystemDark()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                           @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k?.GetValue("AppsUseLightTheme");
                    if (v is int i) return i == 0;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 读 setup.exe 旁边的 config/settings.json 里记的主题 ——
        /// 用户上次在程序里选了浅色，安装包就跟着用浅色，不必再选一遍。
        /// 读不到返回 null。
        /// </summary>
        public static bool? ReadAppTheme(string baseDir)
        {
            try
            {
                if (string.IsNullOrEmpty(baseDir)) return null;
                string p = Path.Combine(baseDir, "config", "settings.json");
                if (!File.Exists(p)) return null;

                using (var doc = JsonDocument.Parse(File.ReadAllText(p)))
                {
                    JsonElement el;
                    if (!doc.RootElement.TryGetProperty("Theme", out el)) return null;
                    string s = el.GetString();
                    if (s == "dark") return true;
                    if (s == "light") return false;
                }
            }
            catch { }
            return null;
        }

        // ── 绘制小工具 ──────────────────────────────────────────

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Max(0, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            if (d <= 0) { path.AddRectangle(r); return path; }

            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color Shift(Color c, double amount)
        {
            // amount < 0 变暗、> 0 变亮
            double f = amount < 0 ? 1 + amount : 1 - amount;
            int adj(int v) => amount < 0
                ? (int)Math.Max(0, v * f)
                : (int)Math.Min(255, v + (255 - v) * amount);
            return Color.FromArgb(c.A, adj(c.R), adj(c.G), adj(c.B));
        }

        public static Color Blend(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)Math.Round(a.A + (b.A - a.A) * t),
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        /// <summary>把当前调色板刷到整棵控件树上（主题切换后调一次）。</summary>
        public static void Apply(Control root)
        {
            if (root == null) return;
            foreach (Control c in root.Controls)
            {
                ApplyOne(c);
                if (c.HasChildren) Apply(c);
            }
            root.Invalidate(true);
        }

        private static void ApplyOne(Control c)
        {
            string tag = c.Tag as string;

            if (c is IosToggle || c is IosButton || c is IosBar || c is IosCard)
            {
                if (tag == "muted") c.ForeColor = Sub;
                else c.ForeColor = Ink;
                c.BackColor = c is IosCard ? Card : (c.Parent != null ? c.Parent.BackColor : Bg);
                c.Invalidate();
                return;
            }

            if (c is TextBox)
            {
                c.BackColor = Field;
                c.ForeColor = Ink;
                return;
            }

            if (c is Label)
            {
                c.BackColor = Color.Transparent;
                c.ForeColor = tag == "muted" ? Sub
                            : tag == "ok" ? Ok
                            : tag == "err" ? Danger
                            : Ink;
                return;
            }

            if (c is Panel || c is Form)
            {
                c.BackColor = tag == "header" ? Header : Bg;
                c.ForeColor = Ink;
                return;
            }

            c.ForeColor = Ink;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  控件
    // ══════════════════════════════════════════════════════════════

    internal enum BtnKind { Plain, Accent, Danger, Ghost }

    /// <summary>iOS 风格按钮：圆角、无边框、悬停变亮、按下变暗。</summary>
    internal sealed class IosButton : Button
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public BtnKind Kind = BtnKind.Plain;

        private bool _hover, _down;

        public IosButton(string text, BtnKind kind = BtnKind.Plain)
        {
            Text = text;
            Kind = kind;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            UseVisualStyleBackColor = false;
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { _down = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { _down = false; Invalidate(); base.OnMouseUp(mevent); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Skin.Bg);

            Color fill, ink;
            switch (Kind)
            {
                case BtnKind.Accent:
                    fill = Skin.Accent; ink = Color.White;
                    if (_hover) fill = Skin.Shift(fill, -0.08);
                    if (_down) fill = Skin.Shift(fill, -0.18);
                    break;
                case BtnKind.Danger:
                    fill = Skin.Danger; ink = Color.White;
                    if (_hover) fill = Skin.Shift(fill, -0.08);
                    if (_down) fill = Skin.Shift(fill, -0.18);
                    break;
                case BtnKind.Ghost:
                    fill = _down ? Skin.PressFill : (_hover ? Skin.HoverFill : Color.Empty);
                    ink = Skin.Accent;
                    break;
                default:
                    fill = _down ? Skin.PressFill : (_hover ? Skin.HoverFill : Skin.Card);
                    ink = Skin.Ink;
                    break;
            }

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Skin.Rounded(r, 9))
            {
                if (fill != Color.Empty)
                    using (var b = new SolidBrush(fill)) g.FillPath(b, path);

                if (Kind == BtnKind.Plain)
                    using (var pen = new Pen(Skin.Border)) g.DrawPath(pen, path);
            }

            if (!Enabled) ink = Color.FromArgb(110, ink);

            TextRenderer.DrawText(g, Text, Font, r, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// iOS 开关：左边文字、右边一个 44×26 的圆角轨道 + 白色圆钮，切换时圆钮滑过去。
    /// 用 <see cref="CheckBox"/> 派生是为了白拿点击、空格键与 <c>Checked</c> 语义。
    /// </summary>
    internal sealed class IosToggle : CheckBox
    {
        private const int TrackW = 44;
        private const int TrackH = 26;
        private const int Knob = 22;
        private const int Pad = 2;

        private readonly Timer _anim;
        private double _pos;
        private bool _hover;

        public IosToggle(string text, bool @checked)
        {
            Text = text;
            Height = 34;

            // SetStyle 必须在设 BackColor 之前 —— Control.set_BackColor 当场就会校验
            // SupportsTransparentBackColor，顺序反了直接抛「Control does not support
            // transparent background colors」。
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            // _anim 必须**先**建好：下面那句 Checked = 会触发 OnCheckedChanged，
            // 而它要碰 _anim —— 顺序反了就是构造期 NullReferenceException。
            _anim = new Timer { Interval = 15 };
            _anim.Tick += (s, e) => Step();

            Checked = @checked;
            _pos = @checked ? 1 : 0;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TextRightPadding { get; set; } = 8;

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            if (_anim == null || _anim.Enabled) return;
            _anim.Start();
        }

        private void Step()
        {
            double target = Checked ? 1 : 0;
            double d = target - _pos;
            if (Math.Abs(d) < 0.02) { _pos = target; _anim.Stop(); }
            else _pos += d * 0.35;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var parentBg = Parent != null ? Parent.BackColor : Skin.Card;
            g.Clear(parentBg);

            // 整行的悬停底色 —— 这就是「按键反馈」
            if (_hover && Enabled)
            {
                using (var b = new SolidBrush(Skin.HoverFill))
                using (var p = Skin.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                    g.FillPath(b, p);
            }

            var track = new Rectangle(Width - TrackW - 2, (Height - TrackH) / 2, TrackW, TrackH);
            Color trackColor = Skin.Blend(Skin.TrackOff, Skin.Accent, _pos);
            if (!Enabled) trackColor = Skin.Blend(trackColor, parentBg, 0.55);

            using (var p = Skin.Rounded(track, TrackH / 2))
            using (var b = new SolidBrush(trackColor))
                g.FillPath(b, p);

            double kx = track.Left + Pad + _pos * (TrackW - Knob - Pad * 2);
            var knob = new Rectangle((int)Math.Round(kx), track.Top + Pad, Knob, Knob);
            using (var b = new SolidBrush(Enabled ? Color.White : Color.FromArgb(200, 245, 245, 245)))
                g.FillEllipse(b, knob);

            int textW = Math.Max(0, track.Left - TextRightPadding);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, textW, Height),
                Enabled ? Skin.Ink : Skin.Sub,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.WordBreak);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _anim?.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>iOS 风格进度条：细圆角轨道 + 强调色填充（系统进度条画不进自定义颜色）。</summary>
    internal sealed class IosBar : Control
    {
        private int _value;

        public IosBar()
        {
            Height = 8;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => _value;
            set
            {
                int v = Math.Max(0, Math.Min(100, value));
                if (v == _value) return;
                _value = v;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Skin.Bg);

            int h = Height;
            using (var p = Skin.Rounded(new Rectangle(0, 0, Width, h), h / 2))
            using (var b = new SolidBrush(Skin.TrackOff))
                g.FillPath(b, p);

            if (_value <= 0) return;

            int w = Math.Max(h, (int)Math.Round(Width * (_value / 100.0)));
            using (var p = Skin.Rounded(new Rectangle(0, 0, w, h), h / 2))
            using (var b = new SolidBrush(Skin.Accent))
                g.FillPath(b, p);
        }
    }

    /// <summary>
    /// 普通面板，但<b>允许透明子控件</b>。
    /// WinForms 的 <c>Panel</c> 默认不支持 <c>BackColor = Transparent</c> 的子控件，
    /// 直接给 Label 设透明会抛「Control does not support transparent background colors」。
    /// </summary>
    internal class IosPanel : Panel
    {
        public IosPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    /// <summary>圆角卡片（白 / 深灰底 + 1px 描边），用来把几行开关收成一组。</summary>
    internal sealed class IosCard : Panel
    {
        public IosCard()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Skin.Card;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Skin.Bg);

            using (var p = Skin.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            {
                using (var b = new SolidBrush(Skin.Card)) g.FillPath(b, p);
                using (var pen = new Pen(Skin.Border)) g.DrawPath(pen, p);
            }
        }
    }
}
