"""
Tolerant tile-layer parser. Levels have up to 8 layers in several cell grammars:
  empty cell   : u8 0x00
  image cell   : u8 0x01, u8 0x00, u32 frame, str img            (e.g. i7)
  image2 cell  : u8 0x01, u8 0x00, u32 frame, str img, str code  (64px layers, code=NWA0 autotile)
  object cell  : u8 0x01, u8 0x01, str datablock                 (e.g. a811Main, a23985Main)
A layer header is [u32 tw][u32 th][u32 cols][u32 rows] with cols*tw==sceneW, rows*th==sceneH.
We auto-pick, per layer, the grammar (1 vs 2 strings on image cells) that consumes EXACTLY
cols*rows cells, then validate the next bytes look like another header / section.

Usage:
  python parse_layers.py Content/Levels/Level_W1_01.tille        # report
  python parse_layers.py all                                     # write out/Level_*.layers.json
"""
import sys, os, json, struct

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LEVELS = os.path.join(ROOT, "Content", "Levels")
OUT = os.path.join(ROOT, "Mod Workspace", "Tools", "out")


def rstr(d, i):
    """read u8-len ascii string at i -> (text, new_i) or (None, i) if not a clean string."""
    if i >= len(d):
        return None, i
    ln = d[i]
    if ln == 0 or i + 1 + ln > len(d):
        return None, i
    s = d[i + 1:i + 1 + ln]
    if not all(32 <= b < 127 for b in s):
        return None, i
    return s.decode("latin1"), i + 1 + ln


def parse_cells(d, start, ncells, two_str):
    """Try to read ncells cells from start. Return (cells, end) or None on grammar break."""
    cells = []
    i = start
    n = len(d)
    for _ in range(ncells):
        if i >= n:
            return None
        flag = d[i]; i += 1
        if flag == 0:
            cells.append(None)
            continue
        if flag != 1:
            return None
        if i >= n:
            return None
        sub = d[i]; i += 1
        if sub == 0:
            if i + 4 > n:
                return None
            frame = struct.unpack_from("<I", d, i)[0]; i += 4
            img, j = rstr(d, i)
            if img is None:
                return None
            i = j
            code = None
            if two_str:
                # code = u8 len + raw bytes (NOT always ascii: e.g. "NWA0", "MWA"+0x01
                # = 3-char dir code + variant byte). read raw, keep ascii-or-hex.
                if i >= len(d):
                    return None
                ln = d[i]
                if ln == 0 or ln > 16 or i + 1 + ln > len(d):
                    return None
                raw = d[i + 1:i + 1 + ln]
                i += 1 + ln
                code = raw.decode("latin1") if all(32 <= b < 127 for b in raw) else raw.hex()
            cells.append({"frame": frame, "img": img, "code": code})
        elif sub == 1:
            db, j = rstr(d, i)
            if db is None:
                return None
            i = j
            cells.append({"db": db})
        else:
            return None
    return cells, i


def find_headers(d):
    sw, sh = struct.unpack_from("<II", d, 0)
    hdrs = []
    for o in range(0, len(d) - 16):
        tw, th, c, r = struct.unpack_from("<IIII", d, o)
        if 8 <= tw <= 512 and 8 <= th <= 512 and 1 <= c <= 2048 and 1 <= r <= 2048 \
                and c * tw == sw and r * th == sh:
            hdrs.append((o, tw, th, c, r))
    return sw, sh, hdrs


def parse_level(path):
    d = open(path, "rb").read()
    sw, sh, hdrs = find_headers(d)
    layers = []
    for (o, tw, th, c, r) in hdrs:
        ncells = c * r
        best = None
        for two in (False, True):
            res = parse_cells(d, o + 16, ncells, two)
            if res:
                cells, end = res
                # prefer the grammar whose end lands on another header or near EOF
                ends_clean = (end >= len(d) - 4) or any(h[0] == end for h in hdrs) \
                    or all(32 <= d[end] < 127 or d[end] in (0, 1) for _ in [0])
                filled = sum(x is not None for x in cells)
                cand = (two, cells, end, filled, ends_clean)
                if best is None or (ends_clean and not best[4]):
                    best = cand
        if best:
            two, cells, end, filled, clean = best
            layers.append(dict(off=o, tile=[tw, th], cols=c, rows=r,
                               two_str=two, filled=filled, end=end, cells=cells))
    return dict(file=os.path.basename(path), scene=[sw, sh], headers=len(hdrs),
                layers=layers)


def summary(res):
    print(f"{res['file']} scene={res['scene']} headers={res['headers']} parsed_layers={len(res['layers'])}")
    for L in res["layers"]:
        kinds = {}
        for cdef in L["cells"]:
            if cdef is None:
                continue
            k = "db" if "db" in cdef else ("img2" if cdef.get("code") else "img")
            kinds[k] = kinds.get(k, 0) + 1
        print(f"  @{L['off']:6d} {L['tile'][0]}px {L['cols']}x{L['rows']} "
              f"two_str={int(L['two_str'])} filled={L['filled']} kinds={kinds} end={L['end']}")


def main():
    a = sys.argv[1]
    if a == "all":
        nw = 0
        for w in range(1, 6):
            for l in range(1, 13):
                base = "Level_W%d_%02d" % (w, l)
                p = os.path.join(LEVELS, base + ".tille")
                if not os.path.exists(p):
                    continue
                res = parse_level(p)
                json.dump(res, open(os.path.join(OUT, base + ".layers.json"), "w"))
                nw += 1
                print(f"{base}: headers={res['headers']} layers={len(res['layers'])}")
        print(f"\nwrote {nw} *.layers.json")
    else:
        summary(parse_level(a))


if __name__ == "__main__":
    main()
