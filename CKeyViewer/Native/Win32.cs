using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CKeyViewer.Native
{
    /// <summary>
    /// Win32 互操作：窗口扩展样式（置顶 / 穿透 / 不抢焦点）、按键状态、全局热键。
    /// </summary>
    internal static class Win32
    {
        public const int GWL_EXSTYLE = -20;

        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;

        public const int WM_HOTKEY = 0x0312;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONWARNING = 0x00000030;
        private const uint MB_SETFOREGROUND = 0x00010000;
        private const uint MB_TOPMOST = 0x00040000;

        /// <summary>
        /// 置顶 + 抢前台的提示弹窗。
        /// 特意不用 WinForms/WPF 的 MessageBox：本程序有全屏**置顶**覆盖层，
        /// 普通弹窗会被它压在下面，用户根本看不见。这里靠 <c>MB_TOPMOST</c> 顶到最前。
        /// </summary>
        public static void Alert(string text, string caption)
        {
            MessageBoxW(IntPtr.Zero, text, caption,
                MB_OK | MB_ICONWARNING | MB_SETFOREGROUND | MB_TOPMOST);
        }

        /// <summary>
        /// 把 stdout 挂到父进程的控制台。WPF 程序标记为 WinExe，默认没有控制台，
        /// 不挂的话 <c>--selftest</c> 的 <c>Console.Write</c> 会石沉大海。
        /// </summary>
        public static void AttachParentConsole()
        {
            const int ATTACH_PARENT_PROCESS = -1;
            try { AttachConsole(ATTACH_PARENT_PROCESS); }
            catch { /* 没有父控制台就算了，自检结果还会写文件 */ }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
        private static extern bool IsWindowVisibleNative(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        public static void GetWindowRect(IntPtr hwnd, out int left, out int top, out int right, out int bottom)
        {
            if (GetWindowRect(hwnd, out RECT r))
            {
                left = r.Left; top = r.Top; right = r.Right; bottom = r.Bottom;
            }
            else
            {
                left = top = right = bottom = 0;
            }
        }

        public static bool IsWindowVisible(IntPtr hwnd) => IsWindowVisibleNative(hwnd);

        /// <summary>把窗口重新提到最顶层（不激活）。</summary>
        public static void BringToTop(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        // ---- 显示器 ----

        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;

        /// <summary>
        /// 主显示器物理像素尺寸。进程已声明 PerMonitorV2 DPI 感知，故这里是真实像素而非虚拟值；
        /// 若取不到则回退到 WPF 的逻辑尺寸，保证不返回 0。
        /// </summary>
        public static void GetPrimaryScreenPixels(out int width, out int height)
        {
            width = GetSystemMetrics(SM_CXSCREEN);
            height = GetSystemMetrics(SM_CYSCREEN);
            if (width <= 0) width = (int)SystemParameters.PrimaryScreenWidth;
            if (height <= 0) height = (int)SystemParameters.PrimaryScreenHeight;
            if (width <= 0) width = 1920;
            if (height <= 0) height = 1080;
        }

        // ---- 扩展样式 ----

        public static int GetExStyle(IntPtr hwnd)
        {
            return IntPtr.Size == 8
                ? (int)GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64()
                : GetWindowLong(hwnd, GWL_EXSTYLE);
        }

        public static void SetExStyle(IntPtr hwnd, int value)
        {
            if (IntPtr.Size == 8)
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(value));
            else
                SetWindowLong(hwnd, GWL_EXSTYLE, value);
        }

        public static void AddExStyle(IntPtr hwnd, int flags)
        {
            SetExStyle(hwnd, GetExStyle(hwnd) | flags);
        }

        public static void RemoveExStyle(IntPtr hwnd, int flags)
        {
            SetExStyle(hwnd, GetExStyle(hwnd) & ~flags);
        }

        /// <summary>把窗口设成不抢焦点、不进 Alt+Tab 的工具窗口。</summary>
        public static void MakeToolWindow(IntPtr hwnd)
        {
            AddExStyle(hwnd, WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }

        /// <summary>开启/关闭鼠标穿透。</summary>
        public static void SetClickThrough(IntPtr hwnd, bool enabled)
        {
            if (enabled)
                AddExStyle(hwnd, WS_EX_TRANSPARENT | WS_EX_LAYERED);
            else
                RemoveExStyle(hwnd, WS_EX_TRANSPARENT);
        }

        /// <summary>开启/关闭置顶。</summary>
        public static void SetTopmost(IntPtr hwnd, bool enabled)
        {
            if (enabled)
                AddExStyle(hwnd, WS_EX_TOPMOST);
            else
                RemoveExStyle(hwnd, WS_EX_TOPMOST);
        }

        // ---- 按键 ----

        /// <summary>物理按键是否处于按下状态（全局，不受焦点影响）。</summary>
        public static bool IsKeyDown(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        // ---- 鼠标 ----

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X, Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>当前鼠标位置（**物理像素**，屏幕坐标）。</summary>
        public static bool GetCursorPosition(out int x, out int y)
        {
            if (GetCursorPos(out POINT p)) { x = p.X; y = p.Y; return true; }
            x = y = 0;
            return false;
        }

        /// <summary>虚拟键码常量（布局模式的 Esc / 方向键 / 鼠标左键轮询用）。</summary>
        public const int VK_LBUTTON = 0x01;
        public const int VK_SHIFT = 0x10;
        public const int VK_ESCAPE = 0x1B;
        public const int VK_LEFT = 0x25;
        public const int VK_UP = 0x26;
        public const int VK_RIGHT = 0x27;
        public const int VK_DOWN = 0x28;

        // ---- 热键 ----

        public static bool RegisterHotKeySafe(IntPtr hwnd, int id, uint mods, uint vk)
        {
            return RegisterHotKey(hwnd, id, mods | MOD_NOREPEAT, vk);
        }

        public static void UnregisterHotKeySafe(IntPtr hwnd, int id)
        {
            UnregisterHotKey(hwnd, id);
        }

        // ---- 窗口辅助 ----

        public static IntPtr Handle(Window w)
        {
            return new WindowInteropHelper(w).Handle;
        }
    }
}
