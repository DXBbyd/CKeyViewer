"""把发布资产整理到 release/ 下，供 GitHub Release 上传。

用法: python tools/build_release.py

产物：
  release/CKeyViewer-<版本>-portable-win64.zip   完整程序（便携版，解压即用）
  release/CKeyViewerSetup-<版本>.exe             安装程序
  release/SHA256SUMS.txt                         两个文件的校验和

前置：先跑 tools/build_setup.sh 把 release/app 与 release/setup 产出来。
"""
import hashlib
import io
import os
import shutil
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VERSION = "1.0.0"

APP_EXE = os.path.join(ROOT, "release", "app", "CKeyViewer.exe")
SETUP_EXE = os.path.join(ROOT, "release", "setup", "CKeyViewerSetup.exe")

ZIP_NAME = "CKeyViewer-%s-portable-win64.zip" % VERSION
SETUP_NAME = "CKeyViewerSetup-%s.exe" % VERSION

README_TXT = u"""CKeyViewer 按键可视化覆盖层
版本 {version}
作者 DXBbyd   QQ 3157037483
仓库 https://github.com/DXBbyd/CKeyViewer

────────────────────────────────────────────
怎么用
────────────────────────────────────────────

1. 这个目录里的 CKeyViewer.exe 就是完整程序，自带 .NET 运行时，不用装任何东西。

2. **必须以管理员身份运行**：右键 CKeyViewer.exe → 以管理员身份运行。
   直接双击会弹出「请使用管理员运行此程序」然后退出。

   原因：按键状态用 GetAsyncKeyState 读取，低完整性权限读不到更高级别进程
   （比如以管理员启动的游戏）的按键 —— 那样覆盖层就会完全没反应。

   想省掉每次右键：右键 exe → 属性 → 兼容性 → 勾选「以管理员身份运行此程序」。
   或者用安装程序（CKeyViewerSetup）装，它会自动帮你打上这个标记。

3. 启动后是没有窗口的：程序以托盘图标 + 全屏透明覆盖层的形式存在。
   如果看不到覆盖层，检查托盘（Win11 默认收进「隐藏的图标」折叠区）。

────────────────────────────────────────────
热键
────────────────────────────────────────────

  Ctrl+Alt+K / F9        显示 / 隐藏覆盖层
  Ctrl+Alt+R / F10       所有计数归零
  Ctrl+Alt+N / F11       切到下一个档案
  Ctrl+Alt+S / F12       打开设置面板
  Ctrl+Alt+L / Ctrl+Alt+F8   进入 / 退出自由布局拖动模式（Esc 退出）

────────────────────────────────────────────
配置文件
────────────────────────────────────────────

  <exe 同级>/config/settings.json
  <exe 同级>/config/profiles/<档案名>.json

格式与 JipperKeyViewer（原版游戏 Mod）双向兼容，原版的 config 目录可以直接拷过来用。

────────────────────────────────────────────
自检
────────────────────────────────────────────

  CKeyViewer.exe --selftest

跑一遍无界面的按键捕获自检（20 项断言），结果写到 exe 同级的 ckv_selftest.txt。

────────────────────────────────────────────
许可
────────────────────────────────────────────

MIT License，见 LICENSE。

本项目是独立实现，不是 JipperKeyViewer 的官方版本，与原版作者 HitMargin 无隶属关系。
仓库内不含原版的任何二进制、反编译代码或美术资源。
""".format(version=VERSION)


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while True:
            b = f.read(1 << 20)
            if not b:
                break
            h.update(b)
    return h.hexdigest()


def main():
    for p in (APP_EXE, SETUP_EXE):
        if not os.path.exists(p):
            print("缺少 %s —— 先跑 tools/build_setup.sh" % p)
            return 1

    # ---- 便携版 zip ----
    zip_path = os.path.join(ROOT, "release", ZIP_NAME)
    print("打包 %s ..." % ZIP_NAME)
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        z.write(APP_EXE, "CKeyViewer/CKeyViewer.exe")
        z.write(os.path.join(ROOT, "LICENSE"), "CKeyViewer/LICENSE")
        z.writestr("CKeyViewer/README.md", io.open(os.path.join(ROOT, "README.md"),
                                                   encoding="utf-8").read())
        z.writestr("CKeyViewer/使用说明.txt", README_TXT)

    # ---- 带版本号的安装程序 ----
    setup_path = os.path.join(ROOT, "release", SETUP_NAME)
    print("复制 %s ..." % SETUP_NAME)
    shutil.copyfile(SETUP_EXE, setup_path)

    # ---- 校验和 ----
    sums = os.path.join(ROOT, "release", "SHA256SUMS.txt")
    with io.open(sums, "w", encoding="utf-8", newline="\n") as f:
        for p in (zip_path, setup_path):
            f.write("%s  %s\n" % (sha256(p), os.path.basename(p)))

    print()
    for p in (zip_path, setup_path, sums):
        print("  %10.1f MB  %s" % (os.path.getsize(p) / 1048576.0, p))
    return 0


if __name__ == "__main__":
    sys.exit(main())
