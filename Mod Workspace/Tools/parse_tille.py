"""
Turtix binary parser -> JSON.
Decodes the cracked parts: Game.dat (full) + .tille tile layers + palette.
Placed-object layer NOT yet decoded (needs engine dump ground truth).

Usage:
  python parse_tille.py gamedat  Content/Game.dat            -> out/Game.dat.json
  python parse_tille.py tiles    Content/Levels/X.tille      -> out/X.tiles.json
  python parse_tille.py tilesall Content/Levels              -> all .tille
"""
import sys, os, json, struct


class R:
    def __init__(self, data):
        self.d = data
        self.i = 0
    def eof(self):
        return self.i >= len(self.d)
    def u8(self):
        v = self.d[self.i]; self.i += 1; return v
    def u16(self):
        v = struct.unpack_from('<H', self.d, self.i)[0]; self.i += 2; return v
    def u32(self):
        v = struct.unpack_from('<I', self.d, self.i)[0]; self.i += 4; return v
    def f32(self):
        v = struct.unpack_from('<f', self.d, self.i)[0]; self.i += 4; return v
    def s(self):
        n = self.u8()
        b = self.d[self.i:self.i + n]; self.i += n
        return b.decode('latin1')


# ---------------- Game.dat ----------------
def parse_gamedat(path):
    r = R(open(path, 'rb').read())
    count = r.u32()
    objs = []
    for _ in range(count):
        if r.eof():
            break
        oid = r.u32()
        name = r.s()
        imgw, imgh = r.u32(), r.u32()
        grid = [r.u32(), r.u32(), r.u32(), r.u32()]
        nfields = r.u32()
        fields = []
        # field block is not byte-locked yet; capture raw until next plausible id.
        # store nfields + best-effort key/value pull.
        try:
            for _ in range(nfields):
                k = r.s()
                v = r.s()
                fields.append([k, v])
        except Exception:
            pass
        objs.append(dict(id=oid, name=name, img=[imgw, imgh], grid=grid,
                         nfields=nfields, fields=fields))
    return dict(count=count, parsed=len(objs), objects=objs)


# ---------------- .tille tile layers ----------------
def try_parse_layer(d, p):
    """Try parse a tile layer at offset p. Return (layer, end) or None."""
    if p + 16 > len(d):
        return None
    tw, th, cols, rows = struct.unpack_from('<IIII', d, p)
    # sanity: square-ish tiles, sane grid
    if not (1 <= tw <= 512 and 1 <= th <= 512):
        return None
    if not (1 <= cols <= 4096 and 1 <= rows <= 4096):
        return None
    if cols * rows > 200000:
        return None
    i = p + 16
    cells = []
    n = len(d)
    for _ in range(cols * rows):
        if i + 1 > n:
            return None
        flag = d[i]; i += 1
        if flag == 0:                 # empty cell = single 0x00
            cells.append(None)
        elif flag == 1:               # filled = 01 [pad u8] [u32 frame] [str]
            if i + 5 > n:
                return None
            i += 1                    # pad byte (always 0)
            frame = struct.unpack_from('<I', d, i)[0]; i += 4
            ln = d[i]; i += 1
            if i + ln > n:
                return None
            name = d[i:i + ln].decode('latin1')
            i += ln
            if not name.startswith('i'):
                return None
            cells.append([frame, name])
        else:
            return None
    return dict(tile=[tw, th], cols=cols, rows=rows, cells=cells), i


def parse_tiles(path):
    d = open(path, 'rb').read()
    n = len(d)
    sceneW, sceneH = struct.unpack_from('<II', d, 0)
    layers = []
    # scan for layer headers (square tile dims) and validate full cell stream
    p = 8
    while p < n - 16:
        res = try_parse_layer(d, p)
        if res:
            layer, end = res
            # real grid covers the whole scene; else it's a scanner false-hit in object bytes
            layer['valid'] = (layer['cols'] * layer['tile'][0] == sceneW and
                              layer['rows'] * layer['tile'][1] == sceneH)
            layers.append(layer)
            p = end
        else:
            p += 1
    layers = [L for L in layers if L['valid']]
    return dict(file=os.path.basename(path), scene=[sceneW, sceneH],
                layers=[{k: v for k, v in L.items() if k != 'cells'} | {'filled': sum(c is not None for c in L['cells'])} for L in layers],
                raw_layers=layers)


def out_path(name):
    os.makedirs('Mod Workspace/Tools/out', exist_ok=True)
    return os.path.join('Mod Workspace/Tools/out', name)


def main():
    mode = sys.argv[1]
    if mode == 'gamedat':
        res = parse_gamedat(sys.argv[2])
        op = out_path('Game.dat.json')
        json.dump(res, open(op, 'w'), indent=1)
        print(f"{op}: count={res['count']} parsed={res['parsed']}")
    elif mode == 'tiles':
        res = parse_tiles(sys.argv[2])
        op = out_path(os.path.basename(sys.argv[2]) + '.tiles.json')
        json.dump(res, open(op, 'w'), indent=1)
        print(f"{op}: scene={res['scene']} layers={res['layers']}")
    elif mode == 'tilesall':
        d = sys.argv[2]
        for f in sorted(os.listdir(d)):
            if f.endswith('.tille'):
                res = parse_tiles(os.path.join(d, f))
                op = out_path(f + '.tiles.json')
                json.dump(res, open(op, 'w'), indent=1)
                print(f"{f}: scene={res['scene']} layers={len(res['layers'])} "
                      f"{[ (L['cols'],L['rows'],L['filled']) for L in res['layers'] ]}")
    else:
        print(__doc__)


if __name__ == '__main__':
    main()
