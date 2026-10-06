"""鼠标滚轮。用法: python scroll.py x y <格数>   （正数向下、负数向上）

坐标是屏幕物理像素。
"""
import ctypes
import sys
import time

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()

MOUSEEVENTF_WHEEL = 0x0800
WHEEL_DELTA = 120


def main():
    x, y = int(sys.argv[1]), int(sys.argv[2])
    n = int(sys.argv[3]) if len(sys.argv) > 3 else 3

    user32.SetCursorPos(x, y)
    time.sleep(0.15)
    step = -WHEEL_DELTA if n > 0 else WHEEL_DELTA
    for _ in range(abs(n)):
        user32.mouse_event(MOUSEEVENTF_WHEEL, 0, 0, step, 0)
        time.sleep(0.05)
    print("scrolled %d at %d,%d" % (n, x, y))


if __name__ == "__main__":
    main()
