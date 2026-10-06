"""启动调试版 → 等待 → 读日志 →（可选）截图 → 结束自己拉起的进程。

用法:
    python tools/runapp.py <等待秒数> [截图输出路径] [--no-kill]

调试版用 CKV_SKIP_ADMIN=1 跳过管理员检查（该开关只在 Debug 构建里存在）。
之所以不用 shell 的 `&` 后台：宿主的工具调用结束时会回收整条命令链的子进程，
「启动 + 等待 + 截图」必须由同一个 Python 进程自己扛住。
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
    wait = float(sys.argv[1]) if len(sys.argv) > 1 else 14.0
    shot = sys.argv[2] if len(sys.argv) > 2 and not sys.argv[2].startswith("--") else None
    no_kill = "--no-kill" in sys.argv

    try:
        os.remove(LOG)
    except OSError:
        pass

    env = dict(os.environ)
    env["CKV_SKIP_ADMIN"] = "1"          # Debug 构建才认，Release 里这段被编译掉

    app = subprocess.Popen(["dotnet", "CKeyViewer.dll"], cwd=APP, env=env,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    print("pid=%d  waiting %.1fs ..." % (app.pid, wait))
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
