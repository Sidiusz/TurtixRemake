@echo off
REM ============================================================
REM Turtix scene dumper runner (v5) -- double-click this file.
REM Swaps in the dumper, launches the game (a window opens, runs
REM ~20-40s and quits), then restores the real bootstrap.
REM Output: console.log + Mod Workspace\Tools\out\Level_W*_*.cs
REM ============================================================
setlocal
cd /d "%~dp0\..\.."
echo [dump] root = %CD%

echo [dump] installing dumper as main.cs ...
copy /y "Mod Workspace\Tools\dump_main.cs" "main.cs" >nul

echo [dump] clearing stale main.cs.dso ...
if exist "main.cs.dso" del /q "main.cs.dso"

echo [dump] launching Turtix.exe (wait for it to close) ...
start "" /wait "Turtix.exe"

echo [dump] restoring real bootstrap main.cs ...
copy /y "Mod Workspace\Tools\main.cs.root_backup" "main.cs" >nul
if exist "main.cs.dso" del /q "main.cs.dso"

echo [dump] DONE. Check console.log and Mod Workspace\Tools\out\Level_W*_*.cs
pause
