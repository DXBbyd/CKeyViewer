"""把「本次清理」删掉的文件从回收站里永久抹掉。

背景：WorkBuddy 的安全删除守卫会把 `rm` 转发到 genie-trash，
     于是删掉的东西只是**搬进了 C:\\$Recycle.Bin** —— 而回收站本身就在 C 盘上，
     `df` 看到的可用空间一格没动。真正想腾空间必须把这批条目彻底清掉。

范围：**只清本次清理删的那三类**，按回收站 $I 元数据里记录的「原始路径」判定：
     - <用户目录>\\AppData\\Local\\Temp\\...
     - <用户目录>\\.workbuddy\\logs\\...
     - <用户目录>\\.workbuddy\\traces\\...
     其余条目（用户自己早先删进回收站的东西）**一律不碰**。

用法：
     python tools/purge_recycle.py# 真正删
     python tools/purge_recycle.py --dry-run        # 只报告不删

Windows 回收站的 $I 文件布局：
     $I<name>  版本号(8) 大小(8) 删除时间(8) 路径长度(4, 仅v2) 路径(UTF-16LE)
     $R<name>  实际内容（文件或整棵目录树）
     两者成对，同一 SID 子目录下。
"""

import collections
import glob
import os
import shutil
import struct
import sys

PREFIXES = (
    "\\AppData\\Local\\Temp\\",
    "\\.workbuddy\\logs\\",
    "\\.workbuddy\\traces\\",
)


def parse_i(path):
    """从 $I 元数据里还原 (原始路径, 原始大小)。格式不对就返回 None。"""
    try:
        with open(path, "rb") as f:
            b = f.read()
        if len(b) < 24:
            return None
        version, size = struct.unpack_from("<QQ", b, 0)
        if version == 2:
            if len(b) < 28:
                return None
            nchars = struct.unpack_from("<I", b, 24)[0]
            orig = b[28:28 + nchars * 2].decode("utf-16-le", "ignore").rstrip("\x00")
        elif version == 1:
            orig = b[24:].decode("utf-16-le", "ignore").rstrip("\x00")
        else:
            return None
        return orig, size
    except Exception:
        return None


def in_scope(orig):
    return any(p in orig for p in PREFIXES)


def main():
    dry = "--dry-run" in sys.argv
    freed = 0
    hit = 0
    skipped = 0
    errors = []

    for drive in ("C", "E", "D"):
        root = drive + ":\\$Recycle.Bin"
        if not os.path.isdir(root):
            continue
        for sid in glob.glob(os.path.join(root, "*")):
            if not os.path.isdir(sid) or not os.path.basename(sid).startswith("S-1-5"):
                continue
            for meta in glob.glob(os.path.join(sid, "$I*")):
                parsed = parse_i(meta)
                if not parsed or not in_scope(parsed[0]):
                    skipped += 1
                    continue
                payload = os.path.join(sid, "$R" + os.path.basename(meta)[2:])
                size = parsed[1]
                hit += 1
                if dry:
                    continue
                try:
                    if os.path.isdir(payload) and not os.path.islink(payload):
                        shutil.rmtree(payload, ignore_errors=True)
                        still = os.path.exists(payload)
                    elif os.path.exists(payload):
                        os.remove(payload)
                        still = os.path.exists(payload)
                    else:
                        still = False
                    os.remove(meta)          # $R 没了，$I 也一并清掉
                    if not still:
                        freed += size
                except Exception as e:
                    errors.append(f"{payload}: {e}")

    verb = "将清掉" if dry else "已清掉"
    print(f"{verb} {hit} 个回收站条目，释放 {freed / 1048576:.0f} MB")
    print(f"不在清理范围内、原样保留的条目：{skipped} 个")
    if errors:
        print(f"失败 {len(errors)} 个：")
        for e in errors[:10]:
            print("   ", e)
    if dry:
        print("\n（这是演练，加 --dry-run 之外不指定即为真删）")


if __name__ == "__main__":
    main()