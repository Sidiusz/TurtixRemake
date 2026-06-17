"""
Crack the .tille placed-object layer using the engine objdump as an ORACLE.
objdump gives exact (x,y) per object (in engine order); the binary carries the
datablock NAME per object. We locate each known position in the binary as an
f32 pair and inspect the surrounding bytes to lock the record grammar.

Usage:
  python crack_objects.py Content/Levels/Level_W1_01.tille Mod Workspace/Tools/out/Level_W1_01.objdump.txt
"""
import sys, struct, re


def load_objdump(path):
    objs = []
    for line in open(path, encoding='latin1'):
        line = line.rstrip('\n')
        if line.startswith('#') or not line:
            continue
        f = line.split('|')
        if len(f) < 6:
            continue
        idx, cls, name, pos, size = f[0], f[1], f[2], f[3], f[4]
        px, py = (pos.split() + ['0', '0'])[:2]
        sx, sy = (size.split() + ['0', '0'])[:2]
        objs.append(dict(idx=int(idx), cls=cls, name=name,
                         x=float(px), y=float(py), sx=float(sx), sy=float(sy)))
    return objs


def find_s32(d, val):
    """offsets where a signed int32 == val."""
    target = struct.pack('<i', int(round(val)))
    hits, i = [], 0
    while True:
        j = d.find(target, i)
        if j < 0:
            break
        hits.append(j)
        i = j + 1
    return hits


def find_f32_pair(d, x, y, maxgap=8):
    """offsets where s32 x is closely followed (within maxgap) by s32 y."""
    pairs = []
    for ox in find_s32(d, x):
        for off in range(0, maxgap + 1):
            oy = ox + 4 + off
            if oy + 4 <= len(d):
                v = struct.unpack_from('<i', d, oy)[0]
                if v == int(round(y)):
                    pairs.append((ox, oy, off))
    return pairs


def scan_strings(d, minlen=1, maxlen=40):
    """yield (offset_of_lenbyte, end_offset_after_string, text) for u8-len ASCII strings."""
    i, n = 0, len(d)
    while i < n:
        ln = d[i]
        if minlen <= ln <= maxlen and i + 1 + ln <= n:
            s = d[i+1:i+1+ln]
            if all(32 <= b < 127 for b in s):
                yield i, i + 1 + ln, s.decode('latin1')
        i += 1


def main():
    tille, odump = sys.argv[1], sys.argv[2]
    d = open(tille, 'rb').read()
    objs = load_objdump(odump)
    sprites = [o for o in objs if o['cls'] == 't2dAnimatedSprite' and (o['x'] or o['y'])]
    # map exact (x,y) -> objdump idx (may collide if two objs share a cell; keep list)
    posmap = {}
    for o in sprites:
        posmap.setdefault((int(o['x']), int(o['y'])), []).append(o['idx'])
    print(f"{tille}: size={len(d)}  positioned_sprites={len(sprites)}  unique_pos={len(posmap)}")

    # find strings whose trailing s32 pair == a known sprite position
    matched = {}      # (x,y) -> name
    hits = []
    for (lo, end, s) in scan_strings(d):
        if end + 8 > len(d):
            continue
        x = struct.unpack_from('<i', d, end)[0]
        y = struct.unpack_from('<i', d, end + 4)[0]
        if (x, y) in posmap:
            hits.append((end, s, x, y))
            matched.setdefault((x, y), s)

    print(f"\nstring-then-(x,y) records matching sprite positions: {len(hits)}")
    for (off, s, x, y) in hits:
        idxs = posmap[(x, y)]
        print(f'  @{off:6d} name="{s}" pos=({x},{y}) -> objdump idx {idxs}')

    miss = [p for p in posmap if p not in matched]
    print(f"\nsprite positions WITHOUT a name record: {len(miss)} / {len(posmap)}")
    for (x, y) in miss[:20]:
        print(f"  ({x},{y}) idx {posmap[(x,y)]}")


if __name__ == '__main__':
    main()
