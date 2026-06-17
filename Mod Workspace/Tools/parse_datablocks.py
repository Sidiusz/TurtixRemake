"""
Parse Content/Datablocks/*.cs t2dImageMapDatablock + t2dAnimationDatablock
-> out/imagemaps.json , out/animations.json
Feeds the Unity importer: resolves tile/object image names (i7, i23930, a811Main...)
to real PNG path + cell size, and animations to frame lists.
"""
import re, json, os

CONTENT = "Content"
OUT = "Mod Workspace/Tools/out"

IMG_RE = re.compile(
    r'new t2dImageMapDatablock\((\w+)\)\s*\{(.*?)\};', re.S)
ANIM_RE = re.compile(
    r'new t2dAnimationDatablock\((\w+)\)\s*\{(.*?)\};', re.S)


def field(body, name):
    m = re.search(name + r'\s*=\s*"?([^";\n]+)"?\s*;', body)
    return m.group(1).strip() if m else None


def resolve_png(image_name):
    # "~/Graphics/Worlds/World_1/W1_Blockades" -> Content/Graphics/.../*.png
    p = image_name.replace("~/", "").replace("\\", "/")
    cand = os.path.join(CONTENT, p + ".png")
    return cand.replace("\\", "/")


def parse_imagemaps():
    out = {}
    for fn in ("Datablocks.cs",):
        txt = open(os.path.join(CONTENT, "Datablocks", fn), encoding="latin1").read()
        for name, body in IMG_RE.findall(txt):
            img = field(body, "imageName")
            out[name] = dict(
                png=resolve_png(img) if img else None,
                cellW=int(field(body, "cellWidth") or 0),
                cellH=int(field(body, "cellHeight") or 0),
                mode=field(body, "imageMode"),
            )
    return out


def parse_anims():
    out = {}
    txt = open(os.path.join(CONTENT, "Datablocks", "AnimDatablocks.cs"),
               encoding="latin1").read()
    for name, body in ANIM_RE.findall(txt):
        out[name] = dict(
            imageMap=field(body, "imageMap"),
            frames=field(body, "animationFrames"),
            cycle=field(body, "animationCycle"),
            time=field(body, "animationTime"),
        )
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    imgs = parse_imagemaps()
    anims = parse_anims()
    json.dump(imgs, open(os.path.join(OUT, "imagemaps.json"), "w"), indent=1)
    json.dump(anims, open(os.path.join(OUT, "animations.json"), "w"), indent=1)
    missing = [n for n, v in imgs.items() if v["png"] and not os.path.isfile(v["png"])]
    print(f"imagemaps={len(imgs)}  animations={len(anims)}  png_missing={len(missing)}")
    if missing:
        print("MISSING PNGs:", missing[:10])


if __name__ == "__main__":
    main()
