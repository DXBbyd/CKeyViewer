"""模拟一次鼠标拖动（或点击）。用法: python drag.py x1 y1 x2 y2 [steps] [press_ms]

坐标是屏幕物理像素。若 x2 y2 与 x1 y1 相同，则退化为一次点击。
纯 ctypes SendInput，无第三方依赖。
"""
import ctypes
import ctypes.wintypes as wt
import sys
import time

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()

SM_CXSCREEN = 0
SM_CYSCREEN = 1

INPUT_MOUSE = 0
MOUSEEVENTF_MOVE = 0x0001
MOUSEEVENTF_LEFTDOWN = 0x0002
MOUSEEVENTF_LEFTUP = 0x0004
MOUSEEVENTF_ABSOLUTE = 0x8000
MOUSEEVENTF_VIRTUALDESK = 0x4000


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", ctypes.c_long), ("dy", ctypes.c_long),
                ("mouseData", wt.DWORD), ("dwFlags", wt.DWORD),
                ("time", wt.DWORD), ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong))]


class _INPUTunion(ctypes.Union):
    _fields_ = [("mi", MOUSEINPUT)]


class INPUT(ctypes.Structure):
    _fields_ = [("type", wt.DWORD), ("u", _INPUTunion)]


def _send(flags, dx, dy):
    inp = INPUT(type=INPUT_MOUSE)
    inp.u.mi = MOUSEINPUT(dx, dy, 0, flags, 0, None)
    user32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))


def norm(x, y):
    """屏幕物理像素 -> 绝对坐标（0..65535）。"""
    vx = user32.GetSystemMetrics(76)      # SM_XVIRTUALSCREEN
    vy = user32.GetSystemMetrics(77)
    vw = user32.GetSystemMetrics(78)
    vh = user32.GetSystemMetrics(79)
    if vw <= 1:
        vw, vh, vx, vy = user32.GetSystemMetrics(SM_CXSCREEN), user32.GetSystemMetrics(SM_CYSCREEN), 0, 0
    return (int(round((x - vx) * 65535.0 / (vw - 1))),
            int(round((y - vy) * 65535.0 / (vh - 1))))


def move_to(x, y):
    nx, ny = norm(x, y)
    _send(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, nx, ny)


def main():
    if len(sys.argv) < 5:
        raise SystemExit("usage: drag.py x1 y1 x2 y2 [steps] [press_ms]")
    x1, y1, x2, y2 = (int(v) for v in sys.argv[1:5])
    steps = int(sys.argv[5]) if len(sys.argv) > 5 else 24
    press_ms = int(sys.argv[6]) if len(sys.argv) > 6 else 300

    move_to(x1, y1)
    time.sleep(0.35)                 # 等覆盖层把穿透关掉

    _send(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, *norm(x1, y1))
    time.sleep(press_ms / 1000.0)

    for i in range(1, steps + 1):
        t = i / float(steps)
        move_to(int(round(x1 + (x2 - x1) * t)), int(round(y1 + (y2 - y1) * t)))
        time.sleep(0.02)

    time.sleep(0.15)
    _send(MOUSEEVENTF_LEFTUP | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, *norm(x2, y2))
    print("dragged (%d,%d) -> (%d,%d) in %d steps" % (x1, y1, x2, y2, steps))


if __name__ == "__main__":
    main()
