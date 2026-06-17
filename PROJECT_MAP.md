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
  - `Assets/Sources/OriginalAssets/` - mirrored copy of `Content/Graphics` + `Content/Audio` (if copied)
  - `Assets/Sources/Editor/TurtixLevelImporter.cs` - Editor window for level import
  - `Assets/Sources/Scenes/` - imported .unity scenes
- `Tools/` - reverse-engineering helpers
  - `TILLE_FORMAT.md` - decoded binary spec for `.tille` + `Game.dat`
  - `parse_tille.py` - tille tile-layer + Game.dat parser -> `out/*.json`
  - `parse_datablocks.py` - imageMap/anim datablocks -> `out/imagemaps.json`,`out/animations.json`
  - `analyze_objects.py` - dumps annotated hex of the .tille object/script region (crack helper)
  - `probe.py` - flat byte tokenizer (header/Game.dat ok, drifts on tile grid)
  - `dump_main.cs` - engine dumper v5 (loops ALL 60 levels; writes readable `.cs` per level + objdump to console.log)
  - `run_dump.bat` - one-click: swap dumper -> clear .dso -> launch -> restore bootstrap
  - `main.cs.root_backup` - the REAL 4-line bootstrap (restore target)
  - `Game.dat_strings.txt` - extracted strings
- `Logs/` - engine console logs and run artifacts

## Heavy files (>300 LOC)
- `Content/Game.cs`
- `Content/Datablocks/Datablocks.cs`
- `Content/Datablocks/AnimDatablocks.cs`
- `Content/Gui/*.gui` (dialog definitions, many are large)
