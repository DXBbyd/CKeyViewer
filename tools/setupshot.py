"""给安装程序窗口拍一张截图，用于 README。

用法: python setupshot.py <setup.exe 路径> <输出 png> [标题关键字] [传给 setup.exe 的参数...]

流程：拉起 GUI 安装程序 → 轮询 EnumWindows 找窗口 → PrintWindow 渲染窗口内容 →
存 PNG → 结束进程。全程不点任何按钮，不会真的安装。

想拍卸载界面就追加 --uninstall：
    python setupshot.py <setup.exe> out.png "卸载 CKeyViewer" --uninstall

用 PrintWindow(PW_RENDERFULLCONTENT) 而不是抓屏：窗口可能被别的程序压住
（比如用户正玩着的游戏），抓屏只会拍到压在上面的东西。
"""
import ctypes
import ctypes.wintypes as wt
import os
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import screenshot  # noqa: E402  （顺带设置 SetProcessDPIAware）

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32

EnumWindowsProc = ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)

user32.GetWindowDC.restype = wt.HDC
user32.GetWindowDC.argtypes = [wt.HWND]
user32.ReleaseDC.restype = ctypes.c_int
user32.ReleaseDC.argtypes = [wt.HWND, wt.HDC]
user32.PrintWindow.restype = wt.BOOL
user32.PrintWindow.argtypes = [wt.HWND, wt.HDC, wt.UINT]
user32.GetWindowRect.argtypes = [wt.HWND, ctypes.c_void_p]

gdi32.CreateCompatibleDC.restype = wt.HDC
gdi32.CreateCompatibleDC.argtypes = [wt.HDC]
gdi32.CreateCompatibleBitmap.restype = wt.HBITMAP
gdi32.CreateCompatibleBitmap.argtypes = [wt.HDC, ctypes.c_int, ctypes.c_int]
gdi32.SelectObject.restype = wt.HGDIOBJ
gdi32.SelectObject.argtypes = [wt.HDC, wt.HGDIOBJ]
gdi32.DeleteObject.argtypes = [wt.HGDIOBJ]
gdi32.DeleteDC.argtypes = [wt.HDC]
# 不声明 argtypes 的话，HBITMAP 会被当成 c_int 转换 —— 64 位下直接 OverflowError
gdi32.GetDIBits.restype = ctypes.c_int
gdi32.GetDIBits.argtypes = [wt.HDC, wt.HBITMAP, wt.UINT, wt.UINT,
                            ctypes.c_void_p, ctypes.c_void_p, wt.UINT]

PW_RENDERFULLCONTENT = 0x00000002
HWND_TOPMOST = -1
SWP_NOMOVE = 0x0002
SWP_NOSIZE = 0x0001
SWP_NOACTIVATE = 0x0010


class RECT(ctypes.Structure):
    _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long),
                ("right", ctypes.c_long), ("bottom", ctypes.c_long)]


def find_window(needle, timeout=40.0):
    hit = []

    def cb(hwnd, lparam):
        if not user32.IsWindowVisible(hwnd):
            return True
        n = user32.GetWindowTextLengthW(hwnd)
        if n <= 0:
            return True
        buf = ctypes.create_unicode_buffer(n + 1)
        user32.GetWindowTextW(hwnd, buf, n + 1)
        if needle in buf.value:
            hit.append((hwnd, buf.value))
            return False
        return True

    t0 = time.time()
    while time.time() - t0 < timeout:
        user32.EnumWindows(EnumWindowsProc(cb), 0)
        if hit:
            return hit[0]
        time.sleep(0.15)
    return None


def print_window(hwnd, w, h):
    hdc = user32.GetWindowDC(hwnd)
    memdc = gdi32.CreateCompatibleDC(hdc)
    bmp = gdi32.CreateCompatibleBitmap(hdc, w, h)
    old = gdi32.SelectObject(memdc, bmp)
    try:
        user32.PrintWindow(hwnd, memdc, PW_RENDERFULLCONTENT)

        bi = screenshot.BITMAPINFO()
        bi.bmiHeader.biSize = ctypes.sizeof(screenshot.BITMAPINFOHEADER)
        bi.bmiHeader.biWidth = w
        bi.bmiHeader.biHeight = -h          # 顶向下
        bi.bmiHeader.biPlanes = 1
        bi.bmiHeader.biBitCount = 32
        bi.bmiHeader.biCompression = 0

        buf = ctypes.create_string_buffer(w * h * 4)
        gdi32.GetDIBits(memdc, bmp, 0, h, buf, ctypes.byref(bi), 0)
        return buf.raw
    finally:
        gdi32.SelectObject(memdc, old)
        gdi32.DeleteObject(bmp)
        gdi32.DeleteDC(memdc)
        user32.ReleaseDC(hwnd, hdc)


def main():
    exe = sys.argv[1]
    out = sys.argv[2]
    needle = sys.argv[3] if len(sys.argv) > 3 else "CKeyViewer"
    extra = sys.argv[4:]

    proc = subprocess.Popen([exe] + extra, cwd=os.path.dirname(os.path.abspath(exe)))
    try:
        win = find_window(needle)
        if not win:
            print("找不到窗口")
            return 1
        hwnd, title = win

        # 顶到最前只是为了 PrintWindow 更稳；被遮挡也不影响结果
        user32.SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE)
        time.sleep(1.2)

        r = RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(r))
        w, h = r.right - r.left, r.bottom - r.top
        print("window '%s' hwnd=0x%X rect=%d,%d %dx%d" % (title, hwnd, r.left, r.top, w, h))

        data = print_window(hwnd, w, h)
        if not any(data):
            print("PrintWindow 返回空白")
            return 1
        screenshot.write_png(out, w, h, data)
        print("saved", out)
        return 0
    finally:
        try:
            proc.kill()
        except Exception:
            pass


if __name__ == "__main__":
    sys.exit(main())
