param([int]$Port=8080)
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
$goExe=Join-Path $projectRoot '.tools\go\bin\go.exe'
if (!(Test-Path -LiteralPath $goExe)) {
    $command=Get-Command go -ErrorAction SilentlyContinue
    if (!$command) { throw 'Go not found. Install Go 1.26 or later from https://go.dev/dl/.' }
    $goExe=$command.Source
}
New-Item -ItemType Directory -Force (Join-Path $projectRoot 'bin') | Out-Null
$previousProxy=$env:GOPROXY
if (!$env:GOPROXY) { $env:GOPROXY='https://goproxy.cn,https://proxy.golang.org,direct' }
Push-Location (Join-Path $projectRoot 'server')
try {
    & $goExe build -o (Join-Path $projectRoot 'bin\inspection-server.exe') ./cmd/server
    if ($LASTEXITCODE -ne 0) { throw 'Go build failed.' }
    Write-Host "Open http://localhost:$Port to test. Press Ctrl+C to stop."
    & (Join-Path $projectRoot 'bin\inspection-server.exe') -addr "127.0.0.1:$Port" -db (Join-Path $projectRoot 'data\inspection.db') -origins "http://localhost:5173,http://127.0.0.1:5173,http://localhost:$Port,http://127.0.0.1:$Port"
} finally { Pop-Location; $env:GOPROXY=$previousProxy }
