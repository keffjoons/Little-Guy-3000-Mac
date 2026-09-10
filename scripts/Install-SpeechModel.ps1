param([string]$Destination = (Join-Path (Split-Path $PSScriptRoot -Parent) '.tools\models'))
$ErrorActionPreference = 'Stop'
$modelName = 'ggml-large-v3-turbo-q5_0.bin'
$expectedHash = '394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2'
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$modelPath = Join-Path $Destination $modelName
if ((Test-Path -LiteralPath $modelPath) -and (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash -eq $expectedHash) {
    Write-Output "Verified offline speech model: $modelPath"
    return
}
$partialPath = $modelPath + '.download'
Write-Output 'Downloading the 574 MB Whisper model. No microphone audio is uploaded.'
try {
    Invoke-WebRequest -Uri "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/$modelName" -OutFile $partialPath
    if ((Get-FileHash -LiteralPath $partialPath -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Speech model integrity check failed. The model was not installed.' }
    Move-Item -LiteralPath $partialPath -Destination $modelPath -Force
    Write-Output "Verified offline speech model: $modelPath"
} finally { if (Test-Path -LiteralPath $partialPath) { Remove-Item -LiteralPath $partialPath } }
