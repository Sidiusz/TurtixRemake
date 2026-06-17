"""Render a parsed level to a PNG so we can SEE if the tile/object parse is correct
(independent of Unity). Composites tile layers (bottom->top) + objects.

Usage: python preview_level.py W1_01
"""
import sys, json, os
from PIL import Image

ROOT = r'D:\Torrents\Turtix'
OUT = os.path.join(ROOT, 'Mod Workspace', 'Tools', 'out')


def load_imap():
    return json.load(open(os.path.join(OUT, 'imagemaps.json'), encoding='utf-8'))


def load_anim():
    return json.load(open(os.path.join(OUT, 'animations.json'), encoding='utf-8'))


_sheets = {}
def cell_sprite(imap, img, frame):
    m = imap.get(img)
    if not m:
        return None
    png = os.path.join(ROOT, m['png'].replace('/', os.sep))
    if not os.path.exists(png):
        return None
    if png not in _sheets:
        _sheets[png] = Image.open(png).convert('RGBA')
    sheet = _sheets[png]
    cw, ch = m['cellW'], m['cellH']
    cols = max(1, sheet.width // cw)
    fx = (frame % cols) * cw
    fy = (frame // cols) * ch
    if fy + ch > sheet.height:
        return None
    return sheet.crop((fx, fy, fx + cw, fy + ch))


def main():
    base = 'Level_' + sys.argv[1] if not sys.argv[1].startswith('Level_') else sys.argv[1]
    imap = load_imap(); anim = load_anim()
    L = json.load(open(os.path.join(OUT, base + '.layers.json'), encoding='utf-8'))
    sw, sh = L['scene']
    canvas = Image.new('RGBA', (sw, sh), (30, 30, 40, 255))

    for li, layer in enumerate(L['layers']):
        tw, th = layer['tile']; c = layer['cols']; r = layer['rows']
        for i, cell in enumerate(layer['cells']):
            if cell is None:
                continue
            cx, cy = i % c, i // c
            img = cell.get('img')
            frame = cell.get('frame', 0)
            if not img and 'db' in cell:
                a = anim.get(cell['db'])
                if a:
                    img = a.get('imageMap'); fr = a.get('frames', '').split()
                    frame = int(fr[0]) if fr else 0
            sp = cell_sprite(imap, img, frame) if img else None
            if sp is None:
                continue
            # cell top-left in pixels; layer rows top->down as stored
            px = cx * tw
            py = cy * th
            canvas.alpha_composite(sp, (px, py))
    outp = os.path.join(OUT, base + '.preview_tiles.png')
    canvas.save(outp)
    print('wrote', outp, canvas.size)

    # also per-layer previews to compare
    for li, layer in enumerate(L['layers']):
        tw, th = layer['tile']; c = layer['cols']; r = layer['rows']
        cv = Image.new('RGBA', (sw, sh), (0, 0, 0, 255))
        for i, cell in enumerate(layer['cells']):
            if cell is None:
                continue
            cx, cy = i % c, i // c
            img = cell.get('img'); frame = cell.get('frame', 0)
            if not img and 'db' in cell:
                a = anim.get(cell['db'])
                if a:
                    img = a.get('imageMap'); fr = a.get('frames', '').split(); frame = int(fr[0]) if fr else 0
            sp = cell_sprite(imap, img, frame) if img else None
            if sp:
                cv.alpha_composite(sp, (cx * tw, cy * th))
        cv.save(os.path.join(OUT, f'{base}.layer{li}.png'))
    print('wrote per-layer previews')


if __name__ == '__main__':
    main()
