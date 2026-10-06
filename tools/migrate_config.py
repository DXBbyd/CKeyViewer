"""把一个安装目录的配置迁移到另一个安装目录。

用法: python migrate_config.py <源目录> <目标目录>

搬的是整个 `config/` 目录（档案 + 全局设置），目标里已存在的文件会先备份成
`<文件名>.before-migrate`。迁移前**务必先关掉目标目录里正在运行的程序** ——
否则它下一次落盘会把你刚写进去的档案原样盖回去。

例：python migrate_config.py E:/dsh工作区/Keyviever/dist E:/dsh工作区/Keyviever/dist_new
"""
import glob
import io
import json
import os
import shutil
import sys


def summarize(profile_path):
    try:
        with io.open(profile_path, encoding="utf-8-sig") as f:
            d = json.load(f)
    except Exception as e:
        return "读取失败: %s" % e
    nodes = d.get("CustomNodes") or []
    groups = d.get("LayerGroups") or []
    return ("style=%s 节点=%d 图层组=%d 累计按键=%s 当前档案配色=#%s"
            % (d.get("KeyViewerStyle"), len(nodes), len(groups),
               d.get("TotalCount"), d.get("Background")))


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1

    src = os.path.join(sys.argv[1], "config")
    dst = os.path.join(sys.argv[2], "config")

    if not os.path.isdir(src):
        print("源 config 不存在: %s" % src)
        return 1

    print("源   : %s" % src)
    print("目标 : %s" % dst)

    # 先看一眼源里有什么，别搬了个空的过去
    src_profiles = sorted(glob.glob(os.path.join(src, "profiles", "*.json")))
    src_profiles = [p for p in src_profiles if not p.endswith(".bak")]
    if not src_profiles:
        print("源里没有档案文件，中止")
        return 1
    print("源档案:")
    for p in src_profiles:
        print("  %-14s %s" % (os.path.basename(p), summarize(p)))

    # 目标里同名文件先备份
    moved = 0
    for root, _dirs, files in os.walk(src):
        rel = os.path.relpath(root, src)
        out_dir = dst if rel == "." else os.path.join(dst, rel)
        if not os.path.isdir(out_dir):
            os.makedirs(out_dir)
        for name in files:
            s = os.path.join(root, name)
            t = os.path.join(out_dir, name)
            if os.path.exists(t):
                bak = t + ".before-migrate"
                if os.path.exists(bak):
                    os.remove(bak)
                shutil.move(t, bak)
                print("  备份旧文件 -> %s" % os.path.basename(bak))
            shutil.copy2(s, t)
            moved += 1
            print("  迁移 %s" % os.path.relpath(t, dst))

    print("\n共迁移 %d 个文件。目标档案现在的内容:" % moved)
    for p in sorted(glob.glob(os.path.join(dst, "profiles", "*.json"))):
        if p.endswith(".bak") or p.endswith(".before-migrate"):
            continue
        print("  %-14s %s" % (os.path.basename(p), summarize(p)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
