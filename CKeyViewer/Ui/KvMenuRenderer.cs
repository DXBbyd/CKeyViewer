using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CKeyViewer.Ui
{
    /// <summary>
    /// 把托盘右键菜单画成 iOS / App Store 的样子：圆角卡片、成组的细分隔线、
    /// 悬停时整行铺一层强调色淡底、勾选是强调色的对勾、子菜单箭头是细雪佛龙。
    ///
    /// 为什么要自己画：WinForms 的 <see cref="ContextMenuStrip"/> 默认走
    /// <c>ProfessionalRenderer</c>，那是 Office 2003 的观感 —— 渐变蓝、方角、粗边框，
    /// 摆在已经换成 iOS 风格的设置面板旁边像是两个时代的东西。
    /// 换渲染器不用改任何一行业务代码，是最省事也最不容易出 bug 的做法。
    /// </summary>
    public sealed class KvMenuRenderer : ToolStripProfessionalRenderer
    {
        /// <summary>卡片圆角半径。</summary>
        public const int Radius = 10;


        private readonly Color _card, _text, _sub, _accent, _soft, _hover, _sep, _border, _danger;

        private Size _lastRegion = Size.Empty;

        public KvMenuRenderer(KvPalette p)
            : base(new Table(p ?? KvPalette.Dark))
        {
            KvPalette pal = p ?? KvPalette.Dark;
            _card = C(pal.Card);
            _text = C(pal.Text);
            _sub = C(pal.Sub);
            _accent = C(pal.Accent);
            _soft = C(pal.AccentSoft);
            _hover = C(pal.Hover);
            _sep = C(pal.Separator);
            _border = C(pal.Border);
            _danger = C(pal.Danger);

            // 默认的「圆角边缘」是给专业的渐变边框用的，我们自己在描边上处理
            RoundedEdges = false;
        }

        private static Color C(string hex)
        {
            try { return ColorTranslator.FromHtml(hex); }
            catch { return Color.Gray; }
        }

        /// <summary>让默认的绘制路径（我们没接管的那几处）也拿到同一套颜色。</summary>
        private sealed class Table : ProfessionalColorTable
        {
            private readonly KvPalette _p;

            public Table(KvPalette p)
            {
                _p = p;
                // 不能 override（基类里不是 virtual），只能设属性
                UseSystemColors = false;
            }

            private static Color C(string hex)
            {
                try { return ColorTranslator.FromHtml(hex); }
                catch { return Color.Gray; }
            }

            public override Color ToolStripDropDownBackground => C(_p.Card);
            public override Color ImageMarginGradientBegin => C(_p.Card);
            public override Color ImageMarginGradientMiddle => C(_p.Card);
            public override Color ImageMarginGradientEnd => C(_p.Card);
            public override Color MenuBorder => C(_p.Border);
            public override Color MenuItemBorder => Color.Empty;
            public override Color MenuItemSelected => C(_p.AccentSoft);
            public override Color MenuItemSelectedGradientBegin => C(_p.AccentSoft);
            public override Color MenuItemSelectedGradientEnd => C(_p.AccentSoft);
            public override Color MenuItemPressedGradientBegin => C(_p.AccentSoft);
            public override Color MenuItemPressedGradientEnd => C(_p.AccentSoft);
            public override Color SeparatorDark => C(_p.Separator);
            public override Color SeparatorLight => C(_p.Separator);
        }

        // ── 卡片本体 ─────────────────────────────────────────────

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            if (!(e.ToolStrip is ToolStripDropDown))
            {
                base.OnRenderToolStripBackground(e);
                return;
            }

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rc = new Rectangle(Point.Empty, e.ToolStrip.Size);

            using (var b = new SolidBrush(_card)) g.FillRectangle(b, rc);
            ClipRounded(e.ToolStrip, rc);
        }

        /// <summary>
        /// 把窗口本身裁成圆角。Region 只在尺寸变化时才重设 ——
        /// 每次绘制都赋一次新 Region 会让控件不停地重绘，菜单会闪。
        /// </summary>
        private void ClipRounded(ToolStrip ts, Rectangle rc)
        {
            if (_lastRegion == rc.Size && ts.Region != null) return;
            _lastRegion = rc.Size;

            using (var path = Rounded(rc, Radius))
            {
                var old = ts.Region;
                ts.Region = new Region(path);
                old?.Dispose();
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (!(e.ToolStrip is ToolStripDropDown)) { base.OnRenderToolStripBorder(e); return; }

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rc = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (var path = Rounded(rc, Radius))
            using (var pen = new Pen(_border))
                g.DrawPath(pen, path);
        }

        /// <summary>默认会在左侧画一条渐变竖带，换成和卡片同色。</summary>
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            var rc = e.AffectedBounds;
            using (var b = new SolidBrush(_card)) e.Graphics.FillRectangle(b, rc);
        }

        // ── 行 ───────────────────────────────────────────────────

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            bool hot = item.Selected || item.Pressed;

            if (!hot)
            {
                // 有些平台会把上一个高亮留在背景里，主动刷掉
                using (var b = new SolidBrush(_card))
                    e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, item.Size));
                return;
            }

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rc = new Rectangle(3, 1, item.Width - 7, item.Height - 3);

            using (var path = Rounded(rc, 7))
            using (var b = new SolidBrush(item.Enabled ? _soft : _hover))
                g.FillPath(b, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var item = e.Item;

            // ToolStripMenuItem 的快捷键是**另一次** OnRenderItemText 调用，
            // 这一趟的 e.Text 就是 ShortcutKeyDisplayString —— 靠它区分，
            // 否则会把快捷键也画成主文字色（甚至危险项的红色）。
            bool shortcut = item is ToolStripMenuItem mi
                            && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString)
                            && mi.ShortcutKeyDisplayString == e.Text;

            bool danger = item.Tag as string == "danger";

            if (!item.Enabled) e.TextColor = _sub;
            else if (shortcut) e.TextColor = _sub;
            else if (item.Selected) e.TextColor = _accent;
            else if (danger) e.TextColor = _danger;
            else e.TextColor = _text;

            if (Environment.GetEnvironmentVariable("CKV_MENUDIAG") != null)
                Core.Diag.Log(string.Format("menutext '{0}' size={1} rect={2} color={3} shortcut={4}",
                    e.Text, item.Size, e.TextRectangle, e.TextColor, shortcut));

            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            if (e.Vertical || !(e.Item is ToolStripSeparator)) { base.OnRenderSeparator(e); return; }

            var g = e.Graphics;
            int y = e.Item.Height / 2;
            using (var pen = new Pen(_sep))
                g.DrawLine(pen, 10, y, e.Item.Width - 11, y);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = e.ImageRectangle;
            float cx = r.Left + r.Width * 0.5f;
            float cy = r.Top + r.Height * 0.5f;

            using (var pen = new Pen(_accent, 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - 5f, cy + 0.5f),
                    new PointF(cx - 1.5f, cy + 4f),
                    new PointF(cx + 5f, cy - 4f),
                });
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = e.ArrowRectangle;
            float cx = r.Left + r.Width * 0.5f - 1f;
            float cy = r.Top + r.Height * 0.5f;
            Color c = e.Item.Enabled ? _sub : Color.FromArgb(110, _sub);

            using (var pen = new Pen(c, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - 2f, cy - 4.5f),
                    new PointF(cx + 2.5f, cy),
                    new PointF(cx - 2f, cy + 4.5f),
                });
            }
        }

        // ── 工具 ─────────────────────────────────────────────────

        private static GraphicsPath Rounded(Rectangle r, int radius)
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
    }
}
