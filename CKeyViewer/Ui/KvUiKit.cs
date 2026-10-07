using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CKeyViewer.Core;

namespace CKeyViewer.Ui
{
    /// <summary>
    /// 设置面板的控件工厂。所有控件都是「读 → 改 → 立即生效」的三段式：
    /// 取值靠 getter 委托，写值靠 setter 委托，setter 里统一触发重载与延迟落盘。
    /// </summary>
    internal static class Kit
    {
        // ---- 配色（iOS 风格，浅色 / 深色两套，运行时可切）----

        private static KvPalette _palette = KvPalette.Dark;
        private static readonly Dictionary<string, Brush> BrushCache = new Dictionary<string, Brush>();

        /// <summary>当前调色板。</summary>
        public static KvPalette Palette => _palette;

        /// <summary>当前是不是深色。</summary>
        public static bool IsDark => _palette.IsDark;

        /// <summary>
        /// 主题变了。已经建好的控件不会自己换色（画刷是冻结的），
        /// 所以监听方要负责重建界面 —— 设置面板就是靠这个把整棵树重刷一遍。
        /// </summary>
        public static event Action ThemeChanged;

        /// <summary>切换主题；相同主题重复调用是空操作。</summary>
        public static void UseTheme(bool dark) => UseTheme(KvPalette.For(dark ? "dark" : "light"));

        public static void UseTheme(KvPalette p)
        {
            if (p == null || ReferenceEquals(p, _palette)) return;
            _palette = p;
            BrushCache.Clear();
            try { ThemeChanged?.Invoke(); } catch { }
        }

        /// <summary>十六进制 → 冻结画刷（带缓存，别每次访问都新建）。</summary>
        private static Brush B(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Brushes.Transparent;
            if (BrushCache.TryGetValue(hex, out var b)) return b;
            var nb = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            nb.Freeze();
            BrushCache[hex] = nb;
            return nb;
        }

        private static Brush Freeze(string hex) => B(hex);

        public static Brush Bg => B(_palette.Bg);
        public static Brush Panel => B(_palette.Card);
        public static Brush PanelAlt => B(_palette.CardAlt);
        public static Brush Sidebar => B(_palette.Sidebar);
        public static Brush Border => B(_palette.Border);
        public static Brush Separator => B(_palette.Separator);
        public static Brush Text => B(_palette.Text);
        public static Brush Sub => B(_palette.Sub);
        public static Brush Accent => B(_palette.Accent);
        public static Brush AccentDim => B(_palette.AccentSoft);
        public static Brush Hover => B(_palette.Hover);
        public static Brush Danger => B(_palette.Danger);
        public static Brush DangerSoft => B(_palette.DangerSoft);

        public const double RowHeight = 38;

        internal static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI, Segoe UI");

        public static TextBlock Text2(string s, double size = 12.5, Brush brush = null,
                                      bool bold = false, TextAlignment align = TextAlignment.Left)
        {
            return new TextBlock
            {
                Text = s,
                FontSize = size,
                FontFamily = UiFont,
                Foreground = brush ?? Text,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                TextAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };
        }

        // ---------------------------------------------------------------
        // 容器
        // ---------------------------------------------------------------

        /// <summary>标记：这个元素是分组标题，<see cref="WrapGroups"/> 靠它切卡片。</summary>
        internal const string TagSection = "kv:section";

        /// <summary>标记：这个元素是一行列表项，卡片内部会在相邻两行之间插分隔线。</summary>
        internal const string TagRow = "kv:row";

        /// <summary>
        /// 分组标题。iOS 的分组列表里标题在卡片**外面**、字号小、颜色灰，
        /// 所以这里不再用强调色 + 分隔线的旧样式；真正把它和下面几行包成一张卡片的是
        /// <see cref="WrapGroups"/>。
        /// </summary>
        public static FrameworkElement Section(string title)
        {
            var t = Text2(title, 12.5, Sub, bold: false);
            t.Margin = new Thickness(4, 18, 0, 6);
            t.Tag = TagSection;
            return t;
        }

        /// <summary>页面大标题 —— iOS 那种「大标题 + 说明」的顶部区。</summary>
        public static FrameworkElement LargeTitle(string title, string subtitle = null)
        {
            var sp = new StackPanel { Margin = new Thickness(4, 0, 0, 4) };
            var t = Text2(title, 21, Text, bold: true);
            sp.Children.Add(t);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var s = Text2(subtitle, 12, Sub);
                s.Margin = new Thickness(0, 3, 0, 0);
                s.TextWrapping = TextWrapping.Wrap;
                sp.Children.Add(s);
            }
            return sp;
        }

        /// <summary>
        /// 左侧标签 + 右侧控件的标准行。iOS 的列表行是「标签用主文字色」而不是灰色，
        /// 这里跟着改，浅色下才不会一片灰糊。
        /// </summary>
        public static FrameworkElement Row(string label, UIElement ctrl, double labelWidth = 168)
        {
            var grid = new Grid { MinHeight = RowHeight, Height = double.NaN, Background = Brushes.Transparent };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var t = Text2(label, 13, Text);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Margin = new Thickness(0, 0, 8, 0);
            t.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(t, 0);
            grid.Children.Add(t);

            if (ctrl is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ctrl, 1);
            grid.Children.Add(ctrl);

            grid.Tag = TagRow;
            return grid;
        }

        /// <summary>把一段内容包成 iOS 的分组卡片（白/深灰底 + 圆角 + 内缩）。</summary>
        public static Border Card(params UIElement[] children)
        {
            var sp = new StackPanel();
            for (int i = 0; i < children.Length; i++)
            {
                if (i > 0 && IsRow(children[i - 1]) && IsRow(children[i]))
                    sp.Children.Add(RowSeparator());
                sp.Children.Add(children[i]);
            }

            return new Border
            {
                Background = Panel,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 0, 2),
                Child = sp
            };
        }

        internal static bool IsSection(UIElement e) =>
            e is FrameworkElement fe && fe.Tag is string s && s == TagSection;

        internal static bool IsRow(UIElement e) =>
            e is FrameworkElement fe && fe.Tag is string s && s == TagRow;

        /// <summary>卡片内部两行之间的细分隔线（左侧留出内缩，跟 iOS 一致）。</summary>
        public static Border RowSeparator()
        {
            return new Border
            {
                Height = 1,
                Background = Separator,
                Margin = new Thickness(0, 0, 0, 0)
            };
        }

        /// <summary>
        /// 把「分组标题 + 它下面的一串行」重组成「标题 + 一张圆角卡片」。
        ///
        /// 为什么要做这个后处理而不是改每个页面的写法：设置面板有 13 个页面、几百行
        /// <c>p.Children.Add(Kit.Xxx(...))</c>，逐处改成 Kit.Group(...) 改动面太大、
        /// 也容易漏。这里在页面构建完之后统一扫一遍，调用点一行都不用动。
        /// </summary>
        public static void WrapGroups(Panel panel)
        {
            if (panel == null) return;

            var flat = new List<UIElement>();
            foreach (UIElement c in panel.Children) flat.Add(c);
            panel.Children.Clear();

            var bucket = new List<UIElement>();

            void Flush()
            {
                if (bucket.Count == 0) return;
                panel.Children.Add(Card(bucket.ToArray()));
                bucket.Clear();
            }

            foreach (var c in flat)
            {
                if (IsSection(c)) { Flush(); panel.Children.Add(c); continue; }
                if (IsRow(c)) { bucket.Add(c); continue; }

                // 提示文字 / 错误信息 / 自定义块：打断卡片，自己单独成段。
                // iOS 的分组列表里说明文字本来就是卡片**外面**的脚注，放进去反而怪。
                Flush();
                if (c is FrameworkElement fe) fe.Margin = new Thickness(4, 6, 4, 10);
                panel.Children.Add(c);
            }
            Flush();
        }

        /// <summary>
        /// iOS 分段控件（Segmented Control）—— 一排等宽按钮，选中的用白/深灰底浮起。
        /// 用来替代一些「只有两三个选项」的下拉，比下拉菜单直观得多。
        /// </summary>
        public static FrameworkElement Segmented(IList<string> items, Func<int> get, Action<int> set,
                                                 double width = 240, double height = 32)
        {
            var outer = new Border
            {
                Background = PanelAlt,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(2),
                Width = width,
                Height = height,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            int n = Math.Max(1, items.Count);

            // 列定义必须建在**真正装控件的那一层**上。上一版建在了一个没被使用的 Grid 上，
            // 结果所有按钮都堆在第 0 列、互相盖住。
            var layer = new Grid();
            for (int k = 0; k < n; k++)
                layer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var slide = new TranslateTransform(0, 0);
            var pill = new Border
            {
                Width = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = B(_palette.SegmentOn),
                CornerRadius = new CornerRadius(7),
                RenderTransform = slide
            };
            layer.Children.Add(pill);

            var texts = new List<TextBlock>();
            var buttons = new List<Button>();

            void Refresh(bool animate)
            {
                int cur = get();
                if (cur < 0 || cur >= n) return;

                double x = 0;
                for (int k = 0; k < cur; k++)
                    x += buttons[k].ActualWidth > 0 ? buttons[k].ActualWidth : (width - 4) / n;

                double wNow = buttons[cur].ActualWidth > 0 ? buttons[cur].ActualWidth : (width - 4) / n;

                pill.Width = wNow;
                if (animate)
                {
                    var anim = new System.Windows.Media.Animation.DoubleAnimation(
                        slide.X, x, TimeSpan.FromMilliseconds(190))
                    {
                        EasingFunction = new System.Windows.Media.Animation.CubicEase
                        { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    slide.BeginAnimation(TranslateTransform.XProperty, anim);
                }
                else
                {
                    slide.BeginAnimation(TranslateTransform.XProperty, null);
                    slide.X = x;
                }

                for (int k = 0; k < n; k++)
                {
                    texts[k].Foreground = k == cur ? Text : Sub;
                    texts[k].FontWeight = k == cur ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }

            for (int k = 0; k < n; k++)
            {
                int idx = k;
                var t = Text2(items[idx], 12.5, Sub, align: TextAlignment.Center);
                var b = new Button
                {
                    Content = t,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Focusable = false
                };
                b.Click += (s, e) => { set(idx); Refresh(true); };
                Grid.SetColumn(b, k);
                texts.Add(t);
                buttons.Add(b);
                layer.Children.Add(b);
            }

            outer.Child = layer;

            outer.SizeChanged += (s, e) => Refresh(false);
            // 首次布局完成后才知道每段多宽，所以在 Loaded 里再摆一次药丸
            outer.Dispatcher.BeginInvoke(new Action(() => Refresh(false)),
                System.Windows.Threading.DispatcherPriority.Loaded);

            return outer;
        }

        /// <summary>
        /// 九宫格吸附选择器。左边一块 3×3 的方格，右边显示当前选中的名字 + 一个「自由」按钮。
        /// <para>
        /// 为什么不是下拉框：吸附只有 9 个位置，而且是「空间关系」——
        /// 摆成九宫格能一眼看出选的是哪个角，下拉框还得逐个读字。
        /// </para>
        /// </summary>
        public static FrameworkElement AnchorPicker(Func<int> get, Action<int> set,
                                                    double cellW = 42, double cellH = 30)
        {
            var wrap = new Grid();
            wrap.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            wrap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            wrap.MinHeight = RowHeight;

            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            for (int r = 0; r < 3; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < 3; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var cells = new Border[10];
            var marks = new Border[10];

            var side = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var name = Text2("", 13, Text, bold: true);
            side.Children.Add(name);

            // 先声明后赋值：Refresh() 里要读 freeBtn，而按钮自己的回调又调用 Refresh()。
            // 写成一个 var 声明的话，编译器会判定「在赋值前就被使用」（CS0165）。
            Button freeBtn = null;
            freeBtn = Button("自由摆放", () => { set((int)KvAnchor.Free); Refresh(); }, width: 96);
            freeBtn.Margin = new Thickness(0, 6, 0, 0);
            freeBtn.HorizontalAlignment = HorizontalAlignment.Left;
            side.Children.Add(freeBtn);

            void Refresh()
            {
                int cur = KvSnap.Clamp(get());
                for (int a = 1; a <= 9; a++)
                {
                    bool on = a == cur;
                    cells[a].Background = on ? Accent : PanelAlt;
                    cells[a].BorderBrush = on ? Accent : Border;
                    marks[a].Background = on ? Brushes.White : Sub;
                }

                // 「自由摆放」其实是第 10 个选项，不是「取消」按钮 —— 文案固定四个字
                // （写成「改为自由摆放」六个字会被 96px 的按钮裁掉尾巴），
                // 当前状态靠底色点不点亮来区分，和九宫格是同一套视觉语言。
                bool free = cur == 0;
                freeBtn.Background = free ? Accent : PanelAlt;
                freeBtn.BorderBrush = free ? Accent : Border;
                freeBtn.BorderThickness = new Thickness(free ? 0 : 1);
                freeBtn.Foreground = free ? Brushes.White : Text;

                name.Text = free ? "当前：自由摆放" : "当前：" + KvSnap.NameOf(cur) + " 吸附";
            }

            for (int a = 1; a <= 9; a++)
            {
                int idx = a;
                int col = (idx - 1) % 3;
                int row = (idx - 1) / 3;

                // 格子里画一个「小屏幕 + 落在哪个角的小方块」，一眼就能对上位置。
                // 屏幕边框原来用 Border + 0.7 透明度，在深色卡上基本看不见，
                // 整格读起来就是一团灰 —— 换成次要文字色压淡一点，轮廓才立得起来。
                var mini = new Grid { Width = cellW - 16, Height = cellH - 12 };
                mini.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(3),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Sub,
                    Opacity = 0.45
                });

                var mark = new Border
                {
                    Width = 10,
                    Height = 8,
                    CornerRadius = new CornerRadius(2),
                    Background = Sub,
                    HorizontalAlignment = col == 0 ? HorizontalAlignment.Left
                                      : col == 1 ? HorizontalAlignment.Center
                                                 : HorizontalAlignment.Right,
                    VerticalAlignment = row == 0 ? VerticalAlignment.Top
                                      : row == 1 ? VerticalAlignment.Center
                                                 : VerticalAlignment.Bottom
                };
                marks[idx] = mark;
                mini.Children.Add(mark);

                var b = new Border
                {
                    Width = cellW,
                    Height = cellH,
                    Margin = new Thickness(0, 0, 4, 4),
                    CornerRadius = new CornerRadius(7),
                    BorderThickness = new Thickness(1),
                    Background = PanelAlt,
                    BorderBrush = Border,
                    Cursor = Cursors.Hand,
                    Child = mini
                };
                b.MouseLeftButtonUp += (s, e) => { set(idx); Refresh(); };
                Grid.SetRow(b, row);
                Grid.SetColumn(b, col);
                cells[idx] = b;
                grid.Children.Add(b);
            }

            Grid.SetColumn(grid, 0);
            Grid.SetColumn(side, 1);
            wrap.Children.Add(grid);
            wrap.Children.Add(side);

            Refresh();
            return wrap;
        }

        /// <summary>把若干控件横向排成一行（各自自适应宽度）。</summary>
        public static FrameworkElement HRow(params UIElement[] items)
        {
            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                MinHeight = RowHeight,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = TagRow
            };
            foreach (var it in items)
            {
                if (it is FrameworkElement fe)
                {
                    fe.Margin = new Thickness(0, 0, 8, 0);
                    fe.VerticalAlignment = VerticalAlignment.Center;
                }
                sp.Children.Add(it);
            }
            return sp;
        }

        /// <summary>
        /// 「按住热键拖窗口」用的虚拟键码选择器。按钮显示当前键名，点击后进入监听，
        /// 下一次按键（左右修饰键自动归一化为通用 VK）即写入；0 = 关闭拖动。
        /// </summary>
        public static FrameworkElement HotkeyPicker(string label, Func<int> get, Action<int> set)
        {
            var btn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(14, 7, 14, 7),
                Background = PanelAlt,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                FontFamily = UiFont,
                FontSize = 12.5,
                Foreground = Text,
                Cursor = Cursors.Hand,
                MinWidth = 156,
            };

            bool listening = false;
            void UpdateName()
            {
                int vk = get();
                btn.Content = listening ? "按下任意键…（Esc 取消）"
                                        : (vk == 0 ? "关闭（不拖动）" : VkName(vk));
            }
            UpdateName();

            btn.Click += (s, e) =>
            {
                if (listening) return;
                listening = true;
                UpdateName();
                btn.Focus();
            };

            btn.PreviewKeyDown += (s, e) =>
            {
                if (!listening) return;
                e.Handled = true;
                if (e.Key == Key.Escape) { listening = false; UpdateName(); return; }
                Key k = (e.Key == Key.System) ? e.SystemKey : e.Key;
                int vk = NormalizeVk(KeyInterop.VirtualKeyFromKey(k));
                listening = false;
                set(vk);
                UpdateName();
            };

            return Row(label, btn);
        }

        /// <summary>
        /// 一小段说明文字。支持 <c>**加粗**</c>。
        /// <para>
        /// 之所以要拼 <c>Inlines</c> 而不是直接设 <c>TextBlock.Text</c>：
        /// <c>Text</c> 不认任何标记，写 <c>**没找到**</c> 会把星号原样画到界面上
        /// （踩过一次，「吸附」页的提示里露出裸星号）。**号不成对时整段按普通文字处理，
        /// 免得把用户/作者真想要的星号吞掉。
        /// </para>
        /// </summary>
        public static FrameworkElement Hint(string text)
        {
            string src = (text ?? "").Replace("\r\n", "\n");

            var t = new TextBlock
            {
                FontSize = 11.5,
                FontFamily = UiFont,
                Foreground = Sub,
                Margin = new Thickness(0, 1, 0, 4),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };

            string[] parts = src.Split(new[] { "**" }, StringSplitOptions.None);
            if (parts.Length % 2 == 0)
            {
                // ** 不成对：原样显示
                t.Inlines.Add(new System.Windows.Documents.Run(src));
                return t;
            }

            // 切出来后下标为奇数的段落在 **…** 里面
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                t.Inlines.Add(new System.Windows.Documents.Run(parts[i])
                {
                    FontWeight = i % 2 == 1 ? FontWeights.SemiBold : FontWeights.Normal,
                });
            }
            return t;
        }

        // ---------------------------------------------------------------
        // 主题资源
        // ---------------------------------------------------------------

        /// <summary>
        /// 按当前调色板生成控件模板资源。WPF 默认模板里 ComboBox / ListBoxItem / CheckBox /
        /// Slider 的配色都是写死的，只改属性改不动，所以这里整套替换。
        ///
        /// 用 <c>%%名字%%</c> 占位再替换、而不是 <c>string.Format</c>：XAML 里到处是
        /// <c>{StaticResource}</c> / <c>{TemplateBinding}</c> 这种花括号，用 Format 会把它们当占位符炸掉。
        /// </summary>
        public static ResourceDictionary Theme()
        {
            var p = _palette;
            string xaml = XamlTemplate
                .Replace("%%TEXT%%", p.Text)
                .Replace("%%SUB%%", p.Sub)
                .Replace("%%CARD%%", p.Card)
                .Replace("%%CARD2%%", p.CardAlt)
                .Replace("%%SIDEBAR%%", p.Sidebar)
                .Replace("%%BORDER%%", p.Border)
                .Replace("%%SEP%%", p.Separator)
                .Replace("%%ACCENT%%", p.Accent)
                .Replace("%%ACCENT_SOFT%%", p.AccentSoft)
                .Replace("%%HOVER%%", p.Hover)
                .Replace("%%DANGER%%", p.Danger)
                .Replace("%%SWOFF%%", p.SwitchOff)
                .Replace("%%BG%%", p.Bg);

            return (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        private const string XamlTemplate = @"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">

  <SolidColorBrush x:Key=""KvText""    Color=""%%TEXT%%""/>
  <SolidColorBrush x:Key=""KvSub""     Color=""%%SUB%%""/>
  <SolidColorBrush x:Key=""KvPanel""   Color=""%%CARD%%""/>
  <SolidColorBrush x:Key=""KvPanel2""  Color=""%%CARD2%%""/>
  <SolidColorBrush x:Key=""KvBorder""  Color=""%%BORDER%%""/>
  <SolidColorBrush x:Key=""KvAccent""  Color=""%%ACCENT%%""/>
  <SolidColorBrush x:Key=""KvAccent2"" Color=""%%ACCENT_SOFT%%""/>

  <!-- 侧边栏选中项：iOS 侧边栏那种圆角药丸 -->
  <Style TargetType=""ListBoxItem"">
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""FontFamily"" Value=""Microsoft YaHei UI, Segoe UI""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""Padding"" Value=""11,7""/>
    <Setter Property=""Margin"" Value=""3,1,3,1""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ListBoxItem"">
          <Border x:Name=""B"" Background=""Transparent"" CornerRadius=""8"" Padding=""{TemplateBinding Padding}"">
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""B"" Property=""Background"" Value=""%%HOVER%%""/>
            </Trigger>
            <Trigger Property=""IsSelected"" Value=""True"">
              <Setter TargetName=""B"" Property=""Background"" Value=""%%ACCENT_SOFT%%""/>
              <Setter Property=""Foreground"" Value=""%%ACCENT%%""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- 下拉菜单（Kit.Combo / Kit.EditableCombo 弹的就是它）。
       之前这里只有属性、没有 ControlTemplate —— WPF 默认模板自带系统菜单那套
       白底 + 硬阴影 + 直角，跟旁边的 iOS 面板完全不是一回事（跟托盘菜单
       「纯白卡片」同一个病根）。所以整套模板换掉。 -->
  <Style TargetType=""ContextMenu"">
    <Setter Property=""Background"" Value=""%%CARD%%""/>
    <Setter Property=""BorderBrush"" Value=""%%BORDER%%""/>
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""Padding"" Value=""6""/>
    <Setter Property=""FontFamily"" Value=""Microsoft YaHei UI, Segoe UI""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ContextMenu"">
          <!-- Popup 里必须自带一层带圆角的 Border：ContextMenu 的内容不会被
               客户端圆角裁剪，所以圆角要画在内容下面这一层上。 -->
          <Border Background=""%%CARD%%"" BorderBrush=""%%BORDER%%"" BorderThickness=""1""
                  CornerRadius=""12"" Padding=""{TemplateBinding Padding}"">
            <Border.Effect>
              <DropShadowEffect BlurRadius=""18"" ShadowDepth=""4"" Opacity=""0.28"" Color=""Black""/>
            </Border.Effect>
            <ScrollViewer VerticalScrollBarVisibility=""Auto""
                          HorizontalScrollBarVisibility=""Disabled""
                          Focusable=""False"">
              <StackPanel IsItemsHost=""True"" KeyboardNavigation.DirectionalNavigation=""Cycle""/>
            </ScrollViewer>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- 菜单项：整行圆角淡底 + 左侧固定宽度的对勾列。
       对勾用真正的 IsChecked 画，**别再往 Header 文本里拼 ✔ 和一串空格** ——
       那样选中项与未选中项靠空格数对齐，字体一换行就散。
       对勾本身是Path 几何而不是字符：U+2713 在 Segoe UI Symbol 里并不一定存在，
       缺字时会退化成一个蓝色楔形（离屏渲染抓出来就是那个）。
       （注释里也不能出现成对的英文双引号：整个模板是一个 C# @ 字符串常量。） -->
  <Style TargetType=""MenuItem"">
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""Padding"" Value=""10,7""/>
    <Setter Property=""FontFamily"" Value=""Microsoft YaHei UI, Segoe UI""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""MenuItem"">
          <Border x:Name=""B"" Background=""Transparent"" CornerRadius=""8"" Padding=""{TemplateBinding Padding}"">
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""18""/>
                <ColumnDefinition Width=""*""/>
              </Grid.ColumnDefinitions>
              <!-- 对勾列：勾上时在中间画一个强调色的 ✓ -->
              <Path x:Name=""Check"" Grid.Column=""0"" Data=""M 0,3.5 L 2.6,6.2 L 7.5,0""
                    Stroke=""%%ACCENT%%"" StrokeThickness=""1.9""
                    StrokeStartLineCap=""Round"" StrokeEndLineCap=""Round"" StrokeLineJoin=""Round""
                    Width=""8"" Height=""7"" Stretch=""None""
                    HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                    Visibility=""Collapsed""/>
              <ContentPresenter Grid.Column=""1"" ContentSource=""Header"" VerticalAlignment=""Center""/>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsHighlighted"" Value=""True"">
              <Setter TargetName=""B"" Property=""Background"" Value=""%%HOVER%%""/>
            </Trigger>
            <Trigger Property=""IsChecked"" Value=""True"">
              <Setter TargetName=""Check"" Property=""Visibility"" Value=""Visible""/>
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
              <Setter Property=""Foreground"" Value=""%%SUB%%""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""Separator"">
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Separator"">
          <Border Margin=""10,5,10,5"" Height=""1"" Background=""%%SEP%%""/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- 开关：iOS 的 UISwitch（标签在左、拨杆在右） -->
  <Style TargetType=""CheckBox"">
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""FontFamily"" Value=""Microsoft YaHei UI, Segoe UI""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""CheckBox"">
          <Grid Background=""Transparent"" MinHeight=""38"">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width=""*""/>
              <ColumnDefinition Width=""Auto""/>
            </Grid.ColumnDefinitions>
            <ContentPresenter Grid.Column=""0"" VerticalAlignment=""Center"" RecognizesAccessKey=""True"" Margin=""0,0,10,0""/>
            <Border x:Name=""Track"" Grid.Column=""1"" Width=""46"" Height=""28"" CornerRadius=""14""
                    Background=""%%SWOFF%%"" VerticalAlignment=""Center"">
              <!-- 圆钮靠 animating Margin.Left 走位：Track 46 - Knob 24 - 左右留白 2*2 = 18 的行程。
                   千万别改成 HorizontalAlignment=Right 或者只动右边距 ——
                   左对齐时右边距完全不参与定位，开关看起来会「两边都在左边」。 -->
              <Ellipse x:Name=""Knob"" Width=""24"" Height=""24"" Fill=""White""
                       HorizontalAlignment=""Left"" Margin=""2,0,20,0"">
                <Ellipse.Effect>
                  <DropShadowEffect BlurRadius=""4"" ShadowDepth=""1"" Opacity=""0.3"" Color=""Black""/>
                </Ellipse.Effect>
              </Ellipse>
            </Border>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsChecked"" Value=""True"">
              <Setter TargetName=""Track"" Property=""Background"" Value=""%%ACCENT%%""/>
              <Trigger.EnterActions>
                <BeginStoryboard>
                  <Storyboard>
                    <ThicknessAnimation Storyboard.TargetName=""Knob"" Storyboard.TargetProperty=""Margin""
                                        To=""20,0,2,0"" Duration=""0:0:0.16"">
                      <ThicknessAnimation.EasingFunction>
                        <CubicEase EasingMode=""EaseOut""/>
                      </ThicknessAnimation.EasingFunction>
                    </ThicknessAnimation>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.EnterActions>
              <Trigger.ExitActions>
                <BeginStoryboard>
                  <Storyboard>
                    <ThicknessAnimation Storyboard.TargetName=""Knob"" Storyboard.TargetProperty=""Margin""
                                        To=""2,0,20,0"" Duration=""0:0:0.16"">
                      <ThicknessAnimation.EasingFunction>
                        <CubicEase EasingMode=""EaseOut""/>
                      </ThicknessAnimation.EasingFunction>
                    </ThicknessAnimation>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.ExitActions>
            </Trigger>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Track"" Property=""Opacity"" Value=""0.85""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- 按钮：圆角胶囊 + 按下压暗 -->
  <Style TargetType=""Button"">
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""FontFamily"" Value=""Microsoft YaHei UI, Segoe UI""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""B"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""9"" Padding=""{TemplateBinding Padding}"">
            <ContentPresenter HorizontalAlignment=""{TemplateBinding HorizontalContentAlignment}"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""B"" Property=""Opacity"" Value=""0.86""/></Trigger>
            <Trigger Property=""IsPressed"" Value=""True""><Setter TargetName=""B"" Property=""Opacity"" Value=""0.62""/></Trigger>
            <Trigger Property=""IsEnabled"" Value=""False""><Setter TargetName=""B"" Property=""Opacity"" Value=""0.4""/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- 滑块：细轨 + 圆形拇指 -->
  <Style x:Key=""KvSliderFill"" TargetType=""RepeatButton"">
    <Setter Property=""Focusable"" Value=""False""/>
    <Setter Property=""IsTabStop"" Value=""False""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""RepeatButton"">
          <Border Height=""4"" CornerRadius=""2"" Background=""%%ACCENT%%"" VerticalAlignment=""Center""/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key=""KvSliderEmpty"" TargetType=""RepeatButton"">
    <Setter Property=""Focusable"" Value=""False""/>
    <Setter Property=""IsTabStop"" Value=""False""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""RepeatButton"">
          <Border Height=""4"" CornerRadius=""2"" Background=""Transparent"" VerticalAlignment=""Center""/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key=""KvSliderThumb"" TargetType=""Thumb"">
    <Setter Property=""Width"" Value=""22""/>
    <Setter Property=""Height"" Value=""22""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Thumb"">
          <Ellipse Fill=""#FFFFFF"" Width=""22"" Height=""22"">
            <Ellipse.Effect>
              <DropShadowEffect BlurRadius=""5"" ShadowDepth=""1"" Opacity=""0.32"" Color=""Black""/>
            </Ellipse.Effect>
          </Ellipse>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""Slider"">
    <Setter Property=""Height"" Value=""28""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Slider"">
          <Grid VerticalAlignment=""Center"">
            <Border Height=""4"" CornerRadius=""2"" Background=""%%SWOFF%%"" VerticalAlignment=""Center""/>
            <Track x:Name=""PART_Track"">
              <Track.DecreaseRepeatButton>
                <RepeatButton Command=""Slider.DecreaseLarge"" Style=""{StaticResource KvSliderFill}""/>
              </Track.DecreaseRepeatButton>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command=""Slider.IncreaseLarge"" Style=""{StaticResource KvSliderEmpty}""/>
              </Track.IncreaseRepeatButton>
              <Track.Thumb>
                <Thumb Style=""{StaticResource KvSliderThumb}""/>
              </Track.Thumb>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""TextBox"">
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""CaretBrush"" Value=""{StaticResource KvText}""/>
    <Setter Property=""SelectionBrush"" Value=""%%ACCENT%%""/>
    <Setter Property=""FontFamily"" Value=""Microsoft YaHei UI, Segoe UI""/>
  </Style>

  <Style TargetType=""ScrollBar"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Width"" Value=""10""/>
  </Style>

  <Style TargetType=""ToolTip"">
    <Setter Property=""Background"" Value=""%%CARD%%""/>
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""BorderBrush"" Value=""%%BORDER%%""/>
  </Style>

</ResourceDictionary>";

        // ---------------------------------------------------------------
        // 控件
        // ---------------------------------------------------------------

        public static CheckBox Check(string label, Func<bool> get, Action<bool> set, bool inline = true)
        {
            var cb = new CheckBox
            {
                Content = label,
                IsChecked = get(),
                FontFamily = UiFont,
                FontSize = 13,
                Foreground = Text,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = Cursors.Hand,
                Tag = TagRow
            };
            cb.Checked += (s, e) => { set(true); Pulse(cb, 1.018, 130, 0.0, 0.5); };
            cb.Unchecked += (s, e) => { set(false); Pulse(cb, 1.018, 130, 0.0, 0.5); };
            return cb;
        }

        public static FrameworkElement Slider(string label, double min, double max,
                                              Func<double> get, Action<double> set,
                                              string fmt = "0.##", double labelWidth = 168)
        {
            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = Math.Max(min, Math.Min(max, get())),
                Width = 240,
                VerticalAlignment = VerticalAlignment.Center,
                IsSnapToTickEnabled = false
            };

            var value = Text2(get().ToString(fmt), 12, Text, false, TextAlignment.Right);
            value.Width = 62;
            value.Margin = new Thickness(8, 0, 0, 0);

            slider.ValueChanged += (s, e) =>
            {
                set(slider.Value);
                value.Text = slider.Value.ToString(fmt);
            };

            return Row(label, HRow(slider, value), labelWidth);
        }

        /// <summary>
        /// 精确数值行：可手填的输入框 + 「−/+」步进按钮。
        /// 滑块在 0..1920 这种量程上每像素要跳 8 个单位，做坐标编辑太粗，所以单独做这个。
        /// </summary>
        public static FrameworkElement NumberRow(string label, Func<double> get, Action<double> set,
                                                 double step = 1, double min = double.MinValue, double max = double.MaxValue,
                                                 string fmt = "0", double labelWidth = 168, double boxWidth = 76)
        {
            double Clamp(double v) => Math.Max(min, Math.Min(max, v));

            var box = new TextBox
            {
                Width = boxWidth,
                Text = get().ToString(fmt, CultureInfo.InvariantCulture),
                FontFamily = UiFont,
                FontSize = 12.5,
                Padding = new Thickness(5, 3, 5, 3),
                Background = PanelAlt,
                Foreground = Text,
                BorderBrush = Border,
                CaretBrush = Text,
                TextAlignment = TextAlignment.Right
            };

            void Commit()
            {
                if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                {
                    v = Clamp(v);
                    set(v);
                    box.Text = v.ToString(fmt, CultureInfo.InvariantCulture);
                }
                else
                {
                    box.Text = get().ToString(fmt, CultureInfo.InvariantCulture);
                }
            }

            box.LostFocus += (s, e) => Commit();
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { Commit(); Keyboard.ClearFocus(); }
            };

            Button Nudge(string cap, double d)
            {
                var b = Button(cap, () =>
                {
                    set(Clamp(get() + d));
                    box.Text = get().ToString(fmt, CultureInfo.InvariantCulture);
                });
                b.MinWidth = 28;
                b.Width = 28;
                b.Padding = new Thickness(0);
                b.Margin = new Thickness(4, 0, 0, 0);
                return b;
            }

            return Row(label, HRow(box, Nudge("−", -step), Nudge("+", step)), labelWidth);
        }

        public static FrameworkElement TextBoxRow(string label, Func<string> get, Action<string> set,
                                                  double width = 240, double labelWidth = 168)
        {
            var box = new TextBox
            {
                Text = get() ?? "",
                Width = width,
                FontFamily = UiFont,
                FontSize = 12.5,
                Padding = new Thickness(5, 3, 5, 3),
                Background = PanelAlt,
                Foreground = Text,
                BorderBrush = Border,
                CaretBrush = Text
            };
            box.LostFocus += (s, e) => set(box.Text);
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { set(box.Text); Keyboard.ClearFocus(); }
            };
            return Row(label, box, labelWidth);
        }

        /// <summary>
        /// 下拉选择。刻意不用 ComboBox —— 它的默认模板在深色主题下会渲染成白底白字，
        /// 而自定义模板又会破坏可编辑模式。这里用「按钮 + 上下文菜单」自己拼，完全可控。
        /// </summary>
        public static FrameworkElement Combo(string label, IList<string> items, Func<int> get, Action<int> set,
                                             double width = 240, double labelWidth = 168,
                                             double menuMaxHeight = 320)
        {
            var text = Text2("", 12.5, Text);
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            var arrow = Text2("▾", 11, Sub);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(text, 0);
            grid.Children.Add(text);
            arrow.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(arrow, 1);
            grid.Children.Add(arrow);

            var btn = new Button
            {
                Width = width,
                Content = grid,
                Padding = new Thickness(9, 4, 9, 4),
                Background = PanelAlt,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            void Refresh()
            {
                int i = get();
                text.Text = (items != null && i >= 0 && i < items.Count) ? items[i] : "";
            }
            Refresh();

            void OpenMenu()
            {
                if (items == null || items.Count == 0) return;

                var menu = BuildMenu(btn, menuMaxHeight);
                int cur = get();
                for (int i = 0; i < items.Count; i++)
                {
                    int idx = i;
                    var mi = new MenuItem
                    {
                        Header = items[idx],
                        // 用真正的 IsChecked，样式里那列对勾会自动亮起来。
                        // 早先是在 Header 前面拼 ✔ 和一串空格来假对齐，
                        // 字体一换行就散，而且和 MenuItem 的 Padding 叠在一起会歪。
                        IsCheckable = true,
                        IsChecked = idx == cur,
                        CommandParameter = idx
                    };
                    mi.Click += (s2, e2) => { set(idx); Refresh(); };
                    menu.Items.Add(mi);
                }
                menu.IsOpen = true;
#if DEBUG
                _lastDebugMenu = menu;
#endif
            }

            btn.Click += (s, e) =>
            {
                OpenMenu();
                e.Handled = true;
            };

#if DEBUG
            _debugCombos.Add(new WeakReference(btn));
#endif

            return Row(label, btn, labelWidth);
        }

#if DEBUG
        private static readonly List<WeakReference> _debugCombos = new();
        private static ContextMenu _lastDebugMenu;

        /// <summary>调试：目前一共造了多少个下拉（Combo + EditableCombo）。</summary>
        public static int DebugComboCount => _debugCombos.Count;

        /// <summary>
        /// 调试：弹开第 <paramref name="index"/> 个下拉（按创建顺序，0 起）。
        /// 走的是真实的 Button.Click 事件，不是另写一套弹窗逻辑 ——
        /// 这样截出来的图就是用户真会看到的那一份。
        /// <para>
        /// 合成事件没有鼠标捕获，ContextMenu 会开一下就被焦点变化关掉，
        /// 所以之后用一个 250ms 的定时器反复 IsOpen=true 顶住，
        /// 保证截图那一刻它是张开的。
        /// </para>
        /// </summary>
        public static bool DebugOpenCombo(int index)
        {
            int i = 0;
            foreach (var wr in _debugCombos)
            {
                if (wr.Target is Button b && i++ == index)
                {
                    b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                    var keep = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(250) };
                    int ticks = 0;
                    keep.Tick += (s, e) =>
                    {
                        if (_lastDebugMenu != null) _lastDebugMenu.IsOpen = true;
                        if (++ticks > 80) keep.Stop();      // 顶 20 秒够了
                    };
                    keep.Start();
                    return true;
                }
            }
            return false;
        }
#endif

        /// <summary>
        /// 下拉菜单本体。样式来自 <see cref="Theme"/> 里的 ContextMenu / MenuItem 模板，
        /// 这里只负责把它挂到按钮下面并限高。
        /// </summary>
        private static ContextMenu BuildMenu(FrameworkElement anchor, double menuMaxHeight)
        {
            var menu = new ContextMenu
            {
                MaxHeight = menuMaxHeight,
                MinWidth = Math.Max(anchor.ActualWidth, 120),
                PlacementTarget = anchor,
                Placement = PlacementMode.Bottom,
                VerticalOffset = 4
            };
            return menu;
        }

        /// <summary>可编辑文本 + 下拉候选 —— 等价的「可输入 ComboBox」，但外观完全可控。</summary>
        public static FrameworkElement EditableCombo(string label, IList<string> items, string current,
                                                     Action<string> set,
                                                     double width = 240, double labelWidth = 168,
                                                     double menuMaxHeight = 320)
        {
            var box = new TextBox
            {
                Width = width - 30,
                Text = current ?? "",
                FontFamily = UiFont,
                FontSize = 12.5,
                Padding = new Thickness(6, 3, 6, 3),
                Background = PanelAlt,
                Foreground = Text,
                BorderBrush = Border,
                CaretBrush = Text
            };
            box.LostFocus += (s, e) => set(box.Text);
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) { set(box.Text); Keyboard.ClearFocus(); } };

            var drop = new Button
            {
                Width = 26,
                Content = Text2("▾", 11, Sub, align: TextAlignment.Center),
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(0),
                Background = PanelAlt,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            drop.Click += (s, e) =>
            {
                if (items == null || items.Count == 0) return;

                var menu = BuildMenu(drop, menuMaxHeight);
                foreach (var it in items)
                {
                    string v = it;
                    var mi = new MenuItem { Header = v, IsCheckable = true, IsChecked = v == box.Text };
                    mi.Click += (s2, e2) => { box.Text = v; set(v); };
                    menu.Items.Add(mi);
                }
                menu.IsOpen = true;
#if DEBUG
                _lastDebugMenu = menu;
                _debugCombos.Add(new WeakReference(drop));
#endif
            };

            return Row(label, HRow(box, drop), labelWidth);
        }

        /// <summary>颜色按钮：显示一块色板 + HEX，点击弹出取色器。</summary>
        public static FrameworkElement ColorRow(string label, Func<KvColor> get, Action<KvColor> set,
                                                Action<Action> pushUndo = null, double labelWidth = 168)
        {
            var swatch = new Border
            {
                Width = 34,
                Height = 20,
                CornerRadius = new CornerRadius(4),
                BorderBrush = Border,
                BorderThickness = new Thickness(1)
            };

            var hex = Text2("", 11.5, Sub);
            hex.Width = 88;
            hex.Margin = new Thickness(8, 0, 0, 0);

            void Refresh()
            {
                var c = get();
                swatch.Background = c.ToBrush();
                hex.Text = c.ToHex();
            }
            Refresh();

            var btn = new Button
            {
                Content = HRow(swatch, hex),
                Padding = new Thickness(6, 3, 6, 3),
                Background = PanelAlt,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            btn.Click += (s, e) =>
            {
                var original = get();
                var dlg = new ColorPickerWindow(original, label)
                {
                    Owner = Window.GetWindow(btn)
                };
                if (dlg.ShowDialog() == true)
                {
                    pushUndo?.Invoke(() => set(original));
                    set(dlg.Result);
                    Refresh();
                }
            };

            // 外部（例如「重置」）改动后重新读值
            btn.Tag = new Action(Refresh);

            return Row(label, btn, labelWidth);
        }

        /// <summary>
        /// 按键捕获按钮 —— 点一下变成「请按键…」，随后捕获**一次**按键。
        /// 捕获支持修饰键（Shift / Ctrl / Alt）与鼠标左右中侧键，
        /// 实现见 <see cref="KeyCapture"/>。
        /// </summary>
        public static FrameworkElement KeyRow(string label, Func<int> get, Action<int> set,
                                              double labelWidth = 168)
        {
            var btn = new Button
            {
                Padding = new Thickness(8, 4, 8, 4),
                MinWidth = 150,
                Background = PanelAlt,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                FontFamily = UiFont,
                FontSize = 12.5,
                Foreground = Text,
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Focusable = true
            };

            // 捕获逻辑（轮询 + 忽略起始已按下的键）全在 KeyCapture 里，这里只管文案
            KeyCapture capture = null;

            void Show()
            {
                btn.Content = (capture != null && capture.IsCapturing)
                    ? "请按键…（Esc 取消）"
                    : KeyLabel(get());
            }

            capture = new KeyCapture(set, Show);
            capture.Attach(btn);

            Show();
            return Row(label, btn, labelWidth);
        }

        /// <summary>
        /// 按键捕获器 —— 「点一下按钮 → 按下任意键（含鼠标左/右/中/侧键）→ 捕获」。
        ///
        /// 一律走 <c>GetAsyncKeyState</c> 轮询，不用 WPF 的键盘事件，原因有二：
        /// <list type="bullet">
        /// <item>修饰键在 WPF 里要么被当成组合前缀、要么得等「真正的键」才落地，
        ///       单按 Shift / Ctrl / Alt 永远绑不上（<c>Key.System</c> 也是这个坑）；</item>
        /// <item>鼠标键压根不产生键盘事件，<c>PreviewKeyDown</c> 一次都不会来。</item>
        /// </list>
        /// 轮询则键盘鼠标一视同仁 —— 左键 VK <c>0x01</c> → <c>Mouse0</c>。
        ///
        /// 开始捕获那一刻，点按钮的那一下左键往往还按着，所以会先把当时已按下的键记账，
        /// 等它们抬起来之后才接受，否则一进捕获就立刻被绑成 <c>Mouse0</c>。
        /// </summary>
        internal sealed class KeyCapture
        {
            private readonly Action<int> _onCaptured;
            private readonly Action _onStateChanged;
            private readonly HashSet<int> _held = new HashSet<int>();
            private System.Windows.Threading.DispatcherTimer _timer;

            public KeyCapture(Action<int> onCaptured, Action onStateChanged)
            {
                _onCaptured = onCaptured;
                _onStateChanged = onStateChanged;
            }

            /// <summary>是否正在等待用户按键。</summary>
            public bool IsCapturing { get; private set; }

            /// <summary>开始捕获；重复调用无副作用。</summary>
            public void Start()
            {
                if (IsCapturing) return;
                IsCapturing = true;

                // 把此刻已经按着的键（多半就是点按钮的那一下左键）先记账
                Snapshot(Native.Win32.IsKeyDown, _held);

                _onStateChanged?.Invoke();

                _timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Input)
                {
                    Interval = TimeSpan.FromMilliseconds(16)
                };
                _timer.Tick += (s, e) => Poll();
                _timer.Start();
            }

            /// <summary>取消捕获（不写值）。</summary>
            public void Stop()
            {
                if (!IsCapturing) return;
                Reset();
                Diag.Log("key capture cancelled");
                _onStateChanged?.Invoke();
            }

            /// <summary>把捕获挂到按钮上：按下开始，捕获中再按下则把该鼠标键当成输入。</summary>
            public void Attach(Button btn)
            {
                if (btn == null) return;

                // 用 PreviewMouseDown 而不是 Click：Click 在**抬起**时才发，
                // 而「绑鼠标左键」恰恰要用第二次按下的那一下当输入。捕获中再按下时，
                // PreviewMouseDown 直接给出 ChangedButton，比轮询更精确、也没有竞态。
                btn.PreviewMouseDown += (s, e) =>
                {
                    e.Handled = true;

                    if (!IsCapturing)
                    {
                        Start();
                        btn.Focus();
                        Keyboard.Focus(btn);
                        return;
                    }

                    int m = MouseButtonToUnity(e.ChangedButton);
                    if (m != 0) Capture(m);
                };

                // 捕获期间吞掉所有按键，免得 Space / Enter 触发别的控件、方向键滚动列表。
                // 窗口没焦点时这里不触发，Poll() 照样能捕获 —— 想绑「游戏里那个键」时
                // 把焦点让给游戏反而更方便。
                btn.PreviewKeyDown += (s, e) => { if (IsCapturing) e.Handled = true; };

                // 控件被销毁（切标签页会触发重建）时停掉定时器，
                // 否则会对着已经不在树上的节点继续 poll 并写值
                btn.Unloaded += (s, e) => Stop();
            }

            private void Capture(int unityCode)
            {
                if (!IsCapturing) return;
                Reset();
                // 日志便于排查「某个键绑不上」—— 捕获成功却没写进去，就是 set() 那条链的问题
                Diag.Log(string.Format("key captured: {0} (unity={1}, vk=0x{2:X2})",
                    KeyCodeMap.NameOf(unityCode), unityCode,
                    KeyCodeMap.ToVirtualKey(unityCode)));
                _onCaptured?.Invoke(unityCode);   // 先写值
                _onStateChanged?.Invoke();        // 再刷新文案，否则显示的还是旧键
            }

            private void Reset()
            {
                IsCapturing = false;
                if (_timer != null) { _timer.Stop(); _timer = null; }
                _held.Clear();
            }

            private void Poll()
            {
                // Esc 取消。放在轮询里而不是 PreviewKeyDown 里，这样窗口没焦点也能退出
                if (Native.Win32.IsKeyDown(Native.Win32.VK_ESCAPE)) { Stop(); return; }

                int unity = PickKey(Native.Win32.IsKeyDown, _held);
                if (unity != 0) Capture(unity);
            }

            // ---- 扫描逻辑（纯函数，便于 --selftest 无界面自检）----

            /// <summary>
            /// 捕获开始时，把当前已按下的键全部记账 —— 多半是点按钮的那一下左键，
            /// 不记的话一进捕获就立刻被绑成 <c>Mouse0</c>。
            /// </summary>
            internal static void Snapshot(Func<int, bool> isDown, HashSet<int> held)
            {
                held.Clear();
                for (int vk = 0x01; vk <= 0xFE; vk++)
                    if (isDown(vk)) held.Add(vk);
            }

            /// <summary>
            /// 扫描一遍按键状态，返回**应当捕获的 Unity KeyCode**；没有新按下的键则返回 0。
            /// <paramref name="held"/> 会被就地更新：已抬起的键从中移除，认不出的键加进去。
            ///
            /// 两个关键约定：
            /// <list type="bullet">
            /// <item>泛用的 <c>VK_SHIFT(0x10)</c> / <c>VK_CONTROL(0x11)</c> / <c>VK_MENU(0x12)</c>
            ///       映射不到 Unity KeyCode（原版只认区分左右的 <c>0xA0..0xA5</c>），跳过继续扫，
            ///       否则按 Shift 会绑出个空值；</item>
            /// <item>扫描顺序即优先级：鼠标键 <c>0x01..0x06</c> 在最前，修饰键 <c>0xA0..</c> 靠后，
            ///       但都在 <c>0x10</c> 这些「泛用键」之后被显式跳过，实际互不干扰。</item>
            /// </list>
            /// </summary>
            internal static int PickKey(Func<int, bool> isDown, HashSet<int> held)
            {
                for (int vk = 0x01; vk <= 0xFE; vk++)
                {
                    if (vk == Native.Win32.VK_ESCAPE) continue;

                    if (!isDown(vk)) { held.Remove(vk); continue; }
                    if (held.Contains(vk)) continue;   // 还没抬起来，忽略

                    int unity = KeyCodeMap.FromVirtualKey(vk);
                    if (unity == 0) { held.Add(vk); continue; }

                    return unity;
                }
                return 0;
            }
        }

        /// <summary>WPF <see cref="MouseButton"/> → Unity Mouse KeyCode（Mouse0=左键）。</summary>
        private static int MouseButtonToUnity(MouseButton b)
        {
            switch (b)
            {
                case MouseButton.Left: return KeyCodeMap.UnityMouse0;
                case MouseButton.Right: return KeyCodeMap.UnityMouse0 + 1;
                case MouseButton.Middle: return KeyCodeMap.UnityMouse0 + 2;
                case MouseButton.XButton1: return KeyCodeMap.UnityMouse0 + 3;
                case MouseButton.XButton2: return KeyCodeMap.UnityMouse0 + 4;
                default: return 0;
            }
        }

        public static Button Button(string text, Action act, bool accent = false, double width = double.NaN)
        {
            var b = new Button
            {
                Content = text,
                Padding = new Thickness(14, 7, 14, 7),
                Background = accent ? Accent : PanelAlt,
                BorderBrush = accent ? Accent : Border,
                BorderThickness = new Thickness(accent ? 0 : 1),
                FontFamily = UiFont,
                FontSize = 12.5,
                // 强调按钮在 iOS 里是蓝底白字；普通按钮用主文字色
                Foreground = accent ? Brushes.White : Text,
                Cursor = Cursors.Hand,
                MinWidth = 78,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            b.RenderTransform = new ScaleTransform(1, 1);
            if (!double.IsNaN(width)) b.Width = width;

            // 悬停 / 按下时轻微缩放，给界面一点「动感」
            void ScaleTo(double s)
            {
                try
                {
                    var st = b.RenderTransform as ScaleTransform;
                    if (st == null) return;
                    var anim = new System.Windows.Media.Animation.DoubleAnimation(
                        st.ScaleX, s, System.Windows.Duration.Automatic);
                    anim.Duration = new System.Windows.Duration(TimeSpan.FromMilliseconds(110));
                    anim.EasingFunction = new System.Windows.Media.Animation.CubicEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                }
                catch { }
            }
            b.MouseEnter += (s, e) => ScaleTo(1.04);
            b.MouseLeave += (s, e) => ScaleTo(1.0);
            b.PreviewMouseDown += (s, e) => ScaleTo(0.96);
            b.PreviewMouseUp += (s, e) => ScaleTo(1.04);

            b.Click += (s, e) => act();
            return b;
        }

        /// <summary>危险操作按钮（删除档案等）—— iOS 的红底白字。</summary>
        public static Button DangerButton(string text, Action act)
        {
            var b = Button(text, act);
            b.Background = Danger;
            b.BorderThickness = new Thickness(0);
            b.Foreground = Brushes.White;
            return b;
        }

        /// <summary>
        /// 给控件来一下「弹一下」的缩放反馈：快速放大到 <paramref name="peak"/> 再弹回 1。
        /// 用于勾选 / 切换 / 点按这类需要即时触感的地方。默认从中心缩放。
        /// </summary>
        public static void Pulse(FrameworkElement el, double peak = 1.06, int ms = 140,
                                 double originX = 0.5, double originY = 0.5)
        {
            if (el == null) return;
            try
            {
                var st = el.RenderTransform as ScaleTransform;
                if (st == null)
                {
                    st = new ScaleTransform(1, 1);
                    el.RenderTransform = st;
                    el.RenderTransformOrigin = new Point(originX, originY);
                }

                int up = Math.Max(30, (int)(ms * 0.35));
                var a = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
                a.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
                    peak, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(up))));
                a.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
                    1.0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));

                st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            }
            catch { }
        }

        // ---------------------------------------------------------------
        // 工具
        // ---------------------------------------------------------------

        public static string KeyLabel(int unityCode)
        {
            if (unityCode == 0) return "（未绑定）";
            string n = KeyCodeMap.DisplayName(unityCode);
            return string.IsNullOrEmpty(n) ? "K" + unityCode : n;
        }

        /// <summary>把虚拟键码转成可读名称（用于热键选择器显示）。</summary>
        private static string VkName(int vk)
        {
            switch (vk)
            {
                case 0x01: return "鼠标左键";
                case 0x02: return "鼠标右键";
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x10: return "Shift";
                case 0x11: return "Ctrl";
                case 0x12: return "Alt";
                case 0x13: return "Pause";
                case 0x14: return "Caps";
                case 0x20: return "空格";
                case 0x1B: return "Esc";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "PgUp";
                case 0x22: return "PgDn";
                case 0x25: return "←";
                case 0x26: return "↑";
                case 0x27: return "→";
                case 0x28: return "↓";
                case 0x2C: return "PrtSc";
                case 0x2D: return "Ins";
                case 0x2E: return "Del";
                case 0x5B: case 0x5C: return "Win";
            }
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            if (vk >= 0x60 && vk <= 0x6F) return "小键盘" + (vk - 0x60);
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
            return "键0x" + vk.ToString("X2");
        }

        /// <summary>左右修饰键归一化为通用 VK（Alt/Ctrl/Shift 的左右两边用同一个值判断）。</summary>
        private static int NormalizeVk(int vk)
        {
            if (vk == 0xA4 || vk == 0xA5) return 0x12;   // LMENU / RMENU
            if (vk == 0xA2 || vk == 0xA3) return 0x11;   // LCONTROL / RCONTROL
            if (vk == 0xA0 || vk == 0xA1) return 0x10;   // LSHIFT / RSHIFT
            return vk;
        }
    }
}
