"""裁剪 / 放大本仓库生成的 PNG（screenshot.py 的产物：8bit RGBA、每行 filter=0）。

用法: python crop.py <输入> <输出> x y w h [zoom]
"""
import struct
import sys
import zlib


def read_png(path):
    data = open(path, "rb").read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "not a png"
    pos = 8
    w = h = None
    idat = b""
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        tag = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if tag == b"IHDR":
            w, h, depth, ctype = struct.unpack(">IIBB", body[:10])
            assert depth == 8 and ctype == 6, "expect 8bit RGBA"
        elif tag == b"IDAT":
            idat += body
        elif tag == b"IEND":
            break
    raw = zlib.decompress(idat)
    stride = w * 4
    rows = []
    p = 0
    for _ in range(h):
        f = raw[p]
        assert f == 0, "row filter %d not supported" % f
        p += 1
        rows.append(raw[p:p + stride])
        p += stride
    return w, h, rows


def write_png(path, w, h, rows):
    raw = bytearray()
    for r in rows:
        raw.append(0)
        raw.extend(r)

    def chunk(tag, d):
        return (struct.pack(">I", len(d)) + tag + d
                + struct.pack(">I", zlib.crc32(tag + d) & 0xFFFFFFFF))

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 6))
    png += chunk(b"IEND", b"")
    open(path, "wb").write(png)


def main():
    src, dst = sys.argv[1], sys.argv[2]
    x, y, w, h = (int(v) for v in sys.argv[3:7])
    zoom = int(sys.argv[7]) if len(sys.argv) > 7 else 1

    sw, sh, rows = read_png(src)
    x = max(0, min(x, sw - 1)); y = max(0, min(y, sh - 1))
    w = max(1, min(w, sw - x)); h = max(1, min(h, sh - y))

    out = []
    for r in range(h):
        line = rows[y + r][x * 4:(x + w) * 4]
        if zoom > 1:
            line = b"".join(line[i:i + 4] * zoom for i in range(0, len(line), 4))
        for _ in range(zoom):
            out.append(line)

    write_png(dst, w * zoom, h * zoom, out)
    print("cropped %s -> %s  %dx%d (zoom %d)" % (src, dst, w * zoom, h * zoom, zoom))


if __name__ == "__main__":
    main()
