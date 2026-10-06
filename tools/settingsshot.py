"""给设置面板的某个标签页拍图。

用法: python settingsshot.py <输出路径> [标签页下标] [裁剪形式 window|full]

标签页下标：0 档案 / 1 布局 / 2 自由布局 / 3 外观 / 4 文字 / 5 雨线 /
            6 按键绑定 / 7 每键配色 / 8 按压动画 / 9 统计 / 10 热键信息
"""
import io
import json
import os
import subprocess
import sys
import time

ROOT = r"E:/dsh工作区/Keyviever"
APP = os.path.join(ROOT, "CKeyViewer", "bin", "Debug", "net10.0-windows")
TOOLS = os.path.join(ROOT, "tools")
SETTINGS = os.path.join(APP, "config", "settings.json")
PY = sys.executable


def script(name, *args):
    return [PY, os.path.join(TOOLS, name)] + [str(a) for a in args]


def run(name, *args):
    r = subprocess.run(script(name, *args), cwd=APP, capture_output=True, text=True)
    return (r.stdout + r.stderr).strip()


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(TOOLS, "_settings.png")
    tab = int(sys.argv[2]) if len(sys.argv) > 2 else 2
    mode = sys.argv[3] if len(sys.argv) > 3 else "window"

    try:
        st = json.load(io.open(SETTINGS, encoding="utf-8-sig"))
    except Exception:
        st = {}
    st["UiTab"] = tab
    io.open(SETTINGS, "w", encoding="utf-8").write(json.dumps(st, ensure_ascii=False, indent=2))

    try:
        os.remove(os.path.join(APP, "ckv_error.log"))
    except OSError:
        pass

    run("unlock.py")
    app = subprocess.Popen(["dotnet", "CKeyViewer.dll"], cwd=APP,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        time.sleep(7)
        print(run("showdesktop.py"))
        time.sleep(0.8)
        print(run("sendchord.py", "ctrl+alt+s"))
        time.sleep(3.0)

        log = io.open(os.path.join(APP, "ckv_error.log"), encoding="utf-8", errors="ignore").read()
        print("\n".join(l for l in log.splitlines() if "settings" in l))

        if mode == "full":
            print(run("screenshot.py", out))
        else:
            # 设置窗口居中，940x660，多留一点边
            print(run("screenshot.py", out, 470, 180, 1000, 720))
    finally:
        app.terminate()
        time.sleep(1.0)
    return 0


if __name__ == "__main__":
    sys.exit(main())
