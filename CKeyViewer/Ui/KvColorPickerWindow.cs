using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CKeyViewer.Core;

namespace CKeyViewer.Ui
{
    /// <summary>
    /// 轻量 RGBA 取色器。原版用的是 Unity 的 Color 选择器（含 Alpha），
    /// WinForms 的 ColorDialog 不支持 Alpha，所以这里自己画一个。
    /// </summary>
    internal sealed class ColorPickerWindow : Window
    {
        public KvColor Result { get; private set; }

        private readonly Slider[] _sliders = new Slider[4];
        private readonly TextBox[] _boxes = new TextBox[4];
        private readonly Border _preview;
        private readonly Border _previewOld;
        private bool _sync;

        public ColorPickerWindow(KvColor initial, string title)
        {
            Result = initial;

            Title = "选择颜色 —— " + title;
            Width = 360;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Kit.Bg;
            Foreground = Kit.Text;
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            WindowStyle = WindowStyle.ToolWindow;
            ShowInTaskbar = false;

            var root = new StackPanel { Margin = new Thickness(14) };

            // ---- 预览 ----
            _previewOld = new Border
            {
                Height = 42,
                BorderBrush = Kit.Border,
                BorderThickness = new Thickness(1),
                Background = initial.ToBrush()
            };

            _preview = new Border
            {
                Height = 42,
                BorderBrush = Kit.Border,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 6, 0, 0),
                Background = initial.ToBrush()
            };

            root.Children.Add(PreviewRow("原始", _previewOld));
            root.Children.Add(_preview);

            // ---- 四个分量 ----
            string[] names = { "R", "G", "B", "A" };
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var slider = new Slider
                {
                    Minimum = 0,
                    Maximum = 255,
                    Width = 200,
                    IsSnapToTickEnabled = true,
                    TickFrequency = 1
                };

                var box = new TextBox
                {
                    Width = 48,
                    Padding = new Thickness(4, 2, 4, 2),
                    Background = Kit.PanelAlt,
                    Foreground = Kit.Text,
                    BorderBrush = Kit.Border,
                    FontSize = 12.5
                };

                slider.ValueChanged += (s, e) =>
                {
                    if (_sync) return;
                    box.Text = ((int)slider.Value).ToString();
                    Push();
                };
                box.LostFocus += (s, e) => CommitBox(idx, box);
                box.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitBox(idx, box); };

                _sliders[i] = slider;
                _boxes[i] = box;

                root.Children.Add(Kit.Row(names[i], Kit.HRow(slider, box), 34));
            }

            // ---- HEX ----
            var hex = new TextBox
            {
                Width = 200,
                Padding = new Thickness(5, 3, 5, 3),
                Background = Kit.PanelAlt,
                Foreground = Kit.Text,
                BorderBrush = Kit.Border,
                FontSize = 12.5,
                Text = initial.ToHex()
            };
            hex.KeyDown += (s, e) =>
            {
                if (e.Key != Key.Enter) return;
                var c = KvColor.Parse(hex.Text);
                Load(c);
            };
            hex.LostFocus += (s, e) =>
            {
                var c = KvColor.Parse(hex.Text);
                Load(c);
            };
            root.Children.Add(Kit.Row("HEX (#AARRGGBB)", hex, 34));

            // ---- 按钮 ----
            var ok = Kit.Button("确定", () => { DialogResult = true; }, accent: true);
            var cancel = Kit.Button("取消", () => { DialogResult = false; });
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            buttons.Children.Add(ok);
            cancel.Margin = new Thickness(8, 0, 0, 0);
            buttons.Children.Add(cancel);
            root.Children.Add(buttons);

            Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Resources = Kit.Theme();

            Load(initial);
        }

        private static FrameworkElement PreviewRow(string label, Border box)
        {
            var sp = new StackPanel();
            sp.Children.Add(Kit.Text2(label, 11.5, Kit.Sub));
            sp.Children.Add(box);
            return sp;
        }

        private void CommitBox(int idx, TextBox box)
        {
            if (int.TryParse(box.Text, out int v))
            {
                _sliders[idx].Value = Math.Max(0, Math.Min(255, v));
            }
            box.Text = ((int)_sliders[idx].Value).ToString();
        }

        private void Load(KvColor c)
        {
            _sync = true;
            _sliders[0].Value = Math.Round(c.r * 255.0);
            _sliders[1].Value = Math.Round(c.g * 255.0);
            _sliders[2].Value = Math.Round(c.b * 255.0);
            _sliders[3].Value = Math.Round(c.a * 255.0);
            _sync = false;
            Push();
        }

        private void Push()
        {
            Result = KvColor.Rgba(
                (float)(_sliders[0].Value / 255.0),
                (float)(_sliders[1].Value / 255.0),
                (float)(_sliders[2].Value / 255.0),
                (float)(_sliders[3].Value / 255.0));

            _preview.Background = Result.ToBrush();

            for (int i = 0; i < 4; i++) _boxes[i].Text = ((int)_sliders[i].Value).ToString();
        }
    }
}
