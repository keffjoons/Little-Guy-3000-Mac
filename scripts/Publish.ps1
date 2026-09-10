param([switch]$Zip)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$publishDotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }
$output = Join-Path $projectRoot 'artifacts\LittleGuy3000-win-x64'
& $publishDotnet publish (Join-Path $projectRoot 'src\LittleGuy3000.Desktop\LittleGuy3000.Desktop.csproj') -c Release -r win-x64 --self-contained true -p:Platform=x64 -o $output --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed ($LASTEXITCODE)." }
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'PRIVACY.md'), (Join-Path $projectRoot 'TESTING.md'), (Join-Path $projectRoot 'IMPLEMENTATION_PLAN.md'), (Join-Path $projectRoot 'CODEX_INTEGRATION.md'), (Join-Path $projectRoot 'SCREEN_CAPTURE.md'), (Join-Path $projectRoot 'OVERLAY_COORDINATES.md') -Destination $output
if ($Zip) {
  $archive = Join-Path $projectRoot 'artifacts\LittleGuy3000-0.1.13-win-x64.zip'
  Compress-Archive -Path "$output\*" -DestinationPath $archive -Force
  Write-Output $archive
}
Write-Output "Run $output\LittleGuy3000.Desktop.exe"
