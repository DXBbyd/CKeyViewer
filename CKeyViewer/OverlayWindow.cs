using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CKeyViewer.Native;
using CKeyViewer.Render;

namespace CKeyViewer
{
    /// <summary>
    /// 覆盖层窗口：无边框 + 逐像素透明 + 置顶 + 鼠标穿透 + 不抢焦点。
    /// </summary>
    public sealed class OverlayWindow : Window
    {
        private IntPtr _hwnd = IntPtr.Zero;

        public OverlayRenderer Renderer { get; }

        private bool _clickThrough = true;
        private bool _topmost = true;

        public OverlayWindow()
        {
            Renderer = new OverlayRenderer();

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
            Content = Renderer;

            SourceInitialized += OnSourceInitialized;
        }

        public bool ClickThrough
        {
            get => _clickThrough;
            set { _clickThrough = value; ApplyClickThrough(); }
        }

        public IntPtr Handle => _hwnd;

        /// <summary>
        /// 当前监视器的 DPI 缩放（DIP → 物理像素）。窗口坐标用 DIP，
        /// 而屏幕尺寸要用物理像素，两者之间必须经过它换算。
        /// </summary>
        public double DpiScale
        {
            get
            {
                try
                {
                    double s = VisualTreeHelper.GetDpi(this).DpiScaleX;
                    return s > 0.01 ? s : 1.0;
                }
                catch
                {
                    return 1.0;
                }
            }
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            _hwnd = Win32.Handle(this);
            Win32.MakeToolWindow(_hwnd);
            ApplyClickThrough();
            ApplyTopmost();
        }

        private void ApplyClickThrough()
        {
            if (_hwnd == IntPtr.Zero) return;
            Win32.SetClickThrough(_hwnd, _clickThrough);
        }

        private void ApplyTopmost()
        {
            if (_hwnd == IntPtr.Zero) return;
            Win32.SetTopmost(_hwnd, _topmost);
        }
    }
}
