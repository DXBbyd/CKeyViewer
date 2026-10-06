"""给设置面板的某个标签页拍图（Debug 构建 + PrintWindow）。

用法: python appshot.py <输出 png> [标签页下标]

标签页：0 档案 / 1 布局 / 2 自由布局 / 3 外观 / 4 文字 / 5 雨线 /
        6 按键绑定 / 7 每键配色 / 8 按压动画 / 9 统计 / 10 热键信息 / 11 关于

靠 Debug 构建里的两个开关自动进页面：
  CKV_SKIP_ADMIN=1     跳过管理员检查（本机调试未必有管理员权限）
  CKV_OPEN_SETTINGS=n  启动 N 秒后自动把设置面板打开到第 n 页
**不发任何模拟按键** —— 上一次用 sendchord 抢进了用户正在用的键盘。
"""
import ctypes
import os
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import setupshot as S  # noqa: E402  复用 find_window / print_window / RECT

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
APP = os.path.join(ROOT, "CKeyViewer", "bin", "Debug", "net10.0-windows")


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "tools", "_tab.png")
    tab = sys.argv[2] if len(sys.argv) > 2 else "11"

    env = dict(os.environ)
    env["CKV_SKIP_ADMIN"] = "1"
    env["CKV_OPEN_SETTINGS"] = str(tab)

    proc = subprocess.Popen(["dotnet", "CKeyViewer.dll"], cwd=APP, env=env,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        win = S.find_window("CKeyViewer 设置", timeout=45)
        if not win:
            print("找不到设置窗口")
            return 1
        hwnd, title = win
        time.sleep(2.5)          # 等这一页画完

        r = S.RECT()
        S.user32.GetWindowRect(hwnd, ctypes.byref(r))
        w, h = r.right - r.left, r.bottom - r.top
        print("window '%s' hwnd=0x%X rect=%d,%d %dx%d" % (title, hwnd, r.left, r.top, w, h))

        data = S.print_window(hwnd, w, h)
        if not any(data):
            print("PrintWindow 返回空白")
            return 1
        S.screenshot.write_png(out, w, h, data)
        print("saved", out)
        return 0
    finally:
        try:
            proc.kill()
        except Exception:
            pass


if __name__ == "__main__":
    sys.exit(main())
