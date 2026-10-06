"""最小化所有窗口，露出桌面壁纸。

Win+M 是「最小化全部」而不是 Win+D 的「切换」，重复调用不会把窗口还回来，
所以比 showdesktop.py 更适合连续截图。
"""
import ctypes
import time

user32 = ctypes.windll.user32
VK_LWIN = 0x5B
VK_M = 0x4D
KEYUP = 0x0002


def main():
    user32.keybd_event(VK_LWIN, 0, 0, 0)
    user32.keybd_event(VK_M, 0, 0, 0)
    user32.keybd_event(VK_M, 0, KEYUP, 0)
    user32.keybd_event(VK_LWIN, 0, KEYUP, 0)
    time.sleep(0.5)
    print("sent Win+M (minimize all)")


if __name__ == "__main__":
    main()
