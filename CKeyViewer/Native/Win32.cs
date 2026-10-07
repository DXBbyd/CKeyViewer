using System;
using System.Collections.Generic;
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

        // ---- 枚举 / 查找窗口（吸附到 ADOFAI 游戏窗口用）----

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        /// <summary>一个可见顶层窗口的快照（吸附判定 + 诊断共用）。</summary>
        public struct WinInfo
        {
            public IntPtr Hwnd;
            public int Pid;
            public string Class;
            public string Title;
            public int Left, Top, Right, Bottom;
            /// <summary>最小化（图标化）。这种窗口的矩形是 -32000 之类的假坐标，不能拿来吸附。</summary>
            public bool Iconic;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
            public int Area => Math.Max(0, Width) * Math.Max(0, Height);
        }

        /// <summary>游戏窗口可用的最小面积（物理像素）。用来滤掉 1×1 / 0×0 / 1905×4 这类
        /// 「可见但没意义」的系统辅助窗口 —— 它们曾经让 UnityWndClass 兜底匹配到错误的窗口。</summary>
        public const int MinGameWindowArea = 200 * 200;

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        /// <summary>枚举全部可见顶层窗口。一次 EnumWindows，不区分归属进程。</summary>
        public static List<WinInfo> ListVisibleTopLevelWindows()
        {
            var list = new List<WinInfo>(48);
            EnumWindows((hwnd, lparam) =>
            {
                if (!IsWindowVisibleNative(hwnd)) return true;
                if (!GetWindowRect(hwnd, out RECT rr)) return true;

                var cls = new System.Text.StringBuilder(256);
                GetClassName(hwnd, cls, cls.Capacity);

                string title = "";
                int len = GetWindowTextLength(hwnd);
                if (len > 0)
                {
                    var sb = new System.Text.StringBuilder(len + 1);
                    GetWindowText(hwnd, sb, len + 1);
                    title = sb.ToString();
                }

                GetWindowThreadProcessId(hwnd, out uint pid);

                list.Add(new WinInfo
                {
                    Hwnd = hwnd,
                    Pid = (int)pid,
                    Class = cls.ToString(),
                    Title = title,
                    Left = rr.Left,
                    Top = rr.Top,
                    Right = rr.Right,
                    Bottom = rr.Bottom,
                    Iconic = IsIconic(hwnd),
                });
                return true;
            }, IntPtr.Zero);
            return list;
        }

        /// <summary>
        /// 从窗口快照里挑出游戏窗口（**纯函数**，便于自测）。
        /// <para>
        /// 挑选顺序（先满足的优先，同级取面积最大者）：
        /// <list type="number">
        /// <item>归属 ADOFAI 进程 且 类是 UnityWndClass</item>
        /// <item>归属 ADOFAI 进程（其余窗口类）</item>
        /// <item>标题命中</item>
        /// </list>
        /// 「按 PID 认」比「按标题认」可靠得多：Unity Mod Manager 会改写窗口标题，
        /// 汉化 / 新版也可能换标题，但进程名一直是 <c>A Dance of Fire and Ice</c>；
        /// 而且切全屏时 Unity 可能换一个窗口，PID 不变。
        /// </para>
        /// <para>
        /// 这里**故意不再保留**「只按 <c>UnityWndClass</c> 兜底」那条路：
        /// 用户机器上同时装着别的 Unity 游戏（Hollow Knight、Rhythm Doctor…），
        /// 那样会把覆盖层吸到无关的游戏窗口上 —— 而 ADOFAI 没开时本来就该退回工作区吸附。
        /// 换过 exe 名又换过窗口标题的版本，让用户在「游戏窗口匹配（标题包含）」里自己填。
        /// </para>
        /// 最小化的窗口不算命中（<paramref name="minimized"/> 会置位），
        /// 让调用方退回普通吸附，而不是把覆盖层吸到 -32000 的假坐标上。
        /// </summary>
        public static bool PickGameWindow(IList<WinInfo> wins, string titlePart, IList<int> pids,
                                          int selfPid, out WinInfo win, out bool minimized, out string reason)
        {
            win = default;
            minimized = false;
            reason = null;

            string needle = (string.IsNullOrEmpty(titlePart) ? "A Dance of Fire and Ice"
                                                             : titlePart).ToLowerInvariant();
            var pidSet = new HashSet<int>();
            if (pids != null) foreach (int p in pids) pidSet.Add(p);

            // 自己进程的窗口永远不是游戏窗口（覆盖层的标题是空的，但别留这个隐患）
            bool Usable(WinInfo w) => !w.Iconic && w.Area >= MinGameWindowArea && w.Pid != selfPid;
            bool OwnedByGame(WinInfo w) => pidSet.Count > 0 && pidSet.Contains(w.Pid);

            // 有没有一个被最小化的游戏窗口？单独记一笔，用来区分「游戏没开」和「游戏最小化了」
            bool minimizedHit = false;

            WinInfo? best = null;
            int bestRank = int.MaxValue;
            int bestArea = -1;

            void Consider(WinInfo w, int rank)
            {
                if (OwnedByGame(w) && w.Iconic) minimizedHit = true;
                if (!Usable(w)) return;
                int area = w.Area;
                if (rank < bestRank || (rank == bestRank && area > bestArea))
                {
                    best = w; bestRank = rank; bestArea = area;
                }
            }

            foreach (WinInfo w in wins)
            {
                bool unity = string.Equals(w.Class, "UnityWndClass", StringComparison.Ordinal);
                bool titled = w.Title.Length > 0 && w.Title.ToLowerInvariant().Contains(needle);

                if (OwnedByGame(w)) Consider(w, unity ? 0 : 1);
                else if (titled) Consider(w, 2);
            }

            if (best.HasValue) { win = best.Value; return true; }

            if (minimizedHit)
            {
                minimized = true;
                reason = "游戏窗口已最小化，暂时不吸附";
            }
            else if (pidSet.Count == 0)
            {
                reason = "没找到 ADOFAI 进程（游戏没开？）";
            }
            else
            {
                reason = $"找到 {pidSet.Count} 个 ADOFAI 进程，但没有可用的顶层窗口（游戏还在启动？）";
            }
            return false;
        }

        /// <summary>
        /// 找 ADOFAI 游戏窗口：按进程归属 + 标题 + Unity 窗口类三重判定（见
        /// <see cref="PickGameWindow"/>）。命中返回物理像素矩形。
        /// </summary>
        /// <param name="pids">ADOFAI 的进程 ID（可空；空则只按标题 / 窗口类认）。</param>
        /// <param name="reason">未命中时的原因（诊断用）。</param>
        /// <param name="info">命中时的窗口快照（诊断用）。</param>
        public static bool FindGameWindow(string titlePart, IList<int> pids,
                                          out int left, out int top, out int right, out int bottom,
                                          out string reason, out WinInfo info)
        {
            left = top = right = bottom = 0;
            reason = null;
            info = default;

            var wins = ListVisibleTopLevelWindows();
            int self = System.Diagnostics.Process.GetCurrentProcess().Id;
            if (!PickGameWindow(wins, titlePart, pids, self, out WinInfo w, out bool minimized, out reason))
            {
                if (minimized) reason = "游戏窗口已最小化，暂时不吸附";
                return false;
            }

            info = w;
            left = w.Left; top = w.Top; right = w.Right; bottom = w.Bottom;
            return true;
        }

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
        public const int VK_MENU = 0x12;   // 左 Alt：预设窗口「按住热键拖」的默认键
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
