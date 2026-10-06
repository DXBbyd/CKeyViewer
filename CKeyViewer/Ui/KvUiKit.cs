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
        // ---- 配色 ----
        public static readonly Brush Bg = Freeze("#1C1922");
        public static readonly Brush Panel = Freeze("#252131");
        public static readonly Brush PanelAlt = Freeze("#2E2939");
        public static readonly Brush Sidebar = Freeze("#211D2B");
        public static readonly Brush Border = Freeze("#3B3549");
        public static readonly Brush Text = Freeze("#E9E5F2");
        public static readonly Brush Sub = Freeze("#9B94AB");
        public static readonly Brush Accent = Freeze("#9B4DFF");
        public static readonly Brush AccentDim = Freeze("#3A2B52");

        public const double RowHeight = 30;

        private static Brush Freeze(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        private static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI, Segoe UI");

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

        /// <summary>分组标题（带一条分隔线）。</summary>
        public static FrameworkElement Section(string title)
        {
            var grid = new Grid { Margin = new Thickness(0, 14, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var t = Text2(title, 13, Accent, bold: true);
            Grid.SetColumn(t, 0);
            grid.Children.Add(t);

            var line = new Border
            {
                Height = 1,
                Background = Border,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(line, 1);
            grid.Children.Add(line);

            return grid;
        }

        /// <summary>左侧标签 + 右侧控件的标准行。</summary>
        public static FrameworkElement Row(string label, UIElement ctrl, double labelWidth = 168)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3), MinHeight = RowHeight };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var t = Text2(label, 12.5, Sub);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(t, 0);
            grid.Children.Add(t);

            if (ctrl is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ctrl, 1);
            grid.Children.Add(ctrl);

            return grid;
        }

        /// <summary>把若干控件横向排成一行（各自自适应宽度）。</summary>
        public static FrameworkElement HRow(params UIElement[] items)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var it in items)
            {
                if (it is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 8, 0);
                sp.Children.Add(it);
            }
            return sp;
        }

        public static FrameworkElement Hint(string text)
        {
            var t = Text2(text, 11.5, Sub);
            t.Margin = new Thickness(0, 1, 0, 4);
            t.TextWrapping = TextWrapping.Wrap;
            return t;
        }

        // ---------------------------------------------------------------
        // 主题资源
        // ---------------------------------------------------------------

        /// <summary>
        /// 深色主题资源。WPF 默认模板里 ComboBox / ListBoxItem / CheckBox 的配色都是写死的，
        /// 只改属性改不动，所以这里直接替换模板。
        /// </summary>
        public static ResourceDictionary Theme()
        {
            const string xaml = @"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">

  <SolidColorBrush x:Key=""KvText""    Color=""#E9E5F2""/>
  <SolidColorBrush x:Key=""KvSub""     Color=""#9B94AB""/>
  <SolidColorBrush x:Key=""KvPanel""   Color=""#252131""/>
  <SolidColorBrush x:Key=""KvPanel2""  Color=""#2E2939""/>
  <SolidColorBrush x:Key=""KvBorder""  Color=""#3B3549""/>
  <SolidColorBrush x:Key=""KvAccent""  Color=""#9B4DFF""/>
  <SolidColorBrush x:Key=""KvAccent2"" Color=""#3A2B52""/>

  <!-- 列表项 -->
  <Style TargetType=""ListBoxItem"">
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""Padding"" Value=""10,7""/>
    <Setter Property=""Margin"" Value=""0,1,0,1""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ListBoxItem"">
          <Border x:Name=""B"" Background=""Transparent"" CornerRadius=""4"" Padding=""{TemplateBinding Padding}"">
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""B"" Property=""Background"" Value=""#2E2939""/>
            </Trigger>
            <Trigger Property=""IsSelected"" Value=""True"">
              <Setter TargetName=""B"" Property=""Background"" Value=""#9B4DFF""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- 菜单（下拉用） -->
  <Style TargetType=""ContextMenu"">
    <Setter Property=""Background"" Value=""#252131""/>
    <Setter Property=""BorderBrush"" Value=""#3B3549""/>
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""Padding"" Value=""2""/>
  </Style>

  <Style TargetType=""MenuItem"">
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""Padding"" Value=""10,6""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""MenuItem"">
          <Border x:Name=""B"" Background=""Transparent"" CornerRadius=""3"" Padding=""{TemplateBinding Padding}"">
            <ContentPresenter ContentSource=""Header""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsHighlighted"" Value=""True"">
              <Setter TargetName=""B"" Property=""Background"" Value=""#3A2B52""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- 复选框 -->
  <Style TargetType=""CheckBox"">
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""CheckBox"">
          <StackPanel Orientation=""Horizontal"" Background=""Transparent"">
            <Border x:Name=""Box"" Width=""15"" Height=""15"" CornerRadius=""3""
                    Background=""#2E2939"" BorderBrush=""#3B3549"" BorderThickness=""1""
                    VerticalAlignment=""Center"">
              <Path x:Name=""Tick"" Data=""M 2,6 L 5.5,9.5 L 12,2"" Stroke=""White"" StrokeThickness=""2""
                    Visibility=""Collapsed"" StrokeStartLineCap=""Round"" StrokeEndLineCap=""Round""/>
            </Border>
            <ContentPresenter Margin=""8,0,0,0"" VerticalAlignment=""Center"" RecognizesAccessKey=""True""/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsChecked"" Value=""True"">
              <Setter TargetName=""Box"" Property=""Background"" Value=""#9B4DFF""/>
              <Setter TargetName=""Box"" Property=""BorderBrush"" Value=""#9B4DFF""/>
              <Setter TargetName=""Tick"" Property=""Visibility"" Value=""Visible""/>
            </Trigger>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Box"" Property=""BorderBrush"" Value=""#9B4DFF""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- 按钮 -->
  <Style TargetType=""Button"">
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""B"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""4"" Padding=""{TemplateBinding Padding}"">
            <ContentPresenter HorizontalAlignment=""{TemplateBinding HorizontalContentAlignment}"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""B"" Property=""Opacity"" Value=""0.85""/></Trigger>
            <Trigger Property=""IsPressed"" Value=""True""><Setter TargetName=""B"" Property=""Opacity"" Value=""0.7""/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- 滑块 -->
  <Style TargetType=""Slider"">
    <Setter Property=""Height"" Value=""22""/>
  </Style>

  <Style TargetType=""TextBox"">
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""CaretBrush"" Value=""{StaticResource KvText}""/>
    <Setter Property=""SelectionBrush"" Value=""#9B4DFF""/>
  </Style>

  <Style TargetType=""ScrollBar"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Width"" Value=""10""/>
  </Style>

  <Style TargetType=""ToolTip"">
    <Setter Property=""Background"" Value=""#252131""/>
    <Setter Property=""Foreground"" Value=""{StaticResource KvText}""/>
    <Setter Property=""BorderBrush"" Value=""#3B3549""/>
  </Style>

</ResourceDictionary>";

            return (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml);
        }

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
                FontSize = 12.5,
                Foreground = Text,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand
            };
            cb.Checked += (s, e) => set(true);
            cb.Unchecked += (s, e) => set(false);
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

            btn.Click += (s, e) =>
            {
                if (items == null || items.Count == 0) return;

                var menu = new ContextMenu { MaxHeight = menuMaxHeight, PlacementTarget = btn, Placement = PlacementMode.Bottom };
                int cur = get();
                for (int i = 0; i < items.Count; i++)
                {
                    int idx = i;
                    var mi = new MenuItem { Header = (idx == cur ? "✔  " : "     ") + items[idx] };
                    mi.Click += (s2, e2) => { set(idx); Refresh(); };
                    menu.Items.Add(mi);
                }
                menu.IsOpen = true;
                e.Handled = true;
            };

            return Row(label, btn, labelWidth);
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
                var menu = new ContextMenu { MaxHeight = menuMaxHeight, PlacementTarget = drop, Placement = PlacementMode.Bottom };
                foreach (var it in items)
                {
                    string v = it;
                    var mi = new MenuItem { Header = v };
                    mi.Click += (s2, e2) => { box.Text = v; set(v); };
                    menu.Items.Add(mi);
                }
                menu.IsOpen = true;
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
                Padding = new Thickness(12, 5, 12, 5),
                Background = accent ? Accent : PanelAlt,
                BorderBrush = accent ? Accent : Border,
                BorderThickness = new Thickness(1),
                FontFamily = UiFont,
                FontSize = 12.5,
                Foreground = Text,
                Cursor = Cursors.Hand,
                MinWidth = 78
            };
            if (!double.IsNaN(width)) b.Width = width;
            b.Click += (s, e) => act();
            return b;
        }

        /// <summary>危险操作按钮（删除档案等）。</summary>
        public static Button DangerButton(string text, Action act)
        {
            var b = Button(text, act);
            b.Background = Freeze("#4A2230");
            b.BorderBrush = Freeze("#7A3348");
            return b;
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
    }
}
