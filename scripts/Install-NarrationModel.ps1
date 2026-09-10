param([string]$Destination = (Join-Path (Split-Path $PSScriptRoot -Parent) '.tools\models'))
$ErrorActionPreference = 'Stop'
$expectedHash = '0cfd5e79aab70a3d8c1a57dc639835110ddb32c9f5ff4fdd1f4db202ea43bb05'
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$modelPath = Join-Path $Destination 'kokoro.onnx'
if ((Test-Path -LiteralPath $modelPath) -and (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash -eq $expectedHash) {
    Write-Output "Verified local narration model: $modelPath"
    return
}
$partialPath = $modelPath + '.download'
try {
    Write-Output 'Downloading the 326 MB Kokoro neural voice model. Speech runs locally after installation.'
    Invoke-WebRequest -Uri 'https://github.com/Lyrcaxis/KokoroSharpBinaries/releases/download/v2.0.0/kokoro.onnx' -OutFile $partialPath
    if ((Get-FileHash -LiteralPath $partialPath -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Narration model integrity check failed. The model was not installed.' }
    Move-Item -LiteralPath $partialPath -Destination $modelPath -Force
    Write-Output "Verified local narration model: $modelPath"
} finally { if (Test-Path -LiteralPath $partialPath) { Remove-Item -LiteralPath $partialPath } }
