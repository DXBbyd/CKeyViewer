"""发送一个组合键。用法: python sendchord.py ctrl+alt+s

纯 ctypes 调用 keybd_event，VK 走真实扫描码，避免顺序错乱。
"""
import ctypes
import sys
import time

user32 = ctypes.windll.user32
KEYEVENTF_KEYUP = 0x0002

VK = {
    "ctrl": 0x11, "control": 0x11, "alt": 0x12, "shift": 0x10, "win": 0x5B,
    "esc": 0x1B, "enter": 0x0D, "tab": 0x09, "space": 0x20,
    "left": 0x25, "up": 0x26, "right": 0x27, "down": 0x28,
}
for i in range(1, 13):
    VK["f%d" % i] = 0x6F + i
for c in "abcdefghijklmnopqrstuvwxyz":
    VK[c] = ord(c.upper())
for d in "0123456789":
    VK[d] = ord(d)


def vk_of(name):
    n = name.strip().lower()
    if n in VK:
        return VK[n]
    raise SystemExit("unknown key: " + name)


def press(vk):
    sc = user32.MapVirtualKeyW(vk, 0)
    user32.keybd_event(vk, sc, 0, 0)


def release(vk):
    sc = user32.MapVirtualKeyW(vk, 0)
    user32.keybd_event(vk, sc, KEYEVENTF_KEYUP, 0)


def main():
    if len(sys.argv) < 2:
        raise SystemExit("usage: sendchord.py ctrl+alt+s")
    mods = []
    key = None
    for part in sys.argv[1].split("+"):
        v = vk_of(part)
        key = v  # 最后一个当作主键
        mods.append(v)

    for v in mods[:-1]:
        press(v)
        time.sleep(0.03)
    press(key)
    time.sleep(0.05)
    release(key)
    for v in reversed(mods[:-1]):
        release(v)
    print("sent", sys.argv[1])


if __name__ == "__main__":
    main()
