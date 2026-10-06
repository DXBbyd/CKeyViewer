using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CKeyViewer.Core;

namespace CKeyViewer.Render
{
    /// <summary>
    /// 单元素渲染器：整个 KV 在 OnRender 里一次画完（键帽 + 文本 + 雨线）。
    /// 使用即时模式绘制，避免大量 WPF 元素带来的开销。
    /// </summary>
    public sealed class OverlayRenderer : FrameworkElement
    {
        // ---- 输入 ----
        public IList<KeySlot> Slots;
        public IDictionary<int, KeyRuntime> Keys;
        public KvTheme Theme = new KvTheme();

        /// <summary>
        /// 每个键槽最终生效的配色 / 字号（已由 KvHost 预解析）。
        /// 查不到时回落到 <see cref="Theme"/> 的全局配色。
        /// </summary>
        public IDictionary<int, KeyStyle> Styles;

        /// <summary>当前是否自由布局（KeyViewerStyle.Custom）。</summary>
        public bool IsCustomLayout;

        /// <summary>是否处于布局模式（在覆盖层上直接拖动节点）。</summary>
        public bool LayoutMode;

        /// <summary>布局模式下被选中的节点（高亮显示）。</summary>
        public KvFmNode SelectedNode;

        /// <summary>布局模式下是否启用方向键微调（提示条据此显示对应说明）。</summary>
        public bool ArrowNudgeHint;

        /// <summary>是否启用按压缩放动画。</summary>
        public bool EnablePressAnimation;

        /// <summary>按压动画是否同时作用于雨线宽度与基线。</summary>
        public bool AnimAffectsRain = true;

        /// <summary>参考像素 → DIP 的缩放。</summary>
        public double Scale = 1.0;

        /// <summary>区块左边界（参考像素）。</summary>
        public double OriginX;

        /// <summary>区块最高点（局部 y 最大值）。</summary>
        public double TopExtent;

        /// <summary>雨线区在内容之上额外占用的高度（参考单位），仅用于诊断。</summary>
        public double RainPadding;

        /// <summary>区块尺寸（参考像素，含雨线区）。</summary>
        public double BlockWidth = 1, BlockHeight = 1;


        public double KeyFontSize = 21;
        public double CountFontSize = 14;
        public string FontRef = "Segoe UI";
        public bool Bold = true;
        public bool Italic;

        // ---- 覆盖层背景图片 ----
        public string BackgroundImagePath;
        public int BackgroundImageMode;            // 0=拉伸 1=覆盖 2=适应
        public KvColor BackgroundImageBorder = KvColor.Rgba(0.6078f, 0.302f, 1.0f, 1.0f);
        public double BackgroundImageBorderWidth;
        public double BackgroundImageOpacity = 1.0;

        public bool CountFormatting = true;
        public string KpsLabel = "KPS";
        public string TotalLabel = "Total";
        public bool HideKpsTotalLabel;
        public bool StreamerMode;
        public bool HideMainKeyCount;

        public double TotalCount;
        public int TotalKps;

        /// <summary>当前时间（秒），由上层每帧写入。</summary>
        public double Now;

        /// <summary>OnRender 被调用的次数（诊断用）。</summary>
        public int RenderCount;

        // 雨线
        public bool RainEnabled = true;
        public IRainLayer Rain;

        /// <summary>逐键槽字体缓存：避免每帧都去解析 Typeface。</summary>
        private readonly Dictionary<string, Typeface> _tfCache = new Dictionary<string, Typeface>();

        protected override void OnRender(DrawingContext dc)
        {
            RenderCount++;
            if (Slots == null) return;

            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = KvFonts.Resolve(FontRef, Bold, Italic);

            // 背景图片画在最底层（在雨线 / 键帽 / 网格之前）
            DrawBackground(dc);

            if (RainEnabled && Rain != null)
                Rain.Draw(dc, this, ppd);

            if (LayoutMode) DrawLayoutGrid(dc);

            foreach (var slot in Slots)
            {
                DrawKey(dc, slot, typeface, ppd);
            }

            if (LayoutMode)
            {
                // 选中高亮与提示条画在键帽之上，否则会被键帽盖住
                DrawLayoutSelection(dc, typeface, ppd);
                DrawLayoutBanner(dc, typeface, ppd);
            }

            // ADOFAI 信息不再画在这里 —— 它有自己的全屏窗口（AdofaiWindow），
            // 这样元素才能被拖到屏幕的任何位置，而不受这块键帽窗口的限制。
        }

        // ---------------------------------------------------------------
        // 覆盖层背景图片
        // ---------------------------------------------------------------

        /// <summary>
        /// 把 <see cref="BackgroundImagePath"/> 指定的图片铺在覆盖层窗口后面，
        /// 并在其外缘画一圈描边（让背景与桌面分隔得更干净）。
        /// </summary>
        private void DrawBackground(DrawingContext dc)
        {
            if (string.IsNullOrWhiteSpace(BackgroundImagePath)) return;
            var img = LoadImage(BackgroundImagePath);
            if (img == null) return;

            double w = Math.Max(1, ActualWidth);
            double h = Math.Max(1, ActualHeight);

            double border = Math.Max(0, BackgroundImageBorderWidth * Scale);
            // 收缩半个线宽，免得描边被画布边缘裁掉
            var rect = new Rect(
                border * 0.5,
                border * 0.5,
                Math.Max(0, w - border),
                Math.Max(0, h - border));

            if (BackgroundImageOpacity > 0.001 && BackgroundImageOpacity < 0.999)
                dc.PushOpacity(BackgroundImageOpacity);

            Rect target = FitImage(img.Width, img.Height, rect, BackgroundImageMode);
            dc.DrawImage(img, target);

            if (border > 0 && !BackgroundImageBorder.IsInvisible)
            {
                var pen = new Pen(BackgroundImageBorder.ToBrush(), border);
                pen.Freeze();
                dc.DrawRoundedRectangle(null, pen, rect, Math.Min(border, 8), Math.Min(border, 8));
            }

            if (BackgroundImageOpacity > 0.001 && BackgroundImageOpacity < 0.999)
                dc.Pop();
        }

        /// <summary>按缩放模式把图片映射进目标矩形：0=拉伸 / 1=覆盖（裁切）/ 2=适应（留边）。</summary>
        private static Rect FitImage(double imgW, double imgH, Rect area, int mode)
        {
            if (imgW <= 0 || imgH <= 0) return area;
            double aw = area.Width, ah = area.Height;
            if (mode == 1) // 覆盖：等比放大到铺满，居中裁切
            {
                double s = Math.Max(aw / imgW, ah / imgH);
                double dw = imgW * s, dh = imgH * s;
                return new Rect(area.X + (aw - dw) * 0.5, area.Y + (ah - dh) * 0.5, dw, dh);
            }
            if (mode == 2) // 适应：等比缩小到完整显示，居中留边
            {
                double s = Math.Min(aw / imgW, ah / imgH);
                double dw = imgW * s, dh = imgH * s;
                return new Rect(area.X + (aw - dw) * 0.5, area.Y + (ah - dh) * 0.5, dw, dh);
            }
            return area; // 拉伸填满
        }

        /// <summary>解析某键槽的字体：为空（或解析失败）时回落到全局 <see cref="FontRef"/>。</summary>
        private Typeface ResolveSlotTypeface(string fontRef)
        {
            if (string.IsNullOrWhiteSpace(fontRef)) return null;
            lock (_tfCache)
            {
                if (_tfCache.TryGetValue(fontRef, out var cached)) return cached;
                var tf = KvFonts.Resolve(fontRef, Bold, Italic);
                _tfCache[fontRef] = tf;
                return tf;
            }
        }

        // ---------------------------------------------------------------
        // 布局模式（拖动编辑）的视觉层
        // ---------------------------------------------------------------

        private static readonly Brush GridThin = Frozen(Color.FromArgb(24, 155, 77, 255));
        private static readonly Brush GridBold = Frozen(Color.FromArgb(58, 155, 77, 255));
        private static readonly Brush NodeOutline = Frozen(Color.FromArgb(130, 255, 255, 255));
        private static readonly Brush SelectFill = Frozen(Color.FromArgb(46, 155, 77, 255));
        private static readonly Brush SelectStroke = Frozen(Color.FromRgb(155, 77, 255));
        private static readonly Brush BannerBg = Frozen(Color.FromArgb(226, 33, 29, 43));
        private static readonly Brush BannerFg = Frozen(Color.FromRgb(233, 229, 242));

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>编辑参考网格 + 每个节点的虚线轮廓（画在键帽下方，不挡内容）。</summary>
        private void DrawLayoutGrid(DrawingContext dc)
        {
            if (!(Scale > 0.0001)) return;

            double w = Math.Max(1, ActualWidth);
            double h = Math.Max(1, ActualHeight);

            var thin = new Pen(GridThin, 1); thin.Freeze();
            var bold = new Pen(GridBold, 1); bold.Freeze();

            // 每 60 单位一条淡线，每 540 单位（= 半个 1080 画布）一条亮线
            for (int i = 0; ; i++)
            {
                double x = i * 60.0 * Scale;
                if (x > w) break;
                dc.DrawLine((i % 9 == 0) ? bold : thin, new Point(x, 0), new Point(x, h));
            }
            for (int i = 0; ; i++)
            {
                double y = i * 60.0 * Scale;
                if (y > h) break;
                dc.DrawLine((i % 9 == 0) ? bold : thin, new Point(0, y), new Point(w, y));
            }

            var dash = new Pen(NodeOutline, 1) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) };
            dash.Freeze();

            foreach (var slot in Slots)
            {
                var n = slot.Node;
                if (n == null) continue;
                var r = NodeRect(n);
                if (r.Width <= 0 || r.Height <= 0) continue;
                dc.DrawRoundedRectangle(null, dash, r, 3, 3);
            }
        }

        /// <summary>选中节点的高亮框 + 四角把手 + 坐标标签。</summary>
        private void DrawLayoutSelection(DrawingContext dc, Typeface typeface, double ppd)
        {
            var sel = SelectedNode;
            if (sel == null) return;

            var sr = NodeRect(sel);
            var selPen = new Pen(SelectStroke, Math.Max(1.5, 2 * Scale));
            selPen.Freeze();
            dc.DrawRoundedRectangle(null, selPen, sr, 3, 3);

            // 四角把手，明示「这个可以拖」
            double grip = Math.Max(3, 4 * Scale);
            foreach (var pt in new[]
            {
                new Point(sr.Left, sr.Top), new Point(sr.Right, sr.Top),
                new Point(sr.Left, sr.Bottom), new Point(sr.Right, sr.Bottom)
            })
            {
                dc.DrawRectangle(SelectStroke, null,
                    new Rect(pt.X - grip, pt.Y - grip, grip * 2, grip * 2));
            }

            string info = string.Format("#{0}  X {1:0}  Y {2:0}  {3:0}×{4:0}",
                sel.Id, sel.X, sel.Y, sel.Width, sel.Height);
            DrawChip(dc, typeface, ppd, info, sr);
        }

        private Rect NodeRect(KvFmNode n)
        {
            return new Rect(n.X * Scale, n.Y * Scale,
                            Math.Max(0, n.Width * Scale), Math.Max(0, n.Height * Scale));
        }

        /// <summary>在节点上方贴一个坐标小标签（超出屏幕顶部时自动翻到节点下方）。</summary>
        private void DrawChip(DrawingContext dc, Typeface typeface, double ppd, string text, Rect anchor)
        {
            double font = Math.Max(10, 13 * Scale);
            var probe = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                          typeface, font, BannerFg, ppd);
            double pad = 5;
            double bw = probe.Width + pad * 2;
            double bh = probe.Height + pad;

            double bx = anchor.X;
            double by = anchor.Y - bh - 4;
            if (by < 2) by = anchor.Bottom + 4;
            if (bx + bw > ActualWidth) bx = Math.Max(0, ActualWidth - bw - 2);

            dc.DrawRoundedRectangle(BannerBg, null, new Rect(bx, by, bw, bh), 3, 3);
            dc.DrawText(probe, new Point(bx + pad, by + pad * 0.5));
        }

        /// <summary>顶部操作提示条。</summary>
        private void DrawLayoutBanner(DrawingContext dc, Typeface typeface, double ppd)
        {
            string arrow = ArrowNudgeHint ? " · 方向键微调（Shift ×10）" : "";
            string text = SelectedNode != null
                ? "布局模式：拖动节点调整位置" + arrow + " · Esc 退出"
                : "布局模式：点击一个节点开始拖动" + arrow + " · Esc 退出";

            double font = Math.Max(11, 14 * Scale);
            var probe = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                          typeface, font, BannerFg, ppd);

            double pad = 10;
            double bw = Math.Min(Math.Max(1, ActualWidth - 20), probe.Width + pad * 2);
            double bh = probe.Height + pad;
            double bx = (ActualWidth - bw) * 0.5;
            const double by = 8;

            dc.DrawRoundedRectangle(BannerBg, new Pen(SelectStroke, 1), new Rect(bx, by, bw, bh), 6, 6);
            DrawTextCentered(dc, text, typeface, font, KvColor.White,
                             new Rect(bx, by, bw, bh), ppd, false, KvColor.Black, 0, false, KvColor.Black, 0, 0);
        }

        // ---------------------------------------------------------------
        // 单个键槽
        // ---------------------------------------------------------------

        private void DrawKey(DrawingContext dc, KeySlot slot, Typeface typeface, double ppd)
        {
            KeyRuntime key = null;
            if (slot.Index >= 0 && Keys != null) Keys.TryGetValue(slot.Index, out key);

            double x = (slot.X - OriginX) * Scale;
            double y = (TopExtent - slot.Top) * Scale;
            double w = slot.W * Scale;
            double h = slot.H * Scale;

            bool pressed = key != null && key.Pressed;

            // 按压动画：以键帽中心为原点整体缩放（原版对 key.visuals.localScale 做的事）
            double anim = 1.0;
            if (EnablePressAnimation && key != null)
            {
                anim = key.AnimScale;
                if (anim < 0.05 || anim > 4.0) anim = 1.0;
            }

            if (anim != 1.0)
            {
                double cx = x + w * 0.5;
                double cy = y + h * 0.5;
                w *= anim;
                h *= anim;
                x = cx - w * 0.5;
                y = cy - h * 0.5;
            }

            var rect = new Rect(x, y, w, h);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            KvColor bg, outline, fg;
            double fontOverride = 0;
            bool showCount = true, hideLabel = false;
            double opacity = 1.0, radiusOverride = -1, borderOverride = -1;

            // 节点自定义字体：解析一次（带缓存），失败则回落全局字体
            Typeface textTypeface = typeface;

            if (Styles != null && Styles.TryGetValue(slot.Index, out var st))
            {
                bg = st.Bg(pressed); outline = st.Line(pressed); fg = st.Fg(pressed);
                fontOverride = st.FontSize;
                showCount = st.ShowCount;
                hideLabel = st.HideLabel;
                if (st.Opacity > 0) opacity = st.Opacity;
                radiusOverride = st.CornerRadius;
                borderOverride = st.BorderThickness;
                if (!string.IsNullOrEmpty(st.FontRef))
                {
                    var tf = ResolveSlotTypeface(st.FontRef);
                    if (tf != null) textTypeface = tf;
                }
            }
            else if (slot.Index == -1)
            {
                bg = Theme.KpsBackground; outline = Theme.KpsOutline; fg = Theme.KpsText;
            }
            else if (slot.Index == -2)
            {
                bg = Theme.TotalBackground; outline = Theme.TotalOutline; fg = Theme.TotalText;
            }
            else
            {
                bg = pressed ? Theme.BackgroundClicked : Theme.Background;
                outline = pressed ? Theme.OutlineClicked : Theme.Outline;
                fg = pressed ? Theme.TextClicked : Theme.Text;
            }

            bool useOpacity = opacity > 0.0 && opacity < 0.999;
            if (useOpacity) dc.PushOpacity(opacity);

            double radius = (radiusOverride > 0 ? radiusOverride : Theme.CornerRadius) * Scale;
            double borderW = borderOverride > 0 ? borderOverride : Theme.OutlineWidth;

            // ---- 图片节点：直接贴图，不画键帽形状 ----
            var node = slot.Node;
            if (node != null && node.NodeType == 3)
            {
                var img = LoadImage(pressed && !string.IsNullOrEmpty(node.ImagePathPressed)
                                    ? node.ImagePathPressed
                                    : node.ImagePath);
                if (img != null) dc.DrawImage(img, rect);
                if (useOpacity) dc.Pop();
                return;
            }

            if (!bg.IsInvisible)
                dc.DrawRoundedRectangle(bg.ToBrush(), null, rect, radius, radius);

            if (!outline.IsInvisible && borderW > 0)
            {
                var pen = new Pen(outline.ToBrush(), borderW * Scale);
                pen.Freeze();
                // 描边居中在边界上，收缩半个线宽以免被裁掉
                double half = borderW * Scale * 0.5;
                var inset = new Rect(rect.X + half, rect.Y + half,
                                     Math.Max(0, rect.Width - half * 2), Math.Max(0, rect.Height - half * 2));
                dc.DrawRoundedRectangle(null, pen, inset, radius, radius);
            }

            // KPS / Total 条没有 KeyRuntime，必须先于空检查处理
            if (slot.IsStat)
            {
                DrawStatText(dc, slot, rect, typeface, ppd, fg);
                if (useOpacity) dc.Pop();
                return;
            }

            if (key == null)
            {
                if (useOpacity) dc.Pop();
                return;
            }

            DrawKeyText(dc, slot, rect, key, textTypeface, ppd, fg, fontOverride, showCount, hideLabel);
            if (useOpacity) dc.Pop();
        }

        // ---- 图片缓存（自由布局的 ImagePath / ImagePathPressed）----

        private static readonly Dictionary<string, ImageSource> ImageCache =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        private static ImageSource LoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (ImageCache.TryGetValue(path, out var cached)) return cached;

            ImageSource src = null;
            try
            {
                if (System.IO.File.Exists(path))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    src = bmp;
                }
            }
            catch
            {
                src = null;   // 路径不存在 / 格式不支持：当作没有图片
            }

            ImageCache[path] = src;
            return src;
        }

        /// <summary>丢开图片缓存（节点改了图片路径时调用）。</summary>
        public static void ClearImageCache() => ImageCache.Clear();

        private void DrawKeyText(DrawingContext dc, KeySlot slot, Rect rect, KeyRuntime key,
                                 Typeface typeface, double ppd, KvColor fg,
                                 double fontOverride, bool showCount, bool hideLabel)
        {
            double baseFont = fontOverride > 0 ? fontOverride : KeyFontSize;

            // 键名：在整个键帽内垂直+水平居中（DisplayLabel 会在按下时切到 PressedText）
            string label = hideLabel ? null : key.DisplayLabel;
            if (!string.IsNullOrEmpty(label))
            {
                DrawTextCentered(dc, label, typeface, baseFont * Scale, fg, rect, ppd,
                                 Theme.EnableKeyTextOutline, Theme.KeyTextOutlineColor, Theme.KeyTextOutlineThickness,
                                 Theme.EnableKeyTextShadow, Theme.KeyTextShadowColor,
                                 Theme.KeyTextShadowOffsetX, Theme.KeyTextShadowOffsetY);
            }

            if (!showCount || HideMainKeyCount || key.IsFootKey) return;

            // 计数：贴底居中的小字，不与键名抢占视觉中心
            double countH = Math.Min(rect.Height * 0.32, 15 * Scale);
            var countRect = new Rect(rect.X, rect.Y + rect.Height - countH, rect.Width, countH);
            string countText = NumFormat.Format(key.Count, CountFormatting);
            DrawTextCentered(dc, countText, typeface, CountFontSize * Scale, fg, countRect, ppd,
                             Theme.EnableCountTextOutline, Theme.CountTextOutlineColor, Theme.CountTextOutlineThickness,
                             Theme.EnableCountTextShadow, Theme.CountTextShadowColor,
                             Theme.CountTextShadowOffsetX, Theme.CountTextShadowOffsetY);
        }

        private void DrawStatText(DrawingContext dc, KeySlot slot, Rect rect, Typeface typeface,
                                  double ppd, KvColor fg)
        {
            if (StreamerMode) return;

            double fontSize = KeyFontSize * Scale;
            string text;

            if (slot.Index == -1)
            {
                text = HideKpsTotalLabel ? TotalKps.ToString() : KpsLabel + "   " + TotalKps;
            }
            else
            {
                text = HideKpsTotalLabel
                    ? NumFormat.Format((long)TotalCount, CountFormatting)
                    : TotalLabel + "   " + NumFormat.Format((long)TotalCount, CountFormatting);
            }

            // 按可用宽度自动缩字号。
            //
            // 为什么必须缩：Key10 / Key12 这两个预设的统计条只有 **77 个单位宽**
            // （KvGeometry 里 ExtraSlot(-1, 0, 225, 77, -1)），屏幕上约 60px；
            // 而「总和 942,253」这种文本在默认字号 22.47 下要 ~90px。
            // 更糟的是 Total 那条的右边界正好等于区块右边界（X=351 + W=77 = 428），
            // 于是超出的部分**不是被框子裁掉而是被窗口裁掉** ——
            // 症状就是「总数那串数字不见了 / 只剩一半」。
            // Key14/16/20/24 的条是 212 宽，所以那几个预设看不出来。
            fontSize = FitStatFont(text, typeface, fontSize, rect, ppd);

            DrawTextCentered(dc, text, typeface, fontSize, fg, rect, ppd,
                             Theme.EnableKeyTextOutline, Theme.KeyTextOutlineColor, Theme.KeyTextOutlineThickness,
                             Theme.EnableKeyTextShadow, Theme.KeyTextShadowColor,
                             Theme.KeyTextShadowOffsetX, Theme.KeyTextShadowOffsetY);
        }

        /// <summary>
        /// 把 <paramref name="fontSize"/> 缩到刚好能把 <paramref name="text"/> 放进
        /// <paramref name="rect"/> 的宽度里；本来就放得下就原样返回。
        /// <para>
        /// 下限 7 DIP —— 再小就真的看不清了。
        /// </para>
        /// </summary>
        private double FitStatFont(string text, Typeface typeface, double fontSize, Rect rect, double ppd)
        {
            if (string.IsNullOrEmpty(text)) return fontSize;

            // 左右各留 4 个单位当内边距，不然文字会贴着圆角边框
            double avail = rect.Width - 8 * Scale;
            if (avail <= 4 || fontSize <= 0) return fontSize;

            var probe = new FormattedText(text, CultureInfo.InvariantCulture,
                                          FlowDirection.LeftToRight, typeface, fontSize,
                                          Brushes.Black, ppd);
            double w = probe.WidthIncludingTrailingWhitespace;
            if (w <= avail) return fontSize;

            double scaled = fontSize * (avail / w);
            const double MinDip = 7.0;
            return Math.Max(scaled, MinDip * Scale);
        }

        // ---------------------------------------------------------------
        // 文本工具
        // ---------------------------------------------------------------

        public void DrawTextCentered(DrawingContext dc, string text, Typeface typeface, double fontSize,
                                     KvColor fg, Rect area, double ppd,
                                     bool outline, KvColor outlineColor, double outlineWidth,
                                     bool shadow, KvColor shadowColor, double shadowDx, double shadowDy)
        {
            if (string.IsNullOrEmpty(text) || area.Width <= 1 || area.Height <= 1) return;

            FormattedText Make(Brush brush)
            {
                // 注意：绝不能设 MaxTextHeight —— 行高略大于区域高度时 WPF 会把整段文字裁掉，
                // 这正是之前「按键计数看不见」的原因。
                return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                         typeface, fontSize, brush, ppd)
                {
                    TextAlignment = TextAlignment.Center,
                    MaxTextWidth = area.Width,
                    Trimming = TextTrimming.None
                };
            }

            var probe = Make(Brushes.Black);
            double lineH = probe.Height;
            double top = area.Y + Math.Max(0, (area.Height - lineH) * 0.5);
            double cx = area.X + area.Width * 0.5;

            if (shadow && !shadowColor.IsInvisible)
            {
                dc.DrawText(Make(shadowColor.ToBrush()),
                            new Point(area.X + shadowDx * Scale, top + shadowDy * Scale));
            }

            if (outline && !outlineColor.IsInvisible)
            {
                var geometry = probe.BuildGeometry(new Point(0, 0));
                if (!geometry.IsEmpty())
                {
                    var pen = new Pen(outlineColor.ToBrush(), Math.Max(0.5, outlineWidth * fontSize));
                    pen.LineJoin = PenLineJoin.Round;
                    pen.Freeze();

                    var shifted = geometry.Clone();
                    shifted.Transform = new TranslateTransform(area.X, top);
                    shifted.Freeze();
                    dc.DrawGeometry(null, pen, shifted);
                }
            }

            dc.DrawText(Make(fg.ToBrush()), new Point(area.X, top));
            _ = cx;
        }

        public void Invalidate() => InvalidateVisual();
    }

    /// <summary>雨线层接口。由上层每帧调用 <see cref="Update"/> 推进，再由 <see cref="Draw"/> 绘制。</summary>
    public interface IRainLayer
    {
        /// <summary>
        /// 推进雨线动画并回收已结束的条目，应在绘制前调用。
        /// 返回是否仍有活跃雨线 —— 上层据此决定是否需要重绘。
        /// </summary>
        bool Update(IEnumerable<KeyRuntime> keys, double now);

        void Draw(DrawingContext dc, OverlayRenderer host, double pixelsPerDip);
    }
}
