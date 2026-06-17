// ============================================================
// Turtix scene dumper v5  -- RUN INTERACTIVELY ON A REAL DESKTOP
// (headless/agent runs hang: GUI + OpenGL needs a display window)
//
// SETUP:
//   1. backup real root main.cs  (saved: Tools/main.cs.root_backup)
//   2. copy THIS file over root  main.cs
//   3. double-click Turtix.exe   (a window opens, runs ~20-40s, quits)
//   4. read output:  console.log (game root)  +  Mod Workspace/Tools/out/Level_W*_*.cs (all 60)
//   5. restore real main.cs afterwards
//
// v5: loops ALL 60 levels (W1..W5 x 01..12) in one run -> one click = full ground truth.
//     Per level: new plrLevel -> createLevel -> objdump + scene.save -> clear/delete.
// v4 fix: MUST call setModPaths + MUST call dumpEntry() at end (engine does NOT
//         auto-call onStart). Without these the game won't launch.
// ============================================================

setLogMode(6);              // force console.log (overwrite + flush each line)
setModPaths("Content");     // CRITICAL: path root, same as original bootstrap

function dumpEntry()
{
   echo("=== DUMP START ===");
   exec("Content/Preferences/DefaultPrefs.cs");
   exec("Content/Preferences/Prefs.cs");
   exec("Content/Audio.cs");
   exec("Content/Screenshot.cs");
   setRandomSeed();

   $pref::Video::fullScreen = 0;
   videoSetGammaCorrection($pref::OpenGL::gammaCorrection);
   if (!createCanvas("TurtixDump")) { error("DUMP: canvas failed"); quit(); }
   initializeOpenAL();

   exec("Content/Gui/Profiles.cs");
   exec("Content/Game.cs");

   // exec every GUI so all referenced widgets exist (sceneWindow2D, Score,
   // Profiles*Info labels, MainScreenGui, etc.) -- mirrors initializeData().
   echo("DUMP: guis");
   exec("Content/Gui/MainMenu.gui");
   exec("Content/Gui/MainMenuStoryDlg.gui");
   exec("Content/Gui/MainMenuArcadeDlg.gui");
   exec("Content/Gui/MainMenuHelpDlg.gui");
   exec("Content/Gui/MainMenuOptionsDlg.gui");
   exec("Content/Gui/MainMenuProfilesDlg.gui");
   exec("Content/Gui/MainMenuHighscoreDlg.gui");
   exec("Content/Gui/MainMenuQuitDlg.gui");
   exec("Content/Gui/MainMenuProfileAddDlg.gui");
   exec("Content/Gui/MainMenuProfileRemoveDlg.gui");
   exec("Content/Gui/Loading.gui");
   exec("Content/Gui/MainScreen.gui");
   exec("Content/Gui/PauseMenuDlg.gui");
   exec("Content/Gui/PauseMenuOptionsDlg.gui");
   exec("Content/Gui/PauseMenuHelpDlg.gui");
   exec("Content/Gui/PauseMenuQuitDlg.gui");
   exec("Content/Gui/MessageDlg.gui");
   exec("Content/Gui/HowToPlayDlg.gui");
   exec("Content/Gui/GameOverMenuDlg.gui");
   exec("Content/Gui/GameOverMenuQuitDlg.gui");
   exec("Content/Gui/GameIntro.gui");
   exec("Content/Gui/GameOutro.gui");

   echo("DUMP: datablocks");
   exec("Content/Datablocks/ParticleDatablocks.cs");
   exec("Content/Datablocks/AudioDatablocks.cs");
   exec("Content/Datablocks/Datablocks.cs");
   exec("Content/Datablocks/AnimDatablocks.cs");

   // trimmed initializeGame(): scene + window + game, but NO DirectInput/joystick
   // (activateDirectInput/enableJoystick = original hang cause).
   new t2dSceneGraph(scene);
   sceneWindow2D.setSceneGraph(scene);
   $Game = new plrGame();
   $Game.init();
   $Profile = new plrProfile();
   $Profile.init();
   $Profile.setLifeDifficulty(6, 4, 2);
   $Highscore = new plrHighscore();
   $Highscore.init();

   schedule(1500, 0, "dumpAll");   // let GL + textures settle, then loop all levels
}

function dumpOneLevel(%world, %level)
{
   if (%level < 10)
      %path = "Content/Levels/Level_W" @ %world @ "_0" @ %level @ ".tille";
   else
      %path = "Content/Levels/Level_W" @ %world @ "_" @ %level @ ".tille";

   if (!isFile(%path)) { echo("SKIP (no file): " @ %path); return; }

   %base = "Level_W" @ %world @ "_" @ (%level < 10 ? "0" @ %level : %level);
   %odump = "Mod Workspace/Tools/out/" @ %base @ ".objdump.txt";
   %tiles = "Mod Workspace/Tools/out/" @ %base @ ".tiles_engine.txt";
   %att = "Mod Workspace/Tools/out/" @ %base @ ".attempt";
   %needObj = !isFile(%odump);
   %needTiles = !isFile(%tiles);
   // resume: skip only when BOTH artifacts already exist
   if (!%needObj && !%needTiles)
   {
      echo("SKIP (already dumped): " @ %base);
      return;
   }
   // crash-immunity: if attempted before but never produced output, it crashed -> skip it
   if (isFile(%att))
   {
      echo("SKIP (known-bad, crashed before): " @ %base);
      return;
   }
   // claim attempt BEFORE loading, so a hard crash here is remembered next run
   %fo = new FileObject();
   %fo.openForWrite(%att);
   %fo.writeLine("attempting");
   %fo.close();
   %fo.delete();

   echo("=== LEVEL " @ %base @ " ===");

   $LoadingWorld = %world;
   $LoadingLevel = %level;
   $Level = new plrLevel();
   $Level.setSceneWindow(sceneWindow2D);
   $Level.setSceneGraph(scene);
   $Level.createLevel(%path);

   if (%needObj) dumpObjects(%base);
   if (%needTiles) dumpTiles(%base);
   if (isFile(%att)) fileDelete(%att);

   $Level.clear();
   $Level.delete();
}

function dumpObjects(%base)
{
   %odump = "Mod Workspace/Tools/out/" @ %base @ ".objdump.txt";
   // identity dump -> per-level file (survives crashes; console.log is unreliable).
   // t2dAnimatedSprite identity lives in its ANIMATION DATABLOCK (e.g. a811Main ->
   // imageMap i23982 -> PNG). imageMap/animationName fields read empty, so probe the
   // real accessors: getAnimationName(), getDataBlock().getName(), plus field fallbacks.
   // cols: idx|class|name|pos|size|rot|layer|flipX|flipY|anim_get|db_name|f_AnimationName|f_imageMap|frame
   %n = scene.getCount();
   %od = new FileObject();
   %od.openForWrite(%odump);
   %od.writeLine("# " @ %base @ " count=" @ %n);
   %od.writeLine("# idx|class|name|pos|size|rot|layer|flipX|flipY|anim_get|db_name|f_AnimationName|f_imageMap|frame");
   for (%i = 0; %i < %n; %i++)
   {
      %o = scene.getObject(%i);
      %db = %o.getDataBlock();
      %dbn = isObject(%db) ? %db.getName() : "";
      %od.writeLine(%i @ "|" @ %o.getClassName() @ "|" @ %o.getName() @ "|"
         @ %o.getPosition() @ "|" @ %o.getSize() @ "|" @ %o.getRotation() @ "|"
         @ %o.Layer @ "|" @ %o.FlipX @ "|" @ %o.FlipY @ "|"
         @ %o.getAnimationName() @ "|" @ %dbn @ "|"
         @ %o.AnimationName @ "|" @ %o.imageMap @ "|" @ %o.frame);
   }
   %od.close();
   %od.delete();
   echo("=== OBJDUMP " @ %base @ " count=" @ %n @ " -> " @ %odump @ " ===");
}

// Export every t2dTileLayer's tiles via the engine. Writes out/<base>.tiles_engine.txt:
//   layer|<idx>|layerOrder=<L>|cols=<x>|rows=<y>|tile=<w>x<h>
//   <x>|<y>|<type>|<imageMap>|<frame>|<anim>
// Also dumps the FIRST layer's full member list to console.log (DIAGNOSTIC) so if any
// getter name is wrong we can read the real API and fix next run.
function dumpTiles(%base)
{
   %path = "Mod Workspace/Tools/out/" @ %base @ ".tiles_engine.txt";
   %fo = new FileObject();
   %fo.openForWrite(%path);
   %n = scene.getCount();
   %diag = 0;
   for (%i = 0; %i < %n; %i++)
   {
      %o = scene.getObject(%i);
      if (%o.getClassName() !$= "t2dTileLayer")
         continue;
      if (!%diag) { %diag = 1; echo("=== TILELAYER DUMP (API diagnostic) ==="); %o.dump(); echo("=== END TILELAYER DUMP ==="); }

      %cx = %o.getTileCountX();
      %cy = %o.getTileCountY();
      %fo.writeLine("layer|" @ %i @ "|layerOrder=" @ %o.Layer @ "|cols=" @ %cx @ "|rows=" @ %cy
                    @ "|size=" @ %o.getSize());
      for (%ty = 0; %ty < %cy; %ty++)
         for (%tx = 0; %tx < %cx; %tx++)
         {
            %type = %o.getTileType(%tx, %ty);
            %img = %o.getStaticTileImageMap(%tx, %ty);
            %frame = %o.getStaticTileFrame(%tx, %ty);
            %anim = %o.getAnimatedTileAnimationName(%tx, %ty);
            if (%img !$= "" || %anim !$= "" || (%type !$= "" && %type !$= "empty"))
               %fo.writeLine(%tx @ "|" @ %ty @ "|" @ %type @ "|" @ %img @ "|" @ %frame @ "|" @ %anim);
         }
   }
   %fo.close();
   %fo.delete();
   echo("=== TILES " @ %base @ " -> " @ %path @ " ===");
}

function dumpAll()
{
   // objects (objdump) + engine tiles (tiles_engine) for all 60. Resumable per-artifact.
   for (%w = 1; %w <= 5; %w++)
      for (%l = 1; %l <= 12; %l++)
         dumpOneLevel(%w, %l);

   echo("=== DUMP DONE (objects + engine tiles, all levels) ===");
   quit();
}

dumpEntry();   // engine does NOT auto-run; invoke explicitly
