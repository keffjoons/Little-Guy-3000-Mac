param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug', [switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$buildDotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }
Push-Location $projectRoot
try {
  $buildArgs = @('build','LittleGuy3000.sln','-c',$Configuration,'-p:Platform=x64','--nologo')
  if ($NoRestore) { $buildArgs += '--no-restore' }
  & $buildDotnet @buildArgs
  if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
} finally { Pop-Location }
