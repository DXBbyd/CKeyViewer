"""枚举所有可见顶层窗口（hwnd / 归属 PID / 类名 / 标题 / 矩形）。
用来验证 FindGameWindow 那套「标题子串 + UnityWndClass 兜底」到底能不能命中游戏窗口。

用法:
  python winprobe.py                 # 全部可见顶层窗口
  python winprobe.py "dance"         # 只看标题含该子串的
  python winprobe.py --pid 2652      # 只看某个 PID 的窗口
"""
import ctypes
import ctypes.wintypes as wt
import sys

u32 = ctypes.WinDLL("user32", use_last_error=True)

WNDENUMPROC = ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)
u32.EnumWindows.argtypes = [WNDENUMPROC, wt.LPARAM]
u32.GetWindowTextLengthW.argtypes = [wt.HWND]
u32.GetWindowTextW.argtypes = [wt.HWND, ctypes.c_wchar_p, ctypes.c_int]
u32.GetClassNameW.argtypes = [wt.HWND, ctypes.c_wchar_p, ctypes.c_int]
u32.IsWindowVisible.argtypes = [wt.HWND]


class RECT(ctypes.Structure):
    _fields_ = [("Left", ctypes.c_long), ("Top", ctypes.c_long),
                ("Right", ctypes.c_long), ("Bottom", ctypes.c_long)]


u32.GetWindowRect.argtypes = [wt.HWND, ctypes.POINTER(RECT)]


def text_of(hwnd):
    n = u32.GetWindowTextLengthW(hwnd)
    if n <= 0:
        return ""
    buf = ctypes.create_unicode_buffer(n + 1)
    u32.GetWindowTextW(hwnd, buf, n + 1)
    return buf.value


def class_of(hwnd):
    buf = ctypes.create_unicode_buffer(256)
    u32.GetClassNameW(hwnd, buf, 256)
    return buf.value


def pid_of(hwnd):
    p = wt.DWORD()
    u32.GetWindowThreadProcessId(hwnd, ctypes.byref(p))
    return p.value


def main():
    needle = None
    want_pid = None
    args = sys.argv[1:]
    i = 0
    while i < len(args):
        if args[i] == "--pid":
            want_pid = int(args[i + 1]); i += 2
        else:
            needle = args[i].lower(); i += 1

    rows = []

    def cb(hwnd, _):
        if not u32.IsWindowVisible(hwnd):
            return True
        pid = pid_of(hwnd)
        if want_pid is not None and pid != want_pid:
            return True
        title = text_of(hwnd)
        cls = class_of(hwnd)
        if needle and needle not in title.lower():
            return True
        r = RECT()
        u32.GetWindowRect(hwnd, ctypes.byref(r))
        rows.append((hwnd, pid, cls, title, r))
        return True

    u32.EnumWindows(WNDENUMPROC(cb), 0)

    print("%-10s %-8s %-24s %-34s %s" % ("hwnd", "pid", "class", "title", "rect"))
    print("-" * 110)
    for hwnd, pid, cls, title, r in rows:
        print("%-10s %-8d %-24s %-34s (%d,%d)-(%d,%d) %dx%d" % (
            hex(hwnd & 0xFFFFFFFF), pid, cls[:24], title[:34],
            r.Left, r.Top, r.Right, r.Bottom, r.Right - r.Left, r.Bottom - r.Top))
    print("-" * 110)
    print("共 %d 个可见顶层窗口" % len(rows))

    uni = [x for x in rows if x[2] == "UnityWndClass"]
    print("UnityWndClass 窗口: %d 个" % len(uni))
    for hwnd, pid, cls, title, r in uni:
        print("   pid=%d title=%r rect=(%d,%d)-(%d,%d)" % (pid, title, r.Left, r.Top, r.Right, r.Bottom))


main()
