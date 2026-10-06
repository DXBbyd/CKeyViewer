"""抓取桌面截图（含分层窗口），保存为 PNG。纯 ctypes，无第三方依赖。

用法: python screenshot.py <输出路径> [x y w h] [zoom]

zoom 为整数最近邻放大倍数（默认 1），用于放大查看文字细节。
"""
import ctypes
import ctypes.wintypes as wt
import struct
import sys
import zlib

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32

user32.SetProcessDPIAware()

SM_CXSCREEN = 0
SM_CYSCREEN = 1
SRCCOPY = 0x00CC0020
CAPTUREBLT = 0x40000000


class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [
        ("biSize", wt.DWORD), ("biWidth", ctypes.c_long), ("biHeight", ctypes.c_long),
        ("biPlanes", wt.WORD), ("biBitCount", wt.WORD), ("biCompression", wt.DWORD),
        ("biSizeImage", wt.DWORD), ("biXPelsPerMeter", ctypes.c_long),
        ("biYPelsPerMeter", ctypes.c_long), ("biClrUsed", wt.DWORD), ("biClrImportant", wt.DWORD),
    ]


class BITMAPINFO(ctypes.Structure):
    _fields_ = [("bmiHeader", BITMAPINFOHEADER), ("bmiColors", wt.DWORD * 3)]


def capture(x, y, w, h):
    hdesktop = user32.GetDesktopWindow()
    hdc = user32.GetWindowDC(hdesktop)
    memdc = gdi32.CreateCompatibleDC(hdc)
    bmp = gdi32.CreateCompatibleBitmap(hdc, w, h)
    gdi32.SelectObject(memdc, bmp)

    gdi32.BitBlt(memdc, 0, 0, w, h, hdc, x, y, SRCCOPY | CAPTUREBLT)

    bi = BITMAPINFO()
    bi.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
    bi.bmiHeader.biWidth = w
    bi.bmiHeader.biHeight = -h          # 顶向下
    bi.bmiHeader.biPlanes = 1
    bi.bmiHeader.biBitCount = 32
    bi.bmiHeader.biCompression = 0      # BI_RGB

    buf = ctypes.create_string_buffer(w * h * 4)
    gdi32.GetDIBits(memdc, bmp, 0, h, buf, ctypes.byref(bi), 0)

    gdi32.DeleteObject(bmp)
    gdi32.DeleteDC(memdc)
    user32.ReleaseDC(hdesktop, hdc)
    return buf.raw


def write_png(path, w, h, bgra, zoom=1):
    """bgra 为顶向下 32 位缓冲；zoom>1 时做最近邻放大。"""
    def rows():
        stride = w * 4
        for row in range(h):
            line = bgra[row * stride:(row + 1) * stride]
            # BGRX -> RGBA
            yield b"".join(
                line[i + 2:i + 3] + line[i + 1:i + 2] + line[i:i + 1] + line[i + 3:i + 4]
                for i in range(0, stride, 4)
            )

    out_w, out_h = w * zoom, h * zoom
    raw = bytearray()
    for packed in rows():
        if zoom > 1:
            # 先在横向上复制像素
            wide = b"".join(packed[i:i + 4] * zoom for i in range(0, len(packed), 4))
            for _ in range(zoom):
                raw.append(0)
                raw.extend(wide)
        else:
            raw.append(0)
            raw.extend(packed)

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", out_w, out_h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 6))
    png += chunk(b"IEND", b"")
    open(path, "wb").write(png)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "shot.png"
    if len(sys.argv) >= 6:
        x, y, w, h = (int(v) for v in sys.argv[2:6])
    else:
        x = 0
        y = 0
        w = user32.GetSystemMetrics(SM_CXSCREEN)
        h = user32.GetSystemMetrics(SM_CYSCREEN)

    zoom = int(sys.argv[6]) if len(sys.argv) >= 7 else 1
    if zoom < 1:
        zoom = 1

    data = capture(x, y, w, h)
    write_png(out, w, h, data, zoom)
    print("saved %s  %dx%d  (origin %d,%d, zoom %d)" % (out, w, h, x, y, zoom))


if __name__ == "__main__":
    main()
