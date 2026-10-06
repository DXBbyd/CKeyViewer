"""按住一个键若干秒，然后松开。用法: python press.py a 5

用于在后台跑，同时截图观察按下状态。
"""
import ctypes
import sys
import time

user32 = ctypes.windll.user32
KEYEVENTF_KEYUP = 0x0002

VK = {}
for c in "abcdefghijklmnopqrstuvwxyz":
    VK[c] = ord(c.upper())
for d in "0123456789":
    VK[d] = ord(d)
VK.update({"space": 0x20, "enter": 0x0D, "esc": 0x1B, "tab": 0x09})


def main():
    name = sys.argv[1].lower()
    secs = float(sys.argv[2]) if len(sys.argv) > 2 else 3.0
    vk = VK[name]
    sc = user32.MapVirtualKeyW(vk, 0)

    user32.keybd_event(vk, sc, 0, 0)
    time.sleep(secs)
    user32.keybd_event(vk, sc, KEYEVENTF_KEYUP, 0)
    print("held %s for %.1fs" % (name, secs))


if __name__ == "__main__":
    main()
