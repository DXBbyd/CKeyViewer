"""验证「非管理员 → 弹窗 + 终止进程」。

用法: python admintest.py

故意**不发送任何键鼠输入**：弹窗是模态的，靠发送 Enter 去关会打扰用户
（上一次就是这么抢进用户会话的）。这里只枚举窗口确认弹窗出现、截图留证，
然后直接结束自己拉起来的进程。
"""
import ctypes
import ctypes.wintypes as wt
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

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()

EnumWindowsProc = ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)


def windows():
    out = []

    def cb(hwnd, _):
        n = user32.GetWindowTextLengthW(hwnd)
        if n:
            buf = ctypes.create_unicode_buffer(n + 1)
            user32.GetWindowTextW(hwnd, buf, n + 1)
            cls = ctypes.create_unicode_buffer(256)
            user32.GetClassNameW(hwnd, cls, 256)
            rect = wt.RECT()
            user32.GetWindowRect(hwnd, ctypes.byref(rect))
            out.append((hwnd, buf.value, cls.value, rect, user32.IsWindowVisible(hwnd)))
        return True

    user32.EnumWindows(EnumWindowsProc(cb), 0)
    return out


def run(name, *args):
    r = subprocess.run([PY, os.path.join(TOOLS, name)] + [str(a) for a in args],
                       cwd=APP, capture_output=True, text=True)
    return (r.stdout + r.stderr).strip()


def main():
    for p in (LOG, os.path.join(TOOLS, "_admin.png")):
        try:
            os.remove(p)
        except OSError:
            pass

    app = subprocess.Popen(["dotnet", "CKeyViewer.dll"], cwd=APP,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    pid = app.pid
    hit = None
    try:
        # 从启动起密集轮询：弹窗可能只存在一两秒，5 秒后再看就没了
        deadline = time.time() + 8.0
        while time.time() < deadline:
            dlg = [w for w in windows() if w[2] == "#32770"]
            if dlg:
                hit = dlg[0]
                break
            if app.poll() is not None:
                break
            time.sleep(0.1)

        if hit:
            hwnd, title, cls, rect, vis = hit
            print("DIALOG FOUND hwnd=0x%X title=%r cls=%s rect=%s visible=%s"
                  % (hwnd, title, cls, (rect.left, rect.top, rect.right, rect.bottom), vis))

            def child_texts(h):
                texts = []

                def cb(ch, _):
                    n = user32.GetWindowTextLengthW(ch)
                    if n:
                        b = ctypes.create_unicode_buffer(n + 1)
                        user32.GetWindowTextW(ch, b, n + 1)
                        texts.append(b.value)
                    return True

                user32.EnumChildWindows(h, EnumWindowsProc(cb), 0)
                return texts

            print("  title   :", title)
            print("  children:", child_texts(hwnd))
            # 按对话框矩形精确裁剪，顺便留一份到文档截图目录
            doc = os.path.join(ROOT, "docs", "screenshots", "10-admin-required.png")
            print(run("screenshot.py", doc,
                      rect.left, rect.top,
                      rect.right - rect.left, rect.bottom - rect.top))
            print(run("screenshot.py", os.path.join(TOOLS, "_admin.png"),
                      rect.left, rect.top,
                      rect.right - rect.left, rect.bottom - rect.top))
            print("process alive while dialog up = %s" % (app.poll() is None))
        else:
            print("NO DIALOG (process returncode=%s)" % app.poll())
    finally:
        try:
            subprocess.run(["taskkill", "/F", "/PID", str(pid)], capture_output=True)
        except Exception as e:
            print("taskkill: %s" % e)
        time.sleep(0.8)

    print("process returncode = %s" % app.poll())
    try:
        log = io.open(LOG, encoding="utf-8", errors="ignore").read()
    except OSError:
        log = "(no log)"
    print("---- log ----")
    print(log.strip())
    return 0


if __name__ == "__main__":
    sys.exit(main())
