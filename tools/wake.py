"""唤醒显示器（鼠标微移）+ 阻止休眠。"""
import ctypes
import time

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

ES_CONTINUOUS = 0x80000000
ES_SYSTEM_REQUIRED = 0x00000001
ES_DISPLAY_REQUIRED = 0x00000002
MOUSEEVENTF_MOVE = 0x0001


def wake():
    kernel32.SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED)
    pt = ctypes.wintypes.POINT() if hasattr(ctypes, "wintypes") else None
    # 连续几次微移，触发用户活动
    for _ in range(12):
        user32.mouse_event(MOUSEEVENTF_MOVE, 3, 3, 0, 0)
        time.sleep(0.02)
        user32.mouse_event(MOUSEEVENTF_MOVE, -3, -3, 0, 0)
        time.sleep(0.02)
    time.sleep(0.6)


if __name__ == "__main__":
    wake()
    print("awake")
