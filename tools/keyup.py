"""强制释放若干按键。用法: python keyup.py a s d f

press.py / tap.py 被强行终止时来不及发 KEYUP，异步键状态会一直停在「按下」，
表现就是覆盖层上那颗键永远是按压色。用这个脚本解开。
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
VK.update({"space": 0x20, "enter": 0x0D, "esc": 0x1B, "tab": 0x09,
           "shift": 0x10, "ctrl": 0x11, "alt": 0x12})
for i in range(1, 13):
    VK["f%d" % i] = 0x6F + i


def main():
    names = sys.argv[1:] or list("abcdefghijklmnopqrstuvwxyz")
    done = []
    for n in names:
        vk = VK.get(n.lower())
        if vk is None:
            continue
        sc = user32.MapVirtualKeyW(vk, 0)
        user32.keybd_event(vk, sc, KEYEVENTF_KEYUP, 0)
        done.append(n)
        time.sleep(0.02)
    print("released " + " ".join(done))


if __name__ == "__main__":
    main()
