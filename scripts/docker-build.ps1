# Build entire solution inside Docker (bypasses host NuGet timeouts)
param(
    [ValidateSet('restore', 'build', 'test', 'all')]
    [string]$Target = 'all'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

function Invoke-DockerBuild([string]$dockerTarget) {
    Write-Host ">>> docker build --target $dockerTarget" -ForegroundColor Cyan
    docker build -f docker/Dockerfile.build --target $dockerTarget -t "didibood:$dockerTarget" .
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

switch ($Target) {
    'restore' { Invoke-DockerBuild 'restore' }
    'build'   { Invoke-DockerBuild 'build' }
    'test'    { Invoke-DockerBuild 'test' }
    'all'     {
        Invoke-DockerBuild 'build'
        Invoke-DockerBuild 'test'
    }
}

Write-Host "Done." -ForegroundColor Green
