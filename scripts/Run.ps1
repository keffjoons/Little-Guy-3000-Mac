param([switch]$SelfTest, [switch]$Connect, [switch]$Portable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_ROOT = Join-Path $projectRoot '.tools\dotnet'
$env:LITTLEGUY_DATA_DIR = Join-Path $projectRoot '.local\app'
$codexCommand = Get-Command codex -ErrorAction SilentlyContinue
if ($codexCommand) { $env:LITTLEGUY_CODEX_EXE = $codexCommand.Source }
$executable = Join-Path $projectRoot 'src\LittleGuy3000.Desktop\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\LittleGuy3000.Desktop.exe'
if ($Portable) { $executable = Join-Path $projectRoot 'artifacts\LittleGuy3000-win-x64\LittleGuy3000.Desktop.exe' }
if (!(Test-Path -LiteralPath $executable)) { throw 'Build the application first using scripts\Build.ps1.' }
$startArguments = @{ FilePath = $executable; WorkingDirectory = $projectRoot; PassThru = $true; WindowStyle = 'Hidden' }
if ($SelfTest) { $startArguments.ArgumentList = '--self-test' }
elseif ($Connect) { $startArguments.ArgumentList = '--connect' }
$applicationProcess = Start-Process @startArguments
Write-Output "Little Guy 3000 started (process $($applicationProcess.Id))."
