$ErrorActionPreference='Stop'
$root=$PSScriptRoot
Push-Location (Join-Path $root 'admin-web')
try {
    if(!(Get-Command node -ErrorAction SilentlyContinue)){throw 'Node.js 22.12+ or 24 LTS is required.'}
    if(!(Test-Path -LiteralPath 'node_modules')) {
        & npm.cmd ci
        if($LASTEXITCODE -ne 0){throw 'npm ci failed. Check the network or npm proxy configuration.'}
    }
    Write-Host 'Open http://localhost:5173 . Keep the Go backend running on port 8080.'
    & npm.cmd run dev
} finally {Pop-Location}
