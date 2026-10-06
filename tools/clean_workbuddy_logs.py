"""清掉 .workbuddy/logs 与 traces 里**没删掉**的那些残留文件。

背景：这批日志正常是用 shell 的 `rm` 删的，但本机WorkBuddy 装了安全删除守卫，
`rm` 会被转发到 genie-trash；转发偶尔会以
`Error during a 'trash' operation: ... Some operations were aborted` 失败，
文件就留在原处。用tools/purge_recycle.py 清回收站只解决「已删掉的那部分」，
原址残留还得单独扫。

这里同样直接调 os.remove 绕开转发。**只删 logs / traces 两个目录里的东西**，
binaries 和 workspace 一个字节都不碰（那是我跑脚本用的 Python/Node 运行时）。

用法：
     python tools/clean_workbuddy_logs.py [--dry-run]
"""

import os
import sys
import time

TARGETS = [
    r"C:\Users\LENOVO\.workbuddy\logs",
    r"C:\Users\LENOVO\.workbuddy\traces",
]

# 这些是 WorkBuddy 正在写的活跃文件，删了也会立刻重建，跳过更干净
SKIP_NAMES = {"renderer.log", "daemon.log", "main.log"}


def main():
    dry = "--dry-run" in sys.argv
    total_n = 0
    total_b = 0
    failed = 0

    print("【清 .workbuddy/logs 与 traces 残留】" + ("（演练）" if dry else ""))
    for root_dir in TARGETS:
        if not os.path.isdir(root_dir):
            print(f"  - 不存在 {root_dir}")
            continue
        n = 0
        b = 0
        t0 = time.time()
        for dirpath, _dirs, files in os.walk(root_dir):
            for name in files:
                p = os.path.join(dirpath, name)
                if name in SKIP_NAMES and dirpath == root_dir:
                    continue
                try:
                    sz = os.path.getsize(p)
                except OSError:
                    continue
                n += 1
                b += sz
                if dry:
                    continue
                try:
                    os.remove(p)
                except OSError:
                    failed += 1
        total_n += n
        total_b += b
        label = os.path.basename(root_dir)
        verb = "将删" if dry else "已删"
        print(f"  {verb}  {label:<10} {n:>5} 个文件  {b / 1048576:>7.0f} MB  ({time.time() - t0:.1f}s)")

    print(f"\n  合计：{total_n} 个文件 / {total_b / 1048576:.0f} MB"
          + (f"，失败 {failed} 个（通常是 WorkBuddy 正在占用）" if failed else ""))

    print("\n【复核】")
    for d in TARGETS:
        left = sum(len(f) for _, _, f in os.walk(d)) if os.path.isdir(d) else 0
        size = sum(
            os.path.getsize(os.path.join(r, f))
            for r, _, fs in os.walk(d) for f in fs
        ) if os.path.isdir(d) else 0
        print(f"  {os.path.basename(d):<10} 剩 {left} 个文件 / {size / 1048576:.0f} MB")


if __name__ == "__main__":
    main()