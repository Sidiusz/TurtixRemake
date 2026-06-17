"""Parse engine tile dump (out/Level_*.tiles_engine.txt) -> out/Level_*.tiles_e.json.
Format of dump:
  layer|<objIdx>|layerOrder=<L>|cols=<c>|rows=<r>|size=<W.000000 H.000000>
  <x>|<y>|<typeString>|||        typeString = "static <imageMap> <frame>" | "animated <anim>"
Background layers have cols<=2 (single full-scene image stretched).
Output per level: {level, scene:[W,H], layers:[{order,cols,rows,tileW,tileH,background,cells:[...]}]}
cell = {x,y,kind:"static"|"animated", img|anim, frame}
"""
import sys, os, json, re

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')


def parse(path):
    layers = []
    cur = None
    scene = [0, 0]
    for line in open(path, encoding='latin1'):
        line = line.rstrip('\n')
        if not line:
            continue
        if line.startswith('layer|'):
            f = line.split('|')
            order = int(f[2].split('=')[1])
            cols = int(f[3].split('=')[1])
            rows = int(f[4].split('=')[1])
            size = f[5].split('=')[1].split()
            W, H = int(float(size[0])), int(float(size[1]))
            scene = [W, H]
            tileW = W // cols if cols else W
            tileH = H // rows if rows else H
            cur = dict(order=order, cols=cols, rows=rows, tileW=tileW, tileH=tileH,
                       background=(cols <= 2 and rows <= 2), cells=[])
            layers.append(cur)
        else:
            f = line.split('|')
            x, y = int(f[0]), int(f[1])
            ts = f[2].split()
            if not ts:
                continue
            if ts[0] == 'static':
                if len(ts) < 2:
                    continue   # bare 'static' with no imageMap -> skip
                cur['cells'].append(dict(x=x, y=y, kind='static', img=ts[1],
                                         frame=int(ts[2]) if len(ts) > 2 and ts[2].lstrip('-').isdigit() else 0))
            elif ts[0] == 'animated':
                if len(ts) < 2:
                    continue
                cur['cells'].append(dict(x=x, y=y, kind='animated', anim=ts[1], frame=0))
    # draw order: engine layerOrder high=back. sort so index 0 = backmost.
    layers.sort(key=lambda L: -L['order'])
    return dict(scene=scene, layers=layers)


def main():
    if len(sys.argv) > 1:
        bases = [a if a.startswith('Level_') else 'Level_' + a for a in sys.argv[1:]]
    else:
        bases = [f[:-len('.tiles_engine.txt')] for f in os.listdir(OUT)
                 if f.endswith('.tiles_engine.txt')]
    for base in sorted(bases):
        p = os.path.join(OUT, base + '.tiles_engine.txt')
        if not os.path.exists(p):
            print('missing', p); continue
        res = parse(p)
        res['level'] = base
        json.dump(res, open(os.path.join(OUT, base + '.tiles_e.json'), 'w'))
        bg = sum(1 for L in res['layers'] if L['background'])
        cells = sum(len(L['cells']) for L in res['layers'])
        print(f"{base}: layers={len(res['layers'])} bg={bg} cells={cells} "
              f"orders={[L['order'] for L in res['layers']]}")


if __name__ == '__main__':
    main()
