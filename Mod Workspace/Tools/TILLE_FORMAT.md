# Turtix binary formats (reverse-engineered)

All integers **u32 little-endian**. Strings = **u8 length prefix + ASCII** (no NUL).
Custom Turtix C++ classes (`plrLevel.createLevel`, `plrGame`, etc.) write these — NOT
stock TGB. No public source decodes them; spec below is from byte analysis. CONFIDENCE noted.

## Game.dat  (object templates) — CONFIDENCE: high
```
u32 objectCount            // e.g. 126
repeat objectCount:
  u32 id                   // numeric id, e.g. 1033, 176
  str name                 // "a"+id, e.g. "a1033","a176"
  ... body (see below)
```
Body of a template:
```
u32 imgW   u32 imgH        // e.g. 128,128
u32 a u32 b u32 c u32 d    // frame/grid params (cols,rows,fps,frameCount?) - MEANING UNSURE
u32 fieldCount             // 0 = no script fields (a1033). >0 = dynamic-field block follows
fields[fieldCount]:
  str key   str value      // e.g. "%life"="0", "%score"="0", "10000"...
  (plus a small int table before fields = onCreate event/connection list - UNSURE)
```
Example a1033: 128,128,17,17,17,17,0  -> simple 128x128 sprite, no fields.
Example a176 : 128,128,52,23,47,60 + "onCreate" handler + fields %life/%health/%score=0,
               %scoreForLife=50000, %hurtDisturber="25"/"35"/"50", HealthStep=1, 350...

## .tille  (level) — CONFIDENCE: medium (header+tiles high, object layer LOW)
```
u32 sceneW  u32 sceneH         // pixels, e.g. 1280 x 1152
-- scene script-field block --
u32 fieldCount
  per field: str key, then value/handler  (e.g. onCreate -> "ShowFireball"="0")
-- placed objects (sprites/enemies/items/player) --   <-- CRACKED (validated 42/42 on W1_01, all 60 ok)
  u32 objectCount
  per object:
    s32 typeId      // == a Game.dat template id (e.g. 553=diamond, 22144=End_Level portal, 176=PLAYER)
    s32 uid         // unique instance id, incrementing
    s32 x  s32 y    // world position in PIXELS, SIGNED (off-screen spawns are negative)
    [6+ bytes]      // simple objs: 6 zero bytes. special objs (portal/scroll) append a
                    //   dynamic-field block (onCollision/onComplete handlers, %text, etc) -> VARIABLE length
  typeId -> Game.dat template "a{id}" -> anim datablock "a{id}Main" (player uses a176Stand/Move/Jump..)
  -> imageMap -> PNG. Extractor: build_objects.py (fuses objdump positions + binary typeIds).
  NOTE positions are s32 here, NOT f32. Player template = 176 (exactly one per level).
-- tile layers --                              <-- HIGH CONFIDENCE
repeat per layer:
  u32 tileW  u32 tileH          // e.g. 128,128
  u32 cols   u32 rows           // e.g. 10 x 9
  cols*rows cells, row-major (VERIFIED on 59/60 levels):
    empty cell : u8 0x00                                     // 1 byte
    filled cell: u8 0x01, u8 pad(0), u32 frameIndex, str img // e.g. frame 25, "i9"
  // tileW/tileH always 128. cols*128 == sceneW, rows*128 == sceneH.
-- trailer (image palette) --
u32 layerCount(?)               // e.g. 3
u32 imageCount                  // e.g. 13
imageCount strings              // "i24018","i10","i11",... tileset image refs
```
NOTE: order of "placed objects" vs "tile layers" may interleave per layer; verify against
an engine dump (see dump_main.cs).

## Tools
- `probe.py <file> [n]`  flat tokenizer (drifts on tile grid; good for header/Game.dat).
- `dump_main.cs`         USER runs in engine -> writes readable `.cs` per level to out/.
  ^ Best path: gives ground-truth field names+values, makes the object-layer RE unnecessary.

## Next steps
1. USER: run dump_main.cs, share one `out/Level_W1_01.cs` -> map binary object layer to fields.
2. Build `parse_tille.py` from confirmed spec; validate = parser reaches clean EOF on all 60 levels.
3. Unity importer reads parsed JSON (tiles -> Tilemap, objects -> prefabs by image id).
