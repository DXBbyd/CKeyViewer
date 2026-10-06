"""由 icon/icon.png 生成多尺寸应用图标 assets/app.ico。

用法:
    python tools/makeicon.py                       # 用默认路径
    python tools/makeicon.py <源图> <输出.ico>

产出 ICO 使用 **PNG 压缩条目**（Vista+ 支持），含 16/20/24/32/40/48/64/128/256 九档，
这样任务栏、资源管理器各视图、Alt+Tab 都能取到合适的尺寸而不用现场缩放。

缩小时分级降采样（progressive halving）再收尾到目标尺寸 —— 从 1254px 一步
LANCZOS 压到 16px 会把笔画糊掉，分几步走清晰得多。

依赖 Pillow：pip install Pillow
"""
import io
import os
import struct
import sys

try:
    from PIL import Image
except ImportError:
    sys.stderr.write("需要 Pillow：pip install Pillow\n")
    sys.exit(1)

SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]


def downscale(img, target):
    """分级降采样：每步最多缩一半，最后一步收到 target。"""
    cur = img
    while cur.width // 2 >= target:
        cur = cur.resize((max(target, cur.width // 2), max(target, cur.height // 2)),
                         Image.LANCZOS)
        if cur.width == target:
            return cur
    if cur.size != (target, target):
        cur = cur.resize((target, target), Image.LANCZOS)
    return cur


def build(src_path, out_path):
    src = Image.open(src_path).convert("RGBA")
    print("源图: %s (%dx%d)" % (src_path, src.width, src.height))

    pngs = []
    for s in SIZES:
        im = downscale(src, s)
        buf = io.BytesIO()
        im.save(buf, format="PNG", optimize=True)
        data = buf.getvalue()
        pngs.append(data)
        print("  %3dx%-3d -> %6d bytes" % (s, s, len(data)))

    out_dir = os.path.dirname(os.path.abspath(out_path))
    if out_dir and not os.path.isdir(out_dir):
        os.makedirs(out_dir)

    # ICONDIR + ICONDIRENTRY[] + PNG 数据
    head = struct.pack("<HHH", 0, 1, len(SIZES))
    offset = len(head) + 16 * len(SIZES)
    entries = []
    for s, data in zip(SIZES, pngs):
        dim = 0 if s >= 256 else s          # 256 在目录项里写 0
        entries.append(struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset))
        offset += len(data)

    with open(out_path, "wb") as f:
        f.write(head)
        for e in entries:
            f.write(e)
        for data in pngs:
            f.write(data)

    print("icon written: %s (%d bytes, %d sizes)" % (out_path, os.path.getsize(out_path), len(SIZES)))


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, "CKeyViewer", "icon", "icon.png")
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(root, "CKeyViewer", "assets", "app.ico")

    if not os.path.isfile(src):
        sys.stderr.write("源图不存在: %s\n" % src)
        return 1
    build(src, out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
