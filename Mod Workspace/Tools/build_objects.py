"""
Build per-level placed-object JSON by fusing two validated sources:
  - engine objdump (out/Level_*.objdump.txt): exact class/pos/size/layer/flip per object.
  - binary .tille object records: [s32 typeId(Game.dat id)][s32 uid][s32 x][s32 y][...].
For each positioned t2dAnimatedSprite we look up its binary record by (x,y) to recover
typeId, then resolve typeId -> Game.dat template name "a{id}".

Validated: W1_01 matches 42/42. typeId 176 = player; 22144 = End_Level portal; 553 = diamond.

Usage:
  python build_objects.py            # all 60 levels -> out/Level_*.objects.json
  python build_objects.py W1_01      # one level
"""
import sys, os, re, json, struct

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Mod Workspace", "Tools", "out")
LEVELS = os.path.join(ROOT, "Content", "Levels")
GAMEDAT = os.path.join(ROOT, "Content", "Game.dat")


def gamedat_ids():
    g = open(GAMEDAT, "rb").read()
    ids = set()
    for m in re.finditer(rb"(....)([\x01-\x10])a([0-9]+)", g):
        idv = struct.unpack("<I", m.group(1))[0]
        ln = m.group(2)[0]; nm = m.group(3).decode()
        if ln == len("a" + nm) and str(idv) == nm:
            ids.add(idv)
    return ids


def load_objdump(path):
    objs = []
    for line in open(path, encoding="latin1"):
        if not line or line[0] == "#" or "|" not in line:
            continue
        f = line.rstrip("\n").split("|")
        if len(f) < 9:
            continue
        p = f[3].split(); s = f[4].split()
        objs.append(dict(
            idx=int(f[0]), cls=f[1], name=f[2],
            x=int(float(p[0])) if p else 0, y=int(float(p[1])) if len(p) > 1 else 0,
            sx=float(s[0]) if s else 0, sy=float(s[1]) if len(s) > 1 else 0,
            rot=f[5], layer=f[6], flipX=f[7], flipY=f[8]))
    return objs


def build_level(base, gids):
    tille = os.path.join(LEVELS, base + ".tille")
    odump = os.path.join(OUT, base + ".objdump.txt")
    if not (os.path.exists(tille) and os.path.exists(odump)):
        return None
    d = open(tille, "rb").read()
    objs = load_objdump(odump)

    sprites = [o for o in objs if o["cls"] == "t2dAnimatedSprite" and (o["x"] or o["y"])]
    pos = {}
    for o in sprites:
        pos.setdefault((o["x"], o["y"]), []).append(o)

    # index binary records [typeId in gids][uid][x][y] by (x,y)
    rec_by_pos = {}
    for off in range(8, len(d) - 16):
        t = struct.unpack_from("<i", d, off)[0]
        if t in gids:
            x = struct.unpack_from("<i", d, off + 8)[0]
            y = struct.unpack_from("<i", d, off + 12)[0]
            if (x, y) in pos and (x, y) not in rec_by_pos:
                rec_by_pos[(x, y)] = (off, t)

    out_objs, unresolved = [], 0
    for o in sprites:
        rec = rec_by_pos.get((o["x"], o["y"]))
        typeId = rec[1] if rec else None
        if typeId is None:
            unresolved += 1
        out_objs.append(dict(
            idx=o["idx"], typeId=typeId,
            template=("a%d" % typeId) if typeId else None,
            x=o["x"], y=o["y"], sx=o["sx"], sy=o["sy"],
            layer=int(o["layer"]) if o["layer"].lstrip("-").isdigit() else o["layer"],
            flipX=o["flipX"], flipY=o["flipY"],
            isPlayer=(typeId == 176), isPortal=(typeId == 22144)))
    return dict(level=base, count=len(out_objs), unresolved=unresolved, objects=out_objs)


def main():
    gids = gamedat_ids()
    bases = []
    if len(sys.argv) > 1:
        bases = ["Level_" + a if not a.startswith("Level_") else a for a in sys.argv[1:]]
    else:
        for w in range(1, 6):
            for l in range(1, 13):
                bases.append("Level_W%d_%02d" % (w, l))
    total = 0
    for base in bases:
        res = build_level(base, gids)
        if not res:
            continue
        json.dump(res, open(os.path.join(OUT, base + ".objects.json"), "w"), indent=1)
        total += 1
        flag = "" if res["unresolved"] == 0 else f"  !! {res['unresolved']} unresolved"
        player = sum(1 for o in res["objects"] if o["isPlayer"])
        print(f"{base}: {res['count']} objs, player={player}{flag}")
    print(f"\nwrote {total} *.objects.json")


if __name__ == "__main__":
    main()
