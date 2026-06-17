"""Convert parser outputs to Unity-friendly JSON under Assets/Sources/GeneratedData.

Inputs (Mod Workspace/Tools/out):
  imagemaps.json, animations.json
  Level_*.layers.json   (parse_layers.py: all tile layers, cell variants img/img2/db)
  Level_*.objects.json  (build_objects.py: placed objects by typeId)
Outputs (GeneratedData):
  imagemaps.json [{name,png(assetpath),cellW,cellH,mode}]
  animations.json [{name,imageMap,frames[],cycle,time}]
  tiles/Level_*.json    {file,scene,layers:[{tileW,tileH,cols,rows,cells:[{empty,frame,img}]}]}
  objects/Level_*.json  {level,scene,objects:[{typeId,template,x,y,sx,sy,layer,img,frame,isPlayer,isPortal}]}
db/img2 cells and object typeIds are resolved here to (imageMap,frame) so the C# importer
stays dumb: it only ever needs an imageMap name + a frame index.
"""
import json, os, re

ROOT = r'D:\Torrents\Turtix'
OUT = os.path.join(ROOT, 'Mod Workspace', 'Tools', 'out')
GEN = os.path.join(ROOT, 'Mod Workspace', 'Unity Project', 'TurtixRemake', 'Assets', 'Sources', 'GeneratedData')

_anim = None
_imap = None


def load_src():
    global _anim, _imap
    _anim = json.load(open(os.path.join(OUT, 'animations.json'), encoding='utf-8'))
    _imap = json.load(open(os.path.join(OUT, 'imagemaps.json'), encoding='utf-8'))


def unity_png(png_path):
    rel = re.sub(r'^Content/', '', png_path.replace('\\', '/'))
    return f'Assets/Sources/OriginalAssets/{rel}'


def anim_for_template(typeId):
    """typeId -> (imageMap, frame0) via animation a{id}Stand|Main|*; else (None,0)."""
    pref = ['a%dStand' % typeId, 'a%dMain' % typeId]
    for name in pref:
        if name in _anim:
            return _resolve_anim(name)
    base = 'a%d' % typeId
    for name in _anim:
        if name.startswith(base) and (len(name) == len(base) or not name[len(base)].isdigit()):
            return _resolve_anim(name)
    return None, 0


def _resolve_anim(name):
    a = _anim[name]
    frames = a.get('frames', '')
    f0 = int(frames.split()[0]) if frames.split() else 0
    return a.get('imageMap', ''), f0


def resolve_cell(c):
    """parse_layers cell -> {empty} or {empty:False, frame, img}."""
    if c is None:
        return dict(empty=True)
    if 'db' in c:                      # animated tile datablock -> its imageMap, frame0
        img, f0 = _resolve_anim(c['db']) if c['db'] in _anim else ('', 0)
        return dict(empty=False, frame=f0, img=img or '')
    # image cell (img or img2) -> just use img + frame; 'code' ignored for render
    return dict(empty=False, frame=int(c.get('frame', 0)), img=c.get('img', ''))


def convert_imagemaps():
    maps = [dict(name=n, png=unity_png(v['png']), cellW=v['cellW'], cellH=v['cellH'],
                 mode=v.get('mode', 'cell')) for n, v in _imap.items()]
    json.dump(maps, open(os.path.join(GEN, 'imagemaps.json'), 'w'), indent=1)
    return len(maps)


def convert_animations():
    anims = []
    for name, v in _anim.items():
        frames = [int(x) for x in v['frames'].split()] if v.get('frames') else []
        anims.append(dict(name=name, imageMap=v.get('imageMap', ''), frames=frames,
                          cycle=int(v.get('cycle', '0') or 0), time=float(v.get('time', '0') or 0)))
    json.dump(anims, open(os.path.join(GEN, 'animations.json'), 'w'), indent=1)
    return len(anims)


def _is_collision_img(img):
    m = _imap.get(img)
    return bool(m and 'Collisions' in m['png'])


def convert_tiles():
    """Use ENGINE tile dumps (tiles_e.json): sparse cells, background/collision flags,
    animated cells resolved to imageMap+frame0."""
    d = os.path.join(GEN, 'tiles'); os.makedirs(d, exist_ok=True)
    n = 0
    for f in sorted(os.listdir(OUT)):
        if not f.endswith('.tiles_e.json'):
            continue
        data = json.load(open(os.path.join(OUT, f), encoding='utf-8'))
        layers = []
        for L in data['layers']:
            cells = []
            collision = False
            for c in L['cells']:
                img = c.get('img'); frame = c.get('frame', 0)
                if c['kind'] == 'animated':
                    img, frame = _resolve_anim(c['anim']) if c.get('anim') in _anim else ('', 0)
                if not img:
                    continue
                if _is_collision_img(img):
                    collision = True
                cells.append(dict(x=c['x'], y=c['y'], img=img, frame=frame))
            layers.append(dict(order=L['order'], cols=L['cols'], rows=L['rows'],
                               tileW=L['tileW'], tileH=L['tileH'],
                               background=bool(L['background']), collision=collision,
                               cells=cells))
        out = dict(file=data['level'], scene=data['scene'], layers=layers)
        json.dump(out, open(os.path.join(d, data['level'] + '.json'), 'w'))
        n += 1
    return n


def convert_objects():
    d = os.path.join(GEN, 'objects'); os.makedirs(d, exist_ok=True)
    tiles_dir = os.path.join(GEN, 'tiles')
    n = 0
    for f in sorted(os.listdir(OUT)):
        if not f.endswith('.objects.json'):
            continue
        data = json.load(open(os.path.join(OUT, f), encoding='utf-8'))
        base = data['level']
        # scene size from the matching tiles json
        tpath = os.path.join(tiles_dir, base + '.json')
        scene = json.load(open(tpath))['scene'] if os.path.exists(tpath) else [0, 0]
        objs = []
        for o in data['objects']:
            img, frame = anim_for_template(o['typeId']) if o['typeId'] else (None, 0)
            objs.append(dict(typeId=o['typeId'] or 0, template=o.get('template') or '',
                             x=o['x'], y=o['y'], sx=o['sx'], sy=o['sy'],
                             layer=o['layer'] if isinstance(o['layer'], int) else 0,
                             img=img or '', frame=frame,
                             isPlayer=bool(o.get('isPlayer')), isPortal=bool(o.get('isPortal'))))
        json.dump(dict(level=base, scene=scene, objects=objs),
                  open(os.path.join(d, base + '.json'), 'w'))
        n += 1
    return n


def main():
    os.makedirs(GEN, exist_ok=True)
    load_src()
    m = convert_imagemaps(); a = convert_animations()
    t = convert_tiles(); o = convert_objects()
    print(f'Wrote to GeneratedData: imagemaps={m} animations={a} tileLevels={t} objectLevels={o}')


if __name__ == '__main__':
    main()
