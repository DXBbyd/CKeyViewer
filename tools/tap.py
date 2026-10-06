"""快速连打若干键，用于验证 KPS 统计。

用法: python tap.py <按键...> <持续秒数>
例:   python tap.py a s d j k 3.0
"""
import ctypes
import sys
import time

user32 = ctypes.windll.user32
KEYUP = 0x0002


def main():
    names = sys.argv[1:-1]
    dur = float(sys.argv[-1])
    t0 = time.time()
    rounds = 0
    while time.time() - t0 < dur:
        for n in names:
            v = ord(n.upper())
            sc = user32.MapVirtualKeyW(v, 0)
            user32.keybd_event(v, sc, 0, 0)
            time.sleep(0.012)
            user32.keybd_event(v, sc, KEYUP, 0)
            time.sleep(0.012)
        rounds += 1
    print("tapped %d rounds x %d keys" % (rounds, len(names)))


if __name__ == "__main__":
    main()
