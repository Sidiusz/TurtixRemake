# Handoff (Opus -> Sonnet)

## Done this session
- Cracked binary spec -> `TILLE_FORMAT.md`. All u32 LE, str = u8 len + ASCII.
  - **Game.dat**: fully decoded (count, per-template id/name/img/grid/fields). HIGH conf.
  - **.tille header + tile layers + palette**: decoded. HIGH conf.
  - **.tille placed-object layer**: NOT cracked (floats x/y/rot/scale + field block). The 20% left.
- Found root cause of dump hang: `saveScene()` invalid + `initializeData()` inits joystick/
  DirectInput/audio = hang. Fixed -> `dump_main.cs` v2 uses `scene.save(file)` + minimal init.
- Engine truth: `plrLevel`/`plrGame`/`createLevel` are CUSTOM Turtix C++. `.tille`/`Game.dat`
  are proprietary. Stock TGB/Torque2D source does NOT decode them. Drop point 3.
- Built `parse_tille.py`: tile-layer extraction WORKS on 59/60 levels -> `out/*.tiles.json`.
  - Cell grammar locked: empty=u8 0x00; filled=u8 0x01,u8 pad,u32 frame,str img. Tile=128px.
  - Filter: real layer = cols*128==sceneW & rows*128==sceneH (drops scanner false-hits).
  - W3_05 = 0 layers (edge case, layer order/offset differs — investigate later).
  - Game.dat: header (id/name/img/grid) decodes; FIELD block drifts (only 1st record) =
    same variable grammar as the .tille object layer. ONE crack unlocks both.

## Field-block grammar (partial crack)
- Dynamic fields = `[u32 tag][str key][str value]`, VERIFIED clean on a176 (%life=0,
  Life=%life, %scoreForLife=50000, ...). tag values seen: 0x1f, 0x20, 0x47.
- BUT value type varies by tag (some ints, not strings) + a leading console/connection
  block (onCreate + 9-int table) precedes dynamic fields. Full crack = many hrs, error-prone.
- Same encoder used by .tille object layer. Game.dat records: a1033=28B(0 fields, clean),
  a176=3084B(21 fields, big). Record bounds confirmed via id/name scan.

## Blocked on USER (engine dump = correct ground truth in ~1 min)
- Turtix.exe is GUI+OpenGL: CANNOT run headless (hung twice, no output). Needs a real DISPLAY.
- READY: dumper is now v5 (loops ALL 60 levels) + `Tools/run_dump.bat` one-click runner.
  USER just double-clicks `Mod Workspace/Tools/run_dump.bat` -> window runs ~20-40s, quits,
  bootstrap auto-restored. Collect: console.log (OBJDUMP per level) + out/Level_W*_*.cs (60 scenes).
- v5 fixes vs v4: execs ALL guis (sceneWindow2D etc were undefined) + inits Profile/Highscore,
  still SKIPS activateDirectInput/enableJoystick (the original hang cause).
- Engine loads stale `main.cs.dso` over edited `main.cs`; run_dump.bat deletes .dso before+after.
- STOP trying to crack the field block blind / re-run headless. Wait for the dump.

## Dumper findings (v5 runs)
- scene.save() output (.cs) has transforms+physics but NO identity (no imageMap/anim). DROPPED.
- objdump fields imageMap/animationName also read EMPTY on live t2dAnimatedSprite.
- Identity = the AnimationDatablock: placed obj refs a name like `a811Main` (t2dAnimationDatablock)
  -> imageMap `i23982` -> PNG `~/Graphics/Worlds/Bug_01` (cell 48x48). See AnimDatablocks.cs/Datablocks.cs.
- dumper now probes getAnimationName() + getDataBlock().getName() to recover that name. Re-run pending.
- Engine instability: ~5-7 levels per run then exits (no specific bad level). dumper is RESUMABLE
  (skips levels with existing .objdump.txt) + crash-immune (.attempt marker). Re-run a few times if needed.
- Each launch truncates console.log; multiple double-clicks spawn zombie procs that lock the log
  (killed 4). RUN run_dump.bat ONCE per launch.

## Object-layer crack progress (analyze_objects.py, W1_01)
- Region [8 : layer0] = scene onCreate/onEndLevel script block THEN placed objects.
- Scene block: `u32 fieldCount=2`, entries `[u32 tag][str key][str value]` tags 0x1f/0x1c/0x22;
  counters $Diamonds/$Enemies/$Secrets(+All)=0, ShowFireball=0, onEndLevel handler.
- Placed objects carry a NAME (e.g. "End_Level" @ x=2528,y=352, "a811Main"), image ref
  ("i23930","i4","i24047"), then ~5 floats (scale/rot, many 1.0=0x3f800000).
- Big repeating run `01 00 [u32] 02 "i7" 00` from ~off568 = an object grid the tile-parser
  skipped (dims != scene). Likely collectibles/diamonds laid on a grid. NOT byte-locked yet.
- Engine dump makes full byte-crack unnecessary; use it to label these records.

## Unity tile importer spec (READY - no engine needed)
Inputs: out/Level_*.tiles.json + out/imagemaps.json (286 maps, 0 missing PNGs).
Per layer, per filled cell [frame, imgName]:
- imgName -> imagemaps.json {png, cellW, cellH}.
- slice png: cols = imgW/cellW; frame -> col=frame%cols, row=frame/cols.
- tile grid spacing = 128px. cell (cx,cy) centered at (cx*128+64, +64).
  NOTE cellW often 96 != 128: center sprite in the 128 cell.
- Unity Y up: unityRow = (rows-1-cy). Origin bottom-left.
- layer 0 = back; use sorting layer / Z for draw order.
Animations later: out/animations.json (imageMap + frames).

## Next
1. Build Unity tile importer from spec above (Sonnet, in progress).
2. WAIT for user engine dump -> map object layer + Game.dat field block.
3. Finish parse_tille.py object section; validate clean-EOF all 60 levels.
4. Object prefabs by image id + behaviors from Game.dat templates.
