import struct, sys

def rva2off(sections, rva):
    for va, vsz, praw, rsz in sections:
        if va <= rva < va + max(vsz, rsz):
            return praw + (rva - va)
    return None

def parse(path):
    d = open(path, 'rb').read()
    if d[:2] != b'MZ': return None
    e_lfanew = struct.unpack_from('<I', d, 0x3C)[0]
    assert d[e_lfanew:e_lfanew+4] == b'PE\0\0', 'no PE sig'
    coff = e_lfanew + 4
    nsec, = struct.unpack_from('<H', d, coff + 2)
    sizeopt, = struct.unpack_from('<H', d, coff + 16)
    opt = coff + 20
    magic, = struct.unpack_from('<H', d, opt)
    pe32plus = (magic == 0x20B)
    nrvadir_off = opt + (108 if pe32plus else 92)
    nrvadir, = struct.unpack_from('<I', d, nrvadir_off)
    dd = nrvadir_off + 4
    res_rva, res_sz = struct.unpack_from('<II', d, dd + 2 * 8)
    secoff = opt + sizeopt
    sections = []
    for i in range(nsec):
        b = secoff + i * 40
        name = d[b:b+8].rstrip(b'\0').decode('latin1')
        vsz, va, rsz, praw = struct.unpack_from('<IIII', d, b + 8)
        sections.append((va, vsz, praw, rsz))
    return d, sections, res_rva, res_sz

def walk_dir(d, base, off, depth, path, out):
    chars, ts, mv, mn, nnamed, nid = struct.unpack_from('<IIHHHH', d, off)
    total = nnamed + nid
    for i in range(total):
        e = off + 16 + i * 8
        nameid, offdata = struct.unpack_from('<II', d, e)
        if nameid & 0x80000000:
            label = 'named@%d' % (nameid & 0x7FFFFFFF)
        else:
            label = nameid
        if offdata & 0x80000000:
            walk_dir(d, base, base + (offdata & 0x7FFFFFFF), depth + 1, path + [label], out)
        else:
            de = base + offdata
            drva, dsz, cp, rv = struct.unpack_from('<IIII', d, de)
            out.append((path + [label], drva, dsz))

def main(path):
    r = parse(path)
    if not r:
        print('not a PE'); return
    d, sections, res_rva, res_sz = r
    print('PE ok  sections=%d  .rsrc rva=0x%X size=%d' % (len(sections), res_rva, res_sz))
    base = rva2off(sections, res_rva)
    if base is None:
        print('no resource section'); return
    out = []
    walk_dir(d, base, base, 0, [], out)
    types = {}
    for p, rva, sz in out:
        types.setdefault(p[0], []).append((p, rva, sz))
    print('resource types:', sorted(types.keys()))
    # 14 = RT_GROUP_ICON, 3 = RT_ICON, 16 = RT_VERSION
    if 14 in types:
        for p, rva, sz in types[14]:
            o = rva2off(sections, rva)
            blob = d[o:o+sz]
            res, typ, cnt = struct.unpack_from('<HHH', blob, 0)
            print('RT_GROUP_ICON id=%s -> %d entries' % (p[1], cnt))
            for i in range(cnt):
                w, h, cc, rs, pl, bc, bsz, iid = struct.unpack_from('<BBBBHHIH', blob, 6 + 14 * i)
                print('   %3dx%-3d bpp=%-2d bytes=%-6d resId=%d' % (w or 256, h or 256, bc, bsz, iid))
    else:
        print('!! RT_GROUP_ICON (14) NOT FOUND')
    if 3 in types:
        print('RT_ICON count = %d' % len(types[3]))
        for p, rva, sz in sorted(types[3], key=lambda x: x[0][1])[:3]:
            o = rva2off(sections, rva)
            print('   id=%s size=%d sig=%r' % (p[1], sz, d[o:o+4]))
    else:
        print('!! RT_ICON (3) NOT FOUND')
    if 16 in types:
        print('RT_VERSION present ->', [p[1] for p, _, _ in types[16]])

main(sys.argv[1])
