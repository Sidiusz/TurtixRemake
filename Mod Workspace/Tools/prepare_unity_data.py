"""Convert parser outputs to Unity-friendly JSON under Assets/sources/GeneratedData."""
import json, os, shutil, re

ROOT = r'D:\Torrents\Turtix'
OUT = os.path.join(ROOT, 'Mod Workspace', 'Tools', 'out')
GEN = os.path.join(ROOT, 'Mod Workspace', 'Unity Project', 'TurtixRemake', 'Assets', 'Sources', 'GeneratedData')

def unity_png(png_path):
    # Content/Graphics/... -> Assets/Sources/OriginalAssets/Graphics/...
    rel = re.sub(r'^Content/', '', png_path.replace('\\', '/'))
    return f'Assets/Sources/OriginalAssets/{rel}'


def convert_imagemaps():
    data = json.load(open(os.path.join(OUT, 'imagemaps.json'), encoding='utf-8'))
    maps = []
    for name, v in data.items():
        maps.append(dict(
            name=name,
            png=unity_png(v['png']),
            cellW=v['cellW'],
            cellH=v['cellH'],
            mode=v.get('mode', 'cell'),
        ))
    json.dump(maps, open(os.path.join(GEN, 'imagemaps.json'), 'w'), indent=2)
    return len(maps)


def convert_animations():
    data = json.load(open(os.path.join(OUT, 'animations.json'), encoding='utf-8'))
    anims = []
    for name, v in data.items():
        frames = [int(x) for x in v['frames'].split()] if v.get('frames') else []
        anims.append(dict(
            name=name,
            imageMap=v.get('imageMap', ''),
            frames=frames,
            cycle=int(v.get('cycle', '0') or 0),
            time=float(v.get('time', '0') or 0),
        ))
    json.dump(anims, open(os.path.join(GEN, 'animations.json'), 'w'), indent=2)
    return len(anims)


def convert_tiles():
    tiles_dir = os.path.join(GEN, 'tiles')
    os.makedirs(tiles_dir, exist_ok=True)
    count = 0
    for f in sorted(os.listdir(OUT)):
        if not f.endswith('.tille.tiles.json'):
            continue
        data = json.load(open(os.path.join(OUT, f), encoding='utf-8'))
        out_layers = []
        for L in data.get('raw_layers', []):
            cells = []
            for c in L['cells']:
                if c is None:
                    cells.append(dict(empty=True))
                else:
                    cells.append(dict(empty=False, frame=c[0], img=c[1]))
            out_layers.append(dict(
                tileW=L['tile'][0],
                tileH=L['tile'][1],
                cols=L['cols'],
                rows=L['rows'],
                cells=cells,
            ))
        out = dict(
            file=data['file'],
            scene=data['scene'],
            layers=out_layers,
        )
        out_name = f.replace('.tille.tiles.json', '.json')
        json.dump(out, open(os.path.join(tiles_dir, out_name), 'w'), indent=2)
        count += 1
    return count


def main():
    os.makedirs(GEN, exist_ok=True)
    m = convert_imagemaps()
    a = convert_animations()
    t = convert_tiles()
    print(f'Wrote to {GEN}: imagemaps={m} animations={a} tileLevels={t}')


if __name__ == '__main__':
    main()
