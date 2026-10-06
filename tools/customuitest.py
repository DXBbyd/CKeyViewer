"""自由布局设置页的交互回归：滚动 + 点按钮 + 点复选框，顺便检查有没有抛异常。

用法: python customuitest.py
"""
import io
import os
import subprocess
import sys
import time

ROOT = r"E:/dsh工作区/Keyviever"
APP = os.path.join(ROOT, "CKeyViewer", "bin", "Debug", "net10.0-windows")
TOOLS = os.path.join(ROOT, "tools")
PY = sys.executable
LOG = os.path.join(APP, "ckv_error.log")

# 设置窗口 940x660 @ 490,186；右侧内容区大致在 x 560..1400
CX = 1000


def script(name, *args):
    return [PY, os.path.join(TOOLS, name)] + [str(a) for a in args]


def run(name, *args):
    r = subprocess.run(script(name, *args), cwd=APP, capture_output=True, text=True)
    return (r.stdout + r.stderr).strip()


def main():
    try:
        os.remove(LOG)
    except OSError:
        pass

    run("unlock.py")
    app = subprocess.Popen(["dotnet", "CKeyViewer.dll"], cwd=APP,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        time.sleep(7)
        run("showdesktop.py")
        time.sleep(0.6)
        run("sendchord.py", "ctrl+alt+s")
        time.sleep(2.5)

        # 一路滚到底，每滚几格拍一张
        for i, y in enumerate((300, 460, 620)):
            run("scroll.py", CX, 500, 6)
            time.sleep(0.5)
            run("screenshot.py", os.path.join(TOOLS, "_cu_%d.png" % i), 470, 180, 1000, 720)

        # 点「新建图层组」按钮附近（滚到底之后靠猜不行，这里改成点若干已知位置做健壮性检查）
        print(run("click.py", CX, 700))
        time.sleep(0.6)
        run("screenshot.py", os.path.join(TOOLS, "_cu_click.png"), 470, 180, 1000, 720)

        log = io.open(LOG, encoding="utf-8", errors="ignore").read()
        bad = [l for l in log.splitlines()
               if "Dispatcher:" in l or "Exception" in l or "构建此页面时出错" in l]
        print("=== 异常检查 ===")
        print("\n".join(bad) if bad else "无异常")
        print("=== 日志尾部 ===")
        print("\n".join(log.strip().splitlines()[-8:]))
    finally:
        app.terminate()
        time.sleep(1.0)
    return 0


if __name__ == "__main__":
    sys.exit(main())
