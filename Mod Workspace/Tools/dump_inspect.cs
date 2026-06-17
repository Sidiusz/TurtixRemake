// Focused inspector: load W1_01, dump FULL fields of the camera (sceneWindow2D) and every
// tile layer (parallax/scroll/repeat config) + the player. Single run -> console.log preserved.
setLogMode(6);
setModPaths("Content");

function dumpEntry()
{
   echo("=== INSPECT START ===");
   exec("Content/Preferences/DefaultPrefs.cs");
   exec("Content/Preferences/Prefs.cs");
   exec("Content/Audio.cs");
   exec("Content/Screenshot.cs");
   setRandomSeed();
   $pref::Video::fullScreen = 0;
   videoSetGammaCorrection($pref::OpenGL::gammaCorrection);
   if (!createCanvas("TurtixInspect")) { error("canvas failed"); quit(); }
   initializeOpenAL();
   exec("Content/Gui/Profiles.cs");
   exec("Content/Game.cs");
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
   exec("Content/Datablocks/ParticleDatablocks.cs");
   exec("Content/Datablocks/AudioDatablocks.cs");
   exec("Content/Datablocks/Datablocks.cs");
   exec("Content/Datablocks/AnimDatablocks.cs");

   new t2dSceneGraph(scene);
   sceneWindow2D.setSceneGraph(scene);
   $Game = new plrGame();
   $Game.init();
   $Profile = new plrProfile();
   $Profile.init();
   $Profile.setLifeDifficulty(6, 4, 2);
   $Highscore = new plrHighscore();
   $Highscore.init();

   schedule(1500, 0, "inspectNow");
}

function inspectNow()
{
   $LoadingWorld = 1; $LoadingLevel = 1;
   $Level = new plrLevel();
   $Level.setSceneWindow(sceneWindow2D);
   $Level.setSceneGraph(scene);
   $Level.createLevel("Content/Levels/Level_W1_01.tille");

   // let the level start so the camera mounts/parallax bind (plrLevel may set these on start)
   $LevelCreated = 1;
   if (isObject($Level)) { $Level.setSceneTime(); }
   $Level.update();

   echo("=== CAMERA sceneWindow2D ===");
   sceneWindow2D.dump();
   echo("=== CAMERA pos/size ===");
   echo("camPos=" @ sceneWindow2D.getCurrentCameraPosition());
   echo("camZoom=" @ sceneWindow2D.getCurrentCameraZoom());

   %n = scene.getCount();
   echo("=== SCENE count=" @ %n @ " ===");
   for (%i = 0; %i < %n; %i++)
   {
      %o = scene.getObject(%i);
      if (%o.getClassName() $= "t2dTileLayer")
      {
         echo("=== TILELAYER idx" @ %i @ " Layer=" @ %o.Layer @ " ===");
         %o.dump();
      }
   }
   echo("=== INSPECT DONE ===");
   quit();
}

dumpEntry();
