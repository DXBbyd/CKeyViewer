"""启动调试版 → 等待 → 读日志 →（可选）截图 → 结束自己拉起的进程。

用法:
    python tools/runapp.py <等待秒数> [截图输出路径] [--no-kill] [--env KEY=VAL]...

调试版用 CKV_SKIP_ADMIN=1 跳过管理员检查（该开关只在 Debug 构建里存在）。
之所以不用 shell 的 `&` 后台：宿主的工具调用结束时会回收整条命令链的子进程，
「启动 + 等待 + 截图」必须由同一个 Python 进程自己扛住。

`--env CKV_OPEN_SETTINGS=13` 可以直接把设置面板开到第 13 个标签页。
在这台机器上 **发模拟按键是没用的**（SendInput / SetCursorPos 是空操作，
光标永远停在屏幕正中），所以截图路径只能靠环境变量钩子，不能靠 sendchord.py。

要**只拍某一块窗口**（不看桌面），用 overlayshot.py，它自己拉进程也能传环境变量：
    python tools/overlayshot.py out.png 14 --win="CKeyViewer 设置" --env=CKV_OPEN_SETTINGS=13
"""
import io
import os
import subprocess
import sys
import time

ROOT = r"E:/dsh工作区/Keyviever"
APP = os.path.join(ROOT, "CKeyViewer", "bin", "Debug", "net10.0-windows")
TOOLS = os.path.join(ROOT, "tools")
LOG = os.path.join(APP, "ckv_error.log")
PY = sys.executable


def run(name, *args):
    r = subprocess.run([PY, os.path.join(TOOLS, name)] + [str(a) for a in args],
                       cwd=APP, capture_output=True)
    return (r.stdout + r.stderr).decode("utf-8", "ignore").strip()


def main():
    argv = sys.argv[1:]
    envs = {}
    win = None
    rest = []
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--env":
            i += 1
            k, _, v = argv[i].partition("=")
            envs[k] = v
        elif a.startswith("--env="):
            k, _, v = a[6:].partition("=")
            envs[k] = v
        elif a == "--win":
            i += 1
            win = argv[i]
        elif a.startswith("--win="):
            win = a[6:]
        else:
            rest.append(a)
        i += 1

    # 剩下的位置参数里还要把选项剔掉 —— 否则 `--no-kill` 会被当成截图路径。
    pos = [a for a in rest if not a.startswith("--")]
    wait = float(pos[0]) if len(pos) > 0 else 14.0
    shot = pos[1] if len(pos) > 1 else None
    no_kill = "--no-kill" in rest

    try:
        os.remove(LOG)
    except OSError:
        pass

    env = dict(os.environ)
    env["CKV_SKIP_ADMIN"] = "1"          # Debug 构建才认，Release 里这段被编译掉
    env.update(envs)

    app = subprocess.Popen(["dotnet", "CKeyViewer.dll"], cwd=APP, env=env,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    print("pid=%d  waiting %.1fs ..." % (app.pid, wait))
    if envs:
        print("env: %s" % ", ".join("%s=%s" % kv for kv in sorted(envs.items())))
    try:
        time.sleep(wait)

        if shot:
            print(run("screenshot.py", shot))

        try:
            log = io.open(LOG, encoding="utf-8", errors="ignore").read()
        except OSError:
            log = "(no log)"
        print("---- log ----")
        print(log.strip())
    finally:
        if not no_kill:
            try:
                subprocess.run(["taskkill", "/F", "/PID", str(app.pid)], capture_output=True)
            except Exception as e:
                print("taskkill: %s" % e)
            time.sleep(0.8)
            print("returncode = %s" % app.poll())
    return 0


if __name__ == "__main__":
    sys.exit(main())
