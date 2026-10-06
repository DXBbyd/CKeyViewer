"""唤醒显示器并尝试退出锁屏（ESC + Enter）。截图前调用，避免抓到黑屏/壁纸。"""
import ctypes
import time

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

ES_CONTINUOUS = 0x80000000
ES_SYSTEM_REQUIRED = 0x00000001
ES_DISPLAY_REQUIRED = 0x00000002
MOUSEEVENTF_MOVE = 0x0001
KEYEVENTF_KEYUP = 0x0002
VK_ESCAPE = 0x1B
VK_RETURN = 0x0D


def tap(vk):
    user32.keybd_event(vk, 0, 0, 0)
    time.sleep(0.04)
    user32.keybd_event(vk, 0, KEYEVENTF_KEYUP, 0)
    time.sleep(0.15)


def main():
    kernel32.SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED)
    user32.SetProcessDPIAware()

    # 鼠标微移，触发用户活动
    for _ in range(8):
        user32.mouse_event(MOUSEEVENTF_MOVE, 3, 3, 0, 0)
        time.sleep(0.015)
        user32.mouse_event(MOUSEEVENTF_MOVE, -3, -3, 0, 0)
        time.sleep(0.015)

    # 若处于锁屏，ESC + Enter 通常可直接回到桌面
    tap(VK_ESCAPE)
    tap(VK_RETURN)
    time.sleep(0.5)


if __name__ == "__main__":
    main()
    print("awake")
