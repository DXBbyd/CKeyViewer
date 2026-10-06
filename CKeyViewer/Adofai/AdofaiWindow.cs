using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CKeyViewer.Core;
using CKeyViewer.Native;

namespace CKeyViewer.Adofai
{
    /// <summary>
    /// ADOFAI 信息覆盖层自己的窗口：铺满<b>工作区</b>的透明置顶覆盖层。
    /// <para>
    /// 为什么要单独一块窗口，而不是画在按键覆盖层里：按键覆盖层在预设布局下
    /// 只有键帽那么大一块，信息最多只能在那一小块里挪。用户要的是「全屏都能移动」，
    /// 所以这里给信息层一块铺满工作区的画布 —— 元素因此可以放到屏幕任何角落。
    /// </para>
    /// </summary>
    public sealed class AdofaiWindow : Window
    {
        private IntPtr _hwnd;
        private bool _clickThrough = true;

        public AdofaiOverlay Overlay { get; }
        public AdofaiSurface Surface { get; }

        public AdofaiWindow(AdofaiOverlay overlay)
        {
            Overlay = overlay;
            Surface = new AdofaiSurface(overlay);

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SnapsToDevicePixels = false;
            UseLayoutRounding = false;
            Focusable = false;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Content = Surface;

            SourceInitialized += OnSourceInitialized;
            ApplyBounds();
        }

        public IntPtr Handle => _hwnd;

        public double DpiScale
        {
            get
            {
                try
                {
                    double s = VisualTreeHelper.GetDpi(this).DpiScaleX;
                    return s > 0.01 ? s : 1.0;
                }
                catch { return 1.0; }
            }
        }

        public bool ClickThrough
        {
            get => _clickThrough;
            set { _clickThrough = value; Win32.SetClickThrough(_hwnd, value); }
        }

        /// <summary>铺满当前工作区（DIP）。工作区变了就再调一次 —— 这就是「动态吸附」的一半。</summary>
        public void ApplyBounds()
        {
            var wa = KvSnap.WorkArea;
            Left = wa.Left;
            Top = wa.Top;
            Width = Math.Max(1, wa.Width);
            Height = Math.Max(1, wa.Height);
        }

        public void Invalidate() => Surface.InvalidateVisual();

        /// <summary>屏幕物理像素坐标 → 窗口本地 DIP 坐标。</summary>
        public bool TryLocalPoint(out Point p)
        {
            p = new Point();
            if (!Win32.GetCursorPosition(out int px, out int py)) return false;

            double dpi = DpiScale;
            p = new Point(px / dpi - Left, py / dpi - Top);
            return true;
        }

        /// <summary>窗口是 WS_EX_NOACTIVATE，直接 SetWindowPos 抬到 z 序最上面。</summary>
        public void Raise()
        {
            if (_hwnd != IntPtr.Zero) Win32.BringToTop(_hwnd);
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            _hwnd = Win32.Handle(this);
            Win32.MakeToolWindow(_hwnd);
            Win32.SetClickThrough(_hwnd, _clickThrough);
            Win32.SetTopmost(_hwnd, true);
        }
    }

    /// <summary>ADOFAI 信息层的绘制面。</summary>
    public sealed class AdofaiSurface : FrameworkElement
    {
        private readonly AdofaiOverlay _overlay;

        public AdofaiSurface(AdofaiOverlay overlay)
        {
            _overlay = overlay;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double ppd = 1.0;
            try { ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }

            var tf = Fonts.Resolve(_overlay.Settings.FontRef, _overlay.Settings.Bold, _overlay.Settings.Italic);

            // scale = 1：字号就是逻辑像素，随系统缩放自动放大，5K 屏上也不会变成蚂蚁字。
            _overlay.Draw(dc, new Rect(0, 0, w, h), tf, ppd, 1.0);
        }
    }

    /// <summary>字体解析：ADOFAI 层的 FontRef 留空时跟随按键层。</summary>
    internal static class Fonts
    {
        public static Typeface Resolve(string fontRef, bool bold, bool italic)
        {
            var weight = bold ? FontWeights.Bold : FontWeights.Normal;
            var style = italic ? FontStyles.Italic : FontStyles.Normal;

            try
            {
                var family = string.IsNullOrWhiteSpace(fontRef)
                    ? new FontFamily("Microsoft YaHei UI, Segoe UI")
                    : new FontFamily(fontRef);
                return new Typeface(family, style, weight, FontStretches.Normal);
            }
            catch
            {
                return new Typeface(new FontFamily("Segoe UI"), style, weight, FontStretches.Normal);
            }
        }
    }
}
