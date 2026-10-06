"""生成 README 用的文档截图。

流程：启动 KV -> 显示桌面 -> 制造按键活动 -> 按日志里的 win32 rect 精确裁剪。
产出 docs/screenshots/01-overlay.png 与 05-kps-live.png。

用法: python docshot.py [padx] [padtop] [padbottom]
"""
import os
import re
import subprocess
import sys
import time

ROOT = r"E:/dsh工作区/Keyviever"
APP = os.path.join(ROOT, "CKeyViewer", "bin", "Debug", "net10.0-windows")
TOOLS = os.path.join(ROOT, "tools")
OUT = os.path.join(ROOT, "docs", "screenshots")
PY = sys.executable

RECT_RE = re.compile(r"win32 rect=\((-?\d+),(-?\d+)\)-\((-?\d+),(-?\d+)\)")


def script(name, *args):
    return [PY, os.path.join(TOOLS, name)] + [str(a) for a in args]


def run(name, *args):
    r = subprocess.run(script(name, *args), cwd=APP, capture_output=True, text=True)
    return (r.stdout + r.stderr).strip()


def spawn(name, *args):
    return subprocess.Popen(script(name, *args), cwd=APP,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def read_rect():
    path = os.path.join(APP, "ckv_error.log")
    try:
        with open(path, encoding="utf-8", errors="ignore") as f:
            log = f.read()
    except OSError:
        return None
    hits = RECT_RE.findall(log)
    return tuple(int(v) for v in hits[-1]) if hits else None


def main():
    os.makedirs(OUT, exist_ok=True)
    padx = int(sys.argv[1]) if len(sys.argv) > 1 else 26
    padt = int(sys.argv[2]) if len(sys.argv) > 2 else 26
    padb = int(sys.argv[3]) if len(sys.argv) > 3 else 26

    # 清掉旧日志，保证读到的 rect 一定来自本次运行
    try:
        os.remove(os.path.join(APP, "ckv_error.log"))
    except OSError:
        pass

    run("unlock.py")
    app = subprocess.Popen(["dotnet", "CKeyViewer.dll"], cwd=APP,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    workers = []
    try:
        time.sleep(8)
        rect = read_rect()
        if rect is None:
            print("!! 日志里没找到 win32 rect，无法裁剪")
            return 1
        L, T, R, B = rect
        x = max(0, L - padx)
        y = max(0, T - padt)
        w = (R - L) + padx * 2
        h = (B - T) + padt + padb
        print("overlay rect=%s -> crop x=%d y=%d w=%d h=%d" % (rect, x, y, w, h))

        # 先露出干净壁纸。必须排在按键之前 —— Win+D 里的 D 会顺手
        # 把 press.py 正按住的 d 释放掉。
        print(run("showdesktop.py"))
        time.sleep(1.2)

        # ---- 图 1：按住 A/S/D，展示按下高亮 + 雨线 + 每键计数 ----
        workers = [spawn("press.py", k, dur) for k, dur in (("a", 14), ("s", 12), ("d", 10))]
        time.sleep(2.5)
        print(run("screenshot.py", os.path.join(OUT, "01-overlay.png"), x, y, w, h))
        for p in workers:
            p.terminate()
        workers = []
        time.sleep(0.5)

        # ---- 图 2：连打若干键，展示 KPS 实时统计 ----
        workers = [spawn("tap.py", "a", "s", "d", "j", "k", "10")]
        time.sleep(3.0)
        print(run("screenshot.py", os.path.join(OUT, "05-kps-live.png"), x, y, w, h))
    finally:
        for p in workers:
            p.terminate()
        app.terminate()
        time.sleep(0.8)
    return 0


if __name__ == "__main__":
    sys.exit(main())
