"""鼠标点击助手。用法: python click.py <x> <y>

坐标是屏幕物理像素。用于自动化验证 UI。
"""
import ctypes
import sys
import time

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()

MOUSEEVENTF_LEFTDOWN = 0x0002
MOUSEEVENTF_LEFTUP = 0x0004
MOUSEEVENTF_MOVE = 0x0001
MOUSEEVENTF_ABSOLUTE = 0x8000


def main():
    x, y = int(sys.argv[1]), int(sys.argv[2])
    user32.SetCursorPos(x, y)
    time.sleep(0.08)
    user32.mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
    time.sleep(0.05)
    user32.mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)
    print("clicked %d,%d" % (x, y))


if __name__ == "__main__":
    main()
