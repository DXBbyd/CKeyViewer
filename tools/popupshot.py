"""枚举当前所有可见顶层窗口，找 WPF Popup（类名带 HwndWrapper[ContextMenu）。

配合 Kit.DebugOpenCombo 用：这台机器点不开菜单，只能靠环境变量把菜单硬打开，
然后靠这个脚本确认那个 Popup 窗口**真的存在**、再单独 PrintWindow 拍它
—— 因为 Popup 是独立的 HWND，PrintWindow 设置窗口那一路是拍不到它的。

用法: python tools/popupshot.py <输出路径> [标题关键字]
"""
import ctypes
import ctypes.wintypes as wt
import os
import struct
import sys
import time
import zlib

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32
try:
    ctypes.windll.shcore.SetProcessDpiAwareness(2)
except Exception:
    try:
        user32.SetProcessDPIAware()
    except Exception:
        pass


def enum_windows():
    out = []
    WNDENUMPROC = ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)

    def cb(hwnd, _l):
        if not user32.IsWindowVisible(hwnd):
            return True
        n = user32.GetWindowTextLengthW(hwnd)
        buf = ctypes.create_unicode_buffer(n + 2)
        user32.GetWindowTextW(hwnd, buf, n + 2)
        cls = ctypes.create_unicode_buffer(256)
        user32.GetClassNameW(hwnd, cls, 256)
        r = wt.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(r))
        out.append((hwnd, cls.value, buf.value, (r.left, r.top, r.right, r.bottom)))
        return True

    user32.EnumWindows(WNDENUMPROC(cb), 0)
    return out


def write_png(path, w, h, pixels):
    raw = b"".join(b"\x00" + pixels[y * w * 4:(y + 1) * w * 4] for y in range(h))

    def chunk(tag, data):
        c = tag + data
        return struct.pack(">I", len(data)) + c + struct.pack(">I", zlib.crc32(c) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 6))
    png += chunk(b"IEND", b"")
    open(path, "wb").write(png)


def shot(hwnd, path):
    r = wt.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(r))
    w, h = r.right - r.left, r.bottom - r.top
    if w <= 0 or h <= 0:
        return None
    hdc = user32.GetWindowDC(hwnd)
    mdc = gdi32.CreateCompatibleDC(hdc)
    bmp = gdi32.CreateCompatibleBitmap(hdc, w, h)
    gdi32.SelectObject(mdc, bmp)
    # PW_RENDERFULLCONTENT = 2
    user32.PrintWindow(hwnd, mdc, 2)

    class BMI(ctypes.Structure):
        _fields_ = [("biSize", wt.DWORD), ("biWidth", wt.LONG), ("biHeight", wt.LONG),
                    ("biPlanes", wt.WORD), ("biBitCount", wt.WORD), ("biCompression", wt.DWORD),
                    ("biSizeImage", wt.DWORD), ("biXPelsPerMeter", wt.LONG),
                    ("biYPelsPerMeter", wt.LONG), ("biClrUsed", wt.DWORD),
                    ("biClrImportant", wt.DWORD), ("colors", wt.DWORD * 3)]

    bi = BMI()
    bi.biSize = 40
    bi.biWidth = w
    bi.biHeight = -h          # 负数 = 自上而下
    bi.biPlanes = 1
    bi.biBitCount = 32
    buf = ctypes.create_string_buffer(w * h * 4)
    gdi32.GetDIBits(mdc, bmp, 0, h, buf, ctypes.byref(bi), 0)
    write_png(path, w, h, buf.raw)
    gdi32.DeleteObject(bmp)
    gdi32.DeleteDC(mdc)
    user32.ReleaseDC(hwnd, hdc)
    return w, h


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else r"E:\ckv_stage\popup.png"
    pat = sys.argv[2] if len(sys.argv) > 2 else "ContextMenu"

    wins = enum_windows()
    print(f"可见顶层窗口 {len(wins)} 个：")
    for hwnd, cls, title, rect in wins:
        mark = ""
        if pat.lower() in cls.lower():
            mark = "  <== Popup"
        if title or mark:
            print(f"  hwnd={hwnd:<10} {cls[:52]:<52} {title[:34]:<34} "
                  f"{rect[2]-rect[0]}x{rect[3]-rect[1]} @ {rect[0]},{rect[1]}{mark}")

    target = next((w for w in wins if pat.lower() in w[1].lower()), None)
    if not target:
        print(f"\n没找到类名含 {pat} 的窗口")
        return 1
    r = shot(target[0], out)
    print(f"\n已拍 Popup: {out}  {r[0]}x{r[1]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())