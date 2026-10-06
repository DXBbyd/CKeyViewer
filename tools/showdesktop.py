"""显示桌面 —— 截图前露出干净壁纸。

Win+D 是「切换」语义：桌面已经露出时再按会把所有窗口还原回来。
所以这里先判断前台窗口是不是桌面（Progman / WorkerW），是就跳过。
"""
import ctypes
import time

user32 = ctypes.windll.user32
VK_LWIN = 0x5B
VK_D = 0x44
KEYUP = 0x0002

DESKTOP_CLASSES = {"Progman", "WorkerW"}


def foreground_class():
    hwnd = user32.GetForegroundWindow()
    buf = ctypes.create_unicode_buffer(256)
    user32.GetClassNameW(hwnd, buf, 256)
    return buf.value


def main():
    cls = foreground_class()
    if cls in DESKTOP_CLASSES:
        print("already on desktop (%s), skip" % cls)
        return
    user32.keybd_event(VK_LWIN, 0, 0, 0)
    user32.keybd_event(VK_D, 0, 0, 0)
    user32.keybd_event(VK_D, 0, KEYUP, 0)
    user32.keybd_event(VK_LWIN, 0, KEYUP, 0)
    time.sleep(0.2)
    print("sent Win+D (was %s)" % cls)


if __name__ == "__main__":
    main()
