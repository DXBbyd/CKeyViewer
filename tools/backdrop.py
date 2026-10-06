"""铺一块深色背景窗口，垫在覆盖层下面，给文档截图一个稳定可复现的背景。

用法: python backdrop.py [显示秒数] [--keep]

窗口是普通（非置顶）窗口，会盖住浏览器 / 聊天软件，但仍在覆盖层（WS_EX_TOPMOST）之下。
不带 --keep 时会在指定秒数后自己销毁并退出。
"""
import ctypes
import ctypes.wintypes as wt
import sys
import time

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32
user32.SetProcessDPIAware()

WS_POPUP = 0x80000000
WS_VISIBLE = 0x10000000
WS_EX_TOOLWINDOW = 0x00000080
WS_EX_NOACTIVATE = 0x08000000
SW_SHOWNOACTIVATE = 4
WM_PAINT = 0x000F
WM_DESTROY = 0x0002
WM_TIMER = 0x0113
HWND_TOP = 0
SWP_NOSIZE = 0x0001
SWP_NOMOVE = 0x0002
SWP_NOACTIVATE = 0x0010
SWP_SHOWWINDOW = 0x0040

LRESULT = ctypes.c_longlong if ctypes.sizeof(ctypes.c_void_p) == 8 else ctypes.c_long
WNDPROC = ctypes.WINFUNCTYPE(LRESULT, wt.HWND, wt.UINT, wt.WPARAM, wt.LPARAM)


class WNDCLASSW(ctypes.Structure):
    _fields_ = [("style", wt.UINT), ("lpfnWndProc", WNDPROC),
                ("cbClsExtra", ctypes.c_int), ("cbWndExtra", ctypes.c_int),
                ("hInstance", wt.HINSTANCE), ("hIcon", wt.HICON),
                ("hCursor", wt.HANDLE), ("hbrBackground", wt.HBRUSH),
                ("lpszMenuName", wt.LPCWSTR), ("lpszClassName", wt.LPCWSTR)]


class PAINTSTRUCT(ctypes.Structure):
    _fields_ = [("hdc", wt.HDC), ("fErase", wt.BOOL), ("rcPaint", wt.RECT),
                ("fRestore", wt.BOOL), ("fIncUpdate", wt.BOOL),
                ("rgbReserved", ctypes.c_byte * 32)]


class TRIVERTEX(ctypes.Structure):
    _fields_ = [("x", ctypes.c_long), ("y", ctypes.c_long),
                ("Red", ctypes.c_ushort), ("Green", ctypes.c_ushort),
                ("Blue", ctypes.c_ushort), ("Alpha", ctypes.c_ushort)]


class GRADIENT_RECT(ctypes.Structure):
    _fields_ = [("UpperLeft", wt.UINT), ("LowerRight", wt.UINT)]


SW = user32.GetSystemMetrics(0)
SH = user32.GetSystemMetrics(1)

# 与应用主题同色系的两段色（注意 GDI 的 COLORREF 是 BGR：低字节 = Blue）
C_TOP = (0x1C, 0x11, 0x18)     # 近黑紫
C_BOT = (0x52, 0x2C, 0x38)     # 略亮的紫
GRID = 0x00403050              # 网格线（BGR）


def paint(hwnd):
    hdc = user32.GetDC(hwnd)
    rc = wt.RECT()
    user32.GetClientRect(hwnd, ctypes.byref(rc))

    verts = (TRIVERTEX * 2)()
    for i, (col, y) in enumerate(((C_TOP, rc.top), (C_BOT, rc.bottom))):
        verts[i].x, verts[i].y = rc.left, y
        verts[i].Blue, verts[i].Green, verts[i].Red = col[0], col[1], col[2]
        verts[i].Alpha = 0xFF

    gr = GRADIENT_RECT()
    gr.UpperLeft, gr.LowerRight = 0, 1
    try:
        ctypes.windll.msimg32.GradientFill(hdc, verts, 2, ctypes.byref(gr), 1, 0)
    except OSError:
        gdi32.FillRect(hdc, ctypes.byref(rc), gdi32.CreateSolidBrush(0x0015111C))

    # 淡淡的参考网格，衬出覆盖层的网格线
    pen = gdi32.CreatePen(0, 1, GRID)
    old = gdi32.SelectObject(hdc, pen)
    for x in range(0, rc.right, 120):
        gdi32.MoveToEx(hdc, x, 0, None)
        gdi32.LineTo(hdc, x, rc.bottom)
    for y in range(0, rc.bottom, 120):
        gdi32.MoveToEx(hdc, 0, y, None)
        gdi32.LineTo(hdc, rc.right, y)
    gdi32.SelectObject(hdc, old)
    gdi32.DeleteObject(pen)

    user32.ReleaseDC(hwnd, hdc)


def _wndproc(hwnd, msg, wparam, lparam):
    if msg == WM_PAINT:
        ps = PAINTSTRUCT()
        user32.BeginPaint(hwnd, ctypes.byref(ps))
        paint(hwnd)
        user32.EndPaint(hwnd, ctypes.byref(ps))
        return 0
    if msg == WM_TIMER:
        # 前台的聊天 / 浏览器随时会把自己提到前面来，这里定期把自己重新顶上去
        user32.SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW)
        return 0
    if msg == WM_DESTROY:
        user32.PostQuitMessage(0)
        return 0
    return user32.DefWindowProcW(hwnd, msg, wparam, lparam)


PROC = WNDPROC(_wndproc)


def main():
    secs = 30.0
    keep = "--keep" in sys.argv
    for a in sys.argv[1:]:
        try:
            secs = float(a)
        except ValueError:
            pass

    hinst = ctypes.windll.kernel32.GetModuleHandleW(None)
    cls = WNDCLASSW()
    cls.style = 0
    cls.lpfnWndProc = PROC
    cls.hInstance = hinst
    cls.hbrBackground = gdi32.CreateSolidBrush(0x0015111C)
    cls.lpszClassName = "CkvShotBackdrop"
    if not user32.RegisterClassW(ctypes.byref(cls)):
        err = ctypes.windll.kernel32.GetLastError()
        if err != 1410:      # 1410 = class already exists
            raise SystemExit("RegisterClassW failed: %d" % err)

    hwnd = user32.CreateWindowExW(
        WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, "CkvShotBackdrop", "backdrop",
        WS_POPUP | WS_VISIBLE, 0, 0, SW, SH, None, None, hinst, None)
    if not hwnd:
        raise SystemExit("CreateWindowExW failed")

    user32.SetWindowPos(hwnd, HWND_TOP, 0, 0, SW, SH, SWP_SHOWWINDOW | SWP_NOACTIVATE)
    user32.ShowWindow(hwnd, SW_SHOWNOACTIVATE)
    user32.UpdateWindow(hwnd)
    user32.SetTimer(hwnd, 1, 250, None)
    print("backdrop %dx%d shown (%.0fs, keep=%s)" % (SW, SH, secs, keep))

    if keep:
        try:
            while True:
                time.sleep(0.5)
        except KeyboardInterrupt:
            pass
    else:
        time.sleep(secs)

    user32.DestroyWindow(hwnd)
    print("backdrop closed")


if __name__ == "__main__":
    main()
