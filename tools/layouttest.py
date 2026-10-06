"""自由布局 + 布局模式（拖动）的端到端验证。

流程：启动 -> 显示桌面 -> 造按键计数 -> 截图 -> Ctrl+Alt+L 进布局模式
      -> 截图 -> 拖动节点 A -> 截图 -> Esc 退出 -> 截图 -> 校验配置里的 X/Y

用法: python layouttest.py
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
PROFILE = os.path.join(APP, "config", "profiles", "Default.json")
PY = sys.executable

GEOM_RE = re.compile(r"ref->dip=([0-9.]+) custom=\S+ dpi=([0-9.]+)")
HOTKEY_RE = re.compile(r"\| ([^/]+)/ ([^/]+)/ ([^/]+)/ ([^/]+)/ (\S+)")


def script(name, *args):
    return [PY, os.path.join(TOOLS, name)] + [str(a) for a in args]


def run(name, *args):
    r = subprocess.run(script(name, *args), cwd=APP, capture_output=True, text=True)
    return (r.stdout + r.stderr).strip()


def spawn(name, *args):
    return subprocess.Popen(script(name, *args), cwd=APP,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def shot(out, *crop):
    print(run("screenshot.py", out, *crop))


def read_log():
    path = os.path.join(APP, "ckv_error.log")
    try:
        return io.open(path, encoding="utf-8", errors="ignore").read()
    except OSError:
        return ""


def load_profile():
    return json.load(io.open(PROFILE, encoding="utf-8-sig"))


def main():
    print("=== 备份档里的节点 ===")
    prof = load_profile()
    nodes = {n["Id"]: n for n in prof.get("CustomNodes", [])}
    for nid, n in sorted(nodes.items()):
        print("  #%d type=%d key=%s X=%.0f Y=%.0f W=%.0f H=%.0f count=%d"
              % (nid, n["NodeType"], n.get("KeyBind", ""), n["X"], n["Y"],
                 n["Width"], n["Height"], n.get("Count", 0)))

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
        log = read_log()
        m = GEOM_RE.search(log)
        if not m:
            print("!! 日志里没找到 geom，退出。日志尾部：")
            print(log[-1500:])
            return 1

        scale, dpi = float(m.group(1)), float(m.group(2))
        px = scale * dpi          # 参考单位 -> 屏幕物理像素
        print("scale=%s dpi=%s  px/unit=%s" % (scale, dpi, px))

        # 布局模式热键可能被占用而退到备选键，直接从日志里读出实际生效的组合
        m2 = HOTKEY_RE.search(log)
        if not m2:
            print("!! 读不到热键注册结果"); return 1
        chord = m2.group(5).strip().lower()
        print("布局模式热键 = %s" % chord)

        print(run("showdesktop.py"))
        time.sleep(1.0)

        # ---- 造按键活动，让 A/S/D/F 的计数看得见 ----
        workers = [spawn("press.py", k, 16) for k in ("a", "s", "d", "f")]
        time.sleep(3.2)

        # 节点区域：X 300..1320 / Y 150..526
        x0 = int(280 * px); y0 = int(60 * px)
        x1 = int(1360 * px); y1 = int(560 * px)
        print("节点区域 crop=(%d,%d)-(%d,%d)" % (x0, y0, x1, y1))
        shot(os.path.join(TOOLS, "_lt1_counts.png"), x0, y0, x1 - x0, y1 - y0, 1)
        shot(os.path.join(TOOLS, "_lt1_full.png"))

        # ---- 进入布局模式 ----
        print(run("sendchord.py", chord))
        time.sleep(1.2)
        shot(os.path.join(TOOLS, "_lt2_layoutmode.png"), 0, 0, 1920, 1080)

        # ---- 拖动 #1（A）：从节点中心往右下拖 ----
        a = nodes[1]
        cx = int((a["X"] + a["Width"] / 2) * px)
        cy = int((a["Y"] + a["Height"] / 2) * px)
        tx, ty = cx + int(120 * px), cy + int(220 * px)
        print(run("drag.py", cx, cy, tx, ty, 24, 300))
        time.sleep(0.8)
        shot(os.path.join(TOOLS, "_lt3_dragged.png"), 0, 0, 1920, 1080)

        # ---- Esc 退出布局模式 ----
        print(run("sendchord.py", "esc"))
        time.sleep(1.2)
        shot(os.path.join(TOOLS, "_lt4_afteresc.png"), x0, y0, x1 - x0, y1 - y0, 1)

        log = read_log()
        print("--- 日志尾部 ---")
        print("\n".join(log.strip().splitlines()[-26:]))

        after = load_profile()
        n1 = {n["Id"]: n for n in after.get("CustomNodes", [])}.get(1)
        if n1:
            print("=== 拖动后 #1 ===")
            print("  X=%.0f (原 %.0f)  Y=%.0f (原 %.0f)  count=%d"
                  % (n1["X"], a["X"], n1["Y"], a["Y"], n1.get("Count", 0)))
    finally:
        for p in workers:
            try: p.terminate()
            except Exception: pass
        time.sleep(0.4)
        # press.py 被强杀时来不及发 KEYUP，异步键状态会停在下按 —— 必须补一次释放
        run("keyup.py", "a", "s", "d", "f", "j", "k")
        app.terminate()
        time.sleep(1.0)
    return 0


if __name__ == "__main__":
    sys.exit(main())
