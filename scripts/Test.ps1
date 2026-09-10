param([switch]$Speech, [switch]$CodexFixture, [ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_ROOT = Join-Path $projectRoot '.tools\dotnet'
$dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
$test = Join-Path $projectRoot "tests\LittleGuy3000.Tests\bin\$Configuration\net10.0-windows\LittleGuy3000.Tests.dll"
& $dotnet $test
if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
& $dotnet $test --transport
if ($LASTEXITCODE -ne 0) { throw 'Transport checks failed.' }
if ($Speech) {
  $env:LITTLEGUY_SPEECH_MODEL = Join-Path $projectRoot '.tools\models\ggml-large-v3-turbo-q5_0.bin'
  & $dotnet $test --local-speech
  if ($LASTEXITCODE -ne 0) { throw 'Offline Whisper speech test failed.' }
}
if ($CodexFixture) { & python (Join-Path $PSScriptRoot 'Verify-Codex.py'); if ($LASTEXITCODE -ne 0) { throw 'Codex fixture failed.' } }
