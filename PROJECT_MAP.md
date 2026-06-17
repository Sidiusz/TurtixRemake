# Project Map

## Root
- `CLAUDE.md` - project rules
- `main.cs` - original TGE bootstrap (restored)
- `Turtix.exe` - compiled game binary (TGB 1.1.0 / TGE 1.4.2)
- `OpenAL32.dll`, `awstats.dll`, `glu2d3d.dll`, `htmlayout.dll`, `opengl2d3d.dll`, `unicows.dll` - engine DLLs
- `Uninstall.exe`, `Uninstall.dat`, `unins000.exe`, `unins000.dat` - installer leftovers
- `license.txt`, `manifest.xml`, `partner.ini`, `registrator.ini` - metadata

## Content/
- `Main.cs` - entry script (loads prefs/audio, creates canvas, shows logos)
- `Game.cs` - main game logic (`startLoading`, `plrLevel`, level flow)
- `Audio.cs`, `Screenshot.cs` - audio/screenshot helpers
- `Preferences/DefaultPrefs.cs`, `Preferences/Prefs.cs` - `$pref::*` variables
- `Datablocks/ParticleDatablocks.cs`, `AudioDatablocks.cs`, `Datablocks.cs`, `AnimDatablocks.cs` - decompiled asset definitions
- `Gui/Profiles.cs`, `*.gui` - UI profiles and dialog definitions
- `Graphics/Characters/`, `Graphics/Worlds/`, `Graphics/...` - original sprite/background/FX PNGs
- `Language/` - menu/help/loading JPGs
- `Levels/Level_W*_*.tille` - binary level files
- `Game.dat` - binary object templates (ChunkFile)
- `Highscore.dat`, `Profile.dat` - binary save data

## Mod Workspace/
- `Unity Project/TurtixRemake/` - Unity 6 Netcode project
  - `Assets/Sources/` - generated importer scripts, parsed JSON, regenerated assets
  - `Assets/Sources/OriginalAssets/Graphics/` - mirrored PNGs (377, = Content/Graphics)
  - `Assets/Sources/GeneratedData/` - Unity JSON: imagemaps, animations, tiles/, objects/
  - `Assets/Sources/Editor/TurtixLevelImporter.cs` - Editor window: builds tile layers + objects (Y-flip, markers). BG = world-fixed tiled (engine 1:1): tileSize=cell*4, panY=-495, clouds(_C_) auto-pan 10px/s, no parallax. Bakes SpriteAnimator clips from animations.json: objects get default anim (img->anim reverse map), player gets all template state anims (a176Stand/Move/Jump...).
  - `Assets/Sources/Runtime/ParallaxLayer.cs` - world-fixed tiled bg layer (NOT parallax): scales cell*4, Tiled+wrap across scene, panX/Y offset, autoScrollX for clouds
  - `Assets/Sources/Runtime/SpriteAnimator.cs` - runtime frame-cycling animator (engine t2dAnimationDatablock 1:1): named clips (frames/fps/loop), Play(name), deterministic for coop. Clips baked by importer.
  - `Assets/Sources/Scenes/` - imported .unity scenes
  - Packages: Netcode for GameObjects 2.12.0 (coop), 2d.sprite, tilemap. Unity 6000.4.6f1.
- `Tools/` - reverse-engineering helpers
  - `TILLE_FORMAT.md` - decoded binary spec for `.tille` + `Game.dat`
  - `parse_tille.py` - tille tile-layer + Game.dat parser -> `out/*.json`
  - `parse_datablocks.py` - imageMap/anim datablocks -> `out/imagemaps.json`,`out/animations.json`
  - `analyze_objects.py` - dumps annotated hex of the .tille object/script region (crack helper)
  - `crack_objects.py` - object-record crack helper (anchors objdump positions in binary)
  - `build_objects.py` - fuses objdump + binary typeIds -> `out/Level_*.objects.json` (all 60)
  - `parse_layers.py` - binary tile-layer parser (DEPRECATED: gave garbage; engine dump used instead)
  - `parse_tiles_engine.py` - parses engine tile dump -> `out/Level_*.tiles_e.json` (8 layers, bg/collision)
  - `preview_level.py` - renders parsed tiles to PNG (sanity check vs Unity)
  - `prepare_unity_data.py` - resolves engine tiles + objects to Unity JSON under Assets/Sources/GeneratedData
  - `run_dump_loop.ps1` - launches Turtix repeatedly until all 60 tile dumps exist (engine crashes ~15/run)
  - `dump_main.cs` - engine dumper: objects (objdump.txt) + tiles via getTileType (tiles_engine.txt), resumable
  - `dump_inspect.cs` - loads W1_01, dumps camera (Extent/zoom) + every t2dTileLayer's AutoPan/PanPos/Wrap/TileSize + camera sweep (pan-vs-cam/time) -> console.log. Swap into root main.cs to run.
  - NOTE: game runnable directly (Start-Process Turtix.exe, windowed); swap dumper into root main.cs first
  - `out/Level_*.objdump.txt` - engine ground-truth per-object dump (class/pos/size/layer/flip)
  - `out/Level_*.objects.json` - final placed objects: typeId/template/x/y/size/layer/flip/isPlayer
  - `out/Level_*.tiles_e.json` - parsed engine tiles (per layer: order/cols/rows/tileW/H/bg/collision/cells)
  - `probe.py` - flat byte tokenizer (header/Game.dat ok, drifts on tile grid)
  - `run_dump.bat` - one-click: swap dumper -> clear .dso -> launch -> restore bootstrap
  - `main.cs.root_backup` - the REAL 4-line bootstrap (restore target)
  - `Game.dat_strings.txt` - extracted strings
- `Logs/` - engine console logs and run artifacts

## Heavy files (>300 LOC)
- `Content/Game.cs`
- `Content/Datablocks/Datablocks.cs`
- `Content/Datablocks/AnimDatablocks.cs`
- `Content/Gui/*.gui` (dialog definitions, many are large)
