"""生成自由布局相关的文档截图 -> docs/screenshots/

07-custom-layout.png  自定义布局成品（每键配色 + 圆环节点 + 统计条 + 文字节点）
08-layout-mode.png    布局模式（参考网格 / 节点虚线框 / 选中高亮 / 顶部提示条）
09-custom-editor.png  设置面板「自由布局」标签页

截图前会铺一块自绘的深色背景窗口（tools/backdrop.py），
这样出来的图不依赖当时桌面上开着什么，随时可复现。

用法: python docshot2.py
"""
import io
import json
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

GEOM_RE = re.compile(r"ref->dip=([0-9.]+) custom=\S+ dpi=([0-9.]+)")
HOTKEY_RE = re.compile(r"\| ([^/]+)/ ([^/]+)/ ([^/]+)/ ([^/]+)/ (\S+)")
SETTINGS_RE = re.compile(r"settings window shown (\d+)x(\d+) @ (-?[\d.]+),(-?[\d.]+)")


def script(name, *args):
    return [PY, os.path.join(TOOLS, name)] + [str(a) for a in args]


def run(name, *args):
    r = subprocess.run(script(name, *args), cwd=APP, capture_output=True, text=True)
    return (r.stdout + r.stderr).strip()


def spawn(name, *args):
    return subprocess.Popen(script(name, *args), cwd=APP,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def read_log():
    try:
        return io.open(os.path.join(APP, "ckv_error.log"), encoding="utf-8", errors="ignore").read()
    except OSError:
        return ""


def main():
    os.makedirs(OUT, exist_ok=True)

    # 打开「自由布局」页，并确认方向键微调是关的
    try:
        sp = os.path.join(APP, "config", "settings.json")
        st = json.load(io.open(sp, encoding="utf-8-sig"))
        st["UiTab"] = 2
        st["ArrowNudge"] = False
        io.open(sp, "w", encoding="utf-8").write(json.dumps(st, ensure_ascii=False, indent=2))
    except Exception as ex:
        print("设置 settings.json 失败（忽略）:", ex)

    try:
        os.remove(os.path.join(APP, "ckv_error.log"))
    except OSError:
        pass

    run("unlock.py")
    app = None
    bd = None
    try:
        app = subprocess.Popen(["dotnet", "CKeyViewer.dll"], cwd=APP,
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        time.sleep(8)
        log = read_log()
        m = GEOM_RE.search(log)
        if not m:
            print("!! 读不到 geom"); return 1
        scale, dpi = float(m.group(1)), float(m.group(2))
        px = scale * dpi
        m2 = HOTKEY_RE.search(log)
        layout_chord = m2.group(5).strip().lower() if m2 else "ctrl+alt+f8"

        # ---- 铺一块深色背景，截图不再受桌面影响 ----
        bd = spawn("backdrop.py", 180)
        time.sleep(1.5)
        run("keyup.py")
        time.sleep(0.3)

        # ---- 07：不打键，纯展示每个节点自己的配色（红/蓝/绿 + 圆环 + 统计条） ----
        print(run("screenshot.py", os.path.join(OUT, "07-custom-layout.png"),
                  int(280 * px), int(60 * px), int(1090 * px), int(500 * px)))

        # ---- 08：进布局模式，展示网格 / 虚线框 / 选中高亮 / 提示条 ----
        print(run("sendchord.py", layout_chord))
        time.sleep(1.3)
        run("screenshot.py", os.path.join(TOOLS, "_lt_raw.png"))
        print(run("crop.py", os.path.join(TOOLS, "_lt_raw.png"),
                  os.path.join(OUT, "08-layout-mode.png"), 240, 0, 1030, 470, 1))
        print(run("sendchord.py", "esc"))
        time.sleep(0.9)

        # ---- 09：设置面板的「自由布局」页 ----
        print(run("sendchord.py", "ctrl+alt+s"))
        time.sleep(3.5)
        m3 = SETTINGS_RE.search(read_log())
        if m3:
            w, h, l, t = (float(m3.group(i)) for i in (1, 2, 3, 4))
            pad = 16
            print(run("screenshot.py", os.path.join(OUT, "09-custom-editor.png"),
                      int(l - pad), int(t - pad), int(w + pad * 2), int(h + pad * 2)))
        else:
            print("!! 读不到设置窗口位置")

        print(run("keyup.py"))
        print("--- 日志尾部 ---")
        print("\n".join(read_log().strip().splitlines()[-5:]))
    finally:
        run("keyup.py")
        if app is not None:
            app.terminate()
        time.sleep(0.8)
        if bd is not None:
            bd.terminate()
    return 0


if __name__ == "__main__":
    sys.exit(main())
