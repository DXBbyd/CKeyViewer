"""精确注入键鼠输入（SendInput），用于验证按键绑定。

用法:
  python sendinput.py probe                     # 列出当前处于按下状态的 VK
  python sendinput.py key <名字> [秒]            # 按住某键，默认 0.25s 后松开
  python sendinput.py mouse <left|right|middle|x1|x2> [秒]

key 支持 lshift/rshift/lctrl/rctrl/lalt/ralt/a..z/0..9/f1..f12/space/enter/tab/esc。

比 press.py 强的地方：能发**区分左右**的修饰键（VK_LSHIFT=0xA0 而不是泛用 0x10），
也能发鼠标左右中侧键 —— 覆盖层和绑定捕获都是按具体 VK 查的。
"""
import ctypes
import sys
import time
from ctypes import wintypes

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()

ULONG_PTR = ctypes.c_uint64


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", wintypes.WORD), ("wScan", wintypes.WORD),
                ("dwFlags", wintypes.DWORD), ("time", wintypes.DWORD),
                ("dwExtraInfo", ULONG_PTR)]


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", wintypes.LONG), ("dy", wintypes.LONG),
                ("mouseData", wintypes.DWORD), ("dwFlags", wintypes.DWORD),
                ("time", wintypes.DWORD), ("dwExtraInfo", ULONG_PTR)]


class HARDWAREINPUT(ctypes.Structure):
    _fields_ = [("uMsg", wintypes.DWORD), ("wParamL", wintypes.WORD),
                ("wParamH", wintypes.WORD)]


class _U(ctypes.Union):
    _fields_ = [("ki", KEYBDINPUT), ("mi", MOUSEINPUT), ("hi", HARDWAREINPUT)]


class INPUT(ctypes.Structure):
    _fields_ = [("type", wintypes.DWORD), ("u", _U)]


INPUT_KEYBOARD = 1
INPUT_MOUSE = 0
KEYEVENTF_KEYUP = 0x0002

MOUSEEVENTF = {
    "left":   (0x0002, 0x0004),
    "right":  (0x0008, 0x0010),
    "middle": (0x0020, 0x0040),
    "x1":     (0x0080, 0x0100),
    "x2":     (0x0080, 0x0100),
}

# 名字 → (wVk, 是否扩展键)。修饰键一律用区分左右的 VK，
# 这样 GetAsyncKeyState(VK_LSHIFT) 才会真的亮起来。
KEYS = {
    "lshift": (0xA0, False), "rshift": (0xA1, True),
    "lctrl": (0xA2, False), "rctrl": (0xA3, True),
    "lalt": (0xA4, False), "ralt": (0xA5, True),
    "lwin": (0x5B, True), "rwin": (0x5C, True),
    "space": (0x20, False), "enter": (0x0D, False),
    "tab": (0x09, False), "esc": (0x1B, False),
    "caps": (0x14, False),
}
for _c in "abcdefghijklmnopqrstuvwxyz":
    KEYS[_c] = (ord(_c.upper()), False)
for _d in "0123456789":
    KEYS[_d] = (ord(_d), False)
for _i in range(1, 13):
    KEYS["f%d" % _i] = (0x6F + _i, False)


def _send(inp):
    n = user32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))
    if n != 1:
        raise OSError("SendInput failed (err=%d)" % ctypes.get_last_error())


def key_down(vk, extended=False):
    inp = INPUT(type=INPUT_KEYBOARD)
    inp.u.ki = KEYBDINPUT(wVk=vk, wScan=user32.MapVirtualKeyW(vk, 0),
                          dwFlags=0x0001 if extended else 0, time=0, dwExtraInfo=0)
    _send(inp)


def key_up(vk, extended=False):
    inp = INPUT(type=INPUT_KEYBOARD)
    inp.u.ki = KEYBDINPUT(wVk=vk, wScan=user32.MapVirtualKeyW(vk, 0),
                          dwFlags=KEYEVENTF_KEYUP | (0x0001 if extended else 0),
                          time=0, dwExtraInfo=0)
    _send(inp)


def mouse_down(btn):
    down, _ = MOUSEEVENTF[btn]
    inp = INPUT(type=INPUT_MOUSE)
    inp.u.mi = MOUSEINPUT(dx=0, dy=0, mouseData=1 if btn == "x1" else (2 if btn == "x2" else 0),
                          dwFlags=down, time=0, dwExtraInfo=0)
    _send(inp)


def mouse_up(btn):
    _, up = MOUSEEVENTF[btn]
    inp = INPUT(type=INPUT_MOUSE)
    inp.u.mi = MOUSEINPUT(dx=0, dy=0, mouseData=1 if btn == "x1" else (2 if btn == "x2" else 0),
                          dwFlags=up, time=0, dwExtraInfo=0)
    _send(inp)


def down_vks():
    out = []
    for vk in range(0x01, 0xFF):
        if user32.GetAsyncKeyState(vk) & 0x8000:
            out.append(vk)
    return out


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1

    cmd = sys.argv[1].lower()

    if cmd == "probe":
        vks = down_vks()
        print("down vks: " + (" ".join("0x%02X" % v for v in vks) if vks else "(none)"))
        return 0

    if cmd == "key":
        name = sys.argv[2].lower()
        secs = float(sys.argv[3]) if len(sys.argv) > 3 else 0.25
        vk, ext = KEYS[name]
        key_down(vk, ext)
        time.sleep(secs)
        key_up(vk, ext)
        print("pressed %s (vk=0x%02X) for %.2fs" % (name, vk, secs))
        return 0

    if cmd == "mouse":
        btn = sys.argv[2].lower()
        secs = float(sys.argv[3]) if len(sys.argv) > 3 else 0.25
        mouse_down(btn)
        time.sleep(secs)
        mouse_up(btn)
        print("pressed mouse %s for %.2fs" % (btn, secs))
        return 0

    if cmd == "keydown":
        vk, ext = KEYS[sys.argv[2].lower()]
        key_down(vk, ext)
        print("down %s" % sys.argv[2])
        return 0

    if cmd == "keyup":
        vk, ext = KEYS[sys.argv[2].lower()]
        key_up(vk, ext)
        print("up %s" % sys.argv[2])
        return 0

    if cmd == "mousedown":
        mouse_down(sys.argv[2].lower())
        print("down mouse %s" % sys.argv[2])
        return 0

    if cmd == "mouseup":
        mouse_up(sys.argv[2].lower())
        print("up mouse %s" % sys.argv[2])
        return 0

    print(__doc__)
    return 1


if __name__ == "__main__":
    sys.exit(main())
