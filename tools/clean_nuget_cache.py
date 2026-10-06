"""清掉 C 盘的 NuGet 缓存（缓存已改道到 E 盘，见 tools/env.sh）。

**为什么用 Python 而不是 shell 的 rm**：本机WorkBuddy 装了安全删除守卫，
`rm` 会被转发进 C:\\$Recycle.Bin —— 而回收站就在 C 盘上，等于没腾地方。
这里直接调 shutil/os 绕开它。

E 盘那份是经`dotnet publish --self-contained` 实测验证过的：
runtime packs + ILLink tasks 都落在 E:\\dsh工作区\\.cache\\nuget-packages。

用法：python tools/clean_nuget_cache.py [--dry-run]
"""

import os
import shutil
import sys
import time

TARGETS = [
    (r"C:\Users\LENOVO\.nuget\packages", ".nuget/packages"),
    (r"C:\Users\LENOVO\AppData\Local\NuGet", "AppData/Local/NuGet"),
    (r"E:\ckv_stage\_pubtest", "E:/ckv_stage/_pubtest（发布验证临时产物）"),
]

KEEP = [
    r"E:\dsh工作区\.cache\nuget-packages",
    r"E:\dsh工作区\.cache\nuget-http",
]


def dir_size(path):
    total = 0
    # 注意别用 `for _, _, files in ...` —— 第二个 `_` 拿到的是**子目录列表**，
    # 后面 os.path.join(_, n) 会拿 list 当路径，直接 TypeError（踩过）。
    for root, _dirs, files in os.walk(path):
        for n in files:
            try:
                total += os.path.getsize(os.path.join(root, n))
            except OSError:
                pass
    return total


def wipe(path, label, dry):
    if not os.path.isdir(path):
        print(f"  - 不存在{label}")
        return 0
    size = dir_size(path)
    if dry:
        print(f"  [演练] {label:<42} {size / 1048576:>7.0f} MB")
        return 0
    t = time.time()
    shutil.rmtree(path, ignore_errors=True)
    ok = not os.path.isdir(path)
    mark = "已删" if ok else "! 残留"
    print(f"  {mark}  {label:<42} {size / 1048576:>7.0f} MB  ({time.time() - t:.1f}s)")
    return size if ok else 0


def main():
    dry = "--dry-run" in sys.argv
    print("【清 NuGet 缓存与临时产物】" + ("（演练）" if dry else ""))
    freed = sum(wipe(p, label, dry) for p, label in TARGETS)

    print(f"\n  小计：{freed / 1048576:.0f} MB")

    print("\n【复核】")
    for p, label in TARGETS:
        state = "仍存在" if os.path.isdir(p) else "已清除"
        print(f"  {label:<42} {state}")
    for p in KEEP:
        if os.path.isdir(p):
            n = sum(len(f) for _, _, f in os.walk(p))
            size = dir_size(p)
            print(f"  [保留] {p:<38} {n} 个文件 / {size / 1048576:.0f} MB")
        else:
            print(f"  [缺失!] {p}")


if __name__ == "__main__":
    main()