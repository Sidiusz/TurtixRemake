# Run the Turtix dumper repeatedly until all 60 tile dumps exist (engine crashes mid-run; resumable).
Set-Location "D:\Torrents\Turtix"
$outDir = "Mod Workspace\Tools\out"
$exe = ".\Turtix.exe"
$target = 60

function TileCount { (Get-ChildItem "$outDir\*.tiles_engine.txt" -ErrorAction SilentlyContinue).Count }

for ($attempt = 1; $attempt -le 14; $attempt++) {
    $have = TileCount
    if ($have -ge $target) { break }
    Get-ChildItem "$outDir\*.attempt" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    if (Test-Path "main.cs.dso") { Remove-Item "main.cs.dso" -Force }
    Start-Process -FilePath $exe
    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        if (-not (Get-Process Turtix -ErrorAction SilentlyContinue)) { break }
        if ((TileCount) -ge $target) { break }
    }
    if (Get-Process Turtix -ErrorAction SilentlyContinue) { Stop-Process -Name Turtix -Force }
    Start-Sleep -Milliseconds 600
    $now = TileCount
    Write-Output ("attempt " + $attempt + ": now have " + $now + " tile dumps")
}
Write-Output ("DONE. tile dumps = " + (TileCount))
