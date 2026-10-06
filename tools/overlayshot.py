"""给覆盖层窗口单独拍图（不抓屏）。

用法: python overlayshot.py <输出路径> [等待秒数] [--no-kill] [--desktop=<路径>]
                            [--win=<标题关键字>] [--tab=<下标>] [--env K=V]...

默认抓的是**覆盖层自己的窗口**；`--win=` 改成按标题找窗口。

`--tab=N` 是抓设置面板的快捷方式，等价于
`--win="CKeyViewer 设置" --env=CKV_OPEN_SETTINGS=N`（标签页下标见 KvSettingsWindow.Tabs）。
靠 Debug 构建的环境变量钩子自动进页面，**不发任何模拟按键** ——
这台机器上 SendInput / SetCursorPos 都是空操作，光标永远停在屏幕正中。

`--desktop=<路径>` 会额外用 screenshot.py 抓一张整个屏幕 —— 游戏是**全屏**时
这张图就是「游戏画面 + 覆盖层」的真实合成效果，适合放文档；而 PrintWindow 那张
只有覆盖层自己（透明处为黑），适合核对渲染内容。两张一起看最靠谱。

为什么不用 screenshot.py 抓屏：用户可能正开着全屏游戏，抓屏只会拍到压在上面的
游戏画面（实测如此）。这里改成找到目标 HWND，用
PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT=2) 强制它把内容画到我们的 DC 上。

覆盖层是分层（WS_EX_LAYERED）窗口，PrintWindow 拿到的透明处是黑色，
而覆盖层文字默认白色 + 黑描边 —— 在纯黑底上正好清晰可读。

调试版用 CKV_SKIP_ADMIN=1 跳过管理员检查（该开关只在 Debug 构建里存在）。
"""
import ctypes
import ctypes.wintypes as wt
import os
import struct
import subprocess
import sys
import time
import zlib

ROOT = r"E:/dsh工作区/Keyviever"
APP = os.path.join(ROOT, "CKeyViewer", "bin", "Debug", "net10.0-windows")
LOG = os.path.join(APP, "ckv_error.log")

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32
user32.SetProcessDPIAware()

PW_RENDERFULLCONTENT = 2
GWL_EXSTYLE = -20
WS_EX_LAYERED = 0x00080000
WS_EX_TRANSPARENT = 0x00000020
WS_EX_NOACTIVATE = 0x08000000


class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [
        ("biSize", wt.DWORD), ("biWidth", ctypes.c_long), ("biHeight", ctypes.c_long),
        ("biPlanes", wt.WORD), ("biBitCount", wt.WORD), ("biCompression", wt.DWORD),
        ("biSizeImage", wt.DWORD), ("biXPelsPerMeter", ctypes.c_long),
        ("biYPelsPerMeter", ctypes.c_long), ("biClrUsed", wt.DWORD), ("biClrImportant", wt.DWORD),
    ]


class BITMAPINFO(ctypes.Structure):
    _fields_ = [("bmiHeader", BITMAPINFOHEADER), ("bmiColors", wt.DWORD * 3)]


def find_by_title(pid, needle):
    """在该进程的顶层窗口里按标题找（标题含 needle）。窗口可能还没 CreateWindow 完，
    所以给一个宽限期轮询 —— 设置面板是启动后几秒才弹出来的。"""
    deadline = time.time() + 20.0
    while time.time() < deadline:
        hit = [None]
        EnumProc = ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)

        def cb(hwnd, lparam):
            wpid = wt.DWORD()
            user32.GetWindowThreadProcessId(hwnd, ctypes.byref(wpid))
            if wpid.value != pid:
                return True
            n = user32.GetWindowTextLengthW(hwnd)
            if n <= 0:
                return True
            buf = ctypes.create_unicode_buffer(n + 1)
            user32.GetWindowTextW(hwnd, buf, n + 1)
            if needle in buf.value:
                r = wt.RECT()
                user32.GetWindowRect(hwnd, ctypes.byref(r))
                hit[0] = (0, hwnd, r.left, r.top,
                          r.right - r.left, r.bottom - r.top,
                          user32.GetWindowLongW(hwnd, GWL_EXSTYLE))
                return False
            return True

        user32.EnumWindows(EnumProc(cb), 0)
        if hit[0]:
            return hit[0]
        time.sleep(0.25)
    return None


def find_overlay(pid):
    """在该进程的顶层窗口里挑覆盖层：分层 + 无激活 + 尺寸最大的那个。"""
    best = [None]
    EnumProc = ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)

    def cb(hwnd, lparam):
        wpid = wt.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(wpid))
        if wpid.value != pid:
            return True
        ex = user32.GetWindowLongW(hwnd, GWL_EXSTYLE)
        if not (ex & WS_EX_LAYERED):
            return True
        r = wt.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(r))
        area = (r.right - r.left) * (r.bottom - r.top)
        if best[0] is None or area > best[0][0]:
            best[0] = (area, hwnd, r.left, r.top, r.right - r.left, r.bottom - r.top, ex)
        return True

    user32.EnumWindows(EnumProc(cb), 0)
    return best[0]


def print_window(hwnd, w, h):
    hdc = user32.GetWindowDC(hwnd)
    memdc = gdi32.CreateCompatibleDC(hdc)
    bmp = gdi32.CreateCompatibleBitmap(hdc, w, h)
    gdi32.SelectObject(memdc, bmp)

    ok = user32.PrintWindow(hwnd, memdc, PW_RENDERFULLCONTENT)

    bi = BITMAPINFO()
    bi.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
    bi.bmiHeader.biWidth = w
    bi.bmiHeader.biHeight = -h
    bi.bmiHeader.biPlanes = 1
    bi.bmiHeader.biBitCount = 32
    bi.bmiHeader.biCompression = 0

    buf = ctypes.create_string_buffer(w * h * 4)
    gdi32.GetDIBits(memdc, bmp, 0, h, buf, ctypes.byref(bi), 0)

    gdi32.DeleteObject(bmp)
    gdi32.DeleteDC(memdc)
    user32.ReleaseDC(hwnd, hdc)
    return ok, buf.raw


def write_png(path, w, h, bgra, zoom=1):
    def rows():
        stride = w * 4
        for row in range(h):
            line = bgra[row * stride:(row + 1) * stride]
            yield b"".join(
                line[i + 2:i + 3] + line[i + 1:i + 2] + line[i:i + 1] + line[i + 3:i + 4]
                for i in range(0, stride, 4)
            )

    out_w, out_h = w * zoom, h * zoom
    raw = bytearray()
    for packed in rows():
        if zoom > 1:
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
    out = sys.argv[1] if len(sys.argv) > 1 else "overlay.png"
    wait = float(sys.argv[2]) if len(sys.argv) > 2 and not sys.argv[2].startswith("--") else 8.0
    no_kill = "--no-kill" in sys.argv
    desktop = None
    win = None
    tab = None
    envs = {}
    for a in sys.argv:
        if a.startswith("--desktop="):
            desktop = a.split("=", 1)[1]
        elif a.startswith("--win="):
            win = a.split("=", 1)[1]
        elif a.startswith("--tab="):
            tab = a.split("=", 1)[1]
        elif a.startswith("--env="):
            k, _, v = a[6:].partition("=")
            envs[k] = v

    if tab is not None:
        win = win or "CKeyViewer 设置"
        envs.setdefault("CKV_OPEN_SETTINGS", tab)

    try:
        os.remove(LOG)
    except OSError:
        pass

    env = dict(os.environ)
    env["CKV_SKIP_ADMIN"] = "1"
    env.update(envs)
    if envs:
        print("env: %s" % ", ".join("%s=%s" % kv for kv in sorted(envs.items())))

    app = subprocess.Popen(["dotnet", "CKeyViewer.dll"], cwd=APP, env=env,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    print("pid=%d waiting %.1fs ..." % (app.pid, wait))
    try:
        time.sleep(wait)

        found = find_by_title(app.pid, win) if win else find_overlay(app.pid)
        if not found:
            print("没找到窗口（%s）" % ("title~" + win if win else "覆盖层"))
            return 2

        area, hwnd, x, y, w, h, ex = found
        print("window hwnd=0x%X rect=%d,%d %dx%d ex=0x%08X" % (hwnd, x, y, w, h, ex & 0xFFFFFFFF))

        ok, data = print_window(hwnd, w, h)
        # 统计非黑像素，确认确实画了东西（分层窗口拿不到 alpha，只能这样看）
        nonblack = 0
        for i in range(0, len(data), 4 * 97):        # 抽样即可
            if data[i] or data[i + 1] or data[i + 2]:
                nonblack += 1

        write_png(out, w, h, data)
        print("PrintWindow=%s saved %s %dx%d sampled_nonblack=%d" % (ok, out, w, h, nonblack))

        if desktop:
            r = subprocess.run([sys.executable, os.path.join(ROOT, "tools", "screenshot.py"), desktop],
                               cwd=APP, capture_output=True)
            print((r.stdout + r.stderr).decode("utf-8", "ignore").strip())

        try:
            log = open(LOG, encoding="utf-8", errors="ignore").read()
            print("---- log tail ----")
            print("\n".join(log.splitlines()[-25:]))
        except OSError:
            print("(no log)")
        return 0
    finally:
        if not no_kill:
            try:
                subprocess.run(["taskkill", "/F", "/PID", str(app.pid)], capture_output=True)
            except Exception:
                pass


if __name__ == "__main__":
    sys.exit(main())
